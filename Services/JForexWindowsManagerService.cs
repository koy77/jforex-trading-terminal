using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
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

        #endregion

        #region Windows API Constants

        private const int SW_MAXIMIZE = 3;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;


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

        #endregion

        /// <summary>
        /// Добавляет новую трендовую линию в GForex окно
        /// </summary>
        /// <param name="targetWindowHandle">Handle окна GForex</param>
        /// <param name="targetWindowHandleWidth">Ширина окна GForex</param>
        /// <param name="point1X">X координата первой точки</param>
        /// <param name="point1Y">Y координата первой точки</param>
        /// <param name="point2X">X координата второй точки</param>
        /// <param name="point2Y">Y координата второй точки</param>
        /// <returns>True если операция выполнена успешно</returns>
        public async Task<bool> AddTrendlineAsync(IntPtr targetWindowHandle, int targetWindowHandleWidth, 
            double point1X, double point1Y, double point2X, double point2Y)
        {
            try
            {
                Logger.LogTagInfo("JForex", $"=== TRENDLINE ADDITION START ===");
                Logger.LogTagInfo("JForex", $"Input coordinates - Point1: ({point1X}, {point1Y}), Point2: ({point2X}, {point2Y})");
                Logger.LogTagInfo("JForex", $"Target window handle: {targetWindowHandle.ToInt64()}");
                Logger.LogTagInfo("JForex", $"Target window width: {targetWindowHandleWidth}");

                // Активируем окно GForex
                if (!ActivateWindow(targetWindowHandle))
                {
                    Logger.LogTagError("JForex", "Ошибка активации окна GForex");
                    return false;
                }

                // Небольшая задержка для стабилизации
                await Task.Delay(200);
                SendEscKey(targetWindowHandle);
                // Отправляем нажатие клавиши A для активации инструмента трендовой линии
                if (!SendKeyPress(targetWindowHandle, 'A'))
                {
                    Logger.LogTagError("JForex", "Ошибка отправки клавиши A");
                    return false;
                }

                await Task.Delay(200);

                // Конвертируем координаты Canvas в координаты окна GForex
                var windowPoint1 = ConvertCanvasToWindowCoordinates(point1X, point1Y, targetWindowHandle);
                var windowPoint2 = ConvertCanvasToWindowCoordinates(point2X, point2Y, targetWindowHandle);

                Logger.LogTagInfo("JForex", $"=== CONVERTED COORDINATES ===");
                Logger.LogTagInfo("JForex", $"windowPoint1: ({windowPoint1.X}, {windowPoint1.Y})");
                Logger.LogTagInfo("JForex", $"windowPoint2: ({windowPoint2.X}, {windowPoint2.Y})");
                Logger.LogTagInfo("JForex", $"=== END CONVERTED COORDINATES ===");

                // Кликаем на первую точку
                if (!ClickAtPosition(targetWindowHandle, windowPoint1.X, windowPoint1.Y))
                {
                    Logger.LogTagError("JForex", $"Ошибка клика на первой точке: ({windowPoint1.X}, {windowPoint1.Y})");
                    return false;
                }

                await Task.Delay(100);

                // Кликаем на вторую точку
                if (!ClickAtPosition(targetWindowHandle, windowPoint2.X, windowPoint2.Y))
                {
                    Logger.LogTagError("JForex", $"Ошибка клика на второй точке: ({windowPoint2.X}, {windowPoint2.Y})");
                    return false;
                }

                SendEscKey(targetWindowHandle);

                Logger.LogTagInfo("JForex", "Треховая линия успешно добавлена");
                Logger.LogTagInfo("JForex", "=== TRENDLINE ADDITION END ===");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Ошибка при добавлении трендовой линии: {ex.Message}", ex);
                return false;
            }
        }

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
       
    }
}