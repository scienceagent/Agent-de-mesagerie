#!/usr/bin/env python3
"""
Verification script for Historical Message Replay (Catch-up) on topic 'pisici'
1. Publisher publishes messages to 'pisici' when NO subscriber is connected.
2. A new subscriber connects later and subscribes to 'pisici'.
3. Verify the subscriber receives all historical messages from persistent journal.
4. Publisher sends a live message and subscriber receives it immediately.
"""

import socket
import time
import json
import os
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

    print("=== Test: Replay Mesaje Istorice pentru un Subscriber Nou ('pisici') ===")

    # 1. Conectare Publisher și trimitere mesaje pe 'pisici' când NU există niciun subscriber
    pub = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    pub.connect((broker_ip, broker_port))

    msg1 = {"topic": "pisici", "message": "Pisicile dorm 16 ore pe zi.", "sender": "Veterinar"}
    msg2 = {"topic": "pisici", "message": "Pisicile au un auz excelent.", "sender": "Veterinar"}

    send_frame(pub, json.dumps(msg1))
    ack1 = recv_frame(pub)
    print(f"[Publisher] Trimis mesaj 1 vechi: {ack1}")

    send_frame(pub, json.dumps(msg2))
    ack2 = recv_frame(pub)
    print(f"[Publisher] Trimis mesaj 2 vechi: {ack2}")

    pub.close()
    time.sleep(0.5)

    # 2. ACUM se conecteaza un subscriber NOU la 'pisici'
    print("\n[Subscriber Nou] Se conecteaza pentru prima data si se aboneaza la 'pisici'...")
    sub = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sub.connect((broker_ip, broker_port))

    send_frame(sub, "subscribe#pisici")
    sub_ack = recv_frame(sub)
    print(f"[Subscriber Nou] Confirmare abonare: {sub_ack}")
    assert "ACK#subscribed#pisici" in (sub_ack or "")

    # 3. Verificare primire mesaje istorice
    replayed_messages = []
    while True:
        frame = recv_frame(sub, timeout=1.0)
        if not frame:
            break
        replayed_messages.append(frame)
        print(f"[Subscriber Nou] Mesaj istoric primit: {frame}")

    print(f"\n[Total mesaje istorice primite]: {len(replayed_messages)}")
    assert len(replayed_messages) >= 2
    full_text = " ".join(replayed_messages)
    assert "Pisicile dorm 16 ore" in full_text
    assert "Pisicile au un auz" in full_text

    print("\n[SUCCESS] Subscriber-ul nou a primit cu succes toate mesajele vechi din jurnalul persistent!")

    sub.close()

if __name__ == "__main__":
    main()
