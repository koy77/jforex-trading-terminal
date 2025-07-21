using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Helpers;

namespace ScreenCaptureApp.Services
{
    // Аргументы события для обнаружения breakout
    public class BreakoutDetectionEventArgs : EventArgs
    {
        public CaptureData Capture { get; }
        public TrendlineBreakResult Result { get; }
        public string FileName { get; }
        public string DebugPath { get; }
        public DateTime Timestamp { get; }
        public double? Confidence { get; }

        public BreakoutDetectionEventArgs(CaptureData capture, TrendlineBreakResult result, string fileName, string debugPath, double? confidence = null)
        {
            Capture = capture;
            Result = result;
            FileName = fileName;
            DebugPath = debugPath;
            Confidence = confidence;
            Timestamp = DateTime.Now;
        }
    }

    // Аргументы события для процесса обработки
    public class ProcessingEventArgs : EventArgs
    {
        public string Message { get; }
        public int ProcessedCount { get; }
        public int TotalCount { get; }
        public DateTime Timestamp { get; }

        public ProcessingEventArgs(string message, int processedCount = 0, int totalCount = 0)
        {
            Message = message;
            ProcessedCount = processedCount;
            TotalCount = totalCount;
            Timestamp = DateTime.Now;
        }
    }

    public class CaptureTrackingService : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly ScreenshotService _screenshotService;
        private Timer _timer;
        private readonly string _trackingDir;
        private bool _isProcessing = false;

        // Единственное асинхронное событие для обнаружения breakout
        public event Func<CaptureData, TrendlineBreakResult, Task> BreakoutDetected;

        // Асинхронное событие окончания итерации трекинга
        public event Func<Task> CaptureTrackingIterationEnded;

        public CaptureTrackingService()
        {
            _databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            _screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
            _trackingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tracking", "captures");
            Directory.CreateDirectory(_trackingDir);
        }

        public void StartTracking()
        {
            if (_timer == null)
            {
                _timer = new Timer(async _ => await ProcessCapturesAsync(), null, 0, 5000);
                Logger.LogInfo("CaptureTrackingService: Трекинг запущен");
            }
            else
            {
                Logger.LogWarning("CaptureTrackingService: Трекинг уже запущен");
            }
        }

        public async Task RunOnceAsync()
        {
            await ProcessCapturesAsync();
        }

