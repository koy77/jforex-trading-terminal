using System;
using System.Collections.Generic;

namespace ScreenCaptureApp.Models
{
    public enum DirectionType
    {
        Up,
        Down
    }

    public class CaptureData
    {
        public string ID { get; set; } = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Handle { get; set; }
        public int Monitor { get; set; }
        public string Timestamp { get; set; }
        public string ScreenshotPath { get; set; }
        public string Symbol { get; set; }
        public double Risk { get; set; }
        public bool IsFired { get; set; } = false;
        public bool IsSkipped { get; set; } = false;
        public string Source { get; set; } = "window"; // "window" или "trading_canvas"
        public string Broker { get; set; } // Название брокера на момент создания Capture
        public int Duration { get; set; } // Длительность (секунд, миллисекунд и т.д.)
        public string Model { get; set; } // "OHLC" или "MACD"
        public string Mt4Order { get; set; } // MT4 order information as JSON string
        public string Period { get; set; } // Период, парсится из заголовка окна
        public string Direction { get; set; } // "Up" или "Down"
        public string Meta { get; set; } = ""; // JSON мета-данные
    }

    public class SymbolData
    {
        public string Symbol { get; set; }
        public double RiskPercent { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastUpdated { get; set; }
    }

    public class ToolbarSettings
    {
        public long Handle { get; set; }
        public double Risk { get; set; }
        public int Duration { get; set; }
        public BrokerType Broker { get; set; }
    }

    public class CaptureDbRoot
    {
        public List<CaptureData> Captures { get; set; } = new List<CaptureData>();
        public List<SymbolData> Symbols { get; set; } = new List<SymbolData>();
    }
}