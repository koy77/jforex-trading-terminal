# CaptureTrackingService

## Описание
`CaptureTrackingService` - сервис для отслеживания и автоматической торговли на основе захваченных паттернов. Обеспечивает мониторинг захватов, сравнение с текущими условиями рынка и автоматическое выполнение торговых операций.

## Зависимости
- `DatabaseService` - для получения захватов из базы данных
- `BinaryOptionsSocketService` - для отправки торговых команд
- `Mt4SocketService` - для интеграции с MT4
- `ScreenshotService` - для создания скриншотов текущего состояния
- `System.Threading.Timer` - для периодического мониторинга

## Основные поля

### Приватные поля
```csharp
private readonly DatabaseService _databaseService;
private readonly BinaryOptionsSocketService _binarySocketService;
private readonly Mt4SocketService _mt4SocketService;
private readonly ScreenshotService _screenshotService;
private Timer _trackingTimer;
private bool _isTrackingEnabled = false;
private readonly object _trackingLock = new object();
```

### Константы
```csharp
private const int TRACKING_INTERVAL_MS = 1000; // 1 секунда
private const double SIMILARITY_THRESHOLD = 0.85; // 85% схожести
```

## Основные методы

### StartTracking
```csharp
public void StartTracking()
```

**Назначение**: Запускает отслеживание захватов.

**Логика работы**:
1. Проверяет, не запущено ли уже отслеживание
2. Создает таймер для периодического мониторинга
3. Устанавливает флаг `_isTrackingEnabled = true`
4. Логирует запуск отслеживания

### StopTracking
```csharp
public void StopTracking()
```

**Назначение**: Останавливает отслеживание захватов.

**Логика работы**:
1. Останавливает таймер отслеживания
2. Устанавливает флаг `_isTrackingEnabled = false`
3. Освобождает ресурсы таймера
4. Логирует остановку отслеживания

### TrackingCallback
```csharp
private void TrackingCallback(object state)
```

**Назначение**: Callback для периодического мониторинга захватов.

**Логика работы**:
1. Получает все захваты для трекинга из базы данных
2. Для каждого захвата проверяет текущие условия рынка
3. Сравнивает текущее состояние с захваченным паттерном
4. При совпадении выполняет торговую операцию
5. Обновляет статус захвата

### CheckPatternMatch
```csharp
private async Task<bool> CheckPatternMatch(CaptureData captureData)
```

**Назначение**: Проверяет соответствие текущего состояния захваченному паттерну.

**Параметры**:
- `captureData` - данные захвата для проверки

**Возвращает**: `true` если паттерн совпадает.

**Логика работы**:
1. Создает скриншот текущего состояния в области захвата
2. Сравнивает с сохраненным скриншотом паттерна
3. Вычисляет процент схожести
4. Возвращает результат сравнения

### ExecuteTradeCommand
```csharp
private async Task ExecuteTradeCommand(CaptureData captureData)
```

**Назначение**: Выполняет торговую команду на основе захваченного паттерна.

**Параметры**:
- `captureData` - данные захвата для торговли

**Логика работы**:
1. Определяет тип брокера
2. Для binary-брокеров отправляет команду через `BinaryOptionsSocketService`
3. Для MT4 отправляет команду через `Mt4SocketService`
4. Обновляет статус захвата в базе данных
5. Логирует выполненную операцию

### UpdateCaptureStatus
```csharp
private void UpdateCaptureStatus(string captureId, bool isFired, bool isSkipped = false)
```

**Назначение**: Обновляет статус захвата в базе данных.

**Параметры**:
- `captureId` - ID захвата
- `isFired` - статус срабатывания
- `isSkipped` - статус пропуска (опционально)

**Логика работы**:
1. Обновляет статус захвата в базе данных
2. Логирует изменение статуса

## Алгоритм сравнения паттернов

### Визуальное сравнение
1. **Создание скриншота**: Создается скриншот текущего состояния в области захвата
2. **Предобработка**: Изображения приводятся к одинаковому размеру и формату
3. **Сравнение пикселей**: Вычисляется процент совпадающих пикселей
4. **Порог схожести**: Используется порог 85% для определения совпадения

