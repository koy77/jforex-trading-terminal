import socket
import logging
import json
import threading
import tkinter as tk
from tkinter import scrolledtext, simpledialog, messagebox

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

class SocketClientGUI:
    def __init__(self, master):
        self.master = master
        self.master.title("PO Binary Trading Client")
        self.master.geometry("700x600")

        # Log area
        self.log_area = scrolledtext.ScrolledText(master, state='disabled', font=("Consolas", 11))
        self.log_area.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        # Command entry and send button
        entry_frame = tk.Frame(master)
        entry_frame.pack(fill=tk.X, padx=10, pady=5)

        self.command_entry = tk.Entry(entry_frame, font=("Consolas", 12))
        self.command_entry.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(0, 5))
        self.command_entry.bind("<Return>", self.send_command)

        send_btn = tk.Button(entry_frame, text="Send", command=self.send_command)
        send_btn.pack(side=tk.LEFT)

        # Test button
        test_btn = tk.Button(master, text="Test Buy Command", command=self.send_test_command, bg="#4CAF50", fg="white", font=("Consolas", 12, "bold"))
        test_btn.pack(pady=5)

        self.socket = None
        self.running = False

        self.host = "127.0.0.1"
        self.port = 65300

        if self.host:
            threading.Thread(target=self.start_client, daemon=True).start()
        else:
            self.log("No host provided. Exiting.")
            self.master.after(1000, self.master.destroy)

    def log(self, message):
        self.log_area.config(state='normal')
        self.log_area.insert(tk.END, message + "\n")
        self.log_area.see(tk.END)
        self.log_area.config(state='disabled')

    def start_client(self):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            self.socket = s
            try:
                s.connect((self.host, self.port))
                self.running = True
                self.log(f"Connected to {self.host}:{self.port}")
                while self.running:
                    data = s.recv(1024)
                    if not data:
                        break
                    try:
                        response = json.loads(data.decode())
                        self.log(f"Received: {response}")
                    except json.JSONDecodeError:
                        self.log(f"Received invalid JSON: {data.decode()}")
            except ConnectionRefusedError:
                self.log(f"Connection refused. Make sure the server is running on {self.host}:{self.port}")
            except socket.gaierror:
                self.log(f"Invalid IP address or hostname: {self.host}")
            except Exception as e:
                self.log(f"An error occurred: {e}")
            self.running = False

    def send_command(self, event=None):
        cmd_text = self.command_entry.get().strip()
        if not cmd_text or not self.socket:
            return
        try:
            command = {"cmd": cmd_text}
            self.socket.sendall(json.dumps(command).encode())
            self.log(f"Sent: {command}")
            self.command_entry.delete(0, tk.END)
        except Exception as e:
            self.log(f"Send error: {e}")

    def send_test_command(self):
        if not self.socket:
            messagebox.showerror("Error", "Not connected to server.")
            return
        test_command = {
            "cmd": "buy",
            "risk": "10%",
            "symbol": "GBPJPY",
            "duration": "1"
        }
        try:
            self.socket.sendall(json.dumps(test_command).encode())
            self.log(f"Sent: {test_command}")
        except Exception as e:
            self.log(f"Send error: {e}")

if __name__ == "__main__":
    root = tk.Tk()
    app = SocketClientGUI(root)
    root.mainloop()
