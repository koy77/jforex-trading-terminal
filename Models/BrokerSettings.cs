using System;

namespace ScreenCaptureApp.Models
{
    /// <summary>
    /// Настройки брокера, хранящиеся в памяти (DI Container)
    /// </summary>
    public class BrokerSettings
    {
        public BrokerType BrokerType { get; set; }
        public double DefaultRisk { get; set; }
        public int DefaultDuration { get; set; }
        
        public BrokerSettings(BrokerType brokerType, double defaultRisk = 1.0, int defaultDuration = 2)
        {
            BrokerType = brokerType;
            DefaultRisk = defaultRisk;
            DefaultDuration = defaultDuration;
        }
    }
} 