using System;
using System.Collections.Generic;
using System.Linq;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Services
{
    /// <summary>
    /// Сервис для управления настройками брокеров в DI Container
    /// </summary>
    public class BrokerSettingsManager
    {
        private readonly Dictionary<BrokerType, BrokerSettings> _brokerSettings = new Dictionary<BrokerType, BrokerSettings>();
        private readonly DatabaseService _databaseService;

        public BrokerSettingsManager(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            InitializeDefaultSettings();
        }

        /// <summary>
        /// Инициализирует дефолтные настройки для всех брокеров
        /// </summary>
        private void InitializeDefaultSettings()
        {
            // Дефолтные настройки для каждого брокера
            _brokerSettings[BrokerType.Forex] = new BrokerSettings(BrokerType.Forex, 1.0, 2);
            _brokerSettings[BrokerType.PocketOption] = new BrokerSettings(BrokerType.PocketOption, 5.0, 2);
            _brokerSettings[BrokerType.Binarium] = new BrokerSettings(BrokerType.Binarium, 10.0, 2);
            _brokerSettings[BrokerType.Quotex] = new BrokerSettings(BrokerType.Quotex, 5.0, 2);
            
            Logger.LogInfo("BrokerSettingsManager initialized with default settings");
        }

        /// <summary>
        /// Получает DefaultRisk для указанного брокера
        /// </summary>
        public double GetDefaultRisk(BrokerType brokerType)
        {
            if (_brokerSettings.TryGetValue(brokerType, out var settings))
            {
                return settings.DefaultRisk;
            }
            
            // Fallback к дефолтному значению
            Logger.LogWarning($"Broker settings not found for {brokerType}, using default risk=1.0");
            return 1.0;
        }

        /// <summary>
        /// Получает DefaultDuration для указанного брокера
        /// </summary>
        public int GetDefaultDuration(BrokerType brokerType)
        {
            if (_brokerSettings.TryGetValue(brokerType, out var settings))
            {
                return settings.DefaultDuration;
            }
            
            // Fallback к дефолтному значению
            Logger.LogWarning($"Broker settings not found for {brokerType}, using default duration=2");
            return 2;
        }

        /// <summary>
        /// Обновляет настройки брокера при создании CaptureData
        /// </summary>
        public void UpdateSettingsFromCapture(BrokerType brokerType, double risk, int duration)
        {
            if (_brokerSettings.TryGetValue(brokerType, out var settings))
            {
                settings.DefaultRisk = risk;
                settings.DefaultDuration = duration;
                Logger.LogInfo($"Updated broker settings for {brokerType}: Risk={risk}, Duration={duration}");
            }
            else
            {
                // Создаем новые настройки, если их еще нет
                _brokerSettings[brokerType] = new BrokerSettings(brokerType, risk, duration);
                Logger.LogInfo($"Created new broker settings for {brokerType}: Risk={risk}, Duration={duration}");
            }
        }

        /// <summary>
        /// Обновляет настройки брокера напрямую
        /// </summary>
        public void UpdateBrokerSettings(BrokerType brokerType, double risk, int duration)
        {
            UpdateSettingsFromCapture(brokerType, risk, duration);
        }

        /// <summary>
        /// Сбрасывает все настройки брокеров к дефолтным значениям
        /// </summary>
        public void ResetAllSettings()
        {
            InitializeDefaultSettings();
            Logger.LogInfo("All broker settings reset to default values");
        }

        /// <summary>
        /// Получает все настройки брокеров
        /// </summary>
        public Dictionary<BrokerType, BrokerSettings> GetAllSettings()
        {
            return new Dictionary<BrokerType, BrokerSettings>(_brokerSettings);
        }

        /// <summary>
        /// Логирует текущие настройки всех брокеров
        /// </summary>
        public void LogCurrentSettings()
        {
            foreach (var kvp in _brokerSettings)
            {
                Logger.LogInfo($"Broker {kvp.Key}: Risk={kvp.Value.DefaultRisk}, Duration={kvp.Value.DefaultDuration}");
            }
        }
    }
} 