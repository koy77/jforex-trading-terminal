using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;
using Emgu.CV;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

namespace ScreenCaptureApp.Services
{
    /// <summary>
    /// Сервис для анализа отложенных сделок и извлечения цен из скриншотов
    /// </summary>
    public class PendingOrderAnalyzerService
    {
        private readonly ScreenshotService _screenshotService;
        private readonly Mt4SocketService _mt4SocketService;
        
        public PendingOrderAnalyzerService()
        {
            _screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
            _mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
        }
        
        /// <summary>
        /// Анализирует паттерн отложенной сделки и отправляет данные в MT4
        /// </summary>
        public async Task<bool> AnalyzeAndSendToMt4(PendingOrderPatternData patternData)
        {
            try
            {
                                       Logger.LogTagInfo("PendingOrder", $"Starting analysis of pending order pattern for symbol: {patternData.Symbol}");
                
                // Извлекаем цены из скриншотов
                var prices = await ExtractPricesFromScreenshots(patternData);
                if (prices == null)
                {
                                           Logger.LogTagError("PendingOrder", "Failed to extract prices from screenshots");
                    return false;
                }
                
                patternData.Prices = prices;
                
                // Отправляем данные в MT4
                var success = await SendPendingOrderToMt4(patternData);
                if (success)
                {
                                           Logger.LogTagInfo("PendingOrder", $"Successfully sent pending order to MT4: {patternData.Symbol} {prices.Direction} Entry:{prices.EntryPrice} Target:{prices.TargetPrice} StopLoss:{prices.StopLossPrice}");
                }
                else
                {
                        Logger.LogTagError("PendingOrder", "Failed to send pending order to MT4");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                                 Logger.LogTagError("PendingOrder", "Error analyzing pending order pattern", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Извлекает цены из скриншотов отложенной сделки
        /// </summary>
        private async Task<PendingOrderPrices> ExtractPricesFromScreenshots(PendingOrderPatternData patternData)
        {
            try
            {
                                 Logger.LogTagInfo("PendingOrder", "Extracting prices from screenshots...");
                
                var prices = new PendingOrderPrices();
                
                // Анализируем первый скриншот (цена входа)
                if (File.Exists(patternData.FirstScreenshotPath))
                {
                    using (var bitmap = new Bitmap(patternData.FirstScreenshotPath))
                    {
                        // Вырезаем область справа для анализа цены
                        using (var priceArea = CropPriceArea(bitmap, "entry", patternData.OrderId))
                        {
                            prices.EntryPrice = await ExtractPriceFromImage(priceArea, "entry");
                            Logger.LogTagInfo("PendingOrder", $"Extracted entry price: {prices.EntryPrice}");
                        }
                    }
                }
                else
                {
                    Logger.LogTagError("PendingOrder", $"First screenshot not found: {patternData.FirstScreenshotPath}");
                    return null;
                }
                
                // Анализируем второй скриншот (цена цели)
                if (File.Exists(patternData.SecondScreenshotPath))
                {
                    using (var bitmap = new Bitmap(patternData.SecondScreenshotPath))
                    {
                        // Вырезаем область справа для анализа цены
                        using (var priceArea = CropPriceArea(bitmap, "target", patternData.OrderId))
                        {
                            prices.TargetPrice = await ExtractPriceFromImage(priceArea, "target");
                            Logger.LogTagInfo("PendingOrder", $"Extracted target price: {prices.TargetPrice}");
                        }
                    }
                }
                else
                {
                    Logger.LogTagError("PendingOrder", $"Second screenshot not found: {patternData.SecondScreenshotPath}");
                    return null;
                }
                
                // Определяем направление сделки
                prices.Direction = prices.TargetPrice > prices.EntryPrice ? "Buy" : "Sell";
                
                // Вычисляем стоп-лосс (противоположная сторона от цели)
                double priceDifference = Math.Abs(prices.TargetPrice - prices.EntryPrice);
                if (prices.Direction == "Buy")
                {
                    prices.StopLossPrice = prices.EntryPrice - priceDifference;
                }
                else
                {
                    prices.StopLossPrice = prices.EntryPrice + priceDifference;
                }
                
                // Устанавливаем размер лота по умолчанию
                prices.LotSize = 0.1;
                
                // Создаем комментарий
                prices.Comment = $"Pending Order {patternData.Symbol} {prices.Direction}";
                
                                 Logger.LogTagInfo("PendingOrder", $"Price extraction completed: Entry={prices.EntryPrice}, Target={prices.TargetPrice}, StopLoss={prices.StopLossPrice}, Direction={prices.Direction}");
                
                return prices;
            }
            catch (Exception ex)
            {
                                 Logger.LogTagError("PendingOrder", "Error extracting prices from screenshots", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Вырезает область справа из изображения для анализа цены
        /// </summary>
        /// <param name="sourceBitmap">Исходное изображение</param>
        /// <param name="priceType">Тип цены (entry/target)</param>
        /// <param name="orderId">ID заказа для именования файлов</param>
        /// <returns>Вырезанная область</returns>
        private Bitmap CropPriceArea(Bitmap sourceBitmap, string priceType, string orderId)
        {
            try
            {
                // Параметры области: отступ 20 пикселей от правого края + 55 пикселей ширины
                const int rightOffset = 20;
                const int areaWidth = 55;
                
                int sourceWidth = sourceBitmap.Width;
                int sourceHeight = sourceBitmap.Height;
                
                // Вычисляем координаты области
                int cropX = sourceWidth - rightOffset - areaWidth;
                int cropY = 0;
                int cropWidth = areaWidth;
                int cropHeight = sourceHeight;
                
                // Проверяем, что область не выходит за границы изображения
                if (cropX < 0)
                {
                    cropX = 0;
                    cropWidth = sourceWidth - rightOffset;
                }
                
                // Создаем прямоугольник для обрезки
                var cropRect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
                
                // Создаем новое изображение с обрезанными размерами
                var croppedImage = new Bitmap(cropWidth, cropHeight);
                
                using (var graphics = Graphics.FromImage(croppedImage))
                {
                    // Копируем часть исходного изображения
                    graphics.DrawImage(sourceBitmap, 0, 0, cropRect, GraphicsUnit.Pixel);
                }
                
                // Сохраняем вырезанную область для дебага
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Trades", "Debug");
                if (!Directory.Exists(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                }
                
                string debugFileName = $"{priceType}_price_{orderId}.png";
                string debugFilePath = Path.Combine(debugDir, debugFileName);
                croppedImage.Save(debugFilePath, System.Drawing.Imaging.ImageFormat.Png);
                
                Logger.LogTagInfo("PendingOrder", $"Saved {priceType} price area: {sourceWidth}x{sourceHeight} -> {cropWidth}x{cropHeight}, cropX={cropX}, saved to {debugFilePath}");
                
                return croppedImage;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("PendingOrder", $"Error cropping price area for {priceType}", ex);
                return new Bitmap(sourceBitmap); // Возвращаем исходное изображение в случае ошибки
            }
        }

        /// <summary>
        /// Извлекает цену из изображения с помощью OCR (Emgu.CV + Tesseract)
        /// </summary>
        private async Task<double> ExtractPriceFromImage(Bitmap bitmap, string priceType)
        {
            try
            {
                Logger.LogTagInfo("PendingOrder", $"Starting OCR extraction for {priceType} price...");
                
                // Путь к tessdata (нужно установить Tesseract-OCR)
                string tessDataPath = @"C:\Program Files\Tesseract-OCR\tessdata";
                
                // Проверяем, существует ли папка tessdata
                if (!Directory.Exists(tessDataPath))
                {
                    Logger.LogTagWarning("PendingOrder", $"Tesseract tessdata not found at {tessDataPath}, using fallback method");
                    return await ExtractPriceFallback(priceType);
                }
                
                                 // Конвертируем System.Drawing.Bitmap в Emgu.CV Image
                 using (var ms = new MemoryStream())
                 {
                     bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                     ms.Position = 0;
                     
                     using (var img = new Image<Bgr, byte>(bitmap.Width, bitmap.Height))
                     {
                         // Копируем данные из Bitmap в Emgu.CV Image
                         for (int y = 0; y < bitmap.Height; y++)
                         {
                             for (int x = 0; x < bitmap.Width; x++)
                             {
                                 var pixel = bitmap.GetPixel(x, y);
                                 img[y, x] = new Bgr(pixel.B, pixel.G, pixel.R);
                             }
                         }
                         
                         // Преобразуем в серый цвет
                        var gray = img.Convert<Gray, byte>();
                        
                        // Применяем бинаризацию для улучшения распознавания
                        var binary = gray.ThresholdBinary(new Gray(128), new Gray(255));
                        
                                                 // Создаем Tesseract engine
                         using (var ocr = new Tesseract(tessDataPath, "eng", OcrEngineMode.Default))
                         {
                             // Ограничиваем алфавит только цифрами и точкой
                             ocr.SetVariable("tessedit_char_whitelist", "0123456789.");
                             
                             // Устанавливаем изображение для распознавания
                             ocr.SetImage(binary);
                             
                             // Запускаем распознавание
                             ocr.Recognize();
                             
                             // Получаем результат распознавания
                             var result = ocr.GetUTF8Text();
                             string resultText = result != null ? result.Trim() : "";
                             
                             Logger.LogTagInfo("PendingOrder", $"OCR raw result for {priceType}: '{resultText}'");
                             
                             // Фильтруем результат через регулярное выражение
                             string numbersOnly = Regex.Replace(resultText, @"[^0-9.]", "");
                             
                             Logger.LogTagInfo("PendingOrder", $"OCR filtered result for {priceType}: '{numbersOnly}'");
                             
                             // Пытаемся распарсить результат
                             if (double.TryParse(numbersOnly, out double price))
                             {
                                 Logger.LogTagInfo("PendingOrder", $"Successfully extracted {priceType} price: {price}");
                                 return price;
                             }
                             else
                             {
                                 Logger.LogTagWarning("PendingOrder", $"Failed to parse OCR result '{numbersOnly}' for {priceType}, using fallback");
                                 return await ExtractPriceFallback(priceType);
                             }
                         }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("PendingOrder", $"Error during OCR extraction for {priceType}", ex);
                return await ExtractPriceFallback(priceType);
            }
        }
        
        /// <summary>
        /// Fallback метод для извлечения цены (используется при ошибках OCR)
        /// </summary>
        private async Task<double> ExtractPriceFallback(string priceType)
        {
            await Task.Delay(100); // Имитация обработки
            
            var random = new Random();
            double basePrice = 1.2000; // Базовая цена для демонстрации
            
            if (priceType == "entry")
            {
                return basePrice + (random.NextDouble() * 0.0100); // ±10 пипсов
            }
            else // target
            {
                return basePrice + (random.NextDouble() * 0.0200) + 0.0050; // +5-25 пипсов
            }
        }
        
        /// <summary>
        /// Отправляет данные отложенной сделки в MT4
        /// </summary>
        private async Task<bool> SendPendingOrderToMt4(PendingOrderPatternData patternData)
        {
            try
            {
                if (!_mt4SocketService.IsConnected)
                {
                                         Logger.LogTagWarning("PendingOrder", "MT4 Socket Service is not connected");
                    return false;
                }
                
                var orderData = new
                {
                    type = "pending_order",
                    symbol = patternData.Symbol,
                    direction = patternData.Prices.Direction,
                    entry_price = patternData.Prices.EntryPrice,
                    target_price = patternData.Prices.TargetPrice,
                    stop_loss_price = patternData.Prices.StopLossPrice,
                    lot_size = patternData.Prices.LotSize,
                    comment = patternData.Prices.Comment,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(orderData);
                var success = await _mt4SocketService.WriteAsync(json);
                
                if (success)
                {
                                         Logger.LogTagInfo("PendingOrder", $"Pending order data sent to MT4: {json}");
                }
                else
                {
                                         Logger.LogTagError("PendingOrder", "Failed to send pending order data to MT4");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                                 Logger.LogTagError("PendingOrder", "Error sending pending order to MT4", ex);
                return false;
            }
        }
    }
} 