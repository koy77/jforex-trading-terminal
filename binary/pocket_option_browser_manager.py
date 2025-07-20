import random
import time
from typing import Tuple

import pyautogui
import math
import win32gui
import win32con
import win32ui
from PIL import Image
import os
import win32api
from helpers import find_browser_window, set_window_size, screenshot_window, HumanMouse, HumanKeyboard

class BrowserManagerService:
    def __init__(self, log_queue=None):
        self.log_queue = log_queue
        if self.log_queue:
            self.log_queue.put("Initializing Browser Manager Service...")
        self.mouse = HumanMouse()
        self.keyboard = HumanKeyboard()
        if self.log_queue:
            self.log_queue.put("Searching for browser window...")
        self.window_handle = find_browser_window()
        
        if self.log_queue:
            self.log_queue.put(f"Browser window found with handle: {self.window_handle}")
        
        # Set window size to full screen width and 600px height
        set_window_size(self.window_handle, self.log_queue)
        
        bridge_folder = os.path.dirname(os.path.abspath(__file__))
        temp_folder = os.path.join(bridge_folder, "temp")

        if self.log_queue:
            self.log_queue.put("Browser manager initialized successfully.")

    def open_symbol_tab(self, index: int):
        """Open a symbol in the browser by index (0..N)."""
        area = self.get_symbol_area(index)
        self.mouse.click_area(area)
        self.mouse.click_area(area)
        

    def set_risk(self, risk: str):
        area = (1351, 320, 1412, 321)
        self.mouse.click_area(area)
        self.keyboard.press_backspace(2)
        self.keyboard.send_keys(str(risk))
        

    def set_duration(self, duration: str):
        area = (1337, 242, 1423, 264)  # Примерные координаты для duration, скорректируйте при необходимости
        self.mouse.click_area(area)
        self.keyboard.keyboard_press_key_sleep()

        # Add logic for specific duration values
        if duration == "1":
            self.mouse.click_area((1132, 351, 1169, 371))
        elif duration == "2":
            self.mouse.click_area((1132, 351, 1169, 371))
        elif duration == "3":
            self.mouse.click_area((1188, 351, 1226, 371))
        elif duration == "4":
            self.mouse.click_area((1244, 390, 1282, 371))
        elif duration == "5":
            self.mouse.click_area((1244, 351, 1282, 371))
        else:
            self.keyboard.send_keys(str(duration))
        self.keyboard.keyboard_press_key_sleep()

    def get_symbol_area(self, index: int):
        base_x1 = 55
        base_x2 = 203
        y1 = 173
        y2 = 197
        step = 172
        x1 = base_x1 + index * step
        x2 = base_x2 + index * step
        return (x1, y1, x2, y2)

    def activate_window(self):
        win32gui.SetForegroundWindow(self.window_handle)
        # win32gui.SetFocus(self.window_handle)

    def buy(self, symbol: int, risk: str, duration: str):
        """
        Simulate a buy action on the trading platform.
        This should select the symbol, set risk and duration, and click the BUY button.
        """
        if self.log_queue:
            self.log_queue.put(f"🟢 Executing BUY order: {symbol} | Risk: {risk} | Duration: {duration}")
        
        self.activate_window()

        BUY_BUTTON_AREA = (1337, 426, 1463, 464)
        # 1. open symbol
        self.open_symbol_tab(symbol)
        # 2. set risk
        self.set_risk(risk)
        # 3. set duration
        self.set_duration(duration)
        # 4. Click BUY button
        if self.log_queue:
            self.log_queue.put("Clicking BUY button")
        self.mouse.click_area(BUY_BUTTON_AREA)
        
        if self.log_queue:
            self.log_queue.put(f"✅ BUY order completed for {symbol}")

    def sell(self, symbol: int, risk: str, duration: str):
        """
        Simulate a sell action on the trading platform.
        This should select the symbol, set risk and duration, and click the SELL button.
        """
        if self.log_queue:
            self.log_queue.put(f"🔴 Executing SELL order: {symbol} | Risk: {risk} | Duration: {duration}")
        
        self.activate_window()

        SELL_BUTTON_AREA = (1335, 481, 1457, 516) 
        # 1. open symbol
        self.open_symbol_tab(symbol)
        # 2. set risk
        self.set_risk(risk)
        # 3. set duration
        self.set_duration(duration)
        # 4. Click SELL button
        if self.log_queue:
            self.log_queue.put("Clicking SELL button")
        self.mouse.click_area(SELL_BUTTON_AREA)
        time.sleep(0.2)
        if self.log_queue:
            self.log_queue.put(f"✅ SELL order completed for {symbol}")



# Пример использования:
# from server_po_browser_manager import BrowserManagerService
# browser_manager = BrowserManagerService()
# browser_manager.click_area((x1, y1, x2, y2))  # On buy command

# Required packages:
# pip install pywin32 pillow pynput pyautogui opencv-python pytesseract