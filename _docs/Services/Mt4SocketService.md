# Mt4SocketService

## Описание
`Mt4SocketService` - сервис для TCP-соединения с MetaTrader 4. Обеспечивает отправку команд, получение истории ордеров, обработку событий и интеграцию с MT4 через сокет-соединение.

## Зависимости
- `System.Net.Sockets` - для TCP-соединения
- `System.Text.Json` - для сериализации/десериализации JSON
- `System.Threading.Tasks` - для асинхронных операций

## Основные поля

### Приватные поля
```csharp
private TcpClient _tcpClient;
private NetworkStream _networkStream;
private StreamReader _reader;
private StreamWriter _writer;
private string _host;
private int _port;
private bool _isConnected = false;
private Task _readTask;
private CancellationTokenSource _cancellationTokenSource;
```

### Константы
```csharp
private const int DEFAULT_PORT = 5555;
private const string DEFAULT_HOST = "localhost";
private const int BUFFER_SIZE = 4096;
```

## События

```csharp
public event EventHandler<string> MessageReceived;
public event EventHandler<bool> ConnectionStatusChanged;
public event EventHandler<OrdersSummary> OrdersSummaryReceived;
```

## Основные методы

### ConnectAsync
```csharp
public async Task<bool> ConnectAsync(string host = null, int port = 0)
```

**Назначение**: Устанавливает TCP-соединение с MT4.

**Параметры**:
- `host` - хост для подключения (по умолчанию localhost)
- `port` - порт для подключения (по умолчанию 5555)

**Возвращает**: `true` если соединение установлено успешно.

**Логика работы**:
1. Создает новый `TcpClient`
2. Устанавливает соединение с указанным хостом и портом
3. Получает `NetworkStream`
4. Создает `StreamReader` и `StreamWriter`
5. Запускает асинхронное чтение сообщений
6. Устанавливает флаг `_isConnected = true`
7. Вызывает событие `ConnectionStatusChanged`

### DisconnectAsync
```csharp
public async Task DisconnectAsync()
```

**Назначение**: Отключает TCP-соединение с MT4.

**Логика работы**:
1. Отменяет токен отмены
2. Закрывает `StreamReader` и `StreamWriter`
3. Закрывает `NetworkStream`
4. Закрывает `TcpClient`
5. Устанавливает флаг `_isConnected = false`
6. Вызывает событие `ConnectionStatusChanged`

### WriteAsync
```csharp
public async Task WriteAsync(string message)
```

**Назначение**: Отправляет сообщение в MT4.

**Параметры**:
- `message` - сообщение для отправки

**Логика работы**:
1. Проверяет, установлено ли соединение
2. Записывает сообщение в поток
3. Очищает буфер потока
4. Логирует отправленное сообщение

### ReadLineAsync
```csharp
private async Task<string> ReadLineAsync()
```

**Назначение**: Читает строку из потока MT4.

**Возвращает**: Прочитанную строку или `null` при ошибке.

**Логика работы**:
1. Читает строку из `StreamReader`
2. Обрабатывает исключения
3. Возвращает прочитанную строку

### ProcessIncomingMessage
```csharp
private void ProcessIncomingMessage(string message)
```

**Назначение**: Обрабатывает входящие сообщения от MT4.

**Параметры**:
- `message` - входящее сообщение

**Логика работы**:
1. Логирует входящее сообщение
2. Пытается десериализовать JSON
3. Обрабатывает различные типы сообщений:
   - `orders_summary` - вызывает событие `OrdersSummaryReceived`
   - `order_closed` - обрабатывает закрытие ордера
   - Другие типы сообщений
4. Вызывает событие `MessageReceived`

### HandleOrderClosedEvent
```csharp
private void HandleOrderClosedEvent(JsonElement data)
```

**Назначение**: Обрабатывает событие закрытия ордера.

**Параметры**:
- `data` - данные о закрытом ордере

**Логика работы**:
1. Извлекает информацию о закрытом ордере
2. Логирует информацию о закрытии
3. Обновляет UI индикаторы через `Mt4Helper`

### HandleOrdersSummaryEvent
```csharp
private void HandleOrdersSummaryEvent(JsonElement data)
```

