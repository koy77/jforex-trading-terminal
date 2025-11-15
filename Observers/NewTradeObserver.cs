using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Helpers;

namespace ScreenCaptureApp.Observers
{
    /// <summary>
    /// Observer for handling new trade pattern setup (B and S keys)
    /// Monitors mouse clicks and sends P key to window, then sends trade data to MT4 Socket
    /// </summary>
    public class NewTradeObserver
    {
        // Mouse hook related fields
        private IntPtr mouseHook = IntPtr.Zero;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc mouseProc;

        // Mouse hook constants
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        // Keyboard constants
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_P = 0x50;
        private const int VK_ESCAPE = 0x1B;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(System.Drawing.Point p);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // State
        private bool _isTradingPatternActive = false;
        private string _tradeType = ""; // "buy" or "sell"
        private int _clickCount = 0;
        private double _entryPrice = 0;
        private double _stopLossPrice = 0;
        private string _symbol = "";
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private bool _wasCanvasWindowVisible = false;

        // Dependencies
        private readonly Dispatcher _dispatcher;
        private readonly Func<bool> _isCanvasWindowVisible;
        private readonly Action _hideCanvasWindow;
        private readonly Action _showCanvasWindow;
        private readonly Func<IntPtr> _getTargetWindowHandle;
        private readonly Func<string> _getActiveSymbol;
        private readonly SimpleTradingOverlay _simpleTradingOverlay;
        private readonly Mt4SocketService _mt4SocketService;
        private readonly JForexWindowsManagerService _jforexWindowsManagerService;

        public NewTradeObserver(
            Dispatcher dispatcher,
            Func<bool> isCanvasWindowVisible,
            Action hideCanvasWindow,
            Action showCanvasWindow,
            Func<IntPtr> getTargetWindowHandle,
            Func<string> getActiveSymbol,
            SimpleTradingOverlay simpleTradingOverlay)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _isCanvasWindowVisible = isCanvasWindowVisible ?? throw new ArgumentNullException(nameof(isCanvasWindowVisible));
            _hideCanvasWindow = hideCanvasWindow ?? throw new ArgumentNullException(nameof(hideCanvasWindow));
            _showCanvasWindow = showCanvasWindow ?? throw new ArgumentNullException(nameof(showCanvasWindow));
            _getTargetWindowHandle = getTargetWindowHandle ?? throw new ArgumentNullException(nameof(getTargetWindowHandle));
            _getActiveSymbol = getActiveSymbol ?? throw new ArgumentNullException(nameof(getActiveSymbol));
            _simpleTradingOverlay = simpleTradingOverlay ?? throw new ArgumentNullException(nameof(simpleTradingOverlay));

            _mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            _jforexWindowsManagerService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
        }

        /// <summary>
        /// Handles B key press - starts buy trade pattern
        /// </summary>
        public void OnBKeyPressed()
        {
            Logger.LogInfo("NewTradeObserver: B key pressed - starting buy trade pattern");
            StartTradingPattern("buy");
        }

        /// <summary>
        /// Handles S key press - starts sell trade pattern
        /// </summary>
        public void OnSKeyPressed()
        {
            Logger.LogInfo("NewTradeObserver: S key pressed - starting sell trade pattern");
            StartTradingPattern("sell");
        }

        /// <summary>
        /// Handles Escape key press - cancels trade pattern
        /// </summary>
        public void OnEscapeKeyPressed()
        {
            if (_isTradingPatternActive)
            {
                Logger.LogInfo("NewTradeObserver: Escape key pressed - canceling trade pattern");
                CancelTradingPattern();
            }
        }

