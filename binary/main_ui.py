import socket
import logging
import json
import threading
import tkinter as tk
from tkinter.scrolledtext import ScrolledText
from tkinter import ttk
from pocket_option_browser_manager import BrowserManagerService
import queue
import os
import glob
import pyautogui
import time
from helpers import HumanMouse  # убедитесь, что импорт есть
from pynput import mouse

# --- Thread-safe log queue ---
log_queue = queue.Queue()

# --- Remove selected files on app start ---
def cleanup_temp_files():
    bridge_folder = os.path.dirname(os.path.abspath(__file__))
    temp_folder = os.path.join(bridge_folder, "temp")
    # Remove po_symbol_block_*.png
    block_files = glob.glob(os.path.join(temp_folder, "po_symbol_block_*.png"))
    for f in block_files:
        try:
            os.remove(f)
            log_queue.put(f"Removed old temp file: {f}")
        except Exception as e:
            log_queue.put(f"Failed to remove {f}: {e}")
    # Remove po_main.png and po_symbols.png
    for fname in ["po_main.png", "po_symbols.png"]:
        fpath = os.path.join(bridge_folder, fname)
        if os.path.exists(fpath):
            try:
                os.remove(fpath)
                log_queue.put(f"Removed old file: {fpath}")
            except Exception as e:
                log_queue.put(f"Failed to remove {fpath}: {e}")

cleanup_temp_files()

# --- Global stop flag for server thread ---
stop_event = threading.Event()

# --- Global stop flag for mouse tracking ---
mouse_tracking_stop_event = threading.Event()

