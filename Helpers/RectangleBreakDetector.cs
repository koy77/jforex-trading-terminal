using System;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using ScreenCaptureApp.Services;
using ScreenCaptureApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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
        public RectangleBreakResult DetectBreakout(Bitmap image, CaptureData capture, string debugPath = null)
        {
            try
            {
                Logger.LogDebug($"RectangleBreakDetector: Starting breakout detection for capture ID={capture.ID}, Direction={capture.Direction}");
                
                // Находим границы прямоугольника (как в метаданных)
                int rightVerticalLineX = FindRightmostVerticalWhiteLine(image);
                if (rightVerticalLineX == -1)
                {
                    Logger.LogDebug($"RectangleBreakDetector: Primary method failed, trying alternative for capture ID={capture.ID}");
                    rightVerticalLineX = FindRightmostVerticalWhiteLineAlternative(image);
                    if (rightVerticalLineX == -1)
                    {
                        Logger.LogDebug($"RectangleBreakDetector: Both methods failed to find right vertical line for capture ID={capture.ID}");
                        return RectangleBreakResult.NoBreakout;
                    }
                }
                
                // Находим левую вертикальную линию
                int leftVerticalLineX = FindLeftmostVerticalWhiteLine(image);
                if (leftVerticalLineX == -1)
                {
                    Logger.LogDebug($"RectangleBreakDetector: Left vertical line not found for capture ID={capture.ID}");
                    return RectangleBreakResult.NoBreakout;
                }
                
                // Определяем левую границу поиска на основе метаданных
                int searchLeftBoundary = leftVerticalLineX; // По умолчанию до левой вертикальной линии
                
                if (!string.IsNullOrEmpty(capture.Meta))
                {
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<JsonElement>(capture.Meta);
                        if (metadata.TryGetProperty("breakoutX", out var breakoutXElement) && 
                            breakoutXElement.TryGetInt32(out int breakoutX) && 
                            breakoutX > 0)
                        {
                            // Вычисляем левую границу поиска: правая линия минус координата пробоя
                            searchLeftBoundary = rightVerticalLineX - breakoutX + 4;
                            Logger.LogDebug($"RectangleBreakDetector: Using metadata breakoutX={breakoutX}, searchLeftBoundary={searchLeftBoundary} (rightX={rightVerticalLineX} - breakoutX={breakoutX})");
                        }
                        else
                        {
                            Logger.LogDebug($"RectangleBreakDetector: No valid breakoutX in metadata or breakoutX=0, using leftVerticalLineX={leftVerticalLineX}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"RectangleBreakDetector: Error parsing metadata for capture ID={capture.ID}, using leftVerticalLineX={leftVerticalLineX}. Exception: {ex.Message}");
                    }
                }
                else
                {
                    Logger.LogDebug($"RectangleBreakDetector: No metadata available, using leftVerticalLineX={leftVerticalLineX}");
                }
                
                var (topY, bottomY) = FindHorizontalLines(image, rightVerticalLineX);
                if (topY == -1 || bottomY == -1)
                {
                    Logger.LogDebug($"RectangleBreakDetector: Horizontal lines not found for capture ID={capture.ID}");
                    return RectangleBreakResult.NoBreakout;
                }
                
                int rectangleHeight = bottomY - topY;
                Logger.LogDebug($"RectangleBreakDetector: Rectangle boundaries - rightX={rightVerticalLineX}, leftX={leftVerticalLineX}, topY={topY}, bottomY={bottomY}, height={rectangleHeight}");
                Logger.LogDebug($"RectangleBreakDetector: Image size: {image.Width}x{image.Height}");
                
                // Определяем область анализа в зависимости от направления
                int analysisStartY, analysisEndY;
                bool lookingForRedPixels;
                
                if (capture.Direction == "Up")
                {
                    // Анализируем верхнюю границу - ищем пробой вверх (зеленые пиксели)
                    analysisStartY = Math.Max(0, topY - rectangleHeight);
                    analysisEndY = topY;
                    lookingForRedPixels = false; // Ищем зеленые пиксели
                    Logger.LogDebug($"RectangleBreakDetector: Analyzing upper boundary for UP breakout (green pixels) from Y={analysisStartY} to Y={analysisEndY}");
                }
                else if (capture.Direction == "Down")
                {
                    // Анализируем нижнюю границу - ищем пробой вниз (красные пиксели)
                    analysisStartY = bottomY;
                    analysisEndY = Math.Min(image.Height - 1, bottomY + rectangleHeight);
                    lookingForRedPixels = true; // Ищем красные пиксели
                    Logger.LogDebug($"RectangleBreakDetector: Analyzing lower boundary for DOWN breakout (red pixels) from Y={analysisStartY} to Y={analysisEndY}");
                }
                else
                {
                    Logger.LogWarning($"RectangleBreakDetector: Unknown direction '{capture.Direction}' for capture ID={capture.ID}");
                    return RectangleBreakResult.NoBreakout;
                }
                
                // Ищем вертикальные кластеры в области анализа между правой линией и левой границей поиска
                var (breakoutDetected, clusterX, clusterStartY, clusterEndY) = DetectVerticalClustersInArea(image, rightVerticalLineX, searchLeftBoundary, analysisStartY, analysisEndY, lookingForRedPixels);
                
                if (breakoutDetected)
                {
                    var result = lookingForRedPixels ? RectangleBreakResult.BreakoutDown : RectangleBreakResult.BreakoutUp;
                    Logger.LogInfo($"RectangleBreakDetector: Breakout detected for capture ID={capture.ID}, result={result}");
                    
                    // Сохраняем отладочное изображение
                    if (!string.IsNullOrEmpty(debugPath))
                    {
                        SaveDebugVisualization(image, rightVerticalLineX, leftVerticalLineX, searchLeftBoundary, topY, bottomY, analysisStartY, analysisEndY, lookingForRedPixels, debugPath, result, clusterX, clusterStartY, clusterEndY);
                    }
                    
                    return result;
                }
                else
                {
                    Logger.LogDebug($"RectangleBreakDetector: No breakout detected for capture ID={capture.ID}");
                    
                    // Сохраняем отладочное изображение даже если пробой не найден
                    if (!string.IsNullOrEmpty(debugPath))
                    {
                        SaveDebugVisualization(image, rightVerticalLineX, leftVerticalLineX, searchLeftBoundary, topY, bottomY, analysisStartY, analysisEndY, lookingForRedPixels, debugPath, RectangleBreakResult.NoBreakout, -1, -1, -1);
                    }
                    
                    return RectangleBreakResult.NoBreakout;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RectangleBreakDetector: Error in DetectBreakout for capture ID={capture.ID}", ex);
                return RectangleBreakResult.NoBreakout;
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

        /// <summary>
        /// Анализирует изображение и строит мета-данные о найденных прямоугольниках
        /// Алгоритм: справа налево находит первую вертикальную белую линию,
        /// затем горизонтальные линии сверху и снизу, анализирует содержимое внутри
        /// </summary>
        public string BuildMetadata(Bitmap image, CaptureData capture, string debugPath = null)
        {
            try
            {
                Logger.LogDebug($"RectangleBreakDetector: Building metadata for image {image.Width}x{image.Height}");
                
                // Вызываем DetectBreakout для определения координаты пробоя
                var breakoutResult = DetectBreakout(image, capture, debugPath);
                
                int breakoutX = 0; // По умолчанию 0, если пробой не найден
                
                // Если найден пробой, вычисляем координату X
                if (breakoutResult == RectangleBreakResult.BreakoutUp || breakoutResult == RectangleBreakResult.BreakoutDown)
                {
                    // Находим границы прямоугольника
                    int rightVerticalLineX = FindRightmostVerticalWhiteLine(image);
                    if (rightVerticalLineX == -1)
                    {
                        rightVerticalLineX = FindRightmostVerticalWhiteLineAlternative(image);
                    }
                    
                    int leftVerticalLineX = FindLeftmostVerticalWhiteLine(image);
                    
                    if (rightVerticalLineX != -1 && leftVerticalLineX != -1)
                    {
                        var (topY, bottomY) = FindHorizontalLines(image, rightVerticalLineX);
                        
                        if (topY != -1 && bottomY != -1)
                        {
                            int rectangleHeight = bottomY - topY;
                            
                            // Определяем область анализа в зависимости от направления
                            int analysisStartY, analysisEndY;
                            bool lookingForRedPixels;
                            
                            if (capture.Direction == "Up")
                            {
                                analysisStartY = Math.Max(0, topY - rectangleHeight);
                                analysisEndY = topY;
                                lookingForRedPixels = false; // Ищем зеленые пиксели
                            }
                            else
                            {
                                analysisStartY = bottomY;
                                analysisEndY = Math.Min(image.Height - 1, bottomY + rectangleHeight);
                                lookingForRedPixels = true; // Ищем красные пиксели
                            }
                            
                            // Ищем кластер в области анализа
                            var (breakoutDetected, clusterX, clusterStartY, clusterEndY) = DetectVerticalClustersInArea(image, rightVerticalLineX, leftVerticalLineX, analysisStartY, analysisEndY, lookingForRedPixels);
                            
                            if (breakoutDetected && clusterX != -1)
                            {
                                // Вычисляем расстояние от правой линии до кластера
                                breakoutX = rightVerticalLineX - clusterX;
                                Logger.LogDebug($"RectangleBreakDetector: Breakout detected at clusterX={clusterX}, rightEdgeX={rightVerticalLineX}, breakoutX={breakoutX}");
                            }
                        }
                    }
                }
                
                // Собираем мета-данные только с координатой пробоя
                var metadata = new
                {
                    // Координата пробоя (дистанция от красной линии до места пробоя)
                    breakoutX = breakoutX
                };

                var jsonResult = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                Logger.LogDebug($"RectangleBreakDetector: Built metadata with breakoutX={breakoutX}, result={breakoutResult}");
                
                return jsonResult;
            }
            catch (Exception ex)
            {
                Logger.LogError($"RectangleBreakDetector: Error building metadata", ex);
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Ищет первую вертикальную белую линию справа налево (основной метод)
        /// </summary>
        private int FindRightmostVerticalWhiteLine(Bitmap image)
        {
            // Более гибкие параметры для поиска вертикальной линии
            int minWhitePixelsInLine = image.Height / 6; // Еще более мягкие требования
            int minConsecutiveWhitePixels = image.Height / 10; // Минимум подряд идущих белых пикселей
            int maxGaps = 5; // Максимальное количество разрывов в линии
            
            Logger.LogDebug($"RectangleBreakDetector: Searching for vertical line with minWhitePixels={minWhitePixelsInLine}, minConsecutive={minConsecutiveWhitePixels}");
            
            for (int x = image.Width - 1; x >= 0; x--)
            {
                int whitePixelsInColumn = 0;
                int consecutiveWhitePixels = 0;
                int maxConsecutiveInColumn = 0;
                int gaps = 0;
                bool lastWasWhite = false;
                
                for (int y = 0; y < image.Height; y++)
                {
                    var pixel = image.GetPixel(x, y);
                    bool isWhite = IsWhite(pixel);
                    
                    if (isWhite)
                    {
                        whitePixelsInColumn++;
                        consecutiveWhitePixels++;
                        maxConsecutiveInColumn = Math.Max(maxConsecutiveInColumn, consecutiveWhitePixels);
                        lastWasWhite = true;
                    }
                    else
                    {
                        if (lastWasWhite && consecutiveWhitePixels > 0)
                        {
                            gaps++;
                        }
                        consecutiveWhitePixels = 0;
                        lastWasWhite = false;
                    }
                }
                
                Logger.LogDebug($"RectangleBreakDetector: Column X={x}: whitePixels={whitePixelsInColumn}, maxConsecutive={maxConsecutiveInColumn}, gaps={gaps}");
                
                // Проверяем несколько условий для определения вертикальной линии
                bool hasEnoughWhitePixels = whitePixelsInColumn >= minWhitePixelsInLine;
                bool hasConsecutiveWhitePixels = maxConsecutiveInColumn >= minConsecutiveWhitePixels;
                bool hasReasonableGaps = gaps <= maxGaps;
                
                if (hasEnoughWhitePixels && hasConsecutiveWhitePixels && hasReasonableGaps)
                {
                    Logger.LogDebug($"RectangleBreakDetector: Found vertical line at X={x} (whitePixels={whitePixelsInColumn}, maxConsecutive={maxConsecutiveInColumn}, gaps={gaps})");
                    return x;
                }
            }
            
            Logger.LogWarning($"RectangleBreakDetector: No vertical line found with current parameters");
            return -1; // Линия не найдена
        }

        /// <summary>
        /// Альтернативный метод поиска вертикальной линии (более простой)
        /// </summary>
        private int FindRightmostVerticalWhiteLineAlternative(Bitmap image)
        {
            Logger.LogDebug($"RectangleBreakDetector: Using alternative method to find vertical line");
            
            // Ищем любую вертикальную линию с белыми пикселями
            int minWhitePixelsInLine = image.Height / 6; // Еще более мягкие требования
            
            for (int x = image.Width - 1; x >= 0; x--)
            {
                int whitePixelsInColumn = 0;
                int whiteSegments = 0;
                bool inWhiteSegment = false;
                
                for (int y = 0; y < image.Height; y++)
                {
                    var pixel = image.GetPixel(x, y);
                    bool isWhite = IsWhite(pixel);
                    
                    if (isWhite)
                    {
                        whitePixelsInColumn++;
                        if (!inWhiteSegment)
                        {
                            whiteSegments++;
                            inWhiteSegment = true;
                        }
                    }
                    else
                    {
                        inWhiteSegment = false;
                    }
                }
                
                Logger.LogDebug($"RectangleBreakDetector: Alternative - Column X={x}: whitePixels={whitePixelsInColumn}, whiteSegments={whiteSegments}");
                
                // Если есть достаточно белых пикселей и несколько сегментов
                if (whitePixelsInColumn >= minWhitePixelsInLine && whiteSegments >= 2)
                {
                    Logger.LogDebug($"RectangleBreakDetector: Alternative method found vertical line at X={x}");
                    return x;
                }
            }
            
            Logger.LogWarning($"RectangleBreakDetector: Alternative method also failed to find vertical line");
            return -1;
        }

        /// <summary>
        /// Ищет левую вертикальную белую линию слева направо
        /// </summary>
        private int FindLeftmostVerticalWhiteLine(Bitmap image)
        {
            // Более гибкие параметры для поиска вертикальной линии
            int minWhitePixelsInLine = image.Height / 6; // Еще более мягкие требования
            int minConsecutiveWhitePixels = image.Height / 10; // Минимум подряд идущих белых пикселей
            int maxGaps = 5; // Максимальное количество разрывов в линии
            
            Logger.LogDebug($"RectangleBreakDetector: Searching for left vertical line with minWhitePixels={minWhitePixelsInLine}, minConsecutive={minConsecutiveWhitePixels}");
            
            for (int x = 0; x < image.Width; x++)
            {
                int whitePixelsInColumn = 0;
                int consecutiveWhitePixels = 0;
                int maxConsecutiveInColumn = 0;
                int gaps = 0;
                bool lastWasWhite = false;
                
                for (int y = 0; y < image.Height; y++)
                {
                    var pixel = image.GetPixel(x, y);
                    bool isWhite = IsWhite(pixel);
                    
                    if (isWhite)
                    {
                        whitePixelsInColumn++;
                        consecutiveWhitePixels++;
                        maxConsecutiveInColumn = Math.Max(maxConsecutiveInColumn, consecutiveWhitePixels);
                        lastWasWhite = true;
                    }
                    else
                    {
                        if (lastWasWhite && consecutiveWhitePixels > 0)
                        {
                            gaps++;
                        }
                        consecutiveWhitePixels = 0;
                        lastWasWhite = false;
                    }
                }
                
                Logger.LogDebug($"RectangleBreakDetector: Column X={x}: whitePixels={whitePixelsInColumn}, maxConsecutive={maxConsecutiveInColumn}, gaps={gaps}");
                
                // Проверяем несколько условий для определения вертикальной линии
                bool hasEnoughWhitePixels = whitePixelsInColumn >= minWhitePixelsInLine;
                bool hasConsecutiveWhitePixels = maxConsecutiveInColumn >= minConsecutiveWhitePixels;
                bool hasReasonableGaps = gaps <= maxGaps;
                
                if (hasEnoughWhitePixels && hasConsecutiveWhitePixels && hasReasonableGaps)
                {
                    Logger.LogDebug($"RectangleBreakDetector: Found left vertical line at X={x} (whitePixels={whitePixelsInColumn}, maxConsecutive={maxConsecutiveInColumn}, gaps={gaps})");
                    return x;
                }
            }
            
            Logger.LogWarning($"RectangleBreakDetector: No left vertical line found with current parameters");
            return -1; // Линия не найдена
        }

        /// <summary>
        /// Ищет горизонтальные линии сверху и снизу
        /// </summary>
        private (int topY, int bottomY) FindHorizontalLines(Bitmap image, int verticalLineX)
        {
            int minWhitePixelsInLine = image.Width / 4; // Минимум белых пикселей для линии
            int topY = -1, bottomY = -1;
            
            // Ищем верхнюю горизонтальную линию
            for (int y = 0; y < image.Height / 2; y++)
            {
                int whitePixelsInRow = 0;
                for (int x = 0; x <= verticalLineX; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (IsWhite(pixel))
                    {
                        whitePixelsInRow++;
                    }
                }
                
                if (whitePixelsInRow >= minWhitePixelsInLine)
                {
                    topY = y;
                    break;
                }
            }
            
            // Ищем нижнюю горизонтальную линию
            for (int y = image.Height - 1; y >= image.Height / 2; y--)
            {
                int whitePixelsInRow = 0;
                for (int x = 0; x <= verticalLineX; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (IsWhite(pixel))
                    {
                        whitePixelsInRow++;
                    }
                }
                
                if (whitePixelsInRow >= minWhitePixelsInLine)
                {
                    bottomY = y;
                    break;
                }
            }
            
            return (topY, bottomY);
        }


        private bool IsWhite(System.Drawing.Color pixel)
        {
            // Более гибкое определение белых пикселей
            // Учитываем, что белые пиксели могут быть не идеально белыми
            int minWhite = 120; // Еще более мягкий порог
            int maxDiff = 40; // Максимальная разница между RGB каналами
            
            bool isBright = pixel.R >= minWhite && pixel.G >= minWhite && pixel.B >= minWhite;
            bool isBalanced = Math.Abs(pixel.R - pixel.G) <= maxDiff && 
                             Math.Abs(pixel.R - pixel.B) <= maxDiff && 
                             Math.Abs(pixel.G - pixel.B) <= maxDiff;
            
            return isBright && isBalanced;
        }

        /// <summary>
        /// Создает отладочное изображение с выделенными областями (без текста)
        /// </summary>
        private void CreateMetadataDebugImage(Bitmap originalImage, int rightEdgeX, int topY, int bottomY, int distanceToContent, object contentAnalysis, string debugPath)
        {
            try
            {
                using (var debugImage = new Bitmap(originalImage))
                using (var g = Graphics.FromImage(debugImage))
                {
                    // Настройка качества отрисовки
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // 1. Выделяем правую вертикальную линию (красная линия)
                    using (var redPen = new Pen(Color.Red, 2))
                    {
                        g.DrawLine(redPen, rightEdgeX, 0, rightEdgeX, originalImage.Height);
                    }

                    // 2. Выделяем верхнюю и нижнюю горизонтальные линии (синие линии)
                    using (var bluePen = new Pen(Color.Blue, 2))
                    {
                        g.DrawLine(bluePen, 0, topY, rightEdgeX, topY);
                        g.DrawLine(bluePen, 0, bottomY, rightEdgeX, bottomY);
                    }

                    // 3. Выделяем область прямоугольника (полупрозрачный желтый)
                    using (var yellowBrush = new SolidBrush(Color.FromArgb(80, Color.Yellow)))
                    {
                        g.FillRectangle(yellowBrush, 0, topY, rightEdgeX, bottomY - topY);
                    }

                    // 4. Выделяем область содержимого (полупрозрачный зеленый)
                    int contentRightEdgeX = rightEdgeX - distanceToContent;
                    using (var greenBrush = new SolidBrush(Color.FromArgb(80, Color.Lime)))
                    {
                        g.FillRectangle(greenBrush, 0, topY + 1, contentRightEdgeX, bottomY - topY - 1);
                    }

                    // 5. Выделяем расстояние до содержимого (полупрозрачный оранжевый)
                    using (var orangeBrush = new SolidBrush(Color.FromArgb(80, Color.Orange)))
                    {
                        g.FillRectangle(orangeBrush, contentRightEdgeX, topY + 1, distanceToContent, bottomY - topY - 1);
                    }

                    // Сохраняем изображение
                    debugImage.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                    Logger.LogDebug($"RectangleBreakDetector: Saved metadata debug image to {debugPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RectangleBreakDetector: Error creating debug image", ex);
            }
        }

        /// <summary>
        /// Ищет вертикальные кластеры пикселей в заданной области для определения пробоя
        /// </summary>
        private (bool found, int clusterX, int clusterStartY, int clusterEndY) DetectVerticalClustersInArea(Bitmap image, int rightEdgeX, int leftEdgeX, int startY, int endY, bool lookingForRedPixels)
        {
            try
            {
                Logger.LogDebug($"RectangleBreakDetector: Searching for vertical clusters in area: X={leftEdgeX} to {rightEdgeX}, Y={startY} to {endY}, lookingForRed={lookingForRedPixels}");
                
                int minClusterHeight = 3; // Минимальная высота кластера
                
                // Ищем кластеры в области анализа справа налево (от красной линии влево до левой линии)
                for (int x = rightEdgeX; x >= leftEdgeX; x--)
                {
                    int clusterHeight = 0;
                    int maxClusterHeightInColumn = 0;
                    int clusterStartY = -1;
                    int clusterEndY = -1;
                    int currentClusterStartY = -1;
                    
                    for (int y = startY; y <= endY; y++)
                    {
                        if (y < 0 || y >= image.Height || x >= image.Width) continue;
                        
                        var pixel = image.GetPixel(x, y);
                        bool isTargetColor = lookingForRedPixels ? IsRed(pixel) : IsGreen(pixel);
                        
                        if (isTargetColor)
                        {
                            if (currentClusterStartY == -1)
                            {
                                currentClusterStartY = y; // Начало нового кластера
                            }
                            clusterHeight++;
                        }
                        else
                        {
                            // Конец кластера
                            if (clusterHeight > maxClusterHeightInColumn)
                            {
                                maxClusterHeightInColumn = clusterHeight;
                                clusterStartY = currentClusterStartY;
                                clusterEndY = y - 1;
                            }
                            clusterHeight = 0;
                            currentClusterStartY = -1;
                        }
                    }
                    
                    // Проверяем последний кластер в колонке
                    if (clusterHeight > maxClusterHeightInColumn)
                    {
                        maxClusterHeightInColumn = clusterHeight;
                        clusterStartY = currentClusterStartY;
                        clusterEndY = endY;
                    }
                    
                    // Проверяем, есть ли достаточно высокий кластер в этой колонке
                    if (maxClusterHeightInColumn >= minClusterHeight)
                    {
                        Logger.LogDebug($"RectangleBreakDetector: Found vertical cluster at X={x}, height={maxClusterHeightInColumn}, startY={clusterStartY}, endY={clusterEndY}, color={(lookingForRedPixels ? "red" : "green")}");
                        return (true, x, clusterStartY, clusterEndY); // Найден подходящий кластер
                    }
                }
                
                Logger.LogDebug($"RectangleBreakDetector: No vertical clusters found in analysis area");
                return (false, -1, -1, -1);
            }
            catch (Exception ex)
            {
                Logger.LogError($"RectangleBreakDetector: Error detecting vertical clusters", ex);
                return (false, -1, -1, -1);
            }
        }

        /// <summary>
        /// Создает отладочное изображение для визуализации процесса поиска пробоя
        /// </summary>
        private void SaveDebugVisualization(Bitmap originalImage, int rightEdgeX, int leftEdgeX, int searchLeftBoundary, int topY, int bottomY, int analysisStartY, int analysisEndY, bool lookingForRedPixels, string debugPath, RectangleBreakResult result, int clusterX = -1, int clusterStartY = -1, int clusterEndY = -1)
        {
            try
            {
                using (var debugImage = new Bitmap(originalImage))
                using (var g = Graphics.FromImage(debugImage))
                {
                    // Настройка качества отрисовки
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // 1. Выделяем правую вертикальную линию (красная линия)
                    using (var redPen = new Pen(Color.Red, 2))
                    {
                        g.DrawLine(redPen, rightEdgeX, 0, rightEdgeX, originalImage.Height);
                    }

                    // 2. Выделяем левую вертикальную линию (зеленая линия)
                    using (var greenPen = new Pen(Color.Lime, 2))
                    {
                        g.DrawLine(greenPen, leftEdgeX, 0, leftEdgeX, originalImage.Height);
                    }

                    // 3. Выделяем левую границу поиска (красная линия)
                    if (searchLeftBoundary != leftEdgeX)
                    {
                        using (var redPen = new Pen(Color.Red, 2))
                        {
                            g.DrawLine(redPen, searchLeftBoundary, 0, searchLeftBoundary, originalImage.Height);
                        }
                    }

                    // 4. Выделяем верхнюю и нижнюю горизонтальные линии (синие линии)
                    using (var bluePen = new Pen(Color.Blue, 2))
                    {
                        g.DrawLine(bluePen, leftEdgeX, topY, rightEdgeX, topY);
                        g.DrawLine(bluePen, leftEdgeX, bottomY, rightEdgeX, bottomY);
                    }

                    // 5. Выделяем область прямоугольника между линиями (полупрозрачный желтый)
                    using (var yellowBrush = new SolidBrush(Color.FromArgb(80, Color.Yellow)))
                    {
                        g.FillRectangle(yellowBrush, leftEdgeX, topY, rightEdgeX - leftEdgeX, bottomY - topY);
                    }

                    // 6. Выделяем область анализа пробоя (полупрозрачный оранжевый)
                    using (var orangeBrush = new SolidBrush(Color.FromArgb(60, Color.Orange)))
                    {
                        g.FillRectangle(orangeBrush, searchLeftBoundary, analysisStartY, rightEdgeX - searchLeftBoundary, analysisEndY - analysisStartY);
                    }

                    // 7. Рисуем границы области анализа (зеленая рамка)
                    using (var greenPen = new Pen(Color.Lime, 2))
                    {
                        g.DrawRectangle(greenPen, searchLeftBoundary, analysisStartY, rightEdgeX - searchLeftBoundary, analysisEndY - analysisStartY);
                    }

                    // 8. Показываем все найденные пиксели целевого цвета в области анализа
                    var targetColor = lookingForRedPixels ? Color.Red : Color.Green;
                    var targetBrush = new SolidBrush(Color.FromArgb(120, targetColor));
                    
                    for (int x = searchLeftBoundary; x <= rightEdgeX; x++)
                    {
                        for (int y = analysisStartY; y <= analysisEndY; y++)
                        {
                            if (y < 0 || y >= originalImage.Height || x >= originalImage.Width) continue;
                            
                            var pixel = originalImage.GetPixel(x, y);
                            bool isTargetColor = lookingForRedPixels ? IsRed(pixel) : IsGreen(pixel);
                            
                            if (isTargetColor)
                            {
                                g.FillRectangle(targetBrush, x - 1, y - 1, 3, 3);
                            }
                        }
                    }

                    // 9. Выделяем жёлтым цветом найденный кластер, который спровоцировал пробой
                    if (clusterX >= 0 && clusterStartY >= 0 && clusterEndY >= 0)
                    {
                        using (var yellowBrush = new SolidBrush(Color.FromArgb(150, Color.Yellow)))
                        {
                            int clusterWidth = 3; // Ширина выделения кластера
                            int clusterHeight = clusterEndY - clusterStartY + 1;
                            g.FillRectangle(yellowBrush, clusterX - clusterWidth/2, clusterStartY, clusterWidth, clusterHeight);
                        }
                        
                        // Рисуем рамку вокруг кластера
                        using (var yellowPen = new Pen(Color.Yellow, 2))
                        {
                            int clusterWidth = 3;
                            int clusterHeight = clusterEndY - clusterStartY + 1;
                            g.DrawRectangle(yellowPen, clusterX - clusterWidth/2, clusterStartY, clusterWidth, clusterHeight);
                        }
                    }

                    // 7. Текстовые надписи убраны по запросу пользователя

                    // Сохраняем изображение
                    debugImage.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                    Logger.LogDebug($"RectangleBreakDetector: Saved breakout debug visualization to {debugPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RectangleBreakDetector: Error creating breakout debug visualization", ex);
            }
        }
    }
} 