**Назначение**: Обрабатывает событие получения сводки ордеров.

**Параметры**:
- `data` - данные сводки ордеров

**Логика работы**:
1. Десериализует данные в `OrdersSummary`
2. Логирует информацию о сводке
3. Вызывает событие `OrdersSummaryReceived`

## Модели данных

### OrdersSummary
```csharp
public class OrdersSummary
{
    public List<SymbolInfo> Symbols { get; set; } = new List<SymbolInfo>();
    public double TotalProfit { get; set; }
    public int TotalOrders { get; set; }
    public string Timestamp { get; set; }
}
```

### SymbolInfo
```csharp
public class SymbolInfo
{
    public string Symbol { get; set; }
    public double Lots { get; set; }
    public double Percent { get; set; }
    public double ProfitPoints { get; set; }
    public string Direction { get; set; }
    public string OrderType { get; set; }
    public int Ticket { get; set; }
}
```

### OrderClosedEvent
```csharp
public class OrderClosedEvent
{
    public int Ticket { get; set; }
    public string Symbol { get; set; }
    public double Profit { get; set; }
    public string CloseReason { get; set; }
    public string Timestamp { get; set; }
}
```

## Команды для MT4

### Основные команды
```json
// Получение сводки ордеров
{"cmd": "get_orders_summary"}

// Закрытие позиций по символу
{"cmd": "close_positions", "symbol": "EURUSD"}

// Открытие ордера
{"cmd": "open_order", "symbol": "EURUSD", "type": "buy", "lots": 0.1, "price": 1.1234}

// Установка стоп-лосса
{"cmd": "set_sl", "ticket": 12345, "sl": 1.1200}

// Установка тейк-профита
{"cmd": "set_tp", "ticket": 12345, "tp": 1.1300}
```

## Взаимодействие с другими компонентами

### Используется в
- **MainWindow**: для интеграции с MT4
- **TradingToolbar**: для отображения информации об ордерах
- **CaptureTrackingService**: для автоматической торговли
- **Mt4Helper**: для обновления UI индикаторов

### Использует
- **Mt4Helper**: для обновления UI
- **Logger**: для логирования операций

## Особенности реализации

1. **Асинхронность**: Все операции выполняются асинхронно
2. **JSON протокол**: Использует JSON для обмена сообщениями
3. **Автоматическое переподключение**: Поддержка переподключения при разрыве соединения
4. **Обработка ошибок**: Корректная обработка сетевых ошибок
5. **События**: Генерация событий для уведомления других компонентов

## Примеры использования

```csharp
// Подключение к MT4
await mt4SocketService.ConnectAsync("localhost", 5555);

// Подписка на события
mt4SocketService.MessageReceived += OnMessageReceived;
mt4SocketService.OrdersSummaryReceived += OnOrdersSummaryReceived;
mt4SocketService.ConnectionStatusChanged += OnConnectionStatusChanged;

// Отправка команды
await mt4SocketService.WriteAsync("{\"cmd\": \"get_orders_summary\"}");

// Отключение
await mt4SocketService.DisconnectAsync();
```

## Обработка событий

### MessageReceived
```csharp
private void OnMessageReceived(object sender, string message)
{
    // Обработка входящих сообщений
    Console.WriteLine($"Received: {message}");
}
```

### OrdersSummaryReceived
```csharp
private void OnOrdersSummaryReceived(object sender, OrdersSummary summary)
{
    // Обновление UI с информацией об ордерах
    foreach (var symbol in summary.Symbols)
    {
        Console.WriteLine($"Symbol: {symbol.Symbol}, Profit: {symbol.ProfitPoints}");
    }
}
```

### ConnectionStatusChanged
```csharp
private void OnConnectionStatusChanged(object sender, bool isConnected)
{
    // Обновление статуса подключения
    Console.WriteLine($"MT4 connection: {(isConnected ? "Connected" : "Disconnected")}");
}
```

## Безопасность и производительность

1. **Таймауты**: Установка таймаутов для сетевых операций
2. **Буферизация**: Эффективная буферизация данных
3. **Отмена операций**: Поддержка отмены длительных операций
4. **Логирование**: Подробное логирование всех операций
5. **Обработка исключений**: Корректная обработка сетевых исключений 