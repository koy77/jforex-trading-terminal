# ScreenshotService

## Описание
`ScreenshotService` - сервис для создания скриншотов областей экрана. Обеспечивает захват изображений с экрана, сохранение в файлы и создание отладочной информации о захваченных областях.

## Зависимости
- `System.Drawing` - для работы с изображениями
- `System.Windows.Forms` - для получения информации о экранах
- `System.IO` - для работы с файлами

## Основные поля

### Приватные поля
```csharp
private readonly string _screenshotsDir;
private readonly string _croppedDir;
```

### Константы
```csharp
private const string SCREENSHOTS_DIR = "screenshots";
private const string CROPPED_DIR = "cropped";
```

## Основные методы

### CaptureScreenAreaDebug
```csharp
public CaptureDebugInfo CaptureScreenAreaDebug(int x, int y, int width, int height, int monitorIndex, string captureId)
```

**Назначение**: Создает скриншот области экрана с отладочной информацией.

**Параметры**:
- `x, y` - координаты области захвата
- `width, height` - размеры области захвата
- `monitorIndex` - индекс монитора
- `captureId` - уникальный ID захвата

**Возвращает**: `CaptureDebugInfo` с информацией о захвате.

**Логика работы**:
1. Получает информацию о мониторе по индексу
2. Вычисляет виртуальные координаты
3. Создает скриншот области
4. Сохраняет изображение в файл
5. Создает и возвращает отладочную информацию

### CaptureScreenArea
```csharp
public string CaptureScreenArea(int x, int y, int width, int height, int monitorIndex, string captureId)
```

**Назначение**: Создает скриншот области экрана и сохраняет в файл.

**Параметры**:
- `x, y` - координаты области захвата
- `width, height` - размеры области захвата
- `monitorIndex` - индекс монитора
- `captureId` - уникальный ID захвата

**Возвращает**: Путь к сохраненному файлу.

**Логика работы**:
1. Получает информацию о мониторе
2. Вычисляет виртуальные координаты
3. Создает скриншот области
4. Сохраняет изображение в файл
5. Возвращает путь к файлу

### CaptureFullScreen
```csharp
public string CaptureFullScreen(string captureId)
```

**Назначение**: Создает скриншот всего экрана.

**Параметры**:
- `captureId` - уникальный ID захвата

**Возвращает**: Путь к сохраненному файлу.

**Логика работы**:
1. Получает размеры виртуального экрана
2. Создает скриншот всего экрана
3. Сохраняет изображение в файл
4. Возвращает путь к файлу

### CaptureWindow
```csharp
public string CaptureWindow(IntPtr windowHandle, string captureId)
```

**Назначение**: Создает скриншот окна по handle.

**Параметры**:
- `windowHandle` - handle окна
- `captureId` - уникальный ID захвата

**Возвращает**: Путь к сохраненному файлу.

**Логика работы**:
1. Получает размеры и позицию окна
2. Создает скриншот области окна
3. Сохраняет изображение в файл
4. Возвращает путь к файлу

## Вспомогательные методы

### GetMonitorInfo
```csharp
private Screen GetMonitorInfo(int monitorIndex)
```

**Назначение**: Получает информацию о мониторе по индексу.

**Параметры**:
- `monitorIndex` - индекс монитора

**Возвращает**: Объект `Screen` с информацией о мониторе.

### CalculateVirtualCoordinates
```csharp
private (int virtualX, int virtualY) CalculateVirtualCoordinates(int x, int y, int monitorIndex)
```

**Назначение**: Вычисляет виртуальные координаты для захвата.

**Параметры**:
- `x, y` - локальные координаты
- `monitorIndex` - индекс монитора

**Возвращает**: Кортеж с виртуальными координатами.

### SaveBitmapToFile
```csharp
private string SaveBitmapToFile(Bitmap bitmap, string captureId, string suffix = "")
```

**Назначение**: Сохраняет изображение в файл.

**Параметры**:
- `bitmap` - изображение для сохранения
- `captureId` - уникальный ID захвата
- `suffix` - суффикс имени файла (опционально)