### Дополнительные критерии
- **Символ**: Должен совпадать с захваченным
- **Время**: Проверяется актуальность паттерна
- **Брокер**: Учитывается тип брокера

## Взаимодействие с другими компонентами

### Использует
- **DatabaseService**: для получения и обновления захватов
- **BinaryOptionsSocketService**: для отправки команд в binary-сокет
- **Mt4SocketService**: для отправки команд в MT4
- **ScreenshotService**: для создания скриншотов

### Используется в
- **MainWindow**: для управления отслеживанием
- **CaptureTrackingViewer**: для отображения статуса захватов

## Особенности реализации

1. **Периодический мониторинг**: Использует таймер для регулярной проверки
2. **Потокобезопасность**: Использует блокировку для безопасной работы
3. **Асинхронность**: Все операции выполняются асинхронно
4. **Обработка ошибок**: Корректная обработка ошибок сравнения и торговли
5. **Логирование**: Подробное логирование всех операций

## Примеры использования

```csharp
// Запуск отслеживания
captureTrackingService.StartTracking();

// Остановка отслеживания
captureTrackingService.StopTracking();

// Проверка статуса
bool isTracking = captureTrackingService.IsTrackingEnabled;
```

## Обработка ошибок

```csharp
try
{
    await captureTrackingService.ExecuteTradeCommand(captureData);
}
catch (Exception ex)
{
    Logger.LogError($"Error executing trade command: {ex.Message}", ex);
    // Обновляем статус захвата как пропущенный
    captureTrackingService.UpdateCaptureStatus(captureData.ID, false, true);
}
```

## Логирование

Сервис ведет подробное логирование всех операций:

```
[INFO] Starting capture tracking service
[INFO] Tracking timer started, interval: 1000ms
[INFO] Checking pattern match for capture: 20241201123456789
[INFO] Pattern similarity: 87.5%
[INFO] Pattern matched! Executing trade command
[INFO] Trade command executed successfully for EURUSD
[INFO] Capture status updated: FIRED
[INFO] Checking pattern match for capture: 20241201123456790
[INFO] Pattern similarity: 45.2%
[INFO] Pattern not matched, skipping
```

## Конфигурация

Сервис использует следующие настройки по умолчанию:
- **Интервал отслеживания**: 1000 мс (1 секунда)
- **Порог схожести**: 85%
- **Таймаут сравнения**: 5 секунд
- **Максимальное количество одновременных проверок**: 10

## Производительность

1. **Эффективное сравнение**: Оптимизированный алгоритм сравнения изображений
2. **Кэширование**: Кэширование результатов сравнения
3. **Ограничение нагрузки**: Ограничение количества одновременных операций
4. **Асинхронная обработка**: Неблокирующая обработка операций

## Безопасность

1. **Валидация данных**: Проверка корректности данных захвата
2. **Ограничение доступа**: Проверка прав на выполнение торговых операций
3. **Логирование операций**: Подробное логирование всех торговых операций
4. **Обработка исключений**: Корректная обработка ошибок

## Интеграция с UI

```csharp
// В MainWindow
private void StartTrackingButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var trackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
        trackingService.StartTracking();
        StartTrackingButton.IsEnabled = false;
        StopTrackingButton.IsEnabled = true;
        StatusLabel.Content = "Tracking: ACTIVE";
    }
    catch (Exception ex)
    {
        Logger.LogError($"Error starting tracking: {ex.Message}", ex);
        MessageBox.Show($"Error starting tracking: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private void StopTrackingButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var trackingService = ServiceContainer.Instance.GetService<CaptureTrackingService>();
        trackingService.StopTracking();
        StartTrackingButton.IsEnabled = true;
        StopTrackingButton.IsEnabled = false;
        StatusLabel.Content = "Tracking: STOPPED";
    }
    catch (Exception ex)
    {
        Logger.LogError($"Error stopping tracking: {ex.Message}", ex);
        MessageBox.Show($"Error stopping tracking: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
``` 