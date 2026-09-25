#!/usr/bin/env python3
"""
Test Suite: Message Ordering Guarantees (Sequence Numbers)
Validates that:
- Messages published to a topic are tagged with monotonically increasing SequenceNumbers (1, 2, 3, 4, 5...).
- Consumers receive messages in strict sequential order without gaps or inversions.
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

    print("=== Testing Message Ordering & Sequence Numbers ===")

    try:
        sub_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sub_sock.connect((broker_ip, broker_port))
        
        pub_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        pub_sock.connect((broker_ip, broker_port))
    except Exception as ex:
        print(f"[SKIP] Broker not reachable: {ex}")
        sys.exit(0)

    # Subscribe to test topic
    topic = f"ordering-test-{int(time.time())}"
    send_frame(sub_sock, f"subscribe#{topic}#live")
    sub_sock.settimeout(2.0)
    ack = sub_sock.recv(1024).decode("utf-8").strip()
    assert f"ACK#subscribed#{topic}" in ack, f"Failed subscribe: {ack}"

    # Publish 5 messages
    expected_sequences = [1, 2, 3, 4, 5]
    received_sequences = []

    for i in expected_sequences:
        payload = {
            "id": "",
            "topic": topic,
            "message": f"Message sequence payload {i}",
            "timestamp": "2026-09-25T08:00:00Z",
            "format": "json",
            "sender": "OrderingTester"
        }
        send_frame(pub_sock, json.dumps(payload))
        # Wait for pub ack
        pub_sock.settimeout(2.0)
        pub_ack = pub_sock.recv(1024).decode("utf-8").strip()
        assert "ACK#published#" in pub_ack, f"Pub ACK error: {pub_ack}"

    # Receive 5 messages on subscriber and verify sequence numbers
    sub_sock.settimeout(4.0)
    buffer = ""
    start_time = time.time()
    
    while len(received_sequences) < 5 and (time.time() - start_time) < 5:
        try:
            chunk = sub_sock.recv(4096).decode("utf-8")
            if not chunk:
                break
            buffer += chunk
            while DELIMITER in buffer:
                line, buffer = buffer.split(DELIMITER, 1)
                line = line.strip()
                if line.startswith("{"):
                    data = json.loads(line)
                    seq = data.get("sequence_number")
                    received_sequences.append(seq)
                    print(f"-> Received Msg #{seq}: '{data.get('message')}'")
        except socket.timeout:
            break

    print(f"Received sequences: {received_sequences}")
    assert received_sequences == expected_sequences, f"Ordering failed! Expected {expected_sequences}, got {received_sequences}"

    sub_sock.close()
    pub_sock.close()
    print("=== Message Ordering Verification PASSED Successfully! ===")

if __name__ == "__main__":
    main()
