using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Helpers;

namespace ScreenCaptureApp.Services
{
    public class BinaryOptionsSocketService : IDisposable
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly string _host = "192.168.0.3";
        private readonly int _port = 65300;
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

        public BinaryOptionsSocketService()
        {
            Logger.LogInfo("Binary Options Socket Service initialized");
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
                
                Logger.LogInfo("Binary Options Socket Service subscribed to CaptureTrackingService BreakoutDetected event");
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
                Logger.LogInfo("Binary Options Socket Service unsubscribed from CaptureTrackingService events");
            }
        }

        // Обработчик события BreakoutDetected
        private async Task OnBreakoutDetected(CaptureData capture, TrendlineBreakResult result)
        {
            Logger.LogInfo($"Binary Options Socket: >>> OnBreakoutDetected handler CALLED <<< (type: {result})");
            try
            {
                // Проверяем, что брокер в Capture - Pocket Option
                if (!string.Equals(capture.Broker, "Pocket Option", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo($"Binary Options Socket: Skipping Pocket Option message - capture broker is {capture.Broker}, not Pocket Option");
                    return;
                }

                if (string.IsNullOrWhiteSpace(capture.Symbol))
                {
                    Logger.LogWarning("Binary Options Socket: Symbol is missing in CaptureData, not sending to Pocket Option");
                    return;
                }

                string json = null;
                if (result == TrendlineBreakResult.BreakoutUp)
                {
                    json = $"{{\"cmd\":\"BUY\",\"symbol\":\"{capture.Symbol}\",\"risk\":{capture.Risk},\"duration\":{capture.Duration}}}";
                }
                else if (result == TrendlineBreakResult.BreakoutDown)
                {
                    json = $"{{\"cmd\":\"SELL\",\"symbol\":\"{capture.Symbol}\",\"risk\":{capture.Risk},\"duration\":{capture.Duration}}}";
                }

                if (json != null)
                {
                    json += "\r\n";
                    Logger.LogInfo($"Binary Options Socket: Sending to Pocket Option: {json.Trim()} (with CRLF)");
                    await WriteAsync(json);
                }

                Logger.LogInfo($"Binary Options Socket: Breakout processing completed for {capture.Symbol}, type: {result}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Binary Options Socket: Error processing breakout for {capture.Symbol}, type: {result}", ex);
            }
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Logger.LogInfo($"Attempting to connect to Pocket Option socket at {_host}:{_port}");
                
                lock (_lockObject)
                {
                    if (_isConnected)
                    {
                        Logger.LogWarning("Already connected to Pocket Option socket");
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

                Logger.LogInfo($"Successfully connected to Pocket Option socket at {_host}:{_port}");
                ConnectionStatusChanged?.Invoke(this, "Connected");
                
                // Start reading messages in background
                _readTask = Task.Run(() => ReadMessagesAsync(_cancellationTokenSource.Token));
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to connect to Pocket Option socket at {_host}:{_port}", ex);
                ConnectionStatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                Logger.LogInfo("Disconnecting from Pocket Option socket");
                
                lock (_lockObject)
                {
                    if (!_isConnected)
                    {
                        Logger.LogWarning("Not connected to Pocket Option socket");
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
                
                Logger.LogInfo("Successfully disconnected from Pocket Option socket");
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error disconnecting from Pocket Option socket", ex);
                return false;
            }
        }

        public async Task<bool> WriteAsync(string message)
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot write message: not connected to Pocket Option socket");
                    ConnectionStatusChanged?.Invoke(this, "Disconnected");
                    return false;
                }

                if (string.IsNullOrEmpty(message))
                {
                    Logger.LogWarning("Cannot write empty message to Pocket Option socket");
                    return false;
                }

                Logger.LogSocket($"PO OUT: {message.Trim()}");
                byte[] data = Encoding.UTF8.GetBytes(message);
                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();
                
                Logger.LogInfo($"Sent message to Pocket Option socket: {message}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write message to Pocket Option socket: {message}", ex);
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
                    Logger.LogWarning("Cannot write data: not connected to Pocket Option socket");
                    ConnectionStatusChanged?.Invoke(this, "Disconnected");
                    return false;
                }

                if (data == null || data.Length == 0)
                {
                    Logger.LogWarning("Cannot write empty data to Pocket Option socket");
                    return false;
                }

                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();
                
                Logger.LogInfo($"Sent {data.Length} bytes to Pocket Option socket");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write data to Pocket Option socket ({data?.Length ?? 0} bytes)", ex);
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
                Logger.LogInfo("Starting to read messages from Pocket Option socket");
                
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        
                        if (bytesRead == 0)
                        {
                            Logger.LogWarning("Pocket Option socket connection closed by remote host");
                            lock (_lockObject)
                            {
                                _isConnected = false;
                            }
                            ConnectionStatusChanged?.Invoke(this, "Disconnected");
                            break;
                        }

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Logger.LogInfo($"Received from Pocket Option socket: {message}");
                        
                        MessageReceived?.Invoke(this, message);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogInfo("Pocket Option socket read operation cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error reading from Pocket Option socket", ex);
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
                Logger.LogError("Fatal error in Pocket Option socket read loop", ex);
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                ConnectionStatusChanged?.Invoke(this, $"Fatal error: {ex.Message}");
            }
            finally
            {
                Logger.LogInfo("Pocket Option socket read loop ended");
                lock (_lockObject)
                {
                    _isConnected = false;
                }
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
            }
        }

        public async Task<string> ReadLineAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot read line: not connected to Pocket Option socket");
                    return null;
                }

                StringBuilder sb = new StringBuilder();
                byte[] buffer = new byte[1];
                
                while (true)
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, 1);
                    
                    if (bytesRead == 0)
                    {
                        Logger.LogWarning("Pocket Option socket connection closed while reading line");
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
                Logger.LogInfo($"Read line from Pocket Option socket: {line}");
                return line;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading line from Pocket Option socket", ex);
                return null;
            }
        }

        public async Task<byte[]> ReadBytesAsync(int count)
        {
            try
            {
                if (!IsConnected)
                {
                    Logger.LogWarning("Cannot read bytes: not connected to Pocket Option socket");
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
                        Logger.LogWarning("Pocket Option socket connection closed while reading bytes");
                        return null;
                    }
                    
                    totalBytesRead += bytesRead;
                }

                Logger.LogInfo($"Read {count} bytes from Pocket Option socket");
                return buffer;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading {count} bytes from Pocket Option socket", ex);
                return null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Logger.LogInfo("Disposing Pocket Option Socket Service");
            
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
                Logger.LogError("Error during Pocket Option Socket Service disposal", ex);
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
} 