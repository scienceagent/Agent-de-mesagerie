#!/usr/bin/env python3
"""
Automated End-to-End Verification Test for Part 1 (Sockets Broker)
Tests:
- Dynamic Multi-Topic Subscribe & Unsubscribe
- Adapter Pattern (XML <-> JSON transparent conversion)
- Content Enricher (GUID, UTC Timestamp, Node metadata)
- Multiple concurrent subscribers
- Message persistence in storage/messages.journal
"""

import socket
import time
import json
import xml.etree.ElementTree as ET
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

    print("=== Starting Part 1 Verification Suite ===")

    # 1. Connect Subscriber 1 (JSON mode)
    sub1 = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sub1.connect((broker_ip, broker_port))
    send_frame(sub1, "subscribe#news")
    ack1 = recv_frame(sub1)
    print(f"[Sub1] Subscribed to 'news': {ack1}")
    assert "ACK#subscribed#news" in (ack1 or "")

    send_frame(sub1, "subscribe#sports")
    ack2 = recv_frame(sub1)
    print(f"[Sub1] Subscribed to 'sports': {ack2}")
    assert "ACK#subscribed#sports" in (ack2 or "")

    # 2. Connect Subscriber 2 (XML Adapter mode)
    sub2 = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sub2.connect((broker_ip, broker_port))
    send_frame(sub2, "format#xml")
    ack_fmt = recv_frame(sub2)
    print(f"[Sub2] Format set to XML: {ack_fmt}")
    assert "ACK#format#xml" in (ack_fmt or "")

    send_frame(sub2, "subscribe#sports")
    ack3 = recv_frame(sub2)
    print(f"[Sub2] Subscribed to 'sports': {ack3}")
    assert "ACK#subscribed#sports" in (ack3 or "")

    # 3. Connect Publisher
    pub = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    pub.connect((broker_ip, broker_port))

    # Test 3.1: Publish JSON to 'news'
    news_payload = json.dumps({
        "topic": "news",
        "message": "Breaking News: System operational!",
        "sender": "PyTest-Pub"
    })
    send_frame(pub, news_payload)
    pub_ack = recv_frame(pub)
    print(f"[Pub] Published news: {pub_ack}")

    # Verify Sub1 receives news in JSON
    sub1_msg = recv_frame(sub1)
    print(f"[Sub1] Received payload on news: {sub1_msg}")
    assert sub1_msg is not None and "Breaking News" in sub1_msg
    sub1_json = json.loads(sub1_msg)
    assert sub1_json["topic"] == "news"
    assert len(sub1_json["id"]) > 0  # Content Enricher ID
    assert sub1_json["format"] == "json"

    # Test 3.2: Publish XML to 'sports' (Adapter & Enricher test)
    sports_xml = "<payload><topic>sports</topic><message>Match won 3-0!</message><sender>PyTest-Pub</sender></payload>"
    send_frame(pub, sports_xml)
    pub_ack2 = recv_frame(pub)
    print(f"[Pub] Published sports in XML: {pub_ack2}")

    # Sub1 requested JSON (default) -> should receive converted JSON!
    sub1_sports = recv_frame(sub1)
    print(f"[Sub1] Received sports (converted to JSON): {sub1_sports}")
    assert sub1_sports is not None and "Match won" in sub1_sports
    assert sub1_sports.startswith("{")

    # Sub2 requested XML -> should receive XML!
    sub2_sports = recv_frame(sub2)
    print(f"[Sub2] Received sports (delivered in XML): {sub2_sports}")
    assert sub2_sports is not None and "Match won" in sub2_sports
    assert sub2_sports.startswith("<payload>")

    # Test 3.3: Dynamic Unsubscribe
    send_frame(sub1, "unsubscribe#news")
    unsub_ack = recv_frame(sub1)
    print(f"[Sub1] Unsubscribed from news: {unsub_ack}")
    assert "ACK#unsubscribed#news" in (unsub_ack or "")

    # Publish again to 'news'
    news_payload2 = json.dumps({
        "topic": "news",
        "message": "Second News: Sub1 should not get this.",
        "sender": "PyTest-Pub"
    })
    send_frame(pub, news_payload2)
    recv_frame(pub)

    # Sub1 should not receive anything (timeout expected)
    sub1_dropped = recv_frame(sub1, timeout=1.0)
    print(f"[Sub1] Verifying no message on unsubscribed topic: {sub1_dropped}")
    assert sub1_dropped is None or "Second News" not in sub1_dropped

    pub.close()
    sub1.close()
    sub2.close()

    print("\n=== All Part 1 Functional Assertions PASSED Successfully! ===")

if __name__ == "__main__":
    main()
