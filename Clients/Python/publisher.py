#!/usr/bin/env python3
"""
Distributed Message Broker - Python Publisher Client
Supports JSON and XML serialization over TCP socket with delimiter framing.
"""

import socket
import json
import xml.etree.ElementTree as ET
import sys
import os
import time

DELIMITER = "\n"

def create_xml_payload(topic: str, message: str, sender: str) -> str:
    root = ET.Element("payload")
    ET.SubElement(root, "id").text = ""
    ET.SubElement(root, "topic").text = topic
    ET.SubElement(root, "message").text = message
    ET.SubElement(root, "timestamp").text = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    ET.SubElement(root, "format").text = "xml"
    ET.SubElement(root, "sender").text = sender
    return ET.tostring(root, encoding="utf-8").decode("utf-8")

def create_json_payload(topic: str, message: str, sender: str) -> str:
    data = {
        "id": "",
        "topic": topic,
        "message": message,
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "format": "json",
        "sender": sender
    }
    return json.dumps(data)

def send_frame(sock: socket.socket, message: str):
    # Normalize to single-line frame ending with delimiter
    clean_msg = message.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
    sock.sendall(clean_msg.encode("utf-8"))

def main():
    broker_ip = os.environ.get("BROKER_IP", "127.0.0.1")
    broker_port = int(os.environ.get("BROKER_PORT", 9000))

    if len(sys.argv) > 1:
        broker_ip = sys.argv[1]
    if len(sys.argv) > 2:
        broker_port = int(sys.argv[2])

    print("=" * 50)
    print("          PYTHON MESSAGE PUBLISHER")
    print("=" * 50)
    print(f"[Config] Target Broker: {broker_ip}:{broker_port}")

    sender_name = input("Enter your Publisher name (press Enter for default): ").strip()
    if not sender_name:
        sender_name = f"python-pub-{os.getpid()}"

    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.connect((broker_ip, broker_port))
        print("[Status] Connected successfully to Broker.")
    except Exception as e:
        print(f"[Error] Failed to connect: {e}")
        return

    print("\nChoose default payload serialization format:")
    print("  1. JSON (Standard)")
    print("  2. XML  (Adapter Pattern Testing)")
    choice = input("Select format [1/2, default: 1]: ").strip()
    fmt = "xml" if choice in ["2", "xml"] else "json"

    print(f"\n[Active] Publishing mode: {fmt.upper()}")
    print("Type 'exit' as topic to quit, or 'switch' to toggle JSON/XML.\n")

    try:
        while True:
            topic = input("\nEnter topic: ").strip()
            if not topic:
                continue

            if topic.lower() == "exit":
                break

            if topic.lower() == "switch":
                fmt = "xml" if fmt == "json" else "json"
                print(f"[Switched] Format is now: {fmt.upper()}")
                continue

            message = input("Enter message: ")

            if fmt == "xml":
                raw_payload = create_xml_payload(topic.lower(), message, sender_name)
            else:
                raw_payload = create_json_payload(topic.lower(), message, sender_name)

            send_frame(sock, raw_payload)
            print(f"-> Sent [{fmt.upper()}] payload to topic '{topic}'")

            # Try reading ACK non-blocking / with short timeout
            sock.settimeout(0.5)
            try:
                ack = sock.recv(1024).decode("utf-8").strip()
                if ack:
                    print(f"  <- [Broker Response] {ack}")
            except socket.timeout:
                pass
            finally:
                sock.settimeout(None)

    except KeyboardInterrupt:
        print("\n[Publisher] Interrupted.")
    finally:
        sock.close()
        print("[Publisher] Disconnected cleanly.")

if __name__ == "__main__":
    main()
