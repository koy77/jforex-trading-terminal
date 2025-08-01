# ToolbarSettingsManager

## Описание
`ToolbarSettingsManager` - сервис для хранения и управления настройками TradingToolbar по handle окна. Singleton DI-сервис, который хранит настройки только в памяти (не сохраняется в базу данных).

## Зависимости
- `System.Collections.Generic` - для работы со словарем
- `ToolbarSettings` - модель настроек тулбара

## Основные поля

### Приватные поля
```csharp
private readonly Dictionary<long, ToolbarSettings> _settingsByHandle = new();
```

## Основные методы

### GetSettings
```csharp
public ToolbarSettings GetSettings(long handle)
```

**Назначение**: Получает настройки тулбара для указанного handle окна.

**Параметры**:
- `handle` - handle окна

**Возвращает**: Объект `ToolbarSettings` или `null`, если настройки не найдены.

**Логика работы**:
1. Пытается найти настройки в словаре по handle
2. Возвращает найденные настройки или `null`

### SetSettings
```csharp
public void SetSettings(long handle, ToolbarSettings settings)
```

**Назначение**: Устанавливает настройки тулбара для указанного handle окна.

**Параметры**:
- `handle` - handle окна
- `settings` - объект настроек

**Логика работы**:
1. Сохраняет настройки в словарь по handle
2. Перезаписывает существующие настройки, если они есть

### UpdateSettings
```csharp
public void UpdateSettings(long handle, double? risk = null, int? duration = null, BrokerType? broker = null)
```

**Назначение**: Обновляет отдельные параметры настроек тулбара.

**Параметры**:
- `handle` - handle окна
- `risk` - новый риск (опционально)
- `duration` - новая длительность (опционально)
- `broker` - новый брокер (опционально)

**Логика работы**:
1. Проверяет, существуют ли настройки для handle
2. Если нет - создает новые настройки с дефолтными значениями
3. Обновляет только переданные параметры
4. Сохраняет обновленные настройки в словарь

### GetOrCreateSettings
```csharp
public ToolbarSettings GetOrCreateSettings(long handle)
```

**Назначение**: Получает существующие настройки или создает новые с дефолтными значениями.

**Параметры**:
- `handle` - handle окна

**Возвращает**: Объект `ToolbarSettings` (существующий или новый).

**Логика работы**:
1. Проверяет, существуют ли настройки для handle
2. Если нет - создает новые настройки с дефолтными значениями
3. Возвращает найденные или созданные настройки

### RemoveSettings
```csharp
public void RemoveSettings(long handle)
```

**Назначение**: Удаляет настройки тулбара для указанного handle окна.

**Параметры**:
- `handle` - handle окна

**Логика работы**:
1. Удаляет настройки из словаря по handle
2. Если настройки не найдены, ничего не происходит

## Модель данных

### ToolbarSettings
```csharp
public class ToolbarSettings
{
    public long Handle { get; set; }
    public double Risk { get; set; } = 1.0;
    public int Duration { get; set; } = 2;
    public BrokerType Broker { get; set; } = BrokerType.Forex;
}
```

### BrokerType
```csharp
public enum BrokerType
{
    Forex,
    PocketOption,
    Binarium,
    Quotex
}
```

## Взаимодействие с другими компонентами

### Используется в
- **CanvasWindow**: для сохранения и загрузки настроек тулбара
- **ScreenCaptureOverlay**: для применения настроек при захвате
- **SimpleTradingOverlay**: для управления настройками по окнам
- **MainWindow**: для инициализации настроек

### Использует
- **ToolbarSettings**: модель настроек тулбара
- **BrokerType**: перечисление типов брокеров

## Особенности реализации

1. **Singleton DI**: Регистрируется как singleton в DI-контейнере
2. **Только в памяти**: Настройки не сохраняются в базу данных
3. **Автоматическое создание**: Создает настройки с дефолтными значениями при необходимости
4. **Дефолтные значения**: Risk=1.0, Duration=2, Broker=Forex
5. **Потокобезопасность**: Не обеспечивает потокобезопасность (требует синхронизации при многопоточном использовании)

## Примеры использования

```csharp
// Получение настроек
var settings = toolbarSettingsManager.GetSettings(windowHandle);
if (settings != null)
{
    Console.WriteLine($"Risk: {settings.Risk}, Duration: {settings.Duration}");
}

// Обновление настроек
toolbarSettingsManager.UpdateSettings(windowHandle, risk: 10, duration: 3);

// Создание или получение настроек
var settings = toolbarSettingsManager.GetOrCreateSettings(windowHandle);

// Удаление настроек
toolbarSettingsManager.RemoveSettings(windowHandle);
```

## Сценарии использования

### При открытии окна
```csharp
// При открытии CanvasWindow или ScreenCaptureOverlay
var settings = _toolbarSettingsManager.GetSettings(targetWindowHandle);
if (settings != null)
{
    // Применяем сохраненные настройки
    selectedRisk = settings.Risk;
    selectedDuration = settings.Duration;
    brokerState.CurrentBroker = settings.Broker;
}
else
{
    // Создаем настройки с дефолтными значениями
    _toolbarSettingsManager.UpdateSettings(targetWindowHandle, selectedRisk, selectedDuration, brokerState.CurrentBroker);
}
```

### При изменении настроек
```csharp
// При изменении риска в TradingToolbar
TradingToolbar.RiskChanged += (risk) => {
    selectedRisk = risk;
    if (targetWindowHandle != IntPtr.Zero)
        _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), risk: risk);
};
```

### При переключении брокера
```csharp
// При переключении брокера
TradingToolbar.BrokerChanged += (broker) => {
    brokerState.CurrentBroker = broker;
    if (targetWindowHandle != IntPtr.Zero)
        _toolbarSettingsManager.UpdateSettings(targetWindowHandle.ToInt64(), broker: broker);
};
```

## Преимущества и недостатки

### Преимущества
1. **Простота**: Простая и понятная реализация
2. **Производительность**: Быстрый доступ к настройкам в памяти
3. **Автоматическое создание**: Автоматически создает настройки при необходимости
4. **Дефолтные значения**: Предоставляет разумные дефолтные значения

### Недостатки
1. **Потеря данных**: Настройки теряются при перезапуске приложения
2. **Потокобезопасность**: Не обеспечивает потокобезопасность
3. **Ограниченная персистентность**: Нет возможности сохранить настройки между сессиями

## Альтернативные решения

### Сохранение в базу данных
```csharp
// Можно добавить методы для сохранения в DatabaseService
public void SaveSettingsToDatabase(long handle, ToolbarSettings settings)
{
    // Сохранение в JSON базу данных
}

public ToolbarSettings LoadSettingsFromDatabase(long handle)
{
    // Загрузка из JSON базы данных
}
```

### Потокобезопасная версия
```csharp
private readonly object _lock = new object();

public void UpdateSettings(long handle, double? risk = null, int? duration = null, BrokerType? broker = null)
{
    lock (_lock)
    {
        // Потокобезопасное обновление настроек
    }
}
``` 