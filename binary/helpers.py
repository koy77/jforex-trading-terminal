from typing import Tuple
from pynput.mouse import Controller as MouseController, Button
from pynput.keyboard import Controller as KeyboardController, Key
import random
import time
import win32gui
import win32con
import win32api
import win32ui
from PIL import Image

class HumanMouse:
    def __init__(self):
        self.mouse = MouseController()

    def _human_move(self, start: Tuple[int, int], end: Tuple[int, int], duration: float = 0.5):
        """Move mouse from start to end in a human-like curve."""
        steps = int(duration * 100)
        for i in range(steps):
            t = i / steps
            # Bezier curve for human-like movement
            x = int(start[0] + (end[0] - start[0]) * t + random.uniform(-2, 2))
            y = int(start[1] + (end[1] - start[1]) * t + random.uniform(-2, 2))
            self.mouse.position = (x, y)
            time.sleep(duration / steps)
        self.mouse.position = end

    def move_to(self, x: int, y: int, duration: float = 0.25):
        start = self.mouse.position
        self._human_move(start, (x, y), duration)

    def click(self, x: int, y: int, button=Button.left, duration: float = 0.25):
        self.move_to(x, y, duration)
        # time.sleep(random.uniform(0.05, 0.1))
        self.mouse.click(button)

    def click_area(self, area: Tuple[int, int, int, int]):
        """Click a random point inside the given area (x1, y1, x2, y2)."""
        x = random.randint(area[0], area[2])
        y = random.randint(area[1], area[3])
        self.click(x, y)

    def move_mouse_to_area(self, area: Tuple[int, int, int, int]):
        """Move mouse to a random point inside the area."""
        x = random.randint(area[0], area[2])
        y = random.randint(area[1], area[3])
        self.move_to(x, y)

class HumanKeyboard:
    def __init__(self):
        self.keyboard = KeyboardController()

    def keyboard_press_key_sleep(self):
        """Add a small random delay between keyboard actions."""
        time.sleep(random.uniform(0.05, 0.15))

    def send_keys(self, text: str):
        """Type the given text as keyboard input with human-like timing."""
        for char in text:
            self.keyboard.press(char)
            self.keyboard.release(char)
            self.keyboard_press_key_sleep()

    def press_key(self, key):
        """Press and release a specific key."""
        self.keyboard.press(key)
        self.keyboard.release(key)
        self.keyboard_press_key_sleep()

    def press_backspace(self, count: int = 1):
        """Press backspace key specified number of times."""
        for _ in range(count):
            self.press_key(Key.backspace)

def find_browser_window(target_title="The Most Innovative Trading Platform"):
    """
    Finds the browser window by title and returns its handle.
    """
    result = []
    def enum_windows_callback(hwnd, _):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if target_title in title:
                result.append(hwnd)
    win32gui.EnumWindows(enum_windows_callback, None)
    if result:
        return result[0]
    else:
        raise Exception(f"Browser window not found. Make sure a browser tab with title containing '{target_title}' is open.")

def set_window_size(window_handle, log_queue=None):
    """Maximize the browser window, then set its height to 600px while keeping full screen width."""
    screen_width = win32api.GetSystemMetrics(0)  # SM_CXSCREEN
    window_width = screen_width
    window_height = 600
    x = 0
    y = 0
    if log_queue:
        log_queue.put(f"Maximizing window, then setting size to {window_width}x{window_height} at position ({x}, {y})")
    time.sleep(1)
    win32gui.SetWindowPos(
        window_handle,
        win32con.HWND_TOP,
        x, y, window_width, window_height,
        win32con.SWP_SHOWWINDOW
    )
    if log_queue:
        log_queue.put("Window maximized and height set to 600px successfully")

def screenshot_window(window_handle):
    """Takes a screenshot of the browser window and returns a PIL Image."""
    hwnd = window_handle
    left, top, right, bot = win32gui.GetWindowRect(hwnd)
    width = right - left
    height = bot - top
    hwndDC = win32gui.GetWindowDC(hwnd)
    mfcDC  = win32ui.CreateDCFromHandle(hwndDC)
    saveDC = mfcDC.CreateCompatibleDC()
    saveBitMap = win32ui.CreateBitmap()
    saveBitMap.CreateCompatibleBitmap(mfcDC, width, height)
    saveDC.SelectObject(saveBitMap)
    win32gui.SetForegroundWindow(hwnd)
    saveDC.BitBlt((0, 0), (width, height), mfcDC, (0, 0), win32con.SRCCOPY)
    bmpinfo = saveBitMap.GetInfo()
    bmpstr = saveBitMap.GetBitmapBits(True)
    img = Image.frombuffer(
        'RGB',
        (bmpinfo['bmWidth'], bmpinfo['bmHeight']),
        bmpstr, 'raw', 'BGRX', 0, 1)
    win32gui.DeleteObject(saveBitMap.GetHandle())
    saveDC.DeleteDC()
    mfcDC.DeleteDC()
    win32gui.ReleaseDC(hwnd, hwndDC)
    return img
