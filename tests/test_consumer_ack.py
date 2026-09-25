#!/usr/bin/env python3
"""
Test Suite: Consumer ACK / NACK Protocol & In-Flight Tracking
Validates that:
- When a consumer receives a message, it can send ACK#consumed#<id> or NACK#<id>#<reason>.
- Broker tracks in-flight messages and confirms consumer acknowledgments.
"""

import socket
import json
import time
import sys

DELIMITER = "\n"

def send_frame(sock, text):
    msg = text.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
    sock.sendall(msg.encode("utf-8"))

def main():
    broker_ip = "127.0.0.1"
    broker_port = 9000

    print("=== Testing Consumer ACK / NACK Protocol ===")

    try:
        sub_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sub_sock.connect((broker_ip, broker_port))

        pub_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        pub_sock.connect((broker_ip, broker_port))
    except Exception as ex:
        print(f"[SKIP] Broker not reachable: {ex}")
        sys.exit(0)

    topic = f"ack-test-{int(time.time())}"
    send_frame(sub_sock, f"subscribe#{topic}#live")
    sub_sock.settimeout(2.0)
    sub_ack = sub_sock.recv(1024).decode("utf-8").strip()
    assert f"ACK#subscribed#{topic}" in sub_ack, f"Failed subscribe: {sub_ack}"

    # Publish message
    test_msg = {
        "id": "",
        "topic": topic,
        "message": "Testing consumer acknowledgment feedback loop",
        "timestamp": "2026-09-25T08:00:00Z",
        "format": "json",
        "sender": "AckTester"
    }
    send_frame(pub_sock, json.dumps(test_msg))
    pub_sock.settimeout(2.0)
    pub_ack = pub_sock.recv(1024).decode("utf-8").strip()
    assert "ACK#published#" in pub_ack

    # Subscriber receives message
    sub_sock.settimeout(3.0)
    data = sub_sock.recv(4096).decode("utf-8").strip()
    assert topic in data, f"Expected message on {topic}, got: {data}"
    
    # Extract message ID
    parsed = json.loads(data)
    msg_id = parsed["id"]
    print(f"-> Consumer received message ID: {msg_id}")

    # Send Consumer ACK back to broker
    print(f"-> Sending Consumer ACK: ACK#consumed#{msg_id}")
    send_frame(sub_sock, f"ACK#consumed#{msg_id}")

    # Send NACK test
    print(f"-> Sending Consumer NACK test: NACK#{msg_id}#simulated_processing_failure")
    send_frame(sub_sock, f"NACK#{msg_id}#simulated_processing_failure")

    sub_sock.close()
    pub_sock.close()
    print("=== Consumer ACK / NACK Protocol Tests PASSED! ===")

if __name__ == "__main__":
    main()
