using System.CommandLine;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;
using peglin_save_explorer.Services;
using Newtonsoft.Json.Linq;

namespace peglin_save_explorer.Commands
{
    public class WebCommand : ICommand
    {
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
            var builder = WebApplication.CreateBuilder();

            // Detect if we're in development mode
            var isDevelopment = builder.Environment.IsDevelopment();
            
            if (isDevelopment)
            {
                Console.WriteLine("Development mode detected - enhanced error reporting enabled");
            }

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

            // Initialize services
            var configManager = new ConfigurationManager();
            var analysisService = new DataAnalysisService(configManager);

            // Serve static files from the built frontend
            var frontendPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            
            // In development mode, try to serve from the dev build first
            if (isDevelopment)
            {
                var devFrontendPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web-frontend", "dist");
                if (Directory.Exists(devFrontendPath))
                {
                    frontendPath = devFrontendPath;
                    Console.WriteLine($"Development mode: serving frontend from {devFrontendPath}");
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

            // In-memory data store for the current session
            var currentData = new RunHistoryData();
            
            // Try to load data from provided file or default configuration
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

                    if (requestBody.CruciballLevel < 0 || requestBody.CruciballLevel > 20)
                    {
                        return Results.BadRequest(CreateApiResponse<object>(null, "Cruciball level must be between 0 and 20"));
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

            app.MapGet("/api/cruciball-levels", () =>
            {
                try
                {
                    var cruciballLevels = SaveDataLoader.GetCruciballLevelsPerClass();
                    return Results.Ok(CreateApiResponse(cruciballLevels));
                }
                catch (Exception ex)
                {
                    if (isDevelopment) Console.WriteLine($"Get cruciball levels error: {ex}");
                    return Results.BadRequest(CreateApiResponse<object>(null, "Failed to get cruciball levels"));
                }
            });

            // Fallback to serve the React app for any non-API routes
            app.MapFallback((HttpContext context) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    return Results.NotFound();
                }

                var indexPath = Path.Combine(frontendPath, "index.html");
                if (File.Exists(indexPath))
                {
                    return Results.File(indexPath, "text/html");
                }

                if (isDevelopment)
                {
                    return Results.NotFound($"Frontend not built. Frontend path: {frontendPath}. Run 'npm run build' in the web-frontend directory.");
                }
                return Results.NotFound("Frontend not built. Run the build process first.");
            });

            Console.WriteLine($"Starting web server on http://localhost:{port}");
            if (currentData.TotalRuns > 0)
            {
                Console.WriteLine($"Loaded {currentData.TotalRuns} runs, {currentData.TotalWins} wins ({currentData.WinRate:P1} win rate)");
            }
            else
            {
                Console.WriteLine("No initial data loaded. Upload a save file via the web interface.");
            }
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

        private object CreateApiResponse<T>(T data, string? error = null)
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
    }

    public class UpdateCruciballRequest
    {
        public string CharacterClass { get; set; } = "";
        public int CruciballLevel { get; set; }
    }
}
