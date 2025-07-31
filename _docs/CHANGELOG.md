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

- **TradingToolbar Binary Broker Integration** - интеграция с binary-брокерами через сокет
  - Автоматическая отправка команд при изменении риска для binary-брокеров
  - Автоматическая отправка команд при изменении duration для binary-брокеров
  - Автоматическая отправка команды открытия символа при показе панели
  - Поддержка брокеров: PocketOption, Binarium, Quotex
  - Интеграция с JForexWindowsManagerService для отправки команд

- **JForexWindowsManagerService Binary Commands** - новые методы для работы с binary-сокетом
  - `SetRisk(string brokerName, double risk)` - отправка команды установки риска
  - `SetDuration(string brokerName, int duration)` - отправка команды установки duration
  - `OpenSymbol(string brokerName, string symbolName)` - отправка команды открытия символа
  - Подробное логирование всех операций в JForex.log
  - Обработка ошибок и проверка подключения к binary-сокету

### Changed
- **MainWindow** - добавлена инициализация SimpleTradingOverlay
  - Добавлено поле `simpleTradingOverlay`
  - Добавлен метод `InitializeSimpleTradingOverlay()`
  - Обновлен метод закрытия для корректной очистки ресурсов

- **TradingToolbar** - обновлена логика работы с binary-брокерами
  - Добавлен метод `SendRiskCommandToBinarySocket(double risk)`
  - Добавлен метод `SendDurationCommandToBinarySocket(int duration)`
  - Добавлен метод `SendOpenSymbolCommand(string symbolName)`
  - Обновлен метод `SetSymbol()` для автоматической отправки команды открытия символа
  - Проверка типа брокера перед отправкой команд (только для non-Forex брокеров)

### Technical Details
- Использование `SetWindowsHookEx` для глобального хука мыши
- Проверка заголовков окон на наличие символов: XAUUSD, GBPJPY, EURUSD, GBPUSD, USDJPY, EURJPY
- Позиционирование оверлея в верхней части окна с шириной равной ширине окна
- Логирование всех операций для отладки
- JSON команды для binary-сокета:
  ```json
  {"cmd": "set_risk", "broker": "BROKER_NAME", "risk": "RISK_VALUE"}
  {"cmd": "set_duration", "broker": "BROKER_NAME", "duration": "DURATION_VALUE"}
  {"cmd": "open_symbol", "symbol": "SYMBOL_NAME", "broker": "BROKER_NAME"}
  ```

### Files Added
- `SimpleTradingOverlay.xaml` - XAML разметка окна
- `SimpleTradingOverlay.xaml.cs` - код-behind с логикой отслеживания

### Files Modified
- `MainWindow.xaml.cs` - добавлена интеграция с SimpleTradingOverlay
- `Controls/TradingToolbar.xaml.cs` - добавлена интеграция с binary-брокерами
- `Services/JForexWindowsManagerService.cs` - добавлены методы для binary-команд
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

### TradingToolbar Binary Broker Integration
1. Выберите binary-брокер (PocketOption, Binarium, Quotex) в выпадающем списке
2. При изменении риска автоматически отправляется команда в binary-сокет
3. При изменении duration автоматически отправляется команда в binary-сокет
4. При показе панели с символом автоматически отправляется команда открытия символа
5. Все команды логируются в JForex.log для отладки

### Supported Symbols
- XAUUSD (XAU/USD)
- GBPJPY (GBP/JPY)
- EURUSD (EUR/USD)
- GBPUSD (GBP/USD)
- USDJPY (USD/JPY)
- EURJPY (EUR/JPY)

### Supported Binary Brokers
- PocketOption
- Binarium
- Quotex 