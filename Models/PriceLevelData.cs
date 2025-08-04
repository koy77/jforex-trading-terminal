using System;
using Newtonsoft.Json;

namespace ScreenCaptureApp.Models
{


    /// <summary>
    /// Модель данных ценового уровня от GForex
    /// </summary>
    public class PriceLevelData
    {
        /// <summary>
        /// Торговый символ (например, EURUSD, GBPUSD)
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Цена уровня
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Временная метка получения данных
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Дополнительные данные (опционально) - JSON объект
        /// </summary>
        public object AdditionalData { get; set; }

        /// <summary>
        /// Тип ценового уровня (например, "support", "resistance", "breakout", "entry", "exit")
        /// </summary>
        public string Type { get; set; }

        public override string ToString()
        {
            return $"Symbol: {Symbol}, Price: {Price}, Type: {Type}, Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
} 