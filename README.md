# Agent de Mesagerie Distribuit Rezilient (Distributed Message Broker)

> **Universitatea Tehnică a Moldovei (UTM) — Facultatea Calculatoare, Informatică și Microelectronică (FCIM)**  
> **Curs:** Dezvoltarea Sistemelor Distribuite (Distributed Systems)  
> **Etapa 1 / Incrementul 1:** *Comunicare fiabilă și mesagerie în prezența latenței, eșecurilor și duplicatelor.*

---

## 1. Viziune și Context Arhitectural

Acest proiect implementează un **Agent de Mesagerie (Message Broker) Distribuit și Rezilient**, conceput conform cerințelor riguroase din programa universitară UTM FCIM. Obiectivul central nu este doar livrarea mesajelor în condiții nominale, ci **garantarea integrității datelor în prezența latenței de rețea, prăbușirii abrupte a nodurilor (crash faults), duplicării mesajelor și pierderii confirmărilor (lost ACKs)**.

Sistemul decuplează complet producătorii (Publishers) de consumatori (Subscribers) din punct de vedere spațial și temporal, oferind o arhitectură asincronă, orientată pe evenimente (Event-Driven Architecture).

```
                      +---------------------------------------+
                      |   PUBLISHERS (Polyglot Clients)       |
                      |   - Python GUI (Tkinter)              |
                      |   - Python CLI                        |
                      |   - C# .NET Console                   |
                      +-------------------+-------------------+
                                          | TCP Socket [Payload + \n Framing]
                                          v
+-----------------------------------------------------------------------------------+
|                        DISTRIBUTED MESSAGE BROKER (.NET 8)                        |
|                                                                                   |
|  +---------------------+   +-----------------------+   +-----------------------+  |
|  | Socket Listener     |-->| Framing & Demuxer     |-->| Serialization Layer   |  |
|  | (Port 9000, async)  |   | (Delimiter \n Stream) |   | (JSON & XML DOM/SAX)  |  |
|  +---------------------+   +-----------------------+   +-----------+-----------+  |
|                                                                    |              |
|                                         +--------------------------+              |
|                                         v                                         |
|  +------------------+      +---------------------------+   +-------------------+  |
|  | Dead-Letter      |<-----| Validation & Enricher     |-->| Monotonic Sequence|  |
|  | Queue (DLQ)      | (err)| - XSD Schema Validator    |   | Number Generator  |  |
|  | (dead_letter.jrn)|      | - UUID & UTC Timestamp    |   | (Per-Topic FIFO)  |  |
|  +------------------+      +-------------+-------------+   +---------+---------+  |
|                                          |                           |            |
|                                          v                           v            |
|  +-----------------------------------------------------------------------------+  |
|  | Persistent Storage Engine (Append-Only Journal: storage/messages.journal)   |  |
|  +---------------------------------------+-------------------------------------+  |
|                                          |                                        |
|                                          v                                        |
|  +-----------------------------------------------------------------------------+  |
|  | Worker Dispatch Pool (4 Concurrent Dispatch Threads)                        |  |
|  | - Multicast Fan-out: Topics standard (1 -> N broadcast)                     |  |
|  | - Unicast Point-to-Point: Queues (queue:*) Round-Robin Competing Consumers |  |
|  | - Adapter Pattern: Format Negotiation (Convert to XML or JSON dynamically)  |  |
|  | - In-Flight Message Registry (Awaiting Consumer ACKs)                       |  |
|  +---------------------------------------+-------------------------------------+  |
+------------------------------------------|----------------------------------------+
                                           | TCP Socket [Delimited Frames]
                                           v
                      +--------------------+------------------+
                      |   SUBSCRIBERS (Polyglot Clients)      |
                      |   - Python GUI (Tkinter Table + Feed) |
                      |   - Python CLI (Competing Workers)   |
                      |   - C# .NET Console Clients           |
                      |                                       |
                      |   * Idempotent Deduplication Cache *  |
                      |   * Auto ACK/NACK Feedback Loop *     |
                      +---------------------------------------+
```

---

## 2. Pilonii Tehnici și Funcționalitățile Implementate

