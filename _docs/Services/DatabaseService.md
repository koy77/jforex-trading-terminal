# DatabaseService

## Описание
`DatabaseService` - сервис для работы с локальной JSON-базой данных. Отвечает за сохранение, обновление и получение данных о захватах (`CaptureData`) и символах (`SymbolData`).

## Зависимости
- `System.Text.Json` - для сериализации/десериализации JSON
- `CaptureData` - модель данных захвата
- `SymbolData` - модель данных символа

## Основные поля

### Приватные поля
```csharp
private readonly string _dbPath;
private readonly JsonSerializerOptions _jsonOptions;
```

### Константы
```csharp
private const string DB_FILENAME = "captures.json";
```

## Основные методы

### SaveCapture
```csharp
public void SaveCapture(CaptureData capture)
```

**Назначение**: Сохраняет новый захват в базу данных.

**Параметры**:
- `capture` - объект `CaptureData` для сохранения

**Логика работы**:
1. Загружает текущую базу данных
2. Добавляет новый захват в список
3. Сохраняет обновленную базу в файл

### UpdateCapture
```csharp
public void UpdateCapture(CaptureData updatedCapture)
```

**Назначение**: Обновляет существующий захват в базе данных.

**Параметры**:
- `updatedCapture` - обновленный объект `CaptureData`

**Логика работы**:
1. Загружает текущую базу данных
2. Находит захват по ID
3. Обновляет данные захвата
4. Сохраняет обновленную базу в файл

### GetAllCaptures
```csharp
public List<CaptureData> GetAllCaptures()
```

**Назначение**: Возвращает все захваты из базы данных.

**Возвращает**: Список всех `CaptureData`.

### GetAllFiredCaptures
```csharp
public List<CaptureData> GetAllFiredCaptures()
```

**Назначение**: Возвращает все сработавшие захваты.

**Возвращает**: Список `CaptureData` с `IsFired = true`.

### GetAllSkippedCaptures
```csharp
public List<CaptureData> GetAllSkippedCaptures()
```

**Назначение**: Возвращает все пропущенные захваты.

**Возвращает**: Список `CaptureData` с `IsSkipped = true`.

### GetTrackingCaptures
```csharp
public List<CaptureData> GetTrackingCaptures()
```

**Назначение**: Возвращает захваты для трекинга (не сработавшие и не пропущенные).

**Возвращает**: Список `CaptureData` с `IsFired = false` и `IsSkipped = false`.

### UpdateCaptureIsFired
```csharp
public void UpdateCaptureIsFired(string captureId, bool isFired)
```

**Назначение**: Обновляет статус срабатывания захвата.

**Параметры**:
- `captureId` - ID захвата
- `isFired` - новый статус срабатывания

### UpdateCaptureIsSkipped
```csharp
public void UpdateCaptureIsSkipped(string captureId, bool isSkipped)
```

**Назначение**: Обновляет статус пропуска захвата.

**Параметры**:
- `captureId` - ID захвата
- `isSkipped` - новый статус пропуска

### SaveSymbol
```csharp
public void SaveSymbol(SymbolData symbol)
```

**Назначение**: Сохраняет новый символ в базу данных.

**Параметры**:
- `symbol` - объект `SymbolData` для сохранения

### UpdateSymbolRisk
```csharp
public void UpdateSymbolRisk(string symbol, double riskPercent)
```

**Назначение**: Обновляет риск для символа.

**Параметры**:
- `symbol` - название символа
- `riskPercent` - новый процент риска

### ClearAllCaptures
```csharp
public void ClearAllCaptures()
```

**Назначение**: Очищает все захваты из базы данных.

### ClearAllData
```csharp
public void ClearAllData()
```

**Назначение**: Очищает все данные (захваты и символы) из базы данных.

## Вспомогательные методы

### LoadDatabase
```csharp
private CaptureDbRoot LoadDatabase()
```

**Назначение**: Загружает базу данных из JSON файла.

**Возвращает**: Объект `CaptureDbRoot` с данными.

