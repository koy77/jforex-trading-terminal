using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

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
                        prices.EntryPrice = await ExtractPriceFromImage(bitmap, "entry");
                                                 Logger.LogTagInfo("PendingOrder", $"Extracted entry price: {prices.EntryPrice}");
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
                        prices.TargetPrice = await ExtractPriceFromImage(bitmap, "target");
                                                 Logger.LogTagInfo("PendingOrder", $"Extracted target price: {prices.TargetPrice}");
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
        /// Извлекает цену из изображения (заглушка - в реальности здесь будет OCR)
        /// </summary>
        private async Task<double> ExtractPriceFromImage(Bitmap bitmap, string priceType)
        {
            // TODO: Здесь должна быть реализация OCR для извлечения цены из изображения
            // Пока возвращаем случайную цену для демонстрации
            
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