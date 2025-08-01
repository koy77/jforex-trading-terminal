# Сервисы (Services)

## Обзор

Папка `Services` содержит все сервисы приложения, реализующие различные аспекты функциональности. Все сервисы зарегистрированы в DI-контейнере (`ServiceContainer`) и доступны через внедрение зависимостей.

## Архитектура сервисов

### Принципы проектирования
- **Single Responsibility Principle**: Каждый сервис отвечает за одну область функциональности
- **Dependency Injection**: Все сервисы используют DI для управления зависимостями
- **Singleton Pattern**: Большинство сервисов зарегистрированы как singleton
- **Interface Segregation**: Сервисы предоставляют четкие интерфейсы

### Регистрация в DI-контейнере
```csharp
// В ServiceContainer
services.AddSingleton<CaptureService>();
services.AddSingleton<DatabaseService>();
services.AddSingleton<HotkeysService>();
services.AddSingleton<Mt4SocketService>();
services.AddSingleton<ToolbarSettingsManager>();
services.AddSingleton<ScreenshotService>();
services.AddSingleton<JForexWindowsManagerService>();
services.AddSingleton<BinaryOptionsSocketService>();
services.AddSingleton<CaptureTrackingService>();
services.AddSingleton<WindowManagementService>();
services.AddSingleton<ToastNotifyService>();
```

## Список сервисов

### 1. [CaptureService](CaptureService.md)
**Назначение**: Основной сервис для захвата областей экрана и управления глобальными хуками мыши.

**Ключевые возможности**:
- Захват областей экрана с символом и риском
- Глобальный хук мыши
- Создание CaptureData
- Интеграция с DatabaseService и ScreenshotService

### 2. [DatabaseService](DatabaseService.md)
**Назначение**: Сервис для работы с локальной JSON-базой данных.

**Ключевые возможности**:
- CRUD операции для CaptureData и SymbolData
- JSON сериализация/десериализация
- Автоматическое создание ID
- Обработка ошибок и валидация

### 3. [HotkeysService](HotkeysService.md)
**Назначение**: Сервис для глобального перехвата клавиш.

**Ключевые возможности**:
- Глобальный хук клавиатуры
- Обработка горячих клавиш
- События для различных клавиш
- Потокобезопасная обработка

### 4. [Mt4SocketService](Mt4SocketService.md)
**Назначение**: Сервис для TCP-соединения с MetaTrader 4.

**Ключевые возможности**:
- Асинхронное TCP-соединение
- JSON протокол обмена сообщениями
- Обработка событий MT4
- Интеграция с торговыми операциями

### 5. [ToolbarSettingsManager](ToolbarSettingsManager.md)
**Назначение**: Сервис для управления настройками TradingToolbar по handle окна.

**Ключевые возможности**:
- Хранение настроек в памяти
- Автоматическое создание дефолтных настроек
- Управление настройками по окнам
- Интеграция с TradingToolbar

### 6. [ScreenshotService](ScreenshotService.md)
**Назначение**: Сервис для создания скриншотов областей экрана.

**Ключевые возможности**:
- Захват областей экрана
- Поддержка многомониторных систем
- Создание отладочной информации
- Автоматическое сохранение в файлы

### 7. [JForexWindowsManagerService](JForexWindowsManagerService.md)
**Назначение**: Сервис для управления окнами JForex и отправки команд в binary-сокет.

**Ключевые возможности**:
- Интеграция с binary-брокерами
- Отправка команд установки риска/длительности
- Поддержка PocketOption, Binarium, Quotex
- Асинхронные операции

### 8. [BinaryOptionsSocketService](BinaryOptionsSocketService.md)
**Назначение**: Сервис для отправки торговых команд в binary-сокет.

**Ключевые возможности**:
- Отправка команд покупки/продажи
- Интеграция с binary-брокерами
- JSON протокол команд
- Автоматическое подключение

### 9. [CaptureTrackingService](CaptureTrackingService.md)
**Назначение**: Сервис для отслеживания и автоматической торговли на основе захваченных паттернов.

**Ключевые возможности**:
- Периодический мониторинг захватов
- Сравнение паттернов
- Автоматическое выполнение торговых операций
- Интеграция с различными брокерами

### 10. [WindowManagementService](WindowManagementService.md)
**Назначение**: Сервис для управления окнами приложения.

**Ключевые возможности**:
- Создание и управление окнами
- Потокобезопасная работа с окнами
- Автоматическое позиционирование
- Управление жизненным циклом окон

### 11. [ToastNotifyService](ToastNotifyService.md)
**Назначение**: Сервис для отображения уведомлений в виде toast-сообщений.

**Ключевые возможности**:
- Различные типы уведомлений (Info, Warning, Error, Success)
- Очередь уведомлений
- Анимации появления/исчезновения
- Автоматическое позиционирование

