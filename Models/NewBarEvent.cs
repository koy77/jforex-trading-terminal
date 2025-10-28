 using System;
using Newtonsoft.Json;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Модель данных события нового бара от JForex стратегии
    /// </summary>
    public class NewBarEvent
    {
        /// <summary>
        /// Торговый символ (например, EURUSD, GBPUSD)
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Ключ графика
        /// </summary>
        public string ChartKey { get; set; }

        /// <summary>
        /// Тип фида (TICK_BAR, TIME_BAR и т.д.)
        /// </summary>
        public string FeedType { get; set; }

        /// <summary>
        /// Размер тикового бара (для TICK_BAR)
        /// </summary>
        public int? TickBarSize { get; set; }

        /// <summary>
        /// Время начала бара
        /// </summary>
        public DateTime? BarStartTime { get; set; }

        /// <summary>
        /// Информация о графике
        /// </summary>
        public string ChartInfo { get; set; }

        /// <summary>
        /// Время тика
        /// </summary>
        public DateTime? TickTime { get; set; }

        /// <summary>
        /// Цена тика
        /// </summary>
        public decimal? TickPrice { get; set; }

        /// <summary>
        /// Временная метка получения данных
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Название стратегии
        /// </summary>
        public string Strategy { get; set; }

        /// <summary>
        /// Дополнительные данные (опционально) - JSON объект
        /// </summary>
        public object AdditionalData { get; set; }

        public NewBarEvent()
        {
        }

        public NewBarEvent(string symbol, string chartKey, string feedType = null, int? tickBarSize = null)
        {
            Symbol = symbol;
            ChartKey = chartKey;
            FeedType = feedType;
            TickBarSize = tickBarSize;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Проверяет, является ли бар тиковым
        /// </summary>
        public bool IsTickBar => !string.IsNullOrEmpty(FeedType) && 
                                FeedType.Equals("TICK_BAR", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Проверяет, соответствует ли символ указанному
        /// </summary>
        public bool MatchesSymbol(string targetSymbol)
        {
            return !string.IsNullOrEmpty(Symbol) && 
                   !string.IsNullOrEmpty(targetSymbol) && 
                   Symbol.Equals(targetSymbol, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, соответствует ли размер тика указанному
        /// </summary>
        public bool MatchesTickSize(int targetTickSize)
        {
            return TickBarSize.HasValue && TickBarSize.Value == targetTickSize;
        }

        /// <summary>
        /// Создает NewBarEvent из dynamic данных с валидацией и безопасным парсингом
        /// </summary>
        /// <param name="newBarData">Dynamic объект с данными нового бара</param>
        /// <returns>Созданный объект NewBarEvent или null при ошибке</returns>
        public static NewBarEvent CreateFromDynamicData(dynamic newBarData)
        {
            if (newBarData == null)
            {
                Logger.LogWarning("Cannot create NewBarEvent from null data");
                return null;
            }

            var newBarEvent = new NewBarEvent();
            
            try
            {
                // Безопасно извлекаем строковые поля
                newBarEvent.Symbol = SafeGetString(newBarData.symbol);
                newBarEvent.ChartKey = SafeGetString(newBarData.chartKey);
                newBarEvent.FeedType = SafeGetString(newBarData.feedType);
                newBarEvent.ChartInfo = SafeGetString(newBarData.chartInfo);
                newBarEvent.Strategy = SafeGetString(newBarData.strategy);
                
                // Сохраняем оригинальные данные
                newBarEvent.AdditionalData = newBarData;
                
                // Безопасно парсим числовые значения
                newBarEvent.TickBarSize = SafeGetInt(newBarData.tickBarSize);
                newBarEvent.TickPrice = SafeGetDecimal(newBarData.tickPrice);
                
                // Безопасно парсим временные значения
                newBarEvent.BarStartTime = SafeGetDateTime(newBarData.barStartTime);
                newBarEvent.TickTime = SafeGetDateTime(newBarData.tickTime);
                
                // Парсим timestamp, если не удалось - используем текущее время
                try
                {
                    if (newBarData.timestamp != null)
                    {
                        if (DateTime.TryParse(newBarData.timestamp.ToString(), out DateTime timestampResult))
                        {
                            newBarEvent.Timestamp = timestampResult;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to parse timestamp: {ex.Message}");
                }
                
                // Валидация обязательных полей
                if (string.IsNullOrEmpty(newBarEvent.Symbol))
                {
                    Logger.LogWarning("NewBarEvent creation failed: Symbol is required but not provided");
                    return null;
                }
                
                Logger.LogDebug($"Successfully created NewBarEvent: {newBarEvent}");
                return newBarEvent;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating NewBarEvent from dynamic data: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Безопасно извлекает строку из dynamic объекта
        /// </summary>
        private static string SafeGetString(dynamic value)
        {
            try
            {
                return value?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Безопасно извлекает int из dynamic объекта
        /// </summary>
        private static int? SafeGetInt(dynamic value)
        {
            try
            {
                if (value == null) return null;
                if (int.TryParse(value.ToString(), out int result))
                {
                    return result;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Безопасно извлекает decimal из dynamic объекта
        /// </summary>
        private static decimal? SafeGetDecimal(dynamic value)
        {
            try
            {
                if (value == null) return null;
                if (decimal.TryParse(value.ToString(), out decimal result))
                {
                    return result;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Безопасно извлекает DateTime из dynamic объекта
        /// </summary>
        private static DateTime? SafeGetDateTime(dynamic value)
        {
            try
            {
                if (value == null) return null;
                if (DateTime.TryParse(value.ToString(), out DateTime result))
                {
                    return result;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public override string ToString()
        {
            var tickInfo = IsTickBar ? $"TickSize={TickBarSize}" : $"FeedType={FeedType}";
            return $"NewBar: Symbol={Symbol}, ChartKey={ChartKey}, {tickInfo}, Time={Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
}
