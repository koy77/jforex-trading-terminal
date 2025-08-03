# Архитектура и документация ScreenCaptureApp

## Введение
ScreenCaptureApp — это WPF-приложение для трекинга, анализа и автоматизации работы с торговыми окнами (JForex, MT4, Pocket Option и др.), с поддержкой глобальных хоткеев, захвата экрана, анализа паттернов, интеграции с брокерами и ведения истории сделок.

## Архитектурные принципы
- **Dependency Injection (DI):** Все сервисы и состояния регистрируются через `ServiceContainer` (Singleton DI).
- **Слои:** UI (WPF окна), Services (логика), Models (данные), Helpers (утилиты).
- **Асинхронность:** Для работы с сокетами, файловыми операциями, обновлением UI.
- **События:** Для коммуникации между сервисами и окнами (например, события BreakoutDetected, CaptureCompleted).

---

## Services
### CaptureService
- **Назначение:** Управление захватом экрана, глобальными хуками мыши, сохранением CaptureData.
- **Поля:**
  - `_databaseService`, `_screenshotService` — DI зависимости.
  - mouseHook, mouseProc, source — для глобального хука мыши.
- **Методы:**
  - `CaptureAreaWithSymbolAndRisk(...)` — основной метод захвата области, сохраняет CaptureData, делает скриншот, обновляет риски.
  - `GetVirtualScreenBounds()` — вычисляет виртуальные границы всех мониторов.
  - Методы для установки/удаления глобального mouse hook.
- **События:**
  - `CaptureCompleted`, `MouseHookEvent` — для уведомления UI.
- **Связи:** Использует DatabaseService, ScreenshotService, BrokerState, DurationState, ToolbarSettingsManager.

### HotkeysService
- **Назначение:** Глобальный перехват клавиш, генерация событий для UI и сервисов.
- **Поля:**
  - Глобальный hook через WinAPI, флаги IsEnabled.
- **События:**
  - OnSpaceKeyPressed, OnEscapeKeyPressed, OnTKeyPressed, OnEnterKeyPressed, OnAKeyPressed и др.
- **Методы:**
  - Enable/Disable, Dispose, внутренний callback KeyboardHookCallback.
- **Связи:** Используется в MainWindow, CaptureService, CanvasWindow.

### Mt4SocketService
- **Назначение:** TCP-клиент для связи с MetaTrader 4, отправка команд, получение истории, обновление статусов.
- **Поля:**
  - _tcpClient, _networkStream, _host, _port, _isConnected, _readTask и др.
- **События:**
  - MessageReceived, ConnectionStatusChanged, OrdersSummaryReceived.
- **Методы:**
  - ConnectAsync, DisconnectAsync, WriteAsync, ReadLineAsync, ProcessIncomingMessage, HandleOrderClosedEvent, HandleOrdersSummaryEvent.
- **Связи:** Используется в MainWindow, CaptureTrackingService, CaptureTrackingViewer.

### HttpServerService
- **Назначение:** HTTP сервер для приема данных от внешних источников (GForex), обработка ценовых уровней и генерация событий.
- **Поля:**
  - _listener, _isRunning, _port, _cancellationTokenSource, RecentPriceLevels
- **События:**
  - PriceLevelReceived, StatusChanged, NewPriceLevelReceived
- **Методы:**
  - StartAsync, StopAsync, HandlePriceLevelRequestAsync, OnNewPriceLevelReceived
- **Обработка данных:**
  - Прием POST запросов с JSON данными ценовых уровней
  - Предобработка JSON для исправления форматов чисел
  - Создание PriceLevelEventData и генерация событий
- **Связи:** Используется TradingToolbar для получения ценовых уровней, отправляемых в MT4.

### DatabaseService
- **Назначение:** Работа с локальной JSON-базой (captures, symbols), CRUD для CaptureData и SymbolData.
- **Поля:**
  - _dbPath, _jsonOptions.
- **Методы:**
  - SaveCapture, UpdateCapture, GetAllCaptures, GetAllFiredCaptures, GetAllSkippedCaptures, GetTrackingCaptures, UpdateCaptureIsFired, UpdateCaptureIsSkipped, InitializeSymbolsFromWindowManagementService, SaveSymbol, UpdateSymbolRisk, ClearAllCaptures, ClearAllData.
- **Связи:** Используется CaptureService, CaptureTrackingViewer, MainWindow.

### ToolbarSettingsManager
- **Назначение:** Хранение и управление настройками TradingToolbar (risk, broker, duration) для каждого окна по window handle. Singleton, только в памяти (DI), не сохраняется в базу.
- **Поля:**
  - Словарь: handle → ToolbarSettings (risk, broker, duration)
