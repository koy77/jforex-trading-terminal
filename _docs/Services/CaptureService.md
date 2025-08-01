# CaptureService

## Описание
`CaptureService` - основной сервис для захвата областей экрана, управления глобальными хуками мыши и создания `CaptureData`. Отвечает за координацию между различными компонентами системы захвата.

## Зависимости
- `DatabaseService` - для сохранения и обновления данных захвата
- `ScreenshotService` - для создания скриншотов областей экрана
- `BrokerState` - для получения информации о текущем брокере
- `DurationState` - для получения текущей длительности
- `MainHelper` - для работы с заголовками окон

## Основные поля

### Приватные поля
```csharp
private readonly DatabaseService _databaseService;
private readonly ScreenshotService _screenshotService;
private IntPtr mouseHook = IntPtr.Zero;
private HwndSource source;
private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
private LowLevelMouseProc mouseProc;
```

### Константы
```csharp
private const int WH_MOUSE_LL = 14;
private const int WM_LBUTTONDOWN = 0x0201;
private const int WM_LBUTTONUP = 0x0202;
private const int WM_MOUSEMOVE = 0x0200;
```

### События
```csharp
public event EventHandler<CaptureEventArgs> CaptureCompleted;
public event EventHandler<MouseHookEventArgs> MouseHookEvent;
```

## Основные методы

### CaptureAreaWithSymbolAndRisk
```csharp
public void CaptureAreaWithSymbolAndRisk(double x, double y, double width, double height, Window overlayWindow, string symbol, IntPtr windowHandle, double risk, int duration = 0)
```

**Назначение**: Основной метод захвата области экрана с символом и риском.

**Параметры**:
- `x, y` - координаты области захвата
- `width, height` - размеры области захвата
- `overlayWindow` - окно оверлея
- `symbol` - символ торговли
- `windowHandle` - handle окна
- `risk` - риск торговли
- `duration` - длительность (по умолчанию 0)

**Логика работы**:
1. Определяет индекс монитора по координатам
2. Если `duration == 0`, получает значение из `DurationState`
3. Если `DurationState.CurrentDuration == 0`, использует дефолтное значение 2
4. Определяет модель (OHLC/MACD) по координатам Y
5. Создает `CaptureData` с полученными параметрами
6. Сохраняет данные через `DatabaseService`
7. Создает скриншот через `ScreenshotService`
8. Обновляет риск символа
9. Вызывает событие `CaptureCompleted`

### GetVirtualScreenBounds
```csharp
public System.Drawing.Rectangle GetVirtualScreenBounds()
```

**Назначение**: Вычисляет виртуальные границы всех мониторов.

**Возвращает**: `Rectangle` с границами виртуального экрана.

**Логика работы**:
1. Проходит по всем экранам
2. Находит минимальные и максимальные координаты
3. Возвращает прямоугольник, охватывающий все экраны

### SetupMouseHook
```csharp
public void SetupMouseHook(Window window)
```

**Назначение**: Устанавливает глобальный хук мыши.

**Параметры**:
- `window` - окно для получения handle

**Логика работы**:
1. Получает handle окна
2. Создает делегат для callback
3. Устанавливает глобальный хук через WinAPI

### RemoveMouseHook
```csharp
public void RemoveMouseHook()
```

**Назначение**: Удаляет глобальный хук мыши.

**Логика работы**:
1. Проверяет, установлен ли хук
2. Удаляет хук через WinAPI
3. Сбрасывает handle хука

### MouseHookCallback
```csharp
private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
```

**Назначение**: Callback для глобального хука мыши.

**Параметры**:
- `nCode` - код действия
- `wParam` - параметр сообщения
- `lParam` - дополнительный параметр

**Логика работы**:
1. Обрабатывает события мыши (нажатие, отпускание, движение)
2. Создает `MouseHookEventArgs`
3. Вызывает событие `MouseHookEvent`
4. Передает управление следующему хуку

## Вспомогательные методы

### GetAllCaptures
```csharp
public List<CaptureData> GetAllCaptures()
```
Возвращает все сохраненные захваты.

### ClearAllCaptures
```csharp
public void ClearAllCaptures()
```
Очищает все сохраненные захваты.

### GetLastCapture
```csharp
public CaptureData GetLastCapture()
```
Возвращает последний захват.

### GetCapturesByDateRange
```csharp
public List<CaptureData> GetCapturesByDateRange(DateTime startDate, DateTime endDate)
```
Возвращает захваты за определенный период.

## WinAPI импорты

```csharp
[DllImport("user32.dll")]
private static extern IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

[DllImport("user32.dll")]
private static extern bool UnhookWindowsHookEx(IntPtr hhk);

[DllImport("user32.dll")]
private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

[DllImport("kernel32.dll")]
private static extern IntPtr GetModuleHandle(string lpModuleName);

[DllImport("user32.dll")]
private static extern IntPtr WindowFromPoint(System.Drawing.Point p);
```

## События

### CaptureEventArgs
```csharp
public class CaptureEventArgs : EventArgs
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string ScreenshotPath { get; set; }
    public CaptureDebugInfo DebugInfo { get; set; }
}
```

### MouseHookEventArgs
```csharp
public class MouseHookEventArgs : EventArgs
{
    public int Message { get; set; }
    public MouseEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### MouseEventType
```csharp
public enum MouseEventType
{
    LeftButtonDown,
    LeftButtonUp,
    MouseMove
}
```

## Взаимодействие с другими компонентами

### Использует
- **DatabaseService**: для сохранения и обновления `CaptureData`
- **ScreenshotService**: для создания скриншотов
- **BrokerState**: для получения информации о брокере
- **DurationState**: для получения длительности
- **MainHelper**: для работы с заголовками окон

### Используется в
- **ScreenCaptureOverlay**: для захвата областей экрана
- **CanvasWindow**: для создания захватов из штрихов
- **MainWindow**: для координации захватов

## Особенности реализации

1. **Глобальный хук мыши**: Использует WinAPI для перехвата событий мыши на уровне системы
2. **Виртуальные координаты**: Поддерживает работу с несколькими мониторами
3. **Автоматическое определение модели**: Определяет OHLC/MACD по координатам Y
4. **Дефолтные значения**: Автоматически использует дефолтные значения для duration
5. **Логирование**: Подробное логирование всех операций

## Примеры использования

```csharp
// Захват области с символом и риском
captureService.CaptureAreaWithSymbolAndRisk(100, 200, 300, 400, overlayWindow, "EURUSD", windowHandle, 10, 2);

// Получение виртуальных границ
var bounds = captureService.GetVirtualScreenBounds();

// Установка глобального хука
captureService.SetupMouseHook(window);

// Подписка на события
captureService.CaptureCompleted += OnCaptureCompleted;
captureService.MouseHookEvent += OnMouseHookEvent;
``` 