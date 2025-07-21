# ScreenCaptureApp - Architectural Documentation

## Overview

ScreenCaptureApp is a WPF-based desktop application designed for automated screen capture and trading platform integration. The application provides real-time screen capture capabilities, trading symbol management, and communication with MetaTrader 4 (MT4) platform through socket connections.

# rules from the user

не нужно добавлять код для миграции данных, считаем что базу в любой момент можно удалить и пересоздать.
держать этот файл в актуальном состоянии после изменений в коде


## Table of Contents

1. [System Architecture](#system-architecture)
2. [Main Window (MainWindow)](#main-window-mainwindow)
3. [Helper Classes](#helper-classes)
4. [Data Models](#data-models)
5. [Service Layer](#service-layer)
6. [UI Components](#ui-components)
7. [Event Flow](#event-flow)
8. [Development Guidelines](#development-guidelines)

---

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
├─────────────────────────────────────────────────────────────┤
│  MainWindow  │  CanvasWindow  │  Overlay                   │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Helper Layer                            │
├─────────────────────────────────────────────────────────────┤
│  MainHelper  │  ServiceInitializer  │  Mt4Helper           │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Service Layer                           │
├─────────────────────────────────────────────────────────────┤
│  HotkeysService  │  WindowManagementService  │  Logger     │
│  ScreenshotService  │  CaptureService  │  DatabaseService  │
│  CaptureTrackingService  │  Mt4SocketService              │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Data Layer                              │
├─────────────────────────────────────────────────────────────┤
│  CaptureData  │  SymbolData  │  CaptureDebugInfo          │
└─────────────────────────────────────────────────────────────┘
```

### Design Patterns

- **Service Layer Pattern**: All business logic is encapsulated in service classes
- **Event-Driven Architecture**: Services communicate through events
- **Dependency Injection**: Services are injected into the main window
- **Factory Pattern**: ServiceInitializer creates and configures services
- **Observer Pattern**: Event handlers for hotkeys and service events
- **Queue Pattern**: Trading stroke processing uses a queue system for sequential processing

---

## Main Window (MainWindow)

### Purpose
The main window serves as the primary user interface and orchestrates all application functionality. It acts as the central coordinator for all services and user interactions.

### Key Responsibilities

1. **Service Management**: Initializes and manages all service instances
2. **Event Handling**: Processes user interactions and service events
3. **UI Coordination**: Manages window positioning and UI state
4. **Hotkey Management**: Handles global keyboard shortcuts
5. **Capture Orchestration**: Coordinates screen capture workflow
6. **MT4 Connection Management**: Monitors and manages MT4 socket connection status

### Core Components

#### Service Instances
```csharp
private HotkeysService hotkeysService;
private WindowManagementService windowManagementService;
private Mt4SocketService mt4SocketService;
private DatabaseService databaseService;
private CaptureTrackingService captureTrackingService;
```

#### State Management
```csharp
private bool isCapturing = false;
private IntPtr targetWindow = IntPtr.Zero;
private CanvasWindow currentCanvasWindow = null;
private string activeSymbol = null;
private CancellationTokenSource _autoTrackingCts; // Для автотрекинга
private Task _autoTrackingTask; // Для автотрекинга
```

#### Timers
```csharp
private DispatcherTimer logRefreshTimer;        // Log refresh every 2 seconds
// autoTrackingTimer удалён, автотрекинг теперь работает в отдельном Task
// private DispatcherTimer mt4StatusCheckTimer;    // MT4 status check every 10 seconds (удалён)
```

### Key Methods

#### Initialization
- `InitializeServices()`: Creates and configures all service instances
- `MainWindow_Loaded()`: Sets up window positioning and initial state

#### Hotkey Handlers
- `OnSpaceKeyPressed()`: Captures target window and opens canvas
- `OnTKeyPressed()`: Initiates screen capture process
- `OnEscapeKeyPressed()`: Cleans up active capture sessions
- `OnQKeyPressed()`: Undoes last stroke in CanvasWindow

#### Capture Management
- `StartCapture()`: Initiates screen capture with validation
- `OnCaptureCompleted()`: Handles capture completion events
- `Canvas_Click()`: Opens canvas window for area selection
- `UndoLastStroke()`: Removes the last drawn stroke from CanvasWindow

#### MT4 Connection Management (Refactored)
- `ReconnectMT4_Click()`: Manual MT4 reconnection with status feedback
- `UpdateMT4StatusIndicator()`: Updates visual connection status indicator
- `Mt4SocketService_ConnectionStatusChanged()`: Handles MT4 connection status events

#### UI Event Handlers
- `SymbolButton_Click()`: Handles trading symbol selection
- `ResetDB_Click()`: Resets database and cleans up files
- `RunCaptureTracking_Click()`: Executes capture tracking service
- `AutoTrackingButton_Click()`: Toggles automatic capture tracking (запускает/останавливает Task)

### Автотрекинг (Auto Tracking)

- Автоматический трекинг теперь реализован через отдельный Task и CancellationTokenSource.
- Метод `StartAutoTracking()` запускает Task, который в цикле вызывает `_captureTrackingService.RunOnceAsync()` и ждёт 1 секунду между итерациями.
- Метод `StopAutoTracking()` отменяет токен и завершает Task.
- Такой подход не блокирует UI-поток и позволяет мгновенно останавливать автотрекинг.
- Вся логика автотрекинга теперь асинхронна и не зависит от DispatcherTimer.

### UI Layout

The main window uses a Grid layout with the following structure:

1. **Top Row**: Symbol buttons and control buttons
2. **Middle Row**: Log display area
3. **Bottom Row**: Status information

#### Control Elements
- **Symbol Buttons**: XAUUSD, GBPJPY, EURUSD, USDJPY, GBPUSD
- **Menu Button**: ToggleButton that opens a popup with control options
- **Menu Popup Controls**:
  - Reset DB: Clears database and files
  - Global Hotkey (Space): ToggleButton with status indicator
  - Run Capture Tracking: Manual tracking execution
  - Auto Tracking: ToggleButton with status indicator
  - Reconnect MT4: Manual MT4 reconnection with status indicator
- **Toggle Tracking Viewer**: Opens/closes tracking viewer window
- **Log TextBox**: Real-time log display

#### Status Indicators
- **Hotkey Status**: Green (enabled) / Red (disabled) circle indicator
- **Auto Tracking Status**: Green (active) / Red (inactive) circle indicator
- **MT4 Socket Status**: 
  - Green: Connected
  - Red: Connection failed
  - Yellow: Connecting
  - Gray: Disconnected or unknown status

---

## Helper Classes

### MainHelper

**Purpose**: Provides utility functions for common operations and Windows API interactions.

#### Key Features
- **Window Management**: Cursor-based window detection and validation
- **File Operations**: Cleanup of cropped files and log files
- **Database Operations**: Database reset functionality
- **Screen Positioning**: Multi-monitor support for window placement
- **Validation**: Target window validation with user feedback

#### Main Methods

##### Window Operations
```csharp
public static IntPtr GetWindowUnderCursor()
public static bool ValidateTargetWindow(IntPtr targetWindow, string errorMessage = null)
public static string HandleSymbolButtonClick(object sender, WindowManagementService windowManagementService)
```

##### File Management
```csharp
public static void DeleteCroppedFiles()
public static void DeleteAppLogFile()
public static void ResetDatabase(DatabaseService databaseService, WindowManagementService windowManagementService)
```

##### Screen Management
```csharp
public static (double left, double top, double width, double height) GetScreenPositioning()
```

#### Windows API Integration
- Uses P/Invoke for Windows API calls
- Handles cursor position detection
- Manages window handles and validation
- Supports multi-monitor configurations

### ServiceInitializer

**Purpose**: Centralized service initialization and configuration management.

#### Key Features
- **Service Configuration**: Standardized service setup
- **Event Binding**: Automatic event handler registration
- **Error Handling**: Comprehensive exception management
- **Timer Management**: Log refresh timer creation

#### Main Methods

##### Service Initialization
```csharp
public static void InitializeHotkeysService(HotkeysService hotkeysService, Dispatcher dispatcher, ...)
public static void InitializeWindowManagementService(WindowManagementService windowManagementService, Dispatcher dispatcher)
public static void InitializeMt4SocketService(Mt4SocketService mt4SocketService, Dispatcher dispatcher)
public static void InitializeDatabaseService(DatabaseService databaseService, WindowManagementService windowManagementService)
```


#### Design Benefits
- **Separation of Concerns**: Isolates initialization logic
- **Reusability**: Standardized initialization patterns
- **Maintainability**: Centralized configuration management
- **Error Isolation**: Prevents initialization errors from affecting main window

### Mt4Helper

**Purpose**: Provides MT4 socket communication utilities and message handling.

#### Key Features
- **Connection Management**: MT4 socket connection handling
- **Message Processing**: Command sending and response handling
- **Event Handling**: MT4 message and status event processing
- **Error Management**: Comprehensive error handling and logging

#### Main Methods

##### Connection Operations
```csharp
public static async Task<bool> ConnectToMt4Socket(Mt4SocketService mt4SocketService)
public static async Task<bool> SendMt4Command(Mt4SocketService mt4SocketService, string command)
public static async Task<string> ReadMt4Line(Mt4SocketService mt4SocketService)
```

##### Event Handlers
```csharp
public static void HandleMt4MessageReceived(string message)
public static void HandleMt4ConnectionStatusChanged(string status)
```

#### MT4 Integration Features
- **Asynchronous Communication**: Non-blocking socket operations
- **Message Queuing**: Reliable message transmission
- **Status Monitoring**: Real-time connection status tracking
- **Error Recovery**: Automatic error handling and logging

#### Mt4Helper — ключевые методы:
- `UpdateMt4StatusUI(Window, Ellipse, string status)` — обновляет заголовок окна и цвет индикатора.
- `ReconnectMt4WithUiAsync(Window, Ellipse, Mt4SocketService)` — выполняет реконнект и обновляет UI.
- `CheckMt4ConnectionStatusWithUi(Window, Ellipse, Mt4SocketService)` — проверяет статус MT4 и обновляет UI.

#### Пример использования в MainWindow:
```csharp
private async void ReconnectMT4_Click(object sender, RoutedEventArgs e)
{
    await Mt4Helper.ReconnectMt4WithUiAsync(this, SocketStatusIndicator, mt4SocketService);
}

private void CheckMT4ConnectionStatus()
{
    Mt4Helper.CheckMt4ConnectionStatusWithUi(this, SocketStatusIndicator, mt4SocketService);
}
```

#### Преимущества:
- UI всегда актуален и синхронизирован с состоянием MT4.
- MainWindow не содержит дублирующей или бизнес-логики MT4.
- Легко поддерживать и расширять обработку статуса MT4 в одном месте.

---

## Data Models

### CaptureData

**Purpose**: Represents screen capture metadata and configuration.

#### Properties
```csharp
public string ID { get; set; }                // Unique identifier (timestamp)
public int X { get; set; }                    // Capture X coordinate
public int Y { get; set; }                    // Capture Y coordinate
public int Width { get; set; }                // Capture width
public int Height { get; set; }               // Capture height
public long Handle { get; set; }              // Window handle
public int Monitor { get; set; }              // Monitor index
public string Timestamp { get; set; }         // Capture timestamp
public string ScreenshotPath { get; set; }    // Screenshot file path
public string Symbol { get; set; }            // Trading symbol
public double Risk { get; set; }              // Risk percentage
public bool IsFired { get; set; }             // Breakout detection status
public bool IsSkipped { get; set; }           // Skip processing flag
public string Source { get; set; }            // Capture source ("window")
public string Broker { get; set; }            // Broker name at the moment of capture
public int Duration { get; set; }             // Duration (for binary brokers, 0 for Forex)
public string Model { get; set; }             // "OHLC" или "MACD"
```

#### ID Field Behavior
- **Format**: `yyyyMMddHHmmssfff` (timestamp with milliseconds)
- **Default Value**: Automatically generated when object is created
- **Purpose**: Unique identifier for each capture record
- **Usage**: Can be used for database operations and record identification

#### IsFired Field Behavior
- **Default Value**: `false` for new captures
- **Purpose**: Prevents duplicate breakout detection processing
- **Update Logic**: Set to `true` when breakout is detected (UP or DOWN)
- **Filtering**: Only captures with `IsFired = false` are processed by tracking service
- **Persistence**: State is maintained across application restarts

#### IsSkipped Field Behavior
- **Default Value**: `false` for new captures
- **Purpose**: Allows manual exclusion of captures from processing
- **Usage**: Can be set to `true` to skip specific captures in tracking
- **Filtering**: Skipped captures can be excluded from processing loops
- **Persistence**: State is maintained across application restarts

#### Source Field Behavior
- **Default Value**: `"window"` for new captures
- **Purpose**: Identifies the source of the capture
- **Values**: 
  - `"window"`: Standard window capture via ScreenCaptureOverlay or trading stroke processing
- **Usage**: Distinguishes between different capture methods for processing
- **Persistence**: Source information is maintained across application restarts

#### Broker Field Behavior
- **Purpose**: Stores the broker name at the moment of capture
- **Values**: 
  - Forex broker: "Forex"
  - Binary broker: "Pocket Option", "Binarium", "Quotex"
- **Usage**: Helps identify the broker for specific capture handling
- **Persistence**: State is maintained across application restarts

#### Duration Field Behavior
- **Purpose**: Stores the duration for binary brokers
- **Values**: 
  - Forex broker: 0
  - Binary broker: 1-5
- **Usage**: Helps identify the broker type and duration for specific capture handling
- **Persistence**: State is maintained across application restarts

#### Model Field Behavior
- **Purpose**: Stores the type of capture area
- **Values**: 
  - "OHLC": If both top and bottom coordinates < 656
  - "MACD": If both coordinates >= 656
  - Default: "OHLC" if one is above, one is below
- **Usage**: Helps identify the type of capture area
- **Persistence**: State is maintained across application restarts

### SymbolData

**Purpose**: Represents trading symbol configuration and risk settings.

#### Properties
```csharp
public string Symbol { get; set; }            // Symbol name
public double RiskPercent { get; set; }       // Risk percentage
public bool IsActive { get; set; }            // Active status
public DateTime CreatedAt { get; set; }       // Creation timestamp
public DateTime? LastUpdated { get; set; }    // Last update timestamp
```

### CaptureDebugInfo

**Purpose**: Provides detailed debugging information for screen captures.

#### Properties
```csharp
public int SelectionX { get; set; }           // Selection X coordinate
public int SelectionY { get; set; }           // Selection Y coordinate
public int SelectionWidth { get; set; }       // Selection width
public int SelectionHeight { get; set; }      // Selection height
public int VirtualLeft { get; set; }          // Virtual screen left
public int VirtualTop { get; set; }           // Virtual screen top
public int VirtualRight { get; set; }         // Virtual screen right
public int VirtualBottom { get; set; }        // Virtual screen bottom
public int CropX { get; set; }                // Crop X coordinate
public int CropY { get; set; }                // Crop Y coordinate
public int CropWidth { get; set; }            // Crop width
public int CropHeight { get; set; }           // Crop height
public string ScreenshotPath { get; set; }    // Screenshot path
public string Error { get; set; }             // Error message
public int MonitorIndex { get; set; }         // Monitor index
```

### CaptureDbRoot

**Purpose**: Root container for database structure.

#### Properties
```csharp
public List<CaptureData> Captures { get; set; } = new List<CaptureData>();
public List<SymbolData> Symbols { get; set; } = new List<SymbolData>();
```

### BrokerState

**Purpose**: Global state management for broker selection and trading platform integration.

#### Properties
```csharp
public BrokerType CurrentBroker { get; set; }     // Current selected broker
public event EventHandler<BrokerType> BrokerChanged; // Event fired when broker changes
```

#### Supported Broker Types
- **Forex**: MetaTrader 4/5 platform integration
- **PocketOption**: Pocket Option binary options platform
- **Binarium**: Binarium binary options platform  
- **Quotex**: Quotex binary options platform

#### Key Features
- **Global State**: Singleton instance managed through DI container
- **Event-Driven**: Fires events when broker selection changes
- **MT4 Integration**: Only sends commands to MT4 when broker is set to Forex
- **UI Integration**: Visual feedback in CanvasWindow with broker selection buttons

#### Usage
```csharp
var brokerState = ServiceContainer.Instance.GetService<BrokerState>();
brokerState.CurrentBroker = BrokerType.Forex;
brokerState.BrokerChanged += (sender, brokerType) => 
    Console.WriteLine($"Broker changed to: {brokerType}");
```

#### MT4 Socket Integration
- **Conditional Messaging**: MT4SocketService only sends breakout commands when `brokerState.IsForex` is true
- **Logging**: All broker-related actions are logged with detailed information
- **Error Handling**: Graceful handling of broker state changes during active trading sessions

### Broker Integration
- **Conditional Messaging**: Only sends breakout commands when `BrokerState.CurrentBroker == BrokerType.Forex`
- **State Validation**: Checks broker state before processing breakout events
- **Logging**: Logs when messages are skipped due to non-Forex broker selection
- **Error Prevention**: Prevents MT4 communication errors when using other brokers
- **Unique UID**: Includes trade counter as unique identifier in MT4 messages

### MT4 Message Format
- **BUY Command**: `{"cmd":"BUY","symbol":"XAUUSD","risk":1,"uid":5}`
- **SELL Command**: `{"cmd":"SELL","symbol":"XAUUSD","risk":1,"uid":6}`
- **uid Field**: Contains the current trade counter value from TradeCounter service
- **Purpose**: Provides unique identification for each trade command sent to MT4

---

## Service Layer

The service layer is comprehensively documented in `Services_Documentation.md`. Key services include:

- **Logger**: Centralized logging with thread safety
- **HotkeysService**: Global keyboard hook management
- **WindowManagementService**: Trading platform window management
- **ScreenshotService**: Screen capture functionality
- **CaptureService**: Capture orchestration and coordination
- **CaptureTrackingService**: Automated capture monitoring with breakout detection
- **DatabaseService**: JSON-based data persistence with IsFired tracking
- **Mt4SocketService**: MT4 platform communication
- **ToastNotifyService**: Показывает всплывающие toast-уведомления (снизу по центру основного экрана, два типа: Success (зелёный), Error (красный)).
- **JForexWindowsManagerService**: Управление окнами GForex, клики мышью и отправка событий клавиатуры для автоматического создания трендовых линий.

### Dependency Injection Container

**ServiceContainer**: Centralized service management using singleton pattern.

#### Key Features
- **Singleton Pattern**: Single instance across application lifecycle
- **Service Registration**: Supports both singleton instances and factory methods
- **Lazy Loading**: Services are created on first access
- **Type Safety**: Strongly typed service resolution
- **Lifecycle Management**: Automatic cleanup of disposable services

#### Usage
```csharp
// Registration
ServiceContainer.Instance.RegisterSingleton(new DatabaseService());
ServiceContainer.Instance.Register(() => new CaptureService());

// Resolution
var databaseService = ServiceContainer.Instance.GetService<DatabaseService>();
var captureService = ServiceContainer.Instance.GetService<CaptureService>();
```

#### Service Initialization
- **ServiceInitializer.RegisterAllServices()**: Registers all application services
- **ServiceInitializer.CleanupServices()**: Disposes and cleans up all services
- **Automatic Dependency Resolution**: Services automatically resolve their dependencies

### DatabaseService - Breakout Tracking Methods

#### GetAllUnfiredCaptures()
```csharp
public List<CaptureData> GetAllUnfiredCaptures()
```
- **Purpose**: Retrieves only captures that haven't been processed for breakout detection
- **Filter**: Returns captures where `IsFired = false`
- **Usage**: Used by CaptureTrackingService to avoid reprocessing fired captures
- **Performance**: Efficient filtering using LINQ Where clause

#### GetAllUnfiredAndUnskippedCaptures()
```csharp
public List<CaptureData> GetAllUnfiredAndUnskippedCaptures()
```
- **Purpose**: Retrieves captures that are both unfired and not skipped
- **Filter**: Returns captures where `IsFired = false` and `IsSkipped = false`
- **Usage**: Used for processing only relevant captures
- **Performance**: Efficient filtering using LINQ Where clause

#### UpdateCaptureIsFired()
```csharp
public void UpdateCaptureIsFired(CaptureData capture, bool isFired = true)
```
- **Purpose**: Marks a capture as processed after breakout detection
- **Parameters**: 
  - `capture`: The capture record to update
  - `isFired`: Boolean flag (defaults to true)
- **Matching Logic**: Finds capture by unique ID field
- **Logging**: Logs successful updates and warnings for missing records
- **Persistence**: Immediately saves changes to database

#### UpdateCaptureIsSkipped()
```csharp
public void UpdateCaptureIsSkipped(CaptureData capture, bool isSkipped = true)
```
- **Purpose**: Marks a capture as skipped to exclude it from processing
- **Parameters**: 
  - `capture`: The capture record to update
  - `isSkipped`: Boolean flag (defaults to true)
- **Matching Logic**: Finds capture by unique ID field
- **Logging**: Logs successful updates and warnings for missing records
- **Persistence**: Immediately saves changes to database

### CaptureTrackingService - Breakout Detection Workflow

#### Enhanced Processing Logic
1. **Unfired and Unskipped Capture Retrieval**: Uses `GetAllUnfiredAndUnskippedCaptures()` to get only relevant captures
2. **Breakout Detection**: Processes each unfired and unskipped capture through TrendlineBreakDetector
3. **State Update**: Calls `UpdateCaptureIsFired()` when breakout is detected
4. **Logging**: Enhanced logging with capture ID for better tracking

#### Breakout Detection States
- **NoTrendline**: No trendline detected, capture remains unfired
- **NoBreakout**: Trendline detected but price hasn't broken through
- **BreakoutUp**: Upward breakout confirmed, capture marked as fired
- **BreakoutDown**: Downward breakout confirmed, capture marked as fired

#### Performance Benefits
- **Eliminates Redundant Processing**: Fired captures are excluded from future scans
- **Manual Exclusion Support**: Skipped captures are excluded from processing
- **Reduces Database Load**: Only processes relevant captures
- **Maintains State**: Breakout and skip status persist across application restarts
- **Scalable**: Efficient handling of large capture databases
- **Unique Identification**: Uses capture ID for precise record tracking

### TrendlineBreakDetector

- **TrendlineBreakDetector**: Детектирование breakout теперь разделено на две функции: `DetectBreakoutOHLC` и `DetectBreakoutMACD`. Основной метод `DetectBreakout(Bitmap, model, debugPath)` принимает только модель ("OHLC" или "MACD") и внутри выбирает нужную функцию и параметры. Все параметры (размер зоны, порог заполнения и т.д.) задаются внутри детектора, а не в TrackingService.
- **CaptureTrackingService**: При вызове детектора передает только capture.Model (строка), остальные параметры определяются внутри TrendlineBreakDetector.

### SymbolSettingsManager

**File:** `Services/SymbolSettingsManager.cs`

**Purpose**: Управляет настройками символов (DefaultRisk и DefaultDuration) в памяти (DI Container) во время выполнения приложения.

**Key Features**:
- **In-Memory Storage**: Настройки символов хранятся в памяти, а не в базе данных
- **DefaultRisk Management**: Управляет значениями риска по умолчанию для каждого символа
- **DefaultDuration Management**: Управляет значениями длительности по умолчанию для каждого символа
- **Automatic Updates**: Автоматически обновляет настройки при создании CaptureData
- **Symbol Initialization**: Инициализирует настройки символов из базы данных при запуске

**Main Methods**:
- `InitializeSymbolSettings()`: Инициализирует настройки символов из базы данных
- `GetDefaultRisk(string symbolKey)`: Получает DefaultRisk для символа
- `GetDefaultDuration(string symbolKey)`: Получает DefaultDuration для символа
- `UpdateSettingsFromCapture(string symbolKey, double risk, int duration)`: Обновляет настройки при создании CaptureData
- `ResetAllSettings()`: Сбрасывает все настройки к дефолтным значениям

**Integration**:
- **CanvasWindow**: Применяет настройки символа при открытии окна и смене символа
- **CaptureService**: Обновляет настройки при создании CaptureData
- **MainWindow**: Применяет настройки к открытому CanvasWindow при смене символа

**Default Values**:
- **XAUUSD**: Risk=1.0, Duration=2
- **GBPJPY**: Risk=5.0, Duration=2
- **USDJPY**: Risk=10.0, Duration=2
- **EURJPY**: Risk=5.0, Duration=2
- **EURUSD**: Risk=10.0, Duration=2
- **GBPUSD**: Risk=5.0, Duration=2

**Usage**:
```csharp
var symbolSettingsManager = ServiceContainer.Instance.GetService<SymbolSettingsManager>();
double risk = symbolSettingsManager.GetDefaultRisk("XAUUSD"); // Returns 1.0
int duration = symbolSettingsManager.GetDefaultDuration("XAUUSD"); // Returns 2
symbolSettingsManager.UpdateSettingsFromCapture("XAUUSD", 5.0, 3); // Updates settings
```

### ToastNotifyService

**File:** `Services/ToastNotifyService.cs`

**Purpose**: Displays temporary notification windows (toasts) to the user

**Features**:
- **Toast Types**:
  - `Success`: Green background (60, 180, 75)
  - `Error`: Yellow background (255, 255, 0) 
  - `BreakoutUp`: Green background (60, 180, 75)
  - `BreakoutDown`: Red background (220, 50, 47)
- **Positioning**: Bottom center of primary screen
- **Auto-dismiss**: Configurable duration (default 2.5s)
- **Non-intrusive**: Doesn't steal focus, appears on top
- **Trade Counter Integration**: Breakout messages include trade numbers (BUY #N, SELL #N)

**Usage**:
```csharp
var toastService = ServiceContainer.Instance.GetService<ToastNotifyService>();
toastService.ShowToast("Message", ToastType.Success);
```

**Breakout Messages**:
- **BUY #N**: Upward breakout detected (green toast)
- **SELL #N**: Downward breakout detected (red toast)
- Where N is the sequential trade number from TradeCounter

#### DurationState

**Purpose**: Глобальное состояние для хранения выбранного значения Duration (для бинарных брокеров).

```csharp
public int CurrentDuration { get; set; } // Default: 2
public event EventHandler<int> DurationChanged;
```

- Хранится в DI-контейнере.
- Используется для UI и при создании Capture.

#### TradeCounter

**Purpose**: Глобальное состояние для подсчета количества протреканных сделок (is_fired).

```csharp
public int FiredTradesCount { get; } // Текущий счетчик протреканных сделок
public int IncrementFiredTrades() // Увеличивает счетчик и возвращает новый номер
public void ResetFiredTrades() // Сбрасывает счетчик в 0
public void SetFiredTradesCount(int count) // Устанавливает счетчик в заданное значение
```

- Хранится в DI-контейнере.
- Используется в CaptureTrackingService для нумерации toast-уведомлений о breakout.
- Сбрасывается при выполнении ResetDB (нужно добавить в группу переменных, которые обнуляются при ResetDB).
- Toast-сообщения теперь имеют формат "BUY #N" и "SELL #N" где N - номер сделки.
- MT4 сообщения включают поле "uid" с текущим значением счетчика.

#### JForexWindowsManagerService

**File:** `Services/JForexWindowsManagerService.cs`

**Purpose**: Управляет окнами GForex, отправляет клики мышью и события клавиатуры для автоматического создания трендовых линий.

**Key Features**:
- **Mouse Control**: Отправка кликов мыши в указанные координаты
- **Keyboard Events**: Отправка нажатий клавиш (например, клавиша 'A' для активации инструмента трендовой линии)
- **Coordinate Conversion**: Конвертация координат Canvas в координаты окна GForex
- **Async Operations**: Асинхронные операции для неблокирующего UI

**Main Methods**:
- `AddTrendlineAsync(IntPtr targetWindowHandle, int targetWindowHandleWidth, double point1X, double point1Y, double point2X, double point2Y)`: Добавляет трендовую линию в GForex
- `SendKeyPress(IntPtr windowHandle, char key)`: Отправляет нажатие клавиши
- `ClickAtPosition(IntPtr windowHandle, int x, int y)`: Кликает мышью в указанной позиции

**Integration**:
- **CanvasWindow**: Интегрируется с CanvasWindow в Trading Mode для автоматического создания трендовых линий при рисовании штрихов
- **Forex Broker**: Работает только когда выбран Forex брокер
- **Stroke Processing**: Обрабатывает штрихи из InkCanvas и конвертирует их в трендовые линии

**Workflow**:
1. При создании штриха в Trading Mode (если брокер Forex)
2. Извлекает первую и последнюю точки штриха
3. Конвертирует координаты Canvas в экранные координаты
4. Отправляет клавишу 'A' для активации инструмента трендовой линии
5. Кликает на первую точку
6. Кликает на вторую точку
7. Логирует результат операции

**Usage**:
```csharp
var jForexService = ServiceContainer.Instance.GetService<JForexWindowsManagerService>();
bool success = await jForexService.AddTrendlineAsync(
    targetWindowHandle, 
    windowWidth, 
    point1X, point1Y, 
    point2X, point2Y
);
```

**Registration**:
```csharp
container.RegisterSingleton(new JForexWindowsManagerService());
```

#### CanvasWindow

- **TRADING!!** — отображается, если выбран брокер Forex.
- **BINARY!!** — отображается, если выбран любой бинарный брокер.
- **RISK** — блок с кнопками выбора риска (1, 5, 10, 20, 30).
- **DURATION** — блок с кнопками выбора длительности (1, 2, 3, 4, 5), появляется только если выбран бинарный брокер. По умолчанию выбрана 2.
- **Hotkeys**:
  - **Q**: Undo last stroke (removes the most recently drawn stroke)
  - **C**: Clear all strokes and delete canvas files
  - **T**: Toggle trading mode
  - **Arrow Keys (←/↑/↓/→)**: Shift canvas strokes
  - **W/A/S/D**: Reserved for future use
  - **Space**: Force focus to InkCanvas3
  - **Escape**: Close window
- При создании Capture:
  - Если брокер Forex — Duration = 0.
  - Если бинарный — Duration = выбранное значение.
- **Trading Mode Queue System**:
  - При рисовании штриха в Trading Mode он добавляется в очередь обработки
  - Штрих остается видимым на Canvas до завершения обработки
  - После обработки штрих удаляется с Canvas и обновляется фон
  - Source у CaptureData всегда "window"
  - JForex Integration работает только для Forex брокера

#### Service Layer

- **DI-контейнер** теперь содержит также DurationState.
- Все сервисы и UI используют DurationState для синхронизации значения длительности.

#### Event Flow

- При смене брокера UI автоматически обновляет надпись и видимость Duration.
- При смене Duration значение сохраняется в DurationState и выделяется соответствующая кнопка.
- При создании Capture значение Duration берется из DurationState (если брокер не Forex).

#### Trading Mode Queue System

**Purpose**: Новая система обработки торговых штрихов в CanvasWindow.

**Key Features**:
- **Queue Management**: Штрихи в Trading Mode добавляются в очередь (`Queue<TradingStrokeItem>`)
- **Sequential Processing**: Обработка штрихов происходит последовательно
- **Visual Feedback**: Штрих остается видимым до завершения обработки
- **Automatic Cleanup**: После обработки штрих удаляется с Canvas
- **Background Update**: После удаления штриха обновляется фоновое изображение

**TradingStrokeItem Class**:
```csharp
private class TradingStrokeItem
{
    public Stroke Stroke { get; set; }
    public CaptureData CaptureData { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Main Methods**:
- `AddStrokeToTradingQueue(Stroke stroke, StrokeCompletedEventArgs args)`: Добавляет штрих в очередь
- `ProcessTradingQueueAsync()`: Асинхронно обрабатывает очередь
- `ProcessTradingStrokeAsync(TradingStrokeItem queueItem)`: Обрабатывает один штрих
- `RemoveStrokeFromCanvas(Stroke stroke)`: Удаляет штрих с Canvas

**Workflow**:
1. Пользователь рисует штрих в Trading Mode
2. Штрих добавляется в очередь обработки
3. Если очередь не обрабатывается — запускается асинхронная обработка
4. Для каждого штриха:
   - Создается CaptureData (source = "window")
   - Сохраняется в базу данных
   - Делается скриншот
   - Если Forex брокер — вызывается GForex Manager
   - После обработки штрих удаляется с Canvas
   - Обновляется фоновое изображение
5. Обработка продолжается до опустошения очереди

**Benefits**:
- **No Trading Canvas**: Убрана сложная логика разделения штрихов
- **Sequential Processing**: Гарантирует порядок обработки штрихов
- **Visual Feedback**: Пользователь видит процесс обработки
- **Automatic Cleanup**: Штрихи автоматически удаляются после обработки
- **Background Updates**: Фон обновляется для отображения результатов

### BrokerSettingsManager

**File:** `Services/BrokerSettingsManager.cs`

**Purpose**: Управляет настройками брокеров (DefaultRisk и DefaultDuration) в памяти (DI Container) во время выполнения приложения.

**Key Features**:
- **Per-Broker Settings**: Каждый брокер имеет свои настройки Risk и Duration
- **In-Memory Storage**: Настройки брокеров хранятся в памяти, а не в базе данных
- **DefaultRisk Management**: Управляет значениями риска по умолчанию для каждого брокера
- **DefaultDuration Management**: Управляет значениями длительности по умолчанию для каждого брокера
- **Automatic Updates**: Автоматически обновляет настройки при создании CaptureData
- **Broker Switching**: При смене брокера автоматически применяются его настройки

**Main Methods**:
- `GetDefaultRisk(BrokerType brokerType)`: Получает DefaultRisk для указанного брокера
- `GetDefaultDuration(BrokerType brokerType)`: Получает DefaultDuration для указанного брокера
- `UpdateSettingsFromCapture(BrokerType brokerType, double risk, int duration)`: Обновляет настройки брокера при создании CaptureData
- `ResetAllSettings()`: Сбрасывает все настройки к дефолтным значениям

**Default Settings**:
- **Forex**: Risk=1.0, Duration=2
- **PocketOption**: Risk=5.0, Duration=2
- **Binarium**: Risk=10.0, Duration=2
- **Quotex**: Risk=5.0, Duration=2

**Integration**:
- Интегрируется с `CanvasWindow` и `ScreenCaptureOverlay` для применения настроек при смене брокера
- Интегрируется с `CaptureService` для обновления настроек при создании CaptureData
- Интегрируется с `MainWindow` для сброса настроек при сбросе базы данных