# HotkeysService

## Описание
`HotkeysService` - сервис для глобального перехвата клавиш. Обеспечивает обработку горячих клавиш на уровне системы и генерацию событий для UI и других сервисов.

## Зависимости
- WinAPI функции для глобального хука клавиатуры
- `System.Runtime.InteropServices` - для импорта WinAPI функций

## Основные поля

### Приватные поля
```csharp
private IntPtr keyboardHook = IntPtr.Zero;
private HwndSource source;
private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
private LowLevelKeyboardProc keyboardProc;
private bool isEnabled = false;
```

### Константы
```csharp
private const int WH_KEYBOARD_LL = 13;
private const int WM_KEYDOWN = 0x0100;
private const int WM_KEYUP = 0x0101;
private const int WM_SYSKEYDOWN = 0x0104;
private const int WM_SYSKEYUP = 0x0105;
```

### Коды клавиш
```csharp
private const int VK_SPACE = 0x20;
private const int VK_ESCAPE = 0x1B;
private const int VK_T = 0x54;
private const int VK_ENTER = 0x0D;
private const int VK_A = 0x41;
private const int VK_S = 0x53;
private const int VK_D = 0x44;
private const int VK_W = 0x57;
private const int VK_C = 0x43;
private const int VK_R = 0x52;
private const int VK_F = 0x46;
private const int VK_G = 0x47;
private const int VK_H = 0x48;
private const int VK_J = 0x4A;
private const int VK_K = 0x4B;
private const int VK_L = 0x4C;
private const int VK_Z = 0x5A;
private const int VK_X = 0x58;
private const int VK_V = 0x56;
private const int VK_B = 0x42;
private const int VK_N = 0x4E;
private const int VK_M = 0x4D;
private const int VK_Q = 0x51;
private const int VK_E = 0x45;
private const int VK_Y = 0x59;
private const int VK_U = 0x55;
private const int VK_I = 0x49;
private const int VK_O = 0x4F;
private const int VK_P = 0x50;
private const int VK_1 = 0x31;
private const int VK_2 = 0x32;
private const int VK_3 = 0x33;
private const int VK_4 = 0x34;
private const int VK_5 = 0x35;
private const int VK_6 = 0x36;
private const int VK_7 = 0x37;
private const int VK_8 = 0x38;
private const int VK_9 = 0x39;
private const int VK_0 = 0x30;
```

## События

```csharp
public event Action OnSpaceKeyPressed;
public event Action OnEscapeKeyPressed;
public event Action OnTKeyPressed;
public event Action OnEnterKeyPressed;
public event Action OnAKeyPressed;
public event Action OnSKeyPressed;
public event Action OnDKeyPressed;
public event Action OnWKeyPressed;
public event Action OnCKeyPressed;
public event Action OnRKeyPressed;
public event Action OnFKeyPressed;
public event Action OnGKeyPressed;
public event Action OnHKeyPressed;
public event Action OnJKeyPressed;
public event Action OnKKeyPressed;
public event Action OnLKeyPressed;
public event Action OnZKeyPressed;
public event Action OnXKeyPressed;
public event Action OnVKeyPressed;
public event Action OnBKeyPressed;
public event Action OnNKeyPressed;
public event Action OnMKeyPressed;
public event Action OnQKeyPressed;
public event Action OnEKeyPressed;
public event Action OnYKeyPressed;
public event Action OnUKeyPressed;
public event Action OnIKeyPressed;
public event Action OnOKeyPressed;
public event Action OnPKeyPressed;
public event Action<int> OnNumberKeyPressed;
```

## Основные методы

### Enable
```csharp
public void Enable()
```

**Назначение**: Включает глобальный хук клавиатуры.

**Логика работы**:
1. Проверяет, не включен ли уже хук
2. Создает делегат для callback
3. Устанавливает глобальный хук через WinAPI
4. Устанавливает флаг `isEnabled = true`

### Disable
```csharp
public void Disable()
```

**Назначение**: Отключает глобальный хук клавиатуры.

**Логика работы**:
1. Проверяет, включен ли хук
2. Удаляет хук через WinAPI
3. Сбрасывает handle хука
4. Устанавливает флаг `isEnabled = false`

### Dispose
```csharp
public void Dispose()
```

**Назначение**: Освобождает ресурсы хука клавиатуры.

**Логика работы**:
1. Отключает хук
2. Освобождает ресурсы

