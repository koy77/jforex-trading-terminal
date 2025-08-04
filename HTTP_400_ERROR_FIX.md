# Исправление ошибки 400 "Invalid data: symbol is required"

## Проблема

В логах `http_service.log` видно, что сервер возвращает ошибку 400 "Invalid data: symbol is required", хотя в JSON есть поле "symbol":

```
[INFO] Price Level Input Data (raw): {"symbol":"GBPJPY","price":196,34135,"timestamp":"2025-08-04T12:07:36.713Z","type":"com.dukascopy.charts.drawings.PriceMarkerChartObject","additionalData":{"objectType":"PRICEMARKER"}}
[INFO] Deserialized PriceLevelData: Symbol='', Price=, Type='', AdditionalData=''
[INFO] Server Response (400): Invalid data: symbol is required
```

## Причина

Проблема в несоответствии типов при десериализации JSON:

1. **Входящий JSON** содержит `additionalData` как объект:
   ```json
   {
     "symbol": "GBPJPY",
     "price": 196.34135,
     "type": "com.dukascopy.charts.drawings.PriceMarkerChartObject",
     "additionalData": {"objectType": "PRICEMARKER"}
   }
   ```

2. **Модель PriceLevelData** ожидала `AdditionalData` как строку:
   ```csharp
   public string AdditionalData { get; set; }
   ```

3. **Результат**: При десериализации все поля становились пустыми, включая `Symbol`.

## Решение

### Изменен тип поля AdditionalData в модели PriceLevelData

```csharp
/// <summary>
/// Дополнительные данные (опционально) - JSON объект
/// </summary>
public object AdditionalData { get; set; }  // Изменено с string на object
```

### Обновлена логика в HttpServerService

```csharp
// Парсим JSON с настройками
var priceLevelData = JsonConvert.DeserializeObject<PriceLevelData>(processedRequestBody, settings);

// Создаем и вызываем событие нового ценового уровня (для обратной совместимости)
var priceLevelEvent = new PriceLevelEventData(
    name: priceLevelData.AdditionalData?.ToString() ?? "Price Level",
    symbol: priceLevelData.Symbol,
    levelValue: priceLevelData.Price,
    type: priceLevelData.Type,
    additionalData: priceLevelData.AdditionalData?.ToString() ?? ""
);

// Создаем и вызываем новое событие JForex объекта
var jforexObjectType = JForexChartObjectData.GetObjectTypeFromClassName(priceLevelData.Type);
var jforexChartObject = new JForexChartObjectData(
    symbol: priceLevelData.Symbol,
    price: priceLevelData.Price,
    objectType: jforexObjectType,
    className: priceLevelData.Type,
    additionalData: priceLevelData.AdditionalData?.ToString() ?? ""
);
```

### Добавлено подробное логирование

```csharp
// Логируем результат десериализации
Logger.LogTagInfo("http_service", $"Deserialized PriceLevelData: Symbol='{priceLevelData?.Symbol}', Price={priceLevelData?.Price}, Type='{priceLevelData?.Type}', AdditionalData='{priceLevelData?.AdditionalData}'");

// Логируем все ответы сервера
Logger.LogTagInfo("http_service", $"Server Response (200): {responseJson}");
Logger.LogTagInfo("http_service", $"Server Response (400): {errorResponse}");
```

## Преимущества решения

1. **Простота**: Не требуется создавать дополнительные модели
2. **Гибкость**: Поле `AdditionalData` может содержать любой JSON объект
3. **Обратная совместимость**: Система работает как со строками, так и с объектами
4. **Типобезопасность**: При необходимости преобразования в строку используется `ToString()`

## Ожидаемый результат

После применения исправлений в логах должно появиться:

```
[INFO] Deserialized PriceLevelData: Symbol='GBPJPY', Price=196.34135, Type='com.dukascopy.charts.drawings.PriceMarkerChartObject', AdditionalData='{"objectType":"PRICEMARKER"}'
[INFO] Server Response (200): {"success":true,"message":"JForex chart object processed successfully","timestamp":"2025-08-04T12:07:36.760Z"}
```

## Дополнительные улучшения

1. **Обработка JForex объектов**: Система корректно определяет тип объекта из поля `Type`
2. **Подробное логирование**: Все ответы сервера логируются для отладки
3. **Обратная совместимость**: Старая логика сохранена для совместимости

## Тестирование

Для тестирования используйте тот же JSON:

```json
{
  "symbol": "GBPJPY",
  "price": 196.34135,
  "timestamp": "2025-08-04T12:07:36.713Z",
  "type": "com.dukascopy.charts.drawings.PriceMarkerChartObject",
  "additionalData": {"objectType": "PRICEMARKER"}
}
```

Система должна корректно обработать запрос и вернуть статус 200. 