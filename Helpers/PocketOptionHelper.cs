using System;
using System.Threading.Tasks;
using ScreenCaptureApp.Services;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;

namespace ScreenCaptureApp.Helpers
{
    /// <summary>
    /// Helper class for Pocket Option socket operations
    /// </summary>
    public static class PocketOptionHelper
    {
        /// <summary>
        /// Connects to Pocket Option socket
        /// </summary>
        /// <param name="pocketOptionSocketService">The Pocket Option socket service instance</param>
        /// <returns>True if connection successful, false otherwise</returns>
        public static async Task<bool> ConnectToPocketOptionSocket(PocketOptionSocketService pocketOptionSocketService)
        {
            try
            {
                bool connected = await pocketOptionSocketService.ConnectAsync();
                if (connected)
                {
                    Logger.LogInfo("Successfully connected to Pocket Option socket");
                    // You can send initial commands here
                    await pocketOptionSocketService.WriteAsync("HELLO");
                    return true;
                }
                else
                {
                    Logger.LogError("Failed to connect to Pocket Option socket");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error connecting to Pocket Option socket", ex);
                return false;
            }
        }

        /// <summary>
        /// Sends a command to Pocket Option
        /// </summary>
        /// <param name="pocketOptionSocketService">The Pocket Option socket service instance</param>
        /// <param name="command">The command to send</param>
        /// <returns>True if command sent successfully, false otherwise</returns>
        public static async Task<bool> SendPocketOptionCommand(PocketOptionSocketService pocketOptionSocketService, string command)
        {
            try
            {
                if (pocketOptionSocketService.IsConnected)
                {
                    bool sent = await pocketOptionSocketService.WriteAsync(command);
                    if (sent)
                    {
                        Logger.LogInfo($"Command sent to Pocket Option: {command}");
                        return true;
                    }
                    else
                    {
                        Logger.LogError($"Failed to send command to Pocket Option: {command}");
                        return false;
                    }
                }
                else
                {
                    Logger.LogWarning("Cannot send command: Pocket Option socket not connected");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending command to Pocket Option: {command}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reads a line from Pocket Option socket
        /// </summary>
        /// <param name="pocketOptionSocketService">The Pocket Option socket service instance</param>
        /// <returns>The read line or null if failed</returns>
        public static async Task<string> ReadPocketOptionLine(PocketOptionSocketService pocketOptionSocketService)
        {
            try
            {
                if (pocketOptionSocketService.IsConnected)
                {
                    string line = await pocketOptionSocketService.ReadLineAsync();
                    if (line != null)
                    {
                        Logger.LogInfo($"Read line from Pocket Option: {line}");
                        return line;
                    }
                    else
                    {
                        Logger.LogWarning("No line received from Pocket Option");
                        return null;
                    }
                }
                else
                {
                    Logger.LogWarning("Cannot read line: Pocket Option socket not connected");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading from Pocket Option socket: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles Pocket Option message received event
        /// </summary>
        /// <param name="message">The received message</param>
        public static void HandlePocketOptionMessageReceived(string message)
        {
            // Update UI with received message
            Logger.LogDebug($"Pocket Option: {message}");
            
            // You can add more specific handling here based on message content
            if (message.Contains("ERROR"))
            {
                Logger.LogWarning($"Pocket Option Error received: {message}");
            }
            else if (message.Contains("SUCCESS"))
            {
                Logger.LogInfo($"Pocket Option Success: {message}");
            }
        }

        /// <summary>
        /// Handles Pocket Option connection status change event
        /// </summary>
        /// <param name="status">The connection status</param>
        public static void HandlePocketOptionConnectionStatusChanged(string status)
        {
            Logger.LogDebug($"Pocket Option Connection: {status}");
            Logger.LogInfo($"Pocket Option Connection status: {status}");
        }

        /// <summary>
        /// Updates the Pocket Option status in the window title and indicator
        /// </summary>
        /// <param name="window">The window whose title to update</param>
        /// <param name="statusIndicator">Ellipse indicator to update</param>
        /// <param name="status">Pocket Option status string</param>
        public static void UpdatePocketOptionStatusUI(Window window, Ellipse statusIndicator, string status)
        {
            string label = "";
            Brush brush = Brushes.Gray;
            if (status == "Connected")
            {
                brush = Brushes.LimeGreen;
                label = "Connected";
                Logger.LogInfo("Pocket Option Socket: Connection status indicator updated to Connected (Green)");
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
                Logger.LogInfo($"Pocket Option Socket: Connection status indicator updated to Unknown (Gray) - {status}");
            }
            if (statusIndicator != null)
                statusIndicator.Fill = brush;
        }

        /// <summary>
        /// Performs reconnect to Pocket Option and updates UI status
        /// </summary>
        public static async Task ReconnectPocketOptionWithUiAsync(Window window, Ellipse statusIndicator, PocketOptionSocketService pocketOptionSocketService)
        {
            try
            {
                Logger.LogInfo("Pocket Option Socket: Manual reconnect initiated");
                UpdatePocketOptionStatusUI(window, statusIndicator, "Connecting");
                if (pocketOptionSocketService != null && pocketOptionSocketService.IsConnected)
                {
                    Logger.LogInfo("Pocket Option Socket: Disconnecting before reconnect");
                    await pocketOptionSocketService.DisconnectAsync();
                }
                if (pocketOptionSocketService != null)
                {
                    bool connected = await pocketOptionSocketService.ConnectAsync();
                    if (connected)
                    {
                        Logger.LogInfo("Pocket Option Socket: Manual reconnect successful");
                        UpdatePocketOptionStatusUI(window, statusIndicator, "Connected");
                    }
                    else
                    {
                        Logger.LogError("Pocket Option Socket: Manual reconnect failed");
                        UpdatePocketOptionStatusUI(window, statusIndicator, "Connection failed: Manual reconnect failed");
                    }
                }
                else
                {
                    Logger.LogError("Pocket Option Socket: Service is null, cannot reconnect");
                    UpdatePocketOptionStatusUI(window, statusIndicator, "Connection failed: Service not available");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Pocket Option Socket: Error during manual reconnect", ex);
                UpdatePocketOptionStatusUI(window, statusIndicator, $"Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks Pocket Option connection status and updates UI
        /// </summary>
        public static void CheckPocketOptionConnectionStatusWithUi(Window window, Ellipse statusIndicator, PocketOptionSocketService pocketOptionSocketService)
        {
            try
            {
                if (pocketOptionSocketService != null)
                {
                    bool isConnected = pocketOptionSocketService.IsConnected;
                    string status = isConnected ? "Connected" : "Disconnected";
                    UpdatePocketOptionStatusUI(window, statusIndicator, status);
                }
                else
                {
                    UpdatePocketOptionStatusUI(window, statusIndicator, "Service not available");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Pocket Option Socket: Error checking connection status", ex);
                UpdatePocketOptionStatusUI(window, statusIndicator, "Status check failed");
            }
        }
    }
} 