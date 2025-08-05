# Risk Synchronization Implementation

## Описание изменений

Реализована единая система управления риском между Trading Toolbar и Rectangle Control через ToolbarSettingsManager.

## Проблема

Риск в Trading Toolbar и Rectangle Control управлялся независимо, что приводило к несогласованности данных.

## Решение

Создана единая система синхронизации риска через ToolbarSettingsManager с двусторонней связью между компонентами.

## Изменения в файлах

### Controls/TradeRectangleControl.xaml.cs

**Добавлены новые элементы:**
- `public event EventHandler<double> RiskChanged;` - событие изменения риска
- `public void SetRisk(double risk)` - метод для установки риска извне

**Обновлен метод `RiskButton_Click`:**
- Добавлен вызов события `RiskChanged?.Invoke(this, risk);`

### SimpleTradingOverlay.xaml.cs

**Обновлен метод `SetupTradeRectangleControlEvents`:**
- Добавлен обработчик события `RiskChanged` от Rectangle Control
- При изменении риска в Rectangle Control:
  - Обновляется риск в Trading Toolbar
  - Сохраняются настройки в ToolbarSettingsManager

**Обновлен метод `SetupTradingToolbarEvents`:**
- Расширен обработчик события `RiskChanged` от Trading Toolbar
- При изменении риска в Trading Toolbar:
  - Обновляется риск в Rectangle Control (если видим)
  - Сохраняются настройки в ToolbarSettingsManager

**Обновлен метод `ProcessRectangleObject`:**
- Добавлено применение настроек риска из ToolbarSettingsManager при показе Rectangle Control
- Если настройки не найдены, используется риск по умолчанию (1.0)

**Исправлен метод `SetupTradeRectangleControlEvents`:**
- В обработчике `SellClicked` теперь используется риск из Rectangle Control вместо Trading Toolbar

## Поток синхронизации

### 1. Изменение риска в Trading Toolbar
```
Trading Toolbar Risk Changed
    ↓
Update Rectangle Control (if visible)
    ↓
Save to ToolbarSettingsManager
```

### 2. Изменение риска в Rectangle Control
```
Rectangle Control Risk Changed
    ↓
Update Trading Toolbar
    ↓
Save to ToolbarSettingsManager
```

### 3. Показ Rectangle Control
```
Rectangle Control Show
    ↓
Load risk from ToolbarSettingsManager
    ↓
Apply to Rectangle Control UI
```

## Преимущества

1. **Единый источник истины**: Все настройки риска хранятся в ToolbarSettingsManager
2. **Двусторонняя синхронизация**: Изменения в любом компоненте отражаются в другом
3. **Сохранение настроек**: Настройки риска сохраняются между сессиями
4. **Консистентность**: Оба компонента всегда показывают одинаковый риск

## Тестирование

Для проверки синхронизации:
1. Запустите приложение
2. Измените риск в Trading Toolbar - Rectangle Control должен обновиться (если видим)
3. Покажите Rectangle Control - он должен использовать тот же риск, что и Trading Toolbar
4. Измените риск в Rectangle Control - Trading Toolbar должен обновиться
5. Настройки риска должны сохраняться между сессиями 