using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Services
{
    public class CaptureService
    {
        private readonly DatabaseService _databaseService;
        private readonly ScreenshotService _screenshotService;
        
        // Mouse hook related fields
        private IntPtr mouseHook = IntPtr.Zero;
        private HwndSource source;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc mouseProc;

        // Mouse hook constants
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;

        // Mouse hook DllImports
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(System.Drawing.Point p);

        public event EventHandler<CaptureEventArgs> CaptureCompleted;
        public event EventHandler<MouseHookEventArgs> MouseHookEvent;

        public CaptureService()
        {
            _databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            _screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
        }

        /// <summary>
        /// Получает виртуальные границы всех мониторов
        /// </summary>
        public System.Drawing.Rectangle GetVirtualScreenBounds()
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            
            System.Diagnostics.Debug.WriteLine($"Total screens: {Screen.AllScreens.Length}");
            
            foreach (var screen in Screen.AllScreens)
            {
                System.Diagnostics.Debug.WriteLine($"Screen: Bounds={screen.Bounds}, Primary={screen.Primary}");
                if (screen.Bounds.Left < minX) minX = screen.Bounds.Left;
                if (screen.Bounds.Top < minY) minY = screen.Bounds.Top;
                if (screen.Bounds.Right > maxX) maxX = screen.Bounds.Right;
                if (screen.Bounds.Bottom > maxY) maxY = screen.Bounds.Bottom;
            }
            
            var bounds = new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
            System.Diagnostics.Debug.WriteLine($"Virtual bounds: {bounds}");
            return bounds;
        }

        /// <summary>
        /// Выполняет захват области экрана с символом и риском
        /// </summary>
        public void CaptureAreaWithSymbolAndRisk(double x, double y, double width, double height, Window overlayWindow, string symbol, IntPtr windowHandle, double risk, int duration = 0)
        {
            // Логируем переданные параметры
            Logger.LogInfo($"CaptureService.CaptureAreaWithSymbolAndRisk called with: risk={risk}, duration={duration}, symbol={symbol}");
            
            int monitorIndex = 0;
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                if (Screen.AllScreens[i].Bounds.Contains((int)x, (int)y))
                {
                    monitorIndex = i;
                    break;
                }
            }
            var windowX = x < 0 ? x - Screen.AllScreens[monitorIndex].Bounds.Left : x;
            var brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            
            // Если duration не передан или равен 0, используем значение из DurationState
            if (duration == 0)
            {
                var durationState = ServiceContainer.Instance.GetService<DurationState>();
                duration = durationState.CurrentDuration;
            }
            
            // Определяем модель по координатам Y
            string model = "OHLC";
            if (y >= 656 && (y + height) >= 656)
                model = "MACD";
            else if (y < 656 && (y + height) < 656)
                model = "OHLC";
            // Если одна точка выше, другая ниже — по умолчанию OHLC
            
            var captureEntry = new CaptureData
            {
                X = (int)windowX,
                Y = (int)y,
                Width = (int)width,
                Height = (int)height,
                Handle = windowHandle.ToInt64(),
                Monitor = monitorIndex,
                Timestamp = DateTime.Now.ToString("o"),
                Symbol = symbol,
                Risk = risk,
                Source = "window",
                Broker = brokerState.GetDisplayName(),
                Duration = duration,
                Model = model
            };
            _databaseService.SaveCapture(captureEntry);
            
            // Подробное логирование созданного CaptureData
            Logger.LogInfo($"CaptureData created: ID={captureEntry.ID}, Symbol={captureEntry.Symbol}, Risk={captureEntry.Risk}, Duration={captureEntry.Duration}, Broker={captureEntry.Broker}, Source={captureEntry.Source}");
            
            // Use the ID for screenshot file name
            var debugInfo = _screenshotService.CaptureScreenAreaDebug((int)x, (int)y, (int)width, (int)height, monitorIndex, captureEntry.ID);
            captureEntry.ScreenshotPath = debugInfo.ScreenshotPath;
            _databaseService.UpdateCapture(captureEntry);
            // Обновить риск у символа
            if (!string.IsNullOrEmpty(symbol))
            {
                _databaseService.UpdateSymbolRisk(symbol, risk);
                
                // Обновляем настройки символа в DI Container
                var symbolSettingsManager = ServiceContainer.Instance.GetService<SymbolSettingsManager>();
                symbolSettingsManager?.UpdateSettingsFromCapture(symbol, risk, duration);
            }
            
            // Обновляем настройки брокера в DI Container
            var brokerSettingsManager = ServiceContainer.Instance.GetService<BrokerSettingsManager>();
            brokerSettingsManager?.UpdateSettingsFromCapture(brokerState.CurrentBroker, risk, duration);

            CaptureCompleted?.Invoke(this, new CaptureEventArgs
            {
                X = (int)x,
                Y = (int)y,
                Width = (int)width,
                Height = (int)height,
                ScreenshotPath = debugInfo.ScreenshotPath,
                DebugInfo = debugInfo
            });
        }

        /// <summary>
        /// Получает все сохраненные захваты
        /// </summary>
        public List<CaptureData> GetAllCaptures()
        {
            return _databaseService.GetAllCaptures();
        }

        /// <summary>
        /// Очищает все сохраненные захваты
        /// </summary>
        public void ClearAllCaptures()
        {
            _databaseService.ClearAllCaptures();
        }

        /// <summary>
        /// Получает последний захват
        /// </summary>
        public CaptureData GetLastCapture()
        {
            return _databaseService.GetLastCapture();
        }

        /// <summary>
        /// Получает захваты за определенный период
        /// </summary>
        public List<CaptureData> GetCapturesByDateRange(DateTime startDate, DateTime endDate)
        {
            return _databaseService.GetCapturesByDateRange(startDate, endDate);
        }

        /// <summary>
        /// Устанавливает глобальный хук мыши
        /// </summary>
        public void SetupMouseHook(Window window)
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            mouseProc = MouseHookCallback;
            mouseHook = SetWindowsHookEx(WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(mouseProc), GetModuleHandle("user32"), 0);
        }

        /// <summary>
        /// Удаляет глобальный хук мыши
        /// </summary>
        public void RemoveMouseHook()
        {
            if (mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Callback для глобального хука мыши
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                
                var mouseEventArgs = new MouseHookEventArgs
                {
                    Message = message,
                    Timestamp = DateTime.Now
                };

                switch (message)
                {
                    case WM_LBUTTONDOWN:
                        System.Diagnostics.Debug.WriteLine("Global mouse hook: LBUTTONDOWN");
                        mouseEventArgs.EventType = MouseEventType.LeftButtonDown;
                        break;
                    case WM_LBUTTONUP:
                        System.Diagnostics.Debug.WriteLine("Global mouse hook: LBUTTONUP");
                        mouseEventArgs.EventType = MouseEventType.LeftButtonUp;
                        break;
                    case WM_MOUSEMOVE:
                        mouseEventArgs.EventType = MouseEventType.MouseMove;
                        break;
                }

                MouseHookEvent?.Invoke(this, mouseEventArgs);
            }
            
            return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }
    }

    public class CaptureEventArgs : EventArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ScreenshotPath { get; set; }
        public CaptureDebugInfo DebugInfo { get; set; }
    }

    public class MouseHookEventArgs : EventArgs
    {
        public int Message { get; set; }
        public MouseEventType EventType { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum MouseEventType
    {
        LeftButtonDown,
        LeftButtonUp,
        MouseMove
    }
} 