# Distributed Message Broker

A TCP-based distributed message broker implementing the Publisher/Subscriber pattern with multi-language support.

## Architecture

- **Broker** (.NET 8 / C#) — Core routing engine with persistent message storage and Worker Pool Dispatcher.
- **Publisher** (.NET 8 / C#) — Sends messages in JSON or XML format.
- **Subscriber** (.NET 8 / C#) — Interactive console client with multi-topic support.
- **Python Clients** — Polyglot publisher, CLI subscriber, and dedicated Tkinter GUI subscriber.

## Prerequisites

| Component | Version |
|-----------|---------|
| .NET SDK  | 8.0+    |
| Python    | 3.9+    |
| Docker & Docker Compose | (Optional for containerized deployment) |

---

## Running with Docker (Deployment)

You can launch the entire Broker service in an isolated Linux container with persistent storage with a single command.

### 1. Build and Start the Broker Container

```bash
docker compose up -d --build
```

- Broker starts in the background, listening on port `9000`.
- Data persistence is automatically handled via the `broker-storage` Docker volume.

### 2. View Logs & Status

```bash
# View live logs
docker compose logs -f broker

# Check container status
docker compose ps
```

### 3. Connect Clients (Host Machine or LAN)

Once the container is running, any client can connect to `localhost:9000`:

```bash
# Python GUI Subscriber
python Clients/Python/subscriber_ui.py --name "Alice"

# Python Publisher
python Clients/Python/publisher.py

# C# Subscriber
dotnet run --project Subscriber/Subscriber.csproj

# C# Publisher
dotnet run --project Publisher/Publisher.csproj
```

### 4. Stop the Broker Container

```bash
docker compose down
```

---

## Running Locally (Without Docker)

### 1. Start the Broker

```bash
dotnet run --project Broker/Broker.csproj
```

### 2. Start a Subscriber

**C# Console Subscriber:**
```bash
dotnet run --project Subscriber/Subscriber.csproj
```

**Python GUI Subscriber (dedicated window per instance):**
```bash
python Clients/Python/subscriber_ui.py --name "Alice"
```

### 3. Start a Publisher

**C# Publisher:**
```bash
dotnet run --project Publisher/Publisher.csproj
```

**Python Publisher:**
```bash
python Clients/Python/publisher.py
```

---

## Key Features

- **Multi-topic subscriptions** — subscribe to multiple topics simultaneously.
- **Single subscription enforcement (Idempotency)** — subscribing twice to the same topic is rejected.
- **Durable vs Ephemeral Subscriptions** — `subscribe#topic` gets historical messages (replay). `subscribe#topic#live` gets only new messages.
- **Dead-Letter Queue (DLQ)** — Invalid or malformed payloads are securely logged to `storage/dead_letter.journal` instead of crashing the broker.
- **Adapter Pattern** — XML publisher → JSON subscriber (broker converts transparently).
- **Content Enricher** — every message gets enriched with UUID, UTC timestamp, and broker node metadata.
- **Persistent storage** — messages survive broker restarts via append-only journal (`storage/messages.journal`).
- **Concurrent Worker Pool** — 4 parallel dispatch workers; a slow subscriber never blocks others.

## Protocol Reference

| Command | Direction | Description |
|---------|-----------|-------------|
| `subscribe#<topic>` | Client → Broker | Subscribe to a topic (with history) |
| `subscribe#<topic>#live` | Client → Broker | Subscribe to a topic (live messages only) |
| `unsubscribe#<topic>` | Client → Broker | Unsubscribe from a topic |
| `history#<topic>` | Client → Broker | Fetch historical messages explicitly |
| `format#json\|xml` | Client → Broker | Set preferred receive format |
| `ACK#subscribed#<topic>` | Broker → Client | Subscription confirmed |
| `ERROR#malformed_payload` | Broker → Client | DLQ triggered for invalid data |

## Testing

With the broker running:
```bash
python tests/test_part1.py
python tests/test_idempotent_subscription.py
python tests/test_history_replay.py
```