**Логика работы**:
1. Проверяет существование файла
2. Если файл не существует, создает новую базу
3. Десериализует JSON в объект `CaptureDbRoot`

### SaveDatabase
```csharp
private void SaveDatabase(CaptureDbRoot dbRoot)
```

**Назначение**: Сохраняет базу данных в JSON файл.

**Параметры**:
- `dbRoot` - объект базы данных для сохранения

**Логика работы**:
1. Сериализует объект в JSON
2. Записывает JSON в файл

## Структура данных

### CaptureDbRoot
```csharp
public class CaptureDbRoot
{
    public List<CaptureData> Captures { get; set; } = new List<CaptureData>();
    public List<SymbolData> Symbols { get; set; } = new List<SymbolData>();
}
```

### CaptureData
```csharp
public class CaptureData
{
    public string ID { get; set; } = DateTime.Now.ToString("yyyyMMddHHmmssfff");
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Handle { get; set; }
    public int Monitor { get; set; }
    public string Timestamp { get; set; }
    public string ScreenshotPath { get; set; }
    public string Symbol { get; set; }
    public double Risk { get; set; }
    public bool IsFired { get; set; } = false;
    public bool IsSkipped { get; set; } = false;
    public string Source { get; set; } = "window";
    public string Broker { get; set; }
    public int Duration { get; set; }
    public string Model { get; set; }
    public string Mt4Order { get; set; }
    public string Period { get; set; }
    public string Direction { get; set; }
    public string Meta { get; set; } = "";
}
```

### SymbolData
```csharp
public class SymbolData
{
    public string Symbol { get; set; }
    public double RiskPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastUpdated { get; set; }
}
```

## Настройки JSON

```csharp
private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

## Взаимодействие с другими компонентами

### Используется в
- **CaptureService**: для сохранения и обновления захватов
- **CaptureTrackingViewer**: для отображения истории захватов
- **MainWindow**: для управления данными
- **CanvasWindow**: для сохранения захватов из штрихов

### Использует
- **CaptureData**: модель данных захвата
- **SymbolData**: модель данных символа
- **CaptureDbRoot**: корневая модель базы данных

## Особенности реализации

1. **JSON сериализация**: Использует System.Text.Json для работы с данными
2. **Автоматическое создание ID**: Генерирует уникальные ID для захватов
3. **Отложенная запись**: Записывает данные в файл только при необходимости
4. **Обработка ошибок**: Корректно обрабатывает отсутствие файла базы данных
5. **Потокобезопасность**: Не обеспечивает потокобезопасность (требует синхронизации при многопоточном использовании)

## Примеры использования

```csharp
// Сохранение нового захвата
var capture = new CaptureData { Symbol = "EURUSD", Risk = 10 };
databaseService.SaveCapture(capture);

// Получение всех захватов
var allCaptures = databaseService.GetAllCaptures();

// Обновление статуса захвата
databaseService.UpdateCaptureIsFired("20241201123456789", true);

// Сохранение символа
var symbol = new SymbolData { Symbol = "GBPUSD", RiskPercent = 5 };
databaseService.SaveSymbol(symbol);

// Очистка всех данных
databaseService.ClearAllData();
```

## Файловая структура

База данных хранится в файле `captures.json` в корневой папке приложения:

```json
{
  "captures": [
    {
      "id": "20241201123456789",
      "x": 100,
      "y": 200,
      "width": 300,
      "height": 400,
      "handle": 123456,
      "monitor": 0,
      "timestamp": "2024-12-01T12:34:56.789Z",
      "screenshotPath": "screenshots/20241201123456789.png",
      "symbol": "EURUSD",
      "risk": 10,
      "isFired": false,
      "isSkipped": false,
      "source": "window",
      "broker": "Pocket Option",
      "duration": 2,
      "model": "OHLC",
      "period": "M5",
      "direction": "Up"
    }
  ],
  "symbols": [
    {
      "symbol": "EURUSD",
      "riskPercent": 10,
      "isActive": true,
      "createdAt": "2024-12-01T12:00:00Z",
      "lastUpdated": "2024-12-01T12:30:00Z"
    }
  ]
}
``` 