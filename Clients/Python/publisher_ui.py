#!/usr/bin/env python3
"""
Distributed Message Broker - Dedicated Publisher GUI Client (Tkinter)
Allows interactive, continuous message publishing without restarting the application.
Features:
- Live TCP Socket connection to Broker
- Continuous message publishing to any topic
- Instant format selection (JSON or XML) for Adapter Pattern testing
- Real-time log of sent messages and Broker ACKs
"""

import tkinter as tk
from tkinter import ttk, messagebox
import socket
import threading
import json
import xml.etree.ElementTree as ET
import os
import sys
import datetime
import time

DELIMITER = "\n"

class PublisherApp:
    def __init__(self, root, default_name="Publisher-1"):
        self.root = root
        self.client_name = default_name
        self.root.title(f"Message Publisher UI - {self.client_name}")
        self.root.geometry("850x620")
        self.root.minsize(700, 480)

        self.sock = None
        self.connected = False
        self.preferred_format = tk.StringVar(value="json")

        self._build_ui()
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

    def _build_ui(self):
        style = ttk.Style()
        style.theme_use("clam")

        # 1. Top Bar: Connection Configuration
        top_frame = ttk.LabelFrame(self.root, text="Broker Connection", padding=(10, 5))
        top_frame.pack(fill="x", padx=10, pady=5)

        ttk.Label(top_frame, text="Host:").pack(side="left", padx=(0, 5))
        self.host_entry = ttk.Entry(top_frame, width=15)
        self.host_entry.insert(0, os.environ.get("BROKER_IP", "127.0.0.1"))
        self.host_entry.pack(side="left", padx=(0, 10))

        ttk.Label(top_frame, text="Port:").pack(side="left", padx=(0, 5))
        self.port_entry = ttk.Entry(top_frame, width=8)
        self.port_entry.insert(0, os.environ.get("BROKER_PORT", "9000"))
        self.port_entry.pack(side="left", padx=(0, 10))

        ttk.Label(top_frame, text="Sender Name:").pack(side="left", padx=(0, 5))
        self.name_entry = ttk.Entry(top_frame, width=15)
        self.name_entry.insert(0, self.client_name)
        self.name_entry.pack(side="left", padx=(0, 15))

        self.btn_connect = ttk.Button(top_frame, text="Connect", command=self._toggle_connection)
        self.btn_connect.pack(side="left", padx=5)

        self.lbl_status = ttk.Label(top_frame, text="● Disconnected", foreground="red", font=("Segoe UI", 9, "bold"))
        self.lbl_status.pack(side="right", padx=10)

        # 2. Middle Frame: Message Composition
        compose_frame = ttk.LabelFrame(self.root, text="Compose & Publish Message", padding=(10, 8))
        compose_frame.pack(fill="x", padx=10, pady=5)

        # Topic & Format row
        row1 = ttk.Frame(compose_frame)
        row1.pack(fill="x", pady=(0, 5))

        ttk.Label(row1, text="Topic:").pack(side="left", padx=(0, 5))
        self.topic_entry = ttk.Entry(row1, width=25)
        self.topic_entry.pack(side="left", padx=(0, 20))
        self.topic_entry.insert(0, "stiri")

        ttk.Label(row1, text="Format:").pack(side="left", padx=(0, 8))
        ttk.Radiobutton(row1, text="JSON", variable=self.preferred_format, value="json").pack(side="left", padx=5)
        ttk.Radiobutton(row1, text="XML (Adapter)", variable=self.preferred_format, value="xml").pack(side="left", padx=5)

        # Message Text Box
        ttk.Label(compose_frame, text="Message Content:").pack(anchor="w", pady=(5, 2))
        self.txt_message = tk.Text(compose_frame, height=4, font=("Consolas", 10), wrap="word")
        self.txt_message.pack(fill="x", pady=(0, 5))
        self.txt_message.insert("1.0", "Salutare tuturor subscriberilor din retea!")

        # Action Buttons row
        btn_row = ttk.Frame(compose_frame)
        btn_row.pack(fill="x")

        self.btn_send = ttk.Button(btn_row, text="➤  Publish Message", command=self._publish, state="disabled")
        self.btn_send.pack(side="left", padx=(0, 10))

        ttk.Button(btn_row, text="Clear Message", command=lambda: self.txt_message.delete("1.0", tk.END)).pack(side="left")
        ttk.Label(btn_row, text="(Press Ctrl+Enter to publish quickly)", font=("Segoe UI", 8, "italic"), foreground="gray").pack(side="left", padx=15)

        self.root.bind("<Control-Return>", lambda e: self._publish())

        # 3. Bottom Frame: Transmission History
        hist_frame = ttk.LabelFrame(self.root, text="Transmission Log & Broker ACKs", padding=(10, 5))
        hist_frame.pack(fill="both", expand=True, padx=10, pady=5)

        columns = ("time", "topic", "format", "message", "status")
        self.tree = ttk.Treeview(hist_frame, columns=columns, show="headings", selectmode="browse")
        self.tree.heading("time", text="Time (Local)")
        self.tree.heading("topic", text="Topic")
        self.tree.heading("format", text="Format")
        self.tree.heading("message", text="Message Content")
        self.tree.heading("status", text="Status / ACK")

        self.tree.column("time", width=90, anchor="center")
        self.tree.column("topic", width=120, anchor="w")
        self.tree.column("format", width=70, anchor="center")
        self.tree.column("message", width=350, anchor="w")
        self.tree.column("status", width=160, anchor="center")

        scrollbar = ttk.Scrollbar(hist_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scrollbar.set)

        self.tree.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")

        # Bottom clear history bar
        bot_bar = ttk.Frame(self.root, padding=(10, 2))
        bot_bar.pack(fill="x")
        ttk.Button(bot_bar, text="Clear Log", command=self._clear_log).pack(side="right")

    def _toggle_connection(self):
        if not self.connected:
            self._connect()
        else:
            self._disconnect()

    def _connect(self):
        host = self.host_entry.get().strip()
        port_str = self.port_entry.get().strip()

        if not host or not port_str.isdigit():
            messagebox.showerror("Validation Error", "Please provide a valid Host and Port.")
            return

        try:
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.settimeout(5.0)
            self.sock.connect((host, int(port_str)))
            self.sock.settimeout(None)

            self.connected = True
            self.lbl_status.config(text="● Connected", foreground="green")
            self.btn_connect.config(text="Disconnect")
            self.btn_send.config(state="normal")
            self.host_entry.config(state="disabled")
            self.port_entry.config(state="disabled")

            # Start background thread to listen for ACKs
            threading.Thread(target=self._listen_acks, daemon=True).start()

        except Exception as ex:
            messagebox.showerror("Connection Failed", f"Could not connect to Broker at {host}:{port_str}\n\nDetails: {ex}")
            self.connected = False
            self.lbl_status.config(text="● Disconnected", foreground="red")

    def _disconnect(self):
        self.connected = False
        if self.sock:
            try:
                self.sock.close()
            except:
                pass
            self.sock = None

        self.lbl_status.config(text="● Disconnected", foreground="red")
        self.btn_connect.config(text="Connect")
        self.btn_send.config(state="disabled")
        self.host_entry.config(state="normal")
        self.port_entry.config(state="normal")

    def _listen_acks(self):
        buffer = ""
        while self.connected and self.sock:
            try:
                data = self.sock.recv(4096).decode("utf-8")
                if not data:
                    break
                buffer += data
                while DELIMITER in buffer:
                    line, buffer = buffer.split(DELIMITER, 1)
                    line = line.strip()
                    if line:
                        self.root.after(0, self._handle_ack, line)
            except:
                break

        if self.connected:
            self.root.after(0, self._disconnect)

    def _handle_ack(self, ack_line):
        # Update the most recent tree item status if matching ACK
        if ack_line.startswith("ACK#published#"):
            msg_id = ack_line.replace("ACK#published#", "")
            items = self.tree.get_children()
            if items:
                last_item = items[0]
                vals = list(self.tree.item(last_item, "values"))
                vals[4] = f"Delivered (ID: {msg_id[:8]}...)"
                self.tree.item(last_item, values=vals)

    def _publish(self):
        if not self.connected or not self.sock:
            messagebox.showwarning("Not Connected", "Please connect to the Broker first.")
            return

        topic = self.topic_entry.get().strip().lower()
        message = self.txt_message.get("1.0", tk.END).strip()
        sender = self.name_entry.get().strip() or self.client_name
        fmt = self.preferred_format.get()

        if not topic:
            messagebox.showwarning("Validation Error", "Topic cannot be empty.")
            return

        if not message:
            messagebox.showwarning("Validation Error", "Message content cannot be empty.")
            return

        # Prepare payload
        timestamp_utc = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

        if fmt == "xml":
            root_el = ET.Element("payload")
            ET.SubElement(root_el, "id").text = ""
            ET.SubElement(root_el, "topic").text = topic
            ET.SubElement(root_el, "message").text = message
            ET.SubElement(root_el, "timestamp").text = timestamp_utc
            ET.SubElement(root_el, "format").text = "xml"
            ET.SubElement(root_el, "sender").text = sender
            payload_str = ET.tostring(root_el, encoding="utf-8").decode("utf-8")
        else:
            payload_dict = {
                "id": "",
                "topic": topic,
                "message": message,
                "timestamp": timestamp_utc,
                "format": "json",
                "sender": sender
            }
            payload_str = json.dumps(payload_dict)

        # Normalize to single-line frame
        clean_msg = payload_str.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER

        try:
            self.sock.sendall(clean_msg.encode("utf-8"))

            local_time = datetime.datetime.now().strftime("%H:%M:%S")
            self.tree.insert("", 0, values=(local_time, topic, fmt.upper(), message, "Sent (Waiting ACK...)"))

            # Clear message text box for convenient next input
            self.txt_message.delete("1.0", tk.END)
            self.txt_message.focus_set()

        except Exception as ex:
            messagebox.showerror("Send Error", f"Failed to send message: {ex}")
            self._disconnect()

    def _clear_log(self):
        for item in self.tree.get_children():
            self.tree.delete(item)

    def _on_close(self):
        self._disconnect()
        self.root.destroy()

def main():
    name = "Publisher-1"
    if len(sys.argv) > 1 and sys.argv[1] == "--name" and len(sys.argv) > 2:
        name = sys.argv[2]
    elif len(sys.argv) > 1 and not sys.argv[1].startswith("--"):
        name = sys.argv[1]

    root = tk.Tk()
    app = PublisherApp(root, default_name=name)
    root.mainloop()

if __name__ == "__main__":
    main()
