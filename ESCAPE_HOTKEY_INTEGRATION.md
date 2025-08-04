# Интеграция Escape с HotkeyService в SimpleTradingOverlay

## Обзор изменений

Добавлена интеграция SimpleTradingOverlay с HotkeyService для обработки нажатия клавиши Escape и сброса состояния ценовых уровней.

## Основные изменения

### 1. Подписка на события HotkeyService

В конструкторе `SimpleTradingOverlay` добавлен вызов `SetupHotkeyServiceEvents()`:

```csharp
// Подписка на события HotkeyService
SetupHotkeyServiceEvents();
```

### 2. Метод SetupHotkeyServiceEvents()

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
        
        Logger.LogInfo("SimpleTradingOverlay subscribed to HotkeyService events");
    }
    else
    {
        Logger.LogWarning("HotkeyService not found, cannot subscribe to hotkey events");
    }
}
```

### 3. Улучшенная обработка Escape

Метод `OnEscapeKeyPressed()` теперь:
- Логирует нажатие Escape через HotkeyService
- Сбрасывает состояние ценовых уровней
- Переводит систему в режим ожидания нового Entry Price

```csharp
public void OnEscapeKeyPressed()
{
    if (!_isEnabled) return;

    // Проверяем, не является ли это сервисным нажатием Escape
    if (_isServiceEscapeSending)
    {
        Logger.LogTagInfo("PendingOrder", "Ignoring service Escape key press");
        return;
    }

    Logger.LogTagInfo("SimpleTradingOverlay", "Escape key pressed via HotkeyService");

    // ... обработка паттернов ...

    // Сбрасываем состояние ценовых уровней и переходим в режим ожидания Entry Price
    if (TradingToolbar != null)
    {
        Logger.LogTagInfo("SimpleTradingOverlay", "Escape key pressed - resetting price level state to wait for Entry Price");
        ResetPriceLevelStateToWaitForEntry();
    }
}
```

### 4. Новый метод ResetPriceLevelStateToWaitForEntry()

```csharp
private void ResetPriceLevelStateToWaitForEntry()
{
    _isWaitingForEntryPrice = true; // Переводим в режим ожидания Entry Price
    _isWaitingForStopLossPrice = false;
    _entryPrice = 0;
    _stopLossPrice = 0;
    _currentTradeSymbol = "";
    
    // Скрываем цены в UI
    TradingToolbar.HidePrices();
    
    Logger.LogTagInfo("SimpleTradingOverlay", "Price level state reset to wait for Entry Price - ready for new PriceMarkerChartObject");
}
```

### 5. Отписка от событий

В методе `OnClosed()` добавлена отписка от событий HotkeyService:

```csharp
// Отписываемся от событий HotkeyService
if (_hotkeysService != null)
{
    _hotkeysService.OnEscapeKeyPressed -= OnEscapeKeyPressed;
    _hotkeysService.OnSKeyPressed -= OnSKeyPressed;
    _hotkeysService.OnAKeyPressed -= OnAKeyPressed;
    _hotkeysService.OnPKeyPressed -= OnPKeyPressed;
    _hotkeysService.OnDKeyPressed -= OnDKeyPressed;
    Logger.LogInfo("SimpleTradingOverlay unsubscribed from HotkeyService events");
}
```

## Логика работы

### До изменений:
- Escape обрабатывался только локально
- Состояние полностью сбрасывалось
- Система не была готова к новому Entry Price

### После изменений:
- Escape обрабатывается через HotkeyService
- Состояние сбрасывается, но система переходит в режим ожидания Entry Price
- `_isWaitingForEntryPrice = true` после нажатия Escape
- Готовность к немедленному получению нового PriceMarkerChartObject

## Тестирование

Используйте обновленный тестовый скрипт `test_price_marker_logic.ps1` для проверки:

1. Установка Entry Price
2. Нажатие Escape
3. Проверка, что система готова к новому Entry Price
4. Установка нового Entry Price
5. Установка Stop Loss
6. Отправка команды в MT4

## Логирование

Все операции логируются с тегами:
- `SimpleTradingOverlay` - основные операции
- `PendingOrder` - операции с отложенными сделками

Примеры логов:
```
[INFO] SimpleTradingOverlay subscribed to HotkeyService events
[INFO] Escape key pressed via HotkeyService
[INFO] Escape key pressed - resetting price level state to wait for Entry Price
[INFO] Price level state reset to wait for Entry Price - ready for new PriceMarkerChartObject
``` 