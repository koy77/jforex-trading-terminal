using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.Json;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Helpers;
using System.Collections.Generic;

namespace ScreenCaptureApp.Services
{
    public class Mt4SocketService : IDisposable
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly string _host = "127.0.0.1";
        private readonly int _port = 23456;
        private bool _isConnected = false;
        private bool _isDisposed = false;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _readTask;
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

        // Ссылка на CaptureTrackingService для подписки на события
        private CaptureTrackingService _captureTrackingService;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ConnectionStatusChanged;

        public class OrdersSummary
        {
            public class SymbolInfo
            {
                public string Symbol { get; set; }
                public double Profit { get; set; }
                public double Percent { get; set; }
                public double Lots { get; set; }
                public double ProfitPoints { get; set; }
            }
            public List<SymbolInfo> Symbols { get; set; } = new List<SymbolInfo>();
            public double TotalBalance { get; set; }
        }

        public event EventHandler<OrdersSummary> OrdersSummaryReceived;

        public bool IsConnected
        {
            get
            {
                lock (_lockObject)
                {
                    return _isConnected && _tcpClient?.Connected == true;
                }
            }
        }

        public Mt4SocketService()
        {
            Logger.LogInfo("MT4 Socket Service initialized");
        }

        /// <summary>
        /// Подписывается на событие BreakoutDetected в CaptureTrackingService
        /// </summary>
        public void SubscribeToCaptureTrackingEvents(CaptureTrackingService captureTrackingService)
        {
            if (_captureTrackingService != null)
            {
                // Отписываемся от предыдущего сервиса
                UnsubscribeFromCaptureTrackingEvents();
            }

            _captureTrackingService = captureTrackingService;
            
            if (_captureTrackingService != null)
            {
                // Подписываемся на событие BreakoutDetected
                _captureTrackingService.BreakoutDetected += OnBreakoutDetected;
                
                Logger.LogInfo("MT4 Socket Service subscribed to CaptureTrackingService BreakoutDetected event");
            }
        }

        /// <summary>
        /// Отписывается от событий CaptureTrackingService
        /// </summary>
        public void UnsubscribeFromCaptureTrackingEvents()
        {
            if (_captureTrackingService != null)
            {
                // Отписываемся от события BreakoutDetected
                _captureTrackingService.BreakoutDetected -= OnBreakoutDetected;
                
                _captureTrackingService = null;
                Logger.LogInfo("MT4 Socket Service unsubscribed from CaptureTrackingService events");
            }
        }

