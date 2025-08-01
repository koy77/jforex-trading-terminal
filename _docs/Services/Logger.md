# Logger

## Описание
`Logger` - сервис для логирования событий приложения. Обеспечивает запись информационных, предупреждающих, критических сообщений и ошибок в файлы логов с различными уровнями детализации.

## Зависимости
- `System.IO` - для работы с файлами
- `System.Threading` - для потокобезопасности
- `System.Text` - для форматирования сообщений

## Основные поля

### Приватные поля
```csharp
private static readonly object _lock = new object();
private static string _logFilePath;
private static LogLevel _currentLogLevel = LogLevel.Info;
private static bool _isInitialized = false;
```

### Константы
```csharp
private const string DEFAULT_LOG_FILE = "app.log";
private const string LOG_FORMAT = "[{0}] {1}: {2}";
private const string ERROR_LOG_FORMAT = "[{0}] {1}: {2} - Exception: {3}";
```

## Основные методы

### Initialize
```csharp
public static void Initialize(string logFilePath = null, LogLevel logLevel = LogLevel.Info)
```

**Назначение**: Инициализирует систему логирования.

**Параметры**:
- `logFilePath` - путь к файлу лога (по умолчанию "app.log")
- `logLevel` - уровень логирования (по умолчанию Info)

**Логика работы**:
1. Устанавливает путь к файлу лога
2. Устанавливает уровень логирования
3. Создает директорию для логов если не существует
4. Устанавливает флаг инициализации
5. Записывает сообщение о запуске логирования

### LogInfo
```csharp
public static void LogInfo(string message)
```

**Назначение**: Записывает информационное сообщение в лог.

**Параметры**:
- `message` - текст сообщения

**Логика работы**:
1. Проверяет инициализацию логгера
2. Проверяет уровень логирования
3. Форматирует сообщение
4. Записывает в файл лога

### LogWarning
```csharp
public static void LogWarning(string message)
```

**Назначение**: Записывает предупреждающее сообщение в лог.

**Параметры**:
- `message` - текст сообщения

**Логика работы**:
1. Проверяет инициализацию логгера
2. Проверяет уровень логирования
3. Форматирует сообщение с уровнем Warning
4. Записывает в файл лога

### LogError
```csharp
public static void LogError(string message, Exception exception = null)
```

**Назначение**: Записывает сообщение об ошибке в лог.

**Параметры**:
- `message` - текст сообщения
- `exception` - исключение (опционально)

**Логика работы**:
1. Проверяет инициализацию логгера
2. Проверяет уровень логирования
3. Форматирует сообщение с уровнем Error
4. Добавляет информацию об исключении если передано
5. Записывает в файл лога

### LogDebug
```csharp
public static void LogDebug(string message)
```

**Назначение**: Записывает отладочное сообщение в лог.

**Параметры**:
- `message` - текст сообщения

**Логика работы**:
1. Проверяет инициализацию логгера
2. Проверяет уровень логирования (Debug)
3. Форматирует сообщение с уровнем Debug
4. Записывает в файл лога

### LogCritical
```csharp
public static void LogCritical(string message, Exception exception = null)
```

**Назначение**: Записывает критическое сообщение в лог.

**Параметры**:
- `message` - текст сообщения
- `exception` - исключение (опционально)

**Логика работы**:
1. Проверяет инициализацию логгера
2. Проверяет уровень логирования
3. Форматирует сообщение с уровнем Critical
4. Добавляет информацию об исключении если передано
5. Записывает в файл лога

### WriteToLog
```csharp
private static void WriteToLog(string message)
```

**Назначение**: Записывает сообщение в файл лога.

**Параметры**:
- `message` - форматированное сообщение для записи

**Логика работы**:
1. Блокирует доступ к файлу для потокобезопасности
2. Добавляет временную метку к сообщению
3. Записывает сообщение в файл
4. Очищает буфер файла
5. Освобождает блокировку

## Уровни логирования

### Debug
- **Описание**: Отладочные сообщения
- **Использование**: Детальная отладочная информация
- **Приоритет**: 0

### Info
- **Описание**: Информационные сообщения
- **Использование**: Общая информация о работе приложения
- **Приоритет**: 1

### Warning
- **Описание**: Предупреждающие сообщения
- **Использование**: Предупреждения, некритичные проблемы
- **Приоритет**: 2

### Error
- **Описание**: Сообщения об ошибках
- **Использование**: Ошибки, исключения
- **Приоритет**: 3

### Critical
- **Описание**: Критические сообщения
- **Использование**: Критические ошибки, сбои системы
- **Приоритет**: 4

## Модели данных

