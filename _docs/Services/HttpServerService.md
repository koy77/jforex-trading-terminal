# HttpServerService

HTTP сервис для приема данных от GForex через REST API.

## Описание

`HttpServerService` предоставляет HTTP endpoint'ы для приема данных ценовых уровней от внешних систем, таких как GForex. Сервис прослушивает порт 7000 и обрабатывает JSON запросы.

## Основные возможности

- Прослушивание HTTP запросов на порту 7000
- Обработка данных ценовых уровней от GForex
- События для уведомления о полученных данных
- Логирование всех операций
- Проверка здоровья сервера
- Статус сервера

## Endpoint'ы

### POST /pricelevel
Принимает данные ценового уровня от GForex.

**Тело запроса (JSON):**
```json
{
  "symbol": "EURUSD",
  "price": 1.0850,
  "type": "support",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "additionalData": "Support level"
}
```

**Поля запроса:**
- `symbol` (обязательное) - торговый символ
- `price` (обязательное) - цена уровня
- `type` (обязательное) - тип ценового уровня (support, resistance, breakout, entry, exit)
- `timestamp` (опциональное) - временная метка
- `additionalData` (опциональное) - дополнительные данные

**Ответ:**
```json
{
  "success": true,
  "message": "Price level data received",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### GET /health
Проверка здоровья сервера.

**Ответ:**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "uptime": "00:15:30",
  "isRunning": true
}
```

### GET /status
Получение статуса сервера.

**Ответ:**
```json
{
  "isRunning": true,
  "port": 7000,
  "recentPriceLevelsCount": 5,
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

## События

### PriceLevelReceived
Возникает при получении данных ценового уровня.

```csharp
httpServerService.PriceLevelReceived += (sender, priceLevelData) =>
{
    Console.WriteLine($"Received: {priceLevelData}");
};
```

### StatusChanged
Возникает при изменении статуса сервера.

```csharp
httpServerService.StatusChanged += (sender, status) =>
{
    Console.WriteLine($"Server status: {status}");
};
```

## Использование

### Регистрация в DI контейнере
```csharp
var container = ServiceContainer.Instance;
container.RegisterSingleton(new HttpServerService());
```

### Запуск сервера
```csharp
var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
await httpServerService.StartAsync();
```

### Остановка сервера
```csharp
await httpServerService.StopAsync();
```

### Получение последних данных
```csharp
var recentData = httpServerService.GetRecentPriceLevels();
```

## Пример отправки данных

```csharp
using var client = new HttpClient();
var priceLevelData = new PriceLevelData
{
    Symbol = "EURUSD",
    Price = 1.0850m,
    Timestamp = DateTime.Now,
    AdditionalData = "Support level"
};

var json = JsonConvert.SerializeObject(priceLevelData);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await client.PostAsync("http://localhost:7000/pricelevel", content);
```

## Логирование

Сервис автоматически логирует:
- Запуск и остановку сервера
- Полученные HTTP запросы
- Обработанные данные ценовых уровней
- Ошибки и исключения

## Безопасность

- Сервер прослушивает только localhost (127.0.0.1)
- Валидация входящих JSON данных
- Обработка исключений для предотвращения сбоев

## Зависимости

- `Newtonsoft.Json` - для сериализации/десериализации JSON
- `System.Net.HttpListener` - для HTTP сервера
- `ScreenCaptureApp.Models.PriceLevelData` - модель данных
- `ScreenCaptureApp.Services.Logger` - для логирования 