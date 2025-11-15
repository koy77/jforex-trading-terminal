using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenCaptureApp.Services
{
    public class HotkeysService : IDisposable
    {
        // Global keyboard hook variables
        private IntPtr keyboardHookId = IntPtr.Zero;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_SPACE = 0x20;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_F = 0x46; // Клавиша F
        private const int VK_ENTER = 0x0D;
        private const int VK_OEM_3 = 0xC0; // '`' (backquote)
        private const int VK_C = 0x43;
        private const int VK_W = 0x57;
        private const int VK_A = 0x41;
        private const int VK_S = 0x53;
        private const int VK_D = 0x44;

        // Events
        public event Action OnSpaceKeyPressed;
        public event Action OnEscapeKeyPressed;
        public event Action OnBackQuoteKeyPressed;
        public event Action OnCKeyPressed;
        public event Action OnWKeyPressed;
        public event Action OnAKeyPressed;
        public event Action OnDKeyPressed;
        public event Action OnFKeyPressed;
        // Symbol hotkey events (1-6)
        public event Action OnSymbolHotkeyPressed1;
        public event Action OnSymbolHotkeyPressed2;
        public event Action OnSymbolHotkeyPressed3;
        public event Action OnSymbolHotkeyPressed4;
        public event Action OnSymbolHotkeyPressed5;
        public event Action OnSymbolHotkeyPressed6;
        public event Action OnQHotkey;
        public event Action OnWHotkey;
        public event Action OnEHotkey;
        public event Action OnRHotkey;
        public event Action OnJHotkey; // JForex integration toggle
        public event Action OnLeftShiftHotkey;
        public event Action OnBKeyPressed; // Buy trade pattern
        public event Action OnSKeyPressed; // Sell trade pattern (replaces old OnSKeyPressed)
        // Properties
        public bool IsEnabled { get; private set; } = false;
        
        // Import Windows API functions
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int VK_1 = 0x31;
        private const int VK_2 = 0x32;
        private const int VK_3 = 0x33;
        private const int VK_4 = 0x34;
        private const int VK_5 = 0x35;
        private const int VK_6 = 0x36;
        private const int VK_NUMPAD1 = 0x61;
        private const int VK_NUMPAD2 = 0x62;
        private const int VK_NUMPAD3 = 0x63;
        private const int VK_NUMPAD4 = 0x64;
        private const int VK_NUMPAD5 = 0x65;
        private const int VK_NUMPAD6 = 0x66;
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_Q = 0x51;
        private const int VK_E = 0x45;
        private const int VK_R = 0x52;
        private const int VK_Z = 0x5A; // Клавиша Z
        private const int VK_J = 0x4A; // Клавиша J
        private const int VK_LSHIFT = 0xA0;
        private const int VK_B = 0x42; // Клавиша B

        // Delegate for the keyboard hook
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc keyboardProc;

        public HotkeysService()
        {
            InitializeKeyboardHook();
        }

        private void InitializeKeyboardHook()
        {
            keyboardProc = KeyboardHookCallback;
            keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, GetModuleHandle("user32"), 0);
            
            if (keyboardHookId == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to set keyboard hook");
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                if (vkCode == VK_SPACE && IsEnabled)
                {
                    // Trigger the space key event
                    Logger.LogDebug("Space key detected and service is enabled");
                    OnSpaceKeyPressed?.Invoke();
                    
                    // return (IntPtr)1; // Prevent further processing
                }
                else if (vkCode == VK_ESCAPE && IsEnabled)
                {
                    // Trigger the escape key event
                    Logger.LogDebug("Escape key detected and service is enabled");
                    OnEscapeKeyPressed?.Invoke();
                    
                    // return (IntPtr)1; // Prevent further processing
                }
                else if (vkCode == VK_F && IsEnabled)
                {
                    // Trigger the F key event
                    Logger.LogDebug("F key detected and service is enabled");
                    OnFKeyPressed?.Invoke();
                }
                // else if (vkCode == VK_ENTER && IsEnabled)
                // {
                //     Logger.LogDebug("Enter key detected and service is enabled");
                //     OnEnterKeyPressed?.Invoke();
                //     return (IntPtr)1;
                // }
                // Symbol hotkeys 1-6 (top row and numpad)
                else if (IsEnabled && (vkCode == VK_1 || vkCode == VK_NUMPAD1))
                {
                    Logger.LogDebug("Symbol hotkey 1 pressed");
                    OnSymbolHotkeyPressed1?.Invoke();
                }
                else if (IsEnabled && (vkCode == VK_2 || vkCode == VK_NUMPAD2))
                {
                    Logger.LogDebug("Symbol hotkey 2 pressed");
                    OnSymbolHotkeyPressed2?.Invoke();
                }
                else if (IsEnabled && (vkCode == VK_3 || vkCode == VK_NUMPAD3))
                {
                    Logger.LogDebug("Symbol hotkey 3 pressed");
                    OnSymbolHotkeyPressed3?.Invoke();
                }
                else if (IsEnabled && (vkCode == VK_4 || vkCode == VK_NUMPAD4))
                {
                    Logger.LogDebug("Symbol hotkey 4 pressed");
                    OnSymbolHotkeyPressed4?.Invoke();
                }
                else if (IsEnabled && (vkCode == VK_5 || vkCode == VK_NUMPAD5))
                {
                    Logger.LogDebug("Symbol hotkey 5 pressed");
                    OnSymbolHotkeyPressed5?.Invoke();
                }
                else if (IsEnabled && (vkCode == VK_6 || vkCode == VK_NUMPAD6))
                {
                    Logger.LogDebug("Symbol hotkey 6 pressed");
                    OnSymbolHotkeyPressed6?.Invoke();
                }
                else if (vkCode == VK_ESCAPE && !IsEnabled)
                {
                    Logger.LogDebug("Escape key detected but service is disabled");
                }
                else if (vkCode == VK_SPACE && !IsEnabled)
                {
                    Logger.LogDebug("Space key detected but service is disabled");
                }
                else if (vkCode == VK_OEM_3 && IsEnabled)
                {
                    Logger.LogDebug("BackQuote (`) key detected and service is enabled");
                    OnBackQuoteKeyPressed?.Invoke();
                }
                else if (vkCode == VK_C && IsEnabled)
                {
                    Logger.LogDebug("C key detected and service is enabled");
                    OnCKeyPressed?.Invoke();
                }
                else if (vkCode == VK_W && IsEnabled)
                {
                    // W key detected, reserved for future use
                    OnWHotkey?.Invoke();
                }
                else if (vkCode == VK_A && IsEnabled)
                {
                    Logger.LogDebug("A key detected and service is enabled");
                    OnAKeyPressed?.Invoke();
                }
                else if (vkCode == VK_D && IsEnabled)
                {
                    Logger.LogDebug("D key detected and service is enabled");
                    // OnDKeyPressed?.Invoke();
                }

                else if (vkCode == VK_Q && IsEnabled)
                {
                    Logger.LogDebug("Q hotkey detected and service is enabled");
                    OnQHotkey?.Invoke();
                }
                else if (vkCode == VK_E && IsEnabled)
                {
                    Logger.LogDebug("E hotkey detected and service is enabled");
                    OnEHotkey?.Invoke();
                }
                else if (vkCode == VK_R && IsEnabled)
                {
                    Logger.LogDebug("R hotkey detected and service is enabled");
                    OnRHotkey?.Invoke();
                }
                else if (vkCode == VK_J && IsEnabled)
                {
                    Logger.LogDebug("J hotkey detected and service is enabled");
                    // OnJHotkey?.Invoke();
                }
                else if (vkCode == VK_Z && IsEnabled)
                {
                    Logger.LogDebug("Z hotkey detected and service is enabled");
                    OnLeftShiftHotkey?.Invoke();
                }
                else if (vkCode == VK_LEFT && IsEnabled)
                {
                    Logger.LogDebug("Left Arrow key detected and service is enabled");
                    OnAKeyPressed?.Invoke();
                }
                else if (vkCode == VK_UP && IsEnabled)
                {
                    Logger.LogDebug("Up Arrow key detected and service is enabled");
                    OnWKeyPressed?.Invoke();
                }
                else if (vkCode == VK_DOWN && IsEnabled)
                {
                    Logger.LogDebug("Down Arrow key detected and service is enabled");
                    // Removed OnSKeyPressed invocation to avoid conflict with S key
                }
                else if (vkCode == VK_RIGHT && IsEnabled)
                {
                    Logger.LogDebug("Right Arrow key detected and service is enabled");
                    OnDKeyPressed?.Invoke();
                }
                else if (vkCode == VK_B)
                {
                    // B key works regardless of IsEnabled state
                    Logger.LogDebug("B key detected (trade pattern)");
                    OnBKeyPressed?.Invoke();
                }
                else if (vkCode == VK_S)
                {
                    // S key works regardless of IsEnabled state, but check if it's not the old S key handler
                    // The old S key handler is for movement, so we need to distinguish
                    // For now, we'll let both handlers work, but the trade pattern takes precedence
                    Logger.LogDebug("S key detected (trade pattern)");
                    OnSKeyPressed?.Invoke();
                }
            }
            
            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        public void Enable()
        {
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        public void Dispose()
        {
            if (keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookId);
                keyboardHookId = IntPtr.Zero;
            }
        }
    }
} 