using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Services
{
    public class ScreenshotService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool GetWindowInfo(IntPtr hwnd, ref WindowInfo pwi);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowInfo
        {
            public uint cbSize;
            public Rect rcWindow;
            public Rect rcClient;
            public uint dwStyle;
            public uint dwExStyle;
            public uint dwWindowStatus;
            public uint cxWindowBorders;
            public uint cyWindowBorders;
            public ushort atomWindowType;
            public ushort wCreatorVersion;
        }

        private const uint SRCCOPY = 0x00CC0020;
        private const int PW_CLIENTONLY = 0x00000001;

        /// <summary>
        /// Создает скриншот всего экрана в выбранной области
        /// </summary>
        public string CaptureScreenArea(int x, int y, int width, int height, string captureId)
        {
            try
            {
                var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
                int cropX = x;
                int cropY = y;
                int cropWidth = width;
                int cropHeight = height;

                if (cropX < virtualScreen.Left) cropX = virtualScreen.Left;
                if (cropY < virtualScreen.Top) cropY = virtualScreen.Top;
                if (cropX + cropWidth > virtualScreen.Right) cropWidth = virtualScreen.Right - cropX;
                if (cropY + cropHeight > virtualScreen.Bottom) cropHeight = virtualScreen.Bottom - cropY;

                if (cropWidth <= 0 || cropHeight <= 0)
                    throw new Exception("Crop area is out of virtual screen bounds");

                using (Bitmap bmp = new Bitmap(cropWidth, cropHeight))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(cropX, cropY, 0, 0, new System.Drawing.Size(cropWidth, cropHeight), CopyPixelOperation.SourceCopy);
                    }
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES", captureId);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string filePath = Path.Combine(dir, "screenshot.png");
                    bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    return filePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screen screenshot failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Создает скриншот окна
        /// </summary>
        public Bitmap CaptureWindow(IntPtr handle)
        {
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                Logger.LogError($"CaptureWindow: Invalid window size width={width}, height={height} for handle={handle}");
                return null;
            }

            Bitmap bmp = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(bmp);

            IntPtr hdcBitmap = graphics.GetHdc();
            IntPtr hdcWindow = GetWindowDC(handle);

            BitBlt(hdcBitmap, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);

            graphics.ReleaseHdc(hdcBitmap);
            graphics.Dispose();

            ReleaseDC(handle, hdcWindow);

            return bmp;
        }

        /// <summary>
        /// Обрезает изображение по заданным координатам
        /// </summary>
        private Bitmap CropImage(Bitmap source, int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                Logger.LogError($"CropImage: Invalid crop size width={width}, height={height}");
                return null;
            }
            Rectangle cropRect = new Rectangle(x, y, width, height);
            return source.Clone(cropRect, source.PixelFormat);
        }

        /// <summary>
        /// Генерирует имя файла для скриншота
        /// </summary>
        private string GenerateFileName()
        {
            return $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        }

        /// <summary>
        /// Получает директорию для сохранения скриншотов
        /// </summary>
        private string GetScreenshotsDirectory()
        {
            string screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cropped");
            if (!Directory.Exists(screenshotsDir))
                Directory.CreateDirectory(screenshotsDir);
            return screenshotsDir;
        }

        public CaptureDebugInfo CaptureScreenAreaDebug(int x, int y, int width, int height, int monitorIndex, string captureId)
        {
            var info = new CaptureDebugInfo();
            try
            {
                var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
                info.SelectionX = x;
                info.SelectionY = y;
                info.SelectionWidth = width;
                info.SelectionHeight = height;
                info.VirtualLeft = virtualScreen.Left;
                info.VirtualTop = virtualScreen.Top;
                info.VirtualRight = virtualScreen.Right;
                info.VirtualBottom = virtualScreen.Bottom;
                info.MonitorIndex = monitorIndex;

                int cropX = Math.Max(x, virtualScreen.Left);
                int cropY = Math.Max(y, virtualScreen.Top);
                int maxWidth = virtualScreen.Right - cropX;
                int maxHeight = virtualScreen.Bottom - cropY;
                int cropWidth = Math.Min(width, maxWidth);
                int cropHeight = Math.Min(height, maxHeight);
                cropWidth = Math.Max(0, cropWidth);
                cropHeight = Math.Max(0, cropHeight);

                info.CropX = cropX;
                info.CropY = cropY;
                info.CropWidth = cropWidth;
                info.CropHeight = cropHeight;

                if (cropWidth <= 0 || cropHeight <= 0)
                    throw new Exception("Crop area is out of virtual screen bounds");

                using (Bitmap bmp = new Bitmap(cropWidth, cropHeight))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(cropX, cropY, 0, 0, new System.Drawing.Size(cropWidth, cropHeight), CopyPixelOperation.SourceCopy);
                    }
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPTURES", captureId);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string filePath = Path.Combine(dir, "screenshot.png");
                    bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    info.ScreenshotPath = filePath;
                }
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }
            return info;
        }

        // Old overload for backward compatibility
        public string CaptureScreenArea(int x, int y, int width, int height)
        {
            return CaptureScreenArea(x, y, width, height, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        }

        // Old overload for backward compatibility
        public CaptureDebugInfo CaptureScreenAreaDebug(int x, int y, int width, int height, int monitorIndex = 0)
        {
            return CaptureScreenAreaDebug(x, y, width, height, monitorIndex, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        }
    }
} 