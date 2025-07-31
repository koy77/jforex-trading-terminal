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
                int screenX = windowRect.Left + x;
                int screenY = windowRect.Top + y;

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
                Logger.LogTagInfo("JForex", "Adding trendline to GForex with direct mouse actions");

                // Отправляем ESC для сброса предыдущих действий
                SendEscKey(targetWindowHandle);
                await Task.Delay(100);

                // Отправляем нажатие клавиши A для активации инструмента трендовой линии
                if (!SendKeyPress(targetWindowHandle, 'A'))
                {
                    Logger.LogTagError("JForex", "Failed to send key A");
                    return false;
                }

                await Task.Delay(200);

                // Конвертируем экранные координаты в координаты окна
                var windowPoint1 = ConvertScreenToWindowCoordinates(firstScreenPoint, targetWindowHandle);
                var windowPoint2 = ConvertScreenToWindowCoordinates(lastScreenPoint, targetWindowHandle);

                Logger.LogTagInfo("JForex", $"Converted coordinates: Point1=({windowPoint1.X}, {windowPoint1.Y}), Point2=({windowPoint2.X}, {windowPoint2.Y})");

                // Кликаем на первую точку
                if (!ClickAtPosition(targetWindowHandle, windowPoint1.X, windowPoint1.Y))
                {
                    Logger.LogTagError("JForex", "Failed to click on first point");
                    return false;
                }

                await Task.Delay(100);

                // Кликаем на вторую точку
                if (!ClickAtPosition(targetWindowHandle, windowPoint2.X, windowPoint2.Y))
                {
                    Logger.LogTagError("JForex", "Failed to click on second point");
                    return false;
                }

                // Отправляем ESC для завершения
                SendEscKey(targetWindowHandle);

                Logger.LogTagInfo("JForex", "Trendline successfully added to GForex with direct mouse actions");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error adding trendline to GForex directly: {ex.Message}", ex);
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
                // Получаем позицию окна на экране
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.LogTagError("JForex", "Failed to get window position");
                    return new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y);
                }

                // Используем подход из MainWindow для получения границ монитора
                int monitorLeftBoundary = GetMonitorLeftBoundaryForWindow(windowHandle);
                
                // Вычисляем координаты: X = левая граница монитора + X, Y остается как есть
                int windowX = monitorLeftBoundary + (int)screenPoint.X;
                int windowY = (int)screenPoint.Y;

                Logger.LogTagInfo("JForex", $"ConvertScreenToWindowCoordinates: screenPoint=({screenPoint.X}, {screenPoint.Y}), monitorLeftBoundary={monitorLeftBoundary}, result=({windowX}, {windowY})");

                return new System.Drawing.Point(windowX, windowY);
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error converting screen coordinates to window coordinates: {ex.Message}", ex);
                return new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y);
            }
        }

        /// <summary>
        /// Получает левую границу монитора для указанного окна, используя подход из MainWindow
        /// </summary>
        private int GetMonitorLeftBoundaryForWindow(IntPtr windowHandle)
        {
            try
            {
                // Получаем позицию окна
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.LogTagError("JForex", "Failed to get window position for monitor detection");
                    return 0;
                }

                // Используем System.Windows.Forms.Screen для получения информации о мониторах
                var screens = System.Windows.Forms.Screen.AllScreens;
                
                // Находим монитор, на котором находится окно
                for (int i = 0; i < screens.Length; i++)
                {
                    var screen = screens[i];
                    var bounds = screen.Bounds;
                    
                    // Проверяем, находится ли окно на этом мониторе
                    if (bounds.Contains(windowRect.Left, windowRect.Top))
                    {
                        Logger.LogTagInfo("JForex", $"Window found on monitor {i}: LeftBoundary={bounds.Left}, Bounds=({bounds.X}, {bounds.Y}, {bounds.Width}, {bounds.Height})");
                        return bounds.Left;
                    }
                }
                
                // Если не найден, возвращаем 0 (основной монитор)
                Logger.LogTagWarning("JForex", "Window not found on any monitor, using primary monitor (LeftBoundary=0)");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error getting monitor left boundary: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        /// Устанавливает риск для binary-брокера через binary-сокет
        /// </summary>
        public async Task<bool> SetRisk(string brokerName, double risk)
        {
            try
            {
                var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                if (binarySocketService != null && binarySocketService.IsConnected)
                {
                    string command = $"{{\"cmd\":\"set_risk\",\"broker\":\"{brokerName}\",\"risk\":\"{risk}\"}}";
                    
                    bool sent = await binarySocketService.WriteAsync(command);
                    if (sent)
                    {
                        Logger.LogTagInfo("JForex", $"Risk command sent to binary socket: {command}");
                        return true;
                    }
                    else
                    {
                        Logger.LogTagError("JForex", $"Failed to send risk command to binary socket: {command}");
                        return false;
                    }
                }
                else
                {
                    Logger.LogTagWarning("JForex", "Binary socket service not available or not connected for risk command");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error sending risk command to binary socket: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Устанавливает duration для binary-брокера через binary-сокет
        /// </summary>
        public async Task<bool> SetDuration(string brokerName, int duration)
        {
            try
            {
                var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                if (binarySocketService != null && binarySocketService.IsConnected)
                {
                    string command = $"{{\"cmd\":\"set_duration\",\"broker\":\"{brokerName}\",\"duration\":\"{duration}\"}}";
                    
                    bool sent = await binarySocketService.WriteAsync(command);
                    if (sent)
                    {
                        Logger.LogTagInfo("JForex", $"Duration command sent to binary socket: {command}");
                        return true;
                    }
                    else
                    {
                        Logger.LogTagError("JForex", $"Failed to send duration command to binary socket: {command}");
                        return false;
                    }
                }
                else
                {
                    Logger.LogTagWarning("JForex", "Binary socket service not available or not connected for duration command");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error sending duration command to binary socket: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Отправляет команду открытия символа для binary-брокера через binary-сокет
        /// </summary>
        public async Task<bool> OpenSymbol(string brokerName, string symbolName)
        {
            try
            {
                var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
                if (binarySocketService != null && binarySocketService.IsConnected)
                {
                    string command = $"{{\"cmd\":\"open_symbol\",\"symbol\":\"{symbolName}\",\"broker\":\"{brokerName}\"}}";
                    
                    bool sent = await binarySocketService.WriteAsync(command);
                    if (sent)
                    {
                        Logger.LogTagInfo("JForex", $"Open symbol command sent to binary socket: {command}");
                        return true;
                    }
                    else
                    {
                        Logger.LogTagError("JForex", $"Failed to send open symbol command to binary socket: {command}");
                        return false;
                    }
                }
                else
                {
                    Logger.LogTagWarning("JForex", "Binary socket service not available or not connected for open symbol command");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error sending open symbol command to binary socket: {ex.Message}", ex);
                return false;
            }
        }
       
    }
}