#!/usr/bin/env python3
"""
Distributed Message Broker - Dedicated Subscriber GUI Client (Tkinter)
Each subscriber instance runs its own independent UI window.
Features:
- Live TCP Socket connection to Broker
- Multi-topic subscription & dynamic unsubscription
- Adapter Pattern demonstration (JSON / XML format toggle)
- Real-time tabular message feed with metadata inspection
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

DELIMITER = "\n"

class SubscriberApp:
    def __init__(self, root, default_name="Subscriber-1"):
        self.root = root
        self.client_name = default_name
        self.root.title(f"Message Subscriber UI - {self.client_name}")
        self.root.geometry("900x650")
        self.root.minsize(750, 500)

        self.sock = None
        self.running = False
        self.buffer = ""
        self.subscribed_topics = set()
        self.preferred_format = "json"

        self._build_ui()
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

    def _build_ui(self):
        style = ttk.Style()
        style.theme_use("clam")

        # Top Bar: Connection Configuration
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

        ttk.Label(top_frame, text="Instance:").pack(side="left", padx=(0, 5))
        self.name_entry = ttk.Entry(top_frame, width=15)
        self.name_entry.insert(0, self.client_name)
        self.name_entry.pack(side="left", padx=(0, 15))

        self.btn_connect = ttk.Button(top_frame, text="Connect", command=self._toggle_connection)
        self.btn_connect.pack(side="left", padx=5)

        self.lbl_status = ttk.Label(top_frame, text="● Disconnected", foreground="red", font=("Segoe UI", 9, "bold"))
        self.lbl_status.pack(side="right", padx=10)

        # Middle: Topics and Controls
        ctrl_frame = ttk.LabelFrame(self.root, text="Subscription Management", padding=(10, 5))
        ctrl_frame.pack(fill="x", padx=10, pady=5)

        ttk.Label(ctrl_frame, text="Topic:").pack(side="left", padx=(0, 5))
        self.topic_entry = ttk.Entry(ctrl_frame, width=20)
        self.topic_entry.pack(side="left", padx=(0, 5))
        self.topic_entry.bind("<Return>", lambda e: self._subscribe())

        self.btn_sub = ttk.Button(ctrl_frame, text="Subscribe", command=self._subscribe, state="disabled")
        self.btn_sub.pack(side="left", padx=5)

        self.btn_unsub = ttk.Button(ctrl_frame, text="Unsubscribe", command=self._unsubscribe, state="disabled")
        self.btn_unsub.pack(side="left", padx=5)

        ttk.Separator(ctrl_frame, orient="vertical").pack(side="left", fill="y", padx=15)

        ttk.Label(ctrl_frame, text="Format (Adapter):").pack(side="left", padx=(0, 5))
        self.format_var = tk.StringVar(value="json")
        self.rb_json = ttk.Radiobutton(ctrl_frame, text="JSON", variable=self.format_var, value="json", command=self._on_format_change)
        self.rb_xml = ttk.Radiobutton(ctrl_frame, text="XML", variable=self.format_var, value="xml", command=self._on_format_change)
        self.rb_json.pack(side="left", padx=2)
        self.rb_xml.pack(side="left", padx=2)

        self.btn_clear = ttk.Button(ctrl_frame, text="Clear Feed", command=self._clear_feed)
        self.btn_clear.pack(side="left", padx=5)

        # Subscribed Topics Chips / Label
        self.lbl_topics = ttk.Label(ctrl_frame, text="Subscribed: [None]", foreground="#0066cc", font=("Segoe UI", 9, "italic"))
        self.lbl_topics.pack(side="right", padx=10)

        # Main Table: Incoming Message Feed
        table_frame = ttk.LabelFrame(self.root, text="Incoming Message Feed (Real-Time)", padding=(10, 5))
        table_frame.pack(fill="both", expand=True, padx=10, pady=5)

        columns = ("id", "timestamp", "topic", "format", "sender", "message")
        self.tree = ttk.Treeview(table_frame, columns=columns, show="headings", selectmode="browse")

        self.tree.heading("id", text="ID")
        self.tree.heading("timestamp", text="Time (UTC)")
        self.tree.heading("topic", text="Topic")
        self.tree.heading("format", text="Format")
        self.tree.heading("sender", text="Sender")
        self.tree.heading("message", text="Message Content")

        self.tree.column("id", width=120, anchor="w")
        self.tree.column("timestamp", width=130, anchor="center")
        self.tree.column("topic", width=100, anchor="center")
        self.tree.column("format", width=70, anchor="center")
        self.tree.column("sender", width=150, anchor="w")
        self.tree.column("message", width=300, anchor="w")

        scrollbar = ttk.Scrollbar(table_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scrollbar.set)

        self.tree.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        self.tree.bind("<<TreeviewSelect>>", self._on_row_select)

        # Bottom: Payload Inspector
        bottom_frame = ttk.LabelFrame(self.root, text="Raw Payload Inspector", padding=(10, 5))
        bottom_frame.pack(fill="x", padx=10, pady=5)

        self.inspector_text = tk.Text(bottom_frame, height=5, wrap="word", font=("Consolas", 9))
        self.inspector_text.pack(fill="x", expand=True)

    def _toggle_connection(self):
        if not self.running:
            host = self.host_entry.get().strip()
            try:
                port = int(self.port_entry.get().strip())
            except ValueError:
                messagebox.showerror("Error", "Port must be an integer.")
                return

            try:
                self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.sock.connect((host, port))
                self.running = True

                self.btn_connect.config(text="Disconnect")
                self.lbl_status.config(text="● Connected", foreground="green")
                self.btn_sub.config(state="normal")
                self.btn_unsub.config(state="normal")
                self.host_entry.config(state="disabled")
                self.port_entry.config(state="disabled")

                # Send format preference
                self._on_format_change()

                # Start background listener
                threading.Thread(target=self._listen_loop, daemon=True).start()
            except Exception as e:
                messagebox.showerror("Connection Error", f"Could not connect to {host}:{port}\n\n{e}")
        else:
            self._disconnect()

    def _disconnect(self):
        self.running = False
        if self.sock:
            try:
                self.sock.close()
            except:
                pass
            self.sock = None

        self.btn_connect.config(text="Connect")
        self.lbl_status.config(text="● Disconnected", foreground="red")
        self.btn_sub.config(state="disabled")
        self.btn_unsub.config(state="disabled")
        self.host_entry.config(state="normal")
        self.port_entry.config(state="normal")
        self.subscribed_topics.clear()
        self._update_topics_label()

    def _send_cmd(self, cmd: str):
        if not self.sock or not self.running:
            return
        frame = cmd.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
        try:
            self.sock.sendall(frame.encode("utf-8"))
        except Exception as e:
            self._disconnect()

    def _subscribe(self):
        topic = self.topic_entry.get().strip().lower()
        if not topic:
            return
        if topic in self.subscribed_topics:
            messagebox.showinfo("Abonare Existentă", f"Sunteți deja abonat la topicul '{topic}'!")
            self.topic_entry.delete(0, tk.END)
            return

        self.subscribed_topics.add(topic)
        self._send_cmd(f"subscribe#{topic}")
        self._update_topics_label()
        self.topic_entry.delete(0, tk.END)

    def _unsubscribe(self):
        topic = self.topic_entry.get().strip().lower()
        if not topic and self.subscribed_topics:
            topic = list(self.subscribed_topics)[-1]

        if not topic:
            return

        if topic not in self.subscribed_topics:
            messagebox.showwarning("Neabonat", f"Nu sunteți abonat la topicul '{topic}'.")
            self.topic_entry.delete(0, tk.END)
            return

        self.subscribed_topics.discard(topic)
        self._send_cmd(f"unsubscribe#{topic}")
        self._update_topics_label()
        self.topic_entry.delete(0, tk.END)

    def _clear_feed(self):
        for item in self.tree.get_children():
            self.tree.delete(item)
        self.inspector_text.delete("1.0", tk.END)

    def _on_format_change(self):
        fmt = self.format_var.get()
        self.preferred_format = fmt
        self._send_cmd(f"format#{fmt}")

    def _update_topics_label(self):
        if self.subscribed_topics:
            self.lbl_topics.config(text=f"Subscribed: [{', '.join(sorted(self.subscribed_topics))}]")
        else:
            self.lbl_topics.config(text="Subscribed: [None]")

    def _listen_loop(self):
        while self.running:
            try:
                data = self.sock.recv(4096)
                if not data:
                    self.root.after(0, self._disconnect)
                    break

                self.buffer += data.decode("utf-8", errors="replace")
                while DELIMITER in self.buffer:
                    frame, self.buffer = self.buffer.split(DELIMITER, 1)
                    frame = frame.strip()
                    if frame:
                        self.root.after(0, self._process_frame, frame)
            except Exception:
                if self.running:
                    self.root.after(0, self._disconnect)
                break

    def _process_frame(self, frame: str):
        if frame.startswith("ACK#") or frame.startswith("TOPICS#") or frame.startswith("ERROR#"):
            self.inspector_text.delete("1.0", tk.END)
            self.inspector_text.insert(tk.END, f"[Broker Response] {frame}")
            return

        parsed = None
        fmt = "UNKNOWN"

        # Try JSON
        if frame.startswith("{"):
            try:
                data = json.loads(frame)
                fmt = "JSON"
                parsed = {
                    "id": str(data.get("id", "")),
                    "timestamp": str(data.get("timestamp", ""))[:19],
                    "topic": str(data.get("topic", "")),
                    "format": fmt,
                    "sender": str(data.get("sender", "anonymous")),
                    "message": str(data.get("message", ""))
                }
            except:
                pass

        # Try XML
        if not parsed and frame.startswith("<"):
            try:
                root = ET.fromstring(frame)
                fmt = "XML"
                parsed = {
                    "id": root.findtext("id", ""),
                    "timestamp": root.findtext("timestamp", "")[:19],
                    "topic": root.findtext("topic", ""),
                    "format": fmt,
                    "sender": root.findtext("sender", "anonymous"),
                    "message": root.findtext("message", "")
                }
            except:
                pass

        if parsed:
            # Send Consumer ACK back to Broker
            if parsed.get("id"):
                self._send_cmd(f"ACK#consumed#{parsed['id']}")

            item_id = self.tree.insert("", 0, values=(
                parsed["id"],
                parsed["timestamp"],
                parsed["topic"],
                parsed["format"],
                parsed["sender"],
                parsed["message"]
            ))
            self.tree.selection_set(item_id)
            self.inspector_text.delete("1.0", tk.END)
            self.inspector_text.insert(tk.END, frame)
        else:
            self.inspector_text.delete("1.0", tk.END)
            self.inspector_text.insert(tk.END, f"[Raw]: {frame}")

    def _on_row_select(self, event):
        selected = self.tree.selection()
        if not selected:
            return
        vals = self.tree.item(selected[0], "values")
        if vals:
            summary = (
                f"ID:        {vals[0]}\n"
                f"Timestamp: {vals[1]}\n"
                f"Topic:     {vals[2]}\n"
                f"Format:    {vals[3]}\n"
                f"Sender:    {vals[4]}\n"
                f"Message:   {vals[5]}"
            )
            self.inspector_text.delete("1.0", tk.END)
            self.inspector_text.insert(tk.END, summary)

    def _on_close(self):
        self._disconnect()
        self.root.destroy()

def main():
    name = "Subscriber-1"
    if "--name" in sys.argv:
        idx = sys.argv.index("--name")
        if idx + 1 < len(sys.argv):
            name = sys.argv[idx + 1]

    root = tk.Tk()
    app = SubscriberApp(root, default_name=name)
    root.mainloop()

if __name__ == "__main__":
    main()
