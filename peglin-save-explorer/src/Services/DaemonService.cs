using System.Text;
using peglin_save_explorer.Core;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Services
{
    public class DaemonService
    {
        private readonly ConfigurationManager _configManager;
        private readonly RunHistoryManager _runHistoryManager;
        private readonly StringBuilder _logBuffer;
        private FileSystemWatcher? _fileWatcher;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _ipcServerTask;
        private DateTime _lastStatsModified = DateTime.MinValue;
        private readonly object _logLock = new object();

        public DaemonService(ConfigurationManager configManager)
        {
            _configManager = configManager;
            _runHistoryManager = new RunHistoryManager(configManager);
            _logBuffer = new StringBuilder();
        }

        public async Task StartAsync()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                LogMessage("Starting Peglin Save Explorer Daemon...");
                
                // Initialize game data
                GameDataService.InitializeGameData(_configManager);
                LogMessage("Game data initialized");
                
                // Start IPC server
                _ipcServerTask = IPCService.StartServerAsync(HandleIPCMessage, _cancellationTokenSource.Token);
                LogMessage("IPC server started");
                
                // Setup file watcher
                SetupFileWatcher();
                
                LogMessage("Daemon started successfully");
                
                // Wait for cancellation
                await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                LogMessage("Daemon shutdown requested");
            }
            catch (Exception ex)
            {
                LogMessage($"Daemon error: {ex.Message}");
                throw;
            }
            finally
            {
                await StopAsync();
            }
        }

        public async Task StopAsync()
        {
            LogMessage("Stopping daemon...");
            
            _fileWatcher?.Dispose();
            _fileWatcher = null;
            
            _cancellationTokenSource?.Cancel();
            
            if (_ipcServerTask != null)
            {
                try
                {
                    await _ipcServerTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    LogMessage("IPC server shutdown timeout");
                }
            }
            
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            
            LogMessage("Daemon stopped");
        }

        private void SetupFileWatcher()
        {
            try
            {
                var saveFilePath = _configManager.GetEffectiveSaveFilePath();
                if (string.IsNullOrEmpty(saveFilePath))
                {
                    LogMessage("No save file path configured, daemon will not watch for changes");
                    return;
                }

                var saveDirectory = Path.GetDirectoryName(saveFilePath);
                if (string.IsNullOrEmpty(saveDirectory) || !Directory.Exists(saveDirectory))
                {
                    LogMessage($"Save directory not found: {saveDirectory}");
                    return;
                }

                _fileWatcher = new FileSystemWatcher(saveDirectory)
                {
                    Filter = "Stats_*.data",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += OnStatsFileChanged;
                LogMessage($"Watching for changes in: {saveDirectory}");
                
                // Get initial timestamp
                var statsFilePath = RunDataService.GetStatsFilePath(saveFilePath);
                if (!string.IsNullOrEmpty(statsFilePath) && File.Exists(statsFilePath))
                {
                    _lastStatsModified = File.GetLastWriteTime(statsFilePath);
                    LogMessage($"Initial stats file timestamp: {_lastStatsModified}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to setup file watcher: {ex.Message}");
            }
        }

        private async void OnStatsFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Debounce file changes - wait for file to be completely written
                await Task.Delay(1000);
                
                var lastModified = File.GetLastWriteTime(e.FullPath);
                if (lastModified <= _lastStatsModified)
                {
                    return; // No actual change
                }
                
                _lastStatsModified = lastModified;
                LogMessage($"Stats file changed: {e.FullPath}");
                
                await ProcessNewRuns();
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing file change: {ex.Message}");
            }
        }

        private async Task ProcessNewRuns()
        {
            try
            {
                LogMessage("Processing new runs...");
                
                var runs = RunDataService.LoadRunHistory(null, _configManager);
                if (runs.Count == 0)
                {
                    LogMessage("No runs found");
                    return;
                }

                // The RunHistoryManager will automatically merge with persistent database
                // and handle deduplication, so we just need to trigger the load
                var newRunsCount = runs.Count;
                LogMessage($"Processed run history update - {newRunsCount} total runs in stats file");
                
                // We could add additional processing here, like notifications
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing new runs: {ex.Message}");
            }
        }

        private async Task<IPCMessage> HandleIPCMessage(IPCMessage message)
        {
            try
            {
                LogMessage($"Handling IPC message: {message.Type}");
                
                switch (message.Type)
                {
                    case IPCMessageType.Status:
                        return new IPCMessage 
                        { 
                            Type = IPCMessageType.Status, 
                            Data = "Daemon is running" 
                        };
                    
                    case IPCMessageType.Stop:
                        LogMessage("Stop requested via IPC");
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(100); // Give time to send response
                            _cancellationTokenSource?.Cancel();
                        });
                        return new IPCMessage 
                        { 
                            Type = IPCMessageType.Status, 
                            Data = "Stopping daemon..." 
                        };
                    
                    case IPCMessageType.GetLogs:
                        return new IPCMessage 
                        { 
                            Type = IPCMessageType.GetLogs, 
                            Data = GetRecentLogs() 
                        };
                    
                    default:
                        return new IPCMessage 
                        { 
                            Type = IPCMessageType.Error, 
                            Data = $"Unknown message type: {message.Type}" 
                        };
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling IPC message: {ex.Message}");
                return new IPCMessage 
                { 
                    Type = IPCMessageType.Error, 
                    Data = ex.Message 
                };
            }
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";
            
            lock (_logLock)
            {
                _logBuffer.AppendLine(logEntry);
                
                // Keep only last 1000 lines
                var lines = _logBuffer.ToString().Split('\n');
                if (lines.Length > 1000)
                {
                    _logBuffer.Clear();
                    _logBuffer.AppendLine(string.Join('\n', lines.TakeLast(1000)));
                }
            }
            
            Logger.Info($"DAEMON: {message}");
        }

        private string GetRecentLogs()
        {
            lock (_logLock)
            {
                return _logBuffer.ToString();
            }
        }
    }
}