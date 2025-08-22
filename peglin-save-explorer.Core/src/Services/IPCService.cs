using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using peglin_save_explorer.Utils;

namespace peglin_save_explorer.Services
{
    public enum IPCMessageType
    {
        Status,
        Stop,
        GetLogs,
        RunProcessed,
        Error
    }

    public class IPCMessage
    {
        public IPCMessageType Type { get; set; }
        public string? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class IPCService
    {
        private const string PIPE_NAME = "PeglinSaveExplorerDaemon";
        private const int CONNECT_TIMEOUT = 5000; // 5 seconds

        public static async Task<IPCMessage?> SendMessageAsync(IPCMessage message, bool suppressErrors = false)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                
                // Try to connect with timeout
                await client.ConnectAsync(CONNECT_TIMEOUT);
                
                // Send message
                var messageJson = JsonSerializer.Serialize(message);
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
                
                await client.WriteAsync(lengthBytes, 0, 4);
                await client.WriteAsync(messageBytes, 0, messageBytes.Length);
                await client.FlushAsync();
                
                // Read response
                var responseLengthBytes = new byte[4];
                await client.ReadExactlyAsync(responseLengthBytes, 0, 4);
                var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
                
                var responseBytes = new byte[responseLength];
                await client.ReadExactlyAsync(responseBytes, 0, responseLength);
                
                var responseJson = Encoding.UTF8.GetString(responseBytes);
                return JsonSerializer.Deserialize<IPCMessage>(responseJson);
            }
            catch (TimeoutException)
            {
                if (!suppressErrors)
                    Logger.Error("Daemon is not responding (timeout)");
                return null;
            }
            catch (Exception ex)
            {
                if (!suppressErrors)
                    Logger.Error($"Failed to communicate with daemon: {ex.Message}");
                return null;
            }
        }

        public static async Task StartServerAsync(Func<IPCMessage, Task<IPCMessage>> messageHandler, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    
                    Logger.Debug("IPC server waiting for client connection...");
                    await server.WaitForConnectionAsync(cancellationToken);
                    Logger.Debug("IPC client connected");

                    try
                    {
                        // Read message length
                        var lengthBytes = new byte[4];
                        await server.ReadExactlyAsync(lengthBytes, 0, 4, cancellationToken);
                        var messageLength = BitConverter.ToInt32(lengthBytes, 0);
                        
                        // Read message content
                        var messageBytes = new byte[messageLength];
                        await server.ReadExactlyAsync(messageBytes, 0, messageLength, cancellationToken);
                        
                        var messageJson = Encoding.UTF8.GetString(messageBytes);
                        var message = JsonSerializer.Deserialize<IPCMessage>(messageJson);
                        
                        if (message != null)
                        {
                            Logger.Debug($"Received IPC message: {message.Type}");
                            
                            // Handle message and get response
                            var response = await messageHandler(message);
                            
                            // Send response
                            var responseJson = JsonSerializer.Serialize(response);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            var responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);
                            
                            await server.WriteAsync(responseLengthBytes, 0, 4, cancellationToken);
                            await server.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
                            await server.FlushAsync(cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error handling IPC message: {ex.Message}");
                    }
                    
                    server.Disconnect();
                }
                catch (OperationCanceledException)
                {
                    Logger.Debug("IPC server cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"IPC server error: {ex.Message}");
                    // Wait a bit before retrying
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public static async Task<bool> IsDaemonRunningAsync()
        {
            try
            {
                var statusMessage = new IPCMessage { Type = IPCMessageType.Status };
                var response = await SendMessageAsync(statusMessage);
                return response != null;
            }
            catch
            {
                return false;
            }
        }
    }
}