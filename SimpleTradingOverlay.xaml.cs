using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Threading;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp
{
    public partial class SimpleTradingOverlay : Window
    {
        // Mouse hook related fields
        private IntPtr mouseHook = IntPtr.Zero; a
        private HwndSource source;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc mouseProc;

        // Mouse hook constants
        private const int WH_MOUSE_LL = 14;
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

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
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
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private readonly ToolbarSettingsManager _toolbarSettingsManager;
        private readonly BrokerState _brokerState;
        private readonly DurationState _durationState;
        private readonly WindowManagementService _windowManagementService;
        private readonly DispatcherTimer _updateTimer;
        private IntPtr _currentWindowHandle = IntPtr.Zero;
        private bool _isEnabled = true;
        


        public SimpleTradingOverlay()
        {
            InitializeComponent();
            
            _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
            _brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            _durationState = ServiceContainer.Instance.GetService<DurationState>();
            _windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();

            // Инициализация окна
            InitializeWindow();
            
            // Настройка событий TradingToolbar
            SetupTradingToolbarEvents();
            
            // Таймер для обновления позиции
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            // Установка глобального хука мыши
            SetupMouseHook();
        }

        private void InitializeWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.ResizeMode = ResizeMode.NoResize;
        }

        private void SetupTradingToolbarEvents()
        {
            TradingToolbar.RiskChanged += (risk) => {
                if (_currentWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(_currentWindowHandle.ToInt64(), risk: risk);
            };

            TradingToolbar.BrokerChanged += (broker) => {
                _brokerState.CurrentBroker = broker;
                UpdateTradingToolbarUiByBroker(broker);
                if (_currentWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(_currentWindowHandle.ToInt64(), broker: broker);
            };

            TradingToolbar.DurationChanged += (duration) => {
                _durationState.CurrentDuration = duration;
                if (_currentWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(_currentWindowHandle.ToInt64(), duration: duration);
            };
        }

        private void UpdateTradingToolbarUiByBroker(BrokerType brokerType)
        {
            if (brokerType == BrokerType.Forex)
            {
                TradingToolbar.SetModeLabel("TRADING!!", true);
                TradingToolbar.ShowDurationPanel(false);
            }
            else
            {
                TradingToolbar.SetModeLabel("BINARY!!", true);
                TradingToolbar.ShowDurationPanel(true);
            }
        }

        /// <summary>
        /// Извлекает символ из заголовка окна JForex используя WindowManagementService
        /// </summary>
        /// <param name="windowHandle">Handle окна</param>
        /// <returns>Ключ символа (например, "XAUUSD") или null если символ не найден</returns>
        private string ExtractSymbolFromWindowTitle(IntPtr windowHandle)
        {
            try
            {
                string windowTitle = _windowManagementService.GetWindowTitle(windowHandle);
                
                if (string.IsNullOrEmpty(windowTitle))
                {
                    Logger.LogDebug($"Empty window title for handle {windowHandle}");
                    return null;
                }

                Logger.LogDebug($"Window title: {windowTitle}");

                // Используем словарь символов из WindowManagementService
                var symbols = _windowManagementService.Symbols;
                
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
                Logger.LogError($"Error extracting symbol from window title for handle {windowHandle}", ex);
                return null;
            }
        }

        private void SetupMouseHook()
        {
            try
            {
                mouseProc = new LowLevelMouseProc(MouseHookCallback);
                IntPtr moduleHandle = GetModuleHandle("user32.dll");
                mouseHook = SetWindowsHookEx(WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(mouseProc), moduleHandle, 0);
                
                if (mouseHook == IntPtr.Zero)
                {
                    Logger.LogError("Failed to set mouse hook");
                }
                else
                {
                    Logger.LogInfo("Mouse hook set successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error setting up mouse hook", ex);
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isEnabled)
            {
                int message = wParam.ToInt32();
                
                if (message == WM_MOUSEMOVE)
                {
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    
                    // Получаем окно под курсором
                    IntPtr windowHandle = WindowFromPoint(new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y));
                    
                    if (windowHandle != IntPtr.Zero && IsWindowVisible(windowHandle))
                    {
                        // Проверяем, что это не наше окно
                        if (windowHandle != new WindowInteropHelper(this).Handle)
                        {
                            // Обновляем позицию оверлея на новом окне
                            UpdateOverlayPosition(windowHandle);
                        }
                    }
                }
            }
            
            return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        private void UpdateOverlayPosition(IntPtr windowHandle)
        {
            if (windowHandle == _currentWindowHandle) return;

            try
            {
                // Проверяем, что окно видимо
                if (!IsWindowVisible(windowHandle))
                {
                    Logger.LogDebug($"Window {windowHandle} is not visible, skipping");
                    return;
                }

                // Извлекаем символ из заголовка окна
                string symbol = ExtractSymbolFromWindowTitle(windowHandle);
                
                if (string.IsNullOrEmpty(symbol))
                {
                    Logger.LogDebug($"No symbol found in window {windowHandle}, keeping overlay on current window");
                    return; // Не переключаем оверлей, если символ не найден
                }

                // Символ найден, обновляем позицию оверлея
                _currentWindowHandle = windowHandle;
                
                // Получаем размеры и позицию окна
                RECT windowRect;
                if (GetWindowRect(windowHandle, out windowRect))
                {
                    // Позиционируем оверлей в верхней части окна
                    double left = windowRect.Left;
                    double top = windowRect.Top;
                    double width = windowRect.Right - windowRect.Left;
                    
                    // Устанавливаем размер оверлея равным ширине окна
                    this.Width = width;
                    this.Left = left;
                    this.Top = top;
                    
                    // Применяем настройки тулбара для этого окна
                    ApplyToolbarSettings(windowHandle);
                    
                    // Показываем окно
                    if (!this.IsVisible)
                    {
                        this.Show();
                    }
                    
                    Logger.LogInfo($"Overlay positioned on window {windowHandle} (symbol: {symbol}): Left={left}, Top={top}, Width={width}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating overlay position for window {windowHandle}", ex);
            }
        }

        private void ApplyToolbarSettings(IntPtr windowHandle)
        {
            try
            {
                var toolbarSettings = _toolbarSettingsManager.GetSettings(windowHandle.ToInt64());
                if (toolbarSettings != null)
                {
                    // Применяем сохраненные настройки
                    TradingToolbar.HighlightSelectedRiskButton(toolbarSettings.Risk);
                    TradingToolbar.HighlightSelectedDurationButton(toolbarSettings.Duration);
                    TradingToolbar.HighlightSelectedBroker(toolbarSettings.Broker);
                    
                    // Обновляем состояния
                    _brokerState.CurrentBroker = toolbarSettings.Broker;
                    _durationState.CurrentDuration = toolbarSettings.Duration;
                    
                    // Обновляем UI в зависимости от брокера
                    UpdateTradingToolbarUiByBroker(toolbarSettings.Broker);
                    
                    Logger.LogInfo($"Applied toolbar settings for window {windowHandle}: Risk={toolbarSettings.Risk}, Duration={toolbarSettings.Duration}, Broker={toolbarSettings.Broker}");
                }
                else
                {
                    // Применяем настройки по умолчанию
                    TradingToolbar.HighlightSelectedRiskButton(1);
                    TradingToolbar.HighlightSelectedDurationButton(2);
                    TradingToolbar.HighlightSelectedBroker(BrokerType.Forex);
                    UpdateTradingToolbarUiByBroker(BrokerType.Forex);
                    
                    Logger.LogInfo($"Applied default toolbar settings for window {windowHandle}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying toolbar settings for window {windowHandle}", ex);
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Дополнительная проверка позиции курсора
            if (_isEnabled)
            {
                var cursorPos = System.Windows.Forms.Cursor.Position;
                IntPtr windowHandle = WindowFromPoint(new System.Drawing.Point(cursorPos.X, cursorPos.Y));
                
                if (windowHandle != IntPtr.Zero && windowHandle != _currentWindowHandle && IsWindowVisible(windowHandle))
                {
                    // Проверяем символ в заголовке окна перед обновлением позиции
                    string symbol = ExtractSymbolFromWindowTitle(windowHandle);
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        UpdateOverlayPosition(windowHandle);
                    }
                }
            }
        }

        public void Enable()
        {
            _isEnabled = true;
            Logger.LogInfo("SimpleTradingOverlay enabled");
        }

        public void Disable()
        {
            _isEnabled = false;
            this.Hide();
            Logger.LogInfo("SimpleTradingOverlay disabled");
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            
            if (mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
            
            base.OnClosed(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        }
    }
} 