# --- GUI Setup ---
class AppGUI:
    def __init__(self, root, browser_manager):
        self.root = root
        self.root.title("PO Binary Trading API Server with Mouse Tracker")
        self.root.geometry("800x600")
        self.mouse = HumanMouse()  # добавлено
        self.browser_manager = browser_manager
        
        # Create main frame
        main_frame = ttk.Frame(root)
        main_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # Create top frame for mouse coordinates
        top_frame = ttk.Frame(main_frame)
        top_frame.pack(fill=tk.X, pady=(0, 10))
        
        # Mouse coordinates display (debug section)
        coord_frame = ttk.LabelFrame(top_frame, text="debug", padding="10")
        coord_frame.pack(fill=tk.X)
        
        # X coordinate (Entry)
        x_frame = ttk.Frame(coord_frame)
        x_frame.pack(fill=tk.X, pady=(0, 5))
        ttk.Label(x_frame, text="X:", font=("Arial", 10, "bold")).pack(side=tk.LEFT)
        self.x_var = tk.StringVar(value="0")
        self.x_entry = ttk.Entry(x_frame, textvariable=self.x_var, width=8, font=("Arial", 10, "bold"), foreground="blue")
        self.x_entry.pack(side=tk.LEFT, padx=(5, 0))
        
        # Y coordinate (Entry)
        y_frame = ttk.Frame(coord_frame)
        y_frame.pack(fill=tk.X)
        ttk.Label(y_frame, text="Y:", font=("Arial", 10, "bold")).pack(side=tk.LEFT)
        self.y_var = tk.StringVar(value="0")
        self.y_entry = ttk.Entry(y_frame, textvariable=self.y_var, width=8, font=("Arial", 10, "bold"), foreground="blue")
        self.y_entry.pack(side=tk.LEFT, padx=(5, 0))
        
        # Отображение последних координат клика
        self.last_click_x = tk.StringVar(value="")
        self.last_click_y = tk.StringVar(value="")
        last_click_frame = ttk.Frame(coord_frame)
        last_click_frame.pack(fill=tk.X, pady=(10, 0))
        ttk.Label(last_click_frame, text="Last Click X:").pack(side=tk.LEFT)
        self.last_click_x_label = ttk.Label(last_click_frame, textvariable=self.last_click_x, foreground="red")
        self.last_click_x_label.pack(side=tk.LEFT, padx=(5, 10))
        ttk.Label(last_click_frame, text="Y:").pack(side=tk.LEFT)
        self.last_click_y_label = ttk.Label(last_click_frame, textvariable=self.last_click_y, foreground="red")
        self.last_click_y_label.pack(side=tk.LEFT, padx=(5, 0))

        # Input для копирования координат клика
        self.click_coords_var = tk.StringVar(value="")
        coords_frame = ttk.Frame(coord_frame)
        coords_frame.pack(fill=tk.X, pady=(2, 0))
        ttk.Label(coords_frame, text="Click (X, Y):").pack(side=tk.LEFT)
        self.click_coords_entry = ttk.Entry(coords_frame, textvariable=self.click_coords_var, width=18, foreground="red")
        self.click_coords_entry.pack(side=tk.LEFT, padx=(5, 0))

        # Mouse tracking controls
        control_frame = ttk.Frame(coord_frame)
        control_frame.pack(fill=tk.X, pady=(10, 0))
        self.tracking_button = ttk.Button(control_frame, text="Stop Mouse Tracking", 
                                         command=self.toggle_mouse_tracking)
        self.tracking_button.pack(side=tk.LEFT)
        
        # Кнопка перемещения мыши
        self.move_mouse_btn = ttk.Button(control_frame, text="Переместить мышь", command=self.move_mouse_to_input)
        self.move_mouse_btn.pack(side=tk.LEFT, padx=(10, 0))

        # --- Broker/Buy/Sell form ---
        form_frame = ttk.Frame(coord_frame)
        form_frame.pack(fill=tk.X, pady=(15, 0))

        # Broker dropdown
        ttk.Label(form_frame, text="Брокер:").grid(row=0, column=0, sticky="e")
        self.broker_var = tk.StringVar(value="PocketOption")
        self.broker_combo = ttk.Combobox(form_frame, textvariable=self.broker_var, values=["PocketOption", "Binarium", "Codecs"], state="readonly", width=12)
        self.broker_combo.grid(row=0, column=1, padx=5)

        # Symbol index
        ttk.Label(form_frame, text="Symbol Index:").grid(row=0, column=2, sticky="e")
        self.symbol_index_var = tk.StringVar()
        ttk.Entry(form_frame, textvariable=self.symbol_index_var, width=6).grid(row=0, column=3, padx=5)

        # Risk
        ttk.Label(form_frame, text="Risk:").grid(row=0, column=4, sticky="e")
        self.risk_var = tk.StringVar()
        ttk.Entry(form_frame, textvariable=self.risk_var, width=8).grid(row=0, column=5, padx=5)

        # Duration
        ttk.Label(form_frame, text="Duration:").grid(row=0, column=6, sticky="e")
        self.duration_var = tk.StringVar()
        ttk.Entry(form_frame, textvariable=self.duration_var, width=8).grid(row=0, column=7, padx=5)

        # Buy/Sell buttons
        self.buy_btn = ttk.Button(form_frame, text="BUY", command=self.send_buy)
        self.buy_btn.grid(row=0, column=8, padx=(10, 2))
        self.sell_btn = ttk.Button(form_frame, text="SELL", command=self.send_sell)
        self.sell_btn.grid(row=0, column=9, padx=(2, 0))
        self.send_duration_btn = ttk.Button(form_frame, text="Send Duration", command=self.send_duration)
        self.send_duration_btn.grid(row=0, column=10, padx=(10, 0))

        # Log text area
        log_frame = ttk.LabelFrame(main_frame, text="Server Log", padding="5")
        log_frame.pack(fill=tk.BOTH, expand=True)
        self.log_text = ScrolledText(log_frame, state='normal', height=20, width=80)
        self.log_text.pack(fill=tk.BOTH, expand=True)
        # Mouse tracking state
        self.mouse_tracking_active = True

        # Запуск слушателя мыши в отдельном потоке
        self.mouse_listener = mouse.Listener(on_click=self.on_mouse_click)
        self.mouse_listener.start()

    def append_log(self, msg):
        self.log_text.config(state='normal')
        self.log_text.insert(tk.END, msg + '\n')
        self.log_text.see(tk.END)
        self.log_text.config(state='normal')  # Keep enabled for now for debugging
    
    def update_mouse_coordinates(self, x, y):
        """Update mouse coordinates display"""
        self.x_var.set(str(x))
        self.y_var.set(str(y))
    
    def toggle_mouse_tracking(self):
        """Toggle mouse tracking on/off"""
        if self.mouse_tracking_active:
            self.mouse_tracking_active = False
            mouse_tracking_stop_event.set()
            self.tracking_button.config(text="Start Mouse Tracking")
        else:
            self.mouse_tracking_active = True
            mouse_tracking_stop_event.clear()
            # Restart mouse tracking thread
            mouse_thread = threading.Thread(target=track_mouse_position, args=(self,), daemon=True)
            mouse_thread.start()
            self.tracking_button.config(text="Stop Mouse Tracking")

    def move_mouse_to_input(self):
        try:
            x = int(self.x_var.get())
            y = int(self.y_var.get())
            self.mouse.move_to(x, y)
            self.append_log(f"[debug] Мышь перемещена в точку ({x}, {y})")
        except Exception as e:
            self.append_log(f"[debug] Ошибка перемещения мыши: {e}")

    def send_buy(self):
        try:
            broker = self.broker_var.get()
            symbol_index = int(self.symbol_index_var.get())
            risk = self.risk_var.get()
            duration = self.duration_var.get()
            if broker == "PocketOption":
                self.browser_manager.buy(symbol_index, risk, duration)
                self.append_log(f"[debug] BUY: {symbol_index}, {risk}, {duration}")
            else:
                self.append_log(f"[debug] BUY для брокера {broker} не реализован")
        except Exception as e:
            self.append_log(f"[debug] Ошибка BUY: {e}")

    def send_sell(self):
        try:
            broker = self.broker_var.get()
            symbol_index = int(self.symbol_index_var.get())
            risk = self.risk_var.get()
            duration = self.duration_var.get()
            if broker == "PocketOption":
                self.browser_manager.sell(symbol_index, risk, duration)
                self.append_log(f"[debug] SELL: {symbol_index}, {risk}, {duration}")
            else:
                self.append_log(f"[debug] SELL для брокера {broker} не реализован")
        except Exception as e:
            self.append_log(f"[debug] Ошибка SELL: {e}")

    def send_duration(self):
        try:
            duration = self.duration_var.get()
            if hasattr(self.browser_manager, "set_duration"):
                self.browser_manager.activate_window()
                self.browser_manager.set_duration(duration)
                self.append_log(f"[debug] Duration sent: {duration}")
            else:
                self.append_log("[debug] set_duration method not found in browser_manager")
        except Exception as e:
            self.append_log(f"[debug] Ошибка отправки duration: {e}")

    def on_mouse_click(self, x, y, button, pressed):
        if pressed and button == mouse.Button.left:
            self.last_click_x.set(str(x))
            self.last_click_y.set(str(y))
            self.click_coords_var.set(f"{x}, {y}")

