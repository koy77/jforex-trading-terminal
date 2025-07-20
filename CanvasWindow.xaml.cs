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
        private const int BackgroundUpdateIntervalSeconds = 5; // match your backgroundUpdateTimer interval
        
        // Brush mode variables
        private bool isTradingDrawingMode = false; // false = yellow (simple), true = trading (white)
        private const string TRADING_MODE = "T";
        private const string SIMPLE_MODE = "S";
        
        // Event for stroke completion
        public event EventHandler<StrokeCompletedEventArgs> StrokeCompleted;

        private string activeSymbol;
        private double selectedRisk = 1.0; // по умолчанию
        private int selectedDuration = 2; // по умолчанию

        // Новая система очереди для Trading Mode
        private Queue<TradingStrokeItem> tradingStrokeQueue = new Queue<TradingStrokeItem>();
        private bool isProcessingTradingQueue = false;
        
        // Удаляем старую переменную tradingStrokes
        // private StrokeCollection tradingStrokes = new StrokeCollection();

        private BrokerState brokerState;
        private DurationState durationState;
        private JForexWindowsManagerService jForexService;
        
        // Canvas shift variables
        private double canvasOffsetX = 0;
        private double canvasOffsetY = 0;
        private const double SHIFT_STEP = 10.0; // 10 pixels for W/S, 5 pixels for A/D

        // Класс для элементов очереди торговых штрихов
        private class TradingStrokeItem
        {
            public Stroke Stroke { get; set; }
            public CaptureData CaptureData { get; set; }
            public DateTime CreatedAt { get; set; }
        }

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
            
            // Подписка на события TradingToolbar
            TradingToolbar.RiskChanged += (risk) => { selectedRisk = risk; };
            TradingToolbar.BrokerChanged += (broker) => {
                brokerState.CurrentBroker = broker;
                UpdateTradingToolbarUiByBroker(broker);
                
                // Применяем настройки брокера из DI Container
                var brokerSettingsManager = ServiceContainer.Instance.GetService<BrokerSettingsManager>();
                if (brokerSettingsManager != null)
                {
                    selectedRisk = brokerSettingsManager.GetDefaultRisk(broker);
                    selectedDuration = brokerSettingsManager.GetDefaultDuration(broker);
                    
                    // Применяем настройки к UI
                    HighlightSelectedRiskButton(selectedRisk);
                    HighlightSelectedDurationButton(selectedDuration);
                    
                    Logger.LogInfo($"Applied broker defaults for {broker}: Risk={selectedRisk}, Duration={selectedDuration}");
                }
            };
            TradingToolbar.DurationChanged += (duration) => {
                durationState.CurrentDuration = duration;
                selectedDuration = duration;
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
            
            // Подписка на события TradingToolbar
            TradingToolbar.RiskChanged += (risk) => { selectedRisk = risk; };
            TradingToolbar.BrokerChanged += (broker) => {
                brokerState.CurrentBroker = broker;
                UpdateTradingToolbarUiByBroker(broker);
                
                // Применяем настройки брокера из DI Container
                var brokerSettingsManager = ServiceContainer.Instance.GetService<BrokerSettingsManager>();
                if (brokerSettingsManager != null)
                {
                    selectedRisk = brokerSettingsManager.GetDefaultRisk(broker);
                    selectedDuration = brokerSettingsManager.GetDefaultDuration(broker);
                    
                    // Применяем настройки к UI
                    HighlightSelectedRiskButton(selectedRisk);
                    HighlightSelectedDurationButton(selectedDuration);
                    
                    Logger.LogInfo($"Applied broker defaults for {broker}: Risk={selectedRisk}, Duration={selectedDuration}");
                }
            };
            TradingToolbar.DurationChanged += (duration) => {
                durationState.CurrentDuration = duration;
                selectedDuration = duration;
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
                // Очищаем очередь торговых штрихов
                tradingStrokeQueue.Clear();
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
                    Logger.LogError("Error marking trading_canvas captures as skipped after canvas clear", ex);
                }
            }
            else if (e.Key == Key.T)
            {
                Logger.LogInfo("Pressed T: toggle trading mode");
                ToggleTradingMode();
            }
            else if (e.Key == Key.Space)
            {
                Logger.LogInfo("Pressed Space: force focus to InkCanvas");
                DrawingCanvas.Focus();
            }
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
            isTradingDrawingMode = false;
            UpdateBrushMode();
            UpdateTradingMode();
            
            // Subscribe to stroke completion events
            DrawingCanvas.StrokeCollected += DrawingCanvas_StrokeCollected;

            // --- Установить риск из Symbol ---
            if (!string.IsNullOrEmpty(activeSymbol))
            {
                var symbolSettingsManager = ServiceContainer.Instance.GetService<SymbolSettingsManager>();
                if (symbolSettingsManager != null)
                {
                    // Получаем DefaultRisk и DefaultDuration из DI Container
                    selectedRisk = symbolSettingsManager.GetDefaultRisk(activeSymbol);
                    selectedDuration = symbolSettingsManager.GetDefaultDuration(activeSymbol);
                    
                    // Применяем настройки к UI
                    HighlightSelectedRiskButton(selectedRisk);
                    HighlightSelectedDurationButton(selectedDuration);
                    
                    Logger.LogInfo($"Applied symbol defaults for {activeSymbol}: Risk={selectedRisk}, Duration={selectedDuration}");
                }
                else
                {
                    // Fallback к старому методу, если SymbolSettingsManager недоступен
                    var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                    var symbol = databaseService.GetSymbolByName(activeSymbol);
                    if (symbol != null)
                    {
                        selectedRisk = symbol.RiskPercent;
                        HighlightSelectedRiskButton(selectedRisk);
                    }
                }
            }

            // Initialize broker state
            InitializeBrokerComboBox();
            UpdateTradingToolbarVisibility();
            
            // Обновляем отображение активного символа
            UpdateActiveSymbolDisplay();
            
            // Устанавливаем фокус на InkCanvas
            DrawingCanvas.Focus();
        }

        private void InitializeInkCanvas()
        {
            // Настройка InkCanvas для рисования
            DrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
            
            // Получаем цвет кисти по умолчанию из MainWindow
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            bool isYellowBrush = true; // default to yellow
            if (mainWindow != null)
            {
                isYellowBrush = mainWindow.IsYellowBrush;
            }
            
            DrawingCanvas.DefaultDrawingAttributes = new System.Windows.Ink.DrawingAttributes
            {
                Color = isYellowBrush ? Colors.Yellow : Colors.Black,
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
            
            Logger.LogInfo($"InkCanvas initialized successfully with {(isYellowBrush ? "Yellow" : "Black")} brush");
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
            return Path.Combine(canvasesDir, handlerStr);
        }

        private string GetTradingCanvasFileBase()
        {
            string handlerStr = targetWindowHandle.ToInt64().ToString();
            string canvasesDir = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Canvases");
            Directory.CreateDirectory(canvasesDir);
            return Path.Combine(canvasesDir, $"trading_{handlerStr}");
        }

        private void SaveCanvas()
        {
            try
            {
                var fileBase = GetCanvasFileBase();
                var xamlFile = fileBase + ".xaml";
                var pngFile = fileBase + ".png";

                // Сохраняем все штрихи как обычные (Trading Canvas больше не используется)
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
                // Сохраняем canvas (Trading Canvas больше не используется)
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
            if (isTradingDrawingMode)
            {
                // White brush for trading drawing
                DrawingCanvas.DefaultDrawingAttributes.Color = Colors.White;
                DrawingCanvas.DefaultDrawingAttributes.Width = 2;
                DrawingCanvas.DefaultDrawingAttributes.Height = 2;
                DrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
                // --- Border: red, thick ---
                CanvasBorder.Stroke = new SolidColorBrush(Colors.Red);
                CanvasBorder.StrokeThickness = 4;
            }
            else
            {
                // Get current brush color from MainWindow
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                bool isYellowBrush = true; // default to yellow
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
                    DrawingCanvas.DefaultDrawingAttributes.Color = Colors.Black;
                }
                // Border always yellow in simple mode
                CanvasBorder.Stroke = new SolidColorBrush(Colors.Yellow);
                CanvasBorder.StrokeThickness = 4;
                DrawingCanvas.DefaultDrawingAttributes.Width = 2;
                DrawingCanvas.DefaultDrawingAttributes.Height = 2;
                DrawingCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
            }
            
            // Убеждаемся, что InkCanvas активен
            DrawingCanvas.IsHitTestVisible = true;
            DrawingCanvas.IsEnabled = true;
            DrawingCanvas.Focus();
            
            Logger.LogInfo($"Brush mode updated: {(isTradingDrawingMode ? "Trading" : "Simple")} mode");
        }

        private void UpdateTradingMode()
        {
            TradingToolbar.ShowDurationPanel(isTradingDrawingMode);
        }

        private void UpdateTradingToolbarVisibility()
        {
            TradingToolbar.Visibility = isTradingDrawingMode ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ToggleTradingMode()
        {
            isTradingDrawingMode = !isTradingDrawingMode;
            UpdateBrushMode();
            UpdateTradingMode();
            UpdateTradingToolbarVisibility();
        }

        public void UpdateSimpleBrushColor(bool isYellow)
        {
            // Update brush color only if we're in simple drawing mode
            if (!isTradingDrawingMode)
            {
                if (isYellow)
                {
                    DrawingCanvas.DefaultDrawingAttributes.Color = Colors.Yellow;
                }
                else
                {
                    DrawingCanvas.DefaultDrawingAttributes.Color = Colors.Black;
                }
                // Border always yellow in simple mode
                CanvasBorder.Stroke = new SolidColorBrush(Colors.Yellow);
                CanvasBorder.StrokeThickness = 4;
                Logger.LogInfo($"Simple brush color updated to {(isYellow ? "Yellow" : "Black")}");
            }
        }

        public void UpdateTargetWindow(IntPtr newTargetWindow, string symbol = null)
        {
            Logger.LogInfo($"Updating target window from {targetWindowHandle.ToInt64()} to {newTargetWindow.ToInt64()}");
            
            // Сохраняем текущий canvas перед сменой окна
            SaveCanvasProperly();
            
            // Обновляем target window
            targetWindowHandle = newTargetWindow;
            if (!string.IsNullOrEmpty(symbol))
            {
                activeSymbol = symbol;
                
                // Применяем настройки символа из DI Container
                var symbolSettingsManager = ServiceContainer.Instance.GetService<SymbolSettingsManager>();
                if (symbolSettingsManager != null)
                {
                    selectedRisk = symbolSettingsManager.GetDefaultRisk(activeSymbol);
                    selectedDuration = symbolSettingsManager.GetDefaultDuration(activeSymbol);
                    
                    // Применяем настройки к UI
                    HighlightSelectedRiskButton(selectedRisk);
                    HighlightSelectedDurationButton(selectedDuration);
                    
                    Logger.LogInfo($"Applied symbol defaults for {activeSymbol}: Risk={selectedRisk}, Duration={selectedDuration}");
                }
            }
            
            // Обновляем отображение активного символа
            UpdateActiveSymbolDisplay();
            
            // Очищаем canvas и загружаем новый
            DrawingCanvas.Strokes.Clear();
            // Очищаем очередь торговых штрихов
            tradingStrokeQueue.Clear();
            LoadCanvas();
            
            // Обновляем фоновое изображение
            SetBackgroundImage();
            
            // Устанавливаем фокус на InkCanvas
            DrawingCanvas.Focus();
            
            Logger.LogInfo($"Target window updated successfully");
        }

        private void DrawingCanvas_StrokeCollected(object sender, System.Windows.Controls.InkCanvasStrokeCollectedEventArgs e)
        {
            // Handle stroke completion
            var args = new StrokeCompletedEventArgs
            {
                Stroke = e.Stroke,
                IsTradingMode = isTradingDrawingMode,
                Bounds = e.Stroke.GetBounds()
            };
            
            StrokeCompleted?.Invoke(this, args);
            
            // If in trading mode, add to processing queue
            if (isTradingDrawingMode)
            {
                AddStrokeToTradingQueue(e.Stroke, args);
            }
            else
            {
                SaveCanvasProperly();
            }
        }
        
        /// <summary>
        /// Добавляет штрих в очередь обработки торговых штрихов
        /// </summary>
        private void AddStrokeToTradingQueue(Stroke stroke, StrokeCompletedEventArgs args)
        {
            try
            {
                // Создаем CaptureData для штриха
                var captureData = CreateCaptureDataFromStroke(args);
                
                // Создаем элемент очереди
                var queueItem = new TradingStrokeItem
                {
                    Stroke = stroke,
                    CaptureData = captureData,
                    CreatedAt = DateTime.Now
                };
                
                // Добавляем в очередь
                tradingStrokeQueue.Enqueue(queueItem);
                Logger.LogInfo($"Stroke added to trading queue. Queue size: {tradingStrokeQueue.Count}");
                
                // Запускаем обработку очереди, если она еще не запущена
                if (!isProcessingTradingQueue)
                {
                    ProcessTradingQueueAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding stroke to trading queue: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Создает CaptureData из штриха
        /// </summary>
        private CaptureData CreateCaptureDataFromStroke(StrokeCompletedEventArgs args)
        {
            try
            {
                // Get stroke bounds with margin
                var bounds = args.Bounds;
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
                if (bounds.Top >= 656 && bounds.Bottom >= 656)
                    model = "MACD";
                else if (bounds.Top < 656 && bounds.Bottom < 656)
                    model = "OHLC";
                
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
                    Source = "window", // Изменено с "trading_canvas" на "window"
                    Broker = brokerState.GetDisplayName(),
                    Duration = duration,
                    Model = model
                };
                
                Logger.LogInfo($"CaptureData created for trading queue: ID={captureEntry.ID}, Symbol={captureEntry.Symbol}, Risk={captureEntry.Risk}, Duration={captureEntry.Duration}, Broker={captureEntry.Broker}, Source={captureEntry.Source}");
                
                return captureEntry;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating CaptureData from stroke: {ex.Message}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// Асинхронно обрабатывает очередь торговых штрихов
        /// </summary>
        private async void ProcessTradingQueueAsync()
        {
            if (isProcessingTradingQueue)
            {
                Logger.LogInfo("Trading queue processing already in progress");
                return;
            }
            
            isProcessingTradingQueue = true;
            Logger.LogInfo("Starting trading queue processing");
            
            try
            {
                while (tradingStrokeQueue.Count > 0)
                {
                    var queueItem = tradingStrokeQueue.Dequeue();
                    Logger.LogInfo($"Processing stroke from queue. Remaining items: {tradingStrokeQueue.Count}");
                    
                    // Обрабатываем штрих
                    await ProcessTradingStrokeAsync(queueItem);
                    
                    // Небольшая задержка между обработкой штрихов
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing trading queue: {ex.Message}", ex);
            }
            finally
            {
                isProcessingTradingQueue = false;
                Logger.LogInfo("Trading queue processing completed");
            }
        }
        
        /// <summary>
        /// Обрабатывает один торговый штрих
        /// </summary>
        private async Task ProcessTradingStrokeAsync(TradingStrokeItem queueItem)
        {
            try
            {
                // Сохраняем CaptureData в базу данных
                var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                var screenshotService = ServiceContainer.Instance.GetService<ScreenshotService>();
                
                databaseService.SaveCapture(queueItem.CaptureData);
                
                // Capture screenshot
                var debugInfo = screenshotService.CaptureScreenAreaDebug(
                    queueItem.CaptureData.X, 
                    queueItem.CaptureData.Y, 
                    queueItem.CaptureData.Width, 
                    queueItem.CaptureData.Height, 
                    queueItem.CaptureData.Monitor, 
                    queueItem.CaptureData.ID
                );
                queueItem.CaptureData.ScreenshotPath = debugInfo.ScreenshotPath;
                databaseService.UpdateCapture(queueItem.CaptureData);
                
                // Обновляем настройки символа и брокера
                UpdateSettingsFromCapture(queueItem.CaptureData);
                
                Logger.LogInfo($"CaptureData saved to database: ID={queueItem.CaptureData.ID}");
                
                // Если это Forex брокер, добавляем трендовую линию в GForex
                if (brokerState.CurrentBroker == BrokerType.Forex)
                {
                    bool success = await AddTrendlineToGForexAsync(queueItem.Stroke);
                    
                    if (success)
                    {
                        Logger.LogInfo("Trendline successfully added to GForex");
                    }
                    else
                    {
                        Logger.LogError("Failed to add trendline to GForex");
                    }
                }
                
                // Удаляем штрих с экрана
                RemoveStrokeFromCanvas(queueItem.Stroke);
                
                // Обновляем фоновое изображение
                SetBackgroundImage();
                
                Logger.LogInfo($"Trading stroke processing completed: ID={queueItem.CaptureData.ID}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing trading stroke: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Обновляет настройки символа и брокера из CaptureData
        /// </summary>
        private void UpdateSettingsFromCapture(CaptureData captureData)
        {
            try
            {
                // Обновляем настройки символа
                if (!string.IsNullOrEmpty(activeSymbol))
                {
                    var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                    databaseService.UpdateSymbolRisk(activeSymbol, selectedRisk);
                    
                    var symbolSettingsManager = ServiceContainer.Instance.GetService<SymbolSettingsManager>();
                    symbolSettingsManager?.UpdateSettingsFromCapture(activeSymbol, selectedRisk, captureData.Duration);
                }
                
                // Обновляем настройки брокера
                var brokerSettingsManager = ServiceContainer.Instance.GetService<BrokerSettingsManager>();
                brokerSettingsManager?.UpdateSettingsFromCapture(brokerState.CurrentBroker, selectedRisk, captureData.Duration);
                
                Logger.LogInfo("Settings updated from capture");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating settings from capture: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Удаляет штрих с Canvas
        /// </summary>
        private void RemoveStrokeFromCanvas(Stroke stroke)
        {
            try
            {
                if (DrawingCanvas.Strokes.Contains(stroke))
                {
                    DrawingCanvas.Strokes.Remove(stroke);
                    Logger.LogInfo("Stroke removed from canvas");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error removing stroke from canvas: {ex.Message}", ex);
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
            TradingToolbar.SetModeLabel(brokerType == BrokerType.Forex ? "TRADING!!" : "BINARY!!", true);
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
                int width = (int)DrawingCanvas.ActualWidth;
                int height = (int)DrawingCanvas.ActualHeight;
                if (width == 0 || height == 0) return;

                var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);

                // Визуализируем фон через WPF Image
                if (BackgroundImage.Source != null)
                {
                    var bg = new System.Windows.Controls.Image
                    {
                        Source = BackgroundImage.Source,
                        Width = width,
                        Height = height,
                        Stretch = Stretch.None
                    };
                    bg.Measure(new System.Windows.Size(width, height));
                    bg.Arrange(new Rect(0, 0, width, height));
                    rtb.Render(bg);
                }

                // Визуализируем InkCanvas
                DrawingCanvas.Measure(new System.Windows.Size(width, height));
                DrawingCanvas.Arrange(new Rect(0, 0, width, height));
                rtb.Render(DrawingCanvas);

                System.Windows.Clipboard.SetImage(rtb);

                var toast = ServiceContainer.Instance.GetService<ToastNotifyService>();
                toast?.ShowToast("Скопировано в буфер обмена!", ToastType.Success);
            }
            catch (Exception ex)
            {
                var toast = ServiceContainer.Instance.GetService<ToastNotifyService>();
                toast?.ShowToast($"Ошибка копирования: {ex.Message}", ToastType.Error);
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
            
            // Обновляем очередь торговых штрихов с учетом сдвига
            var shiftedQueue = new Queue<TradingStrokeItem>();
            foreach (var queueItem in tradingStrokeQueue)
            {
                var shiftedStroke = queueItem.Stroke.Clone();
                var points = new StylusPointCollection();
                
                foreach (var point in queueItem.Stroke.StylusPoints)
                {
                    points.Add(new StylusPoint(point.X + deltaX, point.Y + deltaY, point.PressureFactor));
                }
                
                shiftedStroke.StylusPoints = points;
                
                var shiftedQueueItem = new TradingStrokeItem
                {
                    Stroke = shiftedStroke,
                    CaptureData = queueItem.CaptureData,
                    CreatedAt = queueItem.CreatedAt
                };
                
                shiftedQueue.Enqueue(shiftedQueueItem);
            }
            
            tradingStrokeQueue = shiftedQueue;
            
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
            // Очищаем очередь торговых штрихов
            tradingStrokeQueue.Clear();
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
                Logger.LogError("Error marking trading_canvas captures as skipped after canvas clear", ex);
            }
        }

        public void ReinitializeForNewSymbol(string newSymbol)
        {
            Logger.LogInfo($"Reinitializing CanvasWindow for new symbol: {newSymbol}");
            
            // Сохраняем текущий canvas перед сменой символа
            SaveCanvasProperly();
            
            // Обновляем активный символ
            activeSymbol = newSymbol;
            
            // Обновляем отображение активного символа
            UpdateActiveSymbolDisplay();
            
            // Получаем новый target window для символа
            var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
            var newTargetWindow = windowManagementService?.GetSymbolWindowHandle(newSymbol) ?? IntPtr.Zero;
            
            if (newTargetWindow != IntPtr.Zero)
            {
                targetWindowHandle = newTargetWindow;
                Logger.LogInfo($"Updated target window to {newTargetWindow.ToInt64()} for symbol {newSymbol}");
            }
            
            // Применяем настройки символа из DI Container
            var symbolSettingsManager = ServiceContainer.Instance.GetService<SymbolSettingsManager>();
            if (symbolSettingsManager != null)
            {
                selectedRisk = symbolSettingsManager.GetDefaultRisk(activeSymbol);
                selectedDuration = symbolSettingsManager.GetDefaultDuration(activeSymbol);
                
                // Применяем настройки к UI
                HighlightSelectedRiskButton(selectedRisk);
                HighlightSelectedDurationButton(selectedDuration);
                
                Logger.LogInfo($"Applied symbol defaults for {activeSymbol}: Risk={selectedRisk}, Duration={selectedDuration}");
            }
            
            // Очищаем canvas и загружаем новый
            DrawingCanvas.Strokes.Clear();
            // Очищаем очередь торговых штрихов
            tradingStrokeQueue.Clear();
            LoadCanvas();
            
            // Обновляем фоновое изображение
            SetBackgroundImage();
            
            // Устанавливаем фокус на InkCanvas
            DrawingCanvas.Focus();
            
            Logger.LogInfo($"CanvasWindow reinitialized successfully for symbol {newSymbol}");
        }

        private void UpdateActiveSymbolDisplay()
        {
            if (!string.IsNullOrEmpty(activeSymbol))
            {
                ActiveSymbolText.Text = activeSymbol;
                ActiveSymbolText.Visibility = Visibility.Visible;
                Logger.LogInfo($"Active symbol display updated: {activeSymbol}");
            }
            else
            {
                ActiveSymbolText.Visibility = Visibility.Collapsed;
                Logger.LogInfo("Active symbol display hidden - no active symbol");
            }
        }

        /// <summary>
        /// Добавляет трендовую линию в GForex на основе штриха
        /// </summary>
        private async Task<bool> AddTrendlineToGForexAsync(Stroke stroke)
        {
            try
            {
                if (jForexService == null)
                {
                    Logger.LogError("JForex service is not initialized");
                    return false;
                }

                // Получаем точки штриха
                var points = stroke.StylusPoints;
                if (points.Count < 2)
                {
                    Logger.LogError("Stroke has less than 2 points, cannot create trendline");
                    return false;
                }

                // Берем первую и последнюю точки штриха
                var firstPoint = points[0];
                var lastPoint = points[points.Count - 1];

                // Конвертируем координаты из InkCanvas в экранные координаты
                var firstScreenPoint = DrawingCanvas.PointToScreen(new System.Windows.Point(firstPoint.X, firstPoint.Y));
                var lastScreenPoint = DrawingCanvas.PointToScreen(new System.Windows.Point(lastPoint.X, lastPoint.Y));

                // Получаем размеры окна GForex
                int windowWidth = (int)this.ActualWidth;

                Logger.LogInfo($"Adding trendline to GForex: TargetWindowHandle={targetWindowHandle.ToInt64()}, Point1=({firstScreenPoint.X}, {firstScreenPoint.Y}), Point2=({lastScreenPoint.X}, {lastScreenPoint.Y}), WindowWidth={windowWidth}");

                // Вызываем сервис для добавления трендовой линии
                bool success = await jForexService.AddTrendlineAsync(
                    targetWindowHandle,
                    windowWidth,
                    firstScreenPoint.X,
                    firstScreenPoint.Y,
                    lastScreenPoint.X,
                    lastScreenPoint.Y
                );

                if (success)
                {
                    Logger.LogInfo("Trendline successfully added to GForex");
                }
                else
                {
                    Logger.LogError("Failed to add trendline to GForex");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding trendline to GForex: {ex.Message}", ex);
                return false;
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