# ToastNotifyService

## Описание
`ToastNotifyService` - сервис для отображения уведомлений в виде toast-сообщений. Обеспечивает показ информационных, предупреждающих и критических уведомлений пользователю через всплывающие окна.

## Зависимости
- `System.Windows` - для работы с WPF окнами
- `System.Threading.Tasks` - для асинхронных операций
- `System.Collections.Generic` - для управления очередью уведомлений

## Основные поля

### Приватные поля
```csharp
private readonly Queue<ToastNotification> _notificationQueue = new();
private readonly object _queueLock = new object();
private bool _isShowingNotification = false;
private Window _currentToastWindow;
```

### Константы
```csharp
private const int TOAST_DISPLAY_TIME_MS = 3000; // 3 секунды
private const int TOAST_FADE_TIME_MS = 500; // 0.5 секунды
private const double TOAST_OPACITY = 0.9;
```

## Основные методы

### ShowInfo
```csharp
public void ShowInfo(string message, string title = "Information")
```

**Назначение**: Отображает информационное уведомление.

**Параметры**:
- `message` - текст сообщения
- `title` - заголовок уведомления (по умолчанию "Information")

**Логика работы**:
1. Создает объект `ToastNotification` с типом Info
2. Добавляет уведомление в очередь
3. Запускает обработку очереди

### ShowWarning
```csharp
public void ShowWarning(string message, string title = "Warning")
```

**Назначение**: Отображает предупреждающее уведомление.

**Параметры**:
- `message` - текст сообщения
- `title` - заголовок уведомления (по умолчанию "Warning")

**Логика работы**:
1. Создает объект `ToastNotification` с типом Warning
2. Добавляет уведомление в очередь
3. Запускает обработку очереди

### ShowError
```csharp
public void ShowError(string message, string title = "Error")
```

**Назначение**: Отображает уведомление об ошибке.

**Параметры**:
- `message` - текст сообщения
- `title` - заголовок уведомления (по умолчанию "Error")

**Логика работы**:
1. Создает объект `ToastNotification` с типом Error
2. Добавляет уведомление в очередь
3. Запускает обработку очереди

### ShowSuccess
```csharp
public void ShowSuccess(string message, string title = "Success")
```

**Назначение**: Отображает уведомление об успешной операции.

**Параметры**:
- `message` - текст сообщения
- `title` - заголовок уведомления (по умолчанию "Success")

**Логика работы**:
1. Создает объект `ToastNotification` с типом Success
2. Добавляет уведомление в очередь
3. Запускает обработку очереди

### ProcessNotificationQueue
```csharp
private async void ProcessNotificationQueue()
```

**Назначение**: Обрабатывает очередь уведомлений.

**Логика работы**:
1. Проверяет, не отображается ли уже уведомление
2. Извлекает следующее уведомление из очереди
3. Отображает уведомление
4. Ждет указанное время
5. Скрывает уведомление
6. Повторяет процесс для следующего уведомления

### ShowToastNotification
```csharp
private async Task ShowToastNotification(ToastNotification notification)
```

**Назначение**: Отображает конкретное toast-уведомление.

**Параметры**:
- `notification` - объект уведомления для отображения

**Логика работы**:
1. Создает окно toast-уведомления
2. Настраивает внешний вид в зависимости от типа
3. Позиционирует окно на экране
4. Отображает окно с анимацией появления
5. Ждет указанное время
6. Скрывает окно с анимацией исчезновения

## Модели данных

### ToastNotification
```csharp
public class ToastNotification
{
    public string Title { get; set; }
    public string Message { get; set; }
    public ToastType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

### ToastType
```csharp
public enum ToastType
{
    Success,
    Error,
    Info,
    BreakoutUp,
    BreakoutDown
}
```

## Типы уведомлений

### Success (Успех)
- **Цвет**: Зеленый (RGB: 60, 180, 75)
- **Использование**: Успешные операции, подтверждения

### Error (Ошибка)
- **Цвет**: Черный (RGB: 0, 0, 0)
- **Использование**: Критические ошибки, сбои

### Info (Информация)
- **Цвет**: Синий (RGB: 0, 120, 215)
- **Использование**: Информационные сообщения, создание сделок

### BreakoutUp (Прорыв вверх)
- **Цвет**: Зеленый (RGB: 60, 180, 75)
- **Использование**: Сигналы прорыва вверх

### BreakoutDown (Прорыв вниз)
- **Цвет**: Красный (RGB: 220, 50, 47)
- **Использование**: Сигналы прорыва вниз

## Взаимодействие с другими компонентами

### Используется в
- **MainWindow**: для отображения уведомлений о состоянии приложения
- **CaptureService**: для уведомлений о захватах
- **DatabaseService**: для уведомлений о сохранении данных
- **Mt4SocketService**: для уведомлений о подключении
- **BinaryOptionsSocketService**: для уведомлений о торговых операциях

### Использует
- **Logger**: для логирования уведомлений

## Особенности реализации

1. **Очередь уведомлений**: Уведомления отображаются последовательно
2. **Анимации**: Плавные анимации появления и исчезновения
3. **Автоматическое позиционирование**: Автоматическое размещение на экране
4. **Таймауты**: Автоматическое скрытие через указанное время
5. **Потокобезопасность**: Безопасная работа с очередью уведомлений

## Примеры использования

```csharp
// Информационное уведомление
toastNotifyService.ShowToast("Сделка BUY от 2345.67 с риском 1 отправлена", ToastType.Info, 4000);