### 12. [Logger](Logger.md)
**Назначение**: Сервис для логирования событий приложения.

**Ключевые возможности**:
- Различные уровни логирования
- Потокобезопасная запись
- Автоматическое форматирование
- Управление файлами логов

## Взаимодействие сервисов

### Диаграмма зависимостей
```
MainWindow
├── WindowManagementService
├── HotkeysService
└── ToastNotifyService

CaptureService
├── DatabaseService
├── ScreenshotService
├── BrokerState
├── DurationState
└── MainHelper

CaptureTrackingService
├── DatabaseService
├── BinaryOptionsSocketService
├── Mt4SocketService
└── ScreenshotService

TradingToolbar
├── ToolbarSettingsManager
├── JForexWindowsManagerService
└── BinaryOptionsSocketService

CanvasWindow
├── CaptureService
├── ToolbarSettingsManager
└── ScreenshotService

ScreenCaptureOverlay
├── CaptureService
└── ToolbarSettingsManager

SimpleTradingOverlay
├── ToolbarSettingsManager
└── TradingToolbar
```

### Основные потоки данных

#### Захват паттерна
1. **CaptureService** → захват области экрана
2. **ScreenshotService** → создание скриншота
3. **DatabaseService** → сохранение CaptureData
4. **ToastNotifyService** → уведомление о захвате

#### Автоматическая торговля
1. **CaptureTrackingService** → мониторинг паттернов
2. **ScreenshotService** → сравнение с текущим состоянием
3. **BinaryOptionsSocketService** → отправка торговой команды
4. **ToastNotifyService** → уведомление о торговле

#### Управление настройками
1. **TradingToolbar** → изменение настроек
2. **ToolbarSettingsManager** → сохранение настроек
3. **JForexWindowsManagerService** → отправка команд в binary-сокет

## Конфигурация сервисов

### Настройки по умолчанию
```csharp
// Порт для MT4 сокета
private const int DEFAULT_PORT = 5555;

// Порт для binary сокета
private const int BINARY_SOCKET_PORT = 5556;

// Интервал отслеживания паттернов
private const int TRACKING_INTERVAL_MS = 1000;

// Время отображения toast уведомлений
private const int TOAST_DISPLAY_TIME_MS = 3000;

// Порог схожести паттернов
private const double SIMILARITY_THRESHOLD = 0.85;
```

### Настройка уровней логирования
```csharp
// В App.xaml.cs
Logger.Initialize("app.log", LogLevel.Info);
```

## Обработка ошибок

### Общие принципы
1. **Логирование**: Все ошибки логируются через Logger
2. **Уведомления**: Критические ошибки отображаются через ToastNotifyService
3. **Восстановление**: Сервисы пытаются восстановиться после ошибок
4. **Graceful degradation**: Приложение продолжает работать при частичных сбоях

### Примеры обработки ошибок
```csharp
try
{
    // Операция сервиса
    await service.PerformOperation();
}
catch (Exception ex)
{
    Logger.LogError("Operation failed", ex);
    toastNotifyService.ShowError("Operation failed");
}
```

## Производительность

### Оптимизации
1. **Singleton сервисы**: Переиспользование экземпляров
2. **Асинхронные операции**: Неблокирующая обработка
3. **Кэширование**: Кэширование часто используемых данных
4. **Ленивая инициализация**: Инициализация по требованию

### Мониторинг
1. **Логирование производительности**: Время выполнения операций
2. **Использование памяти**: Контроль использования ресурсов
3. **Сетевые операции**: Мониторинг сетевых запросов

## Безопасность

### Принципы безопасности
1. **Валидация данных**: Проверка входных параметров
2. **Обработка исключений**: Корректная обработка ошибок
3. **Логирование безопасности**: Логирование подозрительных операций
4. **Ограничение доступа**: Проверка прав на операции

### Сетевая безопасность
1. **Таймауты**: Установка таймаутов для сетевых операций
2. **Валидация соединений**: Проверка корректности соединений
3. **Обработка разрывов**: Автоматическое переподключение

## Тестирование

### Подходы к тестированию
1. **Unit тесты**: Тестирование отдельных методов сервисов
2. **Integration тесты**: Тестирование взаимодействия сервисов
3. **Mock объекты**: Использование моков для изоляции тестов
4. **End-to-end тесты**: Тестирование полных сценариев

### Примеры тестов
```csharp
[Test]
public void CaptureService_ShouldCreateCaptureData()
{
    // Arrange
    var captureService = new CaptureService();
    
    // Act
    var result = captureService.CaptureAreaWithSymbolAndRisk(...);
    
    // Assert
    Assert.IsNotNull(result);
}
```

## Документация

Каждый сервис имеет подробную документацию в отдельном файле:
- Описание назначения и функциональности
- Детальное описание методов и параметров
- Примеры использования
- Интеграция с другими компонентами
- Особенности реализации и производительности 