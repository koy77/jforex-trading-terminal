using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Модель для хранения данных паттерна отложенной сделки
    /// </summary>
    public class PendingOrderPatternData
    {
        /// <summary>
        /// Первая точка клика (X, Y) - начало паттерна
        /// </summary>
        public TradingPoint FirstClick { get; set; }
        
        /// <summary>
        /// Вторая точка клика (X, Y) - конец паттерна
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
        /// Уникальный ID для этой отложенной сделки
        /// </summary>
        public string OrderId { get; set; }
        
        /// <summary>
        /// Статус паттерна
        /// </summary>
        public PendingOrderPatternStatus Status { get; set; }
        
        /// <summary>
        /// Путь к первому скриншоту
        /// </summary>
        public string FirstScreenshotPath { get; set; }
        
        /// <summary>
        /// Путь ко второму скриншоту
        /// </summary>
        public string SecondScreenshotPath { get; set; }
        
        /// <summary>
        /// Анализированные цены из скриншотов
        /// </summary>
        public PendingOrderPrices Prices { get; set; }
        
        /// <summary>
        /// Цена входа (извлекается из первого скриншота)
        /// </summary>
        public double EntryPrice { get; set; }
        
        public PendingOrderPatternData()
        {
            CreatedAt = DateTime.Now;
            Status = PendingOrderPatternStatus.InProgress;
            Prices = new PendingOrderPrices();
        }
    }
    
    /// <summary>
    /// Статус паттерна отложенной сделки
    /// </summary>
    public enum PendingOrderPatternStatus
    {
        /// <summary>
        /// В процессе создания (ожидание кликов)
        /// </summary>
        InProgress,
        
        /// <summary>
        /// Завершен (получены оба клика и скриншоты)
        /// </summary>
        Completed,
        
        /// <summary>
        /// Отменен (нажата Escape)
        /// </summary>
        Cancelled
    }
    
    /// <summary>
    /// Цены, извлеченные из скриншотов отложенной сделки
    /// </summary>
    public class PendingOrderPrices
    {
        /// <summary>
        /// Цена входа (из первого скриншота)
        /// </summary>
        public double EntryPrice { get; set; }
        
        /// <summary>
        /// Цена стоп-лосса (из второго скриншота)
        /// </summary>
        public double StopLossPrice { get; set; }
        
        /// <summary>
        /// Направление сделки (Buy/Sell)
        /// </summary>
        public string Direction { get; set; }
        
        /// <summary>
        /// Размер лота
        /// </summary>
        public double LotSize { get; set; }
        
        /// <summary>
        /// Комментарий к сделке
        /// </summary>
        public string Comment { get; set; }
    }
} 