- **Методы:**
  - GetSettings(handle), SetSettings(handle, ...), UpdateSettings(handle, ...), RemoveSettings(handle)
- **Связи:** Используется CanvasWindow, ScreenCaptureOverlay, MainWindow для инициализации и сохранения настроек тулбара по окну.

### TradingToolbar
- **Назначение:** Основной UI компонент для управления торговыми операциями, обработки ценовых уровней от HTTP сервера и отправки команд в MT4.
- **Поля:**
  - Состояния ценовых уровней: `_isWaitingForEntryPrice`, `_isWaitingForStopLossPrice`
  - Цены: `_entryPrice`, `_stopLossPrice`, `_currentTradeSymbol`
  - Сервисы: `_mt4SocketService`, `_httpServerService`, `_toastNotifyService`
- **Методы:**
  - `ProcessPriceLevelEvent()` - обработка событий от HTTP сервера
  - `SendTradeCommandToMt4()` - отправка команд в MT4
  - `CancelPriceLevelEntry()` - отмена по Escape
  - `ShowPrices()`, `HidePrices()` - управление UI
- **События:**
  - `RiskChanged`, `BrokerChanged`, `DurationChanged`
- **Логика обработки цен:**
  1. Первое событие → заполняет EntryPrice
  2. Второе событие → заполняет StopLoss → отправляет команду в MT4
  3. Escape → сбрасывает все цены
- **Связи:** Использует HttpServerService, Mt4SocketService, ToastNotifyService, ToolbarSettingsManager.

(Остальные сервисы: CaptureTrackingService, ScreenshotService, WindowManagementService, JForexWindowsManagerService, ToastNotifyService, Logger — аналогично, с описанием их задач и основных методов.)

---

## Models
### CaptureData
- **Назначение:** Основная модель захвата (скриншота/паттерна).
- **Поля:**
  - ID, X, Y, Width, Height, Handle, Monitor, Timestamp, ScreenshotPath, Symbol, Risk, IsFired, IsSkipped, Source, Broker, Duration, Model, Mt4Order, Period.

### TradeCounter
- **Назначение:** Глобальный счетчик срабатываний (fired trades).
- **Поля/методы:** FiredTradesCount, IncrementFiredTrades, ResetFiredTrades, SetFiredTradesCount.

### CaptureDebugInfo
- **Назначение:** Детальная информация о захваченном участке (координаты, путь к скриншоту, ошибки).
- **Поля:** SelectionX, SelectionY, SelectionWidth, SelectionHeight, VirtualLeft, VirtualTop, VirtualRight, VirtualBottom, CropX, CropY, CropWidth, CropHeight, ScreenshotPath, Error, MonitorIndex.

### DurationState
- **Назначение:** Глобальное состояние длительности (duration) для захватов.
- **Поля:** CurrentDuration, событие DurationChanged.

### ToolbarSettings
- **Назначение:** Модель для хранения настроек TradingToolbar по handle окна.
- **Поля:** Handle, Risk, Duration, Broker.

---

## Helpers
### MainHelper
- **Назначение:** Утилиты для работы с окнами, позиционированием, сбросом базы, парсингом Period из заголовка окна.
- **Ключевые методы:**
  - GetWindowTitle, ParsePeriodFromTitle, GetWindowUnderCursor, ResetDatabase, HandleSymbolButtonClick, DeleteCroppedFiles, DeleteAppLogFile, GetScreenPositioning, GetTrackingViewerPositioning.

### ServiceInitializer
- **Назначение:** Регистрация и инициализация всех сервисов в DI-контейнере, подписка на события.
- **Ключевые методы:** RegisterAllServices, CleanupServices, InitializeHotkeysService, InitializeWindowManagementService, InitializeMt4SocketService, InitializePocketOptionSocketService, InitializeDatabaseService, InitializeToolbarSettingsManager.

### TrendlineBreakDetector
- **Назначение:** Алгоритмы анализа скриншотов на предмет пробоя трендовой линии (OHLC/MACD), кластеризация, поиск breakout.
- **Ключевые методы:** DetectBreakout, DetectBreakoutOHLC, DetectBreakoutMACD, SaveDebugVisualization.

### Mt4Helper, PocketOptionHelper
- **Назначение:** Вспомогательные методы для работы с MT4 и Pocket Option сокетами, обновление UI-индикаторов, reconnect, обработка сообщений.

---

