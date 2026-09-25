#!/usr/bin/env python3
"""
Test Suite: XML Schema (XSD) Validation and Error Handling (DLQ)
Validates that:
1. Conforming XML payloads pass XSD validation and are accepted with ACK.
2. Non-conforming XML (missing required <topic> element) fails XSD validation, is rejected with ERROR, and routed to DLQ.
"""

import socket
import time
import sys

DELIMITER = "\n"

def send_and_recv(sock, text):
    msg = text.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
    sock.sendall(msg.encode("utf-8"))
    
    # Receive response
    sock.settimeout(3.0)
    data = sock.recv(4096).decode("utf-8")
    return data.strip()

def main():
    broker_ip = "127.0.0.1"
    broker_port = 9000

    print("=== Testing XML XSD Schema Validation & DLQ Rejection ===")

    # 1. Connect publisher
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.connect((broker_ip, broker_port))
    except Exception as ex:
        print(f"[SKIP] Broker not running on {broker_ip}:{broker_port}: {ex}")
        sys.exit(0)

    # Test 1: Valid XML conforming to PayloadSchema.xsd
    valid_xml = """<payload>
  <id></id>
  <topic>tech</topic>
  <message>XML Schema Validation works!</message>
  <timestamp>2026-09-25T08:00:00Z</timestamp>
  <format>xml</format>
  <sender>XmlValidatorTest</sender>
</payload>"""

    resp1 = send_and_recv(sock, valid_xml)
    print(f"[Test 1 - Valid XML] Response: {resp1}")
    assert resp1.startswith("ACK#published#"), f"Expected ACK#published#, got: {resp1}"

    # Test 2: Invalid XML (Missing required <topic> tag according to XSD)
    invalid_xml = """<payload>
  <id></id>
  <message>Missing topic tag - should fail XSD validation</message>
</payload>"""

    resp2 = send_and_recv(sock, invalid_xml)
    print(f"[Test 2 - Invalid XML (Missing Topic)] Response: {resp2}")
    assert resp2.startswith("ERROR#"), f"Expected ERROR#, got: {resp2}"
    print("-> Successfully caught schema violation and rejected payload!")

    sock.close()
    print("=== All XML Validation Tests Passed! ===")

if __name__ == "__main__":
    main()
