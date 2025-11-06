using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Forms;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Collections.Generic;
using System.IO;
using System.Windows.Threading;
using ScreenCaptureApp.Helpers;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenCaptureApp
{
    public partial class MainWindow : Window
    {
        private ScreenCaptureOverlay overlay;
        private SimpleTradingOverlay simpleTradingOverlay;
        private bool isCapturing = false;
        private IntPtr targetWindow = IntPtr.Zero; // Variable to store target window handle
        private CanvasWindow currentCanvasWindow = null;
        private CanvasWindow secondaryCanvasWindow = null; // Canvas для второго монитора
        private string activeSymbol = null;
        private CancellationTokenSource _autoTrackingCts;
        private Task _autoTrackingTask;
        private bool isAutoTrackingActive = false;
        private const double CollapsedHeight = 55;
        private const double ExpandedHeight = 600;
        
        // Brush color state
        private bool isYellowBrush = false; // true = yellow, false = white - default to white for SimpleMod
        
        // Public property to access brush color state
        public bool IsYellowBrush => isYellowBrush;
        
        // Trading Toolbar visibility control
        private bool showTradingToolbar = false; // false = do not show TradingToolbar

        // JForex integration setting
        private bool useJForexIntegration = false; // false = save to CaptureData with TradingCanvas source, true = send to JForex

        // Canvas Window on secondary monitor control
        // false = open Canvas only on primary monitor (default), true = open on all monitors
        private bool openCanvasOnSecondaryMonitor = false;

        public MainWindow()
        {
            ServiceInitializer.RegisterAllServices();
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            InitializeServices();

            StartAutoTracking(); // Автотрекинг включен по умолчанию

            var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            if (mt4SocketService != null)
            {
                mt4SocketService.ConnectionStatusChanged += Mt4SocketService_ConnectionStatusChanged;
                mt4SocketService.OrdersSummaryReceived += Mt4SocketService_OrdersSummaryReceived;
            }

            var captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
            if (mt4SocketService != null && captureTrackingService != null)
                mt4SocketService.SubscribeToCaptureTrackingEvents(captureTrackingService);

            // var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>(); // Temporarily disabled
            // if (binaryOptionsSocketService != null && captureTrackingService != null)
            //     binaryOptionsSocketService.SubscribeToCaptureTrackingEvents(captureTrackingService);

            // Инициализация HTTP Server Service
            ServiceInitializer.InitializeHttpServerService(this.Dispatcher);

            this.Loaded += async (s, e) =>
            {
                if (mt4SocketService != null)
                    await mt4SocketService.ConnectAsync();
            };

            var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            ((App)System.Windows.Application.Current).SubscribeToCaptureTrackingIteration(captureTrackingService, databaseService);

                    // Инициализация SimpleTradingOverlay
        InitializeSimpleTradingOverlay();

            var hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
            if (hotkeysService != null)
            {
                hotkeysService.OnSymbolHotkeyPressed1 += () => TriggerSymbolButton(0);
                hotkeysService.OnSymbolHotkeyPressed2 += () => TriggerSymbolButton(1);
                hotkeysService.OnSymbolHotkeyPressed3 += () => TriggerSymbolButton(2);
                hotkeysService.OnSymbolHotkeyPressed4 += () => TriggerSymbolButton(3);
                hotkeysService.OnSymbolHotkeyPressed5 += () => TriggerSymbolButton(4);
                hotkeysService.OnSymbolHotkeyPressed6 += () => TriggerSymbolButton(5);
                hotkeysService.OnQHotkey += () => { 
                    Logger.LogInfo("[DEBUG] OnQHotkey event in MainWindow - Skipping all tracking captures"); 
                    var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                    if (databaseService != null)
                    {
                        databaseService.SkipAllTrackingCaptures();
                        Logger.LogInfo("Q hotkey: All tracking captures marked as skipped");
                    }
                    else
                    {
                        Logger.LogWarning("Q hotkey: DatabaseService not available");
                    }
                };
                hotkeysService.OnWHotkey += () => { Logger.LogInfo("[DEBUG] OnWHotkey event in MainWindow"); ClickHotkeyButtonByIndex(1); };
                hotkeysService.OnEHotkey += () => { Logger.LogInfo("[DEBUG] OnEHotkey event in MainWindow"); ClickHotkeyButtonByIndex(2); };
                hotkeysService.OnRHotkey += () => { Logger.LogInfo("[DEBUG] OnRHotkey event in MainWindow"); ClickHotkeyButtonByIndex(3); };
                hotkeysService.OnJHotkey += () => { Logger.LogInfo("[DEBUG] OnJHotkey event in MainWindow"); ToggleJForexIntegration(); };
                hotkeysService.OnLeftShiftHotkey += () =>
                {
                    Logger.LogInfo("[DEBUG] OnLeftShiftHotkey event in MainWindow");
                    ToggleTrackingViewer_Click(null, null);
                };
            }
        }

        private void InitializeSimpleTradingOverlay()
        {
            try
            {
                simpleTradingOverlay = new SimpleTradingOverlay();
                
                // Управляем показом Trading Toolbar через переменную
                simpleTradingOverlay.SetTradingToolbarEnabled(showTradingToolbar);
                
                Logger.LogInfo("SimpleTradingOverlay initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing SimpleTradingOverlay", ex);
            }
        }

        private void InitializeServices()
        {
            try
            {
                // Initialize DatabaseService first
                var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                if (databaseService != null)
                {
                    databaseService.Initialize();
                    Logger.LogInfo("DatabaseService initialized successfully");
                }

                // Initialize services using helper
                            ServiceInitializer.InitializeHotkeysService(
                Dispatcher,
                OnSpaceKeyPressed,
                OnEscapeKeyPressed,
                OnBackQuoteKeyPressed,
                OnCKeyPressed,
                OnWKeyPressed,
                OnAKeyPressed,
                OnSKeyPressed,
                OnDKeyPressed);

                ServiceInitializer.InitializeWindowManagementService(Dispatcher);
                ServiceInitializer.InitializeMt4SocketService(Dispatcher);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initialize services: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSpaceKeyPressed()
        {
            IntPtr newTargetWindow = MainHelper.GetWindowUnderCursor();
            
            if (newTargetWindow == IntPtr.Zero)
            {
                Logger.LogDebug("No target window found under cursor");
                return;
            }
            
            Logger.LogDebug($"Target window captured (Handle: 0x{newTargetWindow:X})");
            targetWindow = newTargetWindow;
            
            // Извлекаем символ из заголовка окна на основном мониторе
            string symbolFromPrimaryWindow = MainHelper.ExtractSymbolFromWindowTitle(newTargetWindow);
            Logger.LogDebug($"Symbol extracted from primary window: {symbolFromPrimaryWindow ?? "null"}");
            
            // Проверяем, все ли Canvas открыты и видимы - если да, скрываем все
            var screens = Screen.AllScreens;
            bool allVisible = true;
            if (currentCanvasWindow == null || !currentCanvasWindow.IsVisible)
                allVisible = false;
            if (openCanvasOnSecondaryMonitor && screens.Length > 1 && (secondaryCanvasWindow == null || !secondaryCanvasWindow.IsVisible))
                allVisible = false;
            
            if (allVisible)
            {
                // Скрываем все Canvas
                if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
                {
                    currentCanvasWindow.Visibility = Visibility.Hidden;
                    Logger.LogDebug("Space pressed: Primary Canvas window HIDDEN");
                }
                if (secondaryCanvasWindow != null && secondaryCanvasWindow.IsVisible)
                {
                    secondaryCanvasWindow.Visibility = Visibility.Hidden;
                    Logger.LogDebug("Space pressed: Secondary Canvas window HIDDEN");
                }
                return;
            }
            
            // Открываем/показываем Canvas на мониторах (в зависимости от настройки openCanvasOnSecondaryMonitor)
            if (openCanvasOnSecondaryMonitor && screens.Length > 1)
            {
                Logger.LogDebug($"Opening Canvas on all {screens.Length} monitor(s)");
            }
            else
            {
                Logger.LogDebug("Opening Canvas on primary monitor only");
            }
            
            // Монитор 0 (основной) - используем окно под курсором
            if (currentCanvasWindow != null)
            {
                currentCanvasWindow.UpdateTargetWindow(newTargetWindow, activeSymbol);
                currentCanvasWindow.Visibility = Visibility.Visible;
                currentCanvasWindow.Activate();
                currentCanvasWindow.Focus();
                Logger.LogDebug("Space pressed: Primary Canvas window SHOWN (existing window)");
            }
            else
            {
                OpenCanvasOnPrimaryMonitor(newTargetWindow);
                Logger.LogDebug("Space pressed: Primary Canvas window SHOWN (new window)");
            }
            
            // Монитор 1 (вторичный), если есть - ищем окно с тем же символом
            if (openCanvasOnSecondaryMonitor && screens.Length > 1)
            {
                IntPtr secondaryTargetWindow = IntPtr.Zero;
                
                if (!string.IsNullOrEmpty(symbolFromPrimaryWindow))
                {
                    // Ищем окно с тем же символом на втором мониторе
                    secondaryTargetWindow = MainHelper.FindWindowBySymbolOnMonitor(symbolFromPrimaryWindow, 1);
                    if (secondaryTargetWindow != IntPtr.Zero)
                    {
                        Logger.LogDebug($"Found window with symbol '{symbolFromPrimaryWindow}' on secondary monitor (Handle: 0x{secondaryTargetWindow:X})");
                    }
                    else
                    {
                        Logger.LogWarning($"No window found with symbol '{symbolFromPrimaryWindow}' on secondary monitor, using primary window");
                        // Если не нашли, используем то же окно (fallback)
                        secondaryTargetWindow = newTargetWindow;
                    }
                }
                else
                {
                    // Если не удалось извлечь символ, используем то же окно
                    Logger.LogWarning("Could not extract symbol from primary window, using same window for secondary monitor");
                    secondaryTargetWindow = newTargetWindow;
                }
                
                if (secondaryCanvasWindow != null)
                {
                    secondaryCanvasWindow.UpdateTargetWindow(secondaryTargetWindow, activeSymbol);
                    secondaryCanvasWindow.Visibility = Visibility.Visible;
                    secondaryCanvasWindow.Activate();
                    secondaryCanvasWindow.Focus();
                    Logger.LogDebug("Space pressed: Secondary Canvas window SHOWN (existing window)");
                }
                else
                {
                    OpenCanvasOnSecondaryMonitor(secondaryTargetWindow);
                    Logger.LogDebug("Space pressed: Secondary Canvas window SHOWN (new window)");
                }
            }
        }
        
        /// <summary>
        /// Открывает Canvas на основном мониторе
        /// </summary>
        private void OpenCanvasOnPrimaryMonitor(IntPtr targetWindowHandle)
        {
            try
            {
                if (!MainHelper.ValidateTargetWindow(targetWindowHandle))
                {
                    return;
                }
                
                if (currentCanvasWindow != null)
                {
                    currentCanvasWindow.Close();
                    currentCanvasWindow = null;
                }
                
                currentCanvasWindow = new CanvasWindow(targetWindowHandle, activeSymbol, 0); // 0 = primary monitor
                currentCanvasWindow.SetJForexIntegration(useJForexIntegration);
                currentCanvasWindow.Closed += (s, args) => currentCanvasWindow = null;
                
                currentCanvasWindow.Show();
                
                // Обновляем позиции тостов после создания CanvasWindow
                var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                toastService?.RefreshToastPositions();
                
                Logger.LogDebug("Primary Canvas window opened");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening primary canvas window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Открывает Canvas на втором мониторе
        /// </summary>
        private void OpenCanvasOnSecondaryMonitor(IntPtr targetWindowHandle)
        {
            try
            {
                if (!MainHelper.ValidateTargetWindow(targetWindowHandle))
                {
                    return;
                }
                
                // Проверяем, есть ли второй монитор
                var screens = Screen.AllScreens;
                if (screens.Length < 2)
                {
                    Logger.LogWarning("Secondary monitor not found - cannot open secondary canvas");
                    return;
                }
                
                if (secondaryCanvasWindow != null)
                {
                    secondaryCanvasWindow.Close();
                    secondaryCanvasWindow = null;
                }
                
                secondaryCanvasWindow = new CanvasWindow(targetWindowHandle, activeSymbol, 1); // 1 = secondary monitor
                secondaryCanvasWindow.SetJForexIntegration(useJForexIntegration);
                secondaryCanvasWindow.Closed += (s, args) => secondaryCanvasWindow = null;
                
                secondaryCanvasWindow.Show();
                
                // Обновляем позиции тостов после создания CanvasWindow
                var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                toastService?.RefreshToastPositions();
                
                Logger.LogDebug("Secondary Canvas window opened");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening secondary canvas window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsMouseOverCanvasWindow()
        {
            if (currentCanvasWindow == null || !currentCanvasWindow.IsVisible)
                return false;
            
            try
            {
                // Получаем позицию курсора мыши
                var cursorPos = System.Windows.Forms.Cursor.Position;
                
                // Получаем границы CanvasWindow
                var canvasBounds = new Rectangle(
                    (int)currentCanvasWindow.Left,
                    (int)currentCanvasWindow.Top,
                    (int)currentCanvasWindow.Width,
                    (int)currentCanvasWindow.Height
                );
                
                // Проверяем, находится ли курсор в пределах CanvasWindow
                return canvasBounds.Contains(cursorPos.X, cursorPos.Y);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking if mouse is over CanvasWindow", ex);
                return false;
            }
        }



                private void OnEscapeKeyPressed()
        {
            Logger.LogDebug("Escape key pressed - starting cleanup");
            
            // Логика обработки ценовых уровней перенесена в SimpleTradingOverlay
            // Отмена происходит через simpleTradingOverlay.OnEscapeKeyPressed()
            
            // Вызываем метод SimpleTradingOverlay для отмены паттерна
            if (simpleTradingOverlay != null)
            {
                simpleTradingOverlay.OnEscapeKeyPressed();
            }
            
            Logger.LogDebug($"Before cleanup: isCapturing={isCapturing}, overlay={(overlay == null ? "null" : "not null")}");
            
            // Check if canvas windows are open and close them
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                Logger.LogDebug("Closing primary canvas window");
                currentCanvasWindow.Close();
                currentCanvasWindow = null;
                Logger.LogDebug("Primary canvas window closed");
            }
            if (secondaryCanvasWindow != null && secondaryCanvasWindow.IsVisible)
            {
                Logger.LogDebug("Closing secondary canvas window");
                secondaryCanvasWindow.Close();
                secondaryCanvasWindow = null;
                Logger.LogDebug("Secondary canvas window closed");
            }
            
            // Check if screen capture overlay is open and close it
            if (overlay != null)
            {
                Logger.LogDebug($"Overlay exists, IsVisible={overlay.IsVisible}");
                overlay.CaptureCompleted -= OnCaptureCompleted; // Unsubscribe from event
                overlay.Close();
                overlay = null;
                isCapturing = false;
                Logger.LogDebug("Overlay closed and state reset");
            }
            else
            {
                Logger.LogDebug("No overlay to close");
            }
            
            Logger.LogDebug($"After cleanup: isCapturing={isCapturing}, overlay={(overlay == null ? "null" : "not null")}");
        }

        private void OnBackQuoteKeyPressed()
        {
            // Переключить Global Hotkeys ToggleButton
            Dispatcher.Invoke(() =>
            {
                HotkeyToggleButton.IsChecked = !(HotkeyToggleButton.IsChecked ?? false);
                if (HotkeyToggleButton.IsChecked == true)
                    EnableHotkeys();
                else
                    DisableHotkeys();
            });
        }
        
        /// <summary>
        /// Получает Canvas на мониторе, где находится курсор
        /// </summary>
        private CanvasWindow GetCanvasOnCursorMonitor()
        {
            var cursorPos = System.Windows.Forms.Cursor.Position;
            int monitorIndex = MainHelper.GetMonitorIndexByCoordinates(cursorPos.X, cursorPos.Y);
            
            Logger.LogDebug($"Cursor is on monitor {monitorIndex}");
            
            if (monitorIndex == 0)
            {
                return currentCanvasWindow;
            }
            else if (monitorIndex == 1)
            {
                return secondaryCanvasWindow;
            }
            
            // Fallback к основному монитору
            return currentCanvasWindow;
        }
        
        private void OnCKeyPressed()
        {
            Logger.LogDebug("C key pressed - clearing canvas on cursor monitor");
            var canvasOnCursorMonitor = GetCanvasOnCursorMonitor();
            if (canvasOnCursorMonitor != null && canvasOnCursorMonitor.IsVisible)
            {
                canvasOnCursorMonitor.ClearCanvas();
                Logger.LogDebug($"Canvas cleared on monitor with cursor");
            }
            else
            {
                Logger.LogDebug("No visible Canvas found on cursor monitor");
            }
        }
        
        private void OnWKeyPressed()
        {
            Logger.LogDebug("W key pressed - shifting canvas up on cursor monitor");
            var canvasOnCursorMonitor = GetCanvasOnCursorMonitor();
            if (canvasOnCursorMonitor != null && canvasOnCursorMonitor.IsVisible)
            {
                canvasOnCursorMonitor.ShiftCanvasUp();
                Logger.LogDebug($"Canvas shifted up on monitor with cursor");
            }
            else
            {
                Logger.LogDebug("No visible Canvas found on cursor monitor");
            }
        }
        
        private void OnAKeyPressed()
        {
            Logger.LogDebug("A key pressed - starting inclined pattern capture");
            
            // Также выполняем оригинальную логику для CanvasWindow
            var canvasOnCursorMonitor = GetCanvasOnCursorMonitor();
            if (canvasOnCursorMonitor != null && canvasOnCursorMonitor.IsVisible)
            {
                canvasOnCursorMonitor.ShiftCanvasLeft();
                Logger.LogDebug($"Canvas shifted left on monitor with cursor");
            }
            else
            {
                Logger.LogDebug("No visible Canvas found on cursor monitor");
            }
        }
        
        private void OnSKeyPressed()
        {
            Logger.LogDebug("S key pressed - starting trading pattern capture");
            
            // Также выполняем оригинальную логику для CanvasWindow
            // if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            // {
            //     // currentCanvasWindow.ShiftCanvasDown();
            // }
        }
        
        private void OnDKeyPressed()
        {
            Logger.LogDebug("D key pressed - shifting canvas right on cursor monitor");
            
            // Также выполняем оригинальную логику для CanvasWindow
            var canvasOnCursorMonitor = GetCanvasOnCursorMonitor();
            if (canvasOnCursorMonitor != null && canvasOnCursorMonitor.IsVisible)
            {
                canvasOnCursorMonitor.ShiftCanvasRight();
                Logger.LogDebug($"Canvas shifted right on monitor with cursor");
            }
            else
            {
                Logger.LogDebug("No visible Canvas found on cursor monitor");
            }
        }

        private void OnPKeyPressed()
        {
            Logger.LogDebug("MainWindow.OnPKeyPressed() called");
            
            // Метод зарезервирован для будущего использования
        }

        private void StartCapture_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogDebug($"StartCapture_Click called. isCapturing: {isCapturing}, overlay: {(overlay == null ? "null" : "not null")}");
            
            if (!isCapturing)
            {
                Logger.LogDebug("Starting capture...");
                StartCapture();
            }
            else
            {
                Logger.LogDebug("Capture already in progress, ignoring click");
            }
        }

        private void StartCapture()
        {
            Logger.LogDebug("StartCapture method called");
            if (!MainHelper.ValidateTargetWindow(targetWindow, "No target window selected! Hover over a window and press Space first."))
            {
                return;
            }
            
            isCapturing = true;
            Logger.LogDebug("Starting capture... Press hotkey again to stop");
            overlay = new ScreenCaptureOverlay(targetWindow, activeSymbol);
            Logger.LogDebug($"Overlay created: {overlay}");
            overlay.CaptureCompleted += OnCaptureCompleted;
            Logger.LogDebug("Event handler subscribed");
            overlay.Show();
            Logger.LogDebug("Overlay shown");
        }

        private void OnCaptureCompleted(object sender, CaptureEventArgs e)
        {
            var info = e.DebugInfo;
            Logger.LogDebug("Capture completed");
            isCapturing = false;

            if (overlay != null)
            {
                overlay.CaptureCompleted -= OnCaptureCompleted; // Unsubscribe from event
                overlay.Close();
                overlay = null;
            }
            
            // Запускаем трекинг после завершения захвата
            var captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
            if (captureTrackingService != null)
            {
                _ = Task.Run(async () => await captureTrackingService.RunOnceAsync());
            }
        }

        private void Canvas_Click(object sender, RoutedEventArgs e)
        {
            OpenCanvasOnPrimaryMonitor(targetWindow);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            StopAutoTracking();
            ServiceInitializer.CleanupServices();
            System.Windows.Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Logger.LogInfo("MainWindow closing - cleaning up resources");
            
            // Stop auto tracking
            StopAutoTracking();
            
            // Save database before closing
            var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            if (databaseService != null)
            {
                databaseService.Shutdown();
                Logger.LogInfo("DatabaseService shutdown completed");
            }
            
                    // Закрытие SimpleTradingOverlay
        if (simpleTradingOverlay != null)
        {
            simpleTradingOverlay.Close();
            simpleTradingOverlay = null;
            Logger.LogInfo("SimpleTradingOverlay closed");
        }
            
            // Cleanup services
            ServiceInitializer.CleanupServices();
            
            Logger.LogInfo("MainWindow cleanup completed");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLog();
            
            // Логируем информацию о мониторах при старте приложения
            LogMonitorInformation();
            
            // Get screen positioning using helper
            var (left, top, width, height) = MainHelper.GetScreenPositioning();

            // Set window parameters
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = CollapsedHeight;

            StartAutoTracking(); // автозапуск при открытии окна
            
            // Включаем hotkeys по умолчанию
            HotkeyToggleButton.IsChecked = true;
            EnableHotkeys();
            
            // Инициализируем статус Binary Options - Temporarily disabled
            // var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
            // if (binaryOptionsSocketService != null)
            // {
            //     PocketOptionHelper.CheckPocketOptionConnectionStatusWithUi(this, BinaryOptionsStatusIndicator, binaryOptionsSocketService);
            // }
            
            // Обновляем заголовок окна с начальными статусами
            UpdateWindowTitle();
            
            // Инициализируем индикаторы кистей
            UpdateBrushColorIndicators();
        }

        /// <summary>
        /// Логирует информацию о количестве мониторов и их координатах
        /// </summary>
        private void LogMonitorInformation()
        {
            try
            {
                int monitorCount = Screen.AllScreens.Length;
                Logger.LogInfo($"=== MONITOR INFORMATION ===");
                Logger.LogInfo($"Total monitors detected: {monitorCount}");
                
                for (int i = 0; i < monitorCount; i++)
                {
                    var screen = Screen.AllScreens[i];
                    var bounds = screen.Bounds;
                    var workingArea = screen.WorkingArea;
                    
                    Logger.LogInfo($"Monitor {i}:");
                    Logger.LogInfo($"  - Primary: {screen.Primary}");
                    Logger.LogInfo($"  - Device Name: {screen.DeviceName}");
                    Logger.LogInfo($"  - Bounds: X={bounds.X}, Y={bounds.Y}, Width={bounds.Width}, Height={bounds.Height}");
                    Logger.LogInfo($"  - Working Area: X={workingArea.X}, Y={workingArea.Y}, Width={workingArea.Width}, Height={workingArea.Height}");
                    Logger.LogInfo($"  - Left Boundary: {bounds.Left}");
                    Logger.LogInfo($"  - Top Boundary: {bounds.Top}");
                    Logger.LogInfo($"  - Right Boundary: {bounds.Right}");
                    Logger.LogInfo($"  - Bottom Boundary: {bounds.Bottom}");
                }
                
                Logger.LogInfo($"=== END MONITOR INFORMATION ===");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error logging monitor information: {ex.Message}", ex);
            }
        }

        private void LoadLog()
        {
            string logContent = Logger.GetAllLogs();
            if (!string.IsNullOrEmpty(logContent))
            {
                var lines = logContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Reverse();
                LogTextBox.Text = string.Join(Environment.NewLine, lines);
            }
            else
            {
                LogTextBox.Text = "Log file not found.";
            }
        }

        private void HotkeyToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (HotkeyToggleButton.IsChecked == true)
            {
                EnableHotkeys();
            }
            else
            {
                DisableHotkeys();
            }
        }

        private void EnableHotkeys()
        {
            var hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
            hotkeysService?.Enable();
            HotkeyToggleButton.Background = System.Windows.Media.Brushes.DarkGreen;
            HotkeyStatusIndicator.Fill = System.Windows.Media.Brushes.LimeGreen;
        }

        private void DisableHotkeys()
        {
            var hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
            hotkeysService?.Disable();
            HotkeyToggleButton.Background = System.Windows.Media.Brushes.DarkRed;
            HotkeyStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
        }

        /// <summary>
        /// Event handler for symbol button clicks
        /// </summary>
        private void SymbolButton_Click(object sender, RoutedEventArgs e)
        {
            var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
            activeSymbol = MainHelper.HandleSymbolButtonClick(sender, windowManagementService);
            UpdateSymbolIndicators(activeSymbol);
            
            // Если CanvasWindow открыты, переинициализируем их для нового символа
            if (currentCanvasWindow != null && !string.IsNullOrEmpty(activeSymbol))
            {
                // Используем новый метод для полной переинициализации
                currentCanvasWindow.ReinitializeForNewSymbol(activeSymbol);
                
                Logger.LogInfo($"Primary CanvasWindow reinitialized for new symbol: {activeSymbol}");
            }
            if (secondaryCanvasWindow != null && !string.IsNullOrEmpty(activeSymbol))
            {
                // Используем новый метод для полной переинициализации
                secondaryCanvasWindow.ReinitializeForNewSymbol(activeSymbol);
                
                Logger.LogInfo($"Secondary CanvasWindow reinitialized for new symbol: {activeSymbol}");
            }
        }

        /// <summary>
        /// Event handler for Reset DB button click
        /// </summary>
        private void ResetDB_Click(object sender, RoutedEventArgs e)
        {
            var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
            MainHelper.ResetDatabase(databaseService, windowManagementService);
            
            // Очищаем папку Trades
            ClearTradesFolder();
            
            // Show toast notification that database is reset
            var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
            toastService?.ShowToast("Database has been reset successfully!", ToastType.Success);
        }

        /// <summary>
        /// Очищает содержимое папки Trades
        /// </summary>
        private void ClearTradesFolder()
        {
            try
            {
                string tradesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Trades");
                if (Directory.Exists(tradesDir))
                {
                    var files = Directory.GetFiles(tradesDir, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            Logger.LogInfo($"Deleted file: {file}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to delete file {file}: {ex.Message}");
                        }
                    }
                    Logger.LogInfo($"Cleared Trades folder: {tradesDir}");
                }
                else
                {
                    Logger.LogInfo("Trades folder does not exist, nothing to clear");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error clearing Trades folder: {ex.Message}", ex);
            }
        }


        private async void RunCaptureTracking_Click(object sender, RoutedEventArgs e)
        {
            var captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
            if (captureTrackingService == null)
            {
                Logger.LogDebug("CaptureTrackingService не инициализирован!");
                return;
            }
            await captureTrackingService.RunOnceAsync();
        }

        private void ToggleTrackingViewer_Click(object sender, RoutedEventArgs e)
        {
            var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            ((App)System.Windows.Application.Current).ToggleTrackingViewer(databaseService);
        }

        // Обработчик события статуса подключения MT4 Socket
        private void Mt4SocketService_ConnectionStatusChanged(object sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                Mt4Helper.UpdateMt4StatusUI(this, SocketStatusIndicator, status);
                UpdateWindowTitle();
            });
        }

        private async void ReconnectMT4_Click(object sender, RoutedEventArgs e)
        {
            var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            await Mt4Helper.ReconnectMt4WithUiAsync(this, SocketStatusIndicator, mt4SocketService);
            UpdateWindowTitle();
        }

        private void AutoTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAutoTrackingActive)
            {
                StopAutoTracking();
            }
            else
            {
                StartAutoTracking();
            }
        }

        private void StartAutoTracking()
        {
            if (isAutoTrackingActive)
                return;
            isAutoTrackingActive = true;
            _autoTrackingCts = new CancellationTokenSource();
            _autoTrackingTask = Task.Run(() => AutoTrackingLoop(_autoTrackingCts.Token));
            AutoTrackingStatusIndicator.Fill = System.Windows.Media.Brushes.LimeGreen;
        }

        private void StopAutoTracking()
        {
            if (!isAutoTrackingActive)
                return;
            isAutoTrackingActive = false;
            _autoTrackingCts?.Cancel();
            _autoTrackingCts = null;
            AutoTrackingStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
        }

        private async Task AutoTrackingLoop(CancellationToken token)
        {
            var captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
            while (true)
            {
                await captureTrackingService.RunOnceAsync();
                if (token.IsCancellationRequested)
                    break;
                try { await Task.Delay(1000, token); } catch { break; }
            }
        }

        public CaptureTrackingService GetCaptureTrackingService()
        {
            return ServiceContainer.Instance.GetService<CaptureTrackingService>();
        }

        private void RefreshLogButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLog();
            if (this.Height <= CollapsedHeight + 1)
                this.Height = ExpandedHeight;
            else
                this.Height = CollapsedHeight;
        }

        // Обработчик события статуса подключения Binary Options Socket
        private void BinaryOptionsSocketService_ConnectionStatusChanged(object sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                PocketOptionHelper.UpdatePocketOptionStatusUI(this, BinaryOptionsStatusIndicator, status);
                UpdateWindowTitle();
            });
        }

        private async void ReconnectBinaryOptions_Click(object sender, RoutedEventArgs e)
        {
            // Temporarily disabled
            // var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
            // if (binaryOptionsSocketService != null)
            // {
            //     await PocketOptionHelper.ReconnectPocketOptionWithUiAsync(this, BinaryOptionsStatusIndicator, binaryOptionsSocketService);
            // }
            UpdateWindowTitle();
        }

        /// <summary>
        /// Обновляет заголовок окна с учетом статуса обоих сервисов
        /// </summary>
        private void UpdateWindowTitle()
        {
            var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            // var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>(); // Temporarily disabled
            
            string mt4Status = "⚫";
            string binaryOptionsStatus = "⚫"; // Always disconnected when service is disabled
            
            if (mt4SocketService != null && mt4SocketService.IsConnected)
                mt4Status = "🟢";
            else if (mt4SocketService != null)
                mt4Status = "🔴";
            
            // if (binaryOptionsSocketService != null && binaryOptionsSocketService.IsConnected)
            //     binaryOptionsStatus = "🟢";
            // else if (binaryOptionsSocketService != null)
            //     binaryOptionsStatus = "🔴";
                
            this.Title = $"Screen Capture Tool [MT4: {mt4Status}] [Binary Options: {binaryOptionsStatus}]";
        }

        private void ResetLogButton_Click(object sender, RoutedEventArgs e)
        {
            MainHelper.DeleteAppLogFile();
            LoadLog();
        }

        private void TriggerSymbolButton(int idx)
        {
            var symbolButtons = new[] {
                FindName("XAUUSD") as System.Windows.Controls.Button,
                FindName("GBPJPY") as System.Windows.Controls.Button,
                FindName("EURUSD") as System.Windows.Controls.Button,
                FindName("USDJPY") as System.Windows.Controls.Button,
                FindName("GBPUSD") as System.Windows.Controls.Button,
                FindName("EURJPY") as System.Windows.Controls.Button
            };
            if (idx >= 0 && idx < symbolButtons.Length && symbolButtons[idx] != null)
            {
                symbolButtons[idx].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                // activeSymbol обновится через SymbolButton_Click
            }
        }

        private void UpdateSymbolIndicators(string activeSymbol)
        {
            // Reset all indicators
            XAUUSD_ActiveIndicator.Visibility = Visibility.Collapsed;
            GBPJPY_ActiveIndicator.Visibility = Visibility.Collapsed;
            EURUSD_ActiveIndicator.Visibility = Visibility.Collapsed;
            USDJPY_ActiveIndicator.Visibility = Visibility.Collapsed;
            GBPUSD_ActiveIndicator.Visibility = Visibility.Collapsed;
            EURJPY_ActiveIndicator.Visibility = Visibility.Collapsed;

            // Show active indicator
            if (!string.IsNullOrEmpty(activeSymbol))
            {
                switch (activeSymbol)
                {
                    case "XAUUSD":
                        XAUUSD_ActiveIndicator.Visibility = Visibility.Visible;
                        break;
                    case "GBPJPY":
                        GBPJPY_ActiveIndicator.Visibility = Visibility.Visible;
                        break;
                    case "EURUSD":
                        EURUSD_ActiveIndicator.Visibility = Visibility.Visible;
                        break;
                    case "USDJPY":
                        USDJPY_ActiveIndicator.Visibility = Visibility.Visible;
                        break;
                    case "GBPUSD":
                        GBPUSD_ActiveIndicator.Visibility = Visibility.Visible;
                        break;
                    case "EURJPY":
                        EURJPY_ActiveIndicator.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void YellowBrushButton_Click(object sender, RoutedEventArgs e)
        {
            isYellowBrush = true;
            UpdateBrushColorIndicators();
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
                currentCanvasWindow.UpdateSimpleBrushColor(isYellowBrush);
            if (secondaryCanvasWindow != null && secondaryCanvasWindow.IsVisible)
                secondaryCanvasWindow.UpdateSimpleBrushColor(isYellowBrush);
            Logger.LogInfo("Brush color set to Yellow");
        }

        private void BlackBrushButton_Click(object sender, RoutedEventArgs e)
        {
            isYellowBrush = false;
            UpdateBrushColorIndicators();
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
                currentCanvasWindow.UpdateSimpleBrushColor(isYellowBrush);
            if (secondaryCanvasWindow != null && secondaryCanvasWindow.IsVisible)
                secondaryCanvasWindow.UpdateSimpleBrushColor(isYellowBrush);
        }

        private void UpdateBrushColorIndicators()
        {
            if (isYellowBrush)
            {
                YellowBrushIndicator.StrokeThickness = 4;
                BlackBrushIndicator.StrokeThickness = 2;
            }
            else
            {
                YellowBrushIndicator.StrokeThickness = 2;
                BlackBrushIndicator.StrokeThickness = 4;
            }
        }

        private void Mt4SocketService_OrdersSummaryReceived(object sender, Mt4SocketService.OrdersSummary summary)
        {
            Dispatcher.Invoke(() =>
            {
                SymbolSummaryPanel.Children.Clear();
                if (summary?.Symbols != null && summary.Symbols.Count > 0)
                {
                    foreach (var symbol in summary.Symbols)
                    {
                        var orderSymbol = new ScreenCaptureApp.Controls.OrderSymbol
                        {
                            Symbol = symbol.Symbol,
                            Lots = symbol.Lots,
                            Percent = symbol.Percent,
                            Points = symbol.ProfitPoints
                        };
                        orderSymbol.CloseClicked += (sym) => CloseSymbolOrder(sym);
                        orderSymbol.BEClicked += (sym) => OnBEClick(sym);
                        orderSymbol.TP1Clicked += (sym) => OnTP1Click(sym);
                        orderSymbol.TP2Clicked += (sym) => OnTP2Click(sym);
                        orderSymbol.TP3Clicked += (sym) => OnTP3Click(sym);
                        SymbolSummaryPanel.Children.Add(orderSymbol);
                    }
                }
                TotalBalanceText.Text = summary != null ? $"$ {summary.TotalBalance:F2}" : string.Empty;
            });
        }

        private void CloseSymbolOrder(string symbol)
        {
            var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            if (mt4SocketService != null && !string.IsNullOrEmpty(symbol))
            {
                // Send close_positions command for the symbol
                var cmd = $"{{\"cmd\":\"close_positions\",\"symbol\":\"{symbol}\"}}\r\n";
                _ = mt4SocketService.WriteAsync(cmd);
            }
        }

        private void OnBEClick(string symbol) { Logger.LogInfo($"BE clicked for {symbol}"); }
        private void OnTP1Click(string symbol) { Logger.LogInfo($"TP1 clicked for {symbol}"); }
        private void OnTP2Click(string symbol) { Logger.LogInfo($"TP2 clicked for {symbol}"); }
        private void OnTP3Click(string symbol) { Logger.LogInfo($"TP3 clicked for {symbol}"); }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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

        private void ClickHotkeyButtonByIndex(int index)
        {
            // Сохраняем текущую позицию мыши
            GetCursorPos(out POINT oldPos);
            // Проверяем, что targetWindow активен и находится на основном мониторе (X=0)
            targetWindow = MainHelper.GetWindowUnderCursor();
            int x = 80 + 200 * index;
            int y = 56; // Y задаёт пользователь
            // Отправляем клик мышкой по окну
            var jforexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
            if (jforexService != null)
            {
                jforexService.ClickAtPosition(targetWindow, x, y);
                Logger.LogInfo($"Hotkey button index {index} clicked at ({x},{y}) on window {targetWindow}");
            }
            // Возвращаем курсор на прежнее место
            SetCursorPos(oldPos.X, oldPos.Y);
            Logger.LogInfo($"[DEBUG] Mouse returned to ({oldPos.X},{oldPos.Y})");
        }

        private void CloseEllipse_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void MinimizeEllipse_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        /// <summary>
        /// Переключает режим интеграции с JForex
        /// </summary>
        public void SetJForexIntegration(bool enabled)
        {
            useJForexIntegration = enabled;
            Logger.LogInfo($"MainWindow: JForex integration {(enabled ? "enabled" : "disabled")}");
            
            // Обновляем настройку в текущих CanvasWindow если они открыты
            if (currentCanvasWindow != null)
            {
                currentCanvasWindow.SetJForexIntegration(enabled);
            }
            if (secondaryCanvasWindow != null)
            {
                secondaryCanvasWindow.SetJForexIntegration(enabled);
            }
        }
        
        /// <summary>
        /// Получает текущее состояние интеграции с JForex
        /// </summary>
        public bool GetJForexIntegration()
        {
            return useJForexIntegration;
        }
        
        /// <summary>
        /// Управляет показом Trading Toolbar в SimpleTradingOverlay
        /// </summary>
        public void SetShowTradingToolbar(bool enabled)
        {
            showTradingToolbar = enabled;
            if (simpleTradingOverlay != null)
            {
                simpleTradingOverlay.SetTradingToolbarEnabled(enabled);
            }
            Logger.LogInfo($"Trading Toolbar visibility set to {(enabled ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Переключает режим интеграции с JForex
        /// </summary>
        private void ToggleJForexIntegration()
        {
            useJForexIntegration = !useJForexIntegration;
            Logger.LogInfo($"JForex integration toggled: {(useJForexIntegration ? "enabled" : "disabled")}");
            
            // Обновляем настройку в текущих CanvasWindow если они открыты
            if (currentCanvasWindow != null)
            {
                currentCanvasWindow.SetJForexIntegration(useJForexIntegration);
            }
            if (secondaryCanvasWindow != null)
            {
                secondaryCanvasWindow.SetJForexIntegration(useJForexIntegration);
            }
            
            // Показываем уведомление
            var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
            toastService?.ShowToast($"JForex integration: {(useJForexIntegration ? "ON" : "OFF")}", 
                useJForexIntegration ? ToastType.Success : ToastType.Info);
        }
    }
} 