        /// <summary>
        /// Starts the trading pattern mode
        /// </summary>
        private void StartTradingPattern(string tradeType)
        {
            try
            {
                if (_isTradingPatternActive)
                {
                    Logger.LogWarning("NewTradeObserver: Trading pattern already active, canceling previous one");
                    CancelTradingPattern();
                }

                _tradeType = tradeType;
                _clickCount = 0;
                _entryPrice = 0;
                _stopLossPrice = 0;
                _symbol = "";

                // Check if Canvas Window is visible
                _wasCanvasWindowVisible = _isCanvasWindowVisible();
                if (_wasCanvasWindowVisible)
                {
                    Logger.LogInfo("NewTradeObserver: Canvas Window is visible, hiding it");
                    _dispatcher.Invoke(() => _hideCanvasWindow());
                }

                // Target window will be determined automatically on mouse click
                _targetWindowHandle = IntPtr.Zero;

                // Show Trading Toolbar
                if (_simpleTradingOverlay != null)
                {
                    _dispatcher.Invoke(() =>
                    {
                        _simpleTradingOverlay.SetTradingToolbarEnabled(true);
                        // Показываем статус торгового паттерна в TradingToolbar
                        var tradingToolbar = _simpleTradingOverlay.GetTradingToolbar();
                        if (tradingToolbar != null)
                        {
                            tradingToolbar.ShowTradingPatternStatus(tradeType);
                        }
                    });
                }

                // Setup mouse hook
                SetupMouseHook();

                _isTradingPatternActive = true;
                Logger.LogInfo($"NewTradeObserver: Trading pattern started - Type: {tradeType}. Waiting for mouse click to determine target window...");
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error starting trading pattern: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Completes the trading pattern (both prices are set)
        /// </summary>
        private void CompleteTradingPattern()
        {
            try
            {
                if (!_isTradingPatternActive)
                    return;

                // Remove mouse hook - больше не слушаем клики
                RemoveMouseHook();

                // Скрываем статус торгового паттерна в TradingToolbar
                if (_simpleTradingOverlay != null)
                {
                    _dispatcher.Invoke(() =>
                    {
                        var tradingToolbar = _simpleTradingOverlay.GetTradingToolbar();
                        if (tradingToolbar != null)
                        {
                            tradingToolbar.HideTradingPatternStatus();
                        }
                    });
                }

                // Reset state
                _isTradingPatternActive = false;
                _clickCount = 0;
                _entryPrice = 0;
                _stopLossPrice = 0;
                _symbol = "";
                _targetWindowHandle = IntPtr.Zero;

                // Restore Canvas Window if it was visible
                if (_wasCanvasWindowVisible)
                {
                    Logger.LogInfo("NewTradeObserver: Restoring Canvas Window after pattern completion");
                    _dispatcher.Invoke(() => _showCanvasWindow());
                }

                Logger.LogInfo("NewTradeObserver: Trading pattern completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error completing trading pattern: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cancels the trading pattern
        /// </summary>
        private void CancelTradingPattern()
        {
            try
            {
                if (!_isTradingPatternActive)
                    return;

                // Remove mouse hook
                RemoveMouseHook();

                // Скрываем статус торгового паттерна в TradingToolbar
                if (_simpleTradingOverlay != null)
                {
                    _dispatcher.Invoke(() =>
                    {
                        var tradingToolbar = _simpleTradingOverlay.GetTradingToolbar();
                        if (tradingToolbar != null)
                        {
                            tradingToolbar.HideTradingPatternStatus();
                        }
                    });
                }

                // Reset state
                _isTradingPatternActive = false;
                _clickCount = 0;
                _entryPrice = 0;
                _stopLossPrice = 0;
                _symbol = "";
                _targetWindowHandle = IntPtr.Zero;

                // Restore Canvas Window if it was visible
                if (_wasCanvasWindowVisible)
                {
                    Logger.LogInfo("NewTradeObserver: Restoring Canvas Window");
                    _dispatcher.Invoke(() => _showCanvasWindow());
                }

                Logger.LogInfo("NewTradeObserver: Trading pattern canceled");
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error canceling trading pattern: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sets up the mouse hook to track clicks
        /// </summary>
        private void SetupMouseHook()
        {
            try
            {
                if (mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(mouseHook);
                }

                mouseProc = MouseHookCallback;
                mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, GetModuleHandle(null), 0);

                if (mouseHook == IntPtr.Zero)
                {
                    Logger.LogError("NewTradeObserver: Failed to set mouse hook");
                }
                else
                {
                    Logger.LogInfo("NewTradeObserver: Mouse hook set up successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error setting up mouse hook: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Removes the mouse hook
        /// </summary>
        private void RemoveMouseHook()
        {
            try
            {
                if (mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(mouseHook);
                    mouseHook = IntPtr.Zero;
                    Logger.LogInfo("NewTradeObserver: Mouse hook removed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error removing mouse hook: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Mouse hook callback to handle clicks
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && _isTradingPatternActive)
            {
                try
                {
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    POINT clickPoint = hookStruct.pt;

                    // Get window under cursor automatically
                    IntPtr clickedWindow = WindowFromPoint(new System.Drawing.Point(clickPoint.X, clickPoint.Y));

                    if (clickedWindow == IntPtr.Zero)
                    {
                        Logger.LogWarning($"NewTradeObserver: No window found at click point ({clickPoint.X}, {clickPoint.Y})");
                        return CallNextHookEx(mouseHook, nCode, wParam, lParam);
                    }

                    // On first click, set target window and get symbol
                    if (_targetWindowHandle == IntPtr.Zero)
                    {
                        _targetWindowHandle = clickedWindow;
                        
                        // Try to get symbol from window title
                        var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
                        if (windowManagementService != null)
                        {
                            string windowTitle = windowManagementService.GetWindowTitle(_targetWindowHandle);
                            _symbol = MainHelper.ExtractSymbolFromWindowTitle(_targetWindowHandle);
                            if (string.IsNullOrEmpty(_symbol))
                            {
                                Logger.LogWarning($"NewTradeObserver: Could not extract symbol from window title: {windowTitle}");
                            }
                            else
                            {
                                Logger.LogInfo($"NewTradeObserver: Symbol extracted from window: {_symbol}");
                            }
                        }
                    }

                    // Only process clicks on the target window
                    if (clickedWindow == _targetWindowHandle)
                    {
                        Logger.LogInfo($"NewTradeObserver: Mouse click detected on target window at ({clickPoint.X}, {clickPoint.Y})");

                        // Convert screen coordinates to client coordinates
                        POINT clientPoint = clickPoint;
                        ScreenToClient(_targetWindowHandle, ref clientPoint);

                        // Send P key to window
                        SendPKeyToWindow(_targetWindowHandle);

                        // Increment click count
                        _clickCount++;

                        if (_clickCount == 1)
                        {
                            Logger.LogInfo("NewTradeObserver: First click - Entry Level");
                            // Entry level will be set by the application when it receives the price level event
                        }
                        else if (_clickCount == 2)
                        {
                            Logger.LogInfo("NewTradeObserver: Second click - Stop Loss Level");
                            // Stop loss level will be set by the application when it receives the price level event
                            // After both levels are set, we'll send to MT4 Socket
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"NewTradeObserver: Click detected on different window, ignoring (target: {_targetWindowHandle.ToInt64()}, clicked: {clickedWindow.ToInt64()})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"NewTradeObserver: Error in mouse hook callback: {ex.Message}", ex);
                }
            }

            return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        /// <summary>
        /// Sends P key to the target window
        /// </summary>
        private void SendPKeyToWindow(IntPtr windowHandle)
        {
            try
            {
                // Bring window to foreground
                SetForegroundWindow(windowHandle);
                System.Threading.Thread.Sleep(100);

                // Send P key using PostMessage
                bool keyDownResult = PostMessage(windowHandle, WM_KEYDOWN, (IntPtr)VK_P, IntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                bool keyUpResult = PostMessage(windowHandle, WM_KEYUP, (IntPtr)VK_P, IntPtr.Zero);

                if (keyDownResult && keyUpResult)
                {
                    Logger.LogInfo($"NewTradeObserver: P key sent successfully to window {windowHandle.ToInt64()}");
                }
                else
                {
                    Logger.LogWarning($"NewTradeObserver: Failed to send P key to window {windowHandle.ToInt64()}");
                }

                // Send Escape after delay (as in Python code)
                Task.Delay(400).ContinueWith(_ =>
                {
                    PostMessage(windowHandle, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                    System.Threading.Thread.Sleep(50);
                    PostMessage(windowHandle, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                    Logger.LogInfo($"NewTradeObserver: Escape key sent after P key");
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error sending P key to window: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles JForex chart object received event - called when Entry or Stop Loss level is set
        /// </summary>
        public void OnJForexChartObjectReceived(ScreenCaptureApp.Models.JForexChartObjectData jforexChartObject)
        {
            if (!_isTradingPatternActive)
                return;

            try
            {
                // Check if this is a PriceMarker object
                if (jforexChartObject.ObjectType != ScreenCaptureApp.Models.JForexChartObjectType.PriceMarker)
                    return;

                // Check if symbol matches
                if (!string.IsNullOrEmpty(_symbol) && !string.IsNullOrEmpty(jforexChartObject.Symbol))
                {
                    if (!_symbol.Equals(jforexChartObject.Symbol, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogDebug($"NewTradeObserver: Symbol mismatch - expected {_symbol}, got {jforexChartObject.Symbol}");
                        return;
                    }
                }

                double price = Convert.ToDouble(jforexChartObject.Price);

                // First price level sets Entry Price
                if (_entryPrice == 0)
                {
                    _entryPrice = price;
                    Logger.LogInfo($"NewTradeObserver: Entry price set: {_entryPrice} for symbol {jforexChartObject.Symbol}");
                    
                    // Обновляем UI в TradingToolbar
                    if (_simpleTradingOverlay != null)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            var tradingToolbar = _simpleTradingOverlay.GetTradingToolbar();
                            if (tradingToolbar != null)
                            {
                                tradingToolbar.UpdateTradingPatternEntry(_entryPrice);
                            }
                        });
                    }
                }
                // Second price level sets Stop Loss Price
                else if (_stopLossPrice == 0)
                {
                    _stopLossPrice = price;
                    Logger.LogInfo($"NewTradeObserver: Stop loss price set: {_stopLossPrice} for symbol {jforexChartObject.Symbol}");

                    // Обновляем UI в TradingToolbar
                    if (_simpleTradingOverlay != null)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            var tradingToolbar = _simpleTradingOverlay.GetTradingToolbar();
                            if (tradingToolbar != null)
                            {
                                tradingToolbar.UpdateTradingPatternStopLoss(_stopLossPrice);
                            }
                        });
                    }

                    // Both levels are set, send to MT4 Socket and complete pattern
                    if (_entryPrice > 0 && _stopLossPrice > 0)
                    {
                        SendTradeToMt4();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error handling JForex chart object: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends trade data to MT4 Socket
        /// </summary>
        private async void SendTradeToMt4()
        {
            try
            {
                if (_mt4SocketService == null || !_mt4SocketService.IsConnected)
                {
                    Logger.LogWarning("NewTradeObserver: MT4 Socket Service is not connected");
                    return;
                }

                if (string.IsNullOrEmpty(_symbol) || _entryPrice <= 0 || _stopLossPrice <= 0)
                {
                    Logger.LogWarning("NewTradeObserver: Invalid trade data, cannot send to MT4");
                    return;
                }

                // Get risk from settings (default to 1.0 if not available)
                double risk = 1.0;
                // TODO: Get risk from settings if available

                // Send trade command to MT4
                bool success = await _mt4SocketService.SendNewOrderCommand(
                    _symbol,
                    _tradeType,
                    _entryPrice,
                    _stopLossPrice,
                    risk
                );

                if (success)
                {
                    Logger.LogInfo($"NewTradeObserver: Trade sent to MT4 successfully - {_tradeType} {_symbol} Entry:{_entryPrice} SL:{_stopLossPrice}");
                    
                    // Complete trading pattern after successful send (both prices are set)
                    CompleteTradingPattern();
                }
                else
                {
                    Logger.LogError("NewTradeObserver: Failed to send trade to MT4");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"NewTradeObserver: Error sending trade to MT4: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            RemoveMouseHook();
        }
    }
}

