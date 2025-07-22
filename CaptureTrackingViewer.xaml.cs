using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;
using System.Globalization;
using System.Windows.Data;
using ScreenCaptureApp.Helpers;
using System.Windows.Media;
using System.Threading.Tasks;

namespace ScreenCaptureApp
{
    public partial class CaptureTrackingViewer : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly string _trackingDir;
        private readonly string _croppedDir;
        private List<CaptureTrackingViewerItem> _allItems = new List<CaptureTrackingViewerItem>();
        private string _currentFilter = "Tracking"; // По умолчанию показываем Tracking
        private CaptureTrackingService _trackingService;

        public CaptureTrackingViewer(DatabaseService databaseService)
        {
            this.Loaded += Window_Loaded;
            this.Closed += Window_Closed;

            InitializeComponent();
            _databaseService = databaseService;
            _trackingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tracking", "captures");
            _croppedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cropped");
            LoadImages();

            // Подписка на BreakoutDetected
            _trackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
            if (_trackingService != null)
            {
                _trackingService.BreakoutDetected += OnBreakoutDetected;
            }

            // Настройки окна для одно мониторной системы
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.ResizeMode = ResizeMode.CanResize;
        }

        public void LoadImages()
        {
            // Загружаем картинки в зависимости от текущего фильтра
            switch (_currentFilter)
            {
                case "Tracking":
                    LoadTrackingImages();
                    break;
                case "Fired":
                    LoadFiredImages();
                    break;
                case "Skipped":
                    LoadSkippedImages();
                    break;
            }
        }

        private void LoadTrackingImages()
        {
            var captures = _databaseService.GetTrackingCaptures();
            _allItems = CreateItemsFromCaptures(captures);
            UpdateCounts();
            ShowFilteredItems("Tracking");
        }

        private void LoadFiredImages()
        {
            var captures = _databaseService.GetAllFiredCaptures();
            _allItems = CreateItemsFromCaptures(captures);
            UpdateCounts();
            ShowFilteredItems("Fired");
        }

        private void LoadSkippedImages()
        {
            var captures = _databaseService.GetAllSkippedCaptures();
            _allItems = CreateItemsFromCaptures(captures);
            UpdateCounts();
            ShowFilteredItems("Skipped");
        }

        private List<CaptureTrackingViewerItem> CreateItemsFromCaptures(List<CaptureData> captures)
        {
            var items = new List<CaptureTrackingViewerItem>();
            foreach (var capture in captures)
            {
                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES", capture.ID);
                string debugPath = Path.Combine(baseDir, "tracking_debug.png");
                string trackingPath = Path.Combine(baseDir, "tracking.png");
                string screenshotPath = Path.Combine(baseDir, "screenshot.png");
                string imagePath = File.Exists(debugPath) ? debugPath :
                                   File.Exists(trackingPath) ? trackingPath :
                                   File.Exists(screenshotPath) ? screenshotPath : string.Empty;
                items.Add(new CaptureTrackingViewerItem
                {
                    ID = capture.ID,
                    Source = capture.Source,
                    ImagePath = imagePath,
                    Symbol = capture.Symbol,
                    Risk = capture.Risk,
                    Handle = capture.Handle,
                    Timestamp = capture.Timestamp,
                    IsFired = capture.IsFired,
                    IsSkipped = capture.IsSkipped,
                    Model = capture.Model,
                    Mt4Order = capture.Mt4Order,
                    Period = capture.Period
                });
            }
            return items;
        }

        private void SetActiveButton(Button active)
        {
            TrackingButton.Background = Brushes.LightGray;
            FiredButton.Background = Brushes.LightGray;
            SkippedButton.Background = Brushes.LightGray;
            active.Background = Brushes.LightGreen;
        }

        private void ShowFilteredItems(string filterType)
        {
            Func<CaptureTrackingViewerItem, DateTime> getDate = x => DateTime.TryParse(x.Timestamp, out var dt) ? dt : DateTime.MinValue;
            var filteredItems = filterType switch
            {
                "Tracking" => _allItems.Where(x => !x.IsFired && !x.IsSkipped && !string.IsNullOrEmpty(x.ImagePath))
                    .OrderByDescending(getDate).ToList(),
                "Fired" => _allItems.Where(x => x.IsFired && !string.IsNullOrEmpty(x.ImagePath))
                    .OrderByDescending(getDate).ToList(),
                "Skipped" => _allItems.Where(x => x.IsSkipped && !string.IsNullOrEmpty(x.ImagePath))
                    .OrderByDescending(getDate).ToList(),
                _ => _allItems.Where(x => !string.IsNullOrEmpty(x.ImagePath))
                    .OrderByDescending(getDate).ToList()
            };

            ImagesPanel.ItemsSource = filteredItems;
            SetActiveButton(filterType switch
            {
                "Tracking" => TrackingButton,
                "Fired" => FiredButton,
                "Skipped" => SkippedButton,
                _ => TrackingButton
            });
        }

        private void TrackingFilter_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = "Tracking";
            LoadImages(); // Перезагружаем картинки для tracking
        }
        
        private void FiredFilter_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = "Fired";
            LoadImages(); // Загружаем картинки для fired
        }
        
        private void SkippedFilter_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = "Skipped";
            LoadImages(); // Загружаем картинки для skipped
        }

        // Метод для обновления только tracking картинок при трекинге
        public void RefreshTrackingImages()
        {
            if (_currentFilter == "Tracking")
            {
                LoadTrackingImages();
            }
        }

        private void UpdateCounts()
        {
            if (TrackingCountText != null)
                TrackingCountText.Text = $"({ _databaseService.GetTrackingCaptures().Count })";
            if (FiredCountText != null)
                FiredCountText.Text = $"({ _databaseService.GetAllFiredCaptures().Count })";
            if (SkippedCountText != null)
                SkippedCountText.Text = $"({ _databaseService.GetAllSkippedCaptures().Count })";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get screen positioning using helper for tracking viewer
            var (left, top, width, height) = MainHelper.GetTrackingViewerPositioning();

            // Set window parameters for single monitor system
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;
        }

        private async Task OnBreakoutDetected(CaptureData capture, TrendlineBreakResult result)
        {
            // Обновляем UI на главном потоке
            await Dispatcher.InvokeAsync(() =>
            {
                if (_currentFilter == "Tracking" || _currentFilter == "Fired")
                {
                    LoadImages();
                }
            });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_trackingService != null)
            {
                _trackingService.BreakoutDetected -= OnBreakoutDetected;
            }
        }

        // Handler for Skip button
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CaptureTrackingViewerItem item)
            {
                var capture = _databaseService.GetAllCaptures().FirstOrDefault(c => c.ID == item.ID);
                if (capture != null)
                {
                    _databaseService.UpdateCaptureIsSkipped(capture, true);
                    LoadImages(); // Refresh UI
                }
            }
        }
    }

    public class CaptureTrackingViewerItem
    {
        public string ID { get; set; }
        public string Source { get; set; }
        public string ImagePath { get; set; }
        public string Symbol { get; set; }
        public double Risk { get; set; }
        public long Handle { get; set; }
        public string Timestamp { get; set; }
        public bool IsFired { get; set; }
        public bool IsSkipped { get; set; }
        public string Model { get; set; }
        public string Mt4Order { get; set; }
        public string Period { get; set; } // <--- добавлено

        public System.Windows.Media.Imaging.BitmapImage ImageSource
        {
            get
            {
                if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath)) return null;
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    using (var stream = new FileStream(ImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    return bitmap;
                }
                catch { return null; }
            }
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 