### 2.1. Nivelul de Rețea și Protocolul de Încadrare (Framing)
* **Transport:** TCP Sockets nativ (`System.Net.Sockets.Socket` în C#, modulul `socket` în Python).
* **Soluționarea problemei "Socket Stickiness" (TCP Packet Splitting & Concatenation):** TCP este un protocol de flux continuu de octeți (byte stream), fără delimitare nativă de mesaje. Pentru a preveni fragmentarea și lipirea pachetelor, am implementat **Delimiter-Based Framing** cu separator newline (`\n` / `0x0A`).
* **Demultiplexare:** Clasa `Common/MessageFraming.cs` gestionează acumularea în buffer și extragerea atomică a cadrelor valide.

### 2.2. Validare XML (XSD Schema) și Modelele de Prelucrare DOM vs. SAX
Sistemul respectă cerința expresă a programei UTM privind studiul modelelor XML:
* **Contractul XSD (`Common/PayloadSchema.xsd`):** Definește schema strictă a pachetului (`topic`, `message`, `sender`, `id`, `timestamp`, `sequence_number`).
* **Modelul SAX (`XmlReader`):** Folosit pentru parsare secvențială, streaming de mare viteză și consum minim de memorie (O(1) overhead) la inspecția antetelor.
* **Modelul DOM (`XDocument`):** Folosit pentru validare structurală completă în memorie împotriva schemei XSD și manipulare arborescentă înainte de serializare.
* **Rejecție în DLQ:** Orice payload XML care încalcă schema XSD sau este malformat este respins cu `ERROR#malformed_payload` și izolat în Dead-Letter Queue.

### 2.3. Ordonarea Mesajelor (FIFO) și Numere de Secvență Monotone
* Fiecare topic menține un contor atomic monoton crescător (`_topicSequences`).
* La recepționarea fiecărui mesaj, brokerul atribuie un `sequence_number` unic per topic.
* Consumatorii pot detecta mesaje lipsă, reordonări sau duplicate inspectând consecutivitatea numerelor de secvență.

### 2.4. Fiabilitate Bidirecțională: Protocolul ACK / NACK și In-Flight Tracking
Sistemul implementează confirmare pe două nivele:
1. **Producer ACK (`ACK#published#<uuid>`):** Brokerul confirmă producătorului că mesajul a fost serializat, îmbogățit (Content Enricher) și scris în jurnalul persistent.
2. **Consumer ACK (`ACK#consumed#<uuid>`):** Consumatorul confirmă explicit brokerului că a prelucrat mesajul cu succes. Brokerul elimină mesajul din registrul `InFlightMessages`.
3. **Consumer NACK (`NACK#<uuid>#<reason>`):** Dacă prelucrarea eșuează pe consumator, acesta emite un NACK, determinând brokerul să mute mesajul în Dead-Letter Queue pentru inspecție ulterioară.

### 2.5. Reziliență la Eșecuri: At-Least-Once Delivery + Consumator Idempotent
* **Problema:** Într-un sistem distribuit, un consumator poate executa un efect local (ex: debitare/creditare cont bancar), dar procesul poate cădea (crash / pană de curent) **înainte** ca pachetul TCP ACK să ajungă la broker. Brokerul va relivra mesajul.
* **Soluția Arhitecturală:** **Idempotent Consumer Pattern**.
  * Consumatorii mențin un cache local al ID-urilor prelucrate (`_processedMessageIds` / `self.processed_ids`).
  * La recepția unui mesaj duplicat, efectul local este omis (skipped), iar confirmarea ACK este retransmisă către broker.
  * **Ecuatia garantiei:** $\text{At-Least-Once Delivery} + \text{Idempotent Consumer} = \text{End-to-End Exactly-Once Processing}$.

### 2.6. Paradigme de Transmisie: Multicast vs. Unicast
Conform cerințelor programei:
* **Multicast (Pub/Sub):** Pentru topicuri standard (ex: `news`, `events`), mesajul este distribuit în evantai (fan-out) către **toți** abonații activi.
* **Unicast (Queue / Competing Consumers):** Pentru topicurile prefixate cu `queue:` (ex: `queue:tasks`, `queue:orders`), brokerul direcționează mesajul către **un singur consumator activ**, folosind algoritmul **Round-Robin**. Acest model asigură partajarea echitabilă a sarcinii (load leveling) între lucrători concurenți fără duplicate.

### 2.7. Dead-Letter Queue (DLQ)
* Situat în `storage/dead_letter.journal`.
* Salvează payload-urile corupte, XML-urile neconforme cu schema XSD, mesajele fără topic și mesajele respinse prin NACK, prevenind blocarea cozilor principale (poison pill protection).

### 2.8. Adapter Pattern (Negociere Poliglotă de Format)
* Producătorul poate trimite în format XML, iar consumatorul poate alege să primească în JSON (sau invers). Brokerul realizează conversia structurală automată și transparentă.

---

## 3. Structura Proiectului

```
Agent-de-mesagerie/
├── Broker/                      # Motorul central de mesagerie (.NET 8)
│   ├── Program.cs               # Inițializare socket listener, worker pool, journal
│   ├── BrokerSocket.cs          # Nivelul de transport TCP asincron
│   ├── PayloadHandler.cs        # Rutare comenzi, validare, asignare secvențe, ACK/NACK
│   ├── ConnectionStorage.cs     # Gestiune conexiuni, subscripții, Round-Robin Queues
│   ├── Worker.cs                # Worker Pool concurent (4 thread-uri de distribuție)
│   ├── PayloadStorage.cs        # Jurnalizare persistentă și buffer în memorie
│   └── DeadLetterQueue.cs       # Izolare mesaje otrăvite / NACK / eronate
│
├── Common/                      # Biblioteca partajată de contracte și utilitare
│   ├── Payload.cs               # Modelul de date al mesajului cu atribute XML & JSON
│   ├── PayloadSchema.xsd        # Schema XSD de validare a mesajelor XML
│   ├── XmlValidation.cs         # Parsare hibridă SAX (XmlReader) și DOM (XDocument)
│   ├── SerializationHelper.cs   # Adapter Pattern (conversie bidirecțională XML/JSON)
│   ├── MessageFraming.cs        # Delimiter framing (\n) peste fluxul de octeți TCP
│   └── ConnectionInfo.cs        # Metadate conexiune, format preferat, InFlight tracking
│
├── Publisher/                   # Client producător în C# (.NET 8)
├── Subscriber/                  # Client consumator în C# (.NET 8) cu Deduplicare
│
├── Clients/Python/              # Clienți poligloți în Python
│   ├── publisher_ui.py          # Interfață Grafică Tkinter modernă pentru Publisher
│   ├── subscriber_ui.py         # Interfață Grafică Tkinter pentru Subscriber (Feed live)
│   ├── publisher.py             # Client consolă pentru publicare
│   └── subscriber.py            # Client consolă pentru recepție cu Idempotent Consumer
│
├── tests/                       # Suita completă de teste automate de verificare
│   ├── test_part1.py            # Verificare completă cerințe de bază Increment 1
│   ├── test_xml_validation.py   # Validare XSD Schema și rejecție în DLQ
│   ├── test_ordering.py         # Verificare ordonare FIFO și numere de secvență
│   ├── test_consumer_ack.py     # Verificare protocol Consumer ACK / NACK
│   ├── test_unicast_queue.py    # Verificare Unicast (Queue) vs Multicast (Pub/Sub)
│   └── test_critical_scenario_crash_before_ack.py # Reproducere incident critic
│
├── docker-compose.yml           # Desfășurare containerizată Broker cu volum persistent
├── Dockerfile                   # Multi-stage Docker build pentru .NET 8 runtime
└── README.md                    # Documentație tehnică și ghid de utilizare
```

---

## 4. Ghid de Rulare și Demonstrare

### Opțiunea A: Rulare cu Docker Compose (Recomandat pentru Producție/Demonstrație)

Lansează Brokerul într-un container complet izolat, cu volum de date mapat pentru persistență:

```bash
# Construiește imaginea și pornește containerul în background
docker compose up -d --build

# Urmărește logurile Brokerului în timp real
docker compose logs -f broker

# Oprirea containerului
docker compose down
```

---

### Opțiunea B: Rulare Nativă Locală (Fără Docker)

#### Pasul 1: Pornirea Brokerului
Deschide un terminal și rulează:
```bash
dotnet run --project Broker/Broker.csproj
```
Brokerul va inițializa socketul pe `0.0.0.0:9000`, va încărca mesajele din `storage/messages.journal` și va porni cele 4 thread-uri din Worker Pool.

#### Pasul 2: Pornirea Interfețelor Grafice (UI)

* **Subscriber UI (Consumator):**
  Deschide un terminal nou:
  ```bash
  python Clients/Python/subscriber_ui.py --name "Consumator-Alice"
  ```
  * Permite conectarea la broker, abonarea la topicuri (`stiri`, `sport` sau `queue:joburi`), comutarea în timp real a formatului preferat (JSON / XML) și inspectarea pachetului brut primit.

* **Publisher UI (Producător):**
  Deschide încă un terminal:
  ```bash
  python Clients/Python/publisher_ui.py --name "Producator-Bob"
  ```
  * Permite trimiterea continuă de mesaje pe orice topic, selectarea formatului de transmisie (JSON sau XML) și vizualizarea confirmărilor `ACK#published#...`.

#### Pasul 3: Pornirea Clienților Consolă (Alternativă)
```bash
# Consumator C#
dotnet run --project Subscriber/Subscriber.csproj

# Producător C#
dotnet run --project Publisher/Publisher.csproj

# Consumator Python CLI
python Clients/Python/subscriber.py
```

---

## 5. Verificare Automată și Demonstrarea Incidentelor Critice

Toate cerințele profesorului și scenariile limită pot fi verificate prin rularea suitei de teste Python (cu Brokerul pornit):

### 1. Testul General de Integrare (Incrementul 1)
Validează subscripțiile multiple, dezabonarea dinamică, format negotiation (Adapter Pattern) și prevenirea subscripțiilor duplicat:
```bash
python tests/test_part1.py
```

### 2. Validare XML Schema (XSD) și Inspecție DLQ
Validează că mesajele XML conforme trec cu succes, iar mesajele XML corupte sau fără topic sunt respinse și stocate în `storage/dead_letter.journal`:
```bash
python tests/test_xml_validation.py
```

### 3. Ordonarea Mesajelor și Numere de Secvență (FIFO)
Validează că 5 mesaje consecutive primesc numere monotone de secvență (1..5) și sunt recepționate în ordine strictă:
```bash
python tests/test_ordering.py
```

### 4. Protocolul Consumer ACK / NACK
Validează bucla de feedback: consumatorul confirmă prelucrarea sau trimite NACK în caz de eșec local:
```bash
python tests/test_consumer_ack.py
```

### 5. Transmisiuni Unicast (Queue) vs Multicast (Pub/Sub)
Validează cerința de transmisiuni unicast și multicast:
* 4 mesaje trimise pe `queue:tasks` sunt împărțite echitabil prin Round-Robin către 2 lucrători diferiți (2 mesaje fiecare, zero coliziuni).
* 2 mesaje trimise pe un topic broadcast sunt livrate ambilor consumatori.
```bash
python tests/test_unicast_queue.py
```

### 6. Reproducerea Incidentului Critic (Crash Before ACK & Deduplicare)
Simulează un eșec catastrofal într-o tranzacție financiară:
1. Producătorul emite un credit de **500 MDL**.
2. Consumatorul aplică efectul local în cont (sold = 500 MDL).
3. **Injectare de haos:** Procesul consumatorului se prăbușește abrupt (crash) înainte de a apuca să trimită ACK-ul către broker.
4. Consumatorul repornește, brokerul îi relivrează mesajul neconfirmat.
5. Consumatorul detectează mesajul prin **Deduplication Cache**, sare peste efectul local și retrimite ACK-ul.
6. Soldul rămâne exact **500 MDL** (fără dublă debitare/creditare).
```bash
python tests/test_critical_scenario_crash_before_ack.py
```

---

## 6. Referința Protocolului de Comunicație

Toate comenzile sunt încadrate prin delimitatorul newline (`\n`):

| Comandă / Format | Direcție | Semnificație Arhitecturală |
|---|---|---|
| `subscribe#<topic>` | Client → Broker | Abonare durabilă la un topic (include replay istoric) |
| `subscribe#<topic>#live` | Client → Broker | Abonare efemeră (numai mesaje viitoare, fără istoric) |
| `subscribe#queue:<nume>` | Client → Broker | Înregistrare ca lucrător concurent pe o coadă Unicast |
| `unsubscribe#<topic>` | Client → Broker | Dezabonare dinamică de la un topic |
| `history#<topic>` | Client → Broker | Solicitare explicită a istoricului din jurnal |
| `topics#list` | Client → Broker | Interogare a topicurilor active în broker |
| `format#json\|xml` | Client → Broker | Negociere format primit (Adapter Pattern) |
| `ACK#consumed#<id>` | Consumator → Broker | Confirmare prelucrare cu succes (eliminare din in-flight) |
| `NACK#<id>#<motiv>` | Consumator → Broker | Semnalare eroare de consum (direcționare payload în DLQ) |
| `ACK#published#<id>` | Broker → Producător | Confirmare stocare și îmbogățire payload (Content Enricher) |
| `ACK#subscribed#<topic>` | Broker → Consumator | Confirmare înregistrare subscripție |
| `ERROR#malformed_payload` | Broker → Client | Rejecție din cauza încălcării schemei XSD sau parsării |

---

## 7. Argumentarea Compromisurilor Arhitecturale (Trade-offs)

1. **At-Least-Once Delivery vs. Exactly-Once Delivery pur de rețea:**
   * În conformitate cu *Teorema celor doi generali* și limitările fundamentale ale protocolului TCP/IP, livrarea exact-o-dată la nivel pur de transport este imposibilă în rețele nesigure.
   * Compromisul asumat este garantarea **At-Least-Once la nivel de transport** combinată cu **Idempotent Consumer la nivel de aplicație**, ceea ce asigură procesare efectivă Exactly-Once fără penalizări masive de performanță prin blocaje 2PC (Two-Phase Commit).
2. **Jurnal Append-Only vs. Bază de Date Relațională:**
   * Utilizarea fișierelor jurnal (`storage/messages.journal` și `storage/dead_letter.journal`) oferă o complexitate de scriere $O(1)$ și viteză maximă de I/O secvențial, eliminând dependențele externe greoaie pentru această primă etapă.
3. **Delimiter Framing (`\n`) vs. Length-Prefix Framing:**
   * Delimitarea prin caracter newline a facilitat testarea poliglotă rapidă, interoperabilitatea cu terminale și unelte standard (netcat/telnet) și parsarea facilă în Python și C#, păstrând simplitatea și claritatea cerută de o verificare universitară.
