using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Services
{
    /// <summary>
    /// HTTP сервис для приема данных от GForex
    /// </summary>
    public class HttpServerService : IDisposable
    {
        private HttpListener _listener;
        private bool _isRunning;
        private readonly int _port;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _listenerTask;

        /// <summary>
        /// Событие, возникающее при получении данных ценового уровня
        /// </summary>
        public event EventHandler<PriceLevelData> PriceLevelReceived;

        /// <summary>
        /// Событие изменения статуса сервера
        /// </summary>
        public event EventHandler<string> StatusChanged;

        /// <summary>
        /// Список последних полученных данных (для отладки)
        /// </summary>
        public List<PriceLevelData> RecentPriceLevels { get; private set; }

        public HttpServerService(int port = 7000)
        {
            _port = port;
            _cancellationTokenSource = new CancellationTokenSource();
            RecentPriceLevels = new List<PriceLevelData>();
            
            Logger.LogInfo($"HTTP Server Service initialized for port {_port}");
        }

        /// <summary>
        /// Запускает HTTP сервер
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning)
            {
                Logger.LogWarning("HTTP Server is already running");
                return;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                
                _listener.Start();
                _isRunning = true;
                
                OnStatusChanged($"HTTP Server started on port {_port}");
                Logger.LogInfo($"HTTP Server started successfully on port {_port}");

                _listenerTask = Task.Run(async () => await ListenAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start HTTP Server on port {_port}", ex);
                OnStatusChanged($"Failed to start HTTP Server: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Останавливает HTTP сервер
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                Logger.LogWarning("HTTP Server is not running");
                return;
            }

            try
            {
                _cancellationTokenSource.Cancel();
                _listener?.Stop();
                _isRunning = false;
                
                if (_listenerTask != null)
                {
                    await _listenerTask;
                }
                
                OnStatusChanged("HTTP Server stopped");
                Logger.LogInfo("HTTP Server stopped successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to stop HTTP Server", ex);
                OnStatusChanged($"Failed to stop HTTP Server: {ex.Message}");
            }
        }

        /// <summary>
        /// Основной цикл прослушивания HTTP запросов
        /// </summary>
        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(async () => await ProcessRequestAsync(context), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    // Сервер остановлен
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in HTTP listener loop", ex);
                }
            }
        }

        /// <summary>
        /// Обрабатывает входящий HTTP запрос
        /// </summary>
        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                Logger.LogDebug($"HTTP Request: {request.HttpMethod} {request.Url?.AbsolutePath}");

                switch (request.Url?.AbsolutePath?.ToLower())
                {
                    case "/pricelevel":
                    case "/api/pricelevel":
                        await HandlePriceLevelRequestAsync(request, response);
                        break;
                    
                    case "/health":
                    case "/api/health":
                        await HandleHealthRequestAsync(response);
                        break;
                    
                    case "/status":
                    case "/api/status":
                        await HandleStatusRequestAsync(response);
                        break;
                    
                    default:
                        await HandleNotFoundAsync(response);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing HTTP request", ex);
                await HandleErrorAsync(context.Response, ex);
            }
        }

        /// <summary>
        /// Обрабатывает запрос ценового уровня
        /// </summary>
        private async Task HandlePriceLevelRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (request.HttpMethod != "POST")
                {
                    await SendResponseAsync(response, "Method not allowed", 405);
                    return;
                }

                // Читаем тело запроса
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                Logger.LogDebug($"Received price level data: {requestBody}");

                // Предобработка JSON для исправления неправильных форматов чисел
                var processedRequestBody = PreprocessJsonForNumberFormat(requestBody);

                // Настройки для десериализации JSON
                var settings = new JsonSerializerSettings
                {
                    Error = (sender, args) =>
                    {
                        Logger.LogWarning($"JSON parsing warning: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    }
                };

                // Парсим JSON с настройками
                var priceLevelData = JsonConvert.DeserializeObject<PriceLevelData>(processedRequestBody, settings);
                
                if (priceLevelData == null || string.IsNullOrEmpty(priceLevelData.Symbol))
                {
                    await SendResponseAsync(response, "Invalid data: symbol is required", 400);
                    return;
                }

                // Устанавливаем значение по умолчанию для поля type если оно не указано (обратная совместимость)
                if (string.IsNullOrEmpty(priceLevelData.Type))
                {
                    priceLevelData.Type = "unknown";
                    Logger.LogInfo("Type field not provided, using default value 'unknown' for backward compatibility");
                }

                // Добавляем в список последних данных (максимум 100 записей)
                lock (RecentPriceLevels)
                {
                    RecentPriceLevels.Add(priceLevelData);
                    if (RecentPriceLevels.Count > 100)
                    {
                        RecentPriceLevels.RemoveAt(0);
                    }
                }

                // Вызываем событие
                OnPriceLevelReceived(priceLevelData);

                // Отправляем успешный ответ
                var responseData = new { success = true, message = "Price level data received", timestamp = DateTime.Now };
                await SendJsonResponseAsync(response, responseData, 200);

                Logger.LogInfo($"Price level processed: {priceLevelData}");
            }
            catch (JsonException ex)
            {
                Logger.LogError("Invalid JSON in price level request", ex);
                await SendResponseAsync(response, "Invalid JSON format", 400);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing price level request", ex);
                await SendResponseAsync(response, "Internal server error", 500);
            }
        }

        /// <summary>
        /// Обрабатывает запрос проверки здоровья сервера
        /// </summary>
        private async Task HandleHealthRequestAsync(HttpListenerResponse response)
        {
            var healthData = new
            {
                status = "healthy",
                timestamp = DateTime.Now,
                uptime = DateTime.Now - Process.GetCurrentProcess().StartTime,
                isRunning = _isRunning
            };
            
            await SendJsonResponseAsync(response, healthData, 200);
        }

        /// <summary>
        /// Обрабатывает запрос статуса сервера
        /// </summary>
        private async Task HandleStatusRequestAsync(HttpListenerResponse response)
        {
            var statusData = new
            {
                isRunning = _isRunning,
                port = _port,
                recentPriceLevelsCount = RecentPriceLevels.Count,
                timestamp = DateTime.Now
            };
            
            await SendJsonResponseAsync(response, statusData, 200);
        }

        /// <summary>
        /// Обрабатывает запросы к несуществующим endpoint'ам
        /// </summary>
        private async Task HandleNotFoundAsync(HttpListenerResponse response)
        {
            var notFoundData = new
            {
                error = "Not Found",
                message = "The requested endpoint does not exist",
                availableEndpoints = new[] { "/pricelevel", "/health", "/status" },
                timestamp = DateTime.Now
            };
            
            await SendJsonResponseAsync(response, notFoundData, 404);
        }

        /// <summary>
        /// Обрабатывает ошибки
        /// </summary>
        private async Task HandleErrorAsync(HttpListenerResponse response, Exception ex)
        {
            var errorData = new
            {
                error = "Internal Server Error",
                message = ex.Message,
                timestamp = DateTime.Now
            };
            
            await SendJsonResponseAsync(response, errorData, 500);
        }

        /// <summary>
        /// Отправляет JSON ответ
        /// </summary>
        private async Task SendJsonResponseAsync(HttpListenerResponse response, object data, int statusCode)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await SendResponseAsync(response, json, statusCode, "application/json");
        }

        /// <summary>
        /// Отправляет текстовый ответ
        /// </summary>
        private async Task SendResponseAsync(HttpListenerResponse response, string content, int statusCode, string contentType = "text/plain")
        {
            response.StatusCode = statusCode;
            response.ContentType = contentType;
            response.ContentEncoding = Encoding.UTF8;

            var buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        /// <summary>
        /// Вызывает событие получения ценового уровня
        /// </summary>
        protected virtual void OnPriceLevelReceived(PriceLevelData priceLevelData)
        {
            PriceLevelReceived?.Invoke(this, priceLevelData);
        }

        /// <summary>
        /// Вызывает событие изменения статуса
        /// </summary>
        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Получает последние данные ценовых уровней
        /// </summary>
        public List<PriceLevelData> GetRecentPriceLevels()
        {
            lock (RecentPriceLevels)
            {
                return new List<PriceLevelData>(RecentPriceLevels);
            }
        }

        /// <summary>
        /// Очищает список последних данных
        /// </summary>
        public void ClearRecentPriceLevels()
        {
            lock (RecentPriceLevels)
            {
                RecentPriceLevels.Clear();
            }
        }

        /// <summary>
        /// Предобрабатывает JSON для исправления неправильных форматов чисел
        /// </summary>
        private string PreprocessJsonForNumberFormat(string json)
        {
            try
            {
                // Исправляем числа с запятыми как десятичными разделителями
                // Паттерн: "price":число,число -> "price":число.число
                var regex = new System.Text.RegularExpressions.Regex(@"""(price|Price)"":\s*(\d+),(\d+)");
                var processedJson = regex.Replace(json, match =>
                {
                    var prefix = match.Groups[1].Value;
                    var wholePart = match.Groups[2].Value;
                    var decimalPart = match.Groups[3].Value;
                    return $"\"{prefix}\": {wholePart}.{decimalPart}";
                });

                Logger.LogDebug($"Preprocessed JSON: {processedJson}");
                return processedJson;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error preprocessing JSON: {ex.Message}");
                return json; // Возвращаем оригинальный JSON если обработка не удалась
            }
        }

        public void Dispose()
        {
            try
            {
                StopAsync().Wait(5000); // Ждем максимум 5 секунд
                _cancellationTokenSource?.Dispose();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error disposing HTTP Server Service", ex);
            }
        }
    }
} 