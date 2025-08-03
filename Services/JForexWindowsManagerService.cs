using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Services
{
    public class JForexWindowsManagerService
    {
        public JForexWindowsManagerService()
        {
        }

        #region Windows API Imports

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        #endregion

        #region Windows API Constants

        private const int SW_MAXIMIZE = 3;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;


        #endregion

        #region Windows API Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion


        /// <summary>
        /// Активирует окно GForex
        /// </summary>
        public bool ActivateWindow(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                {
                    Logger.LogTagError("JForex", "Некорректный handle окна");
                    return false;
                }

                // Разворачиваем окно на весь экран
                ShowWindow(windowHandle, SW_MAXIMIZE);
                
                // Устанавливаем фокус на окно
                bool result = SetForegroundWindow(windowHandle);
                
                if (result)
                {
                    Logger.LogTagInfo("JForex", $"Окно успешно активировано. Handle: {windowHandle.ToInt64()}");
                }
                else
                {
                    Logger.LogTagError("JForex", $"Ошибка активации окна. Handle: {windowHandle.ToInt64()}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Ошибка при активации окна: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Отправляет нажатие клавиши в окно
        /// </summary>
        public bool SendKeyPress(IntPtr windowHandle, char key)
        {
            try
            {
                int virtualKeyCode = (int)key;
                
                // Отправляем WM_KEYDOWN
                bool keyDownResult = PostMessage(windowHandle, WM_KEYDOWN, (IntPtr)virtualKeyCode, IntPtr.Zero);
                
                // Небольшая задержка
                System.Threading.Thread.Sleep(50);
                
                // Отправляем WM_KEYUP
                bool keyUpResult = PostMessage(windowHandle, WM_KEYUP, (IntPtr)virtualKeyCode, IntPtr.Zero);

                if (keyDownResult && keyUpResult)
                {
                    Logger.LogTagInfo("JForex", $"Клавиша '{key}' успешно отправлена в окно {windowHandle.ToInt64()}");
                    return true;
                }
                else
                {
                    Logger.LogTagError("JForex", $"Ошибка отправки клавиши '{key}' в окно {windowHandle.ToInt64()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Ошибка при отправке клавиши '{key}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Кликает мышью в указанной позиции окна
        /// </summary>
        public bool ClickAtPosition(IntPtr windowHandle, int x, int y)
        {
            try
            {
                Logger.LogTagInfo("JForex", $"=== MOUSE CLICK START ===");
                Logger.LogTagInfo("JForex", $"Click coordinates: ({x}, {y})");
                Logger.LogTagInfo("JForex", $"Window handle: {windowHandle.ToInt64()}");

                // Получаем позицию окна на экране
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.LogTagError("JForex", "Ошибка получения позиции окна");
                    return false;
                }

                // Вычисляем абсолютные координаты на экране
                int screenX = x;
                int screenY = y;

                Logger.LogTagInfo("JForex", $"Window rect: Left={windowRect.Left}, Top={windowRect.Top}, Right={windowRect.Right}, Bottom={windowRect.Bottom}");
                Logger.LogTagInfo("JForex", $"Calculated screen coordinates: ({screenX}, {screenY})");

                // Устанавливаем позицию курсора
                SetCursorPos(screenX, screenY);
                
                // Небольшая задержка
                System.Threading.Thread.Sleep(50);

                // Отправляем WM_LBUTTONDOWN
                bool mouseDownResult = PostMessage(windowHandle, WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)((y << 16) | x));
                
                // Небольшая задержка
                System.Threading.Thread.Sleep(50);
                
                // Отправляем WM_LBUTTONUP
                bool mouseUpResult = PostMessage(windowHandle, WM_LBUTTONUP, IntPtr.Zero, (IntPtr)((y << 16) | x));

                if (mouseDownResult && mouseUpResult)
                {
                    Logger.LogTagInfo("JForex", $"Клик успешно выполнен в позиции ({x}, {y}) -> screen ({screenX}, {screenY})");
                    Logger.LogTagInfo("JForex", "=== MOUSE CLICK END ===");
                    return true;
                }
                else
                {
                    Logger.LogTagError("JForex", $"Ошибка клика в позиции ({x}, {y}) -> screen ({screenX}, {screenY})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Ошибка при клике: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Конвертирует координаты Canvas в координаты окна GForex
        /// </summary>
        private POINT ConvertCanvasToWindowCoordinates(double canvasX, double canvasY, IntPtr windowHandle)
        {
            try
            {
                // Получаем позицию окна на экране
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.LogTagError("JForex", $"Ошибка получения позиции окна для handle={windowHandle.ToInt64()}");
                    return new POINT { X = (int)canvasX, Y = (int)canvasY };
                }

                // Определяем индекс монитора по координатам окна
                int monitorIndex = ScreenCaptureApp.Helpers.MainHelper.GetMonitorIndexByCoordinates(windowRect.Left+200, windowRect.Top+200);
                
                // Получаем левую границу монитора
                int monitorLeftBoundary = ScreenCaptureApp.Helpers.MainHelper.GetMonitorLeftBoundary(monitorIndex);

                // Вычисляем абсолютные экранные координаты
                // Если это не основной монитор (индекс > 0), добавляем левую границу монитора
                int screenX = (int)canvasX;
                if (monitorIndex > 0)
                {
                    screenX = monitorLeftBoundary + (int)canvasX;
                }
                int screenY = (int)canvasY;

                Logger.LogTagInfo("JForex", $"=== COORDINATE CONVERSION ===");
                Logger.LogTagInfo("JForex", $"Input canvas coordinates: ({canvasX}, {canvasY})");
                Logger.LogTagInfo("JForex", $"Window handle: {windowHandle.ToInt64()}");
                Logger.LogTagInfo("JForex", $"Window rect: Left={windowRect.Left}, Top={windowRect.Top}, Right={windowRect.Right}, Bottom={windowRect.Bottom}");
                Logger.LogTagInfo("JForex", $"Monitor index: {monitorIndex}");
                Logger.LogTagInfo("JForex", $"Monitor left boundary: {monitorLeftBoundary}");
                Logger.LogTagInfo("JForex", $"Calculated screen coordinates: ({screenX}, {screenY})");
                Logger.LogTagInfo("JForex", $"=== END COORDINATE CONVERSION ===");

                return new POINT
                {
                    X = screenX,
                    Y = screenY
                };
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Ошибка конвертации координат: {ex.Message}", ex);
                return new POINT { X = (int)canvasX, Y = (int)canvasY };
            }
        }



        public void SendEscKey(IntPtr hwnd)
        {
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            const int VK_ESCAPE = 0x1B;
            if (hwnd == IntPtr.Zero) return;
            SendMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
            SendMessage(hwnd, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);
            Logger.LogTagInfo("JForex", $"Sent ESC key to hwnd=0x{hwnd.ToInt64():X}");
        }

        /// <summary>
        /// Добавляет трендовую линию в GForex напрямую с действиями мыши
        /// </summary>
        public async Task<bool> AddTrendlineToGForexDirectlyAsync(IntPtr targetWindowHandle, System.Windows.Point firstScreenPoint, System.Windows.Point lastScreenPoint)
        {
            try
            {
                // Convert screen coordinates to window coordinates
                var firstWindowPoint = ConvertScreenToWindowCoordinates(firstScreenPoint, targetWindowHandle);
                var lastWindowPoint = ConvertScreenToWindowCoordinates(lastScreenPoint, targetWindowHandle);

                // Activate the window first
                if (!ActivateWindow(targetWindowHandle))
                {
                    Logger.LogTagError("JForex", "Failed to activate target window for trendline");
                    return false;
                }

                // Add a small delay to ensure window is fully activated
                await Task.Delay(100);

                // Send 'A' key to activate trendline drawing tool
                if (!SendKeyPress(targetWindowHandle, 'A'))
                {
                    Logger.LogTagError("JForex", "Failed to send 'A' key to activate trendline tool");
                    return false;
                }

                // Add a small delay after sending 'A' key
                await Task.Delay(100);

                // Click at the first point
                if (!ClickAtPosition(targetWindowHandle, firstWindowPoint.X, firstWindowPoint.Y))
                {
                    Logger.LogTagError("JForex", $"Failed to click at first point ({firstWindowPoint.X}, {firstWindowPoint.Y})");
                    return false;
                }

                // Add a small delay between clicks
                await Task.Delay(50);

                // Click at the second point
                if (!ClickAtPosition(targetWindowHandle, lastWindowPoint.X, lastWindowPoint.Y))
                {
                    Logger.LogTagError("JForex", $"Failed to click at second point ({lastWindowPoint.X}, {lastWindowPoint.Y})");
                    return false;
                }

                Logger.LogTagInfo("JForex", $"Successfully added trendline from ({firstWindowPoint.X}, {firstWindowPoint.Y}) to ({lastWindowPoint.X}, {lastWindowPoint.Y})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error adding trendline to GForex: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Конвертирует экранные координаты в координаты окна
        /// </summary>
        private System.Drawing.Point ConvertScreenToWindowCoordinates(System.Windows.Point screenPoint, IntPtr windowHandle)
        {
            try
            {
                // Get window position
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.LogTagError("JForex", "Failed to get window rectangle");
                    return new System.Drawing.Point(0, 0);
                }

                // Calculate window coordinates
                int windowX = (int)screenPoint.X - windowRect.Left;
                int windowY = (int)screenPoint.Y - windowRect.Top;

                // Get monitor info to account for taskbar
                IntPtr monitor = MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                GetMonitorInfo(monitor, ref monitorInfo);

                // Adjust for taskbar
                int taskbarHeight = (monitorInfo.rcMonitor.Bottom - monitorInfo.rcWork.Bottom);
                windowY -= taskbarHeight;

                Logger.LogTagInfo("JForex", $"Converted screen point ({screenPoint.X}, {screenPoint.Y}) to window point ({windowX}, {windowY})");
                return new System.Drawing.Point(windowX, windowY);
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error converting screen to window coordinates: {ex.Message}", ex);
                return new System.Drawing.Point(0, 0);
            }
        }

        /// <summary>
        /// Получает левую границу монитора для указанного окна, используя подход из MainWindow
        /// </summary>
        private int GetMonitorLeftBoundaryForWindow(IntPtr windowHandle)
        {
            try
            {
                IntPtr monitor = MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                GetMonitorInfo(monitor, ref monitorInfo);
                return monitorInfo.rcMonitor.Left;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error getting monitor left boundary: {ex.Message}", ex);
                return 0;
            }
        }
       
    }
}