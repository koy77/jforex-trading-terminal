using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Модель для хранения данных паттерна создания объекта трейдинга
    /// </summary>
    public class TradingPatternData
    {
        /// <summary>
        /// Первая точка клика (X, Y)
        /// </summary>
        public TradingPoint FirstClick { get; set; }
        
        /// <summary>
        /// Вторая точка клика (X, Y)
        /// </summary>
        public TradingPoint SecondClick { get; set; }
        
        /// <summary>
        /// Handle окна, на котором был создан паттерн
        /// </summary>
        public IntPtr WindowHandle { get; set; }
        
        /// <summary>
        /// Символ, связанный с окном
        /// </summary>
        public string Symbol { get; set; }
        
        /// <summary>
        /// Время создания паттерна
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Статус паттерна
        /// </summary>
        public TradingPatternStatus Status { get; set; }
        
        /// <summary>
        /// Флаг, указывающий что это наклонный паттерн (для кнопки A)
        /// </summary>
        public bool IsInclined { get; set; }
        
        public TradingPatternData()
        {
            CreatedAt = DateTime.Now;
            Status = TradingPatternStatus.InProgress;
            IsInclined = false;
        }
    }
    
    /// <summary>
    /// Статус паттерна трейдинга
    /// </summary>
    public enum TradingPatternStatus
    {
        /// <summary>
        /// В процессе создания (ожидание кликов)
        /// </summary>
        InProgress,
        
        /// <summary>
        /// Завершен (получены оба клика)
        /// </summary>
        Completed,
        
        /// <summary>
        /// Отменен (нажата Escape)
        /// </summary>
        Cancelled
    }
    
    /// <summary>
    /// Простая структура для хранения координат точки трейдинга
    /// </summary>
    public struct TradingPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
        
        public TradingPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
} 