def track_mouse_position(app):
    """Track mouse position in a separate thread"""
    while not mouse_tracking_stop_event.is_set():
        try:
            # Get current mouse position
            x, y = pyautogui.position()
            
            # Update coordinates in main thread
            app.root.after(0, app.update_mouse_coordinates, x, y)
            
            # Small delay to reduce CPU usage
            time.sleep(0.1)
            
        except Exception as e:
            log_queue.put(f"Mouse tracking error: {e}")
            break

def process_cmds(command, browser_manager):
    """Processes commands received from the client."""
    cmd = command.get("cmd", "")
    if cmd == "echo":
        return {"cmd": cmd, "result": "Echo successful"}
    elif cmd == "buy":
        # Ensure required keys are present
        required_keys = ["risk", "symbol", "duration"]
        missing_keys = [k for k in required_keys if k not in command]
        if missing_keys:
            return {
                "cmd": cmd,
                "result": f"Missing keys: {', '.join(missing_keys)}"
            }
        risk = command.get("risk", "")
        symbol = command.get("symbol", "")
        duration = command.get("duration", "")
        # Call browser manager buy logic
        browser_manager.buy(symbol=symbol, risk=risk, duration=duration)
        return {
            "cmd": cmd,
            "result": f"Buy order placed",
            "symbol": symbol,
            "risk": risk,
            "duration": duration
        }
    elif cmd == "sell":
        # Ensure required keys are present
        required_keys = ["risk", "symbol", "duration"]
        missing_keys = [k for k in required_keys if k not in command]
        if missing_keys:
            return {
                "cmd": cmd,
                "result": f"Missing keys: {', '.join(missing_keys)}"
            }
        risk = command.get("risk", "")
        symbol = command.get("symbol", "")
        duration = command.get("duration", "")
        # Call browser manager sell logic
        browser_manager.sell(symbol=symbol, risk=risk, duration=duration)
        return {
            "cmd": cmd,
            "result": f"Sell order placed",
            "symbol": symbol,
            "risk": risk,
            "duration": duration
        }
    elif cmd == "get_active_symbols":
        return {
            "cmd": cmd,
            "result": browser_manager.active_symbols
        }
    else:
        return {"cmd": cmd, "result": f"Unknown command: {cmd}"}

