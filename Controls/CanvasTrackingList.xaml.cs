using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Controls
{
    public partial class CanvasTrackingList : UserControl
    {
        private DatabaseService _databaseService;
        private CaptureTrackingService _captureTrackingService;
        private Dictionary<string, UIElement> _trackingItemElements = new Dictionary<string, UIElement>();

        public CanvasTrackingList()
        {
            InitializeComponent();
            
            try
            {
                _databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
                _captureTrackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
                
                // Подписываемся на событие окончания итерации трекинга
                if (_captureTrackingService != null)
                {
                    _captureTrackingService.CaptureTrackingIterationEnded += OnCaptureTrackingIterationEnded;
                    Logger.LogInfo("CanvasTrackingList: Subscribed to CaptureTrackingIterationEnded event");
                }
                
                // Загружаем начальный список трекающихся CaptureData
                UpdateTrackingList();
                
                Logger.LogInfo("CanvasTrackingList: Initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTrackingList: Error initializing: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Обработчик события окончания итерации трекинга
        /// </summary>
        private Task OnCaptureTrackingIterationEnded()
        {
            try
            {
                // Обновляем список в UI потоке
                Dispatcher.Invoke(() => UpdateTrackingList());
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTrackingList: Error handling tracking iteration end: {ex.Message}", ex);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Обновляет список трекающихся CaptureData
        /// </summary>
        private void UpdateTrackingList()
        {
            try
            {
                if (_databaseService == null)
                {
                    Logger.LogWarning("CanvasTrackingList: DatabaseService is null");
                    return;
                }

                // Получаем список трекающихся CaptureData
                var trackingCaptures = _databaseService.GetAllUnfiredAndUnskippedCaptures();
                
                Logger.LogDebug($"CanvasTrackingList: Found {trackingCaptures.Count} tracking captures");

                // Очищаем старые элементы
                TrackingItemsPanel.Children.Clear();
                _trackingItemElements.Clear();

                if (trackingCaptures.Count == 0)
                {
                    MainBorder.Visibility = Visibility.Collapsed;
                    return;
                }

                // Добавляем элементы для каждого трекающегося CaptureData
                foreach (var capture in trackingCaptures)
                {
                    var itemElement = CreateTrackingItem(capture);
                    if (itemElement != null)
                    {
                        TrackingItemsPanel.Children.Add(itemElement);
                        _trackingItemElements[capture.ID] = itemElement;
                    }
                }

                MainBorder.Visibility = Visibility.Visible;
                
                Logger.LogDebug($"CanvasTrackingList: Updated tracking list with {trackingCaptures.Count} items");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTrackingList: Error updating tracking list: {ex.Message}", ex);
                MainBorder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Создает UI элемент для одного CaptureData
        /// </summary>
        private UIElement CreateTrackingItem(CaptureData capture)
        {
            try
            {
                // Основной контейнер
                var border = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 40, 40, 40)),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(5, 0, 5, 0),
                    Padding = new Thickness(5)
                };

                // Горизонтальный контейнер для двух колонок
                var horizontalPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Изображение tracking_picture (левая колонка)
                var image = new System.Windows.Controls.Image
                {
                    MaxHeight = 60,
                    MaxWidth = 80,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                // Загружаем изображение из файла tracking.png
                string trackingImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES", capture.ID, "tracking.png");
                if (File.Exists(trackingImagePath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(trackingImagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        image.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"CanvasTrackingList: Error loading tracking image for {capture.ID}: {ex.Message}", ex);
                    }
                }
                else
                {
                    Logger.LogDebug($"CanvasTrackingList: Tracking image not found: {trackingImagePath}");
                }

                // Правая колонка: символ и кнопка (вертикально)
                var rightPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Символ
                var symbolText = new TextBlock
                {
                    Text = capture.Symbol ?? "N/A",
                    Foreground = new SolidColorBrush(Colors.Yellow),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                // Кнопка Skip
                var skipButton = new Button
                {
                    Content = "Skip",
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 200, 50, 50)),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(10, 5, 10, 5),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Colors.Red),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Обработчик клика по кнопке Skip
                skipButton.Click += (sender, e) => SkipButton_Click(capture);

                rightPanel.Children.Add(symbolText);
                rightPanel.Children.Add(skipButton);

                horizontalPanel.Children.Add(image);
                horizontalPanel.Children.Add(rightPanel);

                border.Child = horizontalPanel;

                return border;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTrackingList: Error creating tracking item for {capture.ID}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Обработчик клика по кнопке Skip
        /// </summary>
        private void SkipButton_Click(CaptureData capture)
        {
            try
            {
                Logger.LogInfo($"CanvasTrackingList: Skip button clicked for capture ID={capture.ID}, Symbol={capture.Symbol}");

                if (_databaseService != null)
                {
                    // Помечаем CaptureData как пропущенный
                    _databaseService.UpdateCaptureIsSkipped(capture, true);
                    
                    // Удаляем элемент из UI
                    if (_trackingItemElements.ContainsKey(capture.ID))
                    {
                        var element = _trackingItemElements[capture.ID];
                        TrackingItemsPanel.Children.Remove(element);
                        _trackingItemElements.Remove(capture.ID);
                    }

                    // Скрываем панель, если больше нет элементов
                    if (TrackingItemsPanel.Children.Count == 0)
                    {
                        MainBorder.Visibility = Visibility.Collapsed;
                    }

                    Logger.LogInfo($"CanvasTrackingList: Capture {capture.ID} marked as skipped and removed from UI");
                }
                else
                {
                    Logger.LogWarning("CanvasTrackingList: DatabaseService is null - cannot skip capture");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTrackingList: Error handling skip button click: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_captureTrackingService != null)
                {
                    _captureTrackingService.CaptureTrackingIterationEnded -= OnCaptureTrackingIterationEnded;
                    Logger.LogInfo("CanvasTrackingList: Unsubscribed from CaptureTrackingIterationEnded event");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CanvasTrackingList: Error disposing: {ex.Message}", ex);
            }
        }
    }
}