// Уведомление об ошибке
toastNotifyService.ShowToast("Failed to save capture data", ToastType.Error, 3000);

// Уведомление об успехе
toastNotifyService.ShowToast("Trade executed successfully", ToastType.Success, 3000);

// Уведомление о прорыве вверх
toastNotifyService.ShowToast("BUY #123 EURUSD 1.0", ToastType.BreakoutUp, 3000);

// Уведомление о прорыве вниз
toastNotifyService.ShowToast("SELL #124 GBPJPY 1.0", ToastType.BreakoutDown, 3000);
```

## Обработка ошибок

```csharp
try
{
    // Выполнение операции
    var result = await someOperation();
    toastNotifyService.ShowSuccess("Operation completed successfully");
}
catch (Exception ex)
{
    Logger.LogError($"Operation failed: {ex.Message}", ex);
    toastNotifyService.ShowError($"Operation failed: {ex.Message}");
}
```

## Интеграция с другими сервисами

```csharp
// В CaptureService
public void CaptureAreaWithSymbolAndRisk(...)
{
    try
    {
        // Логика захвата
        toastNotifyService.ShowSuccess($"Capture saved: {symbol}");
    }
    catch (Exception ex)
    {
        toastNotifyService.ShowError($"Capture failed: {ex.Message}");
    }
}

// В Mt4SocketService
public async Task<bool> ConnectAsync(...)
{
    try
    {
        // Логика подключения
        toastNotifyService.ShowSuccess("Connected to MT4");
        return true;
    }
    catch (Exception ex)
    {
        toastNotifyService.ShowError($"MT4 connection failed: {ex.Message}");
        return false;
    }
}

// В BinaryOptionsSocketService
public async Task<bool> SendBuyCommand(...)
{
    try
    {
        // Логика отправки команды
        toastNotifyService.ShowSuccess($"Buy command sent for {symbolName}");
        return true;
    }
    catch (Exception ex)
    {
        toastNotifyService.ShowError($"Buy command failed: {ex.Message}");
        return false;
    }
}
```

## Конфигурация

Сервис использует следующие настройки по умолчанию:
- **Время отображения**: 3000 мс (3 секунды)
- **Время анимации**: 500 мс (0.5 секунды)
- **Прозрачность**: 0.9 (90%)
- **Максимальная ширина**: 300 пикселей
- **Максимальная высота**: 150 пикселей

## Позиционирование

### Автоматическое позиционирование
1. **Правая сторона**: Уведомления появляются справа
2. **Верхняя часть**: Начинают появляться сверху
3. **Смещение**: Каждое следующее уведомление смещается вниз
4. **Максимум**: Максимум 5 уведомлений одновременно

### Настройка позиции
```csharp
// Можно настроить позицию для конкретного уведомления
private void ShowNotificationAtPosition(string message, double x, double y)
{
    var notification = new ToastNotification
    {
        Title = "Custom Position",
        Message = message,
        Type = ToastType.Info
    };
    
    // Настройка позиции
    ShowToastNotificationAtPosition(notification, x, y);
}
```

## Производительность

1. **Эффективная очередь**: Быстрая обработка очереди уведомлений
2. **Минимальное использование ресурсов**: Освобождение ресурсов после отображения
3. **Асинхронная обработка**: Неблокирующая обработка уведомлений
4. **Ограничение количества**: Ограничение количества одновременных уведомлений

## Безопасность

1. **Потокобезопасность**: Безопасная работа с очередью уведомлений
2. **Валидация данных**: Проверка корректности текста уведомлений
3. **Обработка исключений**: Корректная обработка ошибок отображения
4. **Освобождение ресурсов**: Гарантированное освобождение ресурсов окон 