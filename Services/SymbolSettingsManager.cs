using System;
using System.Collections.Generic;
using System.Linq;
using ScreenCaptureApp.Models;
using ScreenCaptureApp.Services;

namespace ScreenCaptureApp.Services
{
    /// <summary>
    /// Сервис для управления настройками символов в DI Container
    /// </summary>
    public class SymbolSettingsManager
    {
        private readonly Dictionary<string, SymbolSettings> _symbolSettings = new Dictionary<string, SymbolSettings>();
        private readonly DatabaseService _databaseService;
        private readonly WindowManagementService _windowManagementService;

        public SymbolSettingsManager(DatabaseService databaseService, WindowManagementService windowManagementService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _windowManagementService = windowManagementService ?? throw new ArgumentNullException(nameof(windowManagementService));
        }

        /// <summary>
        /// Инициализирует настройки символов из базы данных
        /// </summary>
        public void InitializeSymbolSettings()
        {
            try
            {
                var symbols = _windowManagementService?.Symbols;
                if (symbols != null && symbols.Count > 0)
                {
                    foreach (var symbolKey in symbols.Keys)
                    {
                        var symbolData = _databaseService.GetSymbolByName(symbolKey);
                        if (symbolData != null)
                        {
                            // Создаем настройки символа в памяти
                            var settings = new SymbolSettings(
                                symbolKey,
                                symbolData.RiskPercent, // Используем RiskPercent как DefaultRisk
                                2 // Стандартная длительность
                            );
                            
                            _symbolSettings[symbolKey] = settings;
                            Logger.LogInfo($"Initialized symbol settings for {symbolKey}: DefaultRisk={settings.DefaultRisk}, DefaultDuration={settings.DefaultDuration}");
                        }
                        else
                        {
                            // Если символа нет в базе, создаем с дефолтными значениями
                            var settings = new SymbolSettings(symbolKey, 1.0, 2);
                            _symbolSettings[symbolKey] = settings;
                            Logger.LogInfo($"Created default symbol settings for {symbolKey}: DefaultRisk={settings.DefaultRisk}, DefaultDuration={settings.DefaultDuration}");
                        }
                    }
                }
                
                Logger.LogInfo($"Symbol settings manager initialized with {_symbolSettings.Count} symbols");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize symbol settings manager", ex);
            }
        }

        /// <summary>
        /// Получает настройки символа
        /// </summary>
        public SymbolSettings GetSymbolSettings(string symbolKey)
        {
            if (string.IsNullOrEmpty(symbolKey))
                return null;

            return _symbolSettings.TryGetValue(symbolKey, out var settings) ? settings : null;
        }

        /// <summary>
        /// Получает DefaultRisk для символа
        /// </summary>
        public double GetDefaultRisk(string symbolKey)
        {
            var settings = GetSymbolSettings(symbolKey);
            return settings?.DefaultRisk ?? 1.0;
        }

        /// <summary>
        /// Получает DefaultDuration для символа
        /// </summary>
        public int GetDefaultDuration(string symbolKey)
        {
            var settings = GetSymbolSettings(symbolKey);
            return settings?.DefaultDuration ?? 2;
        }

        /// <summary>
        /// Обновляет DefaultRisk для символа
        /// </summary>
        public void UpdateDefaultRisk(string symbolKey, double defaultRisk)
        {
            if (string.IsNullOrEmpty(symbolKey))
                return;

            if (_symbolSettings.TryGetValue(symbolKey, out var settings))
            {
                settings.DefaultRisk = defaultRisk;
                Logger.LogInfo($"Updated DefaultRisk for {symbolKey} to {defaultRisk}");
            }
            else
            {
                // Создаем новые настройки если символа нет
                _symbolSettings[symbolKey] = new SymbolSettings(symbolKey, defaultRisk, 2);
                Logger.LogInfo($"Created new settings for {symbolKey} with DefaultRisk={defaultRisk}");
            }
        }

        /// <summary>
        /// Обновляет DefaultDuration для символа
        /// </summary>
        public void UpdateDefaultDuration(string symbolKey, int defaultDuration)
        {
            if (string.IsNullOrEmpty(symbolKey))
                return;

            if (_symbolSettings.TryGetValue(symbolKey, out var settings))
            {
                settings.DefaultDuration = defaultDuration;
                Logger.LogInfo($"Updated DefaultDuration for {symbolKey} to {defaultDuration}");
            }
            else
            {
                // Создаем новые настройки если символа нет
                _symbolSettings[symbolKey] = new SymbolSettings(symbolKey, 1.0, defaultDuration);
                Logger.LogInfo($"Created new settings for {symbolKey} with DefaultDuration={defaultDuration}");
            }
        }

        /// <summary>
        /// Обновляет настройки символа при создании CaptureData
        /// </summary>
        public void UpdateSettingsFromCapture(string symbolKey, double risk, int duration)
        {
            if (string.IsNullOrEmpty(symbolKey))
                return;

            UpdateDefaultRisk(symbolKey, risk);
            UpdateDefaultDuration(symbolKey, duration);
            
            Logger.LogInfo($"Updated symbol settings from capture: {symbolKey}, Risk={risk}, Duration={duration}");
        }

        /// <summary>
        /// Применяет настройки символа к глобальным состояниям
        /// </summary>
        public void ApplySymbolDefaults(string symbolKey)
        {
            if (string.IsNullOrEmpty(symbolKey))
                return;

            try
            {
                var settings = GetSymbolSettings(symbolKey);
                if (settings != null)
                {
                    Logger.LogInfo($"Applying defaults for symbol {symbolKey}: DefaultRisk={settings.DefaultRisk}, DefaultDuration={settings.DefaultDuration}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply symbol defaults for {symbolKey}", ex);
            }
        }

        /// <summary>
        /// Получает все настройки символов
        /// </summary>
        public Dictionary<string, SymbolSettings> GetAllSettings()
        {
            return new Dictionary<string, SymbolSettings>(_symbolSettings);
        }

        /// <summary>
        /// Сбрасывает все настройки к дефолтным значениям
        /// </summary>
        public void ResetAllSettings()
        {
            _symbolSettings.Clear();
            InitializeSymbolSettings();
            Logger.LogInfo("All symbol settings reset to defaults");
        }
    }
} 