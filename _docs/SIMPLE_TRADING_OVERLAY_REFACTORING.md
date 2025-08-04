# Рефакторинг SimpleTradingOverlay

## Цель рефакторинга

Упростить класс `SimpleTradingOverlay`, удалив все связанное со скриншотами и старыми паттернами, оставив только:
1. Обработку JForex объектов (`NewJForexChartObject`)
2. Позиционирование Toolbar между окнами
3. Обработку Escape клавиши

## Удаленные компоненты

### 1. Паттерны трейдинга
- `_isTradingPatternActive` - флаг активности паттерна трейдинга
- `_currentTradingPattern` - данные текущего паттерна
- `_clickCount` - счетчик кликов
- `StartTradingPattern()` - начало паттерна
- `StartInclinedTradingPattern()` - начало наклонного паттерна
- `CancelTradingPattern()` - отмена паттерна
- `CompleteTradingPattern()` - завершение паттерна
- `HandleMouseClickForTradingPattern()` - обработка кликов для паттерна

### 2. Паттерны отложенных ордеров
- `_isPendingOrderPatternActive` - флаг активности паттерна отложенного ордера
- `_currentPendingOrderPattern` - данные текущего паттерна отложенного ордера
- `_pendingOrderClickCount` - счетчик кликов
- `StartPendingOrderPattern()` - начало паттерна
- `CancelPendingOrderPattern()` - отмена паттерна
- `CompletePendingOrderPattern()` - завершение паттерна
- `HandleMouseClickForPendingOrderPattern()` - обработка кликов

### 3. Скриншоты и анализ
- `SaveHorizontalStripScreenshot()` - сохранение горизонтальных полос скриншотов
- `CropHorizontalStrip()` - обрезка изображений
- `AnalyzeAndSendPendingOrder()` - анализ и отправка отложенного ордера
- `CreateMetadataForCapture()` - создание метаданных
- `MergeCanvasWithScreenshot()` - слияние Canvas со скриншотом

### 4. Визуальные элементы
- `_patternOverlay` - окно рамки паттерна
- `ShowTradingPatternRectangle()` - показ рамки паттерна
- `SendEscapeToTargetWindow()` - отправка Escape в целевое окно

### 5. Горячие клавиши (кроме Escape)
- `OnSKeyPressed()` - обработка клавиши S
- `OnAKeyPressed()` - обработка клавиши A
- `OnPKeyPressed()` - обработка клавиши P
- `OnDKeyPressed()` - обработка клавиши D

### 6. Сложная логика состояний
- `_isWaitingForEntryPrice` - флаг ожидания цены входа
- `_isWaitingForStopLossPrice` - флаг ожидания цены стоп-лосса

## Оставленные компоненты

### 1. Обработка JForex объектов
- `HttpServerService_NewJForexChartObject()` - обработчик события JForex объектов
- `ProcessJForexChartObject()` - основная логика обработки
- `ProcessPriceMarkerObject()` - обработка PriceMarker объектов
- `ProcessRectangleObject()` - обработка Rectangle объектов (заглушка)
- `ProcessShortLineObject()` - обработка ShortLine объектов (заглушка)

### 2. Позиционирование Toolbar
- `SetupMouseHook()` - настройка хука мыши
- `MouseHookCallback()` - обработчик событий мыши
- `UpdateOverlayPosition()` - обновление позиции оверлея
- `ApplyToolbarSettings()` - применение настроек тулбара
- `ExtractSymbolFromWindowTitle()` - извлечение символа из заголовка окна

### 3. Упрощенное управление состоянием ценовых уровней
- `_entryPrice` - цена входа
- `_stopLossPrice` - цена стоп-лосса
- `_currentTradeSymbol` - текущий символ сделки
- `ResetPriceLevelState()` - сброс состояния
- `ResetPriceLevelStateToWaitForEntry()` - сброс Entry Price

### 4. Интеграция с MT4
- `SendTradeCommandToMt4()` - отправка команды в MT4

