using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScreenCaptureApp.Services
{
    public class WindowManagementService
    {
        // Windows API imports for window handling
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

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

        // Symbols dictionary
        private Dictionary<string, string> jforexSymbols = new Dictionary<string, string>
        {
            { "XAUUSD", "XAU/USD" },
            { "GBPJPY", "GBP/JPY" },
            { "USDJPY", "USD/JPY" },
            { "EURJPY", "EUR/JPY" },
            { "EURUSD", "EUR/USD" },
            { "GBPUSD", "GBP/USD" }            
        };

        private List<IntPtr> foundWindows = new List<IntPtr>();

        public event Action<string> StatusUpdated;

        /// <summary>
        /// Gets the symbols dictionary
        /// </summary>
        public Dictionary<string, string> Symbols => jforexSymbols;

        /// <summary>
        /// Finds windows that contain the specified text in their title and brings ALL of them to foreground
        /// </summary>
        /// <param name="searchText">Text to search for in window titles</param>
        /// <returns>True if any windows were found and brought to foreground, false otherwise</returns>
        public bool FindAndBringWindowToForeground(string searchText)
        {
            foundWindows.Clear();
            List<string> foundWindowTitles = new List<string>();
            int windowsFound = 0;
            
            try
            {
                EnumWindows(EnumWindowsCallback, IntPtr.Zero);
                
                // Find ALL windows that contain the search text in their title
                foreach (IntPtr windowHandle in foundWindows)
                {
                    if (IsWindowVisible(windowHandle))
                    {
                        string windowTitle = GetWindowTitle(windowHandle);
                        if (!string.IsNullOrEmpty(windowTitle) && windowTitle.Contains(searchText))
                        {
                            // Bring the window to foreground
                            if (SetForegroundWindow(windowHandle))
                            {
                                // Maximize the window if it's not already maximized
                                if (!IsZoomed(windowHandle))
                                {
                                    ShowWindow(windowHandle, SW_MAXIMIZE);
                                }
                                
                                foundWindowTitles.Add(windowTitle);
                                windowsFound++;
                                
                                // Small delay to ensure windows are brought to foreground properly
                                Thread.Sleep(100);
                            }
                        }
                    }
                }
                
                if (windowsFound > 0)
                {
                    string titlesText = string.Join(", ", foundWindowTitles);
                    string statusMessage = $"Found and brought {windowsFound} window(s) to foreground: {titlesText}";
                    StatusUpdated?.Invoke(statusMessage);
                    return true;
                }
                else
                {
                    string statusMessage = $"No windows found containing '{searchText}' in title";
                    StatusUpdated?.Invoke(statusMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string statusMessage = $"Error finding windows: {ex.Message}";
                StatusUpdated?.Invoke(statusMessage);
                return false;
            }
        }

        /// <summary>
        /// Finds and brings to foreground windows for a specific symbol
        /// </summary>
        /// <param name="symbolKey">Symbol key (e.g., "XAUUSD")</param>
        /// <returns>True if any windows were found and brought to foreground, false otherwise</returns>
        public bool FindAndBringSymbolWindowsToForeground(string symbolKey)
        {
            if (string.IsNullOrEmpty(symbolKey) || !jforexSymbols.ContainsKey(symbolKey))
            {
                StatusUpdated?.Invoke($"Invalid symbol key: {symbolKey}");
                return false;
            }

            string searchText = jforexSymbols[symbolKey];
            return FindAndBringWindowToForeground(searchText);
        }

        /// <summary>
        /// Callback function for EnumWindows to collect window handles
        /// </summary>
        private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            foundWindows.Add(hWnd);
            return true; // Continue enumeration
        }

        /// <summary>
        /// Gets the title of a window by its handle
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <returns>Window title or empty string if failed</returns>
        public string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                System.Text.StringBuilder title = new System.Text.StringBuilder(256);
                int result = GetWindowText(hWnd, title, title.Capacity);
                return result > 0 ? title.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the class name of a window by its handle
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <returns>Window class name or empty string if failed</returns>
        public string GetWindowClassName(IntPtr hWnd)
        {
            try
            {
                System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                int result = GetClassName(hWnd, className, className.Capacity);
                return result > 0 ? className.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the window handle for a specific symbol
        /// </summary>
        /// <param name="symbolKey">Symbol key (e.g., "XAUUSD")</param>
        /// <returns>Window handle for the symbol or IntPtr.Zero if not found</returns>
        public IntPtr GetSymbolWindowHandle(string symbolKey)
        {
            if (string.IsNullOrEmpty(symbolKey) || !jforexSymbols.ContainsKey(symbolKey))
            {
                StatusUpdated?.Invoke($"Invalid symbol key: {symbolKey}");
                return IntPtr.Zero;
            }

            string searchText = jforexSymbols[symbolKey];
            foundWindows.Clear();
            
            try
            {
                EnumWindows(EnumWindowsCallback, IntPtr.Zero);
                
                // Find the first window that contains the search text in its title
                foreach (IntPtr windowHandle in foundWindows)
                {
                    if (IsWindowVisible(windowHandle))
                    {
                        string windowTitle = GetWindowTitle(windowHandle);
                        if (windowTitle.Contains(searchText))
                        {
                            StatusUpdated?.Invoke($"Found window for {symbolKey}: {windowTitle}");
                            return windowHandle;
                        }
                    }
                }
                
                StatusUpdated?.Invoke($"No window found for symbol: {symbolKey}");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                string statusMessage = $"Error finding window for symbol {symbolKey}: {ex.Message}";
                StatusUpdated?.Invoke(statusMessage);
                return IntPtr.Zero;
            }
        }
    }
} 