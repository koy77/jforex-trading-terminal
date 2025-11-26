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
                int zoneWidth = 10;
                int zoneHeight = 10;  
                int breakoutPixelCount = 5;
                int zoneOffset = 3;
                return DetectBreakoutMACD(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutPixelCount, zoneOffset);
            }
            else
            {
                // OHLC (или по умолчанию)
                int zoneWidth = 20;
                int zoneHeight = 20;
                int breakoutPixelCount = 16;
                int zoneOffset = 5;
                return DetectBreakoutOHLC(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutPixelCount, zoneOffset);
            }
        }

        public TrendlineBreakResult DetectBreakoutOHLC(Bitmap cropped, string saveDebugPath, int zoneWidth, int zoneHeight, int breakoutPixelCount, int zoneOffset)
        {
            Logger.LogDebug($"TrendlineBreakDetector: Start DetectBreakoutOHLC, bitmap size: {cropped.Width}x{cropped.Height}");
            var debugPoints = new List<(int x, int y, bool isBreakout)>();
            Point bestP1 = Point.Empty, bestP2 = Point.Empty;
            List<Point> bestCluster = null;
            using (var analysisBmp = new Bitmap(cropped))
            {
                // Search for bright green trading stroke pixels (RGB approximately 38, 230, 0)
                // Bright green: G > 200, R < 50, B < 50
                List<Point> brightGreenPoints = new List<Point>();
                for (int y = 0; y < analysisBmp.Height; y++)
                    for (int x = 0; x < analysisBmp.Width; x++)
                    {
                        var pixel = analysisBmp.GetPixel(x, y);
                        // Bright green trading stroke: high green (>200), low red (<50), low blue (<50)
                        if (pixel.G > 200 && pixel.R < 50 && pixel.B < 50)
                            brightGreenPoints.Add(new Point(x, y));
                    }
                Logger.LogDebug($"TrendlineBreakDetector: Found {brightGreenPoints.Count} bright green trading stroke pixels");
                List<List<Point>> clusters = new List<List<Point>>();
                int maxDist = 10;
                foreach (var pt in brightGreenPoints)
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
                    Logger.LogInfo("TrendlineBreakDetector: No bright green trading stroke cluster detected");
                    if (!string.IsNullOrEmpty(saveDebugPath))
                        SaveDebugVisualization(cropped, debugPoints, new Emgu.CV.Structure.LineSegment2D(new System.Drawing.Point(0,0), new System.Drawing.Point(0,0)), saveDebugPath);
                    return TrendlineBreakResult.NoTrendline;
                }
                Logger.LogDebug($"TrendlineBreakDetector: Selected bright green trading stroke from ({bestP1.X},{bestP1.Y}) to ({bestP2.X},{bestP2.Y})");
                var trendline = new Emgu.CV.Structure.LineSegment2D(bestP1, bestP2);
                var left = bestP1.X < bestP2.X ? bestP1 : bestP2;
                var right = bestP1.X < bestP2.X ? bestP2 : bestP1;
                bool isDownward = left.Y < right.Y;
                string breakoutType = isDownward ? "BUY" : "SELL";
                Logger.LogDebug($"TrendlineBreakDetector: Trendline direction: {(isDownward ? "Downward (BUY)" : "Upward (SELL)" )}, searching for {breakoutType} breakout");
                (int bx, int by)? breakoutArrow = null;
                
                // Функция для определения, находится ли точка в правильной стороне от линии тренда
                bool IsPointInCorrectSide(int x, int y, bool isDownwardTrend)
                {
                    // Вычисляем Y-координату на линии тренда для данного X
                    double lineY = left.Y + (right.Y - left.Y) * (x - left.X) / (double)(right.X - left.X);
                    
                    if (isDownwardTrend)
                    {
                        // For downward trend we search for breakout up (green BUY pixels above the line)
                        return y < lineY;
                    }
                    else
                    {
                        // For upward trend we search for breakout down (red SELL pixels below the line)
                        return y > lineY;
                    }
                }
                
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
                    int redCount = 0, blueCount = 0, totalPixels = 0;
                    
                    for (int dx = 0; dx < zoneWidth; dx++)
                    {
                        for (int dy = 0; dy < zoneHeight; dy++)
                        {
                            int x = centerX - halfW + dx;
                            int y = offsetY - halfH + dy;
                            if (x < 0 || x >= analysisBmp.Width || y < 0 || y >= analysisBmp.Height)
                                continue;
                            
                            var pixel = analysisBmp.GetPixel(x, y);
                            totalPixels++;
                            
                            // Check all pixels in the zone (zone is already offset in correct direction)
                            // Count colors regardless of side check to catch breakouts more reliably
                            if (IsRed(pixel))
                                redCount++;
                            else if (IsBlue(pixel))
                                blueCount++;
                            
                            // Also verify pixel is on correct side for additional validation
                            bool isOnCorrectSide = IsPointInCorrectSide(x, y, isDownward);
                            debugPoints.Add((x, y, (IsRed(pixel) || IsBlue(pixel)) && isOnCorrectSide));
                        }
                    }
                    
                    if (totalPixels > 0)
                    {
                        // Debug logging for BUY breakout detection
                        if (isDownward && i % 10 == 0)
                        {
                            Logger.LogDebug($"TrendlineBreakDetector: Zone {i} at ({centerX},{offsetY}) - Green (BUY): {blueCount}, Red (SELL): {redCount}, Total: {totalPixels}, Required: {breakoutPixelCount}");
                        }
                        
                        // Check absolute pixel count instead of percentages
                        if (isDownward && blueCount >= breakoutPixelCount)
                        {
                            Logger.LogInfo($"TrendlineBreakDetector: Breakout UP detected at ({centerX},{offsetY}) - Green (BUY) pixels: {blueCount}");
                            if (redCount >= breakoutPixelCount)
                                Logger.LogError("Impossible: Downward trendline cannot simultaneously have SELL breakout (red) and BUY breakout (green). This is a logic error.");
                            if (!string.IsNullOrEmpty(saveDebugPath))
                                SaveDebugVisualization(cropped, debugPoints, trendline, saveDebugPath, (centerX, offsetY), zoneWidth, zoneHeight, true);
                            return TrendlineBreakResult.BreakoutUp;
                        }
                        else if (!isDownward && redCount >= breakoutPixelCount)
                        {
                            Logger.LogInfo($"TrendlineBreakDetector: Breakout DOWN detected at ({centerX},{offsetY}) - Red (SELL) pixels: {redCount}");
                            if (blueCount >= breakoutPixelCount)
                                Logger.LogError("Impossible: Upward trendline cannot simultaneously have BUY breakout (green) and SELL breakout (red). This is a logic error.");
                            if (!string.IsNullOrEmpty(saveDebugPath))
                                SaveDebugVisualization(cropped, debugPoints, trendline, saveDebugPath, (centerX, offsetY), zoneWidth, zoneHeight, true);
                            return TrendlineBreakResult.BreakoutDown;
                        }
                        else if (isDownward && redCount >= breakoutPixelCount)
                        {
                            Logger.LogError("Impossible: Downward trendline cannot have SELL breakout (red). This is a logic error.");
                        }
                        else if (!isDownward && blueCount >= breakoutPixelCount)
                        {
                            Logger.LogError("Impossible: Upward trendline cannot have BUY breakout (green). This is a logic error.");
                        }
                    }
                }
                Logger.LogInfo($"TrendlineBreakDetector: Trendline detected, breakout NOT found. Type: {breakoutType}");
                if (!string.IsNullOrEmpty(saveDebugPath))
                    SaveDebugVisualization(cropped, debugPoints, trendline, saveDebugPath, breakoutArrow, zoneWidth, zoneHeight);
            }
            return TrendlineBreakResult.NoBreakout;
        }

        public TrendlineBreakResult DetectBreakoutMACD(Bitmap cropped, string saveDebugPath, int zoneWidth, int zoneHeight, int breakoutPixelCount, int zoneOffset)
        {
            return DetectBreakoutOHLC(cropped, saveDebugPath, zoneWidth, zoneHeight, breakoutPixelCount, zoneOffset);
        }

        private void SaveDebugVisualization(Bitmap cropped, List<(int x, int y, bool isBreakout)> points, LineSegment2D trendline, string path, (int bx, int by)? breakoutArrow = null, int zoneWidth = 5, int zoneHeight = 5, bool showOnlyBreakoutPixels = true)
        {
            int zoneOffset = 3; // смещение зоны вверх/вниз от линии тренда
            
            using (var vis = new Bitmap(cropped))
            using (var g = Graphics.FromImage(vis))
            {
                // Линия тренда (bright green for trading stroke)
                g.DrawLine(new Pen(Color.FromArgb(38, 230, 0), 2), trendline.P1, trendline.P2);
                
                // Определяем направление тренда для смещения зон
                var left = trendline.P1.X < trendline.P2.X ? trendline.P1 : trendline.P2;
                var right = trendline.P1.X < trendline.P2.X ? trendline.P2 : trendline.P1;
                bool isDownward = left.Y < right.Y;
                
                // Рисуем зоны проверки вдоль линии тренда
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
                    
                    // Zone color depends on trend direction
                    var zoneColor = isDownward ? Color.FromArgb(80, Color.FromArgb(47, 170, 119)) : Color.FromArgb(80, Color.FromArgb(215, 55, 55));
                    using (var brush = new SolidBrush(zoneColor))
                        g.FillRectangle(brush, zoneRect);
                    g.DrawRectangle(new Pen(zoneColor, 1), zoneRect);
                }
                
                // Draw checked pixels
                foreach (var pt in points)
                {
                    if (showOnlyBreakoutPixels && !pt.isBreakout)
                        continue;
                    var color = pt.isBreakout ? Color.FromArgb(47, 170, 119) : Color.FromArgb(128, Color.FromArgb(215, 55, 55));
                    var rect = new Rectangle(pt.x - 2, pt.y - 2, 5, 5);
                    using (var brush = new SolidBrush(Color.FromArgb(80, color)))
                        g.FillRectangle(brush, rect);
                    g.DrawRectangle(new Pen(color, 1), rect);
                }
                
                // Стрелка breakout
                if (breakoutArrow.HasValue)
                {
                    var (bx, by) = breakoutArrow.Value;
                    // Используем уже объявленные выше переменные left, right, isDownward
                    int arrowOffset = 10;
                    int arrowStartY = isDownward ? by - arrowOffset : by + arrowOffset;

                    // Найдём ближайшую точку на линии
                    double minDist = double.MaxValue;
                    int arrowX = 0, arrowY = 0;
                    for (int i = 0; i < 100; i++)
                    {
                        double t = 1.0 - i / 100.0;
                        int x = (int)(trendline.P1.X + t * (trendline.P2.X - trendline.P1.X));
                        int y = (int)(trendline.P1.Y + t * (trendline.P2.Y - trendline.P1.Y));
                        double dist = Math.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
                        if (dist < minDist)
                        {
                            minDist = dist;
                            arrowX = x;
                            arrowY = y;
                        }
                    }
                    // Рисуем стрелку (жёлтая линия)
                    var pen = new Pen(Color.Yellow, 2);
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                    g.DrawLine(pen, bx, arrowStartY, arrowX, arrowY);
                }
                vis.Save(path);
            }
        }

        private bool IsBlue(System.Drawing.Color pixel)
        {
            // First, exclude bright green trading stroke pixels (RGB ~38, 230, 0) to avoid false breakouts
            // Bright green trading strokes: G > 200, R < 50, B < 50
            if (pixel.G > 200 && pixel.R < 50 && pixel.B < 50)
            {
                // This is a bright green trading stroke pixel, not a candle
                return false;
            }
            
            // Check for green BUY candle color: RGB(47, 170, 119) with tolerance
            // Target color: R=47, G=170, B=119
            // Allow deviation: ±30 for each channel
            int targetR = 47;
            int targetG = 170;
            int targetB = 119;
            int tolerance = 30;
            
            // Check if pixel is close to the target green BUY candle color
            bool rMatch = Math.Abs(pixel.R - targetR) <= tolerance;
            bool gMatch = Math.Abs(pixel.G - targetG) <= tolerance;
            bool bMatch = Math.Abs(pixel.B - targetB) <= tolerance;
            
            // All channels must be within tolerance
            return rMatch && gMatch && bMatch;
        }
        private bool IsRed(System.Drawing.Color pixel)
        {
            // Check for red SELL candle color: RGB(215, 55, 55) with tolerance
            // Target color: R=215, G=55, B=55
            // Allow deviation: ±30 for each channel
            int targetR = 215;
            int targetG = 55;
            int targetB = 55;
            int tolerance = 30;
            
            // Check if pixel is close to the target red SELL candle color
            bool rMatch = Math.Abs(pixel.R - targetR) <= tolerance;
            bool gMatch = Math.Abs(pixel.G - targetG) <= tolerance;
            bool bMatch = Math.Abs(pixel.B - targetB) <= tolerance;
            
            // All channels must be within tolerance
            return rMatch && gMatch && bMatch;
        }
    }
} 