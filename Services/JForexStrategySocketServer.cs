using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace ScreenCaptureApp.Services
{
    public class JForexStrategySocketServer : IDisposable
    {
        private TcpListener _tcpListener;
        private readonly string _host = "localhost";
        private readonly int _port = 5555;
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _acceptTask;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ClientConnected;
        public event EventHandler<string> ClientDisconnected;
        public event EventHandler<string> ServerStatusChanged;

        public bool IsRunning
        {
            get
            {
                lock (_lockObject)
                {
                    return _isRunning;
                }
            }
        }

        public int ConnectedClientsCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.Count;
                }
            }
        }

        public JForexStrategySocketServer()
        {
            Logger.LogTagInfo("JForexStrategySocketServer", "JForex Strategy Socket Server initialized");
        }

        /// <summary>
        /// Запуск TCP сервера
        /// </summary>
        public async Task<bool> StartAsync()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    Logger.LogTagInfo("JForexStrategySocketServer", "Server is already running");
                    return true;
                }

                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            try
            {
                _tcpListener = new TcpListener(IPAddress.Parse(_host), _port);
                _tcpListener.Start();
                
                Logger.LogTagInfo("JForexStrategySocketServer", $"JForex Strategy Socket Server started on {_host}:{_port}");
                OnServerStatusChanged($"Server started on {_host}:{_port}");

                _acceptTask = AcceptClientsAsync(_cancellationTokenSource.Token);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogTagInfo("JForexStrategySocketServer", $"Failed to start JForex Strategy Socket Server: {ex.Message}");
                lock (_lockObject)
                {
                    _isRunning = false;
                }
                return false;
            }
        }

        /// <summary>
        /// Остановка TCP сервера
        /// </summary>
        public async Task<bool> StopAsync()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    Logger.LogTagInfo("JForexStrategySocketServer", "Server is not running");
                    return true;
                }

                _isRunning = false;
                _cancellationTokenSource?.Cancel();
            }

            try
            {
                // Закрываем все клиентские соединения
                lock (_clientsLock)
                {
                    foreach (var client in _clients.ToList())
                    {
                        try
                        {
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogTagInfo("JForexStrategySocketServer", $"Error closing client connection: {ex.Message}");
                        }
                    }
                    _clients.Clear();
                }

                // Останавливаем слушатель
                _tcpListener?.Stop();
                
                Logger.LogTagInfo("JForexStrategySocketServer", "JForex Strategy Socket Server stopped");
                OnServerStatusChanged("Server stopped");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogTagInfo("JForexStrategySocketServer", $"Error stopping JForex Strategy Socket Server: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отправка JSON команды всем подключенным клиентам
        /// </summary>
        public async Task<bool> SendCommandToAllClientsAsync(string command, string symbol = null)
        {
            if (!IsRunning)
            {
                Logger.LogTagInfo("JForexStrategySocketServer", "Cannot send command: server is not running");
                return false;
            }

            var commandData = new
            {
                command = command,
                symbol = symbol
            };

            var jsonCommand = JsonSerializer.Serialize(commandData);
            return await SendJsonToAllClientsAsync(jsonCommand);
        }

        /// <summary>
        /// Отправка JSON команды для очистки графика
        /// </summary>
        public async Task<bool> SendClearChartCommandAsync(string symbol)
        {
            return await SendCommandToAllClientsAsync("clear_chart", symbol);
        }

        /// <summary>
        /// Отправка произвольного JSON всем клиентам
        /// </summary>
        public async Task<bool> SendJsonToAllClientsAsync(string jsonMessage)
        {
            if (!IsRunning)
            {
                Logger.LogTagInfo("JForexStrategySocketServer", "Cannot send JSON: server is not running");
                return false;
            }

            var message = jsonMessage + "\n";
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var disconnectedClients = new List<TcpClient>();

            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        if (client.Connected)
                        {
                            client.GetStream().Write(messageBytes, 0, messageBytes.Length);
                            Logger.LogTagInfo("JForexStrategySocketServer", $"Sent JSON command to client: {jsonMessage}");
                        }
                        else
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogTagInfo("JForexStrategySocketServer", $"Error sending JSON to client: {ex.Message}");
                        disconnectedClients.Add(client);
                    }
                }

                // Удаляем отключенных клиентов
                foreach (var client in disconnectedClients)
                {
                    _clients.Remove(client);
                    try
                    {
                        client.Close();
                    }
                    catch { }
                }
            }

            return true;
        }

        /// <summary>
        /// Принятие клиентских соединений
        /// </summary>
        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    var clientEndPoint = client.Client.RemoteEndPoint.ToString();
                    
                    Logger.LogTagInfo("JForexStrategySocketServer", $"New client connected: {clientEndPoint}");
                    OnClientConnected(clientEndPoint);

                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                    }

                    // Запускаем обработку клиента в отдельной задаче
                    _ = Task.Run(() => HandleClientAsync(client, clientEndPoint, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogTagInfo("JForexStrategySocketServer", $"Error accepting client: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Обработка клиентского соединения
        /// </summary>
        private async Task HandleClientAsync(TcpClient client, string clientEndPoint, CancellationToken cancellationToken)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0)
                        {
                            break; // Клиент отключился
                        }

                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        var messages = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var msg in messages)
                        {
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                ProcessMessage(msg.Trim(), clientEndPoint);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogTagInfo("JForexStrategySocketServer", $"Error reading from client {clientEndPoint}: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagInfo("JForexStrategySocketServer", $"Error handling client {clientEndPoint}: {ex.Message}");
            }
            finally
            {
                // Удаляем клиента из списка
                lock (_clientsLock)
                {
                    _clients.Remove(client);
                }

                try
                {
                    client.Close();
                }
                catch { }

                Logger.LogTagInfo("JForexStrategySocketServer", $"Client disconnected: {clientEndPoint}");
                OnClientDisconnected(clientEndPoint);
            }
        }

        /// <summary>
        /// Обработка входящего сообщения от клиента
        /// </summary>
        private void ProcessMessage(string message, string clientEndPoint)
        {
            try
            {
                Logger.LogTagInfo("JForexStrategySocketServer", $"Received message from {clientEndPoint}: {message}");
                OnMessageReceived($"[{clientEndPoint}] {message}");

                // Пытаемся парсить как JSON
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(message);
                    
                    // Обрабатываем разные типы сообщений
                    if (jsonElement.TryGetProperty("type", out var typeElement))
                    {
                        var messageType = typeElement.GetString();
                        
                        switch (messageType)
                        {
                            case "command_response":
                                ProcessCommandResponse(jsonElement, clientEndPoint);
                                break;
                            case "status":
                                ProcessStatusMessage(jsonElement, clientEndPoint);
                                break;
                            default:
                                ProcessEventMessage(jsonElement, clientEndPoint);
                                break;
                        }
                    }
                    else
                    {
                        ProcessEventMessage(jsonElement, clientEndPoint);
                    }
                }
                catch (JsonException)
                {
                    // Если не JSON, обрабатываем как обычный текст
                    Logger.LogTagInfo("JForexStrategySocketServer", $"Received text message from {clientEndPoint}: {message}");
                }
            }
            catch (Exception ex)
            {
                                    Logger.LogTagInfo("JForexStrategySocketServer", $"Error processing message from {clientEndPoint}: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка ответа на команду
        /// </summary>
        private void ProcessCommandResponse(JsonElement jsonElement, string clientEndPoint)
        {
            var status = jsonElement.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : "unknown";
            var message = jsonElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "unknown";
            
            Logger.LogTagInfo("JForexStrategySocketServer", $"Command response from {clientEndPoint} - Status: {status}, Message: {message}");
        }

        /// <summary>
        /// Обработка статусного сообщения
        /// </summary>
        private void ProcessStatusMessage(JsonElement jsonElement, string clientEndPoint)
        {
            var totalObjectsFound = jsonElement.TryGetProperty("totalObjectsFound", out var foundElement) ? foundElement.GetInt32() : 0;
            var totalObjectsSent = jsonElement.TryGetProperty("totalObjectsSent", out var sentElement) ? sentElement.GetInt32() : 0;
            var totalErrors = jsonElement.TryGetProperty("totalErrors", out var errorsElement) ? errorsElement.GetInt32() : 0;
            var totalSocketMessagesSent = jsonElement.TryGetProperty("totalSocketMessagesSent", out var socketMsgElement) ? socketMsgElement.GetInt32() : 0;
            var totalSocketErrors = jsonElement.TryGetProperty("totalSocketErrors", out var socketErrElement) ? socketErrElement.GetInt32() : 0;
            var totalCommandsReceived = jsonElement.TryGetProperty("totalCommandsReceived", out var cmdRecvElement) ? cmdRecvElement.GetInt32() : 0;
            var totalCommandsProcessed = jsonElement.TryGetProperty("totalCommandsProcessed", out var cmdProcElement) ? cmdProcElement.GetInt32() : 0;

            Logger.LogTagInfo("JForexStrategySocketServer", $"Status from {clientEndPoint} - Objects Found: {totalObjectsFound}, Sent: {totalObjectsSent}, Errors: {totalErrors}, " +
                          $"Socket Messages: {totalSocketMessagesSent}, Socket Errors: {totalSocketErrors}, " +
                          $"Commands Received: {totalCommandsReceived}, Processed: {totalCommandsProcessed}");
        }

        /// <summary>
        /// Обработка сообщения о событии
        /// </summary>
        private void ProcessEventMessage(JsonElement jsonElement, string clientEndPoint)
        {
            var eventType = jsonElement.TryGetProperty("event", out var eventElement) ? eventElement.GetString() : "unknown";
            var symbol = jsonElement.TryGetProperty("symbol", out var symbolElement) ? symbolElement.GetString() : "unknown";
            var className = jsonElement.TryGetProperty("className", out var classElement) ? classElement.GetString() : "unknown";
            var objectType = jsonElement.TryGetProperty("objectType", out var typeElement) ? typeElement.GetString() : "unknown";
            var price = jsonElement.TryGetProperty("price", out var priceElement) ? priceElement.GetString() : "unknown";
            var timestamp = jsonElement.TryGetProperty("timestamp", out var timeElement) ? timeElement.GetString() : "unknown";

            Logger.LogTagInfo("JForexStrategySocketServer", $"Event from {clientEndPoint} - Event: {eventType}, Symbol: {symbol}, Class: {className}, " +
                          $"Type: {objectType}, Price: {price}, Timestamp: {timestamp}");

            // Если есть дополнительные данные
            if (jsonElement.TryGetProperty("additionalData", out var additionalDataElement))
            {
                var additionalData = additionalDataElement.GetRawText();
                Logger.LogTagInfo("JForexStrategySocketServer", $"Additional data: {additionalData}");
            }
        }

        /// <summary>
        /// Вызов события получения сообщения
        /// </summary>
        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        /// <summary>
        /// Вызов события подключения клиента
        /// </summary>
        protected virtual void OnClientConnected(string clientEndPoint)
        {
            ClientConnected?.Invoke(this, clientEndPoint);
        }

        /// <summary>
        /// Вызов события отключения клиента
        /// </summary>
        protected virtual void OnClientDisconnected(string clientEndPoint)
        {
            ClientDisconnected?.Invoke(this, clientEndPoint);
        }

        /// <summary>
        /// Вызов события изменения статуса сервера
        /// </summary>
        protected virtual void OnServerStatusChanged(string status)
        {
            ServerStatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                StopAsync().Wait(5000); // Ждем максимум 5 секунд
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during disposal: {ex.Message}");
            }

            _cancellationTokenSource?.Dispose();
        }
    }
} 