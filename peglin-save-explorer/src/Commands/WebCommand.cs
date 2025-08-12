using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;
using peglin_save_explorer.Data;
using System.Diagnostics;

namespace peglin_save_explorer.Commands
{
    public class WebCommand : ICommand
    {
        private Process? _viteProcess;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public Command CreateCommand()
        {
            var command = new Command("web", "Start web server for interactive analysis");

            var portOption = new Option<int>(
                new[] { "--port", "-p" },
                description: "Port to run the web server on",
                getDefaultValue: () => 5000);

            var saveFileOption = new Option<FileInfo?>(
                new[] { "--file", "-f" },
                description: "Path to the Peglin save file to load initially");

            command.AddOption(portOption);
            command.AddOption(saveFileOption);

            command.SetHandler(async (int port, FileInfo? saveFile) =>
            {
                await StartWebServer(port, saveFile);
            }, portOption, saveFileOption);

            return command;
        }

        private async Task StartWebServer(int port, FileInfo? saveFile)
        {
            // Check if port is already in use
            await CheckAndHandlePortConflict(port);
            
            var builder = WebApplication.CreateBuilder();

            // Detect if we're in development mode based on build configuration
            #if DEBUG
            var isDevelopment = true;
            #else
            var isDevelopment = false;
            #endif
            
            if (isDevelopment)
            {
                Console.WriteLine("Development mode detected - enhanced error reporting enabled");
                await StartViteDevServer();
            }

            // Handle graceful shutdown
            AppDomain.CurrentDomain.ProcessExit += (s, e) => StopViteDevServer();
            Console.CancelKeyPress += (s, e) => { StopViteDevServer(); e.Cancel = false; };

            // Add services
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Configure hosting
            builder.WebHost.UseUrls($"http://localhost:{port}");

            var app = builder.Build();

            // Configure pipeline
            app.UseCors();

            // Initialize all data and services before setting up middlewares
            var (configManager, analysisService, currentData, spriteCacheDirectory) = await InitializeDataAndServices(saveFile);
            
            // Configure static file serving for sprite cache
            if (Directory.Exists(spriteCacheDirectory))
            {
                var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                contentTypeProvider.Mappings[".png"] = "image/png";
                contentTypeProvider.Mappings[".jpg"] = "image/jpeg";
                contentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
                contentTypeProvider.Mappings[".gif"] = "image/gif";
                contentTypeProvider.Mappings[".webp"] = "image/webp";
                
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(spriteCacheDirectory),
                    RequestPath = "/sprites",
                    ContentTypeProvider = contentTypeProvider
                });
                Console.WriteLine($"Serving sprites from: {spriteCacheDirectory} at /sprites");
            }
            else
            {
                Console.WriteLine($"Note: Sprite cache directory not found: {spriteCacheDirectory}");
                Console.WriteLine("Run 'extract' command to generate sprite cache first.");
            }

            // In development mode, proxy frontend requests to Vite dev server
            if (isDevelopment && await IsViteDevServerRunning())
            {
                Console.WriteLine("Development mode: proxying frontend requests to Vite dev server at http://localhost:3000");

                // Proxy frontend requests to Vite dev server, but exclude API and sprites
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path.StartsWithSegments("/api") ||
                        context.Request.Path.StartsWithSegments("/sprites"))
                    {
                        await next(context);
                        return;
                    }

                    var viteUrl = $"http://localhost:3000{context.Request.Path}{context.Request.QueryString}";
                    using var httpClient = new HttpClient();