        private void ClearTrackingDirectory()
        {
            try
            {
                if (Directory.Exists(_trackingDir))
                {
                    foreach (string file in Directory.GetFiles(_trackingDir))
                    {
                        File.Delete(file);
                    }
                    Logger.LogInfo($"CaptureTrackingService: Папка трекинга очищена: {_trackingDir}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("CaptureTrackingService: Ошибка при очистке папки трекинга", ex);
            }
        }

        private async Task ProcessCapturesAsync()
        {
            if (_isProcessing) {
                Logger.LogWarning("CaptureTrackingService: Уже выполняется процесс трекинга, повторный запуск игнорируется");
                return;
            }
            _isProcessing = true;
            
            try
            {
                var captures = _databaseService.GetAllUnfiredAndUnskippedCaptures();
                // Logger.LogDebug($"CaptureTrackingService: Найдено {captures.Count} неактивированных и не пропущенных capture(ов) для обработки");
                
                if(captures.Count > 0)
                {
                    ClearTrackingDirectory();
                }
                
                foreach (var capture in captures)
                {
                    try
                    {
                        Logger.LogDebug($"CaptureTrackingService: Обработка capture ID={capture.ID}, Handle={capture.Handle}, X={capture.X}, Y={capture.Y}, W={capture.Width}, H={capture.Height}");
                        
                        using var bmp = _screenshotService.CaptureWindow((IntPtr)capture.Handle);
                        if (bmp == null) {
                            Logger.LogWarning($"CaptureTrackingService: Не удалось получить скриншот окна Handle={capture.Handle}");
                            continue;
                        }

                        Bitmap trackingBitmap;
                        if (capture.Source == "trading_canvas")
                        {
                            trackingBitmap = MergeCanvasWithScreenshot(capture, bmp);
                            if (trackingBitmap == null)
                            {
                                Logger.LogError($"[CaptureTrackingService] Пропуск capture ID={capture.ID}: Canvas не найден для handle {capture.Handle}");
                                continue;
                            }
                        }
                        else
                        {
                            Rectangle cropRect = new Rectangle(capture.X, capture.Y, capture.Width, capture.Height);
                            trackingBitmap = bmp.Clone(cropRect, bmp.PixelFormat);
                        }
                        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES", capture.ID);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        string filePath = Path.Combine(dir, "tracking.png");
                        trackingBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        using (trackingBitmap)
                        {
                            // find breakouts on trackingBitmap
                            var detector = new TrendlineBreakDetector();
                            string debugPath = Path.Combine(dir, "tracking_debug.png");
                            var result = detector.DetectBreakout(trackingBitmap, capture.Model, debugPath);
                            switch (result)
                            {
                                case TrendlineBreakResult.NoTrendline:
                                    Logger.LogInfo($"CaptureTrackingService: [{filePath}] Trendline not detected.");
                                    break;
                                case TrendlineBreakResult.NoBreakout:
                                    Logger.LogInfo($"CaptureTrackingService: [{filePath}] Trendline detected, breakout NOT found.");
                                    break;
                                case TrendlineBreakResult.BreakoutUp:
                                    Logger.LogInfo($"CaptureTrackingService: [{filePath}] Breakout UP detected!");
                                    _databaseService.UpdateCaptureIsFired(capture, true);
                                    // Toast notification with trade counter
                                    var toastUp = ServiceContainer.Instance.GetService<ToastNotifyService>();
                                    var tradeCounterUp = ServiceContainer.Instance.GetService<TradeCounter>();
                                    int tradeNumberUp = tradeCounterUp?.IncrementFiredTrades() ?? 0;
                                    toastUp?.ShowToast($"BUY #{tradeNumberUp} {capture.Symbol} {capture.Risk}", ToastType.BreakoutUp);
                                    
                                    await OnBreakoutDetected(capture, TrendlineBreakResult.BreakoutUp);
                                    break;
                                case TrendlineBreakResult.BreakoutDown:
                                    Logger.LogInfo($"CaptureTrackingService: [{filePath}] Breakout DOWN detected!");
                                    _databaseService.UpdateCaptureIsFired(capture, true);
                                    // Toast notification with trade counter
                                    var toastDown = ServiceContainer.Instance.GetService<ToastNotifyService>();
                                    var tradeCounterDown = ServiceContainer.Instance.GetService<TradeCounter>();
                                    int tradeNumberDown = tradeCounterDown?.IncrementFiredTrades() ?? 0;
                                    toastDown?.ShowToast($"SELL #{tradeNumberDown} {capture.Symbol} {capture.Risk}", ToastType.BreakoutDown);
                                    
                                    await OnBreakoutDetected(capture, TrendlineBreakResult.BreakoutDown);
                                    break;
                            }
                            Logger.LogDebug($"CaptureTrackingService: TrendlineBreakDetector result for {filePath}: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"CaptureTrackingService: Ошибка при обработке capture ID={capture.ID}, Handle={capture.Handle}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("CaptureTrackingService: Ошибка при обработке captures", ex);
            }
            finally
            {
                _isProcessing = false;
                // Logger.LogInfo("CaptureTrackingService: Завершение обработки captures");
                await OnCaptureTrackingIterationEnded();
            }
        }

        // Защищенный метод для вызова события BreakoutDetected
        protected virtual async Task OnBreakoutDetected(CaptureData capture, TrendlineBreakResult result)
        {
            if (BreakoutDetected != null)
            {
                await BreakoutDetected(capture, result);
            }
        }

        // Метод для вызова события окончания итерации
        protected virtual async Task OnCaptureTrackingIterationEnded()
        {
            if (CaptureTrackingIterationEnded != null)
                await CaptureTrackingIterationEnded.Invoke();
        }

        /// <summary>
        /// Мержит Canvas поверх скриншота окна и вырезает нужную область. Если Canvas не найден — возвращает null.
        /// </summary>
        private Bitmap MergeCanvasWithScreenshot(CaptureData capture, Bitmap bmp)
        {
            string canvasesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Canvases");
            string canvasFile = capture.Source == "trading_canvas"
                ? Path.Combine(canvasesDir, $"trading_{capture.Handle}.png")
                : Path.Combine(canvasesDir, capture.Handle + ".png");
            if (!File.Exists(canvasFile))
            {
                Logger.LogError($"Canvas file not found for handle {capture.Handle}: {canvasFile}");
                return null;
            }
            using var canvasBmp = new Bitmap(canvasFile);
            using var merged = new Bitmap(bmp.Width, bmp.Height);
            using (var g = Graphics.FromImage(merged))
            {
                g.DrawImage(bmp, 0, 0);
                g.DrawImage(canvasBmp, 0, 0, canvasBmp.Width, canvasBmp.Height);
            }
            Rectangle cropRect = new Rectangle(capture.X, capture.Y, capture.Width, capture.Height);
            return merged.Clone(cropRect, merged.PixelFormat);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
} 