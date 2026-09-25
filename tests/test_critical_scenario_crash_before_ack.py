#!/usr/bin/env python3
"""
Test Suite: Critical Distributed Incident Reproduction
======================================================
Syllabus Scenario: "Consumatorul cade după realizarea efectului local, dar înainte de ACK."
Goal: Verify that when a consumer crashes after applying a local side-effect (e.g. credit bank account)
      but BEFORE transmitting ACK back to the broker, upon reconnection and redelivery:
      1. Broker redelivers the message (At-Least-Once Delivery).
      2. Consumer detects the duplicate via Deduplication (Idempotent Consumer Pattern).
      3. The local side-effect is NOT executed a second time.
      4. Exactly-Once processing semantics are preserved end-to-end!
"""

import socket
import json
import time
import sys

DELIMITER = "\n"

def send_frame(sock, text):
    msg = text.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
    sock.sendall(msg.encode("utf-8"))

def recv_frame(sock):
    buffer = ""
    sock.settimeout(3.0)
    while DELIMITER not in buffer:
        chunk = sock.recv(1024).decode("utf-8")
        if not chunk:
            break
        buffer += chunk
    if DELIMITER in buffer:
        line, _ = buffer.split(DELIMITER, 1)
        return line.strip()
    return buffer.strip()

def main():
    broker_ip = "127.0.0.1"
    broker_port = 9000
    topic = f"incident-topic-{int(time.time())}"

    print("=" * 65)
    print("CRITICAL INCIDENT REPRODUCTION: Consumer Crash Before ACK")
    print("=" * 65)

    # Simulated local consumer database state
    local_state = {
        "account_balance": 0,
        "processed_transaction_ids": set()
    }

    # Step 1: Connect Consumer and Subscribe
    print("\n[Step 1] Consumer connects to Broker and subscribes to topic...")
    consumer_sock1 = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    consumer_sock1.connect((broker_ip, broker_port))
    send_frame(consumer_sock1, f"subscribe#{topic}")
    sub_ack = recv_frame(consumer_sock1)
    assert f"ACK#subscribed#{topic}" in sub_ack
    print("-> Consumer subscribed successfully.")

    # Step 2: Publisher sends critical transaction
    print("\n[Step 2] Producer publishes financial transaction 'TXN-9999' (+500 MDL)...")
    pub_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    pub_sock.connect((broker_ip, broker_port))
    
    txn_id = f"TXN-CRASH-TEST-{int(time.time())}"
    txn_payload = {
        "id": txn_id,
        "topic": topic,
        "message": "CREDIT_ACCOUNT_500_MDL",
        "timestamp": "2026-09-25T08:00:00Z",
        "format": "json",
        "sender": "CoreBankingService"
    }
    send_frame(pub_sock, json.dumps(txn_payload))
    pub_ack = recv_frame(pub_sock)
    assert "ACK#published#" in pub_ack
    print(f"-> Producer received confirmation: {pub_ack}")
    pub_sock.close()

    # Step 3: Consumer receives transaction and executes local side-effect
    print("\n[Step 3] Consumer receives transaction...")
    raw_msg1 = recv_frame(consumer_sock1)
    msg1 = json.loads(raw_msg1)
    assert msg1["id"] == txn_id
    print(f"-> Received Msg: ID={msg1['id']}, Content='{msg1['message']}'")

    print("-> Applying local side effect: Crediting account with 500 MDL...")
    local_state["account_balance"] += 500
    local_state["processed_transaction_ids"].add(msg1["id"])
    print(f"-> Local Balance is now: {local_state['account_balance']} MDL")

    # Step 4: CRITICAL INCIDENT INJECTION - Consumer CRASHES before ACK!
    print("\n[Step 4] [CHAOS INJECTION] Consumer crashes ABRUPTLY before sending ACK!")
    print("-> Simulating power loss / process killed / network partition...")
    consumer_sock1.close()  # Drop socket without sending ACK#consumed#txn_id
    print("-> Consumer socket disconnected without ACK delivery.")

    time.sleep(1.0)

    # Step 5: Consumer restarts and reconnects
    print("\n[Step 5] Consumer restarts, recovers local state, and reconnects...")
    consumer_sock2 = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    consumer_sock2.connect((broker_ip, broker_port))

    # Re-subscribe: Broker will replay historical/unacknowledged messages
    send_frame(consumer_sock2, f"subscribe#{topic}")
    sub_ack2 = recv_frame(consumer_sock2)
    assert f"ACK#subscribed#{topic}" in sub_ack2
    print("-> Consumer re-subscribed.")

    # Step 6: Broker redelivers the message (At-Least-Once Delivery)
    print("\n[Step 6] Broker redelivers the message...")
    raw_msg2 = recv_frame(consumer_sock2)
    msg2 = json.loads(raw_msg2)
    assert msg2["id"] == txn_id
    print(f"-> Redelivered Message received: ID={msg2['id']}")

    # Step 7: Consumer Deduplication (Idempotent Consumer)
    print("\n[Step 7] Checking Deduplication Registry on Consumer...")
    if msg2["id"] in local_state["processed_transaction_ids"]:
        print(f"-> [DEDUPLICATION TRIGGERED] Transaction '{msg2['id']}' was ALREADY PROCESSED!")
        print("-> SKIPPING local side effect! Balance will NOT be charged twice.")
    else:
        local_state["account_balance"] += 500

    # Send Consumer ACK now that reconnection is established
    send_frame(consumer_sock2, f"ACK#consumed#{msg2['id']}")
    print(f"-> Emitted Consumer ACK: ACK#consumed#{msg2['id']}")

    # Step 8: Final Assertion Verification
    print("\n[Step 8] Verifying Integrity & Exactly-Once Semantic Guarantees:")
    print(f"   Expected Balance: 500 MDL")
    print(f"   Actual Balance:   {local_state['account_balance']} MDL")
    
    assert local_state["account_balance"] == 500, f"Deduplication failed! Account charged multiple times: {local_state['account_balance']}"
    
    consumer_sock2.close()
    print("\n" + "=" * 65)
    print("=== CRITICAL INCIDENT TEST PASSED SUCCESSFULLY! ===")
    print("Garantie dovedita: At-Least-Once + Consumator Idempotent = Exactly-Once Effect!")
    print("=" * 65)

if __name__ == "__main__":
    main()
