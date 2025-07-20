using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Настройки символа, хранящиеся в памяти (DI Container)
    /// </summary>
    public class SymbolSettings
    {
        public string Symbol { get; set; }
        public double DefaultRisk { get; set; }
        public int DefaultDuration { get; set; }
        
        public SymbolSettings(string symbol, double defaultRisk = 1.0, int defaultDuration = 2)
        {
            Symbol = symbol;
            DefaultRisk = defaultRisk;
            DefaultDuration = defaultDuration;
        }
    }
} 