### 5. Обработка Escape
- `OnEscapeKeyPressed()` - обработка клавиши Escape
- `_isServiceEscapeSending` - флаг сервисного нажатия Escape
- `SendDelayedEscape()` - автоматическая отправка Escape с задержкой 200 мс через JForexWindowsManagerService
- `_windowHandle` - глобальный handle окна для использования в других потоках
- `_jforexWindowsManagerService` - сервис для отправки Escape в окна JForex

## Новая упрощенная логика обработки ценовых уровней

### Логика ProcessPriceMarkerObject:
1. **Первый PriceMarker**: Устанавливается как Entry Price
2. **Автоматический Escape**: После установки Entry Price автоматически отправляется Escape через 200 мс
3. **Второй PriceMarker**: Устанавливается как Stop Loss Price
4. **Автоматическая отправка**: Если обе цены установлены, автоматически отправляется команда в MT4
5. **Сброс состояния**: После отправки команды состояние сбрасывается

### Логика Escape:
- **Обнуляет Entry Price**: При нажатии Escape обнуляется только Entry Price
- **Простой сброс**: Нет сложных флагов состояний
- **Автоматическая отправка**: После установки Entry Price автоматически отправляется Escape для сброса состояния

### Преимущества новой логики:
- **Простота**: Нет сложных флагов и состояний
- **Понятность**: Логика очевидна и легко отлаживается
- **Надежность**: Меньше точек отказа
- **Производительность**: Меньше проверок условий
- **Автоматизация**: Автоматический сброс состояния после установки Entry Price
- **Безопасность потоков**: Использование глобального `_windowHandle` и JForexWindowsManagerService для корректной работы с окнами из других потоков

## Решение проблемы с потоками

### Проблема:
- Ошибка "Вызывающий поток не может получить доступ к данному объекту" при попытке получить `WindowInteropHelper(this).Handle` из другого потока

### Решение:
1. **Глобальный handle окна**: Сохранение `_windowHandle` в `OnSourceInitialized()` для использования в других потоках
2. **Использование JForexWindowsManagerService**: Замена собственной реализации отправки Escape на готовый сервис `SendEscKey()`
3. **Безопасная передача параметров**: Передача `_windowHandle` как параметра в `SendDelayedEscape()`

## Улучшения в позиционировании Toolbar

### 1. Уменьшен порог троттлинга
- Изменен с 100 пикселей на 50 пикселей для более отзывчивого переключения

### 2. Улучшено логирование
- Раскомментированы логи для отладки переключения между окнами
- Добавлены подробные логи для отслеживания позиционирования

### 3. Оптимизирована логика переключения
- Убрана обработка кликов мыши (больше не нужна)
- Оставлена только обработка движения мыши для переключения окон

## Исправления в MainWindow

Удалены вызовы несуществующих методов:
- `simpleTradingOverlay.OnAKeyPressed()`
- `simpleTradingOverlay.OnSKeyPressed()`
- `simpleTradingOverlay.OnDKeyPressed()`
- `simpleTradingOverlay.OnPKeyPressed()`

Оставлена только логика для `CanvasWindow`.

## Результат рефакторинга

1. **Упрощенная архитектура**: Класс стал более фокусированным и понятным
2. **Улучшенная производительность**: Убраны тяжелые операции со скриншотами
3. **Лучшая отладка**: Упрощено логирование и отслеживание проблем
4. **Сохранена функциональность**: Оставлены все ключевые функции для работы с JForex
5. **Упрощенная логика состояний**: Убраны сложные флаги, оставлена простая логика

## Тестирование

После рефакторинга необходимо протестировать:
1. Переключение Toolbar между окнами JForex
2. Обработку JForex объектов (PriceMarker, Rectangle, ShortLine)
3. Работу с ценовыми уровнями (Entry/StopLoss) - новую упрощенную логику
4. Обработку клавиши Escape
5. Интеграцию с MT4

## Следующие шаги

1. Реализовать логику для `Rectangle` объектов
2. Реализовать логику для `ShortLine` объектов
3. Добавить дополнительные типы JForex объектов при необходимости
4. Оптимизировать производительность переключения между окнами 