        // Обработчик события BreakoutDetected
        private async Task OnBreakoutDetected(CaptureData capture, TrendlineBreakResult result)
        {
            Logger.LogInfo($"MT4 Socket: >>> OnBreakoutDetected handler CALLED <<< (type: {result})");
            try
            {
                // Проверяем, что брокер в Capture - Forex
                if (!string.Equals(capture.Broker, "Forex", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo($"MT4 Socket: Skipping MT4 message - capture broker is {capture.Broker}, not Forex");
                    return;
                }

                if (string.IsNullOrWhiteSpace(capture.Symbol))
                {
                    Logger.LogWarning("MT4 Socket: Symbol is missing in CaptureData, not sending to MT4");
                    return;
                }

                // Use the capture ID directly
                string captureId = capture.ID;

                string json = null;
                if (result == TrendlineBreakResult.BreakoutUp)
                {
                    json = $"{{\"cmd\":\"breakout_up\",\"symbol\":\"{capture.Symbol}\",\"risk\":{capture.Risk},\"id\":\"{captureId}\"}}";
                }
                else if (result == TrendlineBreakResult.BreakoutDown)
                {
                    json = $"{{\"cmd\":\"breakout_down\",\"symbol\":\"{capture.Symbol}\",\"risk\":{capture.Risk},\"id\":\"{captureId}\"}}";
                }

                if (json != null)
                {
                    json += "\r\n";
                    Logger.LogInfo($"MT4 Socket: Sending to MT4: {json.Trim()} (with CRLF)");
                    await WriteAsync(json);
                }

                Logger.LogInfo($"MT4 Socket: Breakout processing completed for {capture.Symbol}, type: {result}, ID: {captureId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"MT4 Socket: Error processing breakout for {capture.Symbol}, type: {result}", ex);
            }
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Logger.LogInfo($"Attempting to connect to MT4 socket at {_host}:{_port}");
                
                lock (_lockObject)
                {
                    if (_isConnected)
                    {
                        Logger.LogWarning("Already connected to MT4 socket");
                        return true;
                    }

                    _tcpClient = new TcpClient();
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                await _tcpClient.ConnectAsync(_host, _port);
                _networkStream = _tcpClient.GetStream();
                
                lock (_lockObject)
                {
                    _isConnected = true;
                }

                Logger.LogInfo($"Successfully connected to MT4 socket at {_host}:{_port}");
                ConnectionStatusChanged?.Invoke(this, "Connected");
                
                // Start reading messages in background
                _readTask = Task.Run(() => ReadMessagesAsync(_cancellationTokenSource.Token));
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to connect to MT4 socket at {_host}:{_port}", ex);
                ConnectionStatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                Logger.LogInfo("Disconnecting from MT4 socket");
                
                lock (_lockObject)
                {
                    if (!_isConnected)
                    {
                        Logger.LogWarning("Not connected to MT4 socket");
                        return true;
                    }

                    _isConnected = false;
                }

                _cancellationTokenSource?.Cancel();
                
                if (_readTask != null)
                {
                    await _readTask;
                }

                _networkStream?.Close();
                _tcpClient?.Close();
                
                Logger.LogInfo("Successfully disconnected from MT4 socket");
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error disconnecting from MT4 socket", ex);
                return false;
            }
        }

        public async Task<bool> WriteAsync(string message)
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot write message: not connected to MT4 socket");
                    ConnectionStatusChanged?.Invoke(this, "Disconnected");
                    return false;
                }

                if (string.IsNullOrEmpty(message))
                {
                    Logger.LogWarning("Cannot write empty message to MT4 socket");
                    return false;
                }

                Logger.LogSocket($"MT4 OUT: {message.Trim()}");
                byte[] data = Encoding.UTF8.GetBytes(message);
                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();
                
                Logger.LogInfo($"Sent message to MT4 socket: {message}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write message to MT4 socket: {message}", ex);
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                ConnectionStatusChanged?.Invoke(this, $"Write error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> WriteAsync(byte[] data)
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot write data: not connected to MT4 socket");
                    ConnectionStatusChanged?.Invoke(this, "Disconnected");
                    return false;
                }

                if (data == null || data.Length == 0)
                {
                    Logger.LogWarning("Cannot write empty data to MT4 socket");
                    return false;
                }

                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();
                
                Logger.LogInfo($"Sent {data.Length} bytes to MT4 socket");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write data to MT4 socket ({data?.Length ?? 0} bytes)", ex);
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                ConnectionStatusChanged?.Invoke(this, $"Write error: {ex.Message}");
                return false;
            }
        }

        private async Task ReadMessagesAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            
            try
            {
                Logger.LogInfo("Starting to read messages from MT4 socket");
                
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        
                        if (bytesRead == 0)
                        {
                            Logger.LogWarning("MT4 socket connection closed by remote host");
                            lock (_lockObject)
                            {
                                _isConnected = false;
                            }
                            ConnectionStatusChanged?.Invoke(this, "Disconnected");
                            break;
                        }

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Logger.LogInfo($"Received from MT4 socket: {message}");
                        
                        // Process the message for events
                        ProcessIncomingMessage(message);
                        
                        MessageReceived?.Invoke(this, message);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogInfo("MT4 socket read operation cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error reading from MT4 socket", ex);
                        lock (_lockObject)
                        {
                            _isConnected = false;
                        }
                        ConnectionStatusChanged?.Invoke(this, $"Connection error: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Fatal error in MT4 socket read loop", ex);
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                ConnectionStatusChanged?.Invoke(this, $"Fatal error: {ex.Message}");
            }
            finally
            {
                Logger.LogInfo("MT4 socket read loop ended");
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
            }
        }

        /// <summary>
        /// Processes incoming JSON messages from MT4 for specific events
        /// </summary>
        private void ProcessIncomingMessage(string message)
        {
            try
            {
                // Try to parse as JSON
                var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;

                // Check if it's an event message
                if (root.TryGetProperty("event", out var eventElement))
                {
                    string eventType = eventElement.GetString();
                    
                    switch (eventType)
                    {
                        case "order_closed":
                            HandleOrderClosedEvent(root);
                            break;
                        case "orders_summary":
                            HandleOrdersSummaryEvent(root);
                            break;
                        default:
                            Logger.LogInfo($"MT4 Socket: Received unknown event type: {eventType}");
                            break;
                    }
                }
            }
            catch (JsonException ex)
            {
                Logger.LogWarning($"MT4 Socket: Failed to parse JSON message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"MT4 Socket: Error processing incoming message", ex);
            }
        }

        /// <summary>
        /// Handles the order_closed event from MT4
        /// </summary>
        private void HandleOrderClosedEvent(JsonElement eventData)
        {
            try
            {
                Logger.LogInfo("MT4 Socket: Processing order_closed event");
                
                if (eventData.TryGetProperty("order", out var orderElement))
                {
                    // Extract ID from the order data
                    if (orderElement.TryGetProperty("id", out var idElement))
                    {
                        string id = idElement.GetString();
                        
                        // Get the database service
                        var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                        if (databaseService != null)
                        {
                            // Update the capture with MT4 order information
                            string orderJson = orderElement.ToString();
                            databaseService.UpdateCaptureMt4Order(id, orderJson);
                            
                            Logger.LogInfo($"MT4 Socket: Updated capture ID={id} with order data");
                        }
                        else
                        {
                            Logger.LogWarning("MT4 Socket: DatabaseService not available");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("MT4 Socket: order_closed event missing ID field");
                    }
                }
                else
                {
                    Logger.LogWarning("MT4 Socket: order_closed event missing order data");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("MT4 Socket: Error handling order_closed event", ex);
            }
        }

        private void HandleOrdersSummaryEvent(JsonElement eventData)
        {
            try
            {
                Logger.LogInfo("MT4 Socket: Processing orders_summary event");
                var summary = new OrdersSummary();
                if (eventData.TryGetProperty("symbols", out var symbolsElement) && symbolsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var symbolProp in symbolsElement.EnumerateObject())
                    {
                        var symbolInfo = new OrdersSummary.SymbolInfo { Symbol = symbolProp.Name };
                        var val = symbolProp.Value;
                        if (val.TryGetProperty("profit", out var profit))
                            symbolInfo.Profit = profit.GetDouble();
                        if (val.TryGetProperty("percent", out var percent))
                            symbolInfo.Percent = percent.GetDouble();
                        if (val.TryGetProperty("lots", out var lots))
                            symbolInfo.Lots = lots.GetDouble();
                        if (val.TryGetProperty("profit_points", out var profitPoints))
                            symbolInfo.ProfitPoints = profitPoints.GetDouble();
                        summary.Symbols.Add(symbolInfo);
                    }
                }
                if (eventData.TryGetProperty("total_balance", out var balance))
                {
                    summary.TotalBalance = balance.GetDouble();
                }
                OrdersSummaryReceived?.Invoke(this, summary);
            }
            catch (Exception ex)
            {
                Logger.LogError("MT4 Socket: Error handling orders_summary event", ex);
            }
        }

        public async Task<string> ReadLineAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot read line: not connected to MT4 socket");
                    return null;
                }

                StringBuilder sb = new StringBuilder();
                byte[] buffer = new byte[1];
                
                while (true)
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, 1);
                    
                    if (bytesRead == 0)
                    {
                        Logger.LogWarning("MT4 socket connection closed while reading line");
                        return null;
                    }

                    char c = (char)buffer[0];
                    if (c == '\n')
                    {
                        break;
                    }
                    else if (c != '\r')
                    {
                        sb.Append(c);
                    }
                }

                string line = sb.ToString();
                Logger.LogInfo($"Read line from MT4 socket: {line}");
                return line;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading line from MT4 socket", ex);
                return null;
            }
        }

        public async Task<byte[]> ReadBytesAsync(int count)
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot read bytes: not connected to MT4 socket");
                    return null;
                }

                if (count <= 0)
                {
                    Logger.LogWarning($"Invalid byte count for reading: {count}");
                    return null;
                }

                byte[] buffer = new byte[count];
                int totalBytesRead = 0;
                
                while (totalBytesRead < count)
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, totalBytesRead, count - totalBytesRead);
                    
                    if (bytesRead == 0)
                    {
                        Logger.LogWarning("MT4 socket connection closed while reading bytes");
                        return null;
                    }
                    
                    totalBytesRead += bytesRead;
                }

                Logger.LogInfo($"Read {count} bytes from MT4 socket");
                return buffer;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading {count} bytes from MT4 socket", ex);
                return null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Logger.LogInfo("Disposing MT4 Socket Service");
            
            try
            {
                // Отписываемся от событий CaptureTrackingService
                UnsubscribeFromCaptureTrackingEvents();
                
                _cancellationTokenSource?.Cancel();
                _readTask?.Wait(TimeSpan.FromSeconds(5));
                
                _networkStream?.Dispose();
                _tcpClient?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during MT4 Socket Service disposal", ex);
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
} 