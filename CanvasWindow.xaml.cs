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

namespace ScreenCaptureApp
{
    public partial class CanvasWindow : Window
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
        
        // Canvas shift variables
        private double canvasOffsetX = 0;
        private double canvasOffsetY = 0;
        private const double SHIFT_STEP = 4.0; // 10 zpixels for W/S, 5 pixels for A/D
        
        // MACD area threshold - strokes below this Y coordinate are considered trading strokes
        private const double MACD_AREA_THRESHOLD = 756.0;



        private ToolbarSettingsManager _toolbarSettingsManager;

        public CanvasWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += CanvasWindow_PreviewKeyDown;
            this.MouseDown += CanvasWindow_MouseDown;
            this.Activated += CanvasWindow_Activated;
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
        }

        public CanvasWindow(IntPtr targetWindow, string symbol = null)
        {
            InitializeComponent();
            this.PreviewKeyDown += CanvasWindow_PreviewKeyDown;
            this.targetWindowHandle = targetWindow;
            this.activeSymbol = symbol;
            
            // Инициализируем сервисы
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
        }

        private void CanvasWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Logger.LogInfo("Pressed Escape: closing window");
                this.Close();
            }
            else if (e.Key == Key.C)
            {
                Logger.LogInfo("Pressed C: clear canvas and delete files");
                DrawingCanvas.Strokes.Clear();
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
            DrawingCanvas.Strokes.Clear();
            BackgroundImage.Source = null;
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
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo($"Window loaded for handler {targetWindowHandle.ToInt64()}");
            SetFullScreen();
            SetBackgroundImage();
            backgroundUpdateTimer = new DispatcherTimer();
            backgroundUpdateTimer.Interval = TimeSpan.FromSeconds(BackgroundUpdateIntervalSeconds);
            backgroundUpdateTimer.Tick += (s, args) => SetBackgroundImage();
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
            DrawingCanvas.StrokeCollected += DrawingCanvas_StrokeCollected;
            
            // Subscribe to stroke erasing events
            DrawingCanvas.StrokeErasing += DrawingCanvas_StrokeErasing;

            // Initialize broker state
            InitializeBrokerComboBox();
            UpdateTradingToolbarVisibility();
            
            // Обновляем отображение активного символа
            UpdateActiveSymbolDisplay();
            
            // Устанавливаем фокус на InkCanvas
            DrawingCanvas.Focus();
            
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
            // Настройка DrawingCanvas для обычного рисования
            DrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
            
            // Получаем цвет кисти по умолчанию из MainWindow
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            bool isYellowBrush = false; // default to white
            if (mainWindow != null)
            {
                isYellowBrush = mainWindow.IsYellowBrush;
            }
            
            DrawingCanvas.DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes
            {
                Color = isYellowBrush ? Colors.Yellow : Colors.White,
                Width = 2,
                Height = 2,
                FitToCurve = true,
                IgnorePressure = false,
                IsHighlighter = false,
                StylusTip = StylusTip.Ellipse
            };
            
            // Убеждаемся, что InkCanvas может принимать ввод
            DrawingCanvas.IsHitTestVisible = true;
            DrawingCanvas.IsEnabled = true;
            
            // Добавляем обработчики событий для отладки
            DrawingCanvas.MouseDown += DrawingCanvas_MouseDown;
            DrawingCanvas.MouseMove += DrawingCanvas_MouseMove;
            DrawingCanvas.MouseUp += DrawingCanvas_MouseUp;
            
            Logger.LogInfo($"InkCanvas initialized successfully with {(isYellowBrush ? "Yellow" : "Black")} brush for DrawingCanvas");
        }

        private void DrawingCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Logger.LogInfo($"Mouse down on InkCanvas at {e.GetPosition(DrawingCanvas)}");
        }

        private void DrawingCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Логируем только при нажатой кнопке мыши
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                Logger.LogInfo($"Mouse move on InkCanvas at {e.GetPosition(DrawingCanvas)}");
            }
        }

        private void DrawingCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Logger.LogInfo($"Mouse up on InkCanvas at {e.GetPosition(DrawingCanvas)}");
        }

        private void SetFullScreen()
        {
            // Fallback to primary screen if no target window
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            
            this.Left = screen.Bounds.Left;
            this.Top = screen.Bounds.Top;
            this.Width = screen.Bounds.Width;
            this.Height = screen.Bounds.Height;
        }

        private void SetBackgroundImage()
        {
            try
            {
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
                    BackgroundImage.Source = bitmapSource;
                    
                    secondsLeft = BackgroundUpdateIntervalSeconds;
                    TimerText.Text = $"Next update in {secondsLeft}s";
                }
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

        private string GetCanvasFileBase()
        {
            string handlerStr = targetWindowHandle.ToInt64().ToString();
            string canvasesDir = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Canvases");
            Directory.CreateDirectory(canvasesDir);
            
            // Add "trading_" prefix for trading_canvas source to match CaptureTrackingService expectations
            string fileName = useJForexIntegration ? handlerStr : $"trading_{handlerStr}";
            return Path.Combine(canvasesDir, fileName);
        }



        private void SaveCanvas()
        {
            try
            {
                var fileBase = GetCanvasFileBase();
                var xamlFile = fileBase + ".xaml";
                var pngFile = fileBase + ".png";

                // Сохраняем все штрихи
                var xaml = XamlWriter.Save(DrawingCanvas.Strokes);
                File.WriteAllText(xamlFile, xaml);

                // Save as PNG
                var rtb = new RenderTargetBitmap((int)DrawingCanvas.ActualWidth, (int)DrawingCanvas.ActualHeight, 96d, 96d, PixelFormats.Pbgra32);
                DrawingCanvas.Measure(new System.Windows.Size(DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight));
                DrawingCanvas.Arrange(new Rect(0, 0, DrawingCanvas.ActualWidth, DrawingCanvas.ActualHeight));
                rtb.Render(DrawingCanvas);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = new FileStream(pngFile, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                Logger.LogInfo($"Canvas saved: {xamlFile}, {pngFile}");
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




        private void LoadCanvas()
        {
            try
            {
                string basePath = GetCanvasFileBase();
                StrokeCollection baseStrokes = new StrokeCollection();
                if (File.Exists(basePath + ".xaml"))
                {
                    string xaml = File.ReadAllText(basePath + ".xaml");
                    baseStrokes = (StrokeCollection)XamlReader.Parse(xaml);
                }
                DrawingCanvas.Strokes = baseStrokes;
                Logger.LogInfo($"Canvas loaded: {basePath}.xaml");
            }
            catch (Exception ex) { Logger.LogError("LoadCanvas error", ex); }
        }

        private void DeleteCanvasFiles()
        {
            try
            {
                string basePath = GetCanvasFileBase();
                if (File.Exists(basePath + ".png")) { File.Delete(basePath + ".png"); Logger.LogInfo($"Deleted: {basePath}.png"); }
                if (File.Exists(basePath + ".xaml")) { File.Delete(basePath + ".xaml"); Logger.LogInfo($"Deleted: {basePath}.xaml"); }
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
            // Always use DrawingCanvas in simple mode
            DrawingCanvas.Visibility = Visibility.Visible;
            
            // Get current brush color from MainWindow
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            bool isYellowBrush = false; // default to white
            if (mainWindow != null)
            {
                isYellowBrush = mainWindow.IsYellowBrush;
            }
            
            // Set brush color based on MainWindow state
            if (isYellowBrush)
            {
                DrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            }
            else
            {
                DrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
            }
            // Border always yellow in simple mode
            CanvasBorder.Stroke = new SolidColorBrush(Colors.Yellow);
            CanvasBorder.StrokeThickness = 4;
            DrawingCanvas.DefaultDrawingAttributes.Width = 2;
            DrawingCanvas.DefaultDrawingAttributes.Height = 2;
            DrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
        
            // Устанавливаем фокус на DrawingCanvas
            DrawingCanvas.IsHitTestVisible = true;
            DrawingCanvas.IsEnabled = true;
            DrawingCanvas.Focus();
            
            Logger.LogInfo($"Brush mode updated: Simple mode");
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
            // Always update brush color in simple mode
            if (isYellow)
            {
                DrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
            }
            else
            {
                DrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
            }
            // Border always yellow in simple mode
            CanvasBorder.Stroke = new SolidColorBrush(Colors.Yellow);
            CanvasBorder.StrokeThickness = 4;
            Logger.LogInfo($"Simple brush color updated to {(isYellow ? "Yellow" : "White")}");
        }

        public void UpdateTargetWindow(IntPtr newTargetWindow, string symbol = null)
        {
            Logger.LogInfo($"Updating target window from {targetWindowHandle.ToInt64()} to {newTargetWindow.ToInt64()}");
            SaveCanvasProperly();
            targetWindowHandle = newTargetWindow;
            if (!string.IsNullOrEmpty(symbol))
            {
                activeSymbol = symbol;
                // (Удалено: SymbolSettingsManager)
            }
            UpdateActiveSymbolDisplay();
            DrawingCanvas.Strokes.Clear();
            LoadCanvas();
            SetBackgroundImage();
            DrawingCanvas.Focus();
            Logger.LogInfo($"Target window updated successfully");
        }

        private void DrawingCanvas_StrokeCollected(object sender, System.Windows.Controls.InkCanvasStrokeCollectedEventArgs e)
        {
            Logger.LogInfo("Stroke collected - checking if it's a trading stroke");
            
            // Check if this stroke is in MACD area (trading stroke)
            var bounds = e.Stroke.GetBounds();
            bool isTradingStroke = bounds.Top >= MACD_AREA_THRESHOLD && bounds.Bottom >= MACD_AREA_THRESHOLD;
            
            if (isTradingStroke)
            {
                Logger.LogInfo("Trading stroke detected (MACD area) - processing immediately");
                ProcessTradingStrokeDirectly(e.Stroke);
                
                // Keep the stroke on DrawingCanvas after processing
                Logger.LogInfo("Trading stroke kept on Canvas");
            }
            else
            {
                Logger.LogInfo("Simple stroke detected - saving normally");
                // Handle stroke completion for simple mode
                var args = new StrokeCompletedEventArgs
                {
                    Stroke = e.Stroke,
                    IsTradingMode = false,
                    Bounds = bounds
                };
                
                StrokeCompleted?.Invoke(this, args);
                SaveCanvasProperly();
            }
        }
        
        
        private void DrawingCanvas_StrokeErasing(object sender, System.Windows.Controls.InkCanvasStrokeErasingEventArgs e)
        {
            // Always allow stroke erasing in simple mode
            e.Cancel = false;
            Logger.LogInfo("Stroke erasing allowed");
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
                }

                // Сохраняем canvas сразу после создания торгового штриха
                // чтобы Tracking-сервис мог его найти
                SaveCanvas();
                Logger.LogTagInfo("Trading", "Canvas saved immediately for Tracking service");

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

                // Конвертируем координаты из DrawingCanvas в экранные координаты с учетом смещения canvas
                var firstScreenPoint = DrawingCanvas.PointToScreen(new System.Windows.Point(firstPoint.X - canvasOffsetX, firstPoint.Y - canvasOffsetY));
                var lastScreenPoint = DrawingCanvas.PointToScreen(new System.Windows.Point(lastPoint.X - canvasOffsetX, lastPoint.Y - canvasOffsetY));

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
                    if (BackgroundImage.Source != null)
                    {
                        var bg = new System.Windows.Controls.Image
                        {
                            Source = BackgroundImage.Source,
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
                    foreach (var stroke in DrawingCanvas.Strokes)
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

        private void CanvasWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // При клике на окно устанавливаем фокус на InkCanvas
            DrawingCanvas.Focus();
            Logger.LogInfo("Window mouse down - focus set to InkCanvas");
        }

        private void CanvasWindow_Activated(object sender, EventArgs e)
        {
            // При активации окна устанавливаем фокус на InkCanvas
            DrawingCanvas.Focus();
            Logger.LogInfo("Window activated - focus set to InkCanvas");
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
            
            // Сдвигаем все штрихи в DrawingCanvas
            var shiftedStrokes = new StrokeCollection();
            foreach (var stroke in DrawingCanvas.Strokes)
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
            DrawingCanvas.Strokes = shiftedStrokes;
            
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
            DrawingCanvas.Strokes.Clear();
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
            // (Удалено: SymbolSettingsManager)
            // Очищаем canvas и загружаем новый
            DrawingCanvas.Strokes.Clear();
            LoadCanvas();
            SetBackgroundImage();
            DrawingCanvas.Focus();
            Logger.LogInfo($"CanvasWindow reinitialized successfully for symbol {newSymbol}");
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
        /// Обработчик события нового бара от HttpServerService
        /// </summary>
        private void OnNewBarReceived(object sender, dynamic newBarData)
        {
            try
            {
                if (newBarData != null)
                {
                    // Безопасно получаем символ из dynamic объекта
                    string newBarSymbol = null;
                    try
                    {
                        newBarSymbol = newBarData.symbol?.ToString();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error extracting symbol from NewBar data: {ex.Message}");
                    }

                    if (!string.IsNullOrEmpty(newBarSymbol))
                    {
                        // Проверяем, совпадает ли символ с активным символом CanvasWindow
                        if (!string.IsNullOrEmpty(activeSymbol) && activeSymbol.Equals(newBarSymbol, StringComparison.OrdinalIgnoreCase))
                        {
                            // Дополнительная проверка заголовка окна для тиковых баров
                            string feedType = null;
                            try
                            {
                                feedType = newBarData.feedType?.ToString();
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Error extracting feedType from NewBar data: {ex.Message}");
                            }

                            bool shouldShift = true;
                            
                            if (!string.IsNullOrEmpty(feedType) && feedType.Equals("TICK_BAR", StringComparison.OrdinalIgnoreCase))
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
                                Logger.LogInfo($"NewBar received for matching symbol '{newBarSymbol}' - shifting strokes left by {CanvasConstants.NEW_BAR_SHIFT_AMOUNT} pixels");
                                
                                Dispatcher.Invoke(() => {
                                    ShiftStrokes(-CanvasConstants.NEW_BAR_SHIFT_AMOUNT, 0);
                                    
                                    // Сохраняем canvas после сдвига
                                    SaveCanvasProperly();
                                });
                                
                                Logger.LogInfo($"Strokes shifted left for symbol '{newBarSymbol}' due to new bar");
                            }
                        }
                        else
                        {
                            Logger.LogDebug($"NewBar symbol '{newBarSymbol}' does not match active symbol '{activeSymbol}' - ignoring");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("Received NewBar event with null or empty symbol");
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
    
    public class StrokeCompletedEventArgs : EventArgs
    {
        public Stroke Stroke { get; set; }
        public bool IsTradingMode { get; set; }
        public System.Windows.Rect Bounds { get; set; }
    }
} 