# JForex Strategy Socket Server

## Описание

`JForexStrategySocketServer` - это TCP сервер, который работает на порту 5555 и предназначен для коммуникации с Java стратегиями JForex. Сервер принимает JSON сообщения от клиентов и отправляет команды в формате JSON.

## Основные возможности

- **TCP сервер** на localhost:5555
- **JSON коммуникация** - все команды и сообщения в формате JSON
- **Многоклиентская поддержка** - может обслуживать несколько подключенных клиентов одновременно
- **Асинхронная обработка** - неблокирующая обработка клиентских соединений
- **Логирование** - подробное логирование всех операций

## Структура JSON команд

### Отправка команды очистки графика
```json
{
  "command": "clear_chart",
  "symbol": "EURUSD"
}
```

### Отправка произвольной команды
```json
{
  "command": "your_command_name",
  "symbol": "optional_symbol"
}
```

## События

### ServerStatusChanged
Срабатывает при изменении статуса сервера (запуск/остановка).

### ClientConnected
Срабатывает при подключении нового клиента.

### ClientDisconnected
Срабатывает при отключении клиента.

### MessageReceived
Срабатывает при получении сообщения от клиента.

## Основные методы

### StartAsync()
Запускает TCP сервер на localhost:5555.

### StopAsync()
Останавливает сервер и закрывает все клиентские соединения.

### SendCommandToAllClientsAsync(string command, string symbol = null)
Отправляет JSON команду всем подключенным клиентам.

### SendClearChartCommandAsync(string symbol)
Отправляет команду очистки графика для указанного символа.

### SendJsonToAllClientsAsync(string jsonMessage)
Отправляет произвольный JSON всем клиентам.

## Обработка входящих сообщений

Сервер автоматически обрабатывает следующие типы JSON сообщений:

### Command Response
```json
{
  "type": "command_response",
  "status": "success",
  "message": "Command executed successfully"
}
```

### Status Message
```json
{
  "type": "status",
  "totalObjectsFound": 10,
  "totalObjectsSent": 8,
  "totalErrors": 2,
  "totalSocketMessagesSent": 15,
  "totalSocketErrors": 1,
  "totalCommandsReceived": 5,
  "totalCommandsProcessed": 5
}
```

### Event Message
```json
{
  "event": "object_found",
  "symbol": "EURUSD",
  "className": "TrendLine",
  "objectType": "trendline",
  "price": "1.0850",
  "timestamp": "2024-01-15T10:30:00Z",
  "additionalData": {
    "slope": 0.5,
    "length": 100
  }
}
```

## Интеграция с приложением

### Регистрация в DI контейнере
Сервис автоматически регистрируется в `ServiceContainer` при запуске приложения.

### Получение сервиса
```csharp
var jforexStrategySocketServer = ServiceContainer.Instance.GetService<JForexStrategySocketServer>();
```

### Подписка на события
```csharp
jforexStrategySocketServer.ServerStatusChanged += OnServerStatusChanged;
jforexStrategySocketServer.ClientConnected += OnClientConnected;
jforexStrategySocketServer.ClientDisconnected += OnClientDisconnected;
jforexStrategySocketServer.MessageReceived += OnMessageReceived;
```

## Примеры использования

### Отправка команды очистки графика
```csharp
var success = await jforexStrategySocketServer.SendClearChartCommandAsync("EURUSD");
if (success)
{
    Logger.LogInfo("Clear chart command sent successfully");
}
```

### Отправка произвольной команды
```csharp
var success = await jforexStrategySocketServer.SendCommandToAllClientsAsync("custom_command", "GBPUSD");
```

### Отправка произвольного JSON
```csharp
var jsonCommand = "{\"command\":\"custom_action\",\"data\":{\"value\":123}}";
var success = await jforexStrategySocketServer.SendJsonToAllClientsAsync(jsonCommand);
```

## Статус индикатора

В главном окне приложения есть индикатор статуса сервера:
- **Зеленый** - сервер запущен и работает
- **Синий** - клиент подключен
- **Красный** - сервер остановлен или ошибка

## Логирование

Все операции сервера логируются через `Logger`:
- Запуск/остановка сервера
- Подключение/отключение клиентов
- Отправка команд
- Получение сообщений
- Ошибки

## Безопасность

- Сервер работает только на localhost (127.0.0.1)
- Порт 5555 используется для изоляции от других сервисов
- Все соединения обрабатываются асинхронно для предотвращения блокировок 