## Основные окна
### MainWindow
- **Назначение:** Главное окно приложения, панель управления, запуск CanvasWindow, ScreenCaptureOverlay, SimpleTradingOverlay, трекинг, логирование, переключение символов, управление сервисами.
- **Поля:** overlay, simpleTradingOverlay, isCapturing, targetWindow, currentCanvasWindow, activeSymbol, _autoTrackingCts, _autoTrackingTask, isAutoTrackingActive, isYellowBrush.
- **Методы:**
  - Инициализация сервисов, обработка хоткеев, запуск/остановка автотрекинга, обработка событий Capture, обновление UI-индикаторов, логирование, ResetDB, переключение символов, обработка событий MT4 и Pocket Option.
  - `InitializeSimpleTradingOverlay()` — инициализация SimpleTradingOverlay при запуске приложения.
- **Связи:** Использует все сервисы через DI, взаимодействует с CanvasWindow, ScreenCaptureOverlay, SimpleTradingOverlay, CaptureTrackingViewer.

### ScreenCaptureOverlay
- **Назначение:** Окно для выделения области экрана, интеграция с CaptureService, поддержка хоткеев, рисование прямоугольника, отображение координат, выбор риска/длительности.
- **Поля:** startPoint, isDrawing, isDragging, isResizing, dragStartPoint, originalRect, resizeDirection, activeSymbol, _windowHandle, selectedRisk, selectedDuration, brokerState, durationState, _captureService, _databaseService, _hotkeysService.
- **Методы:**
  - Захват области, обработка мыши, обновление координат, взаимодействие с CaptureService, завершение захвата, интеграция с TradingToolbar.
- **События:** CaptureCompleted.

### SimpleTradingOverlay
- **Назначение:** Окно для автоматического отслеживания курсора мыши и показа TradingToolbar на окнах JForex с распознанными символами. Запускается вместе с приложением и работает в фоновом режиме.
- **Поля:** mouseHook, mouseProc, source, _toolbarSettingsManager, _brokerState, _durationState, _windowManagementService, _updateTimer, _currentWindowHandle, _isEnabled.
- **Методы:**
  - `SetupMouseHook()` — установка глобального хука мыши для отслеживания перемещений курсора.
  - `MouseHookCallback()` — обработка событий мыши, определение окна под курсором.
  - `UpdateOverlayPosition()` — позиционирование оверлея на окне с проверкой символа в заголовке.
  - `ExtractSymbolFromWindowTitle()` — извлечение символа из заголовка окна с использованием WindowManagementService.
  - `ApplyToolbarSettings()` — применение настроек тулбара для конкретного окна.
  - `Enable()/Disable()` — включение/отключение функциональности оверлея.
- **Особенности:**
  - Автоматически отслеживает перемещения курсора мыши через глобальный хук.
  - Проверяет заголовок окна под курсором на наличие символов JForex (XAUUSD, GBPJPY, EURUSD и др.).
  - Показывает TradingToolbar только на окнах с распознанными символами.
  - Если символ не найден в заголовке, оверлей остается на предыдущем окне.
  - Использует ToolbarSettingsManager для сохранения индивидуальных настроек по handle окна.
  - Интегрируется с WindowManagementService для получения заголовков окон и словаря символов.

### CanvasWindow
- **Назначение:** Окно для рисования трендовых линий, работы с паттернами, интеграция с JForex, сохранение CaptureData по штрихам, поддержка режимов (simple/trading), сдвиг canvas, обработка stroke queue.
- **Поля:** targetWindowHandle, activeSymbol, selectedRisk, selectedDuration, tradingStrokeQueue, isProcessingTradingQueue, brokerState, durationState, jForexService, canvasOffsetX, canvasOffsetY.
- **Методы:**
  - ToggleTradingMode, UpdateSimpleBrushColor, UpdateTargetWindow, ShiftCanvasUp/Down/Left/Right, ClearCanvas, ReinitializeForNewSymbol, CreateCaptureDataFromStroke, ProcessTradingQueueAsync, AddTrendlineToGForexAsync.
- **События:** StrokeCompleted.

### CaptureTrackingViewer
- **Назначение:** Окно для просмотра истории захватов (Tracking, Fired, Skipped), фильтрация, отображение скриншотов, пропуск/активация захватов, интеграция с DatabaseService.
- **Поля:** _databaseService, _trackingDir, _croppedDir, _allItems, _currentFilter, _trackingService.
- **Методы:** LoadImages, LoadTrackingImages, LoadFiredImages, LoadSkippedImages, ShowFilteredItems, UpdateCounts, RefreshTrackingImages, обработка кнопок фильтрации и Skip.