**Возвращает**: Путь к сохраненному файлу.

### CreateScreenshotDirectories
```csharp
private void CreateScreenshotDirectories()
```

**Назначение**: Создает директории для сохранения скриншотов.

**Логика работы**:
1. Создает директорию `screenshots` если не существует
2. Создает директорию `cropped` если не существует

## Модели данных

### CaptureDebugInfo
```csharp
public class CaptureDebugInfo
{
    public int SelectionX { get; set; }
    public int SelectionY { get; set; }
    public int SelectionWidth { get; set; }
    public int SelectionHeight { get; set; }
    public int VirtualLeft { get; set; }
    public int VirtualTop { get; set; }
    public int VirtualRight { get; set; }
    public int VirtualBottom { get; set; }
    public int CropX { get; set; }
    public int CropY { get; set; }
    public int CropWidth { get; set; }
    public int CropHeight { get; set; }
    public string ScreenshotPath { get; set; }
    public string Error { get; set; }
    public int MonitorIndex { get; set; }
}
```

## WinAPI импорты

```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetDC(IntPtr hWnd);

[DllImport("user32.dll")]
private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

[DllImport("gdi32.dll")]
private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

[DllImport("gdi32.dll")]
private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

[DllImport("gdi32.dll")]
private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

[DllImport("gdi32.dll")]
private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, uint dwRop);

[DllImport("gdi32.dll")]
private static extern bool DeleteObject(IntPtr hObject);

[DllImport("user32.dll")]
private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[DllImport("user32.dll")]
private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
```

## Структуры данных

### RECT
```csharp
[StructLayout(LayoutKind.Sequential)]
private struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
```

## Взаимодействие с другими компонентами

### Используется в
- **CaptureService**: для создания скриншотов при захвате областей
- **CanvasWindow**: для создания фоновых изображений
- **CaptureTrackingViewer**: для отображения скриншотов

### Использует
- **WinAPI**: для захвата изображений с экрана
- **System.Drawing**: для работы с изображениями

## Особенности реализации

1. **Многомониторная поддержка**: Корректно работает с несколькими мониторами
2. **Виртуальные координаты**: Преобразует локальные координаты в виртуальные
3. **Отладочная информация**: Предоставляет подробную информацию о захвате
4. **Автоматическое создание директорий**: Создает необходимые директории
5. **Уникальные имена файлов**: Использует captureId для уникальных имен файлов

## Примеры использования

```csharp
// Захват области экрана с отладочной информацией
var debugInfo = screenshotService.CaptureScreenAreaDebug(100, 200, 300, 400, 0, "capture123");
Console.WriteLine($"Screenshot saved: {debugInfo.ScreenshotPath}");

// Захват области экрана
var path = screenshotService.CaptureScreenArea(100, 200, 300, 400, 0, "capture123");

// Захват всего экрана
var fullScreenPath = screenshotService.CaptureFullScreen("fullscreen123");

// Захват окна
var windowPath = screenshotService.CaptureWindow(windowHandle, "window123");
```

## Структура файлов

```
screenshots/
├── capture123.png
├── capture124.png
└── fullscreen123.png

cropped/
├── capture123_cropped.png
└── capture124_cropped.png
```

## Обработка ошибок

```csharp
try
{
    var debugInfo = screenshotService.CaptureScreenAreaDebug(x, y, width, height, monitorIndex, captureId);
    if (!string.IsNullOrEmpty(debugInfo.Error))
    {
        Logger.LogError($"Screenshot error: {debugInfo.Error}");
    }
}
catch (Exception ex)
{
    Logger.LogError($"Screenshot failed: {ex.Message}", ex);
}
```

## Производительность

1. **Эффективный захват**: Использует WinAPI для быстрого захвата
2. **Оптимизированное сохранение**: Эффективное сохранение в PNG формат
3. **Минимальное использование памяти**: Освобождает ресурсы после использования
4. **Асинхронность**: Может быть легко адаптирован для асинхронной работы 