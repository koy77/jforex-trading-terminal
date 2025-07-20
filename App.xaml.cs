using System.Windows;
using System.Linq;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp
{
    public partial class App : Application
    {
        private CaptureTrackingViewer _trackingViewer;

        public void SubscribeToCaptureTrackingIteration(CaptureTrackingService trackingService, DatabaseService databaseService)
        {
            trackingService.CaptureTrackingIterationEnded += async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (_trackingViewer != null && _trackingViewer.IsVisible)
                    {
                        _trackingViewer.RefreshTrackingImages();
                        _trackingViewer.Activate();
                    }
                    // Если окно не открыто — ничего не делаем
                });
            };
        }

        public void ToggleTrackingViewer(DatabaseService databaseService)
        {
            if (_trackingViewer == null || !_trackingViewer.IsVisible)
            {
                _trackingViewer = new CaptureTrackingViewer(databaseService);
                _trackingViewer.Show();
            }
            else
            {
                _trackingViewer.Close();
                _trackingViewer = null;
            }
        }
    }
} 