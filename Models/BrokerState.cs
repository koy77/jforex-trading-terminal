using System;

namespace ScreenCaptureApp.Models
{
    public enum BrokerType
    {
        Forex,
        PocketOption,
        Binarium,
        Quotex
    }

    public class BrokerState
    {
        private BrokerType _currentBroker = BrokerType.Forex;

        public BrokerType CurrentBroker
        {
            get => _currentBroker;
            set
            {
                if (_currentBroker != value)
                {
                    _currentBroker = value;
                    BrokerChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<BrokerType> BrokerChanged;

        public bool IsForex => CurrentBroker == BrokerType.Forex;
        public bool IsPocketOption => CurrentBroker == BrokerType.PocketOption;
        public bool IsBinarium => CurrentBroker == BrokerType.Binarium;
        public bool IsQuotex => CurrentBroker == BrokerType.Quotex;

        public string GetDisplayName()
        {
            return CurrentBroker switch
            {
                BrokerType.Forex => "Forex",
                BrokerType.PocketOption => "Pocket Option",
                BrokerType.Binarium => "Binarium",
                BrokerType.Quotex => "Quotex",
                _ => "Unknown"
            };
        }
    }
} 