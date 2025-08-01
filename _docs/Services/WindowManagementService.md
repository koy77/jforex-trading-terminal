# WindowManagementService

## Описание
`WindowManagementService` - сервис для управления окнами приложения. Обеспечивает создание, отображение, скрытие и закрытие различных окон приложения, а также управление их состоянием и позиционированием.

## Зависимости
- `System.Windows` - для работы с WPF окнами
- `System.Collections.Generic` - для управления коллекциями окон
- `System.Threading.Tasks` - для асинхронных операций

## Основные поля

### Приватные поля
```csharp
private readonly Dictionary<string, Window> _openWindows = new();
private readonly object _windowsLock = new object();
private Window _mainWindow;
```

## Основные методы

### ShowCanvasWindow
```csharp
public void ShowCanvasWindow(IntPtr targetWindowHandle)
```

**Назначение**: Отображает окно CanvasWindow для указанного handle окна.

**Параметры**:
- `targetWindowHandle` - handle целевого окна

**Логика работы**:
1. Проверяет, не открыто ли уже окно для данного handle
2. Создает новый экземпляр `CanvasWindow`
3. Передает handle целевого окна
4. Отображает окно
5. Добавляет окно в коллекцию открытых окон

### ShowScreenCaptureOverlay
```csharp
public void ShowScreenCaptureOverlay(IntPtr targetWindowHandle)
```

**Назначение**: Отображает окно ScreenCaptureOverlay для захвата области экрана.

**Параметры**:
- `targetWindowHandle` - handle целевого окна

**Логика работы**:
1. Проверяет, не открыто ли уже окно для данного handle
2. Создает новый экземпляр `ScreenCaptureOverlay`
3. Передает handle целевого окна
4. Отображает окно
5. Добавляет окно в коллекцию открытых окон

### ShowSimpleTradingOverlay
```csharp
public void ShowSimpleTradingOverlay(IntPtr targetWindowHandle)
```

**Назначение**: Отображает окно SimpleTradingOverlay для автоматического трейдинга.

**Параметры**:
- `targetWindowHandle` - handle целевого окна

**Логика работы**:
1. Проверяет, не открыто ли уже окно для данного handle
2. Создает новый экземпляр `SimpleTradingOverlay`
3. Передает handle целевого окна
4. Отображает окно
5. Добавляет окно в коллекцию открытых окон

### ShowCaptureTrackingViewer
```csharp
public void ShowCaptureTrackingViewer()
```

**Назначение**: Отображает окно CaptureTrackingViewer для просмотра истории захватов.

**Логика работы**:
1. Проверяет, не открыто ли уже окно
2. Создает новый экземпляр `CaptureTrackingViewer`
3. Отображает окно
4. Добавляет окно в коллекцию открытых окон

### CloseWindow
```csharp
public void CloseWindow(string windowKey)
```

**Назначение**: Закрывает окно по ключу.

**Параметры**:
- `windowKey` - ключ окна для закрытия

**Логика работы**:
1. Находит окно в коллекции по ключу
2. Закрывает окно
3. Удаляет окно из коллекции
4. Освобождает ресурсы

### CloseAllWindows
```csharp
public void CloseAllWindows()
```

**Назначение**: Закрывает все открытые окна.

**Логика работы**:
1. Проходит по всем открытым окнам
2. Закрывает каждое окно
3. Очищает коллекцию окон
4. Освобождает ресурсы

### HideWindow
```csharp
public void HideWindow(string windowKey)
```

**Назначение**: Скрывает окно по ключу.

**Параметры**:
- `windowKey` - ключ окна для скрытия

**Логика работы**:
1. Находит окно в коллекции по ключу
2. Скрывает окно (не закрывает)
3. Окно остается в коллекции

### ShowWindow
```csharp
public void ShowWindow(string windowKey)
```

**Назначение**: Показывает скрытое окно по ключу.

**Параметры**:
- `windowKey` - ключ окна для показа

**Логика работы**:
1. Находит окно в коллекции по ключу
2. Показывает окно
3. Активирует окно

### GetWindow
```csharp
public Window GetWindow(string windowKey)
```

**Назначение**: Получает окно по ключу.

**Параметры**:
- `windowKey` - ключ окна

**Возвращает**: Объект окна или `null`, если окно не найдено.

### IsWindowOpen
```csharp
public bool IsWindowOpen(string windowKey)
```

**Назначение**: Проверяет, открыто ли окно по ключу.

**Параметры**:
- `windowKey` - ключ окна

**Возвращает**: `true` если окно открыто.

## Ключи окон

### CanvasWindow
- **Ключ**: `"CanvasWindow_{handle}"`
- **Описание**: Окно для рисования трендов и паттернов

### ScreenCaptureOverlay
- **Ключ**: `"ScreenCaptureOverlay_{handle}"`
- **Описание**: Окно для захвата областей экрана

