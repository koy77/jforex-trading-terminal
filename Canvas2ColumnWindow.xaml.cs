using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Ink;
using System.Windows.Threading;
using System.IO;
using System.Xml;
using System.Windows.Markup;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Windows.Forms;
using System.Linq;
using System.Windows.Controls;
using System.Collections.Generic;
using ScreenCaptureApp.Helpers;
using ScreenCaptureApp.Observers;

namespace ScreenCaptureApp
{
    public partial class Canvas2ColumnWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint SRCCOPY = 0x00CC0020;

        public IntPtr targetWindowHandle;
        private DispatcherTimer backgroundUpdateTimer;
        private DispatcherTimer countdownTimer;
        private int secondsLeft;
        private const int BackgroundUpdateIntervalSeconds = 1; // match your backgroundUpdateTimer interval
        
        // MT4 window support
        private IntPtr mt4WindowHandle = IntPtr.Zero;
        private int mt4WindowWidth = 0;
        private int mt4WindowHeight = 0;
        
        // Trading mode settings
        private bool useJForexIntegration = false; // false = save to CaptureData with trading_canvas source, true = send to JForex
        
        // Event for stroke completion
        public event EventHandler<StrokeCompletedEventArgs> StrokeCompleted;

        // Event for triggering space hotkey after trendline drawing
        public event Action OnTrendlineDrawn;

        private string activeSymbol;
        private double selectedRisk = 1.0; // по умолчанию
        private int selectedDuration = 2; // по умолчанию

        // Убираем очередь для Trading Mode - упрощаем логику
        
        // Удаляем старую переменную tradingStrokes
        // private StrokeCollection tradingStrokes = new StrokeCollection();

        private BrokerState brokerState;
        private DurationState durationState;
        private JForexWindowsManagerService jForexService;
        private HttpServerService httpServerService;
        private Mt4SocketService mt4SocketService;
        
        // Canvas shift variables
        private double canvasOffsetX = 0;
        private double canvasOffsetY = 0;
        private const double SHIFT_STEP = 4.0; // 10 zpixels for W/S, 5 pixels for A/D
        
        // MACD area threshold - strokes above this Y coordinate (Y >= 740.0) are MACD model (non-trading)
        // Strokes below this Y coordinate (Y < 740.0) are OHLC model (trading)
        private const double MACD_AREA_THRESHOLD = 740.0;
        
        // Track if stroke is trading stroke (OHLC model) when stroke starts
        // true = OHLC model (trading, white color), false = MACD model (non-trading, yellow color)
        private bool isControlPressedAtStrokeStart = false;
        
        // Monitor index for this Canvas window (0 = primary, 1 = secondary, etc.)
        private int monitorIndex = 0;



        private ToolbarSettingsManager _toolbarSettingsManager;

        public Canvas2ColumnWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += Canvas2ColumnWindow_PreviewKeyDown;
            this.MouseDown += Canvas2ColumnWindow_MouseDown;
            this.Activated += Canvas2ColumnWindow_Activated;
            this.targetWindowHandle = IntPtr.Zero;
            brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            durationState = ServiceContainer.Instance.GetService<DurationState>();
            jForexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
            httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
            _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
            
