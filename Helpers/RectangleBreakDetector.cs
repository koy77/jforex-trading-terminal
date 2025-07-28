using System;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Collections.Generic;
using System.Linq;

namespace ScreenCaptureApp.Helpers
{
    public enum RectangleBreakResult
    {
        NoRectangle,
        NoBreakout,
        BreakoutUp,
        BreakoutDown
    }

    public class RectangleBreakDetector
    {
        public RectangleBreakResult DetectBreakout(Bitmap cropped, CaptureData capture, string saveDebugPath = null)
        {
            // Параметры по умолчанию для каждой модели
            if (capture.Model == "MACD")
            {
                // Можно задать другие параметры для MACD
                int zoneWidth = 4;
                int zoneHeight = 4;
                double breakoutThreshold = 0.1;
                int zoneOffset = 5;
                return DetectBreakoutMACD(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutThreshold, zoneOffset);
            }
            else
            {
                // OHLC (или по умолчанию)
                int zoneWidth = 4;
                int zoneHeight = 4;
                double breakoutThreshold = 0.3;
                int zoneOffset = 5;
                return DetectBreakoutOHLC(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutThreshold, zoneOffset);
            }
        }

        public RectangleBreakResult DetectBreakoutOHLC(Bitmap cropped, string saveDebugPath, int zoneWidth, int zoneHeight, double breakoutThreshold, int zoneOffset)
        {
            Logger.LogDebug($"RectangleBreakDetector: Start DetectBreakoutOHLC, bitmap size: {cropped.Width}x{cropped.Height}");
            var debugPoints = new List<(int x, int y, bool isBreakout)>();
            
            using (var analysisBmp = new Bitmap(cropped))
            {
                // Анализируем всю область изображения на наличие красных и зеленых пикселей
                int totalPixels = analysisBmp.Width * analysisBmp.Height;
                int redCount = 0, greenCount = 0;
                List<Point> redPoints = new List<Point>();
                List<Point> greenPoints = new List<Point>();

                // Подсчитываем красные и зеленые пиксели
                for (int y = 0; y < analysisBmp.Height; y++)
                {
                    for (int x = 0; x < analysisBmp.Width; x++)
                    {
                        var pixel = analysisBmp.GetPixel(x, y);
                        if (IsRed(pixel))
                        {
                            redCount++;
                            redPoints.Add(new Point(x, y));
                        }
                        else if (IsGreen(pixel))
                        {
                            greenCount++;
                            greenPoints.Add(new Point(x, y));
                        }
                    }
                }

                Logger.LogDebug($"RectangleBreakDetector: Found {redCount} red pixels and {greenCount} green pixels");

                // Вычисляем соотношения
                double redRatio = (double)redCount / totalPixels;
                double greenRatio = (double)greenCount / totalPixels;

                Logger.LogDebug($"RectangleBreakDetector: Red ratio: {redRatio:F3}, Green ratio: {greenRatio:F3}");

                // Определяем breakout на основе преобладающего цвета
                if (greenRatio >= breakoutThreshold && greenRatio > redRatio)
                {
                    Logger.LogInfo($"RectangleBreakDetector: Breakout UP detected - Green ratio: {greenRatio:F3}");
                    if (!string.IsNullOrEmpty(saveDebugPath))
                        SaveDebugVisualization(cropped, debugPoints, greenPoints, redPoints, saveDebugPath, true);
                    return RectangleBreakResult.BreakoutUp;
                }
                else if (redRatio >= breakoutThreshold && redRatio > greenRatio)
                {
                    Logger.LogInfo($"RectangleBreakDetector: Breakout DOWN detected - Red ratio: {redRatio:F3}");
                    if (!string.IsNullOrEmpty(saveDebugPath))
                        SaveDebugVisualization(cropped, debugPoints, greenPoints, redPoints, saveDebugPath, false);
                    return RectangleBreakResult.BreakoutDown;
                }

                Logger.LogInfo($"RectangleBreakDetector: No breakout detected. Red: {redRatio:F3}, Green: {greenRatio:F3}");
                if (!string.IsNullOrEmpty(saveDebugPath))
                    SaveDebugVisualization(cropped, debugPoints, greenPoints, redPoints, saveDebugPath, null);
            }
            return RectangleBreakResult.NoBreakout;
        }

        public RectangleBreakResult DetectBreakoutMACD(Bitmap cropped, string saveDebugPath, int zoneWidth, int zoneHeight, double breakoutThreshold, int zoneOffset)
        {
            return DetectBreakoutOHLC(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutThreshold, zoneOffset);
        }

        private void SaveDebugVisualization(Bitmap cropped, List<(int x, int y, bool isBreakout)> points, List<Point> greenPoints, List<Point> redPoints, string path, bool? isUpBreakout = null)
        {
            using (var vis = new Bitmap(cropped))
            using (var g = Graphics.FromImage(vis))
            {
                // Рисуем красные пиксели
                foreach (var pt in redPoints)
                {
                    var rect = new Rectangle(pt.X - 1, pt.Y - 1, 3, 3);
                    using (var brush = new SolidBrush(Color.FromArgb(80, Color.Red)))
                        g.FillRectangle(brush, rect);
                    g.DrawRectangle(new Pen(Color.Red, 1), rect);
                }

                // Рисуем зеленые пиксели
                foreach (var pt in greenPoints)
                {
                    var rect = new Rectangle(pt.X - 1, pt.Y - 1, 3, 3);
                    using (var brush = new SolidBrush(Color.FromArgb(80, Color.Green)))
                        g.FillRectangle(brush, rect);
                    g.DrawRectangle(new Pen(Color.Green, 1), rect);
                }

                // Добавляем информацию о результате
                string resultText = isUpBreakout.HasValue 
                    ? (isUpBreakout.Value ? "BREAKOUT UP" : "BREAKOUT DOWN")
                    : "NO BREAKOUT";
                
                var font = new Font("Arial", 12, FontStyle.Bold);
                var textBrush = new SolidBrush(Color.Yellow);
                var textSize = g.MeasureString(resultText, font);
                var textRect = new Rectangle(10, 10, (int)textSize.Width + 20, (int)textSize.Height + 10);
                
                // Фон для текста
                using (var bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    g.FillRectangle(bgBrush, textRect);
                
                g.DrawString(resultText, font, textBrush, 20, 15);
                
                // Статистика
                string stats = $"Red: {redPoints.Count}, Green: {greenPoints.Count}";
                var statsSize = g.MeasureString(stats, font);
                var statsRect = new Rectangle(10, 40, (int)statsSize.Width + 20, (int)statsSize.Height + 10);
                
                using (var bgBrush2 = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    g.FillRectangle(bgBrush2, statsRect);
                
                g.DrawString(stats, font, textBrush, 20, 45);

                vis.Save(path);
            }
        }

        private bool IsGreen(System.Drawing.Color pixel)
        {
            return pixel.G > 130 && pixel.R < 100 && pixel.B < 100;
        }
        
        private bool IsRed(System.Drawing.Color pixel)
        {
            return pixel.R > 130 && pixel.G < 100 && pixel.B < 100;
        }
    }
} 