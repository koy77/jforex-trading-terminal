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
using System.Linq;
using ScreenCaptureApp.Helpers;

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
        /// Событие нового ценового уровня (для обратной совместимости)
        /// </summary>
        public event EventHandler<PriceLevelEventData> NewPriceLevelReceived;

        /// <summary>
        /// Событие нового JForex объекта
        /// </summary>
        public event EventHandler<JForexChartObjectData> NewJForexChartObject;

        /// <summary>
        /// Событие нового бара
        /// </summary>
        public event EventHandler<dynamic> NewBarReceived;

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

                // Логируем входящий запрос по тегу
                Logger.LogTagInfo("http_service", $"HTTP Request: {request.HttpMethod} {request.Url?.AbsolutePath}");
                Logger.LogDebug($"HTTP Request: {request.HttpMethod} {request.Url?.AbsolutePath}");

                switch (request.Url?.AbsolutePath?.ToLower())
                {
                    case "/pricelevel":
                    case "/api/pricelevel":
                        await HandlePriceLevelRequestAsync(request, response);
                        break;
                    
                    case "/new-bar":
                    case "/api/new-bar":
                        await HandleNewBarRequestAsync(request, response);
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
                Logger.LogTagError("http_service", "Error processing HTTP request", ex);
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
                    var errorResponse = "Method not allowed";
                    Logger.LogTagInfo("http_service", $"Server Response (405): {errorResponse}");
                    await SendResponseAsync(response, errorResponse, 405);
                    return;
                }

                // Читаем тело запроса
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                // Логируем входящие данные по тегу
                Logger.LogTagInfo("http_service", $"Price Level Input Data (raw): {requestBody}");
                Logger.LogDebug($"Received price level data: {requestBody}");

                // Предобработка JSON для исправления неправильных форматов чисел
                var processedRequestBody = PreprocessJsonForNumberFormat(requestBody);

                // Логируем предобработанные данные по тегу
                Logger.LogTagInfo("http_service", $"Preprocessed JSON Data: {processedRequestBody}");

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
                
                // Логируем результат десериализации
                Logger.LogTagInfo("http_service", $"Deserialized PriceLevelData: Symbol='{priceLevelData?.Symbol}', Price={priceLevelData?.Price}, Type='{priceLevelData?.Type}', AdditionalData='{priceLevelData?.AdditionalData}'");
                
                if (priceLevelData == null || string.IsNullOrEmpty(priceLevelData.Symbol))

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

                // Создаем и вызываем событие нового ценового уровня (для обратной совместимости)
                var priceLevelEvent = new PriceLevelEventData(
                    name: priceLevelData.AdditionalData?.ToString() ?? "Price Level",
                    symbol: priceLevelData.Symbol,
                    levelValue: priceLevelData.Price,
                    type: priceLevelData.Type,
                    additionalData: priceLevelData.AdditionalData?.ToString() ?? ""
                );
                OnNewPriceLevelReceived(priceLevelEvent);

                // Создаем и вызываем новое событие JForex объекта
                // Получаем тип объекта из поля Type
                var jforexObjectType = JForexChartObjectData.GetObjectTypeFromClassName(priceLevelData.Type);
                var jforexChartObject = new JForexChartObjectData(
                    symbol: priceLevelData.Symbol,
                    price: priceLevelData.Price,
                    objectType: jforexObjectType,
                    className: priceLevelData.Type,
                    additionalData: priceLevelData.AdditionalData?.ToString() ?? ""
                );
                OnNewJForexChartObject(jforexChartObject);

                // Отправляем успешный ответ
                var responseData = new { success = true, message = "JForex chart object processed successfully", timestamp = DateTime.Now };
                var responseJson = JsonConvert.SerializeObject(responseData, Formatting.Indented);
                await SendJsonResponseAsync(response, responseData, 200);

                // Логируем обработанные данные по тегу
                Logger.LogTagInfo("http_service", $"New JForex Chart Object: {jforexChartObject}");
                Logger.LogTagInfo("http_service", $"Server Response (200): {responseJson}");
                Logger.LogInfo($"Price level processed: {priceLevelData}");
            }
            catch (JsonException ex)
            {
                Logger.LogError("Invalid JSON in price level request", ex);
                Logger.LogTagError("http_service", "Invalid JSON in price level request", ex);
                var errorResponse = "Invalid JSON format";
                Logger.LogTagInfo("http_service", $"Server Response (400): {errorResponse}");
                await SendResponseAsync(response, errorResponse, 400);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing price level request", ex);
                Logger.LogTagError("http_service", "Error processing price level request", ex);
                var errorResponse = "Internal server error";
                Logger.LogTagInfo("http_service", $"Server Response (500): {errorResponse}");
                await SendResponseAsync(response, errorResponse, 500);
            }
        }

        /// <summary>
        /// Обрабатывает запрос нового бара от JForex стратегии
        /// </summary>
        private async Task HandleNewBarRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (request.HttpMethod != "POST")
                {
                    var errorResponse = "Method not allowed";
                    Logger.LogTagInfo("http_service", $"Server Response (405): {errorResponse}");
                    await SendResponseAsync(response, errorResponse, 405);
                    return;
                }

                // Читаем тело запроса
                string requestBody;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                // Логируем входящие данные по тегу
                Logger.LogTagInfo("http_service", $"New Bar Input Data (raw): {requestBody}");
                Logger.LogDebug($"Received new bar data: {requestBody}");

                // Парсим JSON для извлечения данных
                var newBarData = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                if (newBarData != null)
                {
                    // Логируем все поля из JSON
                    Logger.LogTagInfo("http_service", $"New Bar Data - Symbol: {newBarData.symbol}, ChartKey: {newBarData.chartKey}");
                    Logger.LogTagInfo("http_service", $"New Bar Data - FeedType: {newBarData.feedType}, TickBarSize: {newBarData.tickBarSize}");
                    Logger.LogTagInfo("http_service", $"New Bar Data - BarStartTime: {newBarData.barStartTime}, ChartInfo: {newBarData.chartInfo}");
                    Logger.LogTagInfo("http_service", $"New Bar Data - TickTime: {newBarData.tickTime}, TickPrice: {newBarData.tickPrice}");
                    Logger.LogTagInfo("http_service", $"New Bar Data - Timestamp: {newBarData.timestamp}, Strategy: {newBarData.strategy}");
                    
                    Logger.LogInfo($"New bar detected: {newBarData.symbol} on chart {newBarData.chartKey} at {newBarData.timestamp}");
                    
                    // Вызываем событие нового бара
                    OnNewBarReceived(newBarData);
                    
                    // Сдвигаем торговые штрихи в базе данных
                    _ = Task.Run(async () => await ShiftTradingStrokesInDatabase(newBarData));
                }

                // Отправляем успешный ответ
                var responseData = new { success = true, message = "New bar data received and logged", timestamp = DateTime.Now };
                var responseJson = JsonConvert.SerializeObject(responseData, Formatting.Indented);
                await SendJsonResponseAsync(response, responseData, 200);

                // Логируем ответ по тегу
                Logger.LogTagInfo("http_service", $"Server Response (200): {responseJson}");
            }
            catch (JsonException ex)
            {
                Logger.LogError("Invalid JSON in new bar request", ex);
                Logger.LogTagError("http_service", "Invalid JSON in new bar request", ex);
                var errorResponse = "Invalid JSON format";
                Logger.LogTagInfo("http_service", $"Server Response (400): {errorResponse}");
                await SendResponseAsync(response, errorResponse, 400);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing new bar request", ex);
                Logger.LogTagError("http_service", "Error processing new bar request", ex);
                var errorResponse = "Internal server error";
                Logger.LogTagInfo("http_service", $"Server Response (500): {errorResponse}");
                await SendResponseAsync(response, errorResponse, 500);
            }
        }

        /// <summary>
        /// Обрабатывает запрос проверки здоровья сервера
        /// </summary>
        private async Task HandleHealthRequestAsync(HttpListenerResponse response)
        {
            // Логируем запрос здоровья по тегу
            Logger.LogTagInfo("http_service", "Health check request received");
            
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
            // Логируем запрос статуса по тегу
            Logger.LogTagInfo("http_service", "Status request received");
            
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
            // Логируем запрос к несуществующему endpoint по тегу
            Logger.LogTagInfo("http_service", "Not found endpoint request received");
            
            var notFoundData = new
            {
                error = "Not Found",
                message = "The requested endpoint does not exist",
                availableEndpoints = new[] { "/pricelevel", "/new-bar", "/health", "/status" },
                timestamp = DateTime.Now
            };
            
            await SendJsonResponseAsync(response, notFoundData, 404);
        }

        /// <summary>
        /// Обрабатывает ошибки
        /// </summary>
        private async Task HandleErrorAsync(HttpListenerResponse response, Exception ex)
        {
            // Логируем ошибку по тегу
            Logger.LogTagError("http_service", "Internal server error occurred", ex);
            
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
        /// Вызывает событие нового ценового уровня
        /// </summary>
        protected virtual void OnNewPriceLevelReceived(PriceLevelEventData priceLevelEvent)
        {
            NewPriceLevelReceived?.Invoke(this, priceLevelEvent);
        }

        /// <summary>
        /// Вызывает событие нового JForex объекта
        /// </summary>
        protected virtual void OnNewJForexChartObject(JForexChartObjectData jforexChartObject)
        {
            NewJForexChartObject?.Invoke(this, jforexChartObject);
        }

        /// <summary>
        /// Вызывает событие нового бара
        /// </summary>
        protected virtual void OnNewBarReceived(dynamic newBarData)
        {
            NewBarReceived?.Invoke(this, newBarData);
        }

        /// <summary>
        /// Сдвигает торговые штрихи в базе данных при получении нового бара
        /// </summary>
        private async Task ShiftTradingStrokesInDatabase(dynamic newBarData)
        {
            try
            {
                if (newBarData == null)
                {
                    Logger.LogWarning("Cannot shift trading strokes - NewBar data is null");
                    return;
                }

                // Извлекаем данные из NewBar
                string symbol = null;
                string feedType = null;
                int? tickBarSize = null;
                
                try
                {
                    symbol = newBarData.symbol?.ToString();
                    feedType = newBarData.feedType?.ToString();
                    if (newBarData.tickBarSize != null)
                    {
                        tickBarSize = Convert.ToInt32(newBarData.tickBarSize);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error extracting NewBar data: {ex.Message}");
                    return;
                }

                if (string.IsNullOrEmpty(symbol))
                {
                    Logger.LogWarning("Cannot shift trading strokes - symbol is null or empty");
                    return;
                }

                // Получаем DatabaseService
                var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                if (databaseService == null)
                {
                    Logger.LogWarning("Cannot shift trading strokes - DatabaseService is not available");
                    return;
                }

                // Получаем только отслеживаемые CaptureData с Source="trading_canvas"
                var tradingCaptures = databaseService.GetTrackingCaptures();

                Logger.LogInfo($"Found {tradingCaptures.Count} trading canvas captures to check for shifting");

                int shiftedCount = 0;

                foreach (var capture in tradingCaptures)
                {
                    try
                    {
                        // Проверяем символ
                        if (string.IsNullOrEmpty(capture.Symbol) || !capture.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Проверяем период для тиковых баров
                        bool shouldShift = false;
                        
                        if (!string.IsNullOrEmpty(feedType) && feedType.Equals("TICK_BAR", StringComparison.OrdinalIgnoreCase))
                        {
                            // Для тиковых баров проверяем, что период начинается с "T" и содержит размер тика
                            if (!string.IsNullOrEmpty(capture.Period) && 
                                capture.Period.StartsWith("T", StringComparison.OrdinalIgnoreCase) &&
                                tickBarSize.HasValue)
                            {
                                // Извлекаем число из периода (например, "T89" -> 89)
                                string periodNumber = capture.Period.Substring(1);
                                if (int.TryParse(periodNumber, out int periodTickSize) && periodTickSize == tickBarSize.Value)
                                {
                                    shouldShift = true;
                                    Logger.LogDebug($"TICK_BAR match: Symbol={symbol}, Period={capture.Period}, TickSize={tickBarSize}");
                                }
                            }
                        }
                        else
                        {
                            // Для обычных периодов просто проверяем символ
                            shouldShift = true;
                            Logger.LogDebug($"Regular period match: Symbol={symbol}, FeedType={feedType}");
                        }

                        if (shouldShift)
                        {
                            // Сдвигаем координаты влево
                            int newX = capture.X - (int)CanvasConstants.NEW_BAR_SHIFT_AMOUNT;
                            
                            // Обновляем координаты в базе данных
                            capture.X = newX;
                            databaseService.UpdateCapture(capture);
                            
                            shiftedCount++;
                            Logger.LogDebug($"Shifted trading stroke: ID={capture.ID}, OldX={capture.X + (int)CanvasConstants.NEW_BAR_SHIFT_AMOUNT}, NewX={newX}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error processing capture ID={capture.ID}: {ex.Message}", ex);
                    }
                }

                Logger.LogInfo($"Shifted {shiftedCount} trading strokes for symbol '{symbol}' due to new bar");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error shifting trading strokes in database: {ex.Message}", ex);
            }
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
                // Также обрабатываем levelValue, LevelValue и другие числовые поля
                var regex = new System.Text.RegularExpressions.Regex(@"""(price|Price|levelValue|LevelValue|entry_price|stop_loss|risk)"":\s*(\d+),(\d+)");
                var processedJson = regex.Replace(json, match =>
                {
                    var fieldName = match.Groups[1].Value;
                    var wholePart = match.Groups[2].Value;
                    var decimalPart = match.Groups[3].Value;
                    return $"\"{fieldName}\": {wholePart}.{decimalPart}";
                });

                Logger.LogDebug($"Preprocessed JSON: {processedJson}");
                Logger.LogTagInfo("http_service", $"JSON preprocessing completed: {processedJson}");
                return processedJson;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error preprocessing JSON: {ex.Message}");
                Logger.LogTagError("http_service", "Error preprocessing JSON", ex);
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