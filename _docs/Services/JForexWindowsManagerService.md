# JForexWindowsManagerService

## Описание
`JForexWindowsManagerService` - сервис для управления окнами JForex и отправки команд в binary-сокет. Обеспечивает интеграцию с binary-брокерами (PocketOption, Binarium, Quotex) через сокет-соединение.

## Зависимости
- `System.Net.Sockets` - для TCP-соединения с binary-сокетом
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

### SetRisk
```csharp
public async Task<bool> SetRisk(string brokerName, double risk)
```

**Назначение**: Отправляет команду установки риска в binary-сокет.

**Параметры**:
- `brokerName` - название брокера (PocketOption, Binarium, Quotex)
- `risk` - значение риска

**Возвращает**: `true` если команда отправлена успешно.

**Логика работы**:
1. Проверяет подключение к binary-сокету
2. Создает JSON команду для установки риска
3. Отправляет команду через сокет
4. Логирует операцию
5. Возвращает результат отправки

### SetDuration
```csharp
public async Task<bool> SetDuration(string brokerName, int duration)
```

**Назначение**: Отправляет команду установки длительности в binary-сокет.

**Параметры**:
- `brokerName` - название брокера
- `duration` - значение длительности

**Возвращает**: `true` если команда отправлена успешно.

**Логика работы**:
1. Проверяет подключение к binary-сокету
2. Создает JSON команду для установки длительности
3. Отправляет команду через сокет
4. Логирует операцию
5. Возвращает результат отправки

### OpenSymbol
```csharp
public async Task<bool> OpenSymbol(string brokerName, string symbolName)
```

**Назначение**: Отправляет команду открытия символа в binary-сокет.

**Параметры**:
- `brokerName` - название брокера
- `symbolName` - название символа

**Возвращает**: `true` если команда отправлена успешно.

**Логика работы**:
1. Проверяет подключение к binary-сокету
2. Создает JSON команду для открытия символа
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

### Установка риска
```json
{
  "cmd": "set_risk",
  "broker": "PocketOption",
  "risk": "10"
}
```

### Установка длительности
```json
{
  "cmd": "set_duration",
  "broker": "PocketOption",
  "duration": "2"
}
```

### Открытие символа
```json
{
  "cmd": "open_symbol",
  "symbol": "EURUSD",
  "broker": "PocketOption"
}
```

## Поддерживаемые брокеры

### PocketOption
- Название: `"PocketOption"`
- Поддерживаемые символы: EURUSD, GBPUSD, USDJPY, XAUUSD, GBPJPY, EURJPY

### Binarium
- Название: `"Binarium"`
- Поддерживаемые символы: EURUSD, GBPUSD, USDJPY, XAUUSD, GBPJPY, EURJPY

### Quotex
- Название: `"Quotex"`
- Поддерживаемые символы: EURUSD, GBPUSD, USDJPY, XAUUSD, GBPJPY, EURJPY

## Взаимодействие с другими компонентами

### Используется в
- **TradingToolbar**: для отправки команд при изменении настроек
- **MainWindow**: для интеграции с binary-брокерами
- **CanvasWindow**: для отправки команд при торговле

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
// Установка риска для PocketOption
bool success = await jforexService.SetRisk("PocketOption", 10);
if (success)
{
    Console.WriteLine("Risk set successfully");
}

// Установка длительности для Binarium
success = await jforexService.SetDuration("Binarium", 2);
if (success)
{
    Console.WriteLine("Duration set successfully");
}

// Открытие символа для Quotex
success = await jforexService.OpenSymbol("Quotex", "EURUSD");
if (success)
{
    Console.WriteLine("Symbol opened successfully");
}
```

## Обработка ошибок

```csharp
try
{
    bool success = await jforexService.SetRisk("PocketOption", 10);
    if (!success)
    {
        Logger.LogWarning("Failed to set risk for PocketOption");
    }
}
catch (Exception ex)
{
    Logger.LogError($"Error setting risk: {ex.Message}", ex);
}
```

## Логирование

Сервис ведет подробное логирование всех операций:

```
[INFO] Connecting to binary socket: localhost:5556
[INFO] Binary socket connected successfully
[INFO] Sending binary command: {"cmd":"set_risk","broker":"PocketOption","risk":"10"}
[INFO] Binary command sent successfully
[INFO] Sending binary command: {"cmd":"set_duration","broker":"PocketOption","duration":"2"}
[INFO] Binary command sent successfully
```

## Производительность и безопасность

1. **Эффективное подключение**: Переиспользует подключение к binary-сокету
2. **Блокировка**: Использует блокировку для безопасной работы с сокетом
3. **Таймауты**: Устанавливает таймауты для сетевых операций
4. **Обработка исключений**: Корректная обработка сетевых исключений
5. **Логирование**: Подробное логирование для отладки

## Интеграция с TradingToolbar

```csharp
// В TradingToolbar при изменении риска
private async void SendRiskCommandToBinarySocket(double risk)
{
    if (SelectedBroker == BrokerType.Forex)
    {
        return; // Только для binary-брокеров
    }

    try
    {
        var jforexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
        if (jforexService != null)
        {
            string brokerName = SelectedBroker.ToString();
            bool success = await jforexService.SetRisk(brokerName, risk);
            
            if (!success)
            {
                Logger.LogWarning($"Failed to set risk {risk} for broker {brokerName}");
            }
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"Error setting risk via JForex service: {ex.Message}", ex);
    }
}
```

## Конфигурация

Сервис использует следующие настройки по умолчанию:
- **Хост**: `localhost`
- **Порт**: `5556`
- **Таймаут подключения**: 5 секунд
- **Таймаут отправки**: 3 секунды

Эти настройки можно изменить в коде сервиса при необходимости. 