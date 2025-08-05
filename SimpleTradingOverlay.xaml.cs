using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

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
        private readonly JForexWindowsManagerService _jforexWindowsManagerService;
        private IntPtr _currentWindowHandle = IntPtr.Zero;
        private bool _isEnabled = true;
        private int _lastMouseX = 0;
        private int _lastMouseY = 0;
        
        // Флаг для различения пользовательского и сервисного нажатия Escape
        private bool _isServiceEscapeSending = false;

        // Состояние для обработки ценовых уровней
        private double _entryPrice = 0;
        private double _stopLossPrice = 0;
        private string _currentTradeSymbol = "";

        // Состояние для обработки P-кнопки и кликов мыши
        private bool _isWaitingForMouseClick = false;
        private IntPtr _windowHandle = IntPtr.Zero;
        
        // Состояние для обработки торгового паттерна (S-кнопка)
        private bool _isWaitingForTradingPattern = false;
        private int _tradingPatternClickCount = 0;
        
        public SimpleTradingOverlay()
        {
            InitializeComponent();
            
            _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
            _brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            _durationState = ServiceContainer.Instance.GetService<DurationState>();
            _windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
            _hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
            _jforexWindowsManagerService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();

            // Инициализация окна
            InitializeWindow();
            
            // Настройка событий TradingToolbar
            SetupTradingToolbarEvents();
            
            // Подписка на событие HttpServerService
            SetupHttpServerEvents();
            
            // Подписка на события HotkeyService
            SetupHotkeyServiceEvents();
            
            // Настройка событий TradeRectangleControl
            SetupTradeRectangleControlEvents();
            
            // Настройка событий TradePriceLevelControl
            SetupTradePriceLevelControlEvents();
            
            // Инициализация переменных для троттлинга мыши
            _lastMouseX = 0;
            _lastMouseY = 0;

            // Установка символа по умолчанию
            TradingToolbar.SetSymbol("UNKNOWN");

            // Установка глобального хука мыши
            SetupMouseHook();
            
            // Принудительно показываем окно с Trading Toolbar
            ShowTradingToolbar();
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
        
        private void ShowTradingToolbar()
        {
            try
            {
                // Позиционируем окно на основной экран при запуске
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                this.Left = screen.Bounds.Left;
                this.Top = screen.Bounds.Top;
                this.Width = screen.Bounds.Width;
                this.Height = screen.Bounds.Height; // Полная высота экрана
                
                // Показываем окно
                if (!this.IsVisible)
                {
                    this.Show();
                }
                
                // Устанавливаем символ по умолчанию
                TradingToolbar.SetSymbol("UNKNOWN");
                
                // Позиционируем TradeRectangleControl по центру экрана
                var screenRect = new RECT 
                { 
                    Left = screen.Bounds.Left, 
                    Top = screen.Bounds.Top, 
                    Right = screen.Bounds.Right, 
                    Bottom = screen.Bounds.Bottom 
                };
                PositionTradeRectangleControl(screenRect);
                
                Logger.LogInfo($"Trading Toolbar shown successfully on screen: {screen.DeviceName}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error showing Trading Toolbar", ex);
            }
        }

        private void SetupTradingToolbarEvents()
        {
            TradingToolbar.RiskChanged += (risk) => {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Trading Toolbar risk changed to {risk}");
                
                // Обновляем риск в Rectangle Control, если он видим
                if (TradeRectangleControl.Visibility == Visibility.Visible)
                {
                    TradeRectangleControl.SetRisk(risk);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Updated Rectangle Control risk to {risk}");
                }
                
                // Обновляем риск в Price Level Control, если он видим
                if (TradePriceLevelControl.Visibility == Visibility.Visible)
                {
                    TradePriceLevelControl.SetRisk(risk);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Updated Price Level Control risk to {risk}");
                }
                
                // Сохраняем настройки в ToolbarSettingsManager
                if (_currentWindowHandle != IntPtr.Zero)
                {
                    _toolbarSettingsManager.UpdateSettings(_currentWindowHandle.ToInt64(), risk: risk);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Updated toolbar settings for window {_currentWindowHandle}: Risk={risk}");
                }
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

        private void SetupTradeRectangleControlEvents()
        {
            // Обработчик изменения риска в Rectangle Control
            TradeRectangleControl.RiskChanged += (sender, risk) =>
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Rectangle Control risk changed to {risk}");
                
                // Обновляем риск в Trading Toolbar
                TradingToolbar.HighlightSelectedRiskButton(risk);
                
                // Сохраняем настройки в ToolbarSettingsManager
                if (_currentWindowHandle != IntPtr.Zero)
                {
                    _toolbarSettingsManager.UpdateSettings(_currentWindowHandle.ToInt64(), risk: risk);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Updated toolbar settings for window {_currentWindowHandle}: Risk={risk}");
                }
            };

            TradeRectangleControl.BuyClicked += async (sender, args) =>
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Buy clicked for rectangle trade: {args.Symbol} Entry:{args.EntryPrice:F5} SL:{args.StopLossPrice:F5}");
                
                try
                {
                    // Получаем риск из TradeRectangleControl
                    double risk = TradeRectangleControl.SelectedRisk;
                    
                    // Отправляем команду в MT4
                    var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
                    if (mt4SocketService != null)
                    {
                        bool result = await mt4SocketService.SendNewOrderCommand(args.Symbol, args.TradeType, args.EntryPrice, args.StopLossPrice, risk);
                        
                        if (result)
                        {
                            Logger.LogTagInfo("SimpleTradingOverlay", "Rectangle buy trade sent successfully");
                            
                            // Показываем toast сообщение
                            var toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                            if (toastNotifyService != null)
                            {
                                string toastMessage = $"Rectangle BUY от {args.EntryPrice:F5} с риском {risk} отправлена";
                                toastNotifyService.ShowToast(toastMessage, ToastType.Info, 4000);
                            }
                        }
                        else
                        {
                            Logger.LogTagWarning("SimpleTradingOverlay", "Failed to send rectangle buy trade");
                        }
                    }
                    
                    // Скрываем контрол после отправки команды
                    TradeRectangleControl.Hide();
                }
                catch (Exception ex)
                {
                    Logger.LogTagError("SimpleTradingOverlay", "Error processing rectangle buy trade", ex);
                }
            };

            TradeRectangleControl.SellClicked += async (sender, args) =>
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Sell clicked for rectangle trade: {args.Symbol} Entry:{args.EntryPrice:F5} SL:{args.StopLossPrice:F5}");
                
                try
                {
                    // Получаем риск из TradeRectangleControl
                    double risk = TradeRectangleControl.SelectedRisk;
                    
                    // Отправляем команду в MT4
                    var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
                    if (mt4SocketService != null)
                    {
                        bool result = await mt4SocketService.SendNewOrderCommand(args.Symbol, args.TradeType, args.EntryPrice, args.StopLossPrice, risk);
                        
                        if (result)
                        {
                            Logger.LogTagInfo("SimpleTradingOverlay", "Rectangle sell trade sent successfully");
                            
                            // Показываем toast сообщение
                            var toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                            if (toastNotifyService != null)
                            {
                                string toastMessage = $"Rectangle SELL от {args.EntryPrice:F5} с риском {risk} отправлена";
                                toastNotifyService.ShowToast(toastMessage, ToastType.Info, 4000);
                            }
                        }
                        else
                        {
                            Logger.LogTagWarning("SimpleTradingOverlay", "Failed to send rectangle sell trade");
                        }
                    }
                    
                    // Скрываем контрол после отправки команды
                    TradeRectangleControl.Hide();
                }
                catch (Exception ex)
                {
                    Logger.LogTagError("SimpleTradingOverlay", "Error processing rectangle sell trade", ex);
                }
            };
        }

        private void SetupTradePriceLevelControlEvents()
        {
            // Обработчик изменения риска в Price Level Control
            TradePriceLevelControl.RiskChanged += (sender, risk) =>
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Price Level Control risk changed to {risk}");
                
                // Обновляем риск в Trading Toolbar
                TradingToolbar.HighlightSelectedRiskButton(risk);
                
                // Сохраняем настройки в ToolbarSettingsManager
                if (_currentWindowHandle != IntPtr.Zero)
                {
                    _toolbarSettingsManager.UpdateSettings(_currentWindowHandle.ToInt64(), risk: risk);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Updated toolbar settings for window {_currentWindowHandle}: Risk={risk}");
                }
            };


        }

        private void SetupHttpServerEvents()
        {
            var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
            if (httpServerService != null)
            {
                // Подписываемся на новое событие JForex объектов
                httpServerService.NewJForexChartObject += HttpServerService_NewJForexChartObject;
                Logger.LogInfo("SimpleTradingOverlay subscribed to HttpServerService.NewJForexChartObject");
            }
            else
            {
                Logger.LogWarning("HttpServerService not found, cannot subscribe to NewJForexChartObject event");
            }
        }

        private void SetupHotkeyServiceEvents()
        {
            if (_hotkeysService != null)
            {
                _hotkeysService.OnEscapeKeyPressed += OnEscapeKeyPressed;
                _hotkeysService.OnFKeyPressed += OnFKeyPressed;
                _hotkeysService.OnSKeyPressed += OnSKeyPressed;
                
                Logger.LogInfo("SimpleTradingOverlay subscribed to HotkeyService events (Escape, F, and S)");
            }
            else
            {
                Logger.LogWarning("HotkeyService not found, cannot subscribe to hotkey events");
            }
        }

        private async void HttpServerService_NewJForexChartObject(object sender, JForexChartObjectData jforexChartObject)
        {
            try
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Received new JForex chart object: {jforexChartObject}");

                // Проверяем, соответствует ли символ текущему символу в тулбаре
                // Если символ в тулбаре UNKNOWN, обновляем его на символ из события
                if (string.IsNullOrEmpty(TradingToolbar.CurrentSymbol) || TradingToolbar.CurrentSymbol.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Updating toolbar symbol from UNKNOWN to {jforexChartObject.Symbol}");
                    TradingToolbar.SetSymbol(jforexChartObject.Symbol);
                }
                else if (!TradingToolbar.CurrentSymbol.Equals(jforexChartObject.Symbol, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogTagDebug("SimpleTradingOverlay", $"Symbol mismatch: current={TradingToolbar.CurrentSymbol}, event={jforexChartObject.Symbol}");
                    return;
                }

                // Показываем тост сообщение
                var toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                if (toastNotifyService != null)
                {
                    string toastMessage = $"JForex: {jforexChartObject.ObjectType} = {jforexChartObject.Price:F5} ({jforexChartObject.Symbol})";
                    toastNotifyService.ShowToast(toastMessage, ToastType.Success, 3000);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Toast notification shown: {toastMessage}");
                }

                // Обрабатываем JForex объект в зависимости от типа
                ProcessJForexChartObject(jforexChartObject);
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error processing JForex chart object", ex);
            }
        }

        private void ProcessJForexChartObject(JForexChartObjectData jforexChartObject)
        {
            try
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Processing JForex chart object: {jforexChartObject.ObjectType} = {jforexChartObject.Price}");

                // Обрабатываем объект в зависимости от типа
                switch (jforexChartObject.ObjectType)
                {
                    case JForexChartObjectType.PriceMarker:
                        ProcessPriceMarkerObject(jforexChartObject);
                        break;
                    
                    case JForexChartObjectType.Rectangle:
                        ProcessRectangleObject(jforexChartObject);
                        break;
                    
                    case JForexChartObjectType.ShortLine:
                        ProcessShortLineObject(jforexChartObject);
                        break;
                    
                    default:
                        Logger.LogTagWarning("SimpleTradingOverlay", $"Unknown JForex object type: {jforexChartObject.ObjectType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error processing JForex chart object", ex);
            }
        }

        /// <summary>
        /// Обрабатывает PriceMarker объект (аналог старой логики PriceLevel)
        /// </summary>
        private void ProcessPriceMarkerObject(JForexChartObjectData jforexChartObject)
        {
            Logger.LogTagInfo("SimpleTradingOverlay", $"Processing PriceMarker object: {jforexChartObject.Price}");

            // Если Entry Price не установлен, устанавливаем его
            if (_entryPrice == 0)
            {
                _entryPrice = Convert.ToDouble(jforexChartObject.Price);
                _currentTradeSymbol = jforexChartObject.Symbol;
                
                Logger.LogTagInfo("SimpleTradingOverlay", $"Entry Price set: {_entryPrice} for {_currentTradeSymbol}");
                
                // Устанавливаем Entry Level в активном TradingToolbar
                // TradingToolbar.SetEntryLevel(_entryPrice, _currentTradeSymbol);
                
                // Показываем TradePriceLevelControl
                ShowTradePriceLevelControl();
                
                return;
            }

            // Если Entry Price уже установлен, устанавливаем Stop Loss Price
            if (_stopLossPrice == 0)
            {
                _stopLossPrice = Convert.ToDouble(jforexChartObject.Price);

                Logger.LogTagInfo("SimpleTradingOverlay", $"Stop Loss Price set: {_stopLossPrice} for {_currentTradeSymbol}");

                // Скрываем TradePriceLevelControl при установке Stop Loss Price
                TradePriceLevelControl.Hide();
                Logger.LogTagInfo("SimpleTradingOverlay", "TradePriceLevelControl hidden due to Stop Loss Price being set");

                // Показываем тост сообщение о установке Stop Loss Price
                var toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                if (toastNotifyService != null)
                {
                    string tradeType = _entryPrice > _stopLossPrice ? "BUY" : "SELL";
                    string toastMessage = $"Stop Loss: {_stopLossPrice:F5} | Trade: {tradeType} ({_currentTradeSymbol})";
                    toastNotifyService.ShowToast(toastMessage, ToastType.Success, 3000);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Stop Loss toast shown: {toastMessage}");
                }

                // Проверяем, если обе цены установлены, отправляем команду в MT4
                if (_entryPrice > 0 && _stopLossPrice > 0)
                {
                    SendTradeCommandToMt4();
                    ResetPriceLevelStateToWaitForEntry();
                }
            }
        }

        /// <summary>
        /// Обрабатывает Rectangle объект
        /// </summary>
        private void ProcessRectangleObject(JForexChartObjectData jforexChartObject)
        {
            Logger.LogTagInfo("SimpleTradingOverlay", $"Processing Rectangle object: {jforexChartObject.Price}");
            
            try
            {
                // Парсим дополнительные данные для получения верхней и нижней цены
                var rectangleData = ParseRectangleData(jforexChartObject.AdditionalData);
                if (rectangleData != null)
                {
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Parsed rectangle data: Upper={rectangleData.UpperPrice:F5}, Lower={rectangleData.LowerPrice:F5}");
                    
                    // Устанавливаем данные в TradeRectangleControl
                    TradeRectangleControl.SetRectangleData(jforexChartObject.Symbol, rectangleData.UpperPrice, rectangleData.LowerPrice);
                    
                    // Применяем настройки риска из ToolbarSettingsManager
                    if (_currentWindowHandle != IntPtr.Zero)
                    {
                        var toolbarSettings = _toolbarSettingsManager.GetSettings(_currentWindowHandle.ToInt64());
                        if (toolbarSettings != null)
                        {
                            TradeRectangleControl.SetRisk(toolbarSettings.Risk);
                            Logger.LogTagInfo("SimpleTradingOverlay", $"Applied risk {toolbarSettings.Risk} to Rectangle Control from toolbar settings");
                        }
                        else
                        {
                            // Используем риск по умолчанию
                            TradeRectangleControl.SetRisk(1.0);
                            Logger.LogTagInfo("SimpleTradingOverlay", "Applied default risk 1.0 to Rectangle Control");
                        }
                    }
                    
                    // Позиционируем контрол по центру экрана
                    var screen = System.Windows.Forms.Screen.PrimaryScreen;
                    var screenRect = new RECT 
                    { 
                        Left = screen.Bounds.Left, 
                        Top = screen.Bounds.Top, 
                        Right = screen.Bounds.Right, 
                        Bottom = screen.Bounds.Bottom 
                    };
                    PositionTradeRectangleControl(screenRect);
                    
                    // Показываем контрол
                    TradeRectangleControl.Show();
                    
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Rectangle control shown and positioned for {jforexChartObject.Symbol}: Upper={rectangleData.UpperPrice:F5}, Lower={rectangleData.LowerPrice:F5}");
                    Logger.LogTagInfo("SimpleTradingOverlay", $"TradeRectangleControl Visibility: {TradeRectangleControl.Visibility}");
                }
                else
                {
                    Logger.LogTagWarning("SimpleTradingOverlay", "Failed to parse rectangle data from AdditionalData");
                    Logger.LogTagWarning("SimpleTradingOverlay", $"AdditionalData content: {jforexChartObject.AdditionalData}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error processing Rectangle object", ex);
            }
        }

        /// <summary>
        /// Обрабатывает ShortLine объект
        /// </summary>
        private void ProcessShortLineObject(JForexChartObjectData jforexChartObject)
        {
            Logger.LogTagInfo("SimpleTradingOverlay", $"Processing ShortLine object: {jforexChartObject.Price}");
            // TODO: Добавить логику обработки ShortLine объектов
        }

        private async void SendTradeCommandToMt4()
        {
            try
            {
                var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
                if (mt4SocketService == null)
                {
                    Logger.LogTagWarning("SimpleTradingOverlay", "MT4 Socket Service is null, cannot send trade command");
                    return;
                }

                // Определяем тип сделки на основе цен
                string tradeType = _entryPrice > _stopLossPrice ? "buy" : "sell";
                
                // Получаем риск из тулбара
                double risk = TradingToolbar.SelectedRisk;

                Logger.LogTagInfo("SimpleTradingOverlay", $"Sending trade command to MT4: {tradeType} {_currentTradeSymbol} Entry:{_entryPrice} SL:{_stopLossPrice} Risk:{risk}");

                // Показываем toast сообщение о создании новой сделки
                var toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                if (toastNotifyService != null)
                {
                    string tradeTypeDisplay = tradeType.ToUpper();
                    string toastMessage = $"Сделка {tradeTypeDisplay} от {_entryPrice:F5} с риском {risk} отправлена";
                    toastNotifyService.ShowToast(toastMessage, ToastType.Info, 4000);
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Trade creation toast shown: {toastMessage}");
                }

                // Используем новый метод SendNewOrderCommand из MT4SocketService
                bool result = await mt4SocketService.SendNewOrderCommand(_currentTradeSymbol, tradeType, _entryPrice, _stopLossPrice, risk);

                if (result)
                {
                    Logger.LogTagInfo("SimpleTradingOverlay", "MT4 command sent successfully, clearing UI");
                }
                else
                {
                    Logger.LogTagWarning("SimpleTradingOverlay", "Failed to send MT4 command");
                }

                // Сбрасываем состояние после отправки команды в MT4
                ResetPriceLevelState();
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error sending trade command to MT4", ex);
                
                // Даже при ошибке очищаем UI, чтобы избежать неправильного отображения
                Logger.LogTagInfo("SimpleTradingOverlay", "Clearing UI due to error in MT4 command");
                ResetPriceLevelState();
            }
        }

        private void ResetPriceLevelState()
        {
            _entryPrice = 0;
            _stopLossPrice = 0;
            _currentTradeSymbol = "";
            
            // Скрываем цены в UI
            TradingToolbar.HidePrices();
            
            Logger.LogTagInfo("SimpleTradingOverlay", "Price level state reset - ready for new PriceMarkerChartObject cycle");
        }

        /// <summary>
        /// Сбрасывает состояние ценовых уровней - обнуляет Entry Price
        /// Используется при нажатии Escape для отмены текущего состояния
        /// </summary>
        private void ResetPriceLevelStateToWaitForEntry()
        {
            _entryPrice = 0; // Обнуляем только Entry Price
            _stopLossPrice = 0;
            _currentTradeSymbol = "";
            
            // Скрываем цены в UI
            TradingToolbar.HidePrices();
            
            Logger.LogTagInfo("SimpleTradingOverlay", "Entry Price reset - ready for new PriceMarkerChartObject");
        }

        public void OnEscapeKeyPressed()
        {
            if (!_isEnabled) return;

            // Проверяем, не является ли это сервисным нажатием Escape
            if (_isServiceEscapeSending)
            {
                Logger.LogTagInfo("SimpleTradingOverlay", "Ignoring service Escape key press");
                return;
            }

            Logger.LogTagInfo("SimpleTradingOverlay", "Escape key pressed via HotkeyService");

            // Скрываем TradeRectangleControl
            TradeRectangleControl.Hide();
            
            // Скрываем TradePriceLevelControl
            TradePriceLevelControl.Hide();

            // Сбрасываем состояние ценовых уровней и переходим в режим ожидания Entry Price
            if (TradingToolbar != null)
            {
                Logger.LogTagInfo("SimpleTradingOverlay", "Escape key pressed - resetting price level state to wait for Entry Price");
                ResetPriceLevelStateToWaitForEntry();
            }

            // Отменяем торговый паттерн если он активен
            if (_isWaitingForTradingPattern)
            {
                Logger.LogTagInfo("SimpleTradingOverlay", "Escape key pressed - canceling trading pattern capture");
                _isWaitingForTradingPattern = false;
                _tradingPatternClickCount = 0;
            }
        }

        public void OnFKeyPressed()
        {
            if (!_isEnabled) return;

            Logger.LogTagInfo("SimpleTradingOverlay", "F key pressed via HotkeyService - waiting for mouse click");

            // Активируем режим ожидания клика мыши
            _isWaitingForMouseClick = true;
        }

        public void OnSKeyPressed()
        {
            if (!_isEnabled) return;

            Logger.LogTagInfo("SimpleTradingOverlay", "S key pressed via HotkeyService - starting trading pattern capture");

            // Активируем режим ожидания торгового паттерна (два клика мыши)
            _isWaitingForTradingPattern = true;
            _tradingPatternClickCount = 0;
        }

        private void UpdateTradingToolbarUiByBroker(BrokerType brokerType)
        {
            if (brokerType == BrokerType.Forex)
            {
                TradingToolbar.ShowDurationPanel(false);
            }
            else
            {
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
                    
                    // Троттлинг: проверяем, переместилась ли мышь на достаточное расстояние
                    int deltaX = Math.Abs(hookStruct.pt.X - _lastMouseX);
                    int deltaY = Math.Abs(hookStruct.pt.Y - _lastMouseY);
                    
                    // Обновляем позицию только если мышь переместилась на 50 пикселей или больше (уменьшил с 100)
                    if (deltaX >= 50 || deltaY >= 50)
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
                else if (message == WM_LBUTTONDOWN && _isWaitingForMouseClick)
                {
                    // Обрабатываем клик мыши только если ожидаем его после нажатия P
                    Logger.LogTagInfo("SimpleTradingOverlay", "Mouse click detected after P key press");
                    
                    // Отключаем режим ожидания клика
                    _isWaitingForMouseClick = false;
                    
                    // Отправляем Escape через 200 мс в текущее окно Toolbar
                    SendDelayedEscapeToCurrentWindow();
                }
                else if (message == WM_LBUTTONDOWN && _isWaitingForTradingPattern)
                {
                    // Обрабатываем клик мыши для торгового паттерна
                    _tradingPatternClickCount++;
                    Logger.LogTagInfo("SimpleTradingOverlay", $"Trading pattern click {_tradingPatternClickCount} detected");
                    
                    if (_tradingPatternClickCount >= 2)
                    {
                        // Торговый паттерн завершен
                        Logger.LogTagInfo("SimpleTradingOverlay", "Trading pattern completed - sending delayed escape");
                        
                        // Отключаем режим ожидания торгового паттерна
                        _isWaitingForTradingPattern = false;
                        _tradingPatternClickCount = 0;
                        
                        // Отправляем Escape через 300 мс в текущее окно
                        SendDelayedEscapeForTradingPattern();
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
                    // Получаем экран, на котором находится окно
                    var screen = System.Windows.Forms.Screen.FromHandle(windowHandle);
                    if (screen == null)
                    {
                        // Если не удалось определить экран, используем основной
                        screen = System.Windows.Forms.Screen.PrimaryScreen;
                    }
                    
                    // Позиционируем оверлей на весь экран, где находится окно
                    double left = screen.Bounds.Left;
                    double top = screen.Bounds.Top;
                    double width = screen.Bounds.Width;
                    double height = screen.Bounds.Height;
                    
                    // Устанавливаем размер оверлея равным размеру экрана
                    this.Width = width;
                    this.Height = height;
                    this.Left = left;
                    this.Top = top;
                    
                    // Позиционируем TradingToolbar в верхней части экрана по центру
                    Canvas.SetTop(TradingToolbar, 0);
                    
                    // Центрируем TradingToolbar горизонтально
                    double toolbarWidth = TradingToolbar.ActualWidth > 0 ? TradingToolbar.ActualWidth : 1200; // Примерная ширина
                    double centerX = (width - toolbarWidth) / 2;
                    Canvas.SetLeft(TradingToolbar, centerX);
                    
                    // TradingToolbar теперь позиционируется по центру
                    
                    // Позиционируем TradeRectangleControl по центру справа
                    PositionTradeRectangleControl(windowRect);
                    
                    // Устанавливаем символ в тулбаре
                    TradingToolbar.SetSymbol(symbol);
                    
                    // Устанавливаем HandleID
                    TradingToolbar.SetHandleID(windowHandle.ToInt64());
                    
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

        /// <summary>
        /// Позиционирует TradeRectangleControl по центру экрана справа
        /// </summary>
        private void PositionTradeRectangleControl(RECT windowRect)
        {
            try
            {
                // Получаем экран, на котором находится окно
                var screen = System.Windows.Forms.Screen.FromHandle(_currentWindowHandle);
                if (screen == null)
                {
                    // Если не удалось определить экран, используем основной
                    screen = System.Windows.Forms.Screen.PrimaryScreen;
                }
                
                double screenHeight = screen.Bounds.Height;
                double screenWidth = screen.Bounds.Width;
                
                // Вычисляем центр экрана по вертикали
                double centerY = screenHeight / 2;
                
                // Используем фиксированную высоту контрола (примерно 200px) если ActualHeight еще не доступна
                double controlHeight = TradeRectangleControl.ActualHeight > 0 ? TradeRectangleControl.ActualHeight : 200;
                
                // Позиционируем контрол по центру экрана справа
                Canvas.SetTop(TradeRectangleControl, centerY - (controlHeight / 2));
                
                // Устанавливаем отступ справа (контрол должен быть виден)
                Canvas.SetRight(TradeRectangleControl, 3);
                
                Logger.LogTagInfo("SimpleTradingOverlay", $"TradeRectangleControl positioned at screen center-right: Y={centerY:F0}, ControlHeight={controlHeight:F0}, ScreenHeight={screenHeight:F0}, Right=20, Screen: {screen.DeviceName}");
                Logger.LogTagInfo("SimpleTradingOverlay", $"TradeRectangleControl Canvas.Top: {Canvas.GetTop(TradeRectangleControl)}, Canvas.Right: {Canvas.GetRight(TradeRectangleControl)}");
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error positioning TradeRectangleControl", ex);
            }
        }

        /// <summary>
        /// Показывает TradePriceLevelControl
        /// </summary>
        private void ShowTradePriceLevelControl()
        {
            try
            {
                Logger.LogTagInfo("SimpleTradingOverlay", $"Showing TradePriceLevelControl for {_currentTradeSymbol} at entry price {_entryPrice:F5}");
                
                // Устанавливаем данные в контрол
                TradePriceLevelControl.SetPriceLevelData(_currentTradeSymbol, _entryPrice);
                
                // Применяем настройки риска из ToolbarSettingsManager
                if (_currentWindowHandle != IntPtr.Zero)
                {
                    var toolbarSettings = _toolbarSettingsManager.GetSettings(_currentWindowHandle.ToInt64());
                    if (toolbarSettings != null)
                    {
                        TradePriceLevelControl.SetRisk(toolbarSettings.Risk);
                        Logger.LogTagInfo("SimpleTradingOverlay", $"Applied risk {toolbarSettings.Risk} to Price Level Control from toolbar settings");
                    }
                    else
                    {
                        // Используем риск по умолчанию
                        TradePriceLevelControl.SetRisk(1.0);
                        Logger.LogTagInfo("SimpleTradingOverlay", "Applied default risk 1.0 to Price Level Control");
                    }
                }
                
                // Позиционируем контрол по центру экрана
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                var screenRect = new RECT 
                { 
                    Left = screen.Bounds.Left, 
                    Top = screen.Bounds.Top, 
                    Right = screen.Bounds.Right, 
                    Bottom = screen.Bounds.Bottom 
                };
                // Показываем контрол
                TradePriceLevelControl.Show();
                
                // Позиционируем контрол после показа с небольшой задержкой
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PositionTradePriceLevelControl(screenRect);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                Logger.LogTagInfo("SimpleTradingOverlay", $"Price Level control shown and positioned for {_currentTradeSymbol}: Entry={_entryPrice:F5}");
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error showing TradePriceLevelControl", ex);
            }
        }

        /// <summary>
        /// Позиционирует TradePriceLevelControl по центру экрана справа
        /// </summary>
        private void PositionTradePriceLevelControl(RECT windowRect)
        {
            try
            {
                // Получаем экран, на котором находится окно
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                
                double screenHeight = screen.Bounds.Height;
                double screenWidth = screen.Bounds.Width;
                
                // Вычисляем центр экрана по вертикали
                double centerY = screenHeight / 2;
                
                // Используем фиксированную высоту контрола (примерно 150px) если ActualHeight еще не доступна
                double controlHeight = TradePriceLevelControl.ActualHeight > 0 ? TradePriceLevelControl.ActualHeight : 150;
                
                // Позиционируем контрол по центру экрана справа
                Canvas.SetTop(TradePriceLevelControl, centerY - (controlHeight / 2));
                
                // Устанавливаем отступ справа (контрол должен быть виден)
                Canvas.SetRight(TradePriceLevelControl, 3);
                
                // Принудительно обновляем layout
                TradePriceLevelControl.UpdateLayout();
                MainCanvas.UpdateLayout();
                
                Logger.LogTagInfo("SimpleTradingOverlay", $"TradePriceLevelControl positioned at screen center-right: Y={centerY:F0}, ControlHeight={controlHeight:F0}, ScreenHeight={screenHeight:F0}, Right=20, Screen: {screen.DeviceName}");
                Logger.LogTagInfo("SimpleTradingOverlay", $"TradePriceLevelControl Canvas.Top: {Canvas.GetTop(TradePriceLevelControl)}, Canvas.Right: {Canvas.GetRight(TradePriceLevelControl)}");
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error positioning TradePriceLevelControl", ex);
            }
        }

        /// <summary>
        /// Парсит данные прямоугольника из AdditionalData
        /// </summary>
        private RectangleData ParseRectangleData(string additionalData)
        {
            try
            {
                if (string.IsNullOrEmpty(additionalData))
                    return null;

                // Ищем данные о верхней и нижней цене в JSON
                if (additionalData.Contains("upperPrice") && additionalData.Contains("lowerPrice"))
                {
                    // Извлекаем значения цен из JSON строки
                    var upperPriceMatch = System.Text.RegularExpressions.Regex.Match(additionalData, @"""upperPrice"":\s*([\d.]+)");
                    var lowerPriceMatch = System.Text.RegularExpressions.Regex.Match(additionalData, @"""lowerPrice"":\s*([\d.]+)");
                    
                    if (upperPriceMatch.Success && lowerPriceMatch.Success)
                    {
                        double upperPrice = double.Parse(upperPriceMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        double lowerPrice = double.Parse(lowerPriceMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        
                        return new RectangleData { UpperPrice = upperPrice, LowerPrice = lowerPrice };
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogTagError("SimpleTradingOverlay", "Error parsing rectangle data", ex);
                return null;
            }
        }

        /// <summary>
        /// Данные прямоугольника
        /// </summary>
        private class RectangleData
        {
            public double UpperPrice { get; set; }
            public double LowerPrice { get; set; }
        }

        private void ApplyToolbarSettings(IntPtr windowHandle)
        {
            try
            {
                // Устанавливаем символ для текущего окна
                string symbol = ExtractSymbolFromWindowTitle(windowHandle);
                TradingToolbar.SetSymbol(symbol);
                
                // Устанавливаем HandleID
                TradingToolbar.SetHandleID(windowHandle.ToInt64());
                
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
                    
                    // Проверяем, есть ли Rectangle Control, и устанавливаем такой же риск
                    if (TradeRectangleControl != null)
                    {
                        TradeRectangleControl.SetRisk(toolbarSettings.Risk);
                        Logger.LogTagInfo("SimpleTradingOverlay", $"Applied risk {toolbarSettings.Risk} to Rectangle Control from toolbar settings");
                    }
                    
                    Logger.LogInfo($"Applied toolbar settings for window {windowHandle}: Risk={toolbarSettings.Risk}, Duration={toolbarSettings.Duration}, Broker={toolbarSettings.Broker}");
                }
                else
                {
                    // Применяем настройки по умолчанию
                    TradingToolbar.HighlightSelectedRiskButton(1);
                    TradingToolbar.HighlightSelectedDurationButton(2);
                    TradingToolbar.HighlightSelectedBroker(BrokerType.Forex);
                    UpdateTradingToolbarUiByBroker(BrokerType.Forex);
                    
                    // Создаем настройки с дефолтными значениями
                    _toolbarSettingsManager.UpdateSettings(windowHandle.ToInt64(), 1, 2, BrokerType.Forex);
                    
                    // Проверяем, есть ли Rectangle Control, и устанавливаем такой же риск (по умолчанию 1)
                    if (TradeRectangleControl != null)
                    {
                        TradeRectangleControl.SetRisk(1);
                        Logger.LogTagInfo("SimpleTradingOverlay", $"Applied default risk 1 to Rectangle Control from toolbar settings");
                    }
                    
                    Logger.LogInfo($"Applied and created default toolbar settings for window {windowHandle}");
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
        
        public void ForceShowTradingToolbar()
        {
            ShowTradingToolbar();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Отписываемся от событий HotkeyService
            if (_hotkeysService != null)
            {
                _hotkeysService.OnEscapeKeyPressed -= OnEscapeKeyPressed;
                _hotkeysService.OnFKeyPressed -= OnFKeyPressed;
                _hotkeysService.OnSKeyPressed -= OnSKeyPressed;
                Logger.LogInfo("SimpleTradingOverlay unsubscribed from HotkeyService events (Escape, F, and S)");
            }
            
            // Отписываемся от событий HttpServerService
            var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
            if (httpServerService != null)
            {
                httpServerService.NewJForexChartObject -= HttpServerService_NewJForexChartObject;
                Logger.LogInfo("SimpleTradingOverlay unsubscribed from HttpServerService events");
            }
            
            if (mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
            
            base.OnClosed(e);
        }

        /// <summary>
        /// Отправляет Escape с задержкой 200 мс в текущее окно Toolbar
        /// Используется после нажатия P-кнопки и клика мыши
        /// </summary>
        private void SendDelayedEscapeToCurrentWindow()
        {
            Logger.LogTagInfo("SimpleTradingOverlay", "SendDelayedEscapeToCurrentWindow called - sending Escape to current window");
            
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200);
                    _jforexWindowsManagerService?.SendEscKey(_currentWindowHandle);
                }
                catch (Exception ex)
                {
                    Logger.LogTagError("SimpleTradingOverlay", "Error in SendDelayedEscapeToCurrentWindow", ex);
                }
            });
        }

        /// <summary>
        /// Отправляет Escape с задержкой 300 мс в текущее окно после завершения торгового паттерна
        /// Используется после нажатия S-кнопки и двух кликов мыши
        /// </summary>
        private void SendDelayedEscapeForTradingPattern()
        {
            Logger.LogTagInfo("SimpleTradingOverlay", "SendDelayedEscapeForTradingPattern called - sending Escape to current window after trading pattern completion");
            
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300);
                    _jforexWindowsManagerService?.SendEscKey(_currentWindowHandle);
                }
                catch (Exception ex)
                {
                    Logger.LogTagError("SimpleTradingOverlay", "Error in SendDelayedEscapeForTradingPattern", ex);
                }
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            
            // Инициализируем handle окна для использования в других потоках
            _windowHandle = new WindowInteropHelper(this).Handle;
            Logger.LogTagInfo("SimpleTradingOverlay", $"Window handle initialized: {_windowHandle}");
        }
    }
} 