            // Применяем настройки тулбара по handle окна
            if (targetWindowHandle != IntPtr.Zero)
            {
                var toolbarSettings = _toolbarSettingsManager.GetSettings(targetWindowHandle.ToInt64());
                if (toolbarSettings != null)
                {
                    selectedRisk = toolbarSettings.Risk;
                    selectedDuration = toolbarSettings.Duration;
                    brokerState.CurrentBroker = toolbarSettings.Broker;
                    Logger.LogInfo($"Applied toolbar settings for handle={targetWindowHandle.ToInt64()}: Risk={selectedRisk}, Duration={selectedDuration}, Broker={toolbarSettings.Broker}");
                }
            }
            // Подписка на события TradingToolbar
            TradingToolbar.RiskChanged += (risk) => {
                selectedRisk = risk;
                if (targetWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), risk: risk);
            };
            TradingToolbar.BrokerChanged += (broker) => {
                brokerState.CurrentBroker = broker;
                UpdateTradingToolbarUiByBroker(broker);
                if (targetWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), broker: broker);
            };
            TradingToolbar.DurationChanged += (duration) => {
                durationState.CurrentDuration = duration;
                selectedDuration = duration;
                if (targetWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), duration: duration);
            };
            // Инициализация UI по текущему брокеру
            TradingToolbar.HighlightSelectedBroker(brokerState.CurrentBroker);
            TradingToolbar.HighlightSelectedRiskButton(selectedRisk);
            TradingToolbar.HighlightSelectedDurationButton(selectedDuration);
            
            // Инициализация CanvasTradingToolBar
            InitializeCanvasTradingToolBar();
        }

        public Canvas2ColumnWindow(IntPtr targetWindow, string symbol = null, int monitorIndex = 0)
        {
            InitializeComponent();
            this.PreviewKeyDown += Canvas2ColumnWindow_PreviewKeyDown;
            this.targetWindowHandle = targetWindow;
            this.activeSymbol = symbol;
            this.monitorIndex = monitorIndex;
            
            // Инициализируем сервисы
            brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            durationState = ServiceContainer.Instance.GetService<DurationState>();
            jForexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
            httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
            mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
            
            // Применяем настройки тулбара по handle окна
            if (targetWindowHandle != IntPtr.Zero)
            {
                var toolbarSettings = _toolbarSettingsManager.GetSettings(targetWindowHandle.ToInt64());
                if (toolbarSettings != null)
                {
                    selectedRisk = toolbarSettings.Risk;
                    selectedDuration = toolbarSettings.Duration;
                    brokerState.CurrentBroker = toolbarSettings.Broker;
                    Logger.LogInfo($"Applied toolbar settings for handle={targetWindowHandle.ToInt64()}: Risk={selectedRisk}, Duration={selectedDuration}, Broker={toolbarSettings.Broker}");
                }
                else
                {
                    // Если настроек нет, создаем их с дефолтными значениями
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), selectedRisk, selectedDuration, brokerState.CurrentBroker);
                    Logger.LogInfo($"Created default toolbar settings for handle={targetWindowHandle.ToInt64()}: Risk={selectedRisk}, Duration={selectedDuration}, Broker={brokerState.CurrentBroker}");
                }
            }
            // Подписка на события TradingToolbar
            TradingToolbar.RiskChanged += (risk) => {
                selectedRisk = risk;
                if (targetWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), risk: risk);
            };
            TradingToolbar.BrokerChanged += (broker) => {
                brokerState.CurrentBroker = broker;
                UpdateTradingToolbarUiByBroker(broker);
                if (targetWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), broker: broker);
            };
            TradingToolbar.DurationChanged += (duration) => {
                durationState.CurrentDuration = duration;
                selectedDuration = duration;
                if (targetWindowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), duration: duration);
            };
            
            // Инициализация UI по текущему брокеру
            TradingToolbar.HighlightSelectedBroker(brokerState.CurrentBroker);
            TradingToolbar.HighlightSelectedRiskButton(selectedRisk);
            TradingToolbar.HighlightSelectedDurationButton(selectedDuration);
            
            // Инициализация CanvasTradingToolBar
            InitializeCanvasTradingToolBar();
        }

        private void Canvas2ColumnWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Logger.LogInfo("Pressed Escape: closing window");
                this.Close();
            }
            else if (e.Key == Key.C)
            {
                Logger.LogInfo("Pressed C: clear canvas and delete files");
                GeForceDrawingCanvas.Strokes.Clear();
                Mt4DrawingCanvas.Strokes.Clear();
                DeleteCanvasFiles();

                // --- Mark all related captures as skipped ---
                try
                {
                    var databaseService = Services.ServiceContainer.Instance.GetService<Services.DatabaseService>();
                    if (databaseService != null)
                    {
                        long thisHandle = targetWindowHandle.ToInt64();
                        databaseService.MarkTradingCanvasCapturesAsSkipped(thisHandle);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error marking captures as skipped after canvas clear", ex);
                }
            }
            // Space key is now handled globally in MainWindow
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger.LogInfo("Window closing: saving canvas");
            SaveCanvasProperly();
            GeForceDrawingCanvas.Strokes.Clear();
            Mt4DrawingCanvas.Strokes.Clear();
            GeForceBackgroundImage.Source = null;
            Mt4BackgroundImage.Source = null;
            if (backgroundUpdateTimer != null)
            {
                backgroundUpdateTimer.Stop();
                backgroundUpdateTimer = null;
            }
            if (countdownTimer != null)
            {
                countdownTimer.Stop();
                countdownTimer = null;
            }
            
            // Отписываемся от событий NewBar
            if (httpServerService != null)
            {
                httpServerService.NewBarReceived -= OnNewBarReceived;
                Logger.LogInfo("Unsubscribed from NewBar events from HttpServerService");
            }
            
            // Очищаем ресурсы CanvasTradingToolBar
            try
            {
                CanvasTradingToolBar.OnSecButtonClicked -= OnSecButtonClicked;
                CanvasTradingToolBar.Dispose();
                Logger.LogInfo("CanvasTradingToolBar resources cleaned up");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error cleaning up CanvasTradingToolBar: {ex.Message}", ex);
            }
            
            // Очищаем ресурсы CanvasTrackingList
            try
            {
                CanvasTrackingList.Dispose();
                Logger.LogInfo("CanvasTrackingList resources cleaned up");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error cleaning up CanvasTrackingList: {ex.Message}", ex);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo($"Window loaded for handler {targetWindowHandle.ToInt64()}");
            SetFullScreen();
            SetBackgroundImage(shouldLog: true);
            backgroundUpdateTimer = new DispatcherTimer();
            backgroundUpdateTimer.Interval = TimeSpan.FromSeconds(BackgroundUpdateIntervalSeconds);
            backgroundUpdateTimer.Tick += (s, args) => SetBackgroundImage(shouldLog: false);
            backgroundUpdateTimer.Start();
            secondsLeft = BackgroundUpdateIntervalSeconds;
            TimerText.Text = $"{secondsLeft}s";
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();
            
            // Инициализация InkCanvas с правильными настройками
            InitializeInkCanvas();
            
            LoadCanvas();
            
            // Initialize brush indicator
            UpdateBrushMode();
            
            // Subscribe to stroke completion events
            GeForceDrawingCanvas.StrokeCollected += GeForceDrawingCanvas_StrokeCollected;
            Mt4DrawingCanvas.StrokeCollected += Mt4DrawingCanvas_StrokeCollected;
            
            // Subscribe to stroke erasing events
            GeForceDrawingCanvas.StrokeErasing += GeForceDrawingCanvas_StrokeErasing;
            Mt4DrawingCanvas.StrokeErasing += Mt4DrawingCanvas_StrokeErasing;
            
            // Initialize broker state
            InitializeBrokerComboBox();
            UpdateTradingToolbarVisibility();
            
            // Обновляем отображение активного символа
            UpdateActiveSymbolDisplay();
            
            // Показываем CanvasTrackingList только на вторичном Canvas (monitorIndex > 0)
            if (monitorIndex > 0)
            {
                CanvasTrackingList.Visibility = Visibility.Visible;
                Logger.LogInfo($"CanvasTrackingList will be shown for secondary monitor (monitorIndex={monitorIndex})");
            }
            else
            {
                CanvasTrackingList.Visibility = Visibility.Collapsed;
                Logger.LogInfo($"CanvasTrackingList hidden for primary monitor (monitorIndex={monitorIndex})");
            }
            
            // Показываем CanvasTradingToolBar только на Primary Monitor (monitorIndex == 0)
            if (monitorIndex > 0)
            {
                // Скрываем CanvasTradingToolBar на Secondary Monitor
                CanvasTradingToolBar.Visibility = Visibility.Collapsed;
                Logger.LogInfo($"CanvasTradingToolBar hidden for secondary monitor (monitorIndex={monitorIndex})");
            }
            else
            {
                // На Primary Monitor видимость управляется логикой CanvasTradingToolBar
                Logger.LogInfo($"CanvasTradingToolBar will be controlled by its own logic for primary monitor (monitorIndex={monitorIndex})");
            }
            
            // Устанавливаем фокус на GeForce InkCanvas
            GeForceDrawingCanvas.Focus();
            
            // Обновляем позиции тостов после загрузки CanvasWindow
            var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
            toastService?.RefreshToastPositions();
            
            // Подписываемся на события NewBar от HttpServerService
            if (httpServerService != null)
            {
                httpServerService.NewBarReceived += OnNewBarReceived;
                Logger.LogInfo("Subscribed to NewBar events from HttpServerService");
            }
        }

        private void InitializeInkCanvas()
        {
            // Настройка GeForceDrawingCanvas для обычного рисования
            GeForceDrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
            
            // Устанавливаем желтый цвет кисти по умолчанию для GeForce
            GeForceDrawingCanvas.DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes
            {
                Color = Colors.Yellow, // По умолчанию желтый цвет
                Width = 2,
                Height = 2,
                FitToCurve = true,
                IgnorePressure = false,
                IsHighlighter = false,
                StylusTip = StylusTip.Ellipse
            };
            
            // Убеждаемся, что GeForceDrawingCanvas может принимать ввод
            GeForceDrawingCanvas.IsHitTestVisible = true;
            GeForceDrawingCanvas.IsEnabled = true;
            
            // Включаем поддержку различных типов ввода
            GeForceDrawingCanvas.IsManipulationEnabled = true;
            
            // Добавляем обработчики событий для GeForce канваса
            GeForceDrawingCanvas.MouseDown += GeForceDrawingCanvas_MouseDown;
            GeForceDrawingCanvas.PreviewMouseDown += GeForceDrawingCanvas_PreviewMouseDown;
            GeForceDrawingCanvas.PreviewStylusDown += GeForceDrawingCanvas_PreviewStylusDown;
            GeForceDrawingCanvas.StylusDown += GeForceDrawingCanvas_StylusDown;
            GeForceDrawingCanvas.PreviewTouchDown += GeForceDrawingCanvas_PreviewTouchDown;
            GeForceDrawingCanvas.TouchDown += GeForceDrawingCanvas_TouchDown;
            
            // Настройка Mt4DrawingCanvas для рисования
            Mt4DrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
            
            // Устанавливаем желтый цвет кисти по умолчанию для MT4
            Mt4DrawingCanvas.DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes
            {
                Color = Colors.Yellow, // По умолчанию желтый цвет
                Width = 2,
                Height = 2,
                FitToCurve = true,
                IgnorePressure = false,
                IsHighlighter = false,
                StylusTip = StylusTip.Ellipse
            };
            
            // Убеждаемся, что Mt4DrawingCanvas может принимать ввод
            Mt4DrawingCanvas.IsHitTestVisible = true;
            Mt4DrawingCanvas.IsEnabled = true;
            
            // Включаем поддержку различных типов ввода
            Mt4DrawingCanvas.IsManipulationEnabled = true;
            
            Logger.LogInfo($"InkCanvas initialized successfully: GeForceDrawingCanvas and Mt4DrawingCanvas with Yellow brush");
        }

        // GeForce канвас обработчики событий
        private void GeForceDrawingCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var position = e.GetPosition(GeForceDrawingCanvas);
            bool isOHLCModel = position.Y < MACD_AREA_THRESHOLD;
            isControlPressedAtStrokeStart = isOHLCModel;
            
            if (isOHLCModel)
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
                Logger.LogInfo($"GeForce MouseDown: OHLC model detected (Y={position.Y} < {MACD_AREA_THRESHOLD}) - setting brush to White for trading stroke");
            }
            else
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
                Logger.LogInfo($"GeForce MouseDown: MACD model detected (Y={position.Y} >= {MACD_AREA_THRESHOLD}) - setting brush to Yellow for simple stroke");
            }
        }
        
        private void GeForceDrawingCanvas_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var position = e.GetPosition(GeForceDrawingCanvas);
            bool isOHLCModel = position.Y < MACD_AREA_THRESHOLD;
            isControlPressedAtStrokeStart = isOHLCModel;
            
            if (isOHLCModel)
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
                Logger.LogInfo($"GeForce PreviewMouseDown: OHLC model detected - setting brush to White");
            }
            else
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
                Logger.LogInfo($"GeForce PreviewMouseDown: MACD model detected - setting brush to Yellow");
            }
        }
        
        private void GeForceDrawingCanvas_PreviewStylusDown(object sender, System.Windows.Input.StylusDownEventArgs e)
        {
            var position = e.GetPosition(GeForceDrawingCanvas);
            bool isOHLCModel = position.Y < MACD_AREA_THRESHOLD;
            isControlPressedAtStrokeStart = isOHLCModel;
            
            if (isOHLCModel)
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
            }
            else
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            }
        }
        
        private void GeForceDrawingCanvas_StylusDown(object sender, System.Windows.Input.StylusDownEventArgs e)
        {
            var position = e.GetPosition(GeForceDrawingCanvas);
            bool isOHLCModel = position.Y < MACD_AREA_THRESHOLD;
            isControlPressedAtStrokeStart = isOHLCModel;
            
            if (isOHLCModel)
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
            }
            else
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            }
        }
        
        private void GeForceDrawingCanvas_PreviewTouchDown(object sender, System.Windows.Input.TouchEventArgs e)
        {
            var position = e.GetTouchPoint(GeForceDrawingCanvas).Position;
            bool isOHLCModel = position.Y < MACD_AREA_THRESHOLD;
            isControlPressedAtStrokeStart = isOHLCModel;
            
            if (isOHLCModel)
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
            }
            else
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            }
        }
        
        private void GeForceDrawingCanvas_TouchDown(object sender, System.Windows.Input.TouchEventArgs e)
        {
            var position = e.GetTouchPoint(GeForceDrawingCanvas).Position;
            bool isOHLCModel = position.Y < MACD_AREA_THRESHOLD;
            isControlPressedAtStrokeStart = isOHLCModel;
            
            if (isOHLCModel)
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
            }
            else
            {
                GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            }
        }
        

        private void SetFullScreen()
        {
            // Используем указанный монитор или fallback к primary screen
            var screens = System.Windows.Forms.Screen.AllScreens;
            var screen = monitorIndex >= 0 && monitorIndex < screens.Length 
                ? screens[monitorIndex] 
                : System.Windows.Forms.Screen.PrimaryScreen;
            
            this.Left = screen.Bounds.Left;
            this.Top = screen.Bounds.Top;
            this.Width = screen.Bounds.Width;
            this.Height = screen.Bounds.Height;
            
            Logger.LogInfo($"CanvasWindow positioned on monitor {monitorIndex}: X={screen.Bounds.Left}, Y={screen.Bounds.Top}, Width={screen.Bounds.Width}, Height={screen.Bounds.Height}");
        }

        private void SetBackgroundImage()
        {
            SetBackgroundImage(shouldLog: false);
        }
        
        private void SetBackgroundImage(bool shouldLog)
        {
            try
            {
                // Получаем размеры экрана
                var screens = System.Windows.Forms.Screen.AllScreens;
                var screen = monitorIndex >= 0 && monitorIndex < screens.Length 
                    ? screens[monitorIndex] 
                    : System.Windows.Forms.Screen.PrimaryScreen;
                double screenWidth = screen.Bounds.Width;
                double screenHeight = screen.Bounds.Height;
                
                // Проверяем, является ли текущее окно MT4, JForex или другое
                bool isMt4Window = IsMt4Window(targetWindowHandle, shouldLog);
                bool isJForexWindow = IsJForexWindow(targetWindowHandle, shouldLog);
                bool isGForexWindow = !isMt4Window && targetWindowHandle != IntPtr.Zero;
                
                if (shouldLog)
                {
                    Logger.LogInfo($"SetBackgroundImage: targetWindowHandle={targetWindowHandle.ToInt64()}, mt4WindowHandle={mt4WindowHandle.ToInt64()}, isMt4Window={isMt4Window}, isJForexWindow={isJForexWindow}, isGForexWindow={isGForexWindow}");
                }
                
                // Если это MT4 окно, сохраняем его handle и размеры (только если еще не установлен)
                if (isMt4Window && targetWindowHandle != IntPtr.Zero && mt4WindowHandle == IntPtr.Zero)
                {
                    mt4WindowHandle = targetWindowHandle;
                    RECT mt4Rect;
                    if (GetWindowRect(mt4WindowHandle, out mt4Rect))
                    {
                        mt4WindowWidth = mt4Rect.Right - mt4Rect.Left;
                        mt4WindowHeight = mt4Rect.Bottom - mt4Rect.Top;
                        Logger.LogInfo($"MT4 window detected and saved: Handle={mt4WindowHandle.ToInt64()}, Size={mt4WindowWidth}x{mt4WindowHeight}");
                    }
                }
                // Если MT4 handle уже установлен, но текущее окно снова MT4, обновляем размеры (но не handle)
                else if (isMt4Window && targetWindowHandle != IntPtr.Zero && mt4WindowHandle != IntPtr.Zero && targetWindowHandle == mt4WindowHandle)
                {
                    RECT mt4Rect;
                    if (GetWindowRect(mt4WindowHandle, out mt4Rect))
                    {
                        mt4WindowWidth = mt4Rect.Right - mt4Rect.Left;
                        mt4WindowHeight = mt4Rect.Bottom - mt4Rect.Top;
                        Logger.LogInfo($"MT4 window size updated: Handle={mt4WindowHandle.ToInt64()}, Size={mt4WindowWidth}x{mt4WindowHeight}");
                    }
                }
                
                // Если есть MT4 handle, всегда показываем 2-колоночный layout
                if (mt4WindowHandle != IntPtr.Zero)
                {
                    Logger.LogInfo($"2-column layout mode: MT4={mt4WindowHandle.ToInt64()}, Target={targetWindowHandle.ToInt64()}, mt4WindowWidth={mt4WindowWidth}, mt4WindowHeight={mt4WindowHeight}");
                    
                    // Устанавливаем ширину левой колонки для MT4
                    Mt4Column.Width = new System.Windows.GridLength(mt4WindowWidth);
                    
                    // Захватываем MT4 окно для левой колонки
                    Bitmap mt4Screenshot = CaptureWindow(mt4WindowHandle);
                    if (mt4Screenshot != null)
                    {
                        var mt4BitmapSource = ConvertToBitmapSource(mt4Screenshot);
                        Mt4BackgroundImage.Source = mt4BitmapSource;
                        Mt4BackgroundImage.Visibility = Visibility.Visible;
                        Mt4BackgroundImage.Width = mt4WindowWidth;
                        Mt4BackgroundImage.Height = mt4WindowHeight;
                        Mt4BackgroundImage.Stretch = System.Windows.Media.Stretch.None;
                    }
                    
                    // Показываем MT4 канвас в левой колонке
                    Mt4DrawingCanvas.Visibility = Visibility.Visible;
                    Mt4DrawingCanvas.Width = mt4WindowWidth;
                    Mt4DrawingCanvas.Height = screenHeight;
                    Mt4DrawingCanvas.ClipToBounds = true;
                    Mt4DrawingCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    Mt4DrawingCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    
                    // Инициализируем MT4 канвас с желтой кистью
                    var mt4DrawingAttributes = new System.Windows.Ink.DrawingAttributes();
                    mt4DrawingAttributes.Color = Colors.Yellow;
                    mt4DrawingAttributes.Width = 2;
                    mt4DrawingAttributes.Height = 2;
                    Mt4DrawingCanvas.DefaultDrawingAttributes = mt4DrawingAttributes;
                    
                    // Вычисляем ширину правой колонки
                    double geForceWidth = screenWidth - mt4WindowWidth;
                    
                    // Если есть целевое окно (GeForce/JForex), показываем его в правой колонке
                    if (targetWindowHandle != IntPtr.Zero && !isMt4Window)
                    {
                        // Захватываем GeForce/JForex окно для правой колонки
                        Bitmap gforexScreenshot = CaptureWindow(targetWindowHandle);
                        if (gforexScreenshot != null)
                        {
                            var gforexBitmapSource = ConvertToBitmapSource(gforexScreenshot);
                            GeForceBackgroundImage.Source = gforexBitmapSource;
                            GeForceBackgroundImage.Visibility = Visibility.Visible;
                            GeForceBackgroundImage.Width = geForceWidth;
                            GeForceBackgroundImage.Height = screenHeight;
                            GeForceBackgroundImage.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                            GeForceBackgroundImage.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                            GeForceBackgroundImage.Stretch = System.Windows.Media.Stretch.UniformToFill;
                        }
                        
                        // Показываем GeForce канвас в правой колонке
                        GeForceDrawingCanvas.Visibility = Visibility.Visible;
                        GeForceDrawingCanvas.Width = geForceWidth;
                        GeForceDrawingCanvas.Height = screenHeight;
                        GeForceDrawingCanvas.ClipToBounds = true;
                        GeForceDrawingCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                        GeForceDrawingCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                        GeForceDrawingCanvas.Background = System.Windows.Media.Brushes.Transparent;
                    }
                    else
                    {
                        // Если нет целевого окна, показываем пустую правую колонку
                        GeForceBackgroundImage.Visibility = Visibility.Collapsed;
                        GeForceDrawingCanvas.Visibility = Visibility.Visible;
                        GeForceDrawingCanvas.Width = geForceWidth;
                        GeForceDrawingCanvas.Height = screenHeight;
                        GeForceDrawingCanvas.ClipToBounds = true;
                        GeForceDrawingCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                        GeForceDrawingCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                        GeForceDrawingCanvas.Background = System.Windows.Media.Brushes.Transparent;
                    }
                    
                    Logger.LogInfo($"2-column layout: MT4 (left) = {mt4WindowWidth}x{screenHeight}, GeForce (right) = {geForceWidth}x{screenHeight}");
                }
                // Обычный режим - одно окно (только GeForce, без MT4)
                else
                {
                    Logger.LogInfo($"Single window mode: targetWindowHandle={targetWindowHandle.ToInt64()}, mt4WindowHandle={mt4WindowHandle.ToInt64()}");
                    
                    // Скрываем MT4 колонку
                    Mt4Column.Width = new System.Windows.GridLength(0);
                    Mt4BackgroundImage.Visibility = Visibility.Collapsed;
                    Mt4DrawingCanvas.Visibility = Visibility.Collapsed;
                    
                    Bitmap screenshot = null;
                    
                    // If we have a target window, capture that specific window
                    if (targetWindowHandle != IntPtr.Zero)
                    {
                        screenshot = CaptureWindow(targetWindowHandle);
                    }
                    
                    // If no target window or capture failed, fall back to full screen
                    if (screenshot == null)
                    {
                        screenshot = CaptureScreen();
                    }
                    
                    if (screenshot != null)
                    {
                        // Конвертируем в BitmapSource для WPF
                        var bitmapSource = ConvertToBitmapSource(screenshot);
                        
                        // Устанавливаем как Image
                        GeForceBackgroundImage.Source = bitmapSource;
                        GeForceBackgroundImage.Width = screenWidth;
                        GeForceBackgroundImage.Height = screenHeight;
                        GeForceBackgroundImage.Visibility = Visibility.Visible;
                    }
                    
                    // Настраиваем GeForceDrawingCanvas на весь экран
                    GeForceDrawingCanvas.ClipToBounds = false;
                    GeForceDrawingCanvas.Clip = null;
                    GeForceDrawingCanvas.Margin = new Thickness(0);
                    GeForceDrawingCanvas.Width = screenWidth;
                    GeForceDrawingCanvas.Height = screenHeight;
                    GeForceDrawingCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    GeForceDrawingCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                    GeForceDrawingCanvas.Visibility = Visibility.Visible;
                }
                
                secondsLeft = BackgroundUpdateIntervalSeconds;
                TimerText.Text = $"Next update in {secondsLeft}s";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании скриншота: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Bitmap CaptureScreen()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var bitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
            
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(screen.Bounds.Left, screen.Bounds.Top, 0, 0, screen.Bounds.Size);
            }
            
            return bitmap;
        }

        private Bitmap CaptureWindow(IntPtr windowHandle)
        {
            try
            {
                RECT windowRect;
                if (GetWindowRect(windowHandle, out windowRect))
                {
                    int width = windowRect.Right - windowRect.Left;
                    int height = windowRect.Bottom - windowRect.Top;

                    if (width <= 0 || height <= 0)
                        return null;

                    IntPtr hwndDC = GetWindowDC(windowHandle);
                    IntPtr memDC = CreateCompatibleDC(hwndDC);
                    IntPtr hBitmap = CreateCompatibleBitmap(hwndDC, width, height);
                    IntPtr hOld = SelectObject(memDC, hBitmap);

                    bool success = PrintWindow(windowHandle, memDC, 2);

                    if (!success)
                    {
                        // fallback: BitBlt
                        success = BitBlt(memDC, 0, 0, width, height, hwndDC, 0, 0, SRCCOPY);
                        if (!success)
                        {
                            // Clean up
                            SelectObject(memDC, hOld);
                            DeleteObject(hBitmap);
                            DeleteDC(memDC);
                            ReleaseDC(windowHandle, hwndDC);
                            return null;
                        }
                    }

                    Bitmap bmp = System.Drawing.Bitmap.FromHbitmap(hBitmap);

                    // Clean up
                    SelectObject(memDC, hOld);
                    DeleteObject(hBitmap);
                    DeleteDC(memDC);
                    ReleaseDC(windowHandle, hwndDC);

                    return bmp;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при захвате окна: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            var handle = bitmap.GetHbitmap();
            
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        /// <summary>
        /// Получает путь к файлу Canvas.
        /// Для обычного Canvas использует handle окна и монитор: {Handle}_monitor{MonitorIndex}
        /// Для торгового Canvas использует ID capture: trading_{CaptureID}
        /// Для MT4 Canvas добавляет суффикс _mt4
        /// Для GeForce Canvas добавляет суффикс _geforce
        /// </summary>
        private string GetCanvasFileBase(string captureId = null, bool isMt4 = false)
        {
            string canvasesDir = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Canvases");
            Directory.CreateDirectory(canvasesDir);
            
            string fileName;
            if (!string.IsNullOrEmpty(captureId))
            {
                // Торговый Canvas: используем ID capture
                fileName = $"trading_{captureId}";
            }
            else
            {
                // Обычный Canvas: используем handle окна и индекс монитора для независимого сохранения
                string handlerStr = targetWindowHandle.ToInt64().ToString();
                string suffix = isMt4 ? "_mt4" : "_geforce";
                fileName = $"{handlerStr}_monitor{monitorIndex}{suffix}";
            }
            
            return Path.Combine(canvasesDir, fileName);
        }



        private void SaveCanvas()
        {
            try
            {
                // Сохраняем GeForce канвас
                if (GeForceDrawingCanvas.Visibility == Visibility.Visible)
                {
                    var geForceFileBase = GetCanvasFileBase(isMt4: false);
                    var geForceXamlFile = geForceFileBase + ".xaml";
                    var geForcePngFile = geForceFileBase + ".png";

                    // Сохраняем все штрихи GeForce канваса
                    var geForceXaml = XamlWriter.Save(GeForceDrawingCanvas.Strokes);
                    File.WriteAllText(geForceXamlFile, geForceXaml);

                    // Save as PNG
                    var geForceRtb = new RenderTargetBitmap((int)GeForceDrawingCanvas.ActualWidth, (int)GeForceDrawingCanvas.ActualHeight, 96d, 96d, PixelFormats.Pbgra32);
                    GeForceDrawingCanvas.Measure(new System.Windows.Size(GeForceDrawingCanvas.ActualWidth, GeForceDrawingCanvas.ActualHeight));
                    GeForceDrawingCanvas.Arrange(new Rect(0, 0, GeForceDrawingCanvas.ActualWidth, GeForceDrawingCanvas.ActualHeight));
                    geForceRtb.Render(GeForceDrawingCanvas);

                    var geForceEncoder = new PngBitmapEncoder();
                    geForceEncoder.Frames.Add(BitmapFrame.Create(geForceRtb));
                    using (var fs = new FileStream(geForcePngFile, FileMode.Create))
                    {
                        geForceEncoder.Save(fs);
                    }

                    Logger.LogInfo($"GeForce Canvas saved: {geForceXamlFile}, {geForcePngFile}");
                }
                
                // Сохраняем MT4 канвас
                if (Mt4DrawingCanvas.Visibility == Visibility.Visible)
                {
                    var mt4FileBase = GetCanvasFileBase(isMt4: true);
                    var mt4XamlFile = mt4FileBase + ".xaml";
                    var mt4PngFile = mt4FileBase + ".png";

                    // Сохраняем все штрихи MT4 канваса
                    var mt4Xaml = XamlWriter.Save(Mt4DrawingCanvas.Strokes);
                    File.WriteAllText(mt4XamlFile, mt4Xaml);

                    // Save as PNG
                    var mt4Rtb = new RenderTargetBitmap((int)Mt4DrawingCanvas.ActualWidth, (int)Mt4DrawingCanvas.ActualHeight, 96d, 96d, PixelFormats.Pbgra32);
                    Mt4DrawingCanvas.Measure(new System.Windows.Size(Mt4DrawingCanvas.ActualWidth, Mt4DrawingCanvas.ActualHeight));
                    Mt4DrawingCanvas.Arrange(new Rect(0, 0, Mt4DrawingCanvas.ActualWidth, Mt4DrawingCanvas.ActualHeight));
                    mt4Rtb.Render(Mt4DrawingCanvas);

                    var mt4Encoder = new PngBitmapEncoder();
                    mt4Encoder.Frames.Add(BitmapFrame.Create(mt4Rtb));
                    using (var fs = new FileStream(mt4PngFile, FileMode.Create))
                    {
                        mt4Encoder.Save(fs);
                    }

                    Logger.LogInfo($"MT4 Canvas saved: {mt4XamlFile}, {mt4PngFile}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving canvas: {ex.Message}", ex);
            }
        }

        private void SaveCanvasProperly()
        {
            try
            {
                // Сохраняем canvas
                SaveCanvas();
                
                Logger.LogInfo("Canvas saved properly");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving canvas properly: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Сохраняет только последний торговый (белый) штрих в торговый Canvas файл.
        /// Используется для создания Canvas файла, который будет мержиться в Tracking Service.
        /// Торговый Canvas сохраняется с именем trading_{captureId}.png
        /// </summary>
        private void SaveTradingCanvas(Stroke lastTradingStroke, string captureId)
        {
            try
            {
                if (string.IsNullOrEmpty(captureId))
                {
                    Logger.LogError("SaveTradingCanvas: captureId is required for trading canvas");
                    return;
                }

                var fileBase = GetCanvasFileBase(captureId);
                var xamlFile = fileBase + ".xaml";
                var pngFile = fileBase + ".png";

                // Создаем коллекцию с только последним торговым штрихом
                var tradingStrokes = new StrokeCollection();
                
                if (lastTradingStroke != null)
                {
                    // Используем переданный штрих (последний торговый)
                    tradingStrokes.Add(lastTradingStroke);
                    Logger.LogInfo($"SaveTradingCanvas: Using provided last trading stroke");
                }
                else
                {
                    Logger.LogError("SaveTradingCanvas: lastTradingStroke is required");
                    return;
                }

                if (tradingStrokes.Count == 0)
                {
                    Logger.LogWarning("SaveTradingCanvas: No trading strokes found to save");
                    return;
                }

                Logger.LogInfo($"SaveTradingCanvas: Saving {tradingStrokes.Count} trading stroke(s) (out of {GeForceDrawingCanvas.Strokes.Count} total strokes)");

                // Сохраняем только торговый штрих в XAML
                var xaml = XamlWriter.Save(tradingStrokes);
                File.WriteAllText(xamlFile, xaml);

                // Создаем временный InkCanvas только с последним торговым штрихом для рендеринга
                var tempInkCanvas = new System.Windows.Controls.InkCanvas
                {
                    Width = GeForceDrawingCanvas.ActualWidth,
                    Height = GeForceDrawingCanvas.ActualHeight,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Strokes = tradingStrokes
                };

                // Save as PNG
                var rtb = new RenderTargetBitmap((int)GeForceDrawingCanvas.ActualWidth, (int)GeForceDrawingCanvas.ActualHeight, 96d, 96d, PixelFormats.Pbgra32);
                tempInkCanvas.Measure(new System.Windows.Size(GeForceDrawingCanvas.ActualWidth, GeForceDrawingCanvas.ActualHeight));
                tempInkCanvas.Arrange(new Rect(0, 0, GeForceDrawingCanvas.ActualWidth, GeForceDrawingCanvas.ActualHeight));
                rtb.Render(tempInkCanvas);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = new FileStream(pngFile, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                Logger.LogInfo($"Trading Canvas saved: {xamlFile}, {pngFile} (only last trading stroke, captureId={captureId})");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving trading canvas: {ex.Message}", ex);
            }
        }

        private void LoadCanvas()
        {
            try
            {
                // Загружаем GeForce канвас
                string geForceBasePath = GetCanvasFileBase(isMt4: false);
                StrokeCollection geForceStrokes = new StrokeCollection();
                if (File.Exists(geForceBasePath + ".xaml"))
                {
                    string xaml = File.ReadAllText(geForceBasePath + ".xaml");
                    geForceStrokes = (StrokeCollection)XamlReader.Parse(xaml);
                }
                GeForceDrawingCanvas.Strokes = geForceStrokes;
                Logger.LogInfo($"GeForce Canvas loaded: {geForceBasePath}.xaml");
                
                // Загружаем MT4 канвас
                string mt4BasePath = GetCanvasFileBase(isMt4: true);
                StrokeCollection mt4Strokes = new StrokeCollection();
                if (File.Exists(mt4BasePath + ".xaml"))
                {
                    string xaml = File.ReadAllText(mt4BasePath + ".xaml");
                    mt4Strokes = (StrokeCollection)XamlReader.Parse(xaml);
                }
                Mt4DrawingCanvas.Strokes = mt4Strokes;
                Logger.LogInfo($"MT4 Canvas loaded: {mt4BasePath}.xaml");
            }
            catch (Exception ex) { Logger.LogError("LoadCanvas error", ex); }
        }

        private void DeleteCanvasFiles()
        {
            try
            {
                // Удаляем GeForce канвас
                string geForceBasePath = GetCanvasFileBase(isMt4: false);
                if (File.Exists(geForceBasePath + ".png")) { File.Delete(geForceBasePath + ".png"); Logger.LogInfo($"Deleted: {geForceBasePath}.png"); }
                if (File.Exists(geForceBasePath + ".xaml")) { File.Delete(geForceBasePath + ".xaml"); Logger.LogInfo($"Deleted: {geForceBasePath}.xaml"); }
                
                // Удаляем MT4 канвас
                string mt4BasePath = GetCanvasFileBase(isMt4: true);
                if (File.Exists(mt4BasePath + ".png")) { File.Delete(mt4BasePath + ".png"); Logger.LogInfo($"Deleted: {mt4BasePath}.png"); }
                if (File.Exists(mt4BasePath + ".xaml")) { File.Delete(mt4BasePath + ".xaml"); Logger.LogInfo($"Deleted: {mt4BasePath}.xaml"); }
            }
            catch (Exception ex) { Logger.LogError("DeleteCanvasFiles error", ex); }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            secondsLeft--;
            if (secondsLeft <= 0)
            {
                secondsLeft = BackgroundUpdateIntervalSeconds;
            }
            TimerText.Text = $"Next update in {secondsLeft}s";
        }
        
        private void UpdateBrushMode()
        {
            // Always use GeForceDrawingCanvas in simple mode
            GeForceDrawingCanvas.Visibility = Visibility.Visible;
            
            // Устанавливаем желтый цвет по умолчанию (цвет будет выбран при начале рисования по модели)
            GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            Logger.LogInfo($"UpdateBrushMode: Brush color will be determined by model (OHLC=White, MACD=Yellow) when drawing starts");
            
            // Border always yellow in simple mode
            // CanvasBorder removed in 2-column layout
            GeForceDrawingCanvas.DefaultDrawingAttributes.Width = 2;
            GeForceDrawingCanvas.DefaultDrawingAttributes.Height = 2;
            GeForceDrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
        
            // Устанавливаем фокус на GeForceDrawingCanvas
            GeForceDrawingCanvas.IsHitTestVisible = true;
            GeForceDrawingCanvas.IsEnabled = true;
            GeForceDrawingCanvas.Focus();
            
            Logger.LogInfo($"Brush mode updated: Simple mode - color will be set based on model when drawing starts");
        }

        private void UpdateTradingMode()
        {
            // Trading toolbar is always hidden in simple mode
            TradingToolbar.ShowDurationPanel(false);
        }

        private void UpdateTradingToolbarVisibility()
        {
            TradingToolbar.Visibility = Visibility.Collapsed;
        }


        public void UpdateSimpleBrushColor(bool isYellow)
        {
            // Цвет кисти теперь определяется по модели при начале рисования
            // Устанавливаем желтый по умолчанию (цвет будет выбран при начале рисования по модели)
            GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            Logger.LogInfo($"UpdateSimpleBrushColor: Brush color will be determined by model (OHLC=White, MACD=Yellow) when drawing starts");
            
            // Border removed in 2-column layout
            Logger.LogInfo($"Simple brush color updated - color will be set based on model when drawing starts");
        }

        public void UpdateTargetWindow(IntPtr newTargetWindow, string symbol = null)
        {
            Logger.LogInfo($"Updating target window from {targetWindowHandle.ToInt64()} to {newTargetWindow.ToInt64()}, current mt4WindowHandle={mt4WindowHandle.ToInt64()}");
            SaveCanvasProperly();
            
            // Проверяем, является ли новое окно MT4
            bool isMt4Window = IsMt4Window(newTargetWindow);
            
            // Если это MT4 окно, сохраняем его handle и размеры
            if (isMt4Window && newTargetWindow != IntPtr.Zero)
            {
                mt4WindowHandle = newTargetWindow;
                RECT mt4Rect;
                if (GetWindowRect(mt4WindowHandle, out mt4Rect))
                {
                    mt4WindowWidth = mt4Rect.Right - mt4Rect.Left;
                    mt4WindowHeight = mt4Rect.Bottom - mt4Rect.Top;
                    Logger.LogInfo($"MT4 window detected and saved: Handle={mt4WindowHandle.ToInt64()}, Size={mt4WindowWidth}x{mt4WindowHeight}");
                }
            }
            // Если новое окно не является MT4, но mt4WindowHandle уже установлен, сохраняем его
            // (mt4WindowHandle не должен сбрасываться при переключении на GeForce окно)
            else if (!isMt4Window && mt4WindowHandle != IntPtr.Zero)
            {
                Logger.LogInfo($"New window is not MT4, but mt4WindowHandle is preserved: {mt4WindowHandle.ToInt64()}");
            }
            
            targetWindowHandle = newTargetWindow;
            if (!string.IsNullOrEmpty(symbol))
            {
                activeSymbol = symbol;
            }
            UpdateActiveSymbolDisplay();
            GeForceDrawingCanvas.Strokes.Clear();
            Mt4DrawingCanvas.Strokes.Clear();
            LoadCanvas();
            SetBackgroundImage(shouldLog: true);
            GeForceDrawingCanvas.Focus();
            Logger.LogInfo($"Target window updated successfully");
        }
        
        /// <summary>
        /// Устанавливает MT4 handle для специального режима отображения
        /// </summary>
        public void SetMt4Handle(IntPtr mt4Handle)
        {
            if (mt4Handle == IntPtr.Zero)
            {
                mt4WindowHandle = IntPtr.Zero;
                Logger.LogInfo("MT4 handle cleared");
                return;
            }
            
            mt4WindowHandle = mt4Handle;
            RECT mt4Rect;
            if (GetWindowRect(mt4WindowHandle, out mt4Rect))
            {
                mt4WindowWidth = mt4Rect.Right - mt4Rect.Left;
                mt4WindowHeight = mt4Rect.Bottom - mt4Rect.Top;
                Logger.LogInfo($"MT4 handle set: Handle={mt4WindowHandle.ToInt64()}, Size={mt4WindowWidth}x{mt4WindowHeight}");
            }
            
            // Обновляем изображение фона для применения специального режима
            SetBackgroundImage(shouldLog: true);
        }
        
        /// <summary>
        /// Получает текущий MT4 handle
        /// </summary>
        public IntPtr GetMt4Handle()
        {
            return mt4WindowHandle;
        }
        
        /// <summary>
        /// Определяет, является ли окно MT4 окном (проверяет класс окна и заголовок)
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
        
        private bool IsMt4Window(IntPtr windowHandle, bool shouldLog = false)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return false;
                
                // Проверяем класс окна - MT4 обычно использует класс "MetaQuotes::MetaTrader::4.00"
                System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                int result = GetClassName(windowHandle, className, className.Capacity);
                string classNameStr = result > 0 ? className.ToString() : string.Empty;
                
                // Проверяем, является ли это MT4 окном по классу
                bool isMt4ByClass = !string.IsNullOrEmpty(classNameStr) && 
                                    (classNameStr.Contains("MetaTrader") || classNameStr.Contains("MetaQuotes"));
                
                string windowTitle = MainHelper.GetWindowTitle(windowHandle);
                
                // Если это JForex окно, точно не MT4
                bool isJForex = !string.IsNullOrEmpty(windowTitle) && 
                               (windowTitle.Contains("JForex") || windowTitle.Contains("DEMO"));
                
                if (isJForex)
                {
                    if (shouldLog)
                    {
                        Logger.LogInfo($"IsMt4Window check: Title='{windowTitle}', Class='{classNameStr}', isJForex=True, result=False");
                    }
                    return false;
                }
                
                // Если класс окна указывает на MT4, это MT4 окно
                if (isMt4ByClass)
                {
                    if (shouldLog)
                    {
                        Logger.LogInfo($"IsMt4Window check: Title='{windowTitle}', Class='{classNameStr}', isMt4ByClass=True, result=True");
                    }
                    return true;
                }
                
                // Если класс не MT4, проверяем заголовок - если нет символа, возможно это MT4
                string symbol = MainHelper.ExtractSymbolFromWindowTitle(windowHandle);
                bool hasSymbol = !string.IsNullOrEmpty(symbol);
                bool isMt4 = !hasSymbol && !string.IsNullOrEmpty(windowTitle);
                
                if (shouldLog)
                {
                    Logger.LogInfo($"IsMt4Window check: Title='{windowTitle}', Class='{classNameStr}', hasSymbol={hasSymbol}, symbol={symbol ?? "null"}, result={isMt4}");
                }
                return isMt4;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking if window is MT4: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Определяет, является ли окно JForex окном (проверяет заголовок на наличие символа, например "XAU/USD")
        /// </summary>
        private bool IsJForexWindow(IntPtr windowHandle, bool shouldLog = false)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return false;
                
                string windowTitle = MainHelper.GetWindowTitle(windowHandle);
                if (string.IsNullOrEmpty(windowTitle))
                    return false;
                
                // Проверяем, содержит ли заголовок символ (например, "XAU/USD")
                // Если в заголовке есть символ, считаем это JForex окном
                string symbol = MainHelper.ExtractSymbolFromWindowTitle(windowHandle);
                bool hasSymbol = !string.IsNullOrEmpty(symbol);
                
                // Если есть символ в заголовке, считаем это JForex окном
                bool isJForex = hasSymbol;
                
                if (shouldLog)
                {
                    Logger.LogInfo($"IsJForexWindow check: Title='{windowTitle}', hasSymbol={hasSymbol}, symbol={symbol ?? "null"}, result={isJForex}");
                }
                return isJForex;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking if window is JForex: {ex.Message}", ex);
                return false;
            }
        }

        // GeForce канвас обработчики событий для штрихов
        private void GeForceDrawingCanvas_StrokeCollected(object sender, System.Windows.Controls.InkCanvasStrokeCollectedEventArgs e)
        {
            // Определяем модель по начальной точке штриха для проверки
            var firstPoint = e.Stroke.StylusPoints[0];
            bool isOHLCModel = firstPoint.Y < MACD_AREA_THRESHOLD;
            bool isTradingStroke = isOHLCModel;
            
            Logger.LogInfo($"GeForce stroke collected - Model: {(isOHLCModel ? "OHLC (trading)" : "MACD (non-trading)")}, Y={firstPoint.Y}, isTrading={isTradingStroke}");
            
            if (isTradingStroke)
            {
                Logger.LogInfo("Trading stroke detected (OHLC model) - processing immediately");
                ProcessTradingStrokeDirectly(e.Stroke);
                
                // Keep the stroke on GeForceDrawingCanvas after processing
                Logger.LogInfo("Trading stroke kept on GeForce Canvas");
            }
            else
            {
                Logger.LogInfo("Simple stroke detected (MACD model) - saving normally");
                // Handle stroke completion for simple mode
                var args = new StrokeCompletedEventArgs
                {
                    Stroke = e.Stroke,
                    IsTradingMode = false,
                    Bounds = e.Stroke.GetBounds()
                };
                
                StrokeCompleted?.Invoke(this, args);
                SaveCanvasProperly();
            }
            
            // Сбрасываем состояние для следующего штриха
            isControlPressedAtStrokeStart = false;
            
            // Сбрасываем цвет кисти на желтый по умолчанию для следующего штриха
            GeForceDrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            
            Logger.LogInfo("GeForce stroke state reset and brush color reset to Yellow for next stroke");
        }
        
        private void GeForceDrawingCanvas_StrokeErasing(object sender, System.Windows.Controls.InkCanvasStrokeErasingEventArgs e)
        {
            // Always allow stroke erasing in simple mode
            e.Cancel = false;
            Logger.LogInfo("GeForce stroke erasing allowed");
        }
        
        // MT4 канвас обработчики событий для штрихов
        private void Mt4DrawingCanvas_StrokeCollected(object sender, System.Windows.Controls.InkCanvasStrokeCollectedEventArgs e)
        {
            // MT4 канвас всегда использует желтый цвет, просто сохраняем
            Logger.LogInfo("MT4 stroke collected - saving normally");
            
            var args = new StrokeCompletedEventArgs
            {
                Stroke = e.Stroke,
                IsTradingMode = false,
                Bounds = e.Stroke.GetBounds()
            };
            
            StrokeCompleted?.Invoke(this, args);
            SaveCanvasProperly();
        }
        
        private void Mt4DrawingCanvas_StrokeErasing(object sender, System.Windows.Controls.InkCanvasStrokeErasingEventArgs e)
        {
            // Always allow stroke erasing
            e.Cancel = false;
            Logger.LogInfo("MT4 stroke erasing allowed");
        }
        
        /// <summary>
        /// Создает CaptureData из штриха
        /// </summary>
        private CaptureData CreateCaptureDataFromStroke(Stroke stroke)
        {
            try
            {
                // Get stroke bounds with margin
                var bounds = stroke.GetBounds();
                int margin = 20;
                int x = (int)(bounds.Left - margin);
                int y = (int)(bounds.Top - margin);
                int width = (int)(bounds.Width + 2 * margin);
                int height = (int)(bounds.Height + 2 * margin);
                
                // Ensure coordinates are not negative
                x = Math.Max(0, x);
                y = Math.Max(0, y);
                
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
                
                // Get services from DI container
                var brokerState = ServiceContainer.Instance.GetService<BrokerState>();
                int duration = selectedDuration;
                
                // Определяем модель по координатам
                string model = "OHLC";
                if (bounds.Top >= MACD_AREA_THRESHOLD && bounds.Bottom >= MACD_AREA_THRESHOLD)
                    model = "MACD";
                else if (bounds.Top < MACD_AREA_THRESHOLD && bounds.Bottom < MACD_AREA_THRESHOLD)
                    model = "OHLC";
                
                string windowTitle = MainHelper.GetWindowTitle(targetWindowHandle);
                string period = MainHelper.ParsePeriodFromTitle(windowTitle);
                var captureEntry = new CaptureData
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Handle = targetWindowHandle.ToInt64(),
                    Monitor = monitorIndex,
                    Timestamp = DateTime.Now.ToString("o"),
                    Symbol = activeSymbol,
                    Risk = selectedRisk,
                    Source = useJForexIntegration ? "window" : "trading_canvas",
                    Broker = brokerState.GetDisplayName(),
                    Duration = duration,
                    Model = model,
                    Period = period
                };
                
                Logger.LogInfo($"CaptureData created: ID={captureEntry.ID}, Symbol={captureEntry.Symbol}, Risk={captureEntry.Risk}, Duration={captureEntry.Duration}, Broker={captureEntry.Broker}, Source={captureEntry.Source}");
                
                return captureEntry;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating CaptureData from stroke: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Обновляет настройки символа и брокера из CaptureData
        /// </summary>
        private void UpdateSettingsFromCapture(CaptureData captureData)
        {
            try
            {
                if (!string.IsNullOrEmpty(activeSymbol))
                {
                    var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                    databaseService.UpdateSymbolRisk(activeSymbol, selectedRisk);
                }
                Logger.LogInfo("Settings updated from capture");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating settings from capture: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Упрощенная обработка торгового штриха - без очереди, но с сохранением CaptureData
        /// </summary>
        private async void ProcessTradingStrokeDirectly(Stroke stroke)
        {
            try
            {
                // Создаем и сохраняем CaptureData
                var captureData = CreateCaptureDataFromStroke(stroke);
                if (captureData != null)
                {
                    var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                    var screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
                    
                    databaseService.SaveCapture(captureData);
                
                    // Capture screenshot
                    var debugInfo = screenshotService.CaptureScreenAreaDebug(
                            captureData.X, 
                            captureData.Y, 
                            captureData.Width, 
                            captureData.Height, 
                            captureData.Monitor, 
                            captureData.ID
                        );
                    captureData.ScreenshotPath = debugInfo.ScreenshotPath;
                    databaseService.UpdateCapture(captureData);
                
                    // Обновляем настройки символа и брокера
                    UpdateSettingsFromCapture(captureData);
                    
                    Logger.LogTagInfo("Trading", $"CaptureData saved to database: ID={captureData.ID}, Source={captureData.Source}");
                    
                    // Сохраняем только последний торговый штрих в Canvas файл
                    // чтобы Tracking-сервис мог его найти без желтых штрихов
                    // Используем capture.ID для формирования имени файла: trading_{capture.ID}.png
                    if (!string.IsNullOrEmpty(captureData.ID))
                    {
                        SaveTradingCanvas(stroke, captureData.ID);
                        Logger.LogTagInfo("Trading", $"Trading Canvas saved immediately for Tracking service (only last trading stroke, captureId={captureData.ID})");
                    }
                    else
                    {
                        Logger.LogTagError("Trading", "Cannot save trading canvas: captureData.ID is null or empty");
                    }
                }
                

                // Запускаем Tracking-сервис немедленно для обработки нового capture
                var captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
                if (captureTrackingService != null)
                {
                    _ = Task.Run(async () => await captureTrackingService.RunOnceAsync());
                    Logger.LogTagInfo("Trading", "Tracking service triggered immediately");
                }

                // Проверяем настройку интеграции с JForex
                if (useJForexIntegration)
                {
                    await ProcessJForexIntegration(stroke);
                }
                else
                {
                    // Режим без JForex - оставляем Canvas открытым
                    Logger.LogTagInfo("Trading", "Trading stroke processed - keeping Canvas open");
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("Trading", $"Error processing trading stroke directly: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Обработка интеграции с JForex (старая логика)
        /// </summary>
        private async Task ProcessJForexIntegration(Stroke stroke)
        {
            try
            {
                if (jForexService == null)
                {
                    Logger.LogTagError("JForex", "JForex service is not initialized");
                    return;
                }

                // Получаем точки штриха
                var points = stroke.StylusPoints;
                if (points.Count < 2)
                {
                    Logger.LogTagError("JForex", "Stroke has less than 2 points, cannot create trendline");
                    return;
                }

                // Берем первую и последнюю точки штриха
                var firstPoint = points[0];
                var lastPoint = points[points.Count - 1];

                // Конвертируем координаты из GeForceDrawingCanvas в экранные координаты с учетом смещения canvas
                var firstScreenPoint = GeForceDrawingCanvas.PointToScreen(new System.Windows.Point(firstPoint.X - canvasOffsetX, firstPoint.Y - canvasOffsetY));
                var lastScreenPoint = GeForceDrawingCanvas.PointToScreen(new System.Windows.Point(lastPoint.X - canvasOffsetX, lastPoint.Y - canvasOffsetY));

                Logger.LogTagInfo("JForex", $"Processing JForex integration: Canvas offsets: X={canvasOffsetX}, Y={canvasOffsetY}");
                Logger.LogTagInfo("JForex", $"Original stroke points: Point1=({firstPoint.X}, {firstPoint.Y}), Point2=({lastPoint.X}, {lastPoint.Y})");
                Logger.LogTagInfo("JForex", $"Adjusted stroke points: Point1=({firstPoint.X - canvasOffsetX}, {firstPoint.Y - canvasOffsetY}), Point2=({lastPoint.X - canvasOffsetX}, {lastPoint.Y - canvasOffsetY})");
                Logger.LogTagInfo("JForex", $"Final screen points: Point1=({firstScreenPoint.X}, {firstScreenPoint.Y}), Point2=({lastScreenPoint.X}, {lastScreenPoint.Y})");

                // Небольшая задержка для стабилизации
                await Task.Delay(100);

                // Активируем целевое окно и устанавливаем его в foreground
                if (!jForexService.ActivateWindow(targetWindowHandle))
                {
                    Logger.LogTagError("JForex", "Failed to activate target window");
                    return;
                }

                // Небольшая задержка для стабилизации
                await Task.Delay(400);

                bool trendlineDrawn = false;

                trendlineDrawn = await jForexService.AddTrendlineToGForexDirectlyAsync(targetWindowHandle, firstScreenPoint, lastScreenPoint);

                Logger.LogTagInfo("JForex", "JForex integration completed");

                // Если трендовая линия была успешно нарисована, запускаем callback для активации canvas window
                if (trendlineDrawn)
                {
                    Logger.LogTagInfo("JForex", "Trendline drawn successfully - triggering space hotkey callback");
                    OnTrendlineDrawn?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("JForex", $"Error in JForex integration: {ex.Message}", ex);
            }
        }

        private void RiskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && double.TryParse(btn.Content.ToString(), out double risk))
            {
                selectedRisk = risk;
                // Визуально выделить выбранную кнопку
                HighlightSelectedRiskButton(risk);
            }
        }

        private void HighlightSelectedRiskButton(double risk)
        {
            TradingToolbar.HighlightSelectedRiskButton(risk);
        }

        private void InitializeBrokerComboBox()
        {
            try
            {
                var brokerState = ServiceContainer.Instance.GetService<BrokerState>();
                var durationState = ServiceContainer.Instance.GetService<DurationState>();
                TradingToolbar.HighlightSelectedBroker(brokerState.CurrentBroker);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing broker ComboBox", ex);
            }
        }

        private void UpdateTradingToolbarUiByBroker(BrokerType brokerType)
        {
            // Mode label functionality removed - no longer needed
        }

        private void DurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Content.ToString(), out int duration))
            {
                var durationState = ServiceContainer.Instance.GetService<DurationState>();
                durationState.CurrentDuration = duration;
                selectedDuration = duration;
                HighlightSelectedDurationButton(duration);
            }
        }

        private void HighlightSelectedDurationButton(int duration)
        {
            TradingToolbar.HighlightSelectedDurationButton(duration);
        }

        private void CopyImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем размеры целевого окна
                RECT windowRect;
                if (targetWindowHandle != IntPtr.Zero && GetWindowRect(targetWindowHandle, out windowRect))
                {
                    int windowWidth = windowRect.Right - windowRect.Left;
                    int windowHeight = windowRect.Bottom - windowRect.Top;
                    
                    // Создаем RenderTargetBitmap размером с окно
                    var rtb = new RenderTargetBitmap(windowWidth, windowHeight, 96d, 96d, PixelFormats.Pbgra32);
                    
                    // Создаем временный Canvas для композиции
                    var tempCanvas = new System.Windows.Controls.Canvas
                    {
                        Width = windowWidth,
                        Height = windowHeight
                    };
                    
                    // Добавляем скриншот окна как фон
                    if (GeForceBackgroundImage.Source != null)
                    {
                        var bg = new System.Windows.Controls.Image
                        {
                            Source = GeForceBackgroundImage.Source,
                            Width = windowWidth,
                            Height = windowHeight,
                            Stretch = Stretch.None
                        };
                        tempCanvas.Children.Add(bg);
                    }
                    
                    // Создаем временный InkCanvas для штрихов
                    var tempInkCanvas = new System.Windows.Controls.InkCanvas
                    {
                        Width = windowWidth,
                        Height = windowHeight,
                        Background = System.Windows.Media.Brushes.Transparent
                    };
                    
                    // Копируем штрихи с учетом смещения canvas
                    var adjustedStrokes = new StrokeCollection();
                    foreach (var stroke in GeForceDrawingCanvas.Strokes)
                    {
                        var adjustedStroke = stroke.Clone();
                        var points = new StylusPointCollection();
                        
                        foreach (var point in stroke.StylusPoints)
                        {
                            // Применяем смещение canvas к координатам штрихов
                            points.Add(new StylusPoint(
                                point.X - canvasOffsetX, 
                                point.Y - canvasOffsetY, 
                                point.PressureFactor
                            ));
                        }
                        
                        adjustedStroke.StylusPoints = points;
                        adjustedStrokes.Add(adjustedStroke);
                    }
                    
                    tempInkCanvas.Strokes = adjustedStrokes;
                    tempCanvas.Children.Add(tempInkCanvas);
                    
                    // Добавляем CanvasTradingToolBar если он видим
                    if (CanvasTradingToolBar.Visibility == Visibility.Visible)
                    {
                        // Создаем копию CanvasTradingToolBar для рендеринга
                        var toolbarCopy = new Controls.CanvasTradingToolBar();
                        
                        // Копируем текущие данные из оригинального тулбара
                        toolbarCopy.PercentText.Text = CanvasTradingToolBar.PercentText.Text;
                        toolbarCopy.PercentText.Foreground = CanvasTradingToolBar.PercentText.Foreground;
                        toolbarCopy.PointsText.Text = CanvasTradingToolBar.PointsText.Text;
                        toolbarCopy.PointsText.Foreground = CanvasTradingToolBar.PointsText.Foreground;
                        
                        // Позиционируем тулбар в центре верхней части
                        Canvas.SetLeft(toolbarCopy, (windowWidth - 350) / 2); // 350 - фиксированная ширина тулбара
                        Canvas.SetTop(toolbarCopy, 10); // 10px от верха
                        
                        tempCanvas.Children.Add(toolbarCopy);
                        
                        Logger.LogInfo("CanvasTradingToolBar added to clipboard image");
                    }
                    
                    // Рендерим композицию
                    tempCanvas.Measure(new System.Windows.Size(windowWidth, windowHeight));
                    tempCanvas.Arrange(new Rect(0, 0, windowWidth, windowHeight));
                    rtb.Render(tempCanvas);
                    
                    System.Windows.Clipboard.SetImage(rtb);
                    
                    var toast = ServiceContainer.Instance.GetService<ToastNotifyService>();
                    toast?.ShowToast("Скопировано в буфер обмена!", ToastType.Success);
                    
                    Logger.LogInfo($"Copied window area to clipboard: {windowWidth}x{windowHeight} pixels");
                }
            }
            catch (Exception ex)
            {
                var toast = ServiceContainer.Instance.GetService<ToastNotifyService>();
                toast?.ShowToast($"Ошибка копирования: {ex.Message}", ToastType.Error);
                Logger.LogError($"Error copying to clipboard: {ex.Message}", ex);
            }
        }

        private void Canvas2ColumnWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // При клике на окно устанавливаем фокус на GeForce InkCanvas
            GeForceDrawingCanvas.Focus();
            Logger.LogInfo("Window mouse down - focus set to GeForce InkCanvas");
        }

        private void Canvas2ColumnWindow_Activated(object sender, EventArgs e)
        {
            // При активации окна устанавливаем фокус на GeForce InkCanvas
            GeForceDrawingCanvas.Focus();
            Logger.LogInfo($"Window activated - focus set to GeForce InkCanvas. targetWindowHandle={targetWindowHandle.ToInt64()}, mt4WindowHandle={mt4WindowHandle.ToInt64()}, Visibility={this.Visibility}, Topmost={this.Topmost}");
        }
        
        // Canvas shift methods - теперь сдвигаем только штрихи
        public void ShiftCanvasUp()
        {
            ShiftStrokes(0, -SHIFT_STEP);
            Logger.LogDebug($"Strokes shifted up. New offset: X={canvasOffsetX}, Y={canvasOffsetY}");
        }
        
        public void ShiftCanvasDown()
        {
            ShiftStrokes(0, SHIFT_STEP);
            Logger.LogDebug($"Strokes shifted down. New offset: X={canvasOffsetX}, Y={canvasOffsetY}");
        }
        
        public void ShiftCanvasLeft()
        {
            ShiftStrokes(-SHIFT_STEP / 2, 0); // 5 pixels for A/D
            Logger.LogDebug($"Strokes shifted left. New offset: X={canvasOffsetX}, Y={canvasOffsetY}");
        }
        
        public void ShiftCanvasRight()
        {
            ShiftStrokes(SHIFT_STEP / 2, 0); // 5 pixels for A/D
            Logger.LogDebug($"Strokes shifted right. New offset: X={canvasOffsetX}, Y={canvasOffsetY}");
        }
        
        private void ShiftStrokes(double deltaX, double deltaY)
        {
            canvasOffsetX += deltaX;
            canvasOffsetY += deltaY;
            
            // Сдвигаем все штрихи в GeForceDrawingCanvas
            var shiftedStrokes = new StrokeCollection();
            foreach (var stroke in GeForceDrawingCanvas.Strokes)
            {
                var shiftedStroke = stroke.Clone();
                var points = new StylusPointCollection();
                
                foreach (var point in stroke.StylusPoints)
                {
                    points.Add(new StylusPoint(point.X + deltaX, point.Y + deltaY, point.PressureFactor));
                }
                
                shiftedStroke.StylusPoints = points;
                shiftedStrokes.Add(shiftedStroke);
            }
            
            // Обновляем штрихи
            GeForceDrawingCanvas.Strokes = shiftedStrokes;
            
            // Сразу сохраняем обновленные штрихи
            SaveCanvasProperly();
        }
        
        private void ApplyCanvasShift()
        {
            // Этот метод больше не используется, так как мы сдвигаем только штрихи
            // Оставляем для совместимости
        }
        
        public void ResetCanvasPosition()
        {
            // Сбрасываем позицию штрихов к исходному состоянию
            if (canvasOffsetX != 0 || canvasOffsetY != 0)
            {
                ShiftStrokes(-canvasOffsetX, -canvasOffsetY);
                canvasOffsetX = 0;
                canvasOffsetY = 0;
            }
            Logger.LogDebug("Canvas position reset to origin");
        }
        
        public void ClearCanvas()
        {
            Logger.LogInfo("Clearing canvas and deleting files");
            GeForceDrawingCanvas.Strokes.Clear();
            Mt4DrawingCanvas.Strokes.Clear();
            DeleteCanvasFiles();
            
            // Сбрасываем позицию
            canvasOffsetX = 0;
            canvasOffsetY = 0;

            // Mark all related captures as skipped
            try
            {
                var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                if (databaseService != null)
                {
                    long thisHandle = targetWindowHandle.ToInt64();
                    databaseService.MarkTradingCanvasCapturesAsSkipped(thisHandle);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error marking captures as skipped after canvas clear", ex);
            }
        }

        public void ReinitializeForNewSymbol(string newSymbol)
        {
            activeSymbol = newSymbol;
            // Очищаем canvas и загружаем новый
            GeForceDrawingCanvas.Strokes.Clear();
            Mt4DrawingCanvas.Strokes.Clear();
            LoadCanvas();
            SetBackgroundImage(shouldLog: true);
            GeForceDrawingCanvas.Focus();
            
            // Обновляем символ в CanvasTradingToolBar
            CanvasTradingToolBar.SetSymbol(newSymbol);
            
            Logger.LogInfo($"Canvas2ColumnWindow reinitialized successfully for symbol {newSymbol}");
        }

        private void UpdateActiveSymbolDisplay()
        {
            if (!string.IsNullOrEmpty(activeSymbol))
            {
                ActiveSymbolText.Text = activeSymbol;
                ActiveSymbolText.Visibility = Visibility.Visible;
                
                // Устанавливаем HandleID в TradingToolbar
                if (targetWindowHandle != IntPtr.Zero)
                {
                    TradingToolbar.SetHandleID(targetWindowHandle.ToInt64());
                }
                
                // Обновляем символ в CanvasTradingToolBar
                CanvasTradingToolBar.SetSymbol(activeSymbol);
                
                Logger.LogInfo($"Active symbol display updated: {activeSymbol}");
                }
                else
                {
                ActiveSymbolText.Visibility = Visibility.Collapsed;
                Logger.LogInfo("Active symbol display hidden - no active symbol");
            }
        }

        /// <summary>
        /// Переключает режим интеграции с JForex
        /// </summary>
        public void SetJForexIntegration(bool enabled)
        {
            useJForexIntegration = enabled;
            Logger.LogInfo($"JForex integration {(enabled ? "enabled" : "disabled")}. Trading strokes will be saved with Source={(enabled ? "window" : "trading_canvas")}");
        }
        
        /// <summary>
        /// Получает текущее состояние интеграции с JForex
        /// </summary>
        public bool GetJForexIntegration()
        {
            return useJForexIntegration;
        }

        /// <summary>
        /// Инициализирует CanvasTradingToolBar
        /// </summary>
        private void InitializeCanvasTradingToolBar()
        {
            try
            {
                // Устанавливаем индекс монитора в CanvasTradingToolBar
                CanvasTradingToolBar.SetMonitorIndex(monitorIndex);
                
                // Устанавливаем символ в CanvasTradingToolBar
                CanvasTradingToolBar.SetSymbol(activeSymbol);
                
                // Подписываемся на событие клика по кнопке Sec
                CanvasTradingToolBar.OnSecButtonClicked += OnSecButtonClicked;
                
                Logger.LogInfo($"CanvasTradingToolBar initialized successfully (monitorIndex={monitorIndex})");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error initializing CanvasTradingToolBar: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке Sec в CanvasTradingToolBar
        /// </summary>
        private async void OnSecButtonClicked()
        {
            if (!string.IsNullOrEmpty(activeSymbol) && mt4SocketService != null)
            {
                // Используем новый метод ClosePositionsCommand из Mt4SocketService
                bool success = await mt4SocketService.ClosePositionsCommand(activeSymbol);
            }
        }

        /// <summary>
        /// Обработчик события нового бара от HttpServerService
        /// </summary>
        private void OnNewBarReceived(object sender, NewBarEvent newBarEvent)
        {
            try
            {
                if (newBarEvent != null)
                {
                    // Проверяем, совпадает ли символ с активным символом CanvasWindow
                    if (!string.IsNullOrEmpty(activeSymbol) && newBarEvent.MatchesSymbol(activeSymbol))
                    {
                        // Дополнительная проверка заголовка окна для тиковых баров
                        bool shouldShift = true;
                        
                        if (newBarEvent.IsTickBar)
                        {
                            // Для тиковых баров проверяем, что в заголовке окна есть буква T
                            string windowTitle = MainHelper.GetWindowTitle(targetWindowHandle);
                            if (string.IsNullOrEmpty(windowTitle) || !windowTitle.Contains("T"))
                            {
                                Logger.LogDebug($"NewBar is TICK_BAR but window title '{windowTitle}' does not contain 'T' - ignoring");
                                shouldShift = false;
                            }
                            else
                            {
                                Logger.LogInfo($"Window title '{windowTitle}' contains 'T' - proceeding with TICK_BAR shift");
                            }
                        }

                        if (shouldShift)
                        {
                            Logger.LogInfo($"NewBar received for matching symbol '{newBarEvent.Symbol}' - shifting strokes left by {CanvasConstants.NEW_BAR_SHIFT_AMOUNT} pixels");
                            
                            Dispatcher.Invoke(() => {
                                ShiftStrokes(-CanvasConstants.NEW_BAR_SHIFT_AMOUNT, 0);
                            });
                            
                            Logger.LogInfo($"Strokes shifted left for symbol '{newBarEvent.Symbol}' due to new bar");
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"NewBar symbol '{newBarEvent.Symbol}' does not match active symbol '{activeSymbol}' - ignoring");
                    }
                }
                else
                {
                    Logger.LogWarning("Received NewBar event with null data");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling NewBar event: {ex.Message}", ex);
            }
        }
    }
    
} 