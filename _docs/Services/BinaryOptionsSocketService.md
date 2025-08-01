# BinaryOptionsSocketService

## Описание
`BinaryOptionsSocketService` - сервис для отправки торговых команд в binary-сокет. Обеспечивает интеграцию с binary-брокерами (PocketOption, Binarium, Quotex) через TCP-соединение и отправку команд на покупку/продажу опционов.

## Зависимости
- `System.Net.Sockets` - для TCP-соединения
- `System.Text.Json` - для сериализации/десериализации JSON команд
- `System.Threading.Tasks` - для асинхронных операций

## Основные поля

### Приватные поля
```csharp
private TcpClient _binarySocketClient;
private NetworkStream _binaryNetworkStream;
private StreamWriter _binaryWriter;
private bool _isBinaryConnected = false;
private readonly object _binarySocketLock = new object();
```

### Константы
```csharp
private const int BINARY_SOCKET_PORT = 5556;
private const string BINARY_SOCKET_HOST = "localhost";
```

## Основные методы

### SendBuyCommand
```csharp
public async Task<bool> SendBuyCommand(string brokerName, string symbolName, double risk, int duration)
```

**Назначение**: Отправляет команду покупки опциона в binary-сокет.

**Параметры**:
- `brokerName` - название брокера (PocketOption, Binarium, Quotex)
- `symbolName` - название символа
- `risk` - значение риска
- `duration` - длительность опциона

**Возвращает**: `true` если команда отправлена успешно.

**Логика работы**:
1. Проверяет подключение к binary-сокету
2. Создает JSON команду для покупки опциона
3. Отправляет команду через сокет
4. Логирует операцию
5. Возвращает результат отправки

### SendSellCommand
```csharp
public async Task<bool> SendSellCommand(string brokerName, string symbolName, double risk, int duration)
```

**Назначение**: Отправляет команду продажи опциона в binary-сокет.

**Параметры**:
- `brokerName` - название брокера
- `symbolName` - название символа
- `risk` - значение риска
- `duration` - длительность опциона

**Возвращает**: `true` если команда отправлена успешно.

**Логика работы**:
1. Проверяет подключение к binary-сокету
2. Создает JSON команду для продажи опциона
3. Отправляет команду через сокет
4. Логирует операцию
5. Возвращает результат отправки

### ConnectToBinarySocket
```csharp
private async Task<bool> ConnectToBinarySocket()
```

**Назначение**: Устанавливает подключение к binary-сокету.

**Возвращает**: `true` если подключение установлено успешно.

**Логика работы**:
1. Проверяет, не подключен ли уже сокет
2. Создает новый `TcpClient`
3. Устанавливает соединение с binary-сокетом
4. Создает `NetworkStream` и `StreamWriter`
5. Устанавливает флаг `_isBinaryConnected = true`
6. Логирует успешное подключение

### SendBinaryCommand
```csharp
private async Task<bool> SendBinaryCommand(string command)
```

**Назначение**: Отправляет команду в binary-сокет.

**Параметры**:
- `command` - JSON команда для отправки

**Возвращает**: `true` если команда отправлена успешно.

**Логика работы**:
1. Проверяет подключение к binary-сокету
2. Отправляет команду через `StreamWriter`
3. Очищает буфер потока
4. Логирует отправленную команду
5. Возвращает результат отправки

## Команды для binary-сокета

### Команда покупки
```json
{
  "cmd": "buy",
  "broker": "PocketOption",
  "symbol": "EURUSD",
  "risk": "10",
  "duration": "2"
}
```

### Команда продажи
```json
{
  "cmd": "sell",
  "broker": "PocketOption",
  "symbol": "EURUSD",
  "risk": "10",
  "duration": "2"
}
```

## Поддерживаемые брокеры

### PocketOption
- Название: `"PocketOption"`
- Поддерживаемые символы: EURUSD, GBPUSD, USDJPY, XAUUSD, GBPJPY, EURJPY
- Поддерживаемые длительности: 1, 2, 3, 5 минут

### Binarium
- Название: `"Binarium"`
- Поддерживаемые символы: EURUSD, GBPUSD, USDJPY, XAUUSD, GBPJPY, EURJPY
- Поддерживаемые длительности: 1, 2, 3, 5 минут

### Quotex
- Название: `"Quotex"`
- Поддерживаемые символы: EURUSD, GBPUSD, USDJPY, XAUUSD, GBPJPY, EURJPY
- Поддерживаемые длительности: 1, 2, 3, 5 минут

## Взаимодействие с другими компонентами

### Используется в
- **CaptureTrackingService**: для автоматической торговли при срабатывании паттернов
- **CanvasWindow**: для отправки команд при торговле
- **MainWindow**: для интеграции с binary-брокерами

