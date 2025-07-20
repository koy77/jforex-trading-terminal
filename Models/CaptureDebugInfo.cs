namespace ScreenCaptureApp.Models
{
    public class CaptureDebugInfo
    {
        public int SelectionX { get; set; }
        public int SelectionY { get; set; }
        public int SelectionWidth { get; set; }
        public int SelectionHeight { get; set; }
        public int VirtualLeft { get; set; }
        public int VirtualTop { get; set; }
        public int VirtualRight { get; set; }
        public int VirtualBottom { get; set; }
        public int CropX { get; set; }
        public int CropY { get; set; }
        public int CropWidth { get; set; }
        public int CropHeight { get; set; }
        public string ScreenshotPath { get; set; }
        public string Error { get; set; }
        public int MonitorIndex { get; set; }
    }
} 