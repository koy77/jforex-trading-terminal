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
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        #endregion

        #region Windows API Constants

        private const int SW_MAXIMIZE = 3;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

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
                Logger.Log($"JForex: Начинаем добавление трендовой линии. Точка 1: ({point1X}, {point1Y}), Точка 2: ({point2X}, {point2Y})");

                // Активируем окно GForex
                if (!ActivateWindow(targetWindowHandle))
                {
                    Logger.Log("JForex: Ошибка активации окна GForex");
                    return false;
                }

                // Небольшая задержка для стабилизации
                await Task.Delay(100);

                // Отправляем нажатие клавиши A для активации инструмента трендовой линии
                if (!SendKeyPress(targetWindowHandle, 'A'))
                {
                    Logger.Log("JForex: Ошибка отправки клавиши A");
                    return false;
                }

                await Task.Delay(200);

                // Конвертируем координаты Canvas в координаты окна GForex
                var windowPoint1 = ConvertCanvasToWindowCoordinates(point1X, point1Y, targetWindowHandleWidth);
                var windowPoint2 = ConvertCanvasToWindowCoordinates(point2X, point2Y, targetWindowHandleWidth);

                Logger.Log($"JForex: Конвертированные координаты. Точка 1: ({windowPoint1.X}, {windowPoint1.Y}), Точка 2: ({windowPoint2.X}, {windowPoint2.Y})");

                // Кликаем на первую точку
                if (!ClickAtPosition(targetWindowHandle, windowPoint1.X, windowPoint1.Y))
                {
                    Logger.Log("JForex: Ошибка клика на первой точке");
                    return false;
                }

                await Task.Delay(100);

                // Кликаем на вторую точку
                if (!ClickAtPosition(targetWindowHandle, windowPoint2.X, windowPoint2.Y))
                {
                    Logger.Log("JForex: Ошибка клика на второй точке");
                    return false;
                }

                Logger.Log("JForex: Треховая линия успешно добавлена");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"JForex: Ошибка при добавлении трендовой линии: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Активирует окно GForex
        /// </summary>
        private bool ActivateWindow(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                {
                    Logger.Log("JForex: Некорректный handle окна");
                    return false;
                }

                // Разворачиваем окно на весь экран
                ShowWindow(windowHandle, SW_MAXIMIZE);
                
                // Устанавливаем фокус на окно
                bool result = SetForegroundWindow(windowHandle);
                
                if (result)
                {
                    Logger.Log("JForex: Окно успешно активировано");
                }
                else
                {
                    Logger.Log("JForex: Ошибка активации окна");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Log($"JForex: Ошибка при активации окна: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отправляет нажатие клавиши в окно
        /// </summary>
        private bool SendKeyPress(IntPtr windowHandle, char key)
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
                    Logger.Log($"JForex: Клавиша '{key}' успешно отправлена");
                    return true;
                }
                else
                {
                    Logger.Log($"JForex: Ошибка отправки клавиши '{key}'");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"JForex: Ошибка при отправке клавиши: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Кликает мышью в указанной позиции окна
        /// </summary>
        private bool ClickAtPosition(IntPtr windowHandle, int x, int y)
        {
            try
            {
                // Получаем позицию окна на экране
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.Log("JForex: Ошибка получения позиции окна");
                    return false;
                }

                // Вычисляем абсолютные координаты на экране
                int screenX = windowRect.Left + x;
                int screenY = windowRect.Top + y;

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
                    Logger.Log($"JForex: Клик успешно выполнен в позиции ({x}, {y})");
                    return true;
                }
                else
                {
                    Logger.Log($"JForex: Ошибка клика в позиции ({x}, {y})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"JForex: Ошибка при клике: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Конвертирует координаты Canvas в координаты окна GForex
        /// </summary>
        private POINT ConvertCanvasToWindowCoordinates(double canvasX, double canvasY, int windowWidth)
        {
            // Здесь можно добавить логику масштабирования координат
            // Пока используем простое преобразование
            return new POINT
            {
                X = (int)canvasX,
                Y = (int)canvasY
            };
        }

        /// <summary>
        /// Получает две случайные точки в окне для рисования трендовой линии
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>Кортеж с двумя точками (point1X, point1Y, point2X, point2Y)</returns>
        public (int point1X, int point1Y, int point2X, int point2Y) GetRandomPointsInWindow(IntPtr windowHandle)
        {
            try
            {
                // Получаем размеры окна
                RECT windowRect;
                if (!GetWindowRect(windowHandle, out windowRect))
                {
                    Logger.Log("JForex: Ошибка получения размеров окна");
                    return (0, 0, 0, 0);
                }

                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;

                // Генерируем случайные точки, избегая краев окна
                var random = new Random();
                int margin = 50; // Отступ от краев

                int point1X = random.Next(margin, windowWidth - margin);
                int point1Y = random.Next(margin, windowHeight - margin);
                int point2X = random.Next(margin, windowWidth - margin);
                int point2Y = random.Next(margin, windowHeight - margin);

                // Убеждаемся, что точки не слишком близко друг к другу
                while (Math.Abs(point2X - point1X) < 100 && Math.Abs(point2Y - point1Y) < 100)
                {
                    point2X = random.Next(margin, windowWidth - margin);
                    point2Y = random.Next(margin, windowHeight - margin);
                }

                Logger.Log($"JForex: Сгенерированы случайные точки: ({point1X}, {point1Y}) и ({point2X}, {point2Y})");
                return (point1X, point1Y, point2X, point2Y);
            }
            catch (Exception ex)
            {
                Logger.Log($"JForex: Ошибка при генерации случайных точек: {ex.Message}");
                return (0, 0, 0, 0);
            }
        }

       
    }
}