### KeyboardHookCallback
```csharp
private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
```

**Назначение**: Callback для глобального хука клавиатуры.

**Параметры**:
- `nCode` - код действия
- `wParam` - параметр сообщения (код клавиши)
- `lParam` - дополнительный параметр

**Логика работы**:
1. Проверяет код действия
2. Обрабатывает события нажатия клавиш
3. Определяет код клавиши
4. Вызывает соответствующие события
5. Передает управление следующему хуку

## WinAPI импорты

```csharp
[DllImport("user32.dll")]
private static extern IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

[DllImport("user32.dll")]
private static extern bool UnhookWindowsHookEx(IntPtr hhk);

[DllImport("user32.dll")]
private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

[DllImport("kernel32.dll")]
private static extern IntPtr GetModuleHandle(string lpModuleName);
```

## Обработка клавиш

### Основные клавиши
- **Space** - `OnSpaceKeyPressed` - захват области экрана
- **Escape** - `OnEscapeKeyPressed` - отмена операции
- **T** - `OnTKeyPressed` - переключение режима трейдинга
- **Enter** - `OnEnterKeyPressed` - подтверждение операции
- **A** - `OnAKeyPressed` - активация захвата

### Навигационные клавиши
- **W/S** - `OnWKeyPressed`/`OnSKeyPressed` - перемещение по вертикали
- **A/D** - `OnAKeyPressed`/`OnDKeyPressed` - перемещение по горизонтали
- **Q/E** - `OnQKeyPressed`/`OnEKeyPressed` - поворот/масштабирование

### Функциональные клавиши
- **C** - `OnCKeyPressed` - копирование/очистка
- **R** - `OnRKeyPressed` - сброс/обновление
- **F** - `OnFKeyPressed` - поиск/фильтрация
- **Z** - `OnZKeyPressed` - отмена действия

### Цифровые клавиши
- **0-9** - `OnNumberKeyPressed` - выбор значения риска/длительности

## Взаимодействие с другими компонентами

### Используется в
- **MainWindow**: для обработки глобальных хоткеев
- **ScreenCaptureOverlay**: для управления захватом
- **CanvasWindow**: для навигации и управления
- **SimpleTradingOverlay**: для активации функций

### Использует
- **WinAPI**: для глобального хука клавиатуры

## Особенности реализации

1. **Глобальный хук**: Перехватывает клавиши на уровне системы
2. **Множественные события**: Поддерживает множество событий для разных клавиш
3. **Цифровые клавиши**: Передает код клавиши в событие `OnNumberKeyPressed`
4. **Безопасность**: Корректно освобождает ресурсы при отключении
5. **Производительность**: Минимальное влияние на производительность системы

## Примеры использования

```csharp
// Включение сервиса
hotkeysService.Enable();

// Подписка на события
hotkeysService.OnSpaceKeyPressed += () => Console.WriteLine("Space pressed");
hotkeysService.OnEscapeKeyPressed += () => Console.WriteLine("Escape pressed");
hotkeysService.OnNumberKeyPressed += (keyCode) => Console.WriteLine($"Number {keyCode} pressed");

// Отключение сервиса
hotkeysService.Disable();

// Освобождение ресурсов
hotkeysService.Dispose();
```

## События и их назначение

### Основные события
- `OnSpaceKeyPressed` - активация захвата области экрана
- `OnEscapeKeyPressed` - отмена текущей операции
- `OnTKeyPressed` - переключение в режим трейдинга
- `OnEnterKeyPressed` - подтверждение операции

### Навигационные события
- `OnWKeyPressed` - перемещение вверх
- `OnSKeyPressed` - перемещение вниз
- `OnAKeyPressed` - перемещение влево
- `OnDKeyPressed` - перемещение вправо

### Функциональные события
- `OnCKeyPressed` - копирование или очистка
- `OnRKeyPressed` - сброс или обновление
- `OnFKeyPressed` - поиск или фильтрация
- `OnZKeyPressed` - отмена действия

### Цифровые события
- `OnNumberKeyPressed` - выбор числового значения (передает код клавиши)

## Безопасность и производительность

1. **Минимальное влияние**: Хук обрабатывает только необходимые клавиши
2. **Быстрая обработка**: Минимальная задержка в обработке событий
3. **Корректное освобождение**: Все ресурсы освобождаются при отключении
4. **Обработка ошибок**: Корректная обработка ошибок установки/удаления хука 