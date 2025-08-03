using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Данные события нового ценового уровня
    /// </summary>
    public class PriceLevelEventData
    {
        /// <summary>
        /// Название ценового уровня
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Торговый символ
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Значение ценового уровня
        /// </summary>
        public decimal LevelValue { get; set; }

        /// <summary>
        /// Тип уровня (support, resistance, breakout, entry, exit)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Временная метка события
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Дополнительные данные
        /// </summary>
        public string AdditionalData { get; set; }

        public PriceLevelEventData()
        {
        }

        public PriceLevelEventData(string name, string symbol, decimal levelValue, string type = "unknown", string additionalData = null)
        {
            Name = name;
            Symbol = symbol;
            LevelValue = levelValue;
            Type = type;
            AdditionalData = additionalData;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"PriceLevel: {Name} | Symbol: {Symbol} | Value: {LevelValue} | Type: {Type} | Time: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
} 