### SimpleTradingOverlay
- **Ключ**: `"SimpleTradingOverlay_{handle}"`
- **Описание**: Окно для автоматического трейдинга

### CaptureTrackingViewer
- **Ключ**: `"CaptureTrackingViewer"`
- **Описание**: Окно для просмотра истории захватов

## Взаимодействие с другими компонентами

### Используется в
- **MainWindow**: для управления окнами приложения
- **HotkeysService**: для открытия окон по горячим клавишам
- **ServiceContainer**: для регистрации как singleton сервиса

### Использует
- **CanvasWindow**: для создания окон рисования
- **ScreenCaptureOverlay**: для создания окон захвата
- **SimpleTradingOverlay**: для создания окон трейдинга
- **CaptureTrackingViewer**: для создания окон просмотра

## Особенности реализации

1. **Singleton DI**: Регистрируется как singleton в DI-контейнере
2. **Потокобезопасность**: Использует блокировку для безопасной работы с коллекцией окон
3. **Уникальные ключи**: Каждое окно имеет уникальный ключ
4. **Автоматическое управление**: Автоматически управляет жизненным циклом окон
5. **Обработка ошибок**: Корректная обработка ошибок создания и закрытия окон

## Примеры использования

```csharp
// Открытие CanvasWindow
windowManagementService.ShowCanvasWindow(windowHandle);

// Открытие ScreenCaptureOverlay
windowManagementService.ShowScreenCaptureOverlay(windowHandle);

// Открытие SimpleTradingOverlay
windowManagementService.ShowSimpleTradingOverlay(windowHandle);

// Открытие CaptureTrackingViewer
windowManagementService.ShowCaptureTrackingViewer();

// Проверка, открыто ли окно
bool isOpen = windowManagementService.IsWindowOpen("CanvasWindow_123456");

// Получение окна
Window window = windowManagementService.GetWindow("CanvasWindow_123456");

// Скрытие окна
windowManagementService.HideWindow("CanvasWindow_123456");

// Показ скрытого окна
windowManagementService.ShowWindow("CanvasWindow_123456");

// Закрытие окна
windowManagementService.CloseWindow("CanvasWindow_123456");

// Закрытие всех окон
windowManagementService.CloseAllWindows();
```

## Обработка ошибок

```csharp
try
{
    windowManagementService.ShowCanvasWindow(windowHandle);
}
catch (Exception ex)
{
    Logger.LogError($"Error showing CanvasWindow: {ex.Message}", ex);
    MessageBox.Show($"Error opening window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
```

## Интеграция с HotkeysService

```csharp
// В MainWindow при инициализации
private void InitializeHotkeys()
{
    var hotkeysService = ServiceContainer.Instance.GetService<HotkeysService>();
    var windowManagementService = ServiceContainer.Instance.GetService<WindowManagementService>();
    
    // Открытие CanvasWindow по горячей клавише
    hotkeysService.OnCKeyPressed += () => {
        var activeWindow = GetActiveWindowHandle();
        if (activeWindow != IntPtr.Zero)
        {
            windowManagementService.ShowCanvasWindow(activeWindow);
        }
    };
    
    // Открытие ScreenCaptureOverlay по горячей клавише
    hotkeysService.OnSKeyPressed += () => {
        var activeWindow = GetActiveWindowHandle();
        if (activeWindow != IntPtr.Zero)
        {
            windowManagementService.ShowScreenCaptureOverlay(activeWindow);
        }
    };
    
    // Открытие SimpleTradingOverlay по горячей клавише
    hotkeysService.OnTKeyPressed += () => {
        var activeWindow = GetActiveWindowHandle();
        if (activeWindow != IntPtr.Zero)
        {
            windowManagementService.ShowSimpleTradingOverlay(activeWindow);
        }
    };
    
    // Открытие CaptureTrackingViewer по горячей клавише
    hotkeysService.OnVKeyPressed += () => {
        windowManagementService.ShowCaptureTrackingViewer();
    };
}
```

## Управление жизненным циклом

### Создание окна
1. Проверка существования окна
2. Создание нового экземпляра
3. Инициализация параметров
4. Добавление в коллекцию
5. Отображение окна

### Закрытие окна
1. Поиск окна в коллекции
2. Закрытие окна
3. Удаление из коллекции
4. Освобождение ресурсов

### Скрытие/показ окна
1. Поиск окна в коллекции
2. Изменение видимости
3. Сохранение в коллекции

## Производительность

1. **Эффективное управление**: Быстрый поиск окон по ключу
2. **Минимальное использование памяти**: Освобождение ресурсов при закрытии
3. **Асинхронные операции**: Неблокирующие операции создания окон
4. **Кэширование**: Кэширование часто используемых окон

## Безопасность

1. **Потокобезопасность**: Безопасная работа с коллекцией окон
2. **Валидация параметров**: Проверка корректности handle окон
3. **Обработка исключений**: Корректная обработка ошибок
4. **Освобождение ресурсов**: Гарантированное освобождение ресурсов 