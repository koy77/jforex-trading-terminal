# Changelog

Все значимые изменения в проекте документируются в этом файле.

## [Unreleased] - 2024-12-XX

### Added
- **SimpleTradingOverlay** - новое окно для автоматического отслеживания курсора мыши
  - Глобальный хук мыши для отслеживания перемещений курсора
  - Автоматическое распознавание окон JForex по символам в заголовке
  - Показ TradingToolbar только на окнах с распознанными символами
  - Интеграция с WindowManagementService для получения заголовков окон
  - Интеграция с ToolbarSettingsManager для сохранения настроек по окнам
  - Автоматический запуск вместе с приложением

### Changed
- **MainWindow** - добавлена инициализация SimpleTradingOverlay
  - Добавлено поле `simpleTradingOverlay`
  - Добавлен метод `InitializeSimpleTradingOverlay()`
  - Обновлен метод закрытия для корректной очистки ресурсов

### Technical Details
- Использование `SetWindowsHookEx` для глобального хука мыши
- Проверка заголовков окон на наличие символов: XAUUSD, GBPJPY, EURUSD, GBPUSD, USDJPY, EURJPY
- Позиционирование оверлея в верхней части окна с шириной равной ширине окна
- Логирование всех операций для отладки

### Files Added
- `SimpleTradingOverlay.xaml` - XAML разметка окна
- `SimpleTradingOverlay.xaml.cs` - код-behind с логикой отслеживания

### Files Modified
- `MainWindow.xaml.cs` - добавлена интеграция с SimpleTradingOverlay
- `ARCHITECTURE.md` - обновлена документация архитектуры

## [Previous Versions]

### 2024-06-XX
- Удалены SymbolSettingsManager, BrokerSettingsManager
- Реализован ToolbarSettingsManager для хранения настроек по окнам
- Обновлена архитектура приложения

---

## How to Use

### SimpleTradingOverlay
1. Приложение автоматически запускает SimpleTradingOverlay при старте
2. Перемещайте курсор мыши по окнам JForex
3. Оверлей автоматически появится на окнах с распознанными символами
4. Настройки тулбара сохраняются индивидуально для каждого окна
5. Если символ не найден в заголовке, оверлей остается на предыдущем окне

### Supported Symbols
- XAUUSD (XAU/USD)
- GBPJPY (GBP/JPY)
- EURUSD (EUR/USD)
- GBPUSD (GBP/USD)
- USDJPY (USD/JPY)
- EURJPY (EUR/JPY) 