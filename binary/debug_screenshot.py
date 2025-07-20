import win32gui
import win32con
import win32ui
from PIL import Image
import os
import ctypes
from ctypes import wintypes

def find_chrome_window():
    """Find Chrome window with specific title"""
    target_title = "The Most Innovative Trading Platform"
    result = []

    def enum_windows_callback(hwnd, _):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            print(f"Found window: '{title}' - Handle: {hwnd}")
            # Check for Chrome windows
            if "Chrome" in title or "Google" in title:
                print(f"  *** CHROME WINDOW FOUND: '{title}' ***")
            if target_title in title:
                result.append(hwnd)

    win32gui.EnumWindows(enum_windows_callback, None)
    if result:
        return result[0]
    else:
        print("Chrome window not found!")
        return None

def test_screenshot():
    """Test screenshot functionality"""
    print("=== Testing Screenshot Functionality ===")
    
    # Find window
    hwnd = find_chrome_window()
    if not hwnd:
        print("ERROR: Window not found!")
        return
    
    print(f"Window handle: {hwnd}")
    
    # Get window rect
    left, top, right, bot = win32gui.GetWindowRect(hwnd)
    width = right - left
    height = bot - top
    print(f"Window rect: left={left}, top={top}, right={right}, bottom={bot}")
    print(f"Window size: {width}x{height}")
    
    # Check if window is minimized
    if win32gui.IsIconic(hwnd):
        print("WARNING: Window is minimized!")
        return
    
    # Check if window is visible
    if not win32gui.IsWindowVisible(hwnd):
        print("WARNING: Window is not visible!")
        return
    
    try:
        # Get device context
        hwndDC = win32gui.GetWindowDC(hwnd)
        print(f"Got window DC: {hwndDC}")
        
        # Create compatible DC
        mfcDC = win32ui.CreateDCFromHandle(hwndDC)
        print(f"Created compatible DC: {mfcDC}")
        
        # Create compatible bitmap
        saveDC = mfcDC.CreateCompatibleDC()
        saveBitMap = win32ui.CreateBitmap()
        saveBitMap.CreateCompatibleBitmap(mfcDC, width, height)
        saveDC.SelectObject(saveBitMap)
        
        # Bring window to front
        win32gui.SetForegroundWindow(hwnd)
        print("Brought window to front")
        
        # Додаємо функцію PrintWindow через ctypes
        def print_window(hwnd, hdc):
            PW_RENDERFULLCONTENT = 0x00000002  # Для Windows 8.1+ (можна спробувати 0)
            return ctypes.windll.user32.PrintWindow(hwnd, hdc, 0)
        
        # Використати PrintWindow замість BitBlt
        result = print_window(hwnd, saveDC.GetSafeHdc())
        print(f"PrintWindow result: {result}")
        
        # Get bitmap info
        bmpinfo = saveBitMap.GetInfo()
        print(f"Bitmap info: {bmpinfo}")
        
        # Get bitmap bits
        bmpstr = saveBitMap.GetBitmapBits(True)
        print(f"Bitmap bits length: {len(bmpstr)}")
        
        # Create PIL image
        img = Image.frombuffer(
            'RGB',
            (bmpinfo['bmWidth'], bmpinfo['bmHeight']),
            bmpstr, 'raw', 'BGRX', 0, 1)
        
        print(f"Created PIL image: {img.size} {img.mode}")
        
        # Save image
        img.save("debug_screenshot.png")
        print("Saved debug_screenshot.png")
        
        # Cleanup
        win32gui.DeleteObject(saveBitMap.GetHandle())
        saveDC.DeleteDC()
        mfcDC.DeleteDC()
        win32gui.ReleaseDC(hwnd, hwndDC)
        
    except Exception as e:
        print(f"ERROR during screenshot: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    test_screenshot() 