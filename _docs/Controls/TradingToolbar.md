# TradingToolbar - Обработка ценовых уровней

## Обзор

TradingToolbar обрабатывает события от HTTP сервера для автоматического заполнения цен входа (EntryPrice) и стоп-лосса (StopLoss), а затем отправляет команды в MT4 через socket соединение.

## Логика обработки событий

### Состояния ценовых уровней

TradingToolbar поддерживает следующие состояния для обработки ценовых уровней:

```csharp
private bool _isWaitingForEntryPrice = false;
private bool _isWaitingForStopLossPrice = false;
private decimal _entryPrice = 0;
private decimal _stopLossPrice = 0;
private string _currentTradeSymbol = "";
```

### Алгоритм обработки событий

1. **Первое событие** - заполняет `EntryPrice`
   - Устанавливает `_isWaitingForEntryPrice = false`
   - Устанавливает `_isWaitingForStopLossPrice = true`
   - Показывает тост уведомление: "Entry Price: {price} ({symbol})"
   - Обновляет UI: показывает Entry Price и статус "Waiting for Stop Loss"

2. **Второе событие** - заполняет `StopLoss`
   - Устанавливает `_isWaitingForStopLossPrice = false`
   - Определяет тип сделки (BUY/SELL) на основе сравнения цен
   - Показывает тост уведомление: "Stop Loss: {price} | Trade: {type} ({symbol})"
   - **Автоматически отправляет команду в MT4**
   - **Автоматически полностью очищает UI (скрывает панель с ценами)**

3. **Последующие события** - начинают новый цикл
   - Сбрасывают состояние и начинают новый цикл с EntryPrice

### Отправка команды в MT4

Когда заполнены обе цены (EntryPrice и StopLoss), автоматически формируется и отправляется команда:

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

Тип сделки определяется автоматически:
- **BUY**: если EntryPrice > StopLoss
- **SELL**: если EntryPrice < StopLoss

**После отправки команды:**
- Автоматически вызывается `ResetPriceLevelState()`
- EntryPrice и StopLossPrice обнуляются
- UI панель с ценами полностью скрывается (`HidePrices()`)
- Даже при ошибке отправки команды UI очищается
- Система готова к новому циклу

## Отмена операции (Escape)

При нажатии клавиши **Escape** происходит полный сброс состояния:

```csharp
public void CancelPriceLevelEntry()
{
    ResetPriceLevelState();
}

private void ResetPriceLevelState()
{
    _isWaitingForEntryPrice = false;
    _isWaitingForStopLossPrice = false;
    _entryPrice = 0;
    _stopLossPrice = 0;
    _currentTradeSymbol = "";
    HidePrices();
}
```

## UI отображение

### Панель цен (PriceDisplayPanel)

```xml
<StackPanel x:Name="PriceDisplayPanel" Orientation="Vertical" Visibility="Collapsed">
    <TextBlock x:Name="TradeTypeLabel" Text="BUY" Foreground="White"/>
    <TextBlock x:Name="EntryPriceLabel" Text="Entry: 0.00000" Foreground="LightGreen"/>
    <TextBlock x:Name="StopLossPriceLabel" Text="SL: 0.00000" Foreground="LightCoral"/>
</StackPanel>
```

### Методы управления UI

- `ShowPrices(entryPrice, stopLossPrice, tradeType)` - показывает панель с ценами
- `HidePrices()` - скрывает панель с ценами
- `UpdatePricesUI()` - обновляет отображение цен

## Интеграция с SimpleTradingOverlay

### Метод SetEntryLevel

```csharp
public void SetEntryLevel(decimal entryPrice, string symbol)
{
    // Показывает тост уведомление
    // Обновляет UI: показывает Entry Price и статус "Waiting for Stop Loss"
}
```

### Управление UI

- `ShowPrices(entryPrice, stopLossPrice, tradeType)` - показывает панель с ценами
- `HidePrices()` - скрывает панель с ценами
- `UpdatePricesUI()` - обновляет отображение цен

**Примечание:** Логика обработки HTTP событий перенесена в SimpleTradingOverlay. TradingToolbar теперь только отображает UI и предоставляет метод SetEntryLevel для установки цен.

## Логирование

Все операции логируются с тегом "TradingToolbar":

- Получение событий от HTTP сервера
- Установка EntryPrice и StopLoss
- Отправка команд в MT4
- Отмена операций по Escape
- Обновления UI

## Примеры использования

### Последовательность событий

1. HTTP сервер отправляет цену 2345.67 для XAUUSD
   - EntryPrice = 2345.67
   - UI показывает: "Entry: 2345.67" + "Waiting for Stop Loss"

2. HTTP сервер отправляет цену 2340.00 для XAUUSD
   - StopLoss = 2340.00
   - Определяется тип: BUY (2345.67 > 2340.00)
   - UI показывает: "BUY" + "Entry: 2345.67" + "SL: 2340.00"
   - Отправляется команда в MT4

3. Нажатие Escape
   - Все цены сбрасываются
   - UI скрывается
   - Готовность к новому циклу

### Тост уведомления

- **Entry Price**: "Entry Price: 2345.67 (XAUUSD)"
- **Stop Loss**: "Stop Loss: 2340.00 | Trade: BUY (XAUUSD)"
- **HTTP события**: "HTTP: Support Level = 2345.67 (XAUUSD)" 