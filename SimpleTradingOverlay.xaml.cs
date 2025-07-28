using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Drawing;
using System.IO;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Helpers;

namespace ScreenCaptureApp
{
    public partial class SimpleTradingOverlay : Window
    {
        // Mouse hook related fields
        private IntPtr mouseHook = IntPtr.Zero;
        private HwndSource source;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc mouseProc;

        // Mouse hook constants
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        
        // Keyboard message constants
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_ESCAPE = 0x1B;
        private const int SW_RESTORE = 9;

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

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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
        private readonly HotkeysService _hotkeysService;
        private IntPtr _currentWindowHandle = IntPtr.Zero;
        private bool _isEnabled = true;
        private int _lastMouseX = 0;
        private int _lastMouseY = 0;
        private TradingPatternOverlay _patternOverlay = null;
        
        // Паттерн создания объекта трейдинга
        private bool _isTradingPatternActive = false;
        private TradingPatternData _currentTradingPattern = null;
        private int _clickCount = 0;
        
        // События для паттерна трейдинга
        public event EventHandler<TradingPatternData> TradingPatternCompleted;
        public event EventHandler<TradingPatternData> TradingPatternCancelled;
        
        public SimpleTradingOverlay()
        {
            InitializeComponent();
            
            _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
            _brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            _durationState = ServiceContainer.Instance.GetService<DurationState>();
            _windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
            _hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();

            // Инициализация окна
            InitializeWindow();
            
            // Настройка событий TradingToolbar
            SetupTradingToolbarEvents();
            
            // Инициализация переменных для троттлинга мыши
            _lastMouseX = 0;
            _lastMouseY = 0;

            // Установка символа по умолчанию
            TradingToolbar.SetSymbol("UNKNOWN");

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



        public void OnSKeyPressed()
        {
            Logger.LogDebug("SimpleTradingOverlay.OnSKeyPressed() called");
            
            if (!_isEnabled) 
            {
                Logger.LogWarning("SimpleTradingOverlay is disabled, ignoring S key press");
                return;
            }

            Logger.LogInfo("S key pressed - starting trading pattern creation");
            
            // Начинаем паттерн создания объекта трейдинга
            StartTradingPattern();
        }

        public void OnEscapeKeyPressed()
        {
            if (!_isEnabled) return;

            if (_isTradingPatternActive)
            {
                Logger.LogInfo("Escape key pressed - cancelling trading pattern");
                CancelTradingPattern();
            }
        }

        private void StartTradingPattern()
        {
            if (_isTradingPatternActive)
            {
                Logger.LogWarning("Trading pattern already active, cancelling previous one");
                CancelTradingPattern();
            }

            _isTradingPatternActive = true;
            _clickCount = 0;
            _currentTradingPattern = new TradingPatternData
            {
                WindowHandle = _currentWindowHandle,
                Symbol = ExtractSymbolFromWindowTitle(_currentWindowHandle)
            };

            Logger.LogInfo($"Started trading pattern creation for window {_currentWindowHandle} (symbol: {_currentTradingPattern.Symbol})");
        }

        private void CancelTradingPattern()
        {
            if (!_isTradingPatternActive) return;

            _isTradingPatternActive = false;

            _clickCount = 0;
            
            // Закрываем окно рамки если оно открыто
            if (_patternOverlay != null && _patternOverlay.IsVisible)
            {
                _patternOverlay.Close();
            }
            
            if (_currentTradingPattern != null)
            {
                _currentTradingPattern.Status = TradingPatternStatus.Cancelled;
                TradingPatternCancelled?.Invoke(this, _currentTradingPattern);
                Logger.LogInfo("Trading pattern cancelled");
            }
            
            _currentTradingPattern = null;
        }

        private void CompleteTradingPattern()
        {
            if (!_isTradingPatternActive || _currentTradingPattern == null) return;

            _isTradingPatternActive = false;
            _clickCount = 0;
            
            _currentTradingPattern.Status = TradingPatternStatus.Completed;
            TradingPatternCompleted?.Invoke(this, _currentTradingPattern);
            
            // Показываем желтую рамку вокруг области трейдинга
            ShowTradingPatternRectangle(_currentTradingPattern.FirstClick, _currentTradingPattern.SecondClick);

            // --- Сохраняем CaptureData с направлением ---
            var first = _currentTradingPattern.FirstClick;
            var second = _currentTradingPattern.SecondClick;
            var direction = second.Y < first.Y ? "Up" : "Down";
            
            // Получаем размеры и позицию окна для конвертации координат
            RECT windowRect;
            if (!GetWindowRect(_currentWindowHandle, out windowRect))
            {
                Logger.LogError("Failed to get window rect for trading pattern capture");
                return;
            }
            
            // Конвертируем координаты из виртуального экрана в локальные координаты окна
            var windowPoint1 = ConvertVirtualScreenToWindowCoordinates(first.X, first.Y, windowRect);
            var windowPoint2 = ConvertVirtualScreenToWindowCoordinates(second.X, second.Y, windowRect);
            

            // Вычисляем границы области с паддингом 20 пикселей в локальных координатах окна
            int left = Math.Min(windowPoint1.X, windowPoint2.X) - 20;
            int top = Math.Min(windowPoint1.Y, windowPoint2.Y) - 60;
            int right = Math.Max(windowPoint1.X, windowPoint2.X) + 20;
            int bottom = Math.Max(windowPoint1.Y, windowPoint2.Y) + 60;
            int width = right - left;
            int height = bottom - top;
            
            // Определяем монитор
            int monitorIndex = 0;
            for (int i = 0; i < System.Windows.Forms.Screen.AllScreens.Length; i++)
            {
                if (System.Windows.Forms.Screen.AllScreens[i].Bounds.Contains(first.X, first.Y))
                {
                    monitorIndex = i;
                    break;
                }
            }
            
            var capture = new CaptureData
            {
                X = left,
                Y = top,
                Width = width,
                Height = height,
                Handle = _currentWindowHandle.ToInt64(),
                Monitor = monitorIndex,
                Timestamp = DateTime.Now.ToString("o"),
                Symbol = _currentTradingPattern.Symbol,
                Risk = _toolbarSettingsManager.GetSettings(_currentWindowHandle.ToInt64())?.Risk ?? 1,
                Source = "window",
                Broker = _brokerState.CurrentBroker.ToString(),
                Duration = _durationState.CurrentDuration,
                Model = "OHLC_rectangle",
                Period = null,
                Direction = direction
            };
            
            // Создаем мета-данные для CaptureData
            capture.Meta = CreateMetadataForCapture(capture);
            
            var db = ServiceContainer.Instance.GetService<DatabaseService>();
            var screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
            
            // Сохраняем CaptureData с мета-данными
            db.SaveCapture(capture);
            
            // Делаем скриншот области в локальных координатах окна
            var debugInfo = screenshotService.CaptureScreenAreaDebug(capture.X, capture.Y, capture.Width, capture.Height, capture.Monitor, capture.ID);
            capture.ScreenshotPath = debugInfo.ScreenshotPath;
            db.UpdateCapture(capture);
            
            Logger.LogInfo($"TradingPattern CaptureData saved: Symbol={capture.Symbol}, Direction={capture.Direction}, X={capture.X}, Y={capture.Y}, W={capture.Width}, H={capture.Height}, Screenshot={capture.ScreenshotPath}");
            // --- конец блока сохранения ---

            Logger.LogInfo($"Trading pattern completed: FirstClick={_currentTradingPattern.FirstClick}, SecondClick={_currentTradingPattern.SecondClick}");
            
            _currentTradingPattern = null;
        }

        private void HandleMouseClickForTradingPattern(IntPtr lParam)
        {
            if (!_isTradingPatternActive || _currentTradingPattern == null) return;

            try
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                var clickPoint = new TradingPoint(hookStruct.pt.X, hookStruct.pt.Y);
                
                _clickCount++;
                
                if (_clickCount == 1)
                {
                    // Первый клик
                    _currentTradingPattern.FirstClick = clickPoint;
                    Logger.LogInfo($"First click recorded at {clickPoint}");
                }
                else if (_clickCount == 2)
                {
                    // Второй клик - завершаем паттерн
                    _currentTradingPattern.SecondClick = clickPoint;
                    Logger.LogInfo($"Second click recorded at {clickPoint}");
                    CompleteTradingPattern();
                }
                else
                {
                    // Больше двух кликов - игнорируем
                    Logger.LogWarning($"Ignoring click {_clickCount} - pattern requires exactly 2 clicks");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling mouse click for trading pattern", ex);
            }
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
                    // Logger.LogDebug($"Empty window title for handle {windowHandle}");
                    return null;
                }

                // Logger.LogDebug($"Window title: {windowTitle}");

                // Используем словарь символов из WindowManagementService
                var symbols = _windowManagementService.Symbols;
                
                // Проверяем каждый символ в словаре
                foreach (var symbolPair in symbols)
                {
                    string symbolKey = symbolPair.Key;
                    string symbolValue = symbolPair.Value;
                    
                    if (windowTitle.Contains(symbolValue))
                    {
                        // Logger.LogInfo($"Found symbol {symbolKey} in window title: {windowTitle}");
                        return symbolKey;
                    }
                }

                // Logger.LogDebug($"No known symbol found in window title: {windowTitle}");
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
                    
                    // Троттлинг: проверяем, переместилась ли мышь на достаточное расстояние
                    int deltaX = Math.Abs(hookStruct.pt.X - _lastMouseX);
                    int deltaY = Math.Abs(hookStruct.pt.Y - _lastMouseY);
                    
                    // Обновляем позицию только если мышь переместилась на 100 пикселей или больше
                    if (deltaX >= 100 || deltaY >= 100)
                    {
                        _lastMouseX = hookStruct.pt.X;
                        _lastMouseY = hookStruct.pt.Y;
                        
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
                else if (message == WM_LBUTTONDOWN && _isTradingPatternActive)
                {
                    // Обработка клика мыши для паттерна трейдинга
                    HandleMouseClickForTradingPattern(lParam);
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
                    // Logger.LogDebug($"No symbol found in window {windowHandle}, keeping overlay on current window");
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
                    
                    // TradingToolbar теперь позиционируется справа через Canvas.Right="0"
                    
                    // Устанавливаем символ в тулбаре
                    TradingToolbar.SetSymbol(symbol);
                    
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
                // Устанавливаем символ для текущего окна
                string symbol = ExtractSymbolFromWindowTitle(windowHandle);
                TradingToolbar.SetSymbol(symbol);
                
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
            // Отменяем активный паттерн если он есть
            if (_isTradingPatternActive)
            {
                CancelTradingPattern();
            }
            
            // Закрываем окно рамки
            if (_patternOverlay != null && _patternOverlay.IsVisible)
            {
                _patternOverlay.Close();
            }
            
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

        /// <summary>
        /// Показывает желтую рамку вокруг области трейдинга
        /// </summary>
        private void ShowTradingPatternRectangle(TradingPoint firstClick, TradingPoint secondClick)
        {
            try
            {
                if (_currentWindowHandle == IntPtr.Zero)
                {
                    Logger.LogWarning("Cannot show trading pattern rectangle: no current window handle");
                    return;
                }

                // Получаем размеры и позицию окна
                RECT windowRect;
                if (!GetWindowRect(_currentWindowHandle, out windowRect))
                {
                    Logger.LogError("Failed to get window rect for trading pattern rectangle");
                    return;
                }

                Logger.LogInfo($"Current window handle: {_currentWindowHandle}");
                Logger.LogInfo($"Window rect: Left={windowRect.Left}, Top={windowRect.Top}, Right={windowRect.Right}, Bottom={windowRect.Bottom}");

                // Вычисляем границы области с паддингом 20 пикселей в виртуальных координатах экрана
                int left = Math.Min(firstClick.X, secondClick.X) - 20;
                int top = Math.Min(firstClick.Y, secondClick.Y) - 20;
                int right = Math.Max(firstClick.X, secondClick.X) + 20;
                int bottom = Math.Max(firstClick.Y, secondClick.Y) + 20;

                int width = right - left;
                int height = bottom - top;

                Logger.LogInfo($"Original clicks: FirstClick=({firstClick.X}, {firstClick.Y}), SecondClick=({secondClick.X}, {secondClick.Y})");
                Logger.LogInfo($"Pattern area: Left={left}, Top={top}, Width={width}, Height={height}");

                // Создаем или переиспользуем окно для рамки
                if (_patternOverlay == null || !_patternOverlay.IsVisible)
                {
                    _patternOverlay = new TradingPatternOverlay();
                }

                // Показываем рамку в абсолютных координатах экрана
                _patternOverlay.ShowPattern(left, top, width, height);
                
                // Отправляем клавишу Escape в целевое окно
                SendEscapeToTargetWindow();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error showing trading pattern rectangle", ex);
            }
        }

        /// <summary>
        /// Отправляет клавишу Escape в целевое окно в отдельном потоке
        /// </summary>
        private void SendEscapeToTargetWindow()
        {
            if (_currentWindowHandle == IntPtr.Zero)
            {
                Logger.LogWarning("Cannot send Escape key: no current window handle");
                return;
            }

            Logger.LogInfo($"Scheduling Escape key send to window handle: {_currentWindowHandle}");

            // Запускаем отправку клавиши в отдельном потоке
            System.Threading.Thread escapeThread = new System.Threading.Thread(() =>
            {
                try
                {
                    Logger.LogInfo($"Escape thread started for window handle: {_currentWindowHandle}");

                    // // Активируем окно
                    // SetForegroundWindow(_currentWindowHandle);
                    // ShowWindow(_currentWindowHandle, SW_RESTORE);

                    // Небольшая задержка для активации окна
                    System.Threading.Thread.Sleep(500);

                    // Отправляем нажатие клавиши Escape
                    PostMessage(_currentWindowHandle, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                    System.Threading.Thread.Sleep(10);
                    PostMessage(_currentWindowHandle, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);

                    Logger.LogInfo($"Escape key sent successfully to window handle: {_currentWindowHandle}");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error sending Escape key to target window", ex);
                }
            });

            escapeThread.IsBackground = true; // Поток не будет препятствовать завершению приложения
            escapeThread.Start();
        }

        /// <summary>
        /// Конвертирует координаты виртуального экрана в координаты окна
        /// </summary>
        private System.Drawing.Point ConvertVirtualScreenToWindowCoordinates(int virtualX, int virtualY, RECT windowRect)
        {
            // Простая конвертация: вычитаем позицию окна из виртуальных координат
            int windowX = virtualX - windowRect.Left;
            int windowY = virtualY - windowRect.Top;

            // Убеждаемся, что координаты не отрицательные
            windowX = Math.Max(0, windowX);
            windowY = Math.Max(0, windowY);

            Logger.LogDebug($"ConvertVirtualScreenToWindowCoordinates: virtualX={virtualX}, virtualY={virtualY}, windowLeft={windowRect.Left}, windowTop={windowRect.Top}, windowX={windowX}, windowY={windowY}");

            return new System.Drawing.Point(windowX, windowY);
        }

        /// <summary>
        /// Создает мета-данные для CaptureData, используя те же методы, что и CaptureTrackingService
        /// </summary>
        private string CreateMetadataForCapture(CaptureData capture)
        {
            try
            {
                Logger.LogDebug($"SimpleTradingOverlay: Creating metadata for capture ID={capture.ID}");
                
                var screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
                
                // Делаем скриншот окна
                using var bmp = screenshotService.CaptureWindow((IntPtr)capture.Handle);
                if (bmp == null)
                {
                    Logger.LogWarning($"SimpleTradingOverlay: Не удалось получить скриншот окна Handle={capture.Handle}");
                    return "{}";
                }

                Bitmap trackingBitmap;
            
                Rectangle cropRect = new Rectangle(capture.X, capture.Y, capture.Width, capture.Height);
                trackingBitmap = bmp.Clone(cropRect, bmp.PixelFormat);

                using (trackingBitmap)
                {
                    // Сохраняем trackingBitmap для отладки
                    string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES", capture.ID);
                    if (!Directory.Exists(debugDir)) Directory.CreateDirectory(debugDir);
                    string metaDebugPath = Path.Combine(debugDir, "MetaDebug.png");
                    string metaDebugTrackingPath = Path.Combine(debugDir, "MetaDebugTracking.png");
                    trackingBitmap.Save(metaDebugPath, System.Drawing.Imaging.ImageFormat.Png);
                    Logger.LogDebug($"SimpleTradingOverlay: Saved MetaDebug image to {metaDebugPath}");
                    
                    // Создаем детектор и строим мета-данные с отладочным изображением
                    var rectangleDetector = new RectangleBreakDetector();
                    var metadata = rectangleDetector.BuildMetadata(trackingBitmap, capture, metaDebugTrackingPath);
                    
                    Logger.LogDebug($"SimpleTradingOverlay: Created metadata for capture ID={capture.ID}, length={metadata.Length}");
                    
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SimpleTradingOverlay: Error creating metadata for capture ID={capture.ID}", ex);
                return "{}";
            }
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
    }
} 