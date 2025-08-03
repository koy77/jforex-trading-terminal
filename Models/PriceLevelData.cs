using System;

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
        /// Дополнительные данные (опционально)
        /// </summary>
        public string AdditionalData { get; set; }

        public override string ToString()
        {
            return $"Symbol: {Symbol}, Price: {Price}, Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
} 