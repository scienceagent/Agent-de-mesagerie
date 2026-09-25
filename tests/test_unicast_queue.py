#!/usr/bin/env python3
"""
Test Suite: Unicast (Queue Point-to-Point) vs Multicast (Pub/Sub)
Validates syllabus requirement: "Aplicarea tehnicilor pentru transmisiuni unicast și multicast"

Test behavior:
1. Unicast Queues (topics starting with 'queue:'):
   - Two workers subscribe to 'queue:tasks'.
   - Producer publishes 4 tasks.
   - Competing Consumers Pattern: messages are distributed round-robin between Worker 1 and Worker 2.
   - Exactly ONE worker receives each message; total messages received = 4 (2 by Worker 1, 2 by Worker 2).
2. Multicast Topics (standard topics like 'events'):
   - Two consumers subscribe to 'events'.
   - Producer publishes 2 messages.
   - Multicast Pattern: BOTH consumers receive all messages (2 messages each, 4 total dispatches).
"""

import socket
import json
import time
import sys
import threading

DELIMITER = "\n"

def send_frame(sock, text):
    msg = text.replace("\r\n", " ").replace("\n", " ").strip() + DELIMITER
    sock.sendall(msg.encode("utf-8"))

def read_frames(sock, timeout=3.0):
    sock.settimeout(timeout)
    buffer = ""
    frames = []
    start_time = time.time()
    while time.time() - start_time < timeout:
        try:
            chunk = sock.recv(4096).decode("utf-8")
            if not chunk:
                break
            buffer += chunk
            while DELIMITER in buffer:
                line, buffer = buffer.split(DELIMITER, 1)
                line = line.strip()
                if line:
                    frames.append(line)
        except socket.timeout:
            break
        except Exception:
            break
    return frames

def main():
    broker_ip = "127.0.0.1"
    broker_port = 9000

    print("=== Testing Unicast (Queue) vs Multicast (Pub/Sub) ===")

    try:
        w1_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        w1_sock.connect((broker_ip, broker_port))

        w2_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        w2_sock.connect((broker_ip, broker_port))

        pub_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        pub_sock.connect((broker_ip, broker_port))
    except Exception as ex:
        print(f"[SKIP] Broker not reachable at {broker_ip}:{broker_port}: {ex}")
        sys.exit(0)

    queue_topic = f"queue:tasks-{int(time.time())}"
    multicast_topic = f"events-{int(time.time())}"

    # 1. Subscribe Worker 1 and Worker 2 to Unicast Queue
    send_frame(w1_sock, f"subscribe#{queue_topic}#live")
    send_frame(w2_sock, f"subscribe#{queue_topic}#live")
    time.sleep(0.3)

    # 2. Subscribe Worker 1 and Worker 2 to Multicast Topic
    send_frame(w1_sock, f"subscribe#{multicast_topic}#live")
    send_frame(w2_sock, f"subscribe#{multicast_topic}#live")
    time.sleep(0.3)

    # Drain initial subscription ACKs
    w1_frames = read_frames(w1_sock, timeout=0.5)
    w2_frames = read_frames(w2_sock, timeout=0.5)
    print(f"Worker 1 setup ACKs: {w1_frames}")
    print(f"Worker 2 setup ACKs: {w2_frames}")

    # 3. Publish 4 messages to the UNICAST queue
    print("\n--- Publishing 4 tasks to Unicast Queue ---")
    queue_msgs = []
    for i in range(1, 5):
        msg = {
            "id": f"task-uuid-{i}",
            "topic": queue_topic,
            "message": f"Task payload #{i}",
            "timestamp": "2026-09-25T11:00:00Z",
            "format": "json",
            "sender": "TaskDispatcher"
        }
        send_frame(pub_sock, json.dumps(msg))
        pub_ack = pub_sock.recv(1024).decode("utf-8").strip()
        assert "ACK#published#" in pub_ack
        queue_msgs.append(msg)
        time.sleep(0.1)

    time.sleep(0.5)

    # Read queue messages received by Worker 1 and Worker 2
    w1_queue_received = []
    w2_queue_received = []

    for f in read_frames(w1_sock, timeout=1.0):
        if f.startswith("{") and queue_topic in f:
            w1_queue_received.append(json.loads(f))
            send_frame(w1_sock, f"ACK#consumed#{json.loads(f)['id']}")

    for f in read_frames(w2_sock, timeout=1.0):
        if f.startswith("{") and queue_topic in f:
            w2_queue_received.append(json.loads(f))
            send_frame(w2_sock, f"ACK#consumed#{json.loads(f)['id']}")

    print(f"Worker 1 received {len(w1_queue_received)} queue tasks: {[m['message'] for m in w1_queue_received]}")
    print(f"Worker 2 received {len(w2_queue_received)} queue tasks: {[m['message'] for m in w2_queue_received]}")

    # Check Unicast invariants:
    total_queue_received = len(w1_queue_received) + len(w2_queue_received)
    assert total_queue_received == 4, f"Expected total 4 queue messages, got {total_queue_received}"
    assert len(w1_queue_received) == 2, f"Expected Worker 1 to receive exactly 2 tasks, got {len(w1_queue_received)}"
    assert len(w2_queue_received) == 2, f"Expected Worker 2 to receive exactly 2 tasks, got {len(w2_queue_received)}"

    # Ensure no duplicates between workers (disjoint sets)
    w1_ids = {m["id"] for m in w1_queue_received}
    w2_ids = {m["id"] for m in w2_queue_received}
    assert w1_ids.isdisjoint(w2_ids), f"Collision detected! Both workers processed same message: {w1_ids.intersection(w2_ids)}"
    print("[PASS] Unicast Queue: 4 tasks evenly distributed round-robin (2 to Worker 1, 2 to Worker 2), zero collisions.")

    # 4. Publish 2 messages to the MULTICAST topic
    print("\n--- Publishing 2 messages to Multicast Topic ---")
    for i in range(1, 3):
        msg = {
            "id": f"event-uuid-{i}",
            "topic": multicast_topic,
            "message": f"Broadcast event #{i}",
            "timestamp": "2026-09-25T11:00:00Z",
            "format": "json",
            "sender": "EventPublisher"
        }
        send_frame(pub_sock, json.dumps(msg))
        pub_ack = pub_sock.recv(1024).decode("utf-8").strip()
        assert "ACK#published#" in pub_ack
        time.sleep(0.1)

    time.sleep(0.5)

    w1_events = [json.loads(f) for f in read_frames(w1_sock, timeout=1.0) if f.startswith("{") and multicast_topic in f]
    w2_events = [json.loads(f) for f in read_frames(w2_sock, timeout=1.0) if f.startswith("{") and multicast_topic in f]

    print(f"Worker 1 received {len(w1_events)} multicast events: {[m['message'] for m in w1_events]}")
    print(f"Worker 2 received {len(w2_events)} multicast events: {[m['message'] for m in w2_events]}")

    assert len(w1_events) == 2, f"Expected Worker 1 to receive 2 broadcast events, got {len(w1_events)}"
    assert len(w2_events) == 2, f"Expected Worker 2 to receive 2 broadcast events, got {len(w2_events)}"
    print("[PASS] Multicast: All subscribers received all published broadcast messages.")

    w1_sock.close()
    w2_sock.close()
    pub_sock.close()

    print("\n=== Unicast vs Multicast Test Suite: ALL ASSERTIONS PASSED ===")

if __name__ == "__main__":
    main()
