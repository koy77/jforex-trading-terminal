# Логика обработки ценовых уровней в SimpleTradingOverlay

## Краткое описание

SimpleTradingOverlay автоматически обрабатывает события от HTTP сервера для заполнения цен входа и стоп-лосса, а затем отправляет торговые команды в MT4. TradingToolbar используется только для отображения UI.

## Алгоритм работы

### 1. Получение первого события (EntryPrice)
- HTTP сервер отправляет ценовой уровень типа `PriceMarkerChartObject`
- TradingToolbar заполняет `EntryPrice`
- UI показывает: "Entry: {price}" + "Waiting for Stop Loss"
- Тост уведомление: "Entry Price: {price} ({symbol})"

### 2. Получение второго события (StopLoss)
- HTTP сервер отправляет второй ценовой уровень типа `PriceMarkerChartObject`
- TradingToolbar заполняет `StopLoss`
- Автоматически определяется тип сделки:
  - **BUY**: если EntryPrice > StopLoss
  - **SELL**: если EntryPrice < StopLoss
- Тост уведомление: "Stop Loss: {price} | Trade: {type} ({symbol})"
- **Автоматически отправляется команда в MT4**
- **UI полностью очищается (панель с ценами скрывается)**

### 3. Отмена операции (Escape)
- **Нажатие Escape через HotkeyService** полностью сбрасывает состояние
- Обнуляются EntryPrice и StopLoss
- **Система переходит в режим ожидания нового Entry Price**
- UI скрывается
- Готовность к новому циклу

## Команда в MT4

```json
{
  "command": "open_position",
  "symbol": "XAUUSD",
  "type": "buy|sell",
  "entry_price": 2345.67,
  "stop_loss": 2340.00,
  "risk": 1.0
}
```

## Состояния

```csharp
private bool _isWaitingForEntryPrice = false;
private bool _isWaitingForStopLossPrice = false;
private decimal _entryPrice = 0;
private decimal _stopLossPrice = 0;
private string _currentTradeSymbol = "";
```

## Обработка клавиш через HotkeyService

SimpleTradingOverlay подписывается на события HotkeyService в конструкторе:

```csharp
private void SetupHotkeyServiceEvents()
{
    if (_hotkeysService != null)
    {
        _hotkeysService.OnEscapeKeyPressed += OnEscapeKeyPressed;
        _hotkeysService.OnSKeyPressed += OnSKeyPressed;
        _hotkeysService.OnAKeyPressed += OnAKeyPressed;
        _hotkeysService.OnPKeyPressed += OnPKeyPressed;
        _hotkeysService.OnDKeyPressed += OnDKeyPressed;
    }
}
```

## Методы сброса состояния

### ResetPriceLevelState()
- Полностью сбрасывает состояние
- `_isWaitingForEntryPrice = false`
- Используется после отправки команды в MT4

### ResetPriceLevelStateToWaitForEntry()
- Сбрасывает цены, но переводит в режим ожидания Entry Price
- `_isWaitingForEntryPrice = true`
- Используется при нажатии Escape

## Пример последовательности

1. **Событие 1**: 2345.67 → EntryPrice = 2345.67 → UI показывает Entry
2. **Escape**: Сброс EntryPrice → `_isWaitingForEntryPrice = true` → готов к новому Entry
3. **Событие 2**: 2350.00 → новый EntryPrice = 2350.00 → UI показывает Entry
4. **Событие 3**: 2340.00 → StopLoss = 2340.00 → BUY → MT4 команда → **UI полностью скрывается**
5. **Событие 4**: 2360.00 → новый цикл, EntryPrice = 2360.00

## Фильтрация событий

Система обрабатывает только события с типом `PriceMarkerChartObject`:

```csharp
if (!priceLevelEvent.Name.Contains("PriceMarkerChartObject"))
{
    Logger.LogTagDebug("SimpleTradingOverlay", $"Skipping non-PriceMarkerChartObject event: {priceLevelEvent.Name}");
    return;
}
``` 