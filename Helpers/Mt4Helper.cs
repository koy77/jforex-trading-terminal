using System;
using System.Threading.Tasks;
using ScreenCaptureApp.Services;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;

namespace ScreenCaptureApp.Helpers
{
    /// <summary>
    /// Helper class for MT4 socket operations
    /// </summary>
    public static class Mt4Helper
    {
        /// <summary>
        /// Connects to MT4 socket
        /// </summary>
        /// <param name="mt4SocketService">The MT4 socket service instance</param>
        /// <returns>True if connection successful, false otherwise</returns>
        public static async Task<bool> ConnectToMt4Socket(Mt4SocketService mt4SocketService)
        {
            try
            {
                bool connected = await mt4SocketService.ConnectAsync();
                if (connected)
                {
                    Logger.LogInfo("Successfully connected to MT4 socket");
                    // You can send initial commands here
                    await mt4SocketService.WriteAsync("HELLO");
                    return true;
                }
                else
                {
                    Logger.LogError("Failed to connect to MT4 socket");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error connecting to MT4 socket", ex);
                return false;
            }
        }

        /// <summary>
        /// Sends a command to MT4
        /// </summary>
        /// <param name="mt4SocketService">The MT4 socket service instance</param>
        /// <param name="command">The command to send</param>
        /// <returns>True if command sent successfully, false otherwise</returns>
        public static async Task<bool> SendMt4Command(Mt4SocketService mt4SocketService, string command)
        {
            try
            {
                if (mt4SocketService.IsConnected)
                {
                    bool sent = await mt4SocketService.WriteAsync(command);
                    if (sent)
                    {
                        Logger.LogInfo($"Command sent to MT4: {command}");
                        return true;
                    }
                    else
                    {
                        Logger.LogError($"Failed to send command to MT4: {command}");
                        return false;
                    }
                }
                else
                {
                    Logger.LogWarning("Cannot send command: MT4 socket not connected");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending command to MT4: {command}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reads a line from MT4 socket
        /// </summary>
        /// <param name="mt4SocketService">The MT4 socket service instance</param>
        /// <returns>The read line or null if failed</returns>
        public static async Task<string> ReadMt4Line(Mt4SocketService mt4SocketService)
        {
            try
            {
                if (mt4SocketService.IsConnected)
                {
                    string line = await mt4SocketService.ReadLineAsync();
                    if (line != null)
                    {
                        Logger.LogInfo($"Read line from MT4: {line}");
                        return line;
                    }
                    else
                    {
                        Logger.LogWarning("No line received from MT4");
                        return null;
                    }
                }
                else
                {
                    Logger.LogWarning("Cannot read line: MT4 socket not connected");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading from MT4 socket: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles MT4 message received event
        /// </summary>
        /// <param name="message">The received message</param>
        public static void HandleMt4MessageReceived(string message)
        {
            // Update UI with received message
            Logger.LogDebug($"MT4: {message}");
            
            // You can add more specific handling here based on message content
            if (message.Contains("ERROR"))
            {
                Logger.LogWarning($"MT4 Error received: {message}");
            }
            else if (message.Contains("SUCCESS"))
            {
                Logger.LogInfo($"MT4 Success: {message}");
            }
        }

        /// <summary>
        /// Handles MT4 connection status change event
        /// </summary>
        /// <param name="status">The connection status</param>
        public static void HandleMt4ConnectionStatusChanged(string status)
        {
            Logger.LogDebug($"MT4 Connection: {status}");
            Logger.LogInfo($"MT4 Connection status: {status}");
        }

        /// <summary>
        /// Updates the MT4 status in the window title and indicator
        /// </summary>
        /// <param name="window">The window whose title to update</param>
        /// <param name="statusIndicator">Ellipse indicator to update</param>
        /// <param name="status">MT4 status string</param>
        public static void UpdateMt4StatusUI(Window window, Ellipse statusIndicator, string status)
        {
            string label = "";
            Brush brush = Brushes.Gray;
            if (status == "Connected")
            {
                brush = Brushes.LimeGreen;
                label = "Connected";
                Logger.LogInfo("MT4 Socket: Connection status indicator updated to Connected (Green)");
            }
            else if (status.StartsWith("Connection failed"))
            {
                brush = Brushes.Red;
                label = "Connection failed";
            }
            else if (status == "Disconnected")
            {
                brush = Brushes.Gray;
                label = "Disconnected";
            }
            else if (status == "Connecting")
            {
                brush = Brushes.Yellow;
                label = "Connecting";
            }
            else
            {
                brush = Brushes.Gray;
                label = status;
                Logger.LogInfo($"MT4 Socket: Connection status indicator updated to Unknown (Gray) - {status}");
            }
            if (statusIndicator != null)
                statusIndicator.Fill = brush;
        }

        /// <summary>
        /// Performs reconnect to MT4 and updates UI status
        /// </summary>
        public static async Task ReconnectMt4WithUiAsync(Window window, Ellipse statusIndicator, Mt4SocketService mt4SocketService)
        {
            try
            {
                Logger.LogInfo("MT4 Socket: Manual reconnect initiated");
                UpdateMt4StatusUI(window, statusIndicator, "Connecting");
                if (mt4SocketService != null && mt4SocketService.IsConnected)
                {
                    Logger.LogInfo("MT4 Socket: Disconnecting before reconnect");
                    await mt4SocketService.DisconnectAsync();
                }
                if (mt4SocketService != null)
                {
                    bool connected = await mt4SocketService.ConnectAsync();
                    if (connected)
                    {
                        Logger.LogInfo("MT4 Socket: Manual reconnect successful");
                        UpdateMt4StatusUI(window, statusIndicator, "Connected");
                    }
                    else
                    {
                        Logger.LogError("MT4 Socket: Manual reconnect failed");
                        UpdateMt4StatusUI(window, statusIndicator, "Connection failed: Manual reconnect failed");
                    }
                }
                else
                {
                    Logger.LogError("MT4 Socket: Service is null, cannot reconnect");
                    UpdateMt4StatusUI(window, statusIndicator, "Connection failed: Service not available");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("MT4 Socket: Error during manual reconnect", ex);
                UpdateMt4StatusUI(window, statusIndicator, $"Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks MT4 connection status and updates UI
        /// </summary>
        public static void CheckMt4ConnectionStatusWithUi(Window window, Ellipse statusIndicator, Mt4SocketService mt4SocketService)
        {
            try
            {
                if (mt4SocketService != null)
                {
                    bool isConnected = mt4SocketService.IsConnected;
                    string status = isConnected ? "Connected" : "Disconnected";
                    UpdateMt4StatusUI(window, statusIndicator, status);
                }
                else
                {
                    UpdateMt4StatusUI(window, statusIndicator, "Service not available");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("MT4 Socket: Error checking connection status", ex);
                UpdateMt4StatusUI(window, statusIndicator, "Status check failed");
            }
        }
    }
} 