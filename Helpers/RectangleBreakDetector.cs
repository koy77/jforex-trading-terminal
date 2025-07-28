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
        public string BuildMetadata(Bitmap image, string debugPath = null)
        {
            try
            {
                Logger.LogDebug($"RectangleBreakDetector: Building metadata for image {image.Width}x{image.Height}");
                
                // 1. Находим правую вертикальную линию (справа налево)
                int rightVerticalLineX = FindRightmostVerticalWhiteLine(image);
                if (rightVerticalLineX == -1)
                {
                    Logger.LogWarning($"RectangleBreakDetector: Right vertical line not found, trying alternative method");
                    // Попробуем альтернативный метод поиска
                    rightVerticalLineX = FindRightmostVerticalWhiteLineAlternative(image);
                    if (rightVerticalLineX == -1)
                    {
                        Logger.LogWarning($"RectangleBreakDetector: Right vertical line not found with alternative method");
                        return JsonSerializer.Serialize(new { error = "Right vertical line not found" });
                    }
                }
                
                Logger.LogDebug($"RectangleBreakDetector: Found right vertical line at X={rightVerticalLineX}");
                
                // 2. Находим верхнюю и нижнюю горизонтальные линии
                var (topY, bottomY) = FindHorizontalLines(image, rightVerticalLineX);
                if (topY == -1 || bottomY == -1)
                {
                    Logger.LogWarning($"RectangleBreakDetector: Horizontal lines not found. TopY={topY}, BottomY={bottomY}");
                    return JsonSerializer.Serialize(new { error = "Horizontal lines not found" });
                }
                
                Logger.LogDebug($"RectangleBreakDetector: Found horizontal lines at TopY={topY}, BottomY={bottomY}");
                
                // 3. Анализируем содержимое внутри прямоугольника
                var contentAnalysis = AnalyzeRectangleContent(image, rightVerticalLineX, topY, bottomY);
                
                // 4. Находим расстояние от правой линии до содержимого
                int distanceToContent = FindDistanceToContent(image, rightVerticalLineX, topY, bottomY);
                
                Logger.LogDebug($"RectangleBreakDetector: Distance to content = {distanceToContent} pixels");
                
                // 5. Создаем отладочное изображение, если указан путь
                if (!string.IsNullOrEmpty(debugPath))
                {
                    CreateMetadataDebugImage(image, rightVerticalLineX, topY, bottomY, distanceToContent, contentAnalysis, debugPath);
                }
                
                // 6. Собираем все мета-данные
                var metadata = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    imageSize = new { width = image.Width, height = image.Height },
                    
                    // Координаты прямоугольника
                    rectangle = new
                    {
                        rightEdgeX = rightVerticalLineX,
                        topY = topY,
                        bottomY = bottomY,
                        width = rightVerticalLineX,
                        height = bottomY - topY
                    },
                    
                    // Анализ содержимого
                    content = contentAnalysis,
                    
                    // Основное значение - расстояние от правой линии до содержимого
                    distanceToContent = distanceToContent,
                    
                    // Дополнительная информация
                    contentRightEdgeX = rightVerticalLineX - distanceToContent
                };

                var jsonResult = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                Logger.LogDebug($"RectangleBreakDetector: Built metadata with distanceToContent={distanceToContent}");
                
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
            int minWhitePixelsInLine = image.Height / 4; // Уменьшили требование
            int minConsecutiveWhitePixels = image.Height / 8; // Минимум подряд идущих белых пикселей
            int maxGaps = 3; // Максимальное количество разрывов в линии
            
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

        /// <summary>
        /// Анализирует содержимое внутри прямоугольника
        /// </summary>
        private object AnalyzeRectangleContent(Bitmap image, int rightEdgeX, int topY, int bottomY)
        {
            int redCount = 0, greenCount = 0, totalPixels = 0;
            var redPoints = new List<Point>();
            var greenPoints = new List<Point>();
            
            // Анализируем область внутри прямоугольника
            for (int y = topY + 1; y < bottomY; y++)
            {
                for (int x = 0; x < rightEdgeX; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    totalPixels++;
                    
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
            
            // Находим границы содержимого
            int leftmostContentX = redPoints.Count > 0 || greenPoints.Count > 0 ? 
                Math.Min(redPoints.Count > 0 ? redPoints.Min(p => p.X) : int.MaxValue, 
                         greenPoints.Count > 0 ? greenPoints.Min(p => p.X) : int.MaxValue) : -1;
            
            int rightmostContentX = redPoints.Count > 0 || greenPoints.Count > 0 ? 
                Math.Max(redPoints.Count > 0 ? redPoints.Max(p => p.X) : -1, 
                         greenPoints.Count > 0 ? greenPoints.Max(p => p.X) : -1) : -1;
            
            return new
            {
                totalPixels = totalPixels,
                redPixels = redCount,
                greenPixels = greenCount,
                redRatio = totalPixels > 0 ? (double)redCount / totalPixels : 0,
                greenRatio = totalPixels > 0 ? (double)greenCount / totalPixels : 0,
                redPoints = redPoints.Count,
                greenPoints = greenPoints.Count,
                contentBounds = new
                {
                    leftmostX = leftmostContentX,
                    rightmostX = rightmostContentX,
                    hasContent = redCount > 0 || greenCount > 0
                }
            };
        }

        /// <summary>
        /// Находит расстояние от правой линии до содержимого
        /// </summary>
        private int FindDistanceToContent(Bitmap image, int rightEdgeX, int topY, int bottomY)
        {
            // Ищем самый правый красный или зеленый пиксель внутри прямоугольника
            int rightmostContentX = -1;
            
            // Проходим по всем строкам внутри прямоугольника
            for (int y = topY + 1; y < bottomY; y++)
            {
                // Ищем справа налево (от правой линии к левому краю)
                for (int x = rightEdgeX - 1; x >= 0; x--)
                {
                    var pixel = image.GetPixel(x, y);
                    if (IsRed(pixel) || IsGreen(pixel))
                    {
                        // Нашли цветной пиксель, обновляем самую правую позицию
                        if (x > rightmostContentX)
                        {
                            rightmostContentX = x;
                        }
                        break; // Переходим к следующей строке
                    }
                }
            }
            
            if (rightmostContentX == -1)
            {
                Logger.LogDebug($"RectangleBreakDetector: No content found in rectangle");
                return 0; // Содержимое не найдено
            }
            
            // Расстояние от правой линии до содержимого
            int distance = rightEdgeX - rightmostContentX;
            Logger.LogDebug($"RectangleBreakDetector: Rightmost content at X={rightmostContentX}, distance to right edge={distance}");
            
            return distance;
        }



        private bool IsWhite(System.Drawing.Color pixel)
        {
            // Более точное определение белых пикселей
            // Учитываем, что белые пиксели могут быть не идеально белыми
            int minWhite = 150; // Снизили порог
            int maxDiff = 30; // Максимальная разница между RGB каналами
            
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
    }
} 