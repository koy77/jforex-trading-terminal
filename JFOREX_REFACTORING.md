# Рефакторинг для поддержки JForex объектов

## Обзор изменений

Проведен рефакторинг системы для поддержки трех типов JForex объектов:
- `PriceMarkerChartObject` (PriceMarker)
- `RectangleChartObject` (Rectangle) 
- `ShortLineChartObject` (ShortLine)

## Новые типы данных

### JForexChartObjectData

Создан новый класс `JForexChartObjectData` в `Models/JForexChartObjectData.cs`:

```csharp
public enum JForexChartObjectType
{
    PriceMarker,
    Rectangle,
    ShortLine
}

public class JForexChartObjectData
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public JForexChartObjectType ObjectType { get; set; }
    public string ClassName { get; set; }
    public DateTime Timestamp { get; set; }
    public string AdditionalData { get; set; }
}
```

### Автоматическое определение типа

Тип объекта определяется автоматически по имени класса:

```csharp
public static JForexChartObjectType GetObjectTypeFromClassName(string className)
{
    if (className.Contains("PriceMarkerChartObject"))
        return JForexChartObjectType.PriceMarker;
    else if (className.Contains("RectangleChartObject"))
        return JForexChartObjectType.Rectangle;
    else if (className.Contains("ShortLineChartObject"))
        return JForexChartObjectType.ShortLine;
    else
        return JForexChartObjectType.PriceMarker; // По умолчанию
}
```

## Изменения в HttpServerService

### Новое событие

Добавлено новое событие `NewJForexChartObject`:

```csharp
/// <summary>
/// Событие нового JForex объекта
/// </summary>
public event EventHandler<JForexChartObjectData> NewJForexChartObject;
```

### Обработка запросов

В методе `HandlePriceLevelRequestAsync` добавлена обработка JForex объектов:

```csharp
// Создаем и вызываем новое событие JForex объекта
var jforexObjectType = JForexChartObjectData.GetObjectTypeFromClassName(priceLevelData.AdditionalData);
var jforexChartObject = new JForexChartObjectData(
    symbol: priceLevelData.Symbol,
    price: priceLevelData.Price,
    objectType: jforexObjectType,
    className: priceLevelData.AdditionalData,
    additionalData: priceLevelData.AdditionalData
);
OnNewJForexChartObject(jforexChartObject);
```

## Изменения в SimpleTradingOverlay

### Подписка на события

Обновлена подписка на события HttpServerService:

```csharp
private void SetupHttpServerEvents()
{
    var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
    if (httpServerService != null)
    {
        // Подписываемся на новое событие JForex объектов
        httpServerService.NewJForexChartObject += HttpServerService_NewJForexChartObject;
        Logger.LogInfo("SimpleTradingOverlay subscribed to HttpServerService.NewJForexChartObject");
    }
}
```

### Новый обработчик событий

Создан новый обработчик `HttpServerService_NewJForexChartObject`:

```csharp
private async void HttpServerService_NewJForexChartObject(object sender, JForexChartObjectData jforexChartObject)
{
    // Проверка символа
    // Показ toast уведомления
    // Обработка объекта по типу
    ProcessJForexChartObject(jforexChartObject);
}
```

### Обработка по типам

Создан метод `ProcessJForexChartObject` с разделением по типам:

```csharp
private void ProcessJForexChartObject(JForexChartObjectData jforexChartObject)
{
    switch (jforexChartObject.ObjectType)
    {
        case JForexChartObjectType.PriceMarker:
            ProcessPriceMarkerObject(jforexChartObject);
            break;
        
        case JForexChartObjectType.Rectangle:
            ProcessRectangleObject(jforexChartObject);
            break;
        
        case JForexChartObjectType.ShortLine:
            ProcessShortLineObject(jforexChartObject);
            break;
        
        default:
            Logger.LogTagWarning("SimpleTradingOverlay", $"Unknown JForex object type: {jforexChartObject.ObjectType}");
            break;
    }
}
```

## Обработка типов объектов

### PriceMarker (аналог старой логики)

Метод `ProcessPriceMarkerObject` содержит всю старую логику обработки ценовых уровней:

- Установка Entry Price
- Установка Stop Loss Price
- Отправка команды в MT4
- Сброс состояния

### Rectangle и ShortLine

Созданы заглушки для будущей реализации:

```csharp
private void ProcessRectangleObject(JForexChartObjectData jforexChartObject)
{
    Logger.LogTagInfo("SimpleTradingOverlay", $"Processing Rectangle object: {jforexChartObject.Price}");
    // TODO: Добавить логику обработки Rectangle объектов
}

private void ProcessShortLineObject(JForexChartObjectData jforexChartObject)
{
    Logger.LogTagInfo("SimpleTradingOverlay", $"Processing ShortLine object: {jforexChartObject.Price}");
    // TODO: Добавить логику обработки ShortLine объектов
}
```

## Обратная совместимость

Сохранена обратная совместимость:
- Событие `NewPriceLevelReceived` остается для совместимости
- Старая логика обработки PriceMarker полностью сохранена
- Все существующие функции работают как прежде

## Логирование

Добавлено подробное логирование:

```
[INFO] New JForex Chart Object: JForexChartObject: PriceMarker | Symbol: GBPJPY | Price: 196.50000 | Class: com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER
[INFO] Processing JForex chart object: PriceMarker = 196.50000
[INFO] Processing PriceMarker object: 196.50000
```

## Тестирование

Для тестирования используйте те же данные, что и раньше:

```json
{
  "symbol": "GBPJPY",
  "price": 196.50000,
  "additionalData": "com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER"
}
```

Система автоматически определит тип объекта и обработает его соответствующим образом.

## Следующие шаги

1. Реализовать логику для `Rectangle` объектов
2. Реализовать логику для `ShortLine` объектов
3. Добавить дополнительные типы объектов при необходимости
4. Расширить функциональность для новых типов 