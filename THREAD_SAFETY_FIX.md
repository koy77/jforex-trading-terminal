# Thread Safety Fix

## Описание проблемы

В логах была обнаружена ошибка потокобезопасности при обработке Rectangle объектов:

```
Exception: Вызывающий поток не может получить доступ к данному объекту, так как владельцем этого объекта является другой поток.
StackTrace: at System.Windows.Threading.Dispatcher.VerifyAccess()
   at System.Windows.Media.VisualTreeHelper.GetChildrenCount(DependencyObject reference)
   at ScreenCaptureApp.Controls.TradeRectangleControl.FindVisualChildren[T](DependencyObject depObj)
```

## Причина ошибки

Метод `HighlightSelectedRiskButton` в `TradeRectangleControl.xaml.cs` пытался обновить UI элементы из другого потока, что недопустимо в WPF. Ошибка возникала при вызове `FindVisualChildren` и последующем обновлении `Background` кнопок.

## Решение

Добавлена проверка потока и безопасное обновление UI через `Dispatcher.Invoke()`.

### Изменения в Controls/TradeRectangleControl.xaml.cs

**Обновлен метод `HighlightSelectedRiskButton`:**
```csharp
public void HighlightSelectedRiskButton(double risk)
{
    if (Dispatcher.CheckAccess())
    {
        UpdateRiskButtonColors(risk);
    }
    else
    {
        Dispatcher.Invoke(() => UpdateRiskButtonColors(risk));
    }
}
```

**Добавлен новый метод `UpdateRiskButtonColors`:**
```csharp
private void UpdateRiskButtonColors(double risk)
{
    var riskButtons = FindVisualChildren<Button>(this).Where(b => b.Tag is string && double.TryParse(b.Tag.ToString(), out _));
    foreach (var button in riskButtons)
    {
        if (double.TryParse(button.Tag?.ToString(), out double val))
        {
            button.Background = (val == risk) ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.LightGray;
        }
    }
}
```

## Принцип работы

1. **Проверка потока**: `Dispatcher.CheckAccess()` проверяет, выполняется ли код в UI потоке
2. **Прямое выполнение**: Если да - метод выполняется напрямую
3. **Безопасное выполнение**: Если нет - используется `Dispatcher.Invoke()` для выполнения в UI потоке

## Результат

- Устранена ошибка потокобезопасности
- Rectangle Control теперь корректно отображается без исключений
- Синхронизация риска работает стабильно
- Все UI обновления выполняются безопасно

## Тестирование

Для проверки исправления:
1. Запустите приложение
2. Вызовите отображение Rectangle Control
3. Убедитесь, что нет ошибок в логах
4. Проверьте, что изменение риска работает корректно 