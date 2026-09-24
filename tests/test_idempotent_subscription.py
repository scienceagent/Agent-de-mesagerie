#!/usr/bin/env python3
"""
Verification of Idempotent Subscriptions:
Ensures a subscriber can only subscribe ONCE to any given topic.
1. Connect subscriber and subscribe to 'stiri'.
2. Verify ACK#subscribed#stiri.
3. Attempt second subscription to 'stiri'.
4. Verify broker responds with INFO#already_subscribed#stiri.
5. Publish a message to 'stiri'.
6. Verify subscriber receives EXACTLY 1 message (no duplicate delivery).
"""

import socket
import time
import json
import sys

DELIMITER = "\n"

def send_frame(sock, text):
    msg = text.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
    sock.sendall(msg.encode("utf-8"))

def recv_frame(sock, timeout=2.0):
    sock.settimeout(timeout)
    buf = ""
    start = time.time()
    while time.time() - start < timeout:
        try:
            chunk = sock.recv(1024).decode("utf-8", errors="replace")
            if not chunk:
                break
            buf += chunk
            if DELIMITER in buf:
                frame, _ = buf.split(DELIMITER, 1)
                return frame.strip()
        except socket.timeout:
            break
    return buf.strip() if buf else None

def main():
    broker_ip = "127.0.0.1"
    broker_port = 9000

    print("=== Test: Abonare Unica (Idempotenta) la Acelasi Topic ===")

    # 1. Conectare subscriber
    sub = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sub.connect((broker_ip, broker_port))

    # Prima abonare la 'stiri'
    send_frame(sub, "subscribe#stiri")
    ack1 = recv_frame(sub)
    print(f"[Subscriber] Prima cerere subscribe#stiri: {ack1}")
    assert "ACK#subscribed#stiri" in (ack1 or "")

    # A doua abonare la 'stiri' (DUPLICAT)
    send_frame(sub, "subscribe#stiri")
    ack2 = recv_frame(sub)
    print(f"[Subscriber] A doua cerere subscribe#stiri (duplicat): {ack2}")
    assert "INFO#already_subscribed#stiri" in (ack2 or "")

    # 2. Conectare publisher si trimitere mesaj
    pub = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    pub.connect((broker_ip, broker_port))

    test_msg = {"topic": "stiri", "message": "Test stire unica", "sender": "Editor"}
    send_frame(pub, json.dumps(test_msg))
    recv_frame(pub)
    pub.close()

    # 3. Subscriber-ul trebuie sa primeasca EXACT 1 mesaj, nu 2!
    msg1 = recv_frame(sub, timeout=1.5)
    print(f"[Subscriber] Mesaj 1 primit: {msg1}")
    assert msg1 is not None and "Test stire unica" in msg1

    # Verificare ca nu mai soseste un duplicat
    msg_dup = recv_frame(sub, timeout=1.0)
    print(f"[Subscriber] Verificare duplicat (trebuie sa fie None): {msg_dup}")
    assert msg_dup is None or "Test stire unica" not in msg_dup

    sub.close()
    print("\n[SUCCESS] Testul de abonare unica a trecut cu succes! Niciun duplicat nu este permis.")

if __name__ == "__main__":
    main()
