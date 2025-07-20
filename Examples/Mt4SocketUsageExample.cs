using System;
using System.Threading.Tasks;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Examples
{
    /// <summary>
    /// Example usage of the MT4 Socket Service
    /// This demonstrates how to connect, send commands, and receive responses
    /// </summary>
    public class Mt4SocketUsageExample
    {
        private Mt4SocketService _mt4SocketService;

        public Mt4SocketUsageExample()
        {
            _mt4SocketService = new Mt4SocketService();
            
            // Subscribe to events
            _mt4SocketService.MessageReceived += OnMessageReceived;
            _mt4SocketService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        /// <summary>
        /// Example of connecting to MT4 socket and sending initial commands
        /// </summary>
        public async Task<bool> ConnectAndInitialize()
        {
            try
            {
                Logger.LogInfo("Starting MT4 socket connection example");
                
                // Connect to MT4 socket
                bool connected = await _mt4SocketService.ConnectAsync();
                if (!connected)
                {
                    Logger.LogError("Failed to connect to MT4 socket");
                    return false;
                }

                // Send initial handshake
                await _mt4SocketService.WriteAsync("HELLO");
                
                // Wait a moment for response
                await Task.Delay(1000);
                
                // Send some example commands
                await SendExampleCommands();
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ConnectAndInitialize", ex);
                return false;
            }
        }

        /// <summary>
        /// Example of sending various commands to MT4
        /// </summary>
        public async Task SendExampleCommands()
        {
            try
            {
                if (!_mt4SocketService.IsConnected)
                {
                    Logger.LogWarning("Cannot send commands: not connected to MT4");
                    return;
                }

                // Example 1: Send string command
                await _mt4SocketService.WriteAsync("GET_SYMBOL_INFO EURUSD");
                
                // Example 2: Send byte array command
                byte[] commandBytes = System.Text.Encoding.UTF8.GetBytes("GET_ACCOUNT_INFO");
                await _mt4SocketService.WriteAsync(commandBytes);
                
                // Example 3: Send trading command
                await _mt4SocketService.WriteAsync("BUY EURUSD 0.1 1.2000");
                
                Logger.LogInfo("Example commands sent successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sending example commands", ex);
            }
        }

        /// <summary>
        /// Example of reading specific data from MT4
        /// </summary>
        public async Task ReadExampleData()
        {
            try
            {
                if (!_mt4SocketService.IsConnected)
                {
                    Logger.LogWarning("Cannot read data: not connected to MT4");
                    return;
                }

                // Example 1: Read a line of text
                string line = await _mt4SocketService.ReadLineAsync();
                if (line != null)
                {
                    Logger.LogInfo($"Read line: {line}");
                    
                    // Process the line based on content
                    if (line.Contains("EURUSD"))
                    {
                        Logger.LogInfo("EURUSD data received");
                    }
                }

                // Example 2: Read specific number of bytes
                byte[] data = await _mt4SocketService.ReadBytesAsync(1024);
                if (data != null)
                {
                    string dataString = System.Text.Encoding.UTF8.GetString(data);
                    Logger.LogInfo($"Read {data.Length} bytes: {dataString}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading example data", ex);
            }
        }

        /// <summary>
        /// Example of sending a trading order
        /// </summary>
        public async Task SendTradingOrder(string symbol, string orderType, double volume, double price = 0)
        {
            try
            {
                if (!_mt4SocketService.IsConnected)
                {
                    Logger.LogWarning("Cannot send trading order: not connected to MT4");
                    return;
                }

                string orderCommand = $"{orderType.ToUpper()} {symbol} {volume}";
                if (price > 0)
                {
                    orderCommand += $" {price}";
                }

                bool sent = await _mt4SocketService.WriteAsync(orderCommand);
                if (sent)
                {
                    Logger.LogInfo($"Trading order sent: {orderCommand}");
                }
                else
                {
                    Logger.LogError($"Failed to send trading order: {orderCommand}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending trading order: {symbol} {orderType} {volume}", ex);
            }
        }

        /// <summary>
        /// Example of requesting account information
        /// </summary>
        public async Task RequestAccountInfo()
        {
            try
            {
                if (!_mt4SocketService.IsConnected)
                {
                    Logger.LogWarning("Cannot request account info: not connected to MT4");
                    return;
                }

                // Send account info request
                await _mt4SocketService.WriteAsync("GET_ACCOUNT_INFO");
                
                // Wait for response
                await Task.Delay(500);
                
                // Read the response
                string response = await _mt4SocketService.ReadLineAsync();
                if (response != null)
                {
                    Logger.LogInfo($"Account info received: {response}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error requesting account info", ex);
            }
        }

        /// <summary>
        /// Example of monitoring connection and reconnecting if needed
        /// </summary>
        public async Task MonitorConnection()
        {
            try
            {
                while (true)
                {
                    if (!_mt4SocketService.IsConnected)
                    {
                        Logger.LogWarning("MT4 connection lost, attempting to reconnect...");
                        await _mt4SocketService.ConnectAsync();
                    }
                    
                    // Wait before next check
                    await Task.Delay(5000);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in connection monitoring", ex);
            }
        }

        /// <summary>
        /// Cleanup method to properly dispose of the service
        /// </summary>
        public void Dispose()
        {
            try
            {
                _mt4SocketService?.Dispose();
                Logger.LogInfo("MT4 Socket Service disposed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error disposing MT4 Socket Service", ex);
            }
        }

        // Event handlers
        private void OnMessageReceived(object sender, string message)
        {
            Logger.LogInfo($"MT4 Message received: {message}");
            
            // You can add specific message processing here
            if (message.Contains("ORDER_OPENED"))
            {
                Logger.LogInfo("Trading order opened successfully");
            }
            else if (message.Contains("ERROR"))
            {
                Logger.LogWarning($"MT4 Error: {message}");
            }
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            Logger.LogInfo($"MT4 Connection status: {status}");
        }
    }
} 