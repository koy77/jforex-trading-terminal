# Тегированное логирование для HTTP сервера

## Обзор

Добавлено тегированное логирование по тегу `logger.log.tag.info` для всех входных данных на всех endpoint'ах HTTP сервера.

## Что было добавлено

### 1. Логирование входящих HTTP запросов
```csharp
Logger.LogTagInfo("logger.log.tag.info", $"HTTP Request: {request.HttpMethod} {request.Url?.AbsolutePath}");
```

### 2. Логирование сырых данных от GForex
```csharp
Logger.LogTagInfo("logger.log.tag.info", $"Price Level Input Data: {requestBody}");
```

### 3. Логирование предобработанных JSON данных
```csharp
Logger.LogTagInfo("logger.log.tag.info", $"Preprocessed JSON Data: {processedRequestBody}");
```

### 4. Логирование обработанных данных
```csharp
Logger.LogTagInfo("logger.log.tag.info", $"Price Level Processed: {priceLevelData}");
```

### 5. Логирование запросов к endpoint'ам
- `/health` - логирование запросов здоровья сервера
- `/status` - логирование запросов статуса сервера
- Несуществующие endpoint'ы - логирование 404 ошибок

### 6. Логирование ошибок
```csharp
Logger.LogTagError("logger.log.tag.info", "Error message", exception);
```

## Файл лога

Все тегированные логи записываются в файл: `logger.log.tag.info.log`

## Формат записей

```
[2024-12-01 12:34:56.789] [INFO] HTTP Request: POST /pricelevel
[2024-12-01 12:34:56.790] [INFO] Price Level Input Data: {"symbol":"EURUSD","price":1.0850,"type":"support"}
[2024-12-01 12:34:56.791] [INFO] Preprocessed JSON Data: {"symbol":"EURUSD","price":1.0850,"type":"support"}
[2024-12-01 12:34:56.792] [INFO] Price Level Processed: Symbol: EURUSD, Price: 1.0850, Type: support
```

## Тестирование

Создан тестовый скрипт `test_tagged_logging.ps1` для проверки работы тегированного логирования.

## Использование

1. Запустите приложение
2. Отправьте запросы к HTTP серверу
3. Проверьте файл `logger.log.tag.info.log` для просмотра всех входных данных

## Преимущества

- **Полная трассировка:** Все входные данные логируются
- **Отдельный файл:** Логи не смешиваются с основными логами приложения
- **Детальность:** Логируются данные до и после обработки
- **Ошибки:** Все ошибки также логируются с полной информацией 