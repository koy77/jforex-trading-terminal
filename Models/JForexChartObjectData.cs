using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Типы JForex объектов
    /// </summary>
    public enum JForexChartObjectType
    {
        PriceMarker,
        Rectangle,
        ShortLine
    }

    /// <summary>
    /// Данные JForex объекта
    /// </summary>
    public class JForexChartObjectData
    {
        /// <summary>
        /// Торговый символ
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Цена объекта
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Тип объекта
        /// </summary>
        public JForexChartObjectType ObjectType { get; set; }

        /// <summary>
        /// Полное имя класса объекта
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Временная метка
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Дополнительные данные
        /// </summary>
        public string AdditionalData { get; set; }

        public JForexChartObjectData()
        {
        }

        public JForexChartObjectData(string symbol, decimal price, JForexChartObjectType objectType, string className, string additionalData = null)
        {
            Symbol = symbol;
            Price = price;
            ObjectType = objectType;
            ClassName = className;
            AdditionalData = additionalData;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Определяет тип объекта по имени класса
        /// </summary>
        public static JForexChartObjectType GetObjectTypeFromClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return JForexChartObjectType.PriceMarker; // По умолчанию

            if (className.Contains("PriceMarkerChartObject"))
                return JForexChartObjectType.PriceMarker;
            else if (className.Contains("RectangleChartObject"))
                return JForexChartObjectType.Rectangle;
            else if (className.Contains("ShortLineChartObject"))
                return JForexChartObjectType.ShortLine;
            else
                return JForexChartObjectType.PriceMarker; // По умолчанию
        }

        public override string ToString()
        {
            return $"JForexChartObject: {ObjectType} | Symbol: {Symbol} | Price: {Price} | Class: {ClassName} | Time: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
} 