def start_server(host='0.0.0.0', port=65300, browser_manager=None):
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((host, port))
        s.listen()
        log_queue.put(f"Server listening on {host}:{port}")
        s.settimeout(1.0)
        while not stop_event.is_set():
            try:
                conn, addr = s.accept()
            except socket.timeout:
                continue
            except Exception as e:
                log_queue.put(f"Accept error: {e}")
                continue
            with conn:
                log_queue.put(f"Connected by {addr}")
                while not stop_event.is_set():
                    try:
                        data = conn.recv(1024)
                        if not data:
                            break
                        message = json.loads(data.decode())
                        log_queue.put(f"Received from client: {message}")
                        response = process_cmds(message, browser_manager)
                        conn.sendall(json.dumps(response).encode())
                        log_queue.put(f"Sent to client: {response}")
                    except json.JSONDecodeError:
                        log_queue.put(f"Received invalid JSON: {data.decode()}")
                        error_response = {"error": "Invalid JSON"}
                        conn.sendall(json.dumps(error_response).encode())
                        log_queue.put(f"Sent error to client: {error_response}")
                    except Exception as e:
                        log_queue.put(f"An error occurred: {e}")
                        error_response = {"error": str(e)}
                        try:
                            conn.sendall(json.dumps(error_response).encode())
                        except Exception:
                            pass
                        log_queue.put(f"Sent error to client: {error_response}")
                        break  # Break inner loop, but continue accepting new clients
            # After client disconnects, loop back to accept a new connection

def poll_log_queue(app):
    while not log_queue.empty():
        msg = log_queue.get_nowait()
        app.append_log(msg)
    app.root.after(100, poll_log_queue, app)

def run_gui():
    root = tk.Tk()
    # Always create BrowserManagerService with log_queue
    browser_manager = BrowserManagerService(log_queue=log_queue)
    app = AppGUI(root, browser_manager)

    # Test log message to verify log queue and GUI
    log_queue.put("GUI started and log queue is working!")

    # Start MT4 server in a thread, passing browser_manager
    server_thread = threading.Thread(target=start_server, kwargs={'browser_manager': browser_manager}, daemon=True)
    server_thread.start()

    # Start mouse tracking in a separate thread
    mouse_thread = threading.Thread(target=track_mouse_position, args=(app,), daemon=True)
    mouse_thread.start()

    # Graceful shutdown handler
    def on_closing():
        log_queue.put("Shutting down server and closing app...")
        stop_event.set()  # Signal server thread to stop
        mouse_tracking_stop_event.set()  # Signal mouse tracking thread to stop
        root.destroy()

    root.protocol('WM_DELETE_WINDOW', on_closing)

    # Start polling the log queue
    root.after(100, poll_log_queue, app)
    root.mainloop()

if __name__ == "__main__":
    run_gui()