---

## Взаимосвязи между компонентами
- Все сервисы регистрируются через ServiceContainer (DI singleton).
- MainWindow и другие окна получают сервисы через ServiceContainer.Instance.GetService<T>().
- CaptureService и CanvasWindow используют DatabaseService для сохранения CaptureData.
- Mt4SocketService и PocketOptionSocketService работают с внешними брокерами, отправляют команды, получают события.
- CaptureTrackingViewer отображает данные из DatabaseService, реагирует на события CaptureTrackingService.
- HotkeysService генерирует события для MainWindow, CaptureService, CanvasWindow.
- ToolbarSettingsManager обеспечивает хранение и доступ к настройкам TradingToolbar по handle окна (в памяти, на время работы приложения).
- SimpleTradingOverlay использует WindowManagementService для получения заголовков окон и словаря символов JForex.
- SimpleTradingOverlay интегрируется с ToolbarSettingsManager для сохранения индивидуальных настроек по окнам.
- Helpers используются для утилит, парсинга, сброса базы, работы с окнами.

---

## Примеры сценариев работы
1. **Захват паттерна:**
   - Пользователь нажимает хоткей (A, Space, T и др.)
   - CaptureService или CanvasWindow инициирует захват, сохраняет CaptureData через DatabaseService, делает скриншот.
   - CaptureTrackingViewer отображает новый захват.
2. **Автоматический трекинг:**
   - MainWindow запускает CaptureTrackingService по таймеру.
   - При обнаружении паттерна — событие BreakoutDetected, отправка команды в MT4 через Mt4SocketService.
3. **Работа с историей:**
   - CaptureTrackingViewer позволяет фильтровать захваты (Tracking, Fired, Skipped), просматривать скриншоты, помечать как Skipped.
4. **Интеграция с брокерами:**
   - Mt4SocketService и PocketOptionSocketService отправляют команды, получают историю, обновляют UI-индикаторы через Helpers.
5. **Индивидуальные настройки TradingToolbar:**
   - Для каждого окна (handle) ToolbarSettingsManager хранит risk, broker, duration. При открытии окна настройки подгружаются из ToolbarSettingsManager, при изменении — обновляются там же.
6. **Автоматическое отслеживание окон JForex:**
   - SimpleTradingOverlay запускается вместе с приложением и работает в фоновом режиме.
   - При перемещении курсора мыши окно отслеживает окно под курсором через глобальный хук.
   - Проверяет заголовок окна на наличие символов JForex (XAUUSD, GBPJPY, EURUSD и др.).
   - Если символ найден, позиционирует TradingToolbar в верхней части окна и применяет сохраненные настройки.
   - Если символ не найден, оверлей остается на предыдущем окне.

---

_Документация подготовлена для Cursor.AI. Для расширения — добавить описание остальных сервисов, моделей, вспомогательных классов._

---

# UpdateLog

## 2024-06-XX
- Удалены SymbolSettingsManager, BrokerSettingsManager и связанные модели (SymbolSettings, BrokerSettings).
- Вся логика хранения настроек TradingToolbar (risk, broker, duration) теперь реализована через ToolbarSettingsManager — singleton DI-сервис, который хранит настройки для каждого окна по handle (только в памяти, не сохраняется в базу).
- CanvasWindow, ScreenCaptureOverlay и другие окна используют ToolbarSettingsManager для инициализации и сохранения индивидуальных настроек тулбара.
- Документация обновлена: удалены устаревшие разделы, добавлено описание новой архитектуры ToolbarSettingsManager.

## 2024-12-XX
- Добавлено новое окно SimpleTradingOverlay для автоматического отслеживания курсора мыши и показа TradingToolbar на окнах JForex.
- SimpleTradingOverlay использует глобальный хук мыши для отслеживания перемещений курсора.
- Реализована проверка заголовков окон на наличие символов JForex (XAUUSD, GBPJPY, EURUSD, GBPUSD, USDJPY, EURJPY).
- Оверлей показывается только на окнах с распознанными символами, если символ не найден — остается на предыдущем окне.
- Интеграция с WindowManagementService для получения заголовков окон и словаря символов.
- Интеграция с ToolbarSettingsManager для сохранения индивидуальных настроек по handle окна.
- Автоматический запуск SimpleTradingOverlay вместе с приложением в MainWindow.
- Документация обновлена: добавлено описание SimpleTradingOverlay, обновлены разделы архитектуры и примеры сценариев работы.