### LogLevel
```csharp
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
```

## Форматы сообщений

### Информационное сообщение
```
[2024-12-01 12:34:56] INFO: Application started successfully
```

### Предупреждающее сообщение
```
[2024-12-01 12:34:57] WARNING: Connection to MT4 lost
```

### Сообщение об ошибке
```
[2024-12-01 12:34:58] ERROR: Failed to save capture data - Exception: System.IO.IOException: Access denied
```

### Критическое сообщение
```
[2024-12-01 12:34:59] CRITICAL: Database connection failed - Exception: System.Data.SqlException: Connection timeout
```

## Взаимодействие с другими компонентами

### Используется в
- **Все сервисы**: для логирования операций
- **MainWindow**: для логирования событий UI
- **CaptureService**: для логирования захватов
- **DatabaseService**: для логирования операций с базой данных
- **Mt4SocketService**: для логирования сетевых операций
- **BinaryOptionsSocketService**: для логирования торговых команд

## Особенности реализации

1. **Статический класс**: Доступен из любого места приложения
2. **Потокобезопасность**: Использует блокировку для безопасной записи
3. **Уровни логирования**: Поддержка различных уровней детализации
4. **Автоматическое создание файлов**: Автоматически создает файлы логов
5. **Форматирование**: Автоматическое форматирование сообщений

## Примеры использования

```csharp
// Инициализация логгера
Logger.Initialize("app.log", LogLevel.Info);

// Информационные сообщения
Logger.LogInfo("Application started successfully");
Logger.LogInfo("User logged in: john.doe");

// Предупреждающие сообщения
Logger.LogWarning("Connection to MT4 lost");
Logger.LogWarning("Low disk space: 1GB remaining");

// Сообщения об ошибках
try
{
    // Код, который может вызвать исключение
}
catch (Exception ex)
{
    Logger.LogError("Failed to process data", ex);
}

// Отладочные сообщения
Logger.LogDebug("Processing item: 12345");
Logger.LogDebug("Memory usage: 150MB");

// Критические сообщения
Logger.LogCritical("Database connection failed", exception);
Logger.LogCritical("Application crash detected");
```

## Обработка ошибок

```csharp
// В сервисах
public async Task<bool> ConnectAsync()
{
    try
    {
        Logger.LogInfo("Attempting to connect to MT4");
        // Логика подключения
        Logger.LogInfo("Successfully connected to MT4");
        return true;
    }
    catch (Exception ex)
    {
        Logger.LogError("Failed to connect to MT4", ex);
        return false;
    }
}

// В UI
private void Button_Click(object sender, RoutedEventArgs e)
{
    try
    {
        Logger.LogInfo("User clicked capture button");
        // Логика обработки
        Logger.LogInfo("Capture completed successfully");
    }
    catch (Exception ex)
    {
        Logger.LogError("Error during capture", ex);
        MessageBox.Show("Capture failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

## Конфигурация

### Настройка уровней логирования
```csharp
// Только ошибки и критические сообщения
Logger.Initialize("app.log", LogLevel.Error);

// Все сообщения включая отладочные
Logger.Initialize("app.log", LogLevel.Debug);

// Только информационные и выше
Logger.Initialize("app.log", LogLevel.Info);
```

### Настройка файлов логов
```csharp
// Лог в корневой папке
Logger.Initialize("app.log");

// Лог в папке logs
Logger.Initialize("logs/app.log");

// Лог с датой в имени
Logger.Initialize($"logs/app_{DateTime.Now:yyyyMMdd}.log");
```

## Ротация логов

### Автоматическая ротация
```csharp
// Можно добавить автоматическую ротацию логов
private static void RotateLogIfNeeded()
{
    var fileInfo = new FileInfo(_logFilePath);
    if (fileInfo.Exists && fileInfo.Length > 10 * 1024 * 1024) // 10MB
    {
        var backupPath = $"{_logFilePath}.{DateTime.Now:yyyyMMdd_HHmmss}";
        File.Move(_logFilePath, backupPath);
    }
}
```

## Производительность

1. **Буферизация**: Использует буферизацию для эффективной записи
2. **Асинхронная запись**: Может быть легко адаптирован для асинхронной записи
3. **Минимальное влияние**: Минимальное влияние на производительность приложения
4. **Ограничение размера**: Контроль размера файлов логов

## Безопасность

1. **Потокобезопасность**: Безопасная работа в многопоточной среде
2. **Обработка исключений**: Корректная обработка ошибок записи
3. **Валидация данных**: Проверка корректности сообщений
4. **Освобождение ресурсов**: Гарантированное освобождение ресурсов файлов 