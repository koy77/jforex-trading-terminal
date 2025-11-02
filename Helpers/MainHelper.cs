using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Helpers
{
    /// <summary>
    /// Helper class containing utility functions extracted from MainWindow
    /// </summary>
    public static class MainHelper
    {
        // Windows API imports for cursor window handling
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        public static string GetWindowTitle(IntPtr handle)
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(handle, sb, sb.Capacity);
            return sb.ToString();
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        // Window show commands
        private const int SW_MAXIMIZE = 3;
        private const int SW_RESTORE = 9;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Gets the window handle under the current mouse cursor position
        /// </summary>
        /// <returns>Window handle (IntPtr) or IntPtr.Zero if no window found</returns>
        public static IntPtr GetWindowUnderCursor()
        {
            try
            {
                POINT cursorPos;
                if (GetCursorPos(out cursorPos))
                {
                    IntPtr windowHandle = WindowFromPoint(cursorPos);
                    
                    // Verify that the handle is valid
                    if (windowHandle != IntPtr.Zero && IsWindow(windowHandle))
                    {
                        return windowHandle;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error getting window under cursor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return IntPtr.Zero;
        }

        /// <summary>
        /// Deletes all files from the Cropped folder
        /// </summary>
        public static void DeleteCroppedFiles()
        {
            string croppedFolderPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Cropped");

            if (Directory.Exists(croppedFolderPath))
            {
                string[] files = Directory.GetFiles(croppedFolderPath);
                int deletedFilesCount = 0;

                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedFilesCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete file {file}", ex);
                    }
                }

                Logger.LogInfo($"Deleted {deletedFilesCount} files from Cropped folder.");
                Logger.LogDebug($"Status: Database reset complete. Deleted {deletedFilesCount} files from Cropped folder.");
            }
            else
            {
                Logger.LogInfo("Cropped folder not found");
                Logger.LogDebug("Status: Database reset complete. Cropped folder not found.");
            }
        }

        /// <summary>
        /// Deletes the app.log file
        /// </summary>
        public static void DeleteAppLogFile()
        {
            try
            {
                string logFilePath = "app.log";
                
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                    Logger.LogInfo("app.log file deleted successfully");
                }
                else
                {
                    Logger.LogInfo("app.log file not found - nothing to delete");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to delete app.log file", ex);
            }
        }

        /// <summary>
        /// Resets the database by clearing all data and deleting files
        /// </summary>
        /// <param name="databaseService">The database service instance</param>
        /// <param name="windowManagementService">The window management service instance</param>
        public static void ResetDatabase(DatabaseService databaseService, WindowManagementService windowManagementService)
        {
            try
            {
                // Сбрасываем все данные
                databaseService?.ClearAllData();
                Logger.LogInfo("All database data cleared successfully");

                // Удаляем все файлы из папки Cropped
                DeleteCroppedFiles();

                // Удаляем все файлы из папки CAPTURES
                DeleteCapturesFolder();

                // Удаляем все файлы из папки Canvases
                DeleteCanvasesFolder();

                // Удаляем файл app.log
                DeleteAppLogFile();

                // Сбрасываем счетчик протреканных сделок (нужно добавить в группу переменных, которые обнуляются при ResetDB)
                var tradeCounter = ServiceContainer.Instance.GetService<TradeCounter>();
                tradeCounter?.ResetFiredTrades();
                Logger.LogInfo("Trade counter reset successfully");

                // Переинициализируем символы
                databaseService?.InitializeSymbolsFromWindowManagementService(windowManagementService);
                
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to reset database", ex);
                System.Windows.MessageBox.Show(
                    $"Ошибка при сбросе базы данных: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gets screen information for window positioning
        /// </summary>
        /// <returns>Tuple containing left, top, width, and height values</returns>
        public static (double left, double top, double width, double height) GetScreenPositioning()
        {
            // Получаем все экраны
            var screens = Screen.AllScreens;
            
            // Используем вторичный монитор, если он есть, иначе основной
            var screen = screens.Length > 1 ? screens[1] : screens[0];

            // Вычисляем параметры на том же месте относительно экрана
            double left = screen.WorkingArea.Left;
            double top = screen.WorkingArea.Top;
            double width = screen.WorkingArea.Width;
            double height = 500; // Increased height for symbol buttons

            return (left, top, width, height);
        }

        /// <summary>
        /// Gets screen information for tracking viewer window positioning
        /// </summary>
        /// <returns>Tuple containing left, top, width, and height values for tracking viewer</returns>
        public static (double left, double top, double width, double height) GetTrackingViewerPositioning()
        {
            // Получаем все экраны
            var screens = Screen.AllScreens;
            
            // Используем вторичный монитор, если он есть, иначе основной
            var screen = screens.Length > 1 ? screens[1] : screens[0];

            // Вычисляем параметры для окна трекинга на том же месте относительно экрана
            double left = screen.WorkingArea.Left;
            double top = screen.WorkingArea.Top + 100;
            double width = screen.WorkingArea.Width;
            double height = screen.WorkingArea.Height * 0.8;

            return (left, top, width, height);
        }

        /// <summary>
        /// Validates if a target window is selected
        /// </summary>
        /// <param name="targetWindow">The target window handle</param>
        /// <param name="errorMessage">Custom error message to show</param>
        /// <returns>True if window is valid, false otherwise</returns>
        public static bool ValidateTargetWindow(IntPtr targetWindow, string errorMessage = null)
        {
            if (targetWindow == IntPtr.Zero)
            {
                string message = errorMessage ?? "Target window not found! Наведите мышь на нужное окно и нажмите Space.";
                Logger.LogDebug(message);
                System.Windows.MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Handles symbol button click events
        /// </summary>
        /// <param name="sender">The button that was clicked</param>
        /// <param name="windowManagementService">The window management service instance</param>
        /// <returns>The active symbol key if successful, null otherwise</returns>
        public static string HandleSymbolButtonClick(object sender, WindowManagementService windowManagementService)
        {
            if (sender is System.Windows.Controls.Button button)
            {
                string symbolKey = button.Tag?.ToString();
                if (!string.IsNullOrEmpty(symbolKey))
                {
                    windowManagementService?.FindAndBringSymbolWindowsToForeground(symbolKey);
                    return symbolKey;
                }
            }
            return null;
        }

        public static void DeleteCapturesFolder()
        {
            try
            {
                string capturesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES");
                if (Directory.Exists(capturesDir))
                {
                    foreach (var dir in Directory.GetDirectories(capturesDir))
                    {
                        Directory.Delete(dir, true);
                    }
                    foreach (var file in Directory.GetFiles(capturesDir))
                    {
                        File.Delete(file);
                    }
                    Logger.LogInfo("All files and folders deleted from CAPTURES folder.");
                }
                else
                {
                    Logger.LogInfo("CAPTURES folder not found");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to delete CAPTURES folder contents", ex);
            }
        }

        public static void DeleteCanvasesFolder()
        {
            try
            {
                string canvasesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Canvases");
                if (Directory.Exists(canvasesDir))
                {
                    foreach (var file in Directory.GetFiles(canvasesDir))
                    {
                        File.Delete(file);
                    }
                    foreach (var dir in Directory.GetDirectories(canvasesDir))
                    {
                        Directory.Delete(dir, true);
                    }
                    Logger.LogInfo("All files and folders deleted from Canvases folder.");
                }
                else
                {
                    Logger.LogInfo("Canvases folder not found");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to delete Canvases folder contents", ex);
            }
        }

        public static string ParsePeriodFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            var idx = title.IndexOf(',');
            if (idx >= 0 && idx + 1 < title.Length)
            {
                return title.Substring(idx + 1).Trim();
            }
            return null;
        }

        /// <summary>
        /// Определяет индекс монитора по координатам X, Y
        /// </summary>
        /// <param name="x">X координата</param>
        /// <param name="y">Y координата</param>
        /// <returns>Индекс монитора (0 для основного монитора)</returns>
        public static int GetMonitorIndexByCoordinates(int x, int y)
        {
            try
            {
                // Determine monitor index
                int monitorIndex = 0;
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    if (Screen.AllScreens[i].Bounds.Contains(x, y))
                    {
                        monitorIndex = i;
                        break;
                    }
                }
                
                Logger.LogInfo($"Monitor index for coordinates ({x}, {y}): {monitorIndex}");
                return monitorIndex;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting monitor index for coordinates ({x}, {y}): {ex.Message}", ex);
                return 0; // Default to primary monitor
            }
        }

        /// <summary>
        /// Получает левую границу монитора по его индексу
        /// </summary>
        /// <param name="monitorIndex">Индекс монитора</param>
        /// <returns>Левая граница монитора в пикселях</returns>
        public static int GetMonitorLeftBoundary(int monitorIndex)
        {
            try
            {
                if (monitorIndex >= 0 && monitorIndex < Screen.AllScreens.Length)
                {
                    int leftBoundary = Screen.AllScreens[monitorIndex].Bounds.Left;
                    Logger.LogInfo($"Monitor {monitorIndex} left boundary: {leftBoundary}");
                    return leftBoundary;
                }
                else
                {
                    Logger.LogWarning($"Invalid monitor index: {monitorIndex}, using primary monitor");
                    return Screen.AllScreens[0].Bounds.Left;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting monitor left boundary for index {monitorIndex}: {ex.Message}", ex);
                return 0; // Default to 0 for primary monitor
            }
        }

        /// <summary>
        /// Извлекает символ из заголовка окна
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>Ключ символа (например, "XAUUSD") или null если символ не найден</returns>
        public static string ExtractSymbolFromWindowTitle(IntPtr windowHandle)
        {
            try
            {
                var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
                if (windowManagementService == null)
                    return null;
                
                string windowTitle = windowManagementService.GetWindowTitle(windowHandle);
                
                if (string.IsNullOrEmpty(windowTitle))
                {
                    Logger.LogDebug($"Empty window title for handle {windowHandle}");
                    return null;
                }
                
                Logger.LogDebug($"Window title: {windowTitle}");
                
                // Используем словарь символов из WindowManagementService
                var symbols = windowManagementService.Symbols;
                
                // Проверяем каждый символ в словаре
                foreach (var symbolPair in symbols)
                {
                    string symbolKey = symbolPair.Key;
                    string symbolValue = symbolPair.Value;
                    
                    if (windowTitle.Contains(symbolValue))
                    {
                        Logger.LogInfo($"Found symbol {symbolKey} in window title: {windowTitle}");
                        return symbolKey;
                    }
                }
                
                Logger.LogDebug($"No known symbol found in window title: {windowTitle}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error extracting symbol from window title: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Находит окно с указанным символом на конкретном мониторе
        /// </summary>
        /// <param name="symbolKey">Ключ символа (например, "XAUUSD")</param>
        /// <param name="monitorIndex">Индекс монитора (0 = основной, 1 = вторичный, и т.д.)</param>
        /// <returns>Handle окна или IntPtr.Zero если не найдено</returns>
        public static IntPtr FindWindowBySymbolOnMonitor(string symbolKey, int monitorIndex)
        {
            try
            {
                var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
                if (windowManagementService == null || string.IsNullOrEmpty(symbolKey))
                    return IntPtr.Zero;
                
                var symbols = windowManagementService.Symbols;
                if (!symbols.ContainsKey(symbolKey))
                {
                    Logger.LogWarning($"Symbol {symbolKey} not found in symbols dictionary");
                    return IntPtr.Zero;
                }
                
                string searchText = symbols[symbolKey];
                var screens = Screen.AllScreens;
                
                if (monitorIndex < 0 || monitorIndex >= screens.Length)
                {
                    Logger.LogWarning($"Invalid monitor index: {monitorIndex}");
                    return IntPtr.Zero;
                }
                
                var targetScreen = screens[monitorIndex];
                var targetBounds = targetScreen.Bounds;
                
                Logger.LogDebug($"Searching for window with symbol '{symbolKey}' (search text: '{searchText}') on monitor {monitorIndex} (bounds: {targetBounds})");
                
                // Используем EnumWindows для поиска всех окон
                List<IntPtr> candidateWindows = new List<IntPtr>();
                
                EnumWindowsProc callback = (hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        string windowTitle = windowManagementService.GetWindowTitle(hWnd);
                        if (!string.IsNullOrEmpty(windowTitle) && windowTitle.Contains(searchText))
                        {
                            // Проверяем, находится ли окно на целевом мониторе
                            RECT windowRect;
                            if (GetWindowRect(hWnd, out windowRect))
                            {
                                // Используем центр окна для определения монитора
                                int windowCenterX = (windowRect.Left + windowRect.Right) / 2;
                                int windowCenterY = (windowRect.Top + windowRect.Bottom) / 2;
                                
                                if (targetBounds.Contains(windowCenterX, windowCenterY))
                                {
                                    Logger.LogDebug($"Found window on monitor {monitorIndex}: {windowTitle} (Handle: {hWnd.ToInt64()})");
                                    candidateWindows.Add(hWnd);
                                }
                            }
                        }
                    }
                    return true; // Continue enumeration
                };
                
                EnumWindows(callback, IntPtr.Zero);
                
                if (candidateWindows.Count > 0)
                {
                    // Возвращаем первое найденное окно
                    IntPtr foundWindow = candidateWindows[0];
                    string foundTitle = windowManagementService.GetWindowTitle(foundWindow);
                    Logger.LogInfo($"Found window on monitor {monitorIndex} with symbol {symbolKey}: {foundTitle} (Handle: {foundWindow.ToInt64()})");
                    return foundWindow;
                }
                else
                {
                    Logger.LogWarning($"No window found with symbol '{symbolKey}' on monitor {monitorIndex}");
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error finding window by symbol on monitor: {ex.Message}", ex);
                return IntPtr.Zero;
            }
        }
    }
} 