### Использует
- **Logger**: для логирования операций

## Особенности реализации

1. **Асинхронность**: Все операции выполняются асинхронно
2. **JSON протокол**: Использует JSON для обмена командами
3. **Автоматическое подключение**: Автоматически подключается к binary-сокету при необходимости
4. **Потокобезопасность**: Использует блокировку для безопасной работы с сокетом
5. **Обработка ошибок**: Корректная обработка ошибок подключения и отправки

## Примеры использования

```csharp
// Отправка команды покупки для PocketOption
bool success = await binarySocketService.SendBuyCommand("PocketOption", "EURUSD", 10, 2);
if (success)
{
    Console.WriteLine("Buy command sent successfully");
}

// Отправка команды продажи для Binarium
success = await binarySocketService.SendSellCommand("Binarium", "GBPUSD", 5, 3);
if (success)
{
    Console.WriteLine("Sell command sent successfully");
}
```

## Обработка ошибок

```csharp
try
{
    bool success = await binarySocketService.SendBuyCommand("PocketOption", "EURUSD", 10, 2);
    if (!success)
    {
        Logger.LogWarning("Failed to send buy command for PocketOption");
    }
}
catch (Exception ex)
{
    Logger.LogError($"Error sending buy command: {ex.Message}", ex);
}
```

## Логирование

Сервис ведет подробное логирование всех операций:

```
[INFO] Connecting to binary socket: localhost:5556
[INFO] Binary socket connected successfully
[INFO] Sending binary command: {"cmd":"buy","broker":"PocketOption","symbol":"EURUSD","risk":"10","duration":"2"}
[INFO] Binary command sent successfully
[INFO] Sending binary command: {"cmd":"sell","broker":"Binarium","symbol":"GBPUSD","risk":"5","duration":"3"}
[INFO] Binary command sent successfully
```

## Интеграция с CaptureTrackingService

```csharp
// В CaptureTrackingService при срабатывании паттерна
private async void ExecuteTradeCommand(CaptureData captureData)
{
    if (captureData.Broker == "PocketOption" || 
        captureData.Broker == "Binarium" || 
        captureData.Broker == "Quotex")
    {
        try
        {
            var binarySocketService = ServiceContainer.Instance.GetService<BinaryOptionsSocketService>();
            if (binarySocketService != null)
            {
                bool success;
                if (captureData.Direction == "Up")
                {
                    success = await binarySocketService.SendBuyCommand(
                        captureData.Broker, 
                        captureData.Symbol, 
                        captureData.Risk, 
                        captureData.Duration);
                }
                else
                {
                    success = await binarySocketService.SendSellCommand(
                        captureData.Broker, 
                        captureData.Symbol, 
                        captureData.Risk, 
                        captureData.Duration);
                }
                
                if (success)
                {
                    Logger.LogInfo($"Trade command sent successfully for {captureData.Symbol}");
                }
                else
                {
                    Logger.LogWarning($"Failed to send trade command for {captureData.Symbol}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending trade command: {ex.Message}", ex);
        }
    }
}
```

## Производительность и безопасность

1. **Эффективное подключение**: Переиспользует подключение к binary-сокету
2. **Блокировка**: Использует блокировку для безопасной работы с сокетом
3. **Таймауты**: Устанавливает таймауты для сетевых операций
4. **Обработка исключений**: Корректная обработка сетевых исключений
5. **Логирование**: Подробное логирование для отладки

## Конфигурация

Сервис использует следующие настройки по умолчанию:
- **Хост**: `localhost`
- **Порт**: `5556`
- **Таймаут подключения**: 5 секунд
- **Таймаут отправки**: 3 секунды

Эти настройки можно изменить в коде сервиса при необходимости.

## Структура команд

### Общая структура команды
```json
{
  "cmd": "buy|sell",
  "broker": "PocketOption|Binarium|Quotex",
  "symbol": "EURUSD|GBPUSD|USDJPY|XAUUSD|GBPJPY|EURJPY",
  "risk": "1-100",
  "duration": "1|2|3|5"
}
```

### Примеры команд

#### Покупка EURUSD на PocketOption
```json
{
  "cmd": "buy",
  "broker": "PocketOption",
  "symbol": "EURUSD",
  "risk": "10",
  "duration": "2"
}
```

#### Продажа GBPUSD на Binarium
```json
{
  "cmd": "sell",
  "broker": "Binarium",
  "symbol": "GBPUSD",
  "risk": "5",
  "duration": "3"
}
```

#### Покупка XAUUSD на Quotex
```json
{
  "cmd": "buy",
  "broker": "Quotex",
  "symbol": "XAUUSD",
  "risk": "15",
  "duration": "5"
}
``` 