                    try
                    {
                        var response = await httpClient.GetAsync(viteUrl);
                        context.Response.StatusCode = (int)response.StatusCode;

                        foreach (var header in response.Headers)
                        {
                            context.Response.Headers[header.Key] = header.Value.ToArray();
                        }
                        foreach (var header in response.Content.Headers)
                        {
                            context.Response.Headers[header.Key] = header.Value.ToArray();
                        }

                        await response.Content.CopyToAsync(context.Response.Body);
                    }
                    catch
                    {
                        context.Response.StatusCode = 502;
                        await context.Response.WriteAsync("Vite dev server not available");
                    }
                });
            }
            else
            {
                // Serve static files from the built frontend (production mode or dev fallback)
                var frontendPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");

                // In development mode, try to serve from the dev build first
                if (isDevelopment)
                {
                    var devFrontendPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web-frontend", "dist");
                    if (Directory.Exists(devFrontendPath))
                    {
                        frontendPath = devFrontendPath;
                        Console.WriteLine($"Development mode fallback: serving frontend from {devFrontendPath}");
                    }
                }

                if (Directory.Exists(frontendPath))
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(frontendPath),
                        RequestPath = ""
                    });
                    Console.WriteLine($"Serving static files from: {frontendPath}");
                }
                else
                {
                    Console.WriteLine($"Warning: Frontend path not found: {frontendPath}");
                }
            }


            // API Routes with enhanced error handling
            app.MapGet("/api/health", () => 
            {
                try 
                {
                    return CreateApiResponse(new { status = "healthy", timestamp = DateTime.UtcNow });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Health check error: {ex}");
                    return Results.Problem($"Health check failed: {ex.Message}");
                }
            });

            app.MapGet("/api/runs", () => 
            {
                try 
                {
                    return CreateApiResponse(currentData);
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get runs error: {ex}");
                    return Results.Problem($"Failed to get runs: {ex.Message}");
                }
            });

            app.MapGet("/api/runs/filtered", (HttpContext context) =>
            {
                try
                {
                    var filter = CreateFilterFromQuery(context.Request.Query);
                    var filteredRuns = analysisService.FilterRuns(currentData.Runs, filter);
                    return CreateApiResponse(new { runs = filteredRuns, totalCount = filteredRuns.Count });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Filter runs error: {ex}");
                    return Results.Problem($"Failed to filter runs: {ex.Message}");
                }
            });

            app.MapGet("/api/runs/{id}", (string id) =>
            {
                try
                {
                    var run = currentData.Runs.FirstOrDefault(r => r.Id == id);
                    if (run == null)
                    {
                        return Results.NotFound(CreateApiResponse<object>(null, "Run not found"));
                    }
                    return Results.Ok(CreateApiResponse(run));
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get run by ID error: {ex}");
                    return Results.Problem($"Failed to get run: {ex.Message}");
                }
            });

            app.MapGet("/api/statistics", () =>
            {
                try
                {
                    return CreateApiResponse(new
                    {
                        classStatistics = currentData.ClassStatistics,
                        orbStatistics = currentData.OrbStatistics,
                        playerStatistics = currentData.PlayerStatistics,
                        summary = new
                        {
                            totalRuns = currentData.TotalRuns,
                            totalWins = currentData.TotalWins,
                            winRate = currentData.WinRate
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get statistics error: {ex}");
                    return Results.Problem($"Failed to get statistics: {ex.Message}");
                }
            });

            app.MapGet("/api/summary", () =>
            {
                if (currentData.Runs.Count == 0)
                {
                    return CreateApiResponse(new { message = "No data loaded" });
                }

                var basicStats = new
                {
                    totalRuns = currentData.TotalRuns,
                    totalWins = currentData.TotalWins,
                    winRate = Math.Round(currentData.WinRate * 100, 2),
                    averageDamage = currentData.Runs.Count > 0 ? Math.Round(currentData.Runs.Average(r => (double)r.DamageDealt), 0) : 0,
                    averageDuration = currentData.Runs.Count > 0 ? TimeSpan.FromSeconds(currentData.Runs.Average(r => r.Duration.TotalSeconds)) : TimeSpan.Zero,
                    topClasses = currentData.ClassStatistics.OrderByDescending(c => c.Value.WinRate).Take(3).ToDictionary(c => c.Key, c => c.Value)
                };

                return CreateApiResponse(basicStats);
            });

            app.MapPost("/api/load", async (HttpContext context) =>
            {
                try
                {
                    var form = await context.Request.ReadFormAsync();
                    var uploadedFile = form.Files["saveFile"];
                    
                    if (uploadedFile == null || uploadedFile.Length == 0)
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, "No file uploaded"));
                    }

                    // Save uploaded file temporarily
                    var tempPath = Path.GetTempFileName();
                    using (var stream = new FileStream(tempPath, FileMode.Create))
                    {
                        await uploadedFile.CopyToAsync(stream);
                    }

                    // Load the data
                    var fileInfo = new FileInfo(tempPath);
                    currentData = analysisService.LoadCompleteRunData(fileInfo);

                    // Clean up temp file
                    File.Delete(tempPath);

                    Logger.Info($"Loaded {currentData.TotalRuns} runs from uploaded file");
                    return Results.Ok(CreateApiResponse(new
                    {
                        message = $"Successfully loaded {currentData.TotalRuns} runs",
                        totalRuns = currentData.TotalRuns,
                        totalWins = currentData.TotalWins,
                        winRate = currentData.WinRate
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading save file: {ex.Message}");
                    return Results.BadRequest(CreateApiResponse<object>(null, ex.Message));
                }
            });

            app.MapPost("/api/update-cruciball", async (HttpContext context) =>
            {
                try
                {
                    var requestBody = await context.Request.ReadFromJsonAsync<UpdateCruciballRequest>();
                    if (requestBody == null || string.IsNullOrEmpty(requestBody.CharacterClass))
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, "Invalid request data"));
                    }

                    if (requestBody.CruciballLevel < -1 || requestBody.CruciballLevel > 20)
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, "Cruciball level must be between -1 (locked) and 20"));
                    }

                    // Update the save file with the new cruciball level
                    bool success = SaveDataLoader.UpdateCruciballLevel(requestBody.CharacterClass, requestBody.CruciballLevel);
                    
                    if (success)
                    {
                        Logger.Info($"Updated {requestBody.CharacterClass} cruciball level to {requestBody.CruciballLevel}");
                        
                        return Results.Ok(CreateApiResponse(new
                        {
                            message = $"Cruciball level for {requestBody.CharacterClass} updated to {requestBody.CruciballLevel}",
                            characterClass = requestBody.CharacterClass,
                            newLevel = requestBody.CruciballLevel,
                            note = "Changes are in-memory only - binary save file writing requires proper Unity serialization context"
                        }));
                    }
                    else
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Failed to update cruciball level for {requestBody.CharacterClass}"));
                    }
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Update cruciball error: {ex}");
                    return Results.BadRequest(CreateApiResponse<object>(null, "Failed to update cruciball level"));
                }
            });

            app.MapGet("/api/export", (string format = "json") =>
            {
                try
                {
                    if (format.ToLower() == "csv")
                    {
                        var csv = analysisService.ExportToCsv(currentData.Runs);
                        return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "runs.csv");
                    }
                    else
                    {
                        var json = JsonSerializer.Serialize(currentData, new JsonSerializerOptions { WriteIndented = true });
                        return Results.File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "runs.json");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error exporting data: {ex.Message}");
                    return Results.BadRequest(CreateApiResponse<object>(null, ex.Message));
                }
            });

            app.MapGet("/api/classes", () =>
            {
                var classes = currentData.ClassStatistics.Keys.ToList();
                return CreateApiResponse(classes);
            });

            app.MapGet("/api/classes/{className}/stats", (string className) =>
            {
                if (currentData.ClassStatistics.TryGetValue(className, out var stats))
                {
                    return CreateApiResponse(stats);
                }
                return Results.NotFound(CreateApiResponse<object>(null, "Class not found"));
            });

            // Class management endpoints using the consolidated ClassManagementService
            app.MapGet("/api/classes/status", () =>
            {
                try
                {
                    var classService = new ClassManagementService();
                    var classes = classService.ListClasses();
                    return CreateApiResponse(classes);
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get class status error: {ex}");
                    return Results.Problem($"Failed to get class status: {ex.Message}");
                }
            });

            app.MapPost("/api/classes/{className}/unlock", (string className) =>
            {
                try
                {
                    var classService = new ClassManagementService();
                    bool success = classService.UnlockClass(className);
                    
                    if (success)
                    {
                        return Results.Ok(CreateApiResponse(new
                        {
                            message = $"Successfully unlocked class: {className}",
                            className = className,
                            action = "unlock"
                        }));
                    }
                    else
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Failed to unlock class: {className}"));
                    }
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Unlock class error: {ex}");
                    return Results.Problem($"Failed to unlock class: {ex.Message}");
                }
            });

            app.MapPost("/api/classes/{className}/lock", (string className) =>
            {
                try
                {
                    var classService = new ClassManagementService();
                    bool success = classService.LockClass(className);
                    
                    if (success)
                    {
                        return Results.Ok(CreateApiResponse(new
                        {
                            message = $"Successfully locked class: {className}",
                            className = className,
                            action = "lock"
                        }));
                    }
                    else
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Failed to lock class: {className}"));
                    }
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Lock class error: {ex}");
                    return Results.Problem($"Failed to lock class: {ex.Message}");
                }
            });

            app.MapPost("/api/classes/{className}/cruciball", async (HttpContext context, string className) =>
            {
                try
                {
                    var requestBody = await context.Request.ReadFromJsonAsync<SetCruciballRequest>();
                    if (requestBody == null)
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, "Invalid request data"));
                    }

                    if (requestBody.Level < 0 || requestBody.Level > 20)
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, "Cruciball level must be between 0 and 20"));
                    }

                    var classService = new ClassManagementService();
                    bool success = classService.SetCruciballLevel(className, requestBody.Level);
                    
                    if (success)
                    {
                        return Results.Ok(CreateApiResponse(new
                        {
                            message = $"Successfully set {className} cruciball level to {requestBody.Level}",
                            className = className,
                            newLevel = requestBody.Level,
                            action = "set_cruciball"
                        }));
                    }
                    else
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Failed to set cruciball level for {className}"));
                    }
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Set cruciball error: {ex}");
                    return Results.Problem($"Failed to set cruciball level: {ex.Message}");
                }
            });

            // Fallback to serve the React app for any non-API routes (only when not proxying to Vite)
            if (!isDevelopment || !await IsViteDevServerRunning())
            {
                app.MapFallback((HttpContext context) =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        return Results.NotFound();
                    }

                    var frontendPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                    if (isDevelopment)
                    {
                        var devFrontendPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web-frontend", "dist");
                        if (Directory.Exists(devFrontendPath))
                        {
                            frontendPath = devFrontendPath;
                        }
                    }

                    var indexPath = Path.Combine(frontendPath, "index.html");
                    if (File.Exists(indexPath))
                    {
                        return Results.File(indexPath, "text/html");
                    }

                    if (isDevelopment)
                    {
                        return Results.NotFound($"Frontend not built. Frontend path: {frontendPath}. Run 'npm run build' in the web-frontend directory or start the Vite dev server.");
                    }
                    return Results.NotFound("Frontend not built. Run the build process first.");
                });
            }

            Console.WriteLine($"Starting web server on http://localhost:{port}");
            
            // Open the web browser after server startup
            var serverUrl = $"http://localhost:{port}";
            _ = Task.Run(async () =>
            {
                // Wait a moment for the server to fully start
                await Task.Delay(2000);
                try
                {
                    OpenBrowser(serverUrl);
                    Console.WriteLine($"🌐 Opened web browser to {serverUrl}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not open web browser automatically: {ex.Message}");
                    Console.WriteLine($"Please open {serverUrl} in your web browser manually.");
                }
            });
            if (currentData.TotalRuns > 0)
            {
                Console.WriteLine($"Loaded {currentData.TotalRuns} runs, {currentData.TotalWins} wins ({currentData.WinRate:P1} win rate)");
            }
            else
            {
                Console.WriteLine("No initial data loaded. Upload a save file via the web interface.");
            }

            // Sprite API endpoints (now serving sprite files directly via static file middleware at /sprites)
            app.MapGet("/api/sprites", () =>
            {
                try
                {
                    var allSprites = Data.SpriteCacheManager.GetCachedSprites();
                    return CreateApiResponse(new
                    {
                        sprites = allSprites.Select(s => new
                        {
                            id = s.Id,
                            name = s.Name,
                            type = s.Type.ToString().ToLower(),
                            width = s.Width,
                            height = s.Height,
                            frameWidth = s.FrameWidth,
                            frameHeight = s.FrameHeight,
                            frameX = s.FrameX,
                            frameY = s.FrameY,
                            extractedAt = s.ExtractedAt,
                            url = $"/sprites/{GetSpriteTypeDirectory(s.Type)}/{s.Id}.png", // Direct static file URL
                            isAtlas = s.IsAtlas,
                            frameCount = s.AtlasFrames?.Count ?? 0
                        }),
                        total = allSprites.Count,
                        relicCount = allSprites.Count(s => s.Type == Data.SpriteCacheManager.SpriteType.Relic),
                        enemyCount = allSprites.Count(s => s.Type == Data.SpriteCacheManager.SpriteType.Enemy),
                        orbCount = allSprites.Count(s => s.Type == Data.SpriteCacheManager.SpriteType.Orb),
                        atlasCount = allSprites.Count(s => s.IsAtlas)
                    });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get sprites error: {ex}");
                    return Results.Problem($"Failed to get sprites: {ex.Message}");
                }
            });

            app.MapGet("/api/sprites/{type}", (string type) =>
            {
                try
                {
                    if (!Enum.TryParse<Data.SpriteCacheManager.SpriteType>(type, true, out var spriteType))
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Invalid sprite type: {type}. Valid types are: relic, enemy, orb"));
                    }

                    var sprites = Data.SpriteCacheManager.GetCachedSprites(spriteType);
                    return CreateApiResponse(sprites.Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        type = s.Type.ToString().ToLower(),
                        width = s.Width,
                        height = s.Height,
                        frameWidth = s.FrameWidth,
                        frameHeight = s.FrameHeight,
                        frameX = s.FrameX,
                        frameY = s.FrameY,
                        extractedAt = s.ExtractedAt,
                        url = $"/sprites/{GetSpriteTypeDirectory(s.Type)}/{s.Id}.png", // Direct static file URL
                        isAtlas = s.IsAtlas,
                        frameCount = s.AtlasFrames?.Count ?? 0,
                        // Include frame references for atlases
                        frames = s.IsAtlas ? s.AtlasFrames?.Select(f => new
                        {
                            name = f.Name,
                            x = f.X,
                            y = f.Y,
                            width = f.Width,
                            height = f.Height,
                            pivotX = f.PivotX,
                            pivotY = f.PivotY
                        }) : null
                    }));
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get sprites by type error: {ex}");
                    return Results.Problem($"Failed to get sprites: {ex.Message}");
                }
            });

            // Sprite file serving is now handled by static file middleware at /sprites
            // Individual sprite metadata API endpoint with full atlas frame information
            app.MapGet("/api/sprites/{type}/{id}/metadata", (string type, string id) =>
            {
                try
                {
                    if (!Enum.TryParse<Data.SpriteCacheManager.SpriteType>(type, true, out var spriteType))
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Invalid sprite type: {type}. Valid types are: relic, enemy, orb"));
                    }

                    var sprite = Data.SpriteCacheManager.GetSpriteMetadata(spriteType, id);
                    if (sprite == null)
                    {
                        return Results.NotFound(CreateApiResponse<object>(null, $"Sprite not found: {type}/{id}"));
                    }

                    return CreateApiResponse(new
                    {
                        id = sprite.Id,
                        name = sprite.Name,
                        type = sprite.Type.ToString().ToLower(),
                        width = sprite.Width,
                        height = sprite.Height,
                        extractedAt = sprite.ExtractedAt,
                        sourceBundle = sprite.SourceBundle,
                        url = $"/sprites/{GetSpriteTypeDirectory(sprite.Type)}/{sprite.Id}.png", // Direct static file URL
                        exists = File.Exists(sprite.FilePath),
                        isAtlas = sprite.IsAtlas,
                        frames = sprite.IsAtlas ? sprite.AtlasFrames?.Select(f => new
                        {
                            name = f.Name,
                            x = f.X,
                            y = f.Y,
                            width = f.Width,
                            height = f.Height,
                            pivotX = f.PivotX,
                            pivotY = f.PivotY,
                            spritePathID = f.SpritePathID
                        }) : null
                    });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get sprite metadata error: {ex}");
                    return Results.Problem($"Failed to get sprite metadata: {ex.Message}");
                }
            });

            // Get atlas frames for a specific sprite (useful for animations)
            app.MapGet("/api/sprites/{type}/{id}/frames", (string type, string id) =>
            {
                try
                {
                    if (!Enum.TryParse<Data.SpriteCacheManager.SpriteType>(type, true, out var spriteType))
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Invalid sprite type: {type}. Valid types are: relic, enemy, orb"));
                    }

                    var sprite = Data.SpriteCacheManager.GetSpriteMetadata(spriteType, id);
                    if (sprite == null)
                    {
                        return Results.NotFound(CreateApiResponse<object>(null, $"Sprite not found: {type}/{id}"));
                    }

                    if (!sprite.IsAtlas)
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, $"Sprite {id} is not an atlas and has no frame data"));
                    }

                    return CreateApiResponse(new
                    {
                        atlasId = sprite.Id,
                        atlasName = sprite.Name,
                        atlasUrl = $"/sprites/{GetSpriteTypeDirectory(sprite.Type)}/{sprite.Id}.png",
                        atlasWidth = sprite.Width,
                        atlasHeight = sprite.Height,
                        frameCount = sprite.AtlasFrames?.Count ?? 0,
                        frames = sprite.AtlasFrames?.Select(f => new
                        {
                            name = f.Name,
                            x = f.X,
                            y = f.Y,
                            width = f.Width,
                            height = f.Height,
                            pivotX = f.PivotX,
                            pivotY = f.PivotY,
                            // Calculate normalized coordinates for CSS sprites
                            normalizedX = sprite.Width > 0 ? (float)f.X / sprite.Width : 0f,
                            normalizedY = sprite.Height > 0 ? (float)f.Y / sprite.Height : 0f,
                            normalizedWidth = sprite.Width > 0 ? (float)f.Width / sprite.Width : 0f,
                            normalizedHeight = sprite.Height > 0 ? (float)f.Height / sprite.Height : 0f
                        }).OrderBy(f => f.name).ToArray() ?? Array.Empty<object>()
                    });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get sprite frames error: {ex}");
                    return Results.Problem($"Failed to get sprite frames: {ex.Message}");
                }
            });

            app.MapGet("/api/sprites/cache/status", () =>
            {
                try
                {
                    var allSprites = Data.SpriteCacheManager.GetCachedSprites();
                    var cacheDir = Data.SpriteCacheManager.GetSpriteCacheDirectory();
                    
                    return CreateApiResponse(new
                    {
                        cacheDirectory = cacheDir,
                        cacheExists = Directory.Exists(cacheDir),
                        totalSprites = allSprites.Count,
                        relicSprites = allSprites.Count(s => s.Type == Data.SpriteCacheManager.SpriteType.Relic),
                        enemySprites = allSprites.Count(s => s.Type == Data.SpriteCacheManager.SpriteType.Enemy),
                        orbSprites = allSprites.Count(s => s.Type == Data.SpriteCacheManager.SpriteType.Orb),
                        atlasCount = allSprites.Count(s => s.IsAtlas),
                        lastUpdated = allSprites.Any() ? allSprites.Max(s => s.ExtractedAt) : (DateTime?)null
                    });
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get sprite cache status error: {ex}");
                    return Results.Problem($"Failed to get sprite cache status: {ex.Message}");
                }
            });


            // Entity-sprite association API endpoint (loads all data at once for frontend)
            app.MapGet("/api/entities", () =>
            {
                try
                {
                    // Clear sprite cache to ensure fresh data on each request
                    // (could be optimized further with cache invalidation logic)
                    _spriteIdCache = null;
                    _spriteNameCache = null;

                    var entities = new
                    {
                        relics = GetAllRelics(),
                        sprites = GetAllSprites(),
                        enemies = GetAllEnemies(),
                        orbs = GetAllOrbs()
                    };

                    return CreateApiResponse(entities);
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get entities error: {ex}");
                    return Results.Problem($"Failed to get entities: {ex.Message}");
                }
            });

            Console.WriteLine("Press Ctrl+C to stop the server");

            await app.RunAsync();
        }

        private RunFilter CreateFilterFromQuery(IQueryCollection query)
        {
            var filter = new RunFilter();

            if (query.ContainsKey("characterClass") && !string.IsNullOrEmpty(query["characterClass"]))
            {
                filter.CharacterClass = query["characterClass"];
            }

            if (query.ContainsKey("won") && bool.TryParse(query["won"], out bool won))
            {
                filter.Won = won;
            }

            if (query.ContainsKey("startDate") && DateTime.TryParse(query["startDate"], out DateTime startDate))
            {
                filter.StartDate = startDate;
            }

            if (query.ContainsKey("endDate") && DateTime.TryParse(query["endDate"], out DateTime endDate))
            {
                filter.EndDate = endDate;
            }

            if (query.ContainsKey("minDamage") && long.TryParse(query["minDamage"], out long minDamage))
            {
                filter.MinDamage = minDamage;
            }

            if (query.ContainsKey("maxDamage") && long.TryParse(query["maxDamage"], out long maxDamage))
            {
                filter.MaxDamage = maxDamage;
            }

            if (query.ContainsKey("minDuration") && double.TryParse(query["minDuration"], out double minDurationMinutes))
            {
                filter.MinDuration = TimeSpan.FromMinutes(minDurationMinutes);
            }

            if (query.ContainsKey("maxDuration") && double.TryParse(query["maxDuration"], out double maxDurationMinutes))
            {
                filter.MaxDuration = TimeSpan.FromMinutes(maxDurationMinutes);
            }

            return filter;
        }

        private object CreateApiResponse<T>(T? data, string? error = null)
        {
            return new
            {
                success = error == null,
                data = error == null ? data : default(T),
                error = error
            };
        }

        private object CreateApiResponse(RunHistoryData data)
        {
            return CreateApiResponse<RunHistoryData>(data);
        }

        private object CreateApiResponse(RunRecord data)
        {
            return CreateApiResponse<RunRecord>(data);
        }

        private object CreateApiResponse(List<RunRecord> data)
        {
            return CreateApiResponse<List<RunRecord>>(data);
        }

        private object CreateApiResponse(object data)
        {
            return CreateApiResponse<object>(data);
        }

        private async Task StartViteDevServer()
        {
            try
            {
                Console.WriteLine("Starting Vite dev server...");
                
                var frontendPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "web-frontend");
                var absoluteFrontendPath = Path.GetFullPath(frontendPath);
                
                if (!Directory.Exists(absoluteFrontendPath))
                {
                    Console.WriteLine($"Warning: Frontend directory not found at {absoluteFrontendPath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "run dev",
                    WorkingDirectory = absoluteFrontendPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _viteProcess = new Process { StartInfo = startInfo,  };
                
                // _viteProcess.OutputDataReceived += (sender, e) =>
                // {
                //     if (!string.IsNullOrEmpty(e.Data))
                //     {
                //         Console.WriteLine($"[Vite] {e.Data}");
                //     }
                // };
                
                // _viteProcess.ErrorDataReceived += (sender, e) =>
                // {
                //     if (!string.IsNullOrEmpty(e.Data))
                //     {
                //         Console.WriteLine($"[Vite Error] {e.Data}");
                //     }
                // };

                _viteProcess.Start();
                _viteProcess.BeginOutputReadLine();
                _viteProcess.BeginErrorReadLine();

                // Wait a moment for the server to start
                await Task.Delay(3000);
                
                if (await IsViteDevServerRunning())
                {
                    Console.WriteLine("Vite dev server started successfully at http://localhost:3000");
                }
                else
                {
                    Console.WriteLine("Warning: Vite dev server may not have started properly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start Vite dev server: {ex.Message}");
            }
        }

        private void StopViteDevServer()
        {
            try
            {
                if (_viteProcess != null && !_viteProcess.HasExited)
                {
                    Console.WriteLine("Stopping Vite dev server...");
                    _viteProcess.Kill(true); // Kill the process tree
                    _viteProcess.WaitForExit(5000);
                    _viteProcess.Dispose();
                    _viteProcess = null;
                    Console.WriteLine("Vite dev server stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping Vite dev server: {ex.Message}");
            }
        }

        private async Task<bool> IsViteDevServerRunning()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(2);
                var response = await httpClient.GetAsync("http://localhost:3000");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static string GetSpriteTypeDirectory(SpriteCacheManager.SpriteType type)
        {
            return type switch
            {
                SpriteCacheManager.SpriteType.Enemy => "enemies",
                SpriteCacheManager.SpriteType.Relic => "relics",
                _ => type.ToString().ToLower() + "s"
            };
        }


        /// <summary>
        /// Gets sprite reference using correlation data instead of parsing raw data
        /// </summary>
        // Cache for sprite metadata lookups - initialized once per request
        private static Dictionary<string, Data.SpriteCacheManager.SpriteMetadata>? _spriteIdCache;
        private static Dictionary<string, Data.SpriteCacheManager.SpriteMetadata>? _spriteNameCache;

        private static void EnsureSpriteCache()
        {
            if (_spriteIdCache == null || _spriteNameCache == null)
            {
                var allSprites = Data.SpriteCacheManager.GetCachedSprites();
                _spriteIdCache = new Dictionary<string, Data.SpriteCacheManager.SpriteMetadata>();
                _spriteNameCache = new Dictionary<string, Data.SpriteCacheManager.SpriteMetadata>();

                foreach (var sprite in allSprites)
                {
                    if (sprite.Width > 0 && sprite.Height > 0)
                    {
                        _spriteIdCache.TryAdd(sprite.Id, sprite);
                        _spriteNameCache.TryAdd(sprite.Name, sprite);
                    }
                }
            }
        }

        private static object? GetCorrelatedSpriteReference<T>(T entity) where T : class
        {
            try
            {
                // Cache reflection - get properties once per type
                var entityType = typeof(T);
                var correlatedSpriteIdProperty = entityType.GetProperty("CorrelatedSpriteId");
                var spriteFilePathProperty = entityType.GetProperty("SpriteFilePath");
                var correlationMethodProperty = entityType.GetProperty("CorrelationMethod");
                
                var spriteFilePath = spriteFilePathProperty?.GetValue(entity) as string;
                var correlatedSpriteId = correlatedSpriteIdProperty?.GetValue(entity) as string;
                
                if (!string.IsNullOrEmpty(spriteFilePath))
                {
                    // Extract filename from relative path and determine sprite type
                    var filename = Path.GetFileName(spriteFilePath);
                    var correlationMethod = correlationMethodProperty?.GetValue(entity) as string;
                    
                    // Determine sprite type from path (optimized with spans)
                    string spriteType = "unknown";
                    string spriteDirectory = "sprites";
                    
                    if (spriteFilePath.Contains("/orbs/"))
                    {
                        spriteType = "orb";
                        spriteDirectory = "orbs";
                    }
                    else if (spriteFilePath.Contains("/relics/"))
                    {
                        spriteType = "relic";
                        spriteDirectory = "relics";
                    }
                    else if (spriteFilePath.Contains("/enemies/"))
                    {
                        spriteType = "enemy";
                        spriteDirectory = "enemies";
                    }
                    
                    // Ensure sprite cache is initialized
                    EnsureSpriteCache();
                    
                    // Fast dictionary lookup instead of linear search
                    Data.SpriteCacheManager.SpriteMetadata? spriteMetadata = null;
                    
                    if (!string.IsNullOrEmpty(correlatedSpriteId))
                    {
                        _spriteIdCache!.TryGetValue(correlatedSpriteId, out spriteMetadata);
                    }
                    
                    // If no valid metadata found by ID, try by name/filename
                    if (spriteMetadata == null)
                    {
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                        _spriteNameCache!.TryGetValue(nameWithoutExtension, out spriteMetadata);
                        if (spriteMetadata == null)
                        {
                            _spriteNameCache.TryGetValue(filename, out spriteMetadata);
                        }
                    }
                    
                    // Default dimensions if no metadata found - use reasonable defaults for pixel art
                    int width = spriteMetadata?.Width ?? 16;
                    int height = spriteMetadata?.Height ?? 16;
                    int frameWidth = spriteMetadata?.FrameWidth ?? width;
                    int frameHeight = spriteMetadata?.FrameHeight ?? height;
                    
                    return new
                    {
                        id = correlatedSpriteId ?? filename,
                        name = filename,
                        type = spriteType,
                        url = $"/sprites/{spriteDirectory}/{filename}",
                        width = width,
                        height = height,
                        frameWidth = frameWidth,
                        frameHeight = frameHeight,
                        frameX = spriteMetadata?.FrameX ?? 0,
                        frameY = spriteMetadata?.FrameY ?? 0,
                        resolved = true,
                        correlationMethod = correlationMethod ?? "SpriteFilePath"
                    };
                }
                else if (!string.IsNullOrEmpty(correlatedSpriteId))
                {
                    // Fallback to sprite metadata lookup if no file path available
                    var spriteMetadata = Data.SpriteCacheManager.GetCachedSprites()
                        .FirstOrDefault(s => s.Id == correlatedSpriteId);
                        
                    if (spriteMetadata != null)
                    {
                        var correlationMethod = correlationMethodProperty?.GetValue(entity) as string;
                        var filename = Path.GetFileName(spriteMetadata.FilePath);
                        
                        return new
                        {
                            id = correlatedSpriteId,
                            name = spriteMetadata.Name,
                            type = spriteMetadata.Type.ToString().ToLowerInvariant(),
                            url = $"/sprites/{GetSpriteTypeDirectory(spriteMetadata.Type)}/{filename}",
                            width = spriteMetadata.Width,
                            height = spriteMetadata.Height,
                            frameWidth = spriteMetadata.FrameWidth,
                            frameHeight = spriteMetadata.FrameHeight,
                            frameX = spriteMetadata.FrameX,
                            frameY = spriteMetadata.FrameY,
                            resolved = true,
                            correlationMethod = correlationMethod ?? "Sprite Metadata Lookup"
                        };
                    }
                    else
                    {
                        // Sprite ID found but metadata missing - return unresolved info
                        return new
                        {
                            id = correlatedSpriteId,
                            resolved = false,
                            reason = "Sprite metadata not found in cache",
                            filePath = spriteFilePath
                        };
                    }
                }
                
                // Fall back to old method if no correlation data
                var rawDataProperty = typeof(T).GetProperty("RawData");
                if (rawDataProperty != null)
                {
                    var rawData = rawDataProperty.GetValue(entity);
                    if (rawData != null)
                    {
                        return GetSpriteReferenceFromRawData(rawData);
                    }
                }
            }
            catch (Exception ex)
            {
                // Return error info for debugging
                return new { resolved = false, error = ex.Message };
            }
            return null;
        }

        private static object? GetSpriteReferenceFromRawData(object rawData)
        {
            try
            {
                var rawDataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(rawData.ToString()!);
                if (rawDataDict != null && rawDataDict.TryGetValue("sprite", out var spriteRef))
                {
                    // Handle both old string format and new detailed object format
                    if (spriteRef is System.Text.Json.JsonElement element)
                    {
                        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            return new { reference = element.GetString(), resolved = false };
                        }
                        else if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            return System.Text.Json.JsonSerializer.Deserialize<object>(element.GetRawText());
                        }
                    }
                    return spriteRef;
                }
            }
            catch (Exception)
            {
                // If parsing fails, return null
            }
            return null;
        }

        private static List<object> GetAllRelics()
        {
            var relics = new List<object>();
            try
            {
                var cachedRelics = Data.EntityCacheManager.GetCachedRelics();
                foreach (var kvp in cachedRelics)
                {
                    relics.Add(new
                    {
                        id = kvp.Key,
                        name = kvp.Value.Name ?? kvp.Key,
                        description = kvp.Value.Description ?? "",
                        effect = kvp.Value.Effect ?? "",
                        type = "relic",
                        rarity = kvp.Value.Rarity ?? "Unknown",
                        spriteReference = GetCorrelatedSpriteReference(kvp.Value)
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading relics for API: {ex.Message}");
            }
            return relics;
        }

        private static List<object> GetAllEnemies()
        {
            var enemies = new List<object>();
            try
            {
                var cachedEnemies = Data.EntityCacheManager.GetCachedEnemies();
                foreach (var kvp in cachedEnemies)
                {
                    enemies.Add(new
                    {
                        id = kvp.Key,
                        name = kvp.Value.Name ?? kvp.Key,
                        description = kvp.Value.Description ?? "",
                        type = "enemy",
                        enemyType = kvp.Value.Type ?? "NORMAL",
                        maxHealth = kvp.Value.MaxHealth?.ToString() ?? "",
                        location = kvp.Value.Location ?? "",
                        spriteReference = GetCorrelatedSpriteReference(kvp.Value)
                    });
                }
            }
            catch (Exception)
            {
                // Return empty list if loading fails
            }
            return enemies;
        }

        private static List<object> GetAllOrbs()
        {
            var orbs = new List<object>();
            try
            {
                // Only use cached data - no expensive fallbacks
                var cachedOrbs = Data.EntityCacheManager.GetCachedOrbs();
                Logger.Info($"/api/entities: loaded {cachedOrbs.Count} orbs from cache");

                // Pre-format the orb data efficiently
                orbs.Capacity = cachedOrbs.Count; // Pre-size list to avoid reallocations

                foreach (var kvp in cachedOrbs)
                {
                    try
                    {
                        // Pre-format description only once
                        string description = kvp.Value.Description ?? "";
                        if (string.IsNullOrEmpty(description) && kvp.Value.DescriptionStrings?.Count > 0)
                        {
                            // Use StringBuilder for efficient concatenation
                            var sb = new System.Text.StringBuilder();
                            for (int i = 0; i < kvp.Value.DescriptionStrings.Count; i++)
                            {
                                if (i > 0) sb.Append('\n');
                                sb.Append('•').Append(kvp.Value.DescriptionStrings[i]);
                            }
                            description = sb.ToString();
                        }

                        orbs.Add(new
                        {
                            id = kvp.Key,
                            name = kvp.Value.Name ?? kvp.Key,
                            description = description,
                            type = "orb",
                            orbType = kvp.Value.OrbType ?? "ATTACK",
                            damagePerPeg = kvp.Value.DamagePerPeg?.ToString() ?? "",
                            rarity = kvp.Value.Rarity ?? kvp.Value.RarityValue?.ToString() ?? "",
                            spriteReference = GetCorrelatedSpriteReference(kvp.Value)
                        });
                    }
                    catch (Exception itemEx)
                    {
                        Logger.Warning($"Skipping orb {kvp.Key} due to error: {itemEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading orbs for API: {ex.Message}");
            }
            return orbs;
        }

        private static List<object> GetAllSprites()
        {
            var sprites = new List<object>();
            try
            {
                var allSprites = Data.SpriteCacheManager.GetCachedSprites();
                sprites.Capacity = allSprites.Count; // Pre-size list

                // Avoid LINQ overhead - use explicit loop
                foreach (var s in allSprites)
                {
                    sprites.Add(new
                    {
                        id = s.Id,
                        name = s.Name,
                        type = s.Type.ToString().ToLower(),
                        width = s.Width,
                        height = s.Height,
                        url = $"/sprites/{GetSpriteTypeDirectory(s.Type)}/{s.Id}.png",
                        isAtlas = s.IsAtlas,
                        frameCount = s.AtlasFrames?.Count ?? 0,
                        extractedAt = s.ExtractedAt,
                        sourceBundle = s.SourceBundle
                    });
                }
            }
            catch (Exception)
            {
                // Return empty list if loading fails
            }
            return sprites;
        }


        private async Task CheckAndHandlePortConflict(int port)
        {
            try
            {
                // Check if port is in use
                if (!IsPortInUse(port))
                {
                    return; // Port is available
                }

                Console.WriteLine($"⚠️  Port {port} is already in use.");
                
                // Try to identify the process using the port
                var processInfo = GetProcessUsingPort(port);
                if (processInfo.HasValue)
                {
                    var (processName, processId) = processInfo.Value;
                    var currentProcessName = Process.GetCurrentProcess().ProcessName;
                    var currentProcessId = Environment.ProcessId;
                    
                    if (processName.Equals(currentProcessName, StringComparison.OrdinalIgnoreCase) && 
                        processId != currentProcessId)
                    {
                        Console.WriteLine($"The port is being used by another instance of {currentProcessName} (PID: {processId}).");
                        Console.Write($"Would you like to kill the existing process and continue? (y/N): ");
                        
                        var response = Console.ReadLine()?.Trim().ToLower();
                        if (response == "y" || response == "yes")
                        {
                            try
                            {
                                var process = Process.GetProcessById(processId);
                                
                                // Try graceful shutdown first (SIGINT/Ctrl+C)
                                if (TryGracefulShutdown(process))
                                {
                                    Console.WriteLine($"✅ Successfully sent termination signal to process {processId}");
                                    
                                    // Wait for graceful shutdown
                                    if (!process.WaitForExit(8000)) // Wait up to 8 seconds for graceful shutdown
                                    {
                                        Console.WriteLine($"⚠️  Process {processId} didn't exit gracefully, forcing termination...");
                                        process.Kill();
                                        process.WaitForExit(2000); // Wait up to 2 more seconds
                                    }
                                }
                                else
                                {
                                    // Fallback to hard kill if graceful shutdown not supported
                                    Console.WriteLine($"⚠️  Graceful shutdown not supported on this platform, using hard termination...");
                                    process.Kill();
                                    process.WaitForExit(5000);
                                }
                                
                                Console.WriteLine($"✅ Process {processId} terminated");
                                
                                // Wait a moment for the port to be released
                                await Task.Delay(1000);
                                
                                if (IsPortInUse(port))
                                {
                                    Console.WriteLine($"❌ Port {port} is still in use after terminating the process.");
                                    Environment.Exit(1);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Failed to terminate process: {ex.Message}");
                                Environment.Exit(1);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Operation cancelled.");
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"The port is being used by {processName} (PID: {processId}).");
                        Console.WriteLine($"Please stop the process or use a different port with --port <port>");
                        Environment.Exit(1);
                    }
                }
                else
                {
                    Console.WriteLine($"Could not identify the process using port {port}.");
                    Console.WriteLine($"Please stop the process or use a different port with --port <port>");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking port availability: {ex.Message}");
                // Continue anyway - the port binding will fail later if there's a real conflict
            }
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                using var tcpListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                tcpListener.Start();
                tcpListener.Stop();
                return false;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return true;
            }
        }

        private static (string ProcessName, int ProcessId)? GetProcessUsingPort(int port)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "netstat" : "lsof",
                    Arguments = OperatingSystem.IsWindows() ? "-ano" : $"-i :{port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (OperatingSystem.IsWindows())
                {
                    return ParseWindowsNetstatOutput(output, port);
                }
                else
                {
                    // Unix-like systems - parse lsof output
                    // Format: COMMAND PID USER FD TYPE DEVICE SIZE/OFF NODE NAME
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.Skip(1)) // Skip header
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out var pid))
                        {
                            try
                            {
                                var proc = Process.GetProcessById(pid);
                                return (proc.ProcessName, pid);
                            }
                            catch
                            {
                                // Process might have exited
                            }
                        }
                    }
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static (string ProcessName, int ProcessId)? ParseWindowsNetstatOutput(string output, int port)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains($":{port} ") && line.Contains("LISTENING"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 4 && int.TryParse(parts[^1], out var pid))
                    {
                        try
                        {
                            var process = Process.GetProcessById(pid);
                            return (process.ProcessName, pid);
                        }
                        catch
                        {
                            // Process might have exited
                        }
                    }
                }
            }
            return null;
        }

        private static bool TryGracefulShutdown(Process process)
        {
            try
            {
                // Check if the process is still running
                if (process.HasExited)
                    return true;

                // Try different approaches based on the platform
                if (OperatingSystem.IsWindows())
                {
                    // On Windows, try to send Ctrl+C signal
                    return TryWindowsGracefulShutdown(process);
                }
                else
                {
                    // On Unix-like systems (macOS, Linux), send SIGTERM first, then SIGINT
                    return TryUnixGracefulShutdown(process);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryWindowsGracefulShutdown(Process process)
        {
            try
            {
                // On Windows, we can try to close the main window first
                if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                {
                    process.CloseMainWindow();
                    return true;
                }
                
                // Fallback: Windows doesn't have a direct equivalent to SIGINT for arbitrary processes
                // We'll return false to indicate graceful shutdown isn't available
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryUnixGracefulShutdown(Process process)
        {
            try
            {
                // Use kill command to send SIGTERM (15) first, which is more graceful than SIGKILL
                var killProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "kill",
                    Arguments = $"-TERM {process.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                
                killProcess?.WaitForExit();
                return killProcess?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Opens the default web browser to the specified URL
        /// </summary>
        private static void OpenBrowser(string url)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Windows
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c start \"\" \"{url}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // macOS
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = url,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Linux
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = url,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else
                {
                    throw new PlatformNotSupportedException("Automatic browser opening is not supported on this platform.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to open browser: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Initializes all data loading, cache extraction, and services before setting up middlewares
        /// </summary>
        private Task<(ConfigurationManager configManager, DataAnalysisService analysisService, RunHistoryData currentData, string spriteCacheDirectory)> InitializeDataAndServices(FileInfo? saveFile)
        {
            // Initialize services
            var configManager = new ConfigurationManager();
            var analysisService = new DataAnalysisService(configManager);

            // Ensure Peglin data is extracted before starting web server
            var peglinPath = configManager.GetEffectivePeglinPath(null, false);
            if (!string.IsNullOrEmpty(peglinPath))
            {
                Console.WriteLine("🔍 Checking Peglin data extraction status...");
                if (Core.PeglinDataExtractor.IsExtractionNeeded(peglinPath))
                {
                    Console.WriteLine("📥 Extracting Peglin data (this may take a moment)...");
                    var extractionResult = Core.PeglinDataExtractor.ExtractPeglinData(peglinPath, Core.PeglinDataExtractor.ExtractionType.All);
                    
                    if (extractionResult.Success)
                    {
                        if (extractionResult.UsedCache)
                        {
                            Console.WriteLine("✅ Peglin data is up to date");
                        }
                        else
                        {
                            var totalItems = extractionResult.ExtractedCounts.Sum(kvp => kvp.Value);
                            Console.WriteLine($"✅ Extracted {totalItems} items in {extractionResult.Duration.TotalSeconds:F1}s");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Data extraction failed: {extractionResult.ErrorMessage}");
                        Console.WriteLine("Web server will start, but some features may not work properly.");
                    }
                }
                else
                {
                    Console.WriteLine("✅ Peglin data is up to date");
                }
            }
            else
            {
                Console.WriteLine("⚠️ No Peglin installation detected. Some features may not work properly.");
                Console.WriteLine("Use 'peglin-save-explorer extract --peglin-path /path/to/peglin' to extract game data.");
            }

            // Load run data from save files
            var currentData = new RunHistoryData();
            FileInfo? effectiveFile = saveFile;
            if (effectiveFile == null)
            {
                // Try to get default save file from configuration
                var defaultPath = configManager.GetEffectiveSaveFilePath();
                if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
                {
                    effectiveFile = new FileInfo(defaultPath);
                    Console.WriteLine($"Loading data from default save file: {defaultPath}");
                }
            }
            
            if (effectiveFile != null && effectiveFile.Exists)
            {
                currentData = analysisService.LoadCompleteRunData(effectiveFile);
            }

            // Get sprite cache directory path
            var spriteCacheDirectory = Data.SpriteCacheManager.GetSpriteCacheDirectory();

            // Log data loading results
            Console.WriteLine($"🗂️ Data initialization complete:");
            Console.WriteLine($"   • Sprite cache: {(Directory.Exists(spriteCacheDirectory) ? "✅ Available" : "❌ Not found")}");
            Console.WriteLine($"   • Entity cache: {(Data.EntityCacheManager.GetCachedRelics().Count > 0 ? "✅ Available" : "❌ Empty")} ({Data.EntityCacheManager.GetCachedRelics().Count} relics)");
            Console.WriteLine($"   • Run data: {(currentData.TotalRuns > 0 ? "✅ Loaded" : "❌ None")} ({currentData.TotalRuns} runs)");

            return Task.FromResult((configManager, analysisService, currentData, spriteCacheDirectory));
        }
    }

    public class UpdateCruciballRequest
    {
        public string CharacterClass { get; set; } = "";
        public int CruciballLevel { get; set; }
    }

    public class ClassActionRequest
    {
        public string ClassName { get; set; } = "";
    }

    public class SetCruciballRequest
    {
        public int Level { get; set; }
    }
}
