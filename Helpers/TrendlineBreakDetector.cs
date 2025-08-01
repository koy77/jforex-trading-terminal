using System;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Generic;

namespace ScreenCaptureApp.Helpers
{
    public enum TrendlineBreakResult
    {
        NoTrendline,
        NoBreakout,
        BreakoutUp,
        BreakoutDown
    }

    public class TrendlineBreakDetector
    {
        public TrendlineBreakResult DetectBreakout(Bitmap cropped, CaptureData capture, string saveDebugPath = null)
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
                int zoneWidth = 20;
                int zoneHeight = 20;
                double breakoutThreshold = 20; // Минимальное количество пикселей для пробоя
                int zoneOffset = 20;
                return DetectBreakoutOHLC(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutThreshold, zoneOffset);
            }
        }

        public TrendlineBreakResult DetectBreakoutOHLC(Bitmap cropped, string saveDebugPath, int zoneWidth, int zoneHeight, double breakoutThreshold, int zoneOffset)
        {
            Logger.LogDebug($"TrendlineBreakDetector: Start DetectBreakoutOHLC, bitmap size: {cropped.Width}x{cropped.Height}");
            var debugPoints = new List<(int x, int y, bool isBreakout)>();
            Point bestP1 = Point.Empty, bestP2 = Point.Empty;
            List<Point> bestCluster = null;
            using (var analysisBmp = new Bitmap(cropped))
            {
                List<Point> whitePoints = new List<Point>();
                for (int y = 0; y < analysisBmp.Height; y++)
                    for (int x = 0; x < analysisBmp.Width; x++)
                    {
                        var pixel = analysisBmp.GetPixel(x, y);
                        if (pixel.R > 180 && pixel.G > 180 && pixel.B > 180)
                            whitePoints.Add(new Point(x, y));
                    }
                Logger.LogDebug($"TrendlineBreakDetector: Found {whitePoints.Count} white pixels");
                List<List<Point>> clusters = new List<List<Point>>();
                int maxDist = 10;
                foreach (var pt in whitePoints)
                {
                    bool added = false;
                    foreach (var cluster in clusters)
                    {
                        if (cluster.Any(p => Math.Abs(p.X - pt.X) < maxDist && Math.Abs(p.Y - pt.Y) < maxDist))
                        {
                            cluster.Add(pt);
                            added = true;
                            break;
                        }
                    }
                    if (!added)
                        clusters.Add(new List<Point> { pt });
                }
                Logger.LogDebug($"TrendlineBreakDetector: Found {clusters.Count} clusters");
                int cx = analysisBmp.Width / 2, cy = analysisBmp.Height / 2;
                double bestScore = double.MaxValue;
                foreach (var cluster in clusters)
                {
                    if (cluster.Count < 10) continue;
                    double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
                    foreach (var pt in cluster)
                    {
                        sumX += pt.X; sumY += pt.Y; sumXX += pt.X * pt.X; sumXY += pt.X * pt.Y;
                    }
                    double n = cluster.Count;
                    double k = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX + 1e-6);
                    double b = (sumY - k * sumX) / n;
                    int minX = cluster.Min(p => p.X), maxX = cluster.Max(p => p.X);
                    Point p1 = new Point(minX, (int)(k * minX + b));
                    Point p2 = new Point(maxX, (int)(k * maxX + b));
                    double len = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
                    double mx = (p1.X + p2.X) / 2.0, my = (p1.Y + p2.Y) / 2.0;
                    double distToCenter = Math.Sqrt((mx - cx) * (mx - cx) + (my - cy) * (my - cy));
                    double score = distToCenter / (len + 1e-6);
                    Logger.LogDebug($"Cluster: size={n}, len={len:F1}, center=({mx:F1},{my:F1}), score={score:F3}");
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCluster = cluster;
                        bestP1 = p1;
                        bestP2 = p2;
                    }
                }
                if (bestCluster == null)
                {
                    Logger.LogInfo("TrendlineBreakDetector: No white line cluster detected");
                    if (!string.IsNullOrEmpty(saveDebugPath))
                        SaveDebugVisualization(cropped, debugPoints, new Emgu.CV.Structure.LineSegment2D(new System.Drawing.Point(0,0), new System.Drawing.Point(0,0)), saveDebugPath, null, zoneWidth, zoneHeight, true, zoneOffset);
                    return TrendlineBreakResult.NoTrendline;
                }
                Logger.LogDebug($"TrendlineBreakDetector: Selected white line from ({bestP1.X},{bestP1.Y}) to ({bestP2.X},{bestP2.Y})");
                var trendline = new Emgu.CV.Structure.LineSegment2D(bestP1, bestP2);
                var left = bestP1.X < bestP2.X ? bestP1 : bestP2;
                var right = bestP1.X < bestP2.X ? bestP2 : bestP1;
                bool isDownward = left.Y < right.Y;
                string breakoutType = isDownward ? "BUY" : "SELL";
                Logger.LogDebug($"TrendlineBreakDetector: Trendline direction: {(isDownward ? "Downward (BUY)" : "Upward (SELL)" )}, searching for {breakoutType} breakout");
                (int bx, int by)? breakoutArrow = null;
                // Параметры для поиска пикселей
                int minPixelsForBreakout = (int)breakoutThreshold; // Минимальное количество пикселей для пробоя
                
                for (int i = 0; i < 100; i++)
                {
                    double t = 1.0 - i / 100.0;
                    int centerX = (int)(left.X + t * (right.X - left.X));
                    int centerY = (int)(left.Y + t * (right.Y - left.Y));
                    int offsetY = isDownward ? centerY - zoneOffset : centerY + zoneOffset;
                    
                    if (centerX < zoneWidth/2 || centerX >= analysisBmp.Width - zoneWidth/2 || 
                        offsetY < zoneHeight/2 || offsetY >= analysisBmp.Height - zoneHeight/2)
                        continue;
                    
                    int halfW = zoneWidth / 2;
                    int halfH = zoneHeight / 2;
                    
                                         // Ищем красные и зеленые пиксели в области
                     int redPixels = CountPixelsInArea(analysisBmp, centerX - halfW, centerX + halfW, 
                                                     offsetY - halfH, offsetY + halfH, true);
                     int greenPixels = CountPixelsInArea(analysisBmp, centerX - halfW, centerX + halfW, 
                                                       offsetY - halfH, offsetY + halfH, false);
                    
                                         // Логируем найденные пиксели для отладки
                     if (redPixels > 0 || greenPixels > 0)
                     {
                         Logger.LogTagDebug("Trendline Break Detector", $"Zone {i}: Found {redPixels} red pixels, {greenPixels} green pixels in area ({centerX - halfW},{offsetY - halfH}) to ({centerX + halfW},{offsetY + halfH})");
                     }
                     else
                     {
                         // Логируем, если пиксели не найдены
                         Logger.LogTagDebug("Trendline Break Detector", $"Zone {i}: No colored pixels found in area ({centerX - halfW},{offsetY - halfH}) to ({centerX + halfW},{offsetY + halfH})");
                     }
                    
                                         // Добавляем точки для отладки (показываем все найденные пиксели)
                     if (redPixels > 0 || greenPixels > 0)
                     {
                         // Добавляем точки в центре области для визуализации
                         debugPoints.Add((centerX, offsetY, false)); // false - это не брейкаут, просто найденные пиксели
                     }
                    
                                         // Проверяем наличие значимых пикселей
                     bool hasRedBreakout = redPixels >= minPixelsForBreakout;
                     bool hasGreenBreakout = greenPixels >= minPixelsForBreakout;
                     
                     if (isDownward && hasGreenBreakout)
                     {
                         Logger.LogTagInfo("Trendline Break Detector", $"Breakout UP detected at ({centerX},{offsetY}) - Green pixels: {greenPixels}");
                         if (hasRedBreakout)
                             Logger.LogTagError("Trendline Break Detector", "Impossible: Downward trendline cannot одновременно иметь SELL breakout (red) и BUY breakout (green). Это логическая ошибка.");
                         
                         // Добавляем точку брейкаута в правильном месте
                         debugPoints.Add((centerX, offsetY, true));
                         
                         if (!string.IsNullOrEmpty(saveDebugPath))
                             SaveDebugVisualization(cropped, debugPoints, trendline, saveDebugPath, (centerX, offsetY), zoneWidth, zoneHeight, true, zoneOffset);
                         return TrendlineBreakResult.BreakoutUp;
                     }
                     else if (!isDownward && hasRedBreakout)
                     {
                         Logger.LogTagInfo("Trendline Break Detector", $"Breakout DOWN detected at ({centerX},{offsetY}) - Red pixels: {redPixels}");
                         if (hasGreenBreakout)
                             Logger.LogTagError("Trendline Break Detector", "Impossible: Upward trendline cannot одновременно иметь BUY breakout (green) и SELL breakout (red). Это логическая ошибка.");
                         
                         // Добавляем точку брейкаута в правильном месте
                         debugPoints.Add((centerX, offsetY, true));
                         
                         if (!string.IsNullOrEmpty(saveDebugPath))
                             SaveDebugVisualization(cropped, debugPoints, trendline, saveDebugPath, (centerX, offsetY), zoneWidth, zoneHeight, true, zoneOffset);
                         return TrendlineBreakResult.BreakoutDown;
                     }
                     else if (isDownward && hasRedBreakout)
                     {
                         Logger.LogTagError("Trendline Break Detector", "Impossible: Downward trendline cannot have SELL breakout (red). This is a logic error.");
                     }
                     else if (!isDownward && hasGreenBreakout)
                     {
                         Logger.LogTagError("Trendline Break Detector", "Impossible: Upward trendline cannot have BUY breakout (green). This is a logic error.");
                     }
                }
                Logger.LogInfo($"TrendlineBreakDetector: Trendline detected, breakout NOT found. Type: {breakoutType}");
                if (!string.IsNullOrEmpty(saveDebugPath))
                    SaveDebugVisualization(cropped, debugPoints, trendline, saveDebugPath, breakoutArrow, zoneWidth, zoneHeight, true, zoneOffset);
            }
            return TrendlineBreakResult.NoBreakout;
        }

        public TrendlineBreakResult DetectBreakoutMACD(Bitmap cropped, string saveDebugPath, int zoneWidth, int zoneHeight, double breakoutThreshold, int zoneOffset)
        {
            return DetectBreakoutOHLC(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutThreshold, zoneOffset);
        }

        /// <summary>
        /// Подсчитывает количество красных или зеленых пикселей в заданной области
        /// </summary>
        /// <param name="bitmap">Изображение для анализа</param>
        /// <param name="startX">Начальная X координата области</param>
        /// <param name="endX">Конечная X координата области</param>
        /// <param name="startY">Начальная Y координата области</param>
        /// <param name="endY">Конечная Y координата области</param>
        /// <param name="isRed">Искать красные (true) или зеленые (false) пиксели</param>
        /// <returns>Количество найденных пикселей</returns>
        private int CountPixelsInArea(Bitmap bitmap, int startX, int endX, int startY, int endY, bool isRed)
        {
            int count = 0;
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
                        continue;
                    
                    var pixel = bitmap.GetPixel(x, y);
                    bool isTargetColor = isRed ? IsRed(pixel) : IsGreen(pixel);
                    
                    if (isTargetColor)
                    {
                        count++;
                    }
                }
            }
            return count;
        }



        private void SaveDebugVisualization(Bitmap cropped, List<(int x, int y, bool isBreakout)> points, LineSegment2D trendline, string path, (int bx, int by)? breakoutArrow = null, int zoneWidth = 5, int zoneHeight = 5, bool showOnlyBreakoutPixels = true, int zoneOffset = 5)
        {
            bool drawInterestZones = false; // Управление отрисовкой зон интереса (синие/оранжевые прямоугольники)
            
            // Находим область брейкаута для выделения желтой рамкой
            (int breakoutCenterX, int breakoutCenterY)? breakoutZone = null;
            if (points.Any(p => p.isBreakout))
            {
                var breakoutPoint = points.First(p => p.isBreakout);
                breakoutZone = (breakoutPoint.x, breakoutPoint.y);
            }
            
            using (var vis = new Bitmap(cropped))
            using (var g = Graphics.FromImage(vis))
            {
                // Линия тренда
                g.DrawLine(new Pen(Color.White, 2), trendline.P1, trendline.P2);
                
                // Определяем направление тренда для смещения зон
                var left = trendline.P1.X < trendline.P2.X ? trendline.P1 : trendline.P2;
                var right = trendline.P1.X < trendline.P2.X ? trendline.P2 : trendline.P1;
                bool isDownward = left.Y < right.Y;
                
                                 // Рисуем зоны проверки вдоль линии тренда (если включено)
                 if (drawInterestZones)
                 {
                     for (int i = 0; i < 100; i++)
                     {
                         double t = 1.0 - i / 100.0;
                         int centerX = (int)(left.X + t * (right.X - left.X));
                         int centerY = (int)(left.Y + t * (right.Y - left.Y));
                         
                         // Смещаем зону интереса на 3 пикселя вверх или вниз от линии
                         int offsetY = isDownward ? centerY - zoneOffset : centerY + zoneOffset;
                         
                         if (centerX < zoneWidth/2 || centerX >= vis.Width - zoneWidth/2 || 
                             offsetY < zoneHeight/2 || offsetY >= vis.Height - zoneHeight/2)
                             continue;
                         
                         // Рисуем прямоугольник зоны проверки
                         var zoneRect = new Rectangle(
                             centerX - zoneWidth/2, 
                             offsetY - zoneHeight/2, 
                             zoneWidth, 
                             zoneHeight
                         );
                         
                         // Цвет зоны зависит от направления тренда
                         var zoneColor = isDownward ? Color.FromArgb(80, Color.Blue) : Color.FromArgb(80, Color.Orange);
                         using (var brush = new SolidBrush(zoneColor))
                             g.FillRectangle(brush, zoneRect);
                         g.DrawRectangle(new Pen(zoneColor, 1), zoneRect);
                     }
                 }
                
                // Рисуем проверенные пиксели
                foreach (var pt in points)
                {
                    if (showOnlyBreakoutPixels && !pt.isBreakout)
                        continue;
                    
                    // Для пикселей брейкаута используем аква цвет
                    var color = pt.isBreakout ? Color.Aqua : Color.FromArgb(128, Color.Red);
                    var rect = new Rectangle(pt.x - 2, pt.y - 2, 5, 5);
                    using (var brush = new SolidBrush(Color.FromArgb(80, color)))
                        g.FillRectangle(brush, rect);
                    g.DrawRectangle(new Pen(color, 1), rect);
                }
                
                // Рисуем желтую рамку вокруг области брейкаута
                if (breakoutZone.HasValue)
                {
                    var (centerX, centerY) = breakoutZone.Value;
                    var breakoutRect = new Rectangle(
                        centerX - zoneWidth/2, 
                        centerY - zoneHeight/2, 
                        zoneWidth, 
                        zoneHeight
                    );
                    g.DrawRectangle(new Pen(Color.Yellow, 3), breakoutRect);
                }
                

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