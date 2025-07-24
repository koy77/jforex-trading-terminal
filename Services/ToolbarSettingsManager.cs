using System.Collections.Generic;
using ScreenCaptureApp.Models;

namespace ScreenCaptureApp.Services
{
    /// <summary>
    /// Сервис для хранения и управления настройками TradingToolbar по handle окна (только в памяти, singleton DI)
    /// </summary>
    public class ToolbarSettingsManager
    {
        private readonly Dictionary<long, ToolbarSettings> _settingsByHandle = new();

        public ToolbarSettings GetSettings(long handle)
        {
            _settingsByHandle.TryGetValue(handle, out var settings);
            return settings;
        }

        public void SetSettings(long handle, ToolbarSettings settings)
        {
            _settingsByHandle[handle] = settings;
        }

        public void UpdateSettings(long handle, double? risk = null, int? duration = null, BrokerType? broker = null)
        {
            if (!_settingsByHandle.TryGetValue(handle, out var settings))
            {
                settings = new ToolbarSettings { Handle = handle };
                _settingsByHandle[handle] = settings;
            }
            if (risk.HasValue) settings.Risk = risk.Value;
            if (duration.HasValue) settings.Duration = duration.Value;
            if (broker.HasValue) settings.Broker = broker.Value;
        }

        public void RemoveSettings(long handle)
        {
            _settingsByHandle.Remove(handle);
        }
    }
} 