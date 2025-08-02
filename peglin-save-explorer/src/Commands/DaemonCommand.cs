using System.CommandLine;
using System.Diagnostics;
using peglin_save_explorer.Core;
using peglin_save_explorer.Services;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Commands
{
    public class DaemonCommand : ICommand
    {
        public Command CreateCommand()
        {
            var startCommand = new Command("start", "Start the daemon to watch for new runs");
            startCommand.SetHandler(StartDaemon);

            var stopCommand = new Command("stop", "Stop the running daemon");
            stopCommand.SetHandler(StopDaemon);

            var statusCommand = new Command("status", "Check daemon status");
            statusCommand.SetHandler(GetStatus);

            var logsCommand = new Command("logs", "View daemon logs");
            logsCommand.SetHandler(ViewLogs);

            var runDaemonCommand = new Command("run", "Run daemon in foreground (internal use)");
            runDaemonCommand.SetHandler(RunDaemonForeground);
            runDaemonCommand.IsHidden = true; // Hide from help

            var daemonCommand = new Command("daemon", "Manage the daemon service")
            {
                startCommand,
                stopCommand,
                statusCommand,
                logsCommand,
                runDaemonCommand
            };

            return daemonCommand;
        }

        private static async Task StartDaemon()
        {
            try
            {
                // Check if daemon is already running
                if (await IPCService.IsDaemonRunningAsync())
                {
                    Console.WriteLine("Daemon is already running");
                    return;
                }

                Console.WriteLine("Starting daemon...");

                // Get current executable path
                var currentProcess = Process.GetCurrentProcess();
                var executablePath = currentProcess.MainModule?.FileName;
                
                if (string.IsNullOrEmpty(executablePath))
                {
                    Logger.Error("Could not determine executable path");
                    return;
                }

                // Start daemon process in background
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "daemon run",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = false
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    Logger.Error("Failed to start daemon process");
                    return;
                }

                // Write PID file
                WritePidFile(process.Id);

                // Wait a moment and check if daemon started successfully
                await Task.Delay(2000);
                
                if (await IPCService.IsDaemonRunningAsync())
                {
                    Console.WriteLine($"Daemon started successfully (PID: {process.Id})");
                }
                else
                {
                    Console.WriteLine("Daemon may have failed to start. Check logs for details.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start daemon: {ex.Message}");
            }
        }

        private static async Task StopDaemon()
        {
            try
            {
                Console.WriteLine("Stopping daemon...");

                var stopMessage = new IPCMessage { Type = IPCMessageType.Stop };
                var response = await IPCService.SendMessageAsync(stopMessage);

                if (response != null)
                {
                    Console.WriteLine("Daemon stop signal sent");
                    
                    // Wait for daemon to stop - expect it to stop responding
                    for (int i = 0; i < 10; i++)
                    {
                        await Task.Delay(500);
                        
                        // Check if daemon is still running - suppress errors during shutdown
                        var statusMessage = new IPCMessage { Type = IPCMessageType.Status };
                        var statusResponse = await IPCService.SendMessageAsync(statusMessage, suppressErrors: true);
                        if (statusResponse == null)
                        {
                            // Daemon stopped responding, which is expected
                            Console.WriteLine("Daemon stopped successfully");
                            CleanupPidFile();
                            return;
                        }
                    }
                    
                    Console.WriteLine("Daemon is taking longer than expected to stop");
                }
                else
                {
                    Console.WriteLine("Could not communicate with daemon (may not be running)");
                    
                    // Try to cleanup PID file and kill process if found
                    var pid = ReadPidFile();
                    if (pid.HasValue)
                    {
                        try
                        {
                            var process = Process.GetProcessById(pid.Value);
                            process.Kill(true);
                            Console.WriteLine($"Forcefully stopped daemon process (PID: {pid})");
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist
                            Console.WriteLine("Daemon process not found");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Could not kill process {pid}: {ex.Message}");
                        }
                        finally
                        {
                            CleanupPidFile();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error stopping daemon: {ex.Message}");
            }
        }

        private static async Task GetStatus()
        {
            try
            {
                if (await IPCService.IsDaemonRunningAsync())
                {
                    var statusMessage = new IPCMessage { Type = IPCMessageType.Status };
                    var response = await IPCService.SendMessageAsync(statusMessage);
                    
                    if (response != null)
                    {
                        Console.WriteLine($"Daemon Status: {response.Data}");
                        
                        var pid = ReadPidFile();
                        if (pid.HasValue)
                        {
                            Console.WriteLine($"Process ID: {pid}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Daemon is running but not responding");
                    }
                }
                else
                {
                    Console.WriteLine("Daemon is not running");
                    
                    // Check for stale PID file
                    var pid = ReadPidFile();
                    if (pid.HasValue)
                    {
                        Console.WriteLine("Found stale PID file, cleaning up...");
                        CleanupPidFile();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking daemon status: {ex.Message}");
            }
        }

        private static async Task ViewLogs()
        {
            try
            {
                if (!await IPCService.IsDaemonRunningAsync())
                {
                    Console.WriteLine("Daemon is not running");
                    return;
                }

                var logsMessage = new IPCMessage { Type = IPCMessageType.GetLogs };
                var response = await IPCService.SendMessageAsync(logsMessage);

                if (response != null && !string.IsNullOrEmpty(response.Data))
                {
                    Console.WriteLine("=== Daemon Logs ===");
                    Console.WriteLine(response.Data);
                }
                else
                {
                    Console.WriteLine("No logs available or daemon not responding");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving logs: {ex.Message}");
            }
        }

        private static async Task RunDaemonForeground()
        {
            try
            {
                Console.WriteLine("Starting daemon in foreground mode...");
                
                var configManager = new ConfigurationManager();
                var daemon = new DaemonService(configManager);

                // Setup signal handlers
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await daemon.StartAsync();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Daemon shutdown completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Daemon error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static string GetPidFilePath()
        {
            var configFilePath = new ConfigurationManager().GetConfigFilePath();
            var configDir = Path.GetDirectoryName(configFilePath);
            return Path.Combine(configDir!, "daemon.pid");
        }

        private static void WritePidFile(int pid)
        {
            try
            {
                var pidFile = GetPidFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
                File.WriteAllText(pidFile, pid.ToString());
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to write PID file: {ex.Message}");
            }
        }

        private static int? ReadPidFile()
        {
            try
            {
                var pidFile = GetPidFilePath();
                if (File.Exists(pidFile))
                {
                    var pidText = File.ReadAllText(pidFile).Trim();
                    if (int.TryParse(pidText, out var pid))
                    {
                        return pid;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error reading PID file: {ex.Message}");
            }
            return null;
        }

        private static void CleanupPidFile()
        {
            try
            {
                var pidFile = GetPidFilePath();
                if (File.Exists(pidFile))
                {
                    File.Delete(pidFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error cleaning up PID file: {ex.Message}");
            }
        }
    }
}