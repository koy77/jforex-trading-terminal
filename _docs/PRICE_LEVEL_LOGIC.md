# Логика обработки ценовых уровней в SimpleTradingOverlay

## Краткое описание

SimpleTradingOverlay автоматически обрабатывает события от HTTP сервера для заполнения цен входа и стоп-лосса, а затем отправляет торговые команды в MT4. TradingToolbar используется только для отображения UI.

## Алгоритм работы

### 1. Получение первого события (EntryPrice)
- HTTP сервер отправляет ценовой уровень
- TradingToolbar заполняет `EntryPrice`
- UI показывает: "Entry: {price}" + "Waiting for Stop Loss"
- Тост уведомление: "Entry Price: {price} ({symbol})"

### 2. Получение второго события (StopLoss)
- HTTP сервер отправляет второй ценовой уровень
- TradingToolbar заполняет `StopLoss`
- Автоматически определяется тип сделки:
  - **BUY**: если EntryPrice > StopLoss
  - **SELL**: если EntryPrice < StopLoss
- Тост уведомление: "Stop Loss: {price} | Trade: {type} ({symbol})"
- **Автоматически отправляется команда в MT4**
- **UI полностью очищается (панель с ценами скрывается)**

### 3. Отмена операции (Escape)
- Нажатие Escape полностью сбрасывает состояние
- Обнуляются EntryPrice и StopLoss
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

## Пример последовательности

1. **Событие 1**: 2345.67 → EntryPrice = 2345.67 → UI показывает Entry
2. **Событие 2**: 2340.00 → StopLoss = 2340.00 → BUY → MT4 команда → **UI полностью скрывается**
3. **Escape**: Сброс всех цен
4. **Событие 3**: 2350.00 → новый цикл, EntryPrice = 2350.00 