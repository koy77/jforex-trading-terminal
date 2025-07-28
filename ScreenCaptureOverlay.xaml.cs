using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Forms; // Для Screen
using System.Windows.Interop; // Для HwndSource
using ScreenCaptureApp.Services;
using System.Linq;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp
{
    public partial class ScreenCaptureOverlay : Window
    {
        private Point startPoint;
        private bool isDrawing = false;
        private bool isDragging = false;
        private bool isResizing = false;
        private Point dragStartPoint;
        private Rect originalRect;
        private ResizeDirection resizeDirection = ResizeDirection.None;
        private double selectionStartY;
        private double selectionEndY;
        private string activeSymbol;
        private IntPtr _windowHandle = IntPtr.Zero;
        private double selectedRisk = 1; // Значение риска по умолчанию
        private int selectedDuration = 2; // Значение duration по умолчанию
        private BrokerState brokerState;
        private DurationState durationState;
        private ToolbarSettingsManager _toolbarSettingsManager;

        private readonly CaptureService _captureService;
        private readonly DatabaseService _databaseService;
        private readonly HotkeysService _hotkeysService;

        public event EventHandler<CaptureEventArgs> CaptureCompleted;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Получаем виртуальные границы всех мониторов
            var bounds = _captureService.GetVirtualScreenBounds();
            this.Left = bounds.Left;
            this.Top = bounds.Top;
            this.Width = bounds.Width;
            this.Height = bounds.Height;

            // Принудительно активируем окно
            this.Activate();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Window source initialized");
            
            // Принудительно устанавливаем окно поверх всех остальных
            this.Topmost = true;
            this.Activate();
            
            // Устанавливаем глобальный хук мыши
            _captureService.SetupMouseHook(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            _captureService.RemoveMouseHook();
            base.OnClosed(e);
            if (_hotkeysService != null)
                _hotkeysService.OnEnterKeyPressed -= HotkeysService_OnEnterKeyPressed;
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F)
            {
                if (SelectionRectangle.Visibility == Visibility.Visible && SelectionRectangle.Width > 0 && SelectionRectangle.Height > 0)
                {
                    CompleteCapture();
                    e.Handled = true;
                }
            }
        }

        public ScreenCaptureOverlay(IntPtr windowHandle, string symbol = null)
        {
            InitializeComponent();
            _captureService = ServiceContainer.Instance.GetService<CaptureService>();
            _databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
            _hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
            brokerState = ServiceContainer.Instance.GetService<BrokerState>();
            durationState = ServiceContainer.Instance.GetService<DurationState>();
            _toolbarSettingsManager = ServiceContainer.Instance.GetService<ToolbarSettingsManager>();
            _captureService.CaptureCompleted += OnCaptureCompleted;
            _captureService.MouseHookEvent += OnMouseHookEvent;
            activeSymbol = symbol;
            _windowHandle = windowHandle;
            
            // Применяем настройки тулбара по handle окна
            if (_windowHandle != IntPtr.Zero)
            {
                var toolbarSettings = _toolbarSettingsManager.GetSettings(_windowHandle.ToInt64());
                if (toolbarSettings != null)
                {
                    selectedRisk = toolbarSettings.Risk;
                    selectedDuration = toolbarSettings.Duration;
                    brokerState.CurrentBroker = toolbarSettings.Broker;
                    Logger.LogInfo($"Applied toolbar settings for handle={_windowHandle.ToInt64()}: Risk={selectedRisk}, Duration={selectedDuration}, Broker={toolbarSettings.Broker}");
                }
            }
            
            InitializeWindow();
            // Подписка на события TradingToolbar
            TradingToolbar.RiskChanged += (risk) => {
                selectedRisk = risk;
                if (_windowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(_windowHandle.ToInt64(), risk: risk);
            };
            TradingToolbar.BrokerChanged += (broker) => {
                brokerState.CurrentBroker = broker;
                UpdateTradingToolbarUiByBroker(broker);
                if (_windowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(_windowHandle.ToInt64(), broker: broker);
            };
            TradingToolbar.DurationChanged += (duration) => {
                durationState.CurrentDuration = duration;
                selectedDuration = duration;
                if (_windowHandle != IntPtr.Zero)
                    _toolbarSettingsManager.UpdateSettings(_windowHandle.ToInt64(), duration: duration);
            };
            // Инициализация UI по текущему брокеру
            TradingToolbar.HighlightSelectedBroker(brokerState.CurrentBroker);
            TradingToolbar.HighlightSelectedRiskButton(selectedRisk);
            TradingToolbar.HighlightSelectedDurationButton(selectedDuration);
            if (_hotkeysService != null)
                _hotkeysService.OnEnterKeyPressed += HotkeysService_OnEnterKeyPressed;
        }

        private void InitializeWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Cursor = System.Windows.Input.Cursors.Cross;

            // Получаем виртуальные границы всех мониторов
            var bounds = _captureService.GetVirtualScreenBounds();
            this.Left = bounds.Left;
            this.Top = bounds.Top;
            this.Width = bounds.Width;
            this.Height = bounds.Height;

            // Принудительно активируем окно
            this.Activate();
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

        private void OnCaptureCompleted(object sender, CaptureEventArgs e)
        {
            CaptureCompleted?.Invoke(this, e);
        }

        private void OnMouseHookEvent(object sender, MouseHookEventArgs e)
        {
            // Обработка глобальных событий мыши, если нужно
            System.Diagnostics.Debug.WriteLine($"Mouse hook event: {e.EventType}");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point currentPoint = e.GetPosition(CaptureCanvas);
            selectionStartY = currentPoint.Y;
            
            // Отладочная информация
            System.Diagnostics.Debug.WriteLine($"MouseDown: Canvas position: {currentPoint}, Window position: {e.GetPosition(this)}");
            
            if (SelectionRectangle.Visibility == Visibility.Visible)
            {
                // Check if clicking on the selection rectangle
                Rect selectionRect = new Rect(
                    Canvas.GetLeft(SelectionRectangle),
                    Canvas.GetTop(SelectionRectangle),
                    SelectionRectangle.Width,
                    SelectionRectangle.Height);

                if (selectionRect.Contains(currentPoint))
                {
                    // Check if clicking on resize handles
                    resizeDirection = GetResizeDirection(currentPoint, selectionRect);
                    if (resizeDirection != ResizeDirection.None)
                    {
                        isResizing = true;
                        dragStartPoint = currentPoint;
                        originalRect = selectionRect;
                    }
                    else
                    {
                        // Dragging the selection
                        isDragging = true;
                        dragStartPoint = currentPoint;
                        originalRect = selectionRect;
                    }
                }
                else
                {
                    // Start new selection
                    StartNewSelection(currentPoint);
                }
            }
            else
            {
                // Start new selection
                StartNewSelection(currentPoint);
            }
        }

        private void StartNewSelection(Point point)
        {
            startPoint = point;
            isDrawing = true;
            selectionStartY = point.Y;
            
            Canvas.SetLeft(SelectionRectangle, point.X);
            Canvas.SetTop(SelectionRectangle, point.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            SelectionRectangle.Visibility = Visibility.Visible;
            
            CoordinatesText.Visibility = Visibility.Visible;
            InstructionsText.Visibility = Visibility.Collapsed;
            SymbolLabel.Visibility = Visibility.Collapsed;
        }

        private ResizeDirection GetResizeDirection(Point point, Rect rect)
        {
            double handleSize = 8;
            
            // Check corners first
            if (Math.Abs(point.X - rect.Left) < handleSize && Math.Abs(point.Y - rect.Top) < handleSize) 
                return ResizeDirection.TopLeft;
            if (Math.Abs(point.X - rect.Right) < handleSize && Math.Abs(point.Y - rect.Top) < handleSize) 
                return ResizeDirection.TopRight;
            if (Math.Abs(point.X - rect.Left) < handleSize && Math.Abs(point.Y - rect.Bottom) < handleSize) 
                return ResizeDirection.BottomLeft;
            if (Math.Abs(point.X - rect.Right) < handleSize && Math.Abs(point.Y - rect.Bottom) < handleSize) 
                return ResizeDirection.BottomRight;
            
            // Check edges
            if (Math.Abs(point.X - rect.Left) < handleSize && point.Y >= rect.Top && point.Y <= rect.Bottom) 
                return ResizeDirection.Left;
            if (Math.Abs(point.X - rect.Right) < handleSize && point.Y >= rect.Top && point.Y <= rect.Bottom) 
                return ResizeDirection.Right;
            if (Math.Abs(point.Y - rect.Top) < handleSize && point.X >= rect.Left && point.X <= rect.Right) 
                return ResizeDirection.Top;
            if (Math.Abs(point.Y - rect.Bottom) < handleSize && point.X >= rect.Left && point.X <= rect.Right) 
                return ResizeDirection.Bottom;
            
            return ResizeDirection.None;
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point currentPoint = e.GetPosition(CaptureCanvas);
            
            // Отладочная информация (только при рисовании, чтобы не засорять лог)
            if (isDrawing || isDragging || isResizing)
            {
                System.Diagnostics.Debug.WriteLine($"MouseMove: Canvas position: {currentPoint}, Window position: {e.GetPosition(this)}");
            }
            
            if (isDrawing)
            {
                UpdateSelectionRectangle(startPoint, currentPoint);
                UpdateCoordinatesText();
                UpdateResizeHandles();
            }
            else if (isDragging)
            {
                double deltaX = currentPoint.X - dragStartPoint.X;
                double deltaY = currentPoint.Y - dragStartPoint.Y;
                
                Canvas.SetLeft(SelectionRectangle, originalRect.Left + deltaX);
                Canvas.SetTop(SelectionRectangle, originalRect.Top + deltaY);
                
                UpdateCoordinatesText();
                UpdateResizeHandles();
            }
            else if (isResizing)
            {
                ResizeSelection(currentPoint);
                UpdateCoordinatesText();
                UpdateResizeHandles();
            }
            else if (SelectionRectangle.Visibility == Visibility.Visible)
            {
                // Update cursor based on position
                Rect selectionRect = new Rect(
                    Canvas.GetLeft(SelectionRectangle),
                    Canvas.GetTop(SelectionRectangle),
                    SelectionRectangle.Width,
                    SelectionRectangle.Height);

                ResizeDirection direction = GetResizeDirection(currentPoint, selectionRect);
                UpdateCursor(direction);
            }
        }

        private void ResizeSelection(Point currentPoint)
        {
            double deltaX = currentPoint.X - dragStartPoint.X;
            double deltaY = currentPoint.Y - dragStartPoint.Y;
            
            double newLeft = originalRect.Left;
            double newTop = originalRect.Top;
            double newWidth = originalRect.Width;
            double newHeight = originalRect.Height;

            switch (resizeDirection)
            {
                case ResizeDirection.Left:
                    newLeft = originalRect.Left + deltaX;
                    newWidth = originalRect.Width - deltaX;
                    break;
                case ResizeDirection.Right:
                    newWidth = originalRect.Width + deltaX;
                    break;
                case ResizeDirection.Top:
                    newTop = originalRect.Top + deltaY;
                    newHeight = originalRect.Height - deltaY;
                    break;
                case ResizeDirection.Bottom:
                    newHeight = originalRect.Height + deltaY;
                    break;
                case ResizeDirection.TopLeft:
                    newLeft = originalRect.Left + deltaX;
                    newTop = originalRect.Top + deltaY;
                    newWidth = originalRect.Width - deltaX;
                    newHeight = originalRect.Height - deltaY;
                    break;
                case ResizeDirection.TopRight:
                    newTop = originalRect.Top + deltaY;
                    newWidth = originalRect.Width + deltaX;
                    newHeight = originalRect.Height - deltaY;
                    break;
                case ResizeDirection.BottomLeft:
                    newLeft = originalRect.Left + deltaX;
                    newWidth = originalRect.Width - deltaX;
                    newHeight = originalRect.Height + deltaY;
                    break;
                case ResizeDirection.BottomRight:
                    newWidth = originalRect.Width + deltaX;
                    newHeight = originalRect.Height + deltaY;
                    break;
            }

            // Ensure minimum size
            if (newWidth < 10) newWidth = 10;
            if (newHeight < 10) newHeight = 10;

            Canvas.SetLeft(SelectionRectangle, newLeft);
            Canvas.SetTop(SelectionRectangle, newTop);
            SelectionRectangle.Width = newWidth;
            SelectionRectangle.Height = newHeight;
        }

        private void UpdateResizeHandles()
        {
            if (SelectionRectangle.Visibility != Visibility.Visible) return;

            double x = Canvas.GetLeft(SelectionRectangle);
            double y = Canvas.GetTop(SelectionRectangle);
            double width = SelectionRectangle.Width;
            double height = SelectionRectangle.Height;

            // Position corner handles
            Canvas.SetLeft(TopLeftHandle, x - 4);
            Canvas.SetTop(TopLeftHandle, y - 4);
            
            Canvas.SetLeft(TopRightHandle, x + width - 4);
            Canvas.SetTop(TopRightHandle, y - 4);
            
            Canvas.SetLeft(BottomLeftHandle, x - 4);
            Canvas.SetTop(BottomLeftHandle, y + height - 4);
            
            Canvas.SetLeft(BottomRightHandle, x + width - 4);
            Canvas.SetTop(BottomRightHandle, y + height - 4);

            // Position edge handles
            Canvas.SetLeft(TopHandle, x + width / 2 - 4);
            Canvas.SetTop(TopHandle, y - 4);
            
            Canvas.SetLeft(BottomHandle, x + width / 2 - 4);
            Canvas.SetTop(BottomHandle, y + height - 4);
            
            Canvas.SetLeft(LeftHandle, x - 4);
            Canvas.SetTop(LeftHandle, y + height / 2 - 4);
            
            Canvas.SetLeft(RightHandle, x + width - 4);
            Canvas.SetTop(RightHandle, y + height / 2 - 4);

            // Show handles
            TopLeftHandle.Visibility = Visibility.Visible;
            TopRightHandle.Visibility = Visibility.Visible;
            BottomLeftHandle.Visibility = Visibility.Visible;
            BottomRightHandle.Visibility = Visibility.Visible;
            TopHandle.Visibility = Visibility.Visible;
            BottomHandle.Visibility = Visibility.Visible;
            LeftHandle.Visibility = Visibility.Visible;
            RightHandle.Visibility = Visibility.Visible;
        }

        private void UpdateCursor(ResizeDirection direction)
        {
            switch (direction)
            {
                case ResizeDirection.Left:
                case ResizeDirection.Right:
                    this.Cursor = System.Windows.Input.Cursors.SizeWE;
                    break;
                case ResizeDirection.Top:
                case ResizeDirection.Bottom:
                    this.Cursor = System.Windows.Input.Cursors.SizeNS;
                    break;
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    this.Cursor = System.Windows.Input.Cursors.SizeNWSE;
                    break;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    this.Cursor = System.Windows.Input.Cursors.SizeNESW;
                    break;
                default:
                    if (IsInsideSelection())
                        this.Cursor = System.Windows.Input.Cursors.SizeAll;
                    else
                        this.Cursor = System.Windows.Input.Cursors.Cross;
                    break;
            }
        }

        private bool IsInsideSelection()
        {
            Point currentPoint = Mouse.GetPosition(CaptureCanvas);
            Rect selectionRect = new Rect(
                Canvas.GetLeft(SelectionRectangle),
                Canvas.GetTop(SelectionRectangle),
                SelectionRectangle.Width,
                SelectionRectangle.Height);
            
            return selectionRect.Contains(currentPoint);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point currentPoint = e.GetPosition(CaptureCanvas);
            selectionEndY = currentPoint.Y;
            isDrawing = false;
            isDragging = false;
            isResizing = false;
            resizeDirection = ResizeDirection.None;
            
            // Позиционируем верхнюю панель на том же мониторе, где завершилось выделение
            PositionTopPanelOnSelectionMonitor();
            
            if (SelectionRectangle.Visibility != Visibility.Visible)
            {
                SymbolLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSelectionRectangle(Point start, Point end)
        {
            double left = Math.Min(start.X, end.X);
            double top = Math.Min(start.Y, end.Y);
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            
            Canvas.SetLeft(SelectionRectangle, left);
            Canvas.SetTop(SelectionRectangle, top);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;

            // Позиционируем верхнюю панель на том же мониторе, где выделение
            PositionTopPanelOnSelectionMonitor();

            // Update symbol label
            if (width > 0 && height > 0)
            {
                if (!string.IsNullOrEmpty(activeSymbol))
                {
                    SymbolLabel.Visibility = Visibility.Visible;
                    SymbolLabel.Text = activeSymbol;
                    double symbolLabelWidth = SymbolLabel.ActualWidth > 0 ? SymbolLabel.ActualWidth : 60;
                    double symbolLabelHeight = SymbolLabel.ActualHeight > 0 ? SymbolLabel.ActualHeight : 30;
                    double rightX = left + width + 8;
                    double baseY = top + (height - symbolLabelHeight) / 2;
                    Canvas.SetLeft(SymbolLabel, rightX);
                    Canvas.SetTop(SymbolLabel, baseY);
                }
                else
                {
                    SymbolLabel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                SymbolLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void PositionTopPanelOnSelectionMonitor()
        {
            if (SelectionRectangle.Visibility != Visibility.Visible || SelectionRectangle.Width <= 0 || SelectionRectangle.Height <= 0)
                return;

            double x = Canvas.GetLeft(SelectionRectangle);
            double y = Canvas.GetTop(SelectionRectangle);
            double width = SelectionRectangle.Width;
            double height = SelectionRectangle.Height;

            // Центр выделения
            double centerX = x + width / 2;
            double centerY = y + height / 2;

            // Получаем экран, на котором находится центр выделения
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)centerX, (int)centerY));
            var bounds = screen.Bounds;

            // Позиционируем TradingToolbar в верхней части bounds этого экрана
            double panelWidth = TradingToolbar.ActualWidth > 0 ? TradingToolbar.ActualWidth : 600; // Примерная ширина панели
            double left = bounds.Left + (bounds.Width - panelWidth) / 2;
            double top = bounds.Top + 10; // 10px от верхнего края

            // Учитываем смещение виртуального экрана
            double virtualScreenLeft = System.Windows.Forms.Screen.AllScreens[0].Bounds.Left;
            Canvas.SetLeft(TradingToolbar, left - virtualScreenLeft);
            Canvas.SetTop(TradingToolbar, top - System.Windows.Forms.Screen.AllScreens[0].Bounds.Top);
        }

        private void UpdateCoordinatesText() 
        {      
            
            double x = Canvas.GetLeft(SelectionRectangle);
            double y = Canvas.GetTop(SelectionRectangle);
            double width = SelectionRectangle.Width;
            double height = SelectionRectangle.Height;
            
            CoordinatesText.Text = $"X: {(int)x}, Y: {(int)y}, W: {(int)width}, H: {(int)height}";
            
            // Position the text near the selection
            Canvas.SetLeft(CoordinatesText, x + width + 5);
            Canvas.SetTop(CoordinatesText, y);
        }

        private void HotkeysService_OnEnterKeyPressed()
        {
            Dispatcher.Invoke(() => {
                if (SelectionRectangle.Visibility == Visibility.Visible && SelectionRectangle.Width > 0 && SelectionRectangle.Height > 0)
                {
                    CompleteCapture();
                }
            });
        }


        private async void CompleteCapture()
        {
            double x = Canvas.GetLeft(SelectionRectangle);
            double y = Canvas.GetTop(SelectionRectangle);
            double width = SelectionRectangle.Width;
            double height = SelectionRectangle.Height;
            int xVirtual = (int)x;
            if (System.Windows.Forms.Screen.AllScreens.Length > 1)
            {
                int firstScreenWidth = System.Windows.Forms.Screen.AllScreens[0].Bounds.Width;
                if (x >= firstScreenWidth)
                {
                    xVirtual = (int)(x - firstScreenWidth);
                }
            }
            this.Close();
            
            // Логируем значения перед вызовом CaptureAreaWithSymbolAndRisk
            Logger.LogInfo($"ScreenCaptureOverlay calling CaptureAreaWithSymbolAndRisk: SelectedRisk={TradingToolbar.SelectedRisk}, SelectedDuration={TradingToolbar.SelectedDuration}, activeSymbol={activeSymbol}");
            Logger.LogInfo($"ScreenCaptureOverlay local values: selectedRisk={selectedRisk}, selectedDuration={selectedDuration}");
            
            _captureService.CaptureAreaWithSymbolAndRisk(xVirtual, y, width, height, this, activeSymbol, _windowHandle, TradingToolbar.SelectedRisk, TradingToolbar.SelectedDuration);
            // Запуск трекинга сразу после захвата
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var trackingService = mainWindow.GetCaptureTrackingService();
                if (trackingService != null)
                {
                    await trackingService.RunOnceAsync();
                }
            }
        }
    }

    public enum ResizeDirection
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
} 