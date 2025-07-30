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
        private string activeSymbol = null;
        private CancellationTokenSource _autoTrackingCts;
        private Task _autoTrackingTask;
        private bool isAutoTrackingActive = false;
        private const double CollapsedHeight = 55;
        private const double ExpandedHeight = 600;
        
        // Brush color state
        private bool isYellowBrush = true; // true = yellow, false = black - default to yellow for SimpleMod
        
        // Public property to access brush color state
        public bool IsYellowBrush => isYellowBrush;

        public MainWindow()
        {
            ServiceInitializer.RegisterAllServices();
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            InitializeServices();

            StartAutoTracking();

            var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            if (mt4SocketService != null)
            {
                mt4SocketService.ConnectionStatusChanged += Mt4SocketService_ConnectionStatusChanged;
                mt4SocketService.OrdersSummaryReceived += Mt4SocketService_OrdersSummaryReceived;
            }

            var captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
            if (mt4SocketService != null && captureTrackingService != null)
                mt4SocketService.SubscribeToCaptureTrackingEvents(captureTrackingService);

            var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
            if (binaryOptionsSocketService != null && captureTrackingService != null)
                binaryOptionsSocketService.SubscribeToCaptureTrackingEvents(captureTrackingService);

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
                hotkeysService.OnQHotkey += () => { Logger.LogInfo("[DEBUG] OnQHotkey event in MainWindow"); ClickHotkeyButtonByIndex(0); };
                hotkeysService.OnWHotkey += () => { Logger.LogInfo("[DEBUG] OnWHotkey event in MainWindow"); ClickHotkeyButtonByIndex(1); };
                hotkeysService.OnEHotkey += () => { Logger.LogInfo("[DEBUG] OnEHotkey event in MainWindow"); ClickHotkeyButtonByIndex(2); };
                hotkeysService.OnRHotkey += () => { Logger.LogInfo("[DEBUG] OnRHotkey event in MainWindow"); ClickHotkeyButtonByIndex(3); };
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
                // Initialize services using helper
                            ServiceInitializer.InitializeHotkeysService(
                Dispatcher,
                OnSpaceKeyPressed,
                OnTKeyPressed,
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
            // Проверяем, находится ли курсор мыши над CanvasWindow
            if (IsMouseOverCanvasWindow())
            {
                // Если курсор над CanvasWindow — переключаем Trading Mode
                if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
                {
                    currentCanvasWindow.ToggleTradingMode();
                    Logger.LogDebug("Space pressed over CanvasWindow: toggled trading mode");
                }
                return;
            }
            
            // Если курсор не над CanvasWindow — обновляем target window
            IntPtr newTargetWindow = MainHelper.GetWindowUnderCursor();
            
            // Show status information
            if (newTargetWindow != IntPtr.Zero)
            {
                Logger.LogDebug($"Target window captured (Handle: 0x{newTargetWindow:X})");
            }
            else
            {
                Logger.LogDebug("No target window found under cursor");
                return;
            }
            
            // Если CanvasWindow уже открыт, обновляем его target window
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                currentCanvasWindow.UpdateTargetWindow(newTargetWindow, activeSymbol);
                targetWindow = newTargetWindow;
                Logger.LogDebug("CanvasWindow target updated");
            }
            else
            {
                // Если CanvasWindow не открыт, открываем новый
                targetWindow = newTargetWindow;
                Canvas_Click(null, null);
                Logger.LogDebug("New CanvasWindow opened");
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

        private void OnTKeyPressed()
        {
            // Get the window handle under the current mouse cursor
            targetWindow = MainHelper.GetWindowUnderCursor();

            // Show status information
            if (targetWindow != IntPtr.Zero)
            {
                Logger.LogDebug($"Target window captured (Handle: 0x{targetWindow:X})");
            }
            else
            {
                Logger.LogDebug("No target window found under cursor");
            }

            Logger.LogDebug("T key pressed - starting capture");
            StartCapture_Click(null, null);
        }

        private void OnEscapeKeyPressed()
        {
            Logger.LogDebug("Escape key pressed - starting cleanup");
            
            // Вызываем метод SimpleTradingOverlay для отмены паттерна
            if (simpleTradingOverlay != null)
            {
                simpleTradingOverlay.OnEscapeKeyPressed();
            }
            
            Logger.LogDebug($"Before cleanup: isCapturing={isCapturing}, overlay={(overlay == null ? "null" : "not null")}");
            
            // Check if canvas window is open and close it
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                Logger.LogDebug("Closing canvas window");
                currentCanvasWindow.Close();
                currentCanvasWindow = null;
                Logger.LogDebug("Canvas window closed");
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
        
        private void OnCKeyPressed()
        {
            Logger.LogDebug("C key pressed - clearing canvas");
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                currentCanvasWindow.ClearCanvas();
            }
        }
        
        private void OnWKeyPressed()
        {
            Logger.LogDebug("W key pressed - shifting canvas up");
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                currentCanvasWindow.ShiftCanvasUp();
            }
        }
        
        private void OnAKeyPressed()
        {
            Logger.LogDebug("A key pressed - shifting canvas left");
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                currentCanvasWindow.ShiftCanvasLeft();
            }
        }
        
        private void OnSKeyPressed()
        {
            
            // Вызываем метод SimpleTradingOverlay для паттерна трейдинга
            if (simpleTradingOverlay != null)
            {
                simpleTradingOverlay.OnSKeyPressed();
            }
            else
            {
                Logger.LogWarning("SimpleTradingOverlay is null, cannot call OnSKeyPressed");
            }
            
            // Также выполняем оригинальную логику для CanvasWindow
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                currentCanvasWindow.ShiftCanvasDown();
            }
        }
        
        private void OnDKeyPressed()
        {
            Logger.LogDebug("D key pressed - shifting canvas right");
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
            {
                currentCanvasWindow.ShiftCanvasRight();
            }
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
            try
            {
                if (!MainHelper.ValidateTargetWindow(targetWindow))
                {
                    return;
                }
                
                if (currentCanvasWindow != null)
                {
                    currentCanvasWindow.Close();
                    currentCanvasWindow = null;
                }
                currentCanvasWindow = new CanvasWindow(targetWindow, activeSymbol);
                currentCanvasWindow.Closed += (s, args) => currentCanvasWindow = null;
                
                // Subscribe to the trendline drawn event to trigger space hotkey functionality
                currentCanvasWindow.OnTrendlineDrawn += () =>
                {
                    Logger.LogInfo("Trendline drawn event received - triggering space hotkey functionality");
                    // Add a small delay to ensure the JForex window is stable
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            OnSpaceKeyPressed();
                        });
                    });
                };
                
                currentCanvasWindow.Show();
                
                // Обновляем позиции тостов после создания CanvasWindow
                var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
                toastService?.RefreshToastPositions();
                
                Logger.LogDebug("Canvas window opened");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening canvas window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            
            // Обновляем заголовок окна с начальными статусами
            UpdateWindowTitle();
            
            // Инициализируем индикаторы кистей
            UpdateBrushColorIndicators();
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
            
            // Если CanvasWindow открыт, переинициализируем его для нового символа
            if (currentCanvasWindow != null && !string.IsNullOrEmpty(activeSymbol))
            {
                // Используем новый метод для полной переинициализации
                currentCanvasWindow.ReinitializeForNewSymbol(activeSymbol);
                
                Logger.LogInfo($"CanvasWindow reinitialized for new symbol: {activeSymbol}");
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
            
            // Show toast notification that database is reset
            var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
            toastService?.ShowToast("Database has been reset successfully!", ToastType.Success);
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
            var binaryOptionsSocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
            if (binaryOptionsSocketService != null)
            {
                await binaryOptionsSocketService.ConnectAsync();
            }
            UpdateWindowTitle();
        }

        /// <summary>
        /// Обновляет заголовок окна с учетом статуса обоих сервисов
        /// </summary>
        private void UpdateWindowTitle()
        {
            var mt4SocketService = ServiceContainer.Instance.GetService<Mt4SocketService>();
            
            string mt4Status = "⚫";
            string binaryOptionsStatus = "⚫";
            
            if (mt4SocketService != null && mt4SocketService.IsConnected)
                mt4Status = "🟢";
            else if (mt4SocketService != null)
                mt4Status = "🔴";
                
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
            Logger.LogInfo("Brush color set to Yellow");
        }

        private void BlackBrushButton_Click(object sender, RoutedEventArgs e)
        {
            isYellowBrush = false;
            UpdateBrushColorIndicators();
            if (currentCanvasWindow != null && currentCanvasWindow.IsVisible)
                currentCanvasWindow.UpdateSimpleBrushColor(isYellowBrush);
            Logger.LogInfo("Brush color set to Black");
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

        private struct POINT
        {
            public int X;
            public int Y;
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
    }
} 