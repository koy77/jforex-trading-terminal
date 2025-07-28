using System;
using System.Windows;
using System.Windows.Threading;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp
{
    public partial class TradingPatternOverlay : Window
    {
        private DispatcherTimer _hideTimer;

        public TradingPatternOverlay()
        {
            InitializeComponent();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromSeconds(3);
            _hideTimer.Tick += (s, e) => {
                this.Close();
                _hideTimer.Stop();
            };
        }

        public void ShowPattern(int x, int y, int width, int height)
        {
            try
            {
                // Устанавливаем размеры и позицию окна
                this.Left = x;
                this.Top = y;
                this.Width = width;
                this.Height = height;

                // Устанавливаем размеры рамки
                TradingPatternRectangle.Width = width;
                TradingPatternRectangle.Height = height;

                // Показываем окно
                this.Show();

                // Запускаем таймер для автоматического скрытия
                _hideTimer.Start();

                Logger.LogInfo($"TradingPatternOverlay shown at ({x}, {y}) with size {width}x{height}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error showing trading pattern overlay", ex);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
            }
            base.OnClosed(e);
        }
    }
} 