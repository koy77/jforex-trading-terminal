# SimpleTradingOverlay - Обработка ценовых уровней

## Обзор

SimpleTradingOverlay теперь обрабатывает события от HTTP сервера для автоматического заполнения цен входа (EntryPrice) и стоп-лосса (StopLoss), а затем отправляет команды в MT4 через socket соединение.

## Логика обработки событий

### Состояния ценовых уровней

SimpleTradingOverlay поддерживает следующие состояния для обработки ценовых уровней:

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
   - Вызывает `TradingToolbar.SetEntryLevel()` для отображения в UI
   - Показывает тост уведомление: "Entry Price: {price} ({symbol})"

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
- UI панель с ценами полностью скрывается (`TradingToolbar.HidePrices()`)
- Даже при ошибке отправки команды UI очищается
- Система готова к новому циклу

## Отмена операции (Escape)

При нажатии клавиши **Escape** происходит полный сброс состояния:

```csharp
public void OnEscapeKeyPressed()
{
    // ... другая логика ...
    
    // Отменяем Entry Level при нажатии Escape
    if (TradingToolbar != null)
    {
        Logger.LogTagInfo("SimpleTradingOverlay", "Escape key pressed - cancelling price level entry");
        ResetPriceLevelState();
    }
}

private void ResetPriceLevelState()
{
    _isWaitingForEntryPrice = false;
    _isWaitingForStopLossPrice = false;
    _entryPrice = 0;
    _stopLossPrice = 0;
    _currentTradeSymbol = "";
    
    // Скрываем цены в UI
    TradingToolbar.HidePrices();
    
    Logger.LogTagInfo("SimpleTradingOverlay", "Price level state reset");
}
```

## Интеграция с HTTP сервером

### Подписка на события

```csharp
private void SetupHttpServerEvents()
{
    var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
    if (httpServerService != null)
    {
        httpServerService.NewPriceLevelReceived += HttpServerService_NewPriceLevelReceived;
        Logger.LogInfo("SimpleTradingOverlay subscribed to HttpServerService.NewPriceLevelReceived");
    }
}
```

### Обработка событий

```csharp
private async void HttpServerService_NewPriceLevelReceived(object sender, PriceLevelEventData priceLevelEvent)
{
    try
    {
        Logger.LogTagInfo("SimpleTradingOverlay", $"Received new price level event: {priceLevelEvent}");

        // Проверяем, соответствует ли символ текущему символу в тулбаре
        if (string.IsNullOrEmpty(TradingToolbar.CurrentSymbol) || !TradingToolbar.CurrentSymbol.Equals(priceLevelEvent.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogTagDebug("SimpleTradingOverlay", $"Symbol mismatch: current={TradingToolbar.CurrentSymbol}, event={priceLevelEvent.Symbol}");
            return;
        }

        // Показываем тост уведомление
        var toastNotifyService = ServiceContainer.Instance.GetService<ToastNotifyService>();
        if (toastNotifyService != null)
        {
            string toastMessage = $"HTTP: {priceLevelEvent.Name} = {priceLevelEvent.LevelValue:F5} ({priceLevelEvent.Symbol})";
            toastNotifyService.ShowToast(toastMessage, ToastType.Success, 3000);
            Logger.LogTagInfo("SimpleTradingOverlay", $"Toast notification shown: {toastMessage}");
        }

        // Обрабатываем ценовой уровень в зависимости от состояния
        ProcessPriceLevelEvent(priceLevelEvent);
    }
    catch (Exception ex)
    {
        Logger.LogTagError("SimpleTradingOverlay", "Error processing price level event", ex);
    }
}
```

## Взаимодействие с TradingToolbar

### Установка Entry Level

```csharp
// Устанавливаем Entry Level в активном TradingToolbar
TradingToolbar.SetEntryLevel(_entryPrice, _currentTradeSymbol);
```

### Очистка UI

```csharp
// Скрываем цены в UI
TradingToolbar.HidePrices();
```

## Логирование

Все операции логируются с тегом "SimpleTradingOverlay":

- Получение событий от HTTP сервера
- Установка EntryPrice и StopLoss
- Отправка команд в MT4
- Отмена операций по Escape
- Обновления UI через TradingToolbar

## Примеры использования

### Последовательность событий

1. HTTP сервер отправляет цену 2345.67 для XAUUSD
   - EntryPrice = 2345.67
   - Вызывается `TradingToolbar.SetEntryLevel()`
   - UI показывает: "Entry: 2345.67" + "Waiting for Stop Loss"

2. HTTP сервер отправляет цену 2340.00 для XAUUSD
   - StopLoss = 2340.00
   - Определяется тип: BUY (2345.67 > 2340.00)
   - Отправляется команда в MT4
   - UI полностью скрывается

3. Нажатие Escape
   - Все цены сбрасываются
   - UI скрывается
   - Готовность к новому циклу

### Тост уведомления

- **Entry Price**: "Entry Price: 2345.67 (XAUUSD)"
- **Stop Loss**: "Stop Loss: 2340.00 | Trade: BUY (XAUUSD)"
- **HTTP события**: "HTTP: Support Level = 2345.67 (XAUUSD)" 