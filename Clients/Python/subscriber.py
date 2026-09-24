#!/usr/bin/env python3
"""
Distributed Message Broker - Python Subscriber CLI Client
Supports multi-topic subscriptions, dynamic unsubscription, format negotiation, and real-time message stream.
"""

import socket
import threading
import json
import xml.etree.ElementTree as ET
import sys
import os

DELIMITER = "\n"

class PythonSubscriber:
    def __init__(self, broker_ip="127.0.0.1", broker_port=9000):
        self.broker_ip = broker_ip
        self.broker_port = broker_port
        self.sock = None
        self.running = False
        self.buffer = ""
        self.subscribed_topics = set()

    def connect(self):
        try:
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.connect((self.broker_ip, self.broker_port))
            self.running = True
            print(f"[Subscriber] Connected to Broker at {self.broker_ip}:{self.broker_port}")

            # Start listener thread
            threading.Thread(target=self._listen_loop, daemon=True).start()
            return True
        except Exception as e:
            print(f"[Error] Failed to connect to Broker: {e}")
            return False

    def send_command(self, cmd: str):
        if not self.sock:
            return
        frame = cmd.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
        self.sock.sendall(frame.encode("utf-8"))

    def subscribe(self, topic: str):
        topic = topic.strip().lower()
        if topic in self.subscribed_topics:
            print(f"[Subscriber] You are ALREADY subscribed to topic: '{topic}'. Request skipped.")
            return
        self.subscribed_topics.add(topic)
        self.send_command(f"subscribe#{topic}")
        print(f"[Subscriber] Sent subscribe request for: '{topic}'")

    def unsubscribe(self, topic: str):
        topic = topic.strip().lower()
        if topic not in self.subscribed_topics:
            print(f"[Subscriber] You are NOT subscribed to topic: '{topic}'.")
            return
        self.subscribed_topics.discard(topic)
        self.send_command(f"unsubscribe#{topic}")
        print(f"[Subscriber] Sent unsubscribe request for: '{topic}'")

    def request_topics(self):
        self.send_command("topics#list")

    def set_format(self, fmt: str):
        fmt = fmt.strip().lower()
        self.send_command(f"format#{fmt}")
        print(f"[Subscriber] Requested format preference: {fmt.upper()}")

    def _listen_loop(self):
        while self.running:
            try:
                data = self.sock.recv(4096)
                if not data:
                    print("\n[Subscriber] Connection closed by Broker.")
                    self.running = False
                    break

                self.buffer += data.decode("utf-8", errors="replace")
                while DELIMITER in self.buffer:
                    frame, self.buffer = self.buffer.split(DELIMITER, 1)
                    frame = frame.strip()
                    if frame:
                        self._handle_frame(frame)
            except Exception as e:
                if self.running:
                    print(f"\n[Subscriber] Connection error: {e}")
                break

    def _handle_frame(self, frame: str):
        # Check control messages
        if frame.startswith("ACK#") or frame.startswith("TOPICS#") or frame.startswith("ERROR#"):
            print(f"\n[Broker Control] {frame}")
            return

        # Attempt JSON parse
        try:
            if frame.startswith("{"):
                obj = json.loads(frame)
                self._print_payload(
                    topic=obj.get("topic", "unknown"),
                    msg=obj.get("message", ""),
                    msg_id=obj.get("id", ""),
                    timestamp=obj.get("timestamp", ""),
                    sender=obj.get("sender", "anonymous"),
                    fmt="JSON"
                )
                return
        except Exception:
            pass

        # Attempt XML parse
        try:
            if frame.startswith("<"):
                root = ET.fromstring(frame)
                self._print_payload(
                    topic=root.findtext("topic", "unknown"),
                    msg=root.findtext("message", ""),
                    msg_id=root.findtext("id", ""),
                    timestamp=root.findtext("timestamp", ""),
                    sender=root.findtext("sender", "anonymous"),
                    fmt="XML"
                )
                return
        except Exception:
            pass

        # Fallback raw text
        print(f"\n[Raw Message]: {frame}")

    def _print_payload(self, topic, msg, msg_id, timestamp, sender, fmt):
        print("\n" + "-" * 50)
        print(f"[RECEIVED MESSAGE] Topic: '{topic}' (Format: {fmt})")
        print(f"  ID:        {msg_id}")
        print(f"  Time:      {timestamp}")
        print(f"  Sender:    {sender}")
        print(f"  Content:   {msg}")
        print("-" * 50)

    def close(self):
        self.running = False
        if self.sock:
            self.sock.close()

def main():
    broker_ip = os.environ.get("BROKER_IP", "127.0.0.1")
    broker_port = int(os.environ.get("BROKER_PORT", 9000))

    if len(sys.argv) > 1:
        broker_ip = sys.argv[1]
    if len(sys.argv) > 2:
        broker_port = int(sys.argv[2])

    print("=" * 50)
    print("          PYTHON MESSAGE SUBSCRIBER")
    print("=" * 50)

    client = PythonSubscriber(broker_ip, broker_port)
    if not client.connect():
        return

    initial_topic = input("Enter initial topic to subscribe (press Enter to skip): ").strip()
    if initial_topic:
        client.subscribe(initial_topic)

    print("\nAvailable Commands:")
    print("  sub <topic>     - Subscribe to topic")
    print("  unsub <topic>   - Unsubscribe from topic")
    print("  my              - View active subscriptions")
    print("  list            - List all topics from Broker")
    print("  format <json|xml> - Set output format")
    print("  exit            - Disconnect")

    try:
        while client.running:
            cmd = input("\n[Command] > ").strip()
            if not cmd:
                continue

            if cmd.lower() in ["exit", "quit"]:
                break
            elif cmd.lower().startswith("sub "):
                client.subscribe(cmd[4:].strip())
            elif cmd.lower().startswith("unsub "):
                client.unsubscribe(cmd[6:].strip())
            elif cmd.lower() == "my":
                print(f"Active subscriptions: {list(client.subscribed_topics)}")
            elif cmd.lower() == "list":
                client.request_topics()
            elif cmd.lower().startswith("format "):
                client.set_format(cmd[7:].strip())
            else:
                print("Unknown command. Type sub <topic>, unsub <topic>, list, format, or exit.")
    except KeyboardInterrupt:
        pass
    finally:
        client.close()
        print("[Subscriber] Disconnected cleanly.")

if __name__ == "__main__":
    main()
