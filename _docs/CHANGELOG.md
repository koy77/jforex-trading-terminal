# Changelog

Все значимые изменения в проекте документируются в этом файле.

## [Unreleased] - 2024-12-XX

### Added
- **TradingToolbar OrderSummary Integration** - интеграция с MT4 сокетом для отображения информации об ордерах
  - Подписка на событие `Mt4SocketService.OrdersSummaryReceived`
  - Отображение панели с информацией об ордере справа от символа
  - Кнопки Close, BE, TP1, TP2 для управления ордерами
  - Автоматическое скрытие панели при смене символа
  - Отправка команды `close_positions` при нажатии кнопки Close
  - Стилизация панели в соответствии с дизайном TradingToolbar
  - **Исправление**: Добавлена поддержка символов с суффиксами (сравнение по первым 6 символам)
  - **Исправление**: Добавлено логирование для отладки отображения панели OrderSummary
  - **Исправление**: Использование правильного символа (с суффиксом) при отправке команды Close
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

- **BinaryOptionsSocketService Atomic Commands** - атомарная отправка команд
  - Добавлен `SemaphoreSlim` для синхронизации отправки команд
  - Каждая команда отправляется отдельно с задержкой 50ms между командами
  - Предотвращение отправки нескольких JSON команд в одном сообщении
  - Исправлена проблема с невалидным JSON при одновременной отправке команд

- **TrendlineBreakDetector Improved Area Positioning** - улучшенное позиционирование областей интереса
  - Область интереса теперь сдвигается вправо и вниз/вверх от линии тренда
  - Добавлена фильтрация пикселей по стороне от линии тренда
  - Для нисходящих линий (BUY) ищутся пиксели только под линией
  - Для восходящих линий (SELL) ищутся пиксели только над линией
  - Исправлены ложные срабатывания пробоев
  - Улучшена визуализация отладочных изображений

### Changed
- **TradingToolbar** - добавлена интеграция с MT4 сокетом для отображения ордеров
  - Добавлена панель `OrderSummaryPanel` в XAML с кнопками Close, BE, TP1, TP2
  - Добавлены методы `ShowOrderSummary()`, `HideOrderSummary()`, `UpdateOrderSummaryUI()`
  - Добавлены обработчики событий для кнопок управления ордерами
  - Добавлен метод `CloseSymbolOrder()` для отправки команды закрытия позиций
  - Обновлен метод `SetSymbol()` для скрытия панели при смене символа
  - Добавлена подписка на `Mt4SocketService.OrdersSummaryReceived` в конструкторе

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
- Атомарная отправка команд через `SemaphoreSlim` с задержкой 50ms
- Алгоритм позиционирования областей интереса:
  - Сдвиг вправо на `zoneOffset` пикселей от линии тренда
  - Сдвиг вниз/вверх на `zoneOffset` пикселей в зависимости от направления линии
  - Фильтрация пикселей по математическому уравнению линии тренда

### Files Added
- `SimpleTradingOverlay.xaml` - XAML разметка окна
- `SimpleTradingOverlay.xaml.cs` - код-behind с логикой отслеживания

### Files Modified
- `Controls/TradingToolbar.xaml` - добавлена панель OrderSummaryPanel
- `Controls/TradingToolbar.xaml.cs` - добавлена интеграция с MT4 сокетом и binary-брокерами
- `MainWindow.xaml.cs` - добавлена интеграция с SimpleTradingOverlay
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