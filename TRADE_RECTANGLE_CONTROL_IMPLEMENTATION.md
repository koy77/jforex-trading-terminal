# Trade Rectangle Control - Реализация

## Описание
Реализован новый компонент `TradeRectangleControl` для обработки Rectangle объектов из JForex. Компонент отображается справа по центру целевого окна и содержит две цены (верхнюю и нижнюю) с кнопками Buy и Sell.

## Созданные файлы

### 1. Controls/TradeRectangleControl.xaml
- XAML разметка для нового контрола
- Содержит отображение символа, верхней и нижней цены
- Кнопки Buy (зеленая) и Sell (красная)
- Стилизация в темной теме с прозрачностью

### 2. Controls/TradeRectangleControl.xaml.cs
- Code-behind файл с логикой контрола
- События `BuyClicked` и `SellClicked`
- Методы `SetRectangleData()`, `Show()`, `Hide()`
- Класс `RectangleTradeEventArgs` для передачи данных о сделке

## Интеграция в SimpleTradingOverlay

### 1. SimpleTradingOverlay.xaml
- Добавлен `TradeRectangleControl` в Canvas
- Позиционирование справа с `Panel.ZIndex="1001"`

### 2. SimpleTradingOverlay.xaml.cs
- Обновлен метод `ProcessRectangleObject()` для обработки Rectangle объектов
- Добавлен метод `ParseRectangleData()` для парсинга JSON данных
- Добавлен метод `PositionTradeRectangleControl()` для позиционирования по центру справа
- Обновлен метод `OnEscapeKeyPressed()` для скрытия контрола
- Добавлен метод `SetupTradeRectangleControlEvents()` для обработки событий кнопок

## Функциональность

### Обработка Rectangle объектов
1. При получении Rectangle объекта из JForex парсится JSON с `upperPrice` и `lowerPrice`
2. Данные устанавливаются в `TradeRectangleControl`
3. Контрол показывается справа по центру окна

### Торговые операции
1. При нажатии Buy: Entry = верхняя цена, SL = нижняя цена
2. При нажатии Sell: Entry = нижняя цена, SL = верхняя цена
3. Используется риск из TradingToolbar
4. Команда отправляется в MT4 через `Mt4SocketService`
5. Показывается toast уведомление
6. Контрол скрывается после отправки команды

### Управление видимостью
- Показывается при получении Rectangle объекта
- Скрывается при нажатии Escape
- Скрывается после отправки торговой команды

## Позиционирование
- Контрол позиционируется справа (`Canvas.Right="0"`)
- По вертикали центрируется относительно экрана (не окна)
- Окно SimpleTradingOverlay теперь покрывает весь экран
- Используется фиксированная высота 200px если `ActualHeight` недоступна
- Позиционирование происходит при инициализации и при получении Rectangle объекта

## Логирование
- Все действия логируются с тегом "SimpleTradingOverlay"
- Ошибки парсинга и обработки логируются с деталями
- Успешные торговые операции логируются с параметрами сделки 