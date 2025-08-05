# Trading Toolbar Values Swap

## Описание изменений

Поменяны местами значения Lots и Percent в блоке сделки Trading Toolbar для правильного отображения.

## Проблема

В блоке сделки значения отображались неправильно:
- Проценты показывались там, где должны быть лоты
- Лоты показывались там, где должны быть проценты

## Изменения в файлах

### Controls/TradingToolbar.xaml.cs
**Метод `UpdateOrderSummaryUI`:**
- `OrderLotsText.Text = $"%: {symbolInfo.Percent:F2}";` (показывает % в месте Lots)
- `OrderPercentText.Text = $"Lots: {symbolInfo.Lots:F2}";` (показывает Lots в месте %)

### Controls/OrderSymbol.xaml
**Изменена структура UI:**
- **Первая строка**: Symbol + PercentText + Close button (было Symbol + LotsText + Close)
- **Вторая строка**: LotsText + PointsText + BE/TP buttons (было PercentText + PointsText + BE/TP)

### Controls/OrderSymbol.xaml.cs
**Восстановлены правильные форматы отображения:**
- `Lots` свойство: `"Lots: {value:F2}"`
- `Percent` свойство: `"%: {value:F2}"`

## Результат

Теперь в блоке сделки:
- **Первая строка**: Символ + % значение + кнопка Close
- **Вторая строка**: Лоты + Поинты + кнопки BE/TP1/TP2/TP3

## Тестирование

Для проверки изменений:
1. Запустите приложение
2. Откройте Trading Toolbar
3. Убедитесь, что в блоке сделки:
   - В первой строке показывается символ и процентное значение
   - Во второй строке показывается значение лотов
   - Все остальные элементы остались на своих местах 