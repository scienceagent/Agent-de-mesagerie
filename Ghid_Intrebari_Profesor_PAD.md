# Ghid de Răspunsuri la Întrebările Profesorului (Proiect PAD - Nota 10)

Acest ghid conține răspunsurile tehnice, academice și precise, raportate exact la codul din proiectul nostru.

---

## 1. Versiune & Git

### • Care comandă din Git unește 2 branch-uri?
**Răspuns:**  
Comanda este `git merge <nume_branch>` (de exemplu: ne poziționăm pe branch-ul țintă cu `git checkout main` și rulăm `git merge feature/part1-sockets`).  
*Mențiune pentru nota 10:* Există și comanda `git rebase <nume_branch>`, diferența fiind că `merge` păstrează istoricul real de ramificare prin crearea unui commit de îmbinare (merge commit), pe când `rebase` rescrie istoria liniar.

### • Câte branch-uri ați avut?
**Răspuns:**  
*„Am adoptat o strategie de lucru colaborativă (Git Flow):*
1. `main` — branch-ul de producție (stabil, cu versiunile finale testate).
2. `develop` — branch-ul de integrare continuă.
3. Feature branch-uri: `feature/sockets-core` (pentru logica brokerului), `feature/python-clients` (pentru UI Tkinter și clienții Python) și `feature/docker-compose` (pentru containerizare și deployment).”

---

## 2. Formate de Date & Serializare

### • Diferența dintre JSON și XML
| Criteriu | JSON (JavaScript Object Notation) | XML (eXtensible Markup Language) |
| :--- | :--- | :--- |
| **Sintaxă** | Perechi cheie-valoare și liste (`{ "k": "v" }`) | Noduri arborescente cu tag-uri (`<k>v</k>`) |
| **Dimensiune/Overhead** | Foarte compact, consum redus de bandă | Voluminos din cauza tag-urilor de deschidere/închidere |
| **Tipuri de date** | Nativ suportă String, Number, Boolean, Array, Object, Null | Fără schemă, toate valorile sunt tratate ca text |
| **Validare** | JSON Schema (mai relaxată) | Validare formală riguroasă prin XSD și DTD |
| **Spații de nume** | Nu suportă nativ | Suportă Namespaces (previne coliziuni de nume) |

### • Ce este serializarea?
**Răspuns:**  
Serializarea este procesul de transpunere a stării unui obiect din memoria volatilă RAM (de exemplu, o instanță a clasei C# `Payload`) într-o secvență liniară de octeți (string JSON sau XML), cu scopul de a fi transmis prin rețea pe un Socket TCP sau salvat pe disc într-un fișier.

### • Ce este deserializarea și în ce îl transformă?
**Răspuns:**  
Deserializarea este operația inversă: preia șirul brut de octeți/text primit prin socket (sau citit din jurnal) și **îl reconstruiește într-un obiect puternic tipizat în memorie** (în cazul nostru, un obiect de tip `Payload` cu proprietățile `Id`, `Topic`, `Message`, `Timestamp`, `Sender`).

### • Dacă are avantaj XML față de JSON?
**Răspuns:**  
**Da, categoric.** XML are 3 mari avantaje în sisteme enterprise:
1. **Atribute pe noduri:** permite metadate direct în etichetă (ex: `<payload id="123" timestamp="2026-09-24">...</payload>`), fără a crea obiecte imbricate.
2. **Validare strictă prin XSD (XML Schema Definition):** asigură că datele primite respectă contractul înainte de parsare.
3. **Namespaces (Spații de nume):** permite combinarea datelor din surse diferite în același mesaj fără conflicte de denumire a câmpurilor.

---

## 3. Șabloane de Proiectare (Design Patterns)

### • Ce avantaj are șablonul Mediator?
**Răspuns:**  
În arhitectura noastră, **Brokerul joacă rolul de Mediator**:
1. **Decuplare completă (Loose Coupling):** Publisherii nu știu cine sunt Subscriberii, câte instanțe există sau la ce adrese IP se află aceștia.
2. **Topologie Stea ($N + M$ în loc de $N \times M$):** Fără mediator, fiecare publisher ar trebui să mențină conexiuni directe cu fiecare subscriber. Prin Mediator, toți se conectează la un punct unic.
3. **Centralizarea politicilor:** Filtrarea pe topicuri, persistența, securitatea și conversia de format (Adapter Pattern XML ↔ JSON) sunt controlate într-un singur loc.

---

## 4. Baze de Date: SQLite vs MySQL / SQL Server

### • În ce cazuri folosim SQLite și în ce cazuri MySQL sau SQL Server? Care-s avantajele?
**Răspuns:**
- **SQLite (Bază de date Embedded / Serverless):**
  - *Când folosim:* Aplicații locale desktop, mobile (Android/iOS), unelte CLI, microservicii cu stocare locală sau prototipuri.
  - *Avantaje:* Zero configurare (nu necesită daemon de server), este un simplu fișier pe disc (`.db`), extrem de rapid la citiri.
  - *Limitări:* Suport slab pentru scrieri masive concurente (blochează întregul fișier la scriere - database lock).
- **MySQL / SQL Server (Sisteme Client-Server Enterprise):**
  - *Când folosim:* Aplicații web mari, sisteme bancare, platforme cu mii de utilizatori concurenți care scriu date simultan.
  - *Avantaje:* Server dedicat, mecanisme avansate de concurență (Row-Level Locking), clusterizare, replicare Master-Slave, backup la cald (point-in-time recovery) și gestiune complexă de tranzacții distribuite ACID.
- *Mențiune de 10 pentru proiectul nostru:*  
  *„Pentru brokerul nostru de mesaje, nu am folosit nici SQL, nici SQLite, ci un **Append-Only File (Write-Ahead Log)**, deoarece o bază de date SQL clasică ar fi adăugat o latență uriașă prin reindexare B-Tree la fiecare mesaj primit (exact arhitectura Kafka).”*

---

## 5. Structuri de Date & Complexități

### • Ce structură de date folosim ca să păstrăm abonații? În ce colecție se păstrează?
**Răspuns:**  
În clasa `ConnectionStorage.cs`:
- Conexiunile active se păstrează într-o colecție **`List<ConnectionInfo>`**, protejată împotriva accesului concurent printr-un mecanism de sincronizare `lock (_locker)`.
- În interiorul fiecărui obiect `ConnectionInfo`, topicurile la care este abonat clientul sunt memorate într-un **`HashSet<string>`** (ceea ce asigură că un abonat nu se poate abona de două ori la același topic).
- Alternativ, pentru rutare directă, se poate utiliza un dicționar asociativ `Dictionary<string, List<ConnectionInfo>>` unde cheia este Topicul.

### • Ce este un Dicționar?
**Răspuns:**  
Un Dicționar (`Dictionary<TKey, TValue>`) este o colecție asociativă bazată pe o **Tabelă de Dispersie (Hash Table)**, care mapează o cheie unică la o valoare asociată.

### • Care este cheia la noi și care sunt valorile?
**Răspuns:**  
- **Cheia (Key):** Numele Topicului (ex: `"stiri"`, `"pisici"`, `"tehnologie"`).
- **Valoarea (Value):** Lista de abonați interesați de acel topic (`List<ConnectionInfo>`) sau conexiunea asociată adresei IP.

### • Care este complexitatea de căutare în dicționar și în listă?
**Răspuns:**  
- **În Dicționar (`Dictionary` / Hash Table):**
  - *Cazul mediu (Average):* **$O(1)$** (timp constant — cheia este trecută prin funcția hash și se ajunge direct la index).
  - *Cazul cel mai nefavorabil (Worst-Case):* **$O(N)$** (dacă apar coliziuni masive de hash și toate cheile ajung în aceeași găleată/bucket).
- **În Listă (`List<T>`):**
  - *Căutare după valoare/predicat (`x => x.Address == addr`):* **$O(N)$** (timp liniar — trebuie să itereze element cu element de la cap la coadă).

---

## 6. Rețea, Socket-uri & Porturi

### • Ce I/O folosim ca să ne conectăm la broker?
**Răspuns:**  
Folosim **Network I/O peste TCP Sockets** (`System.Net.Sockets.Socket`). Conexiunea este full-duplex și utilizează operațiuni asincrone non-blocante (`BeginAccept`/`EndAccept` pentru acceptare clienți și `BeginReceive`/`EndReceive` pentru citire stream), prevenind blocarea firului principal de execuție.

### • Ce port putem folosi?
**Răspuns:**  
Orice port disponibil din spațiul **Registered Ports (1024 – 49151)** sau **Dynamic/Private Ports (49152 – 65535)**.  
Noi am ales portul **`9000`** (configurabil prin env var `BROKER_PORT`).  
*De ce nu porturi sub 1024?* Porturile 0–1023 sunt **Well-Known Ports** rezervate de sistemul de operare pentru servicii standard (HTTP 80, HTTPS 443, SSH 22) și necesită permisiuni de administrator/root pentru a fi deschise.

### • La ce IP transmite brokerul?
**Răspuns:**  
Brokerul ascultă pe `0.0.0.0` (`IPAddress.Any`), ceea ce înseamnă că acceptă conexiuni de pe toate interfețele de rețea. Când trimite mesaje înapoi către subscriberi, transmite direct către adresa IP și portul din `connection.Socket.RemoteEndPoint` alocat fiecărui client la conectare (ex: `192.168.1.45:54321` pe LAN).

---

## 7. Căderea Brokerului & Recuperarea Datelor

### • Când brokerul cade, transmite mesajele care trebuie transmise?
**Răspuns:**  
**DA!**  
1. Când un mesaj sosește la broker, înainte de a fi transmis mai departe, este salvat pe disc în fișierul persistent append-only `storage/messages.journal`.
2. Dacă procesul brokerului este oprit forțat (crash / restart), la repornire constructorul clasei `PayloadStorage` parcurge fișierul jurnal și reîncarcă mesajele în memoria RAM.
3. Când un subscriber se reconectează și cere abonare (`subscribe#topic`), brokerul execută automat procedura de **Catch-up Replay**, trimițându-i ultimele mesaje ratate de pe acel topic.

---

## 8. Thread Pool & Concurență

### • Care este avantajul că folosim Thread Pool?
**Răspuns:**  
1. **Reutilizarea resurselor (Overhead redus):** Instanțierea unui fir de execuție (Thread) al sistemului de operare este scumpă (alocă ~1 MB de memorie stivă și consumă timp CPU la context-switch). ThreadPool menține o serie de fire gata create, refolosindu-le imediat ce un task s-a încheiat.
2. **Prevenirea epuizării resurselor (Throttling):** Dacă ar sosi 10.000 de mesaje simultan, crearea a 10.000 de thread-uri ar prăbuși sistemul de operare. ThreadPool-ul reglează numărul optim de thread-uri în funcție de încărcarea procesorului.

### • Cum credeți câte thread-uri sunt în Thread Pool?
**Răspuns:**  
În .NET runtime, ThreadPool-ul este adaptiv și dinamic:
- **MinThreads:** În mod prestabilit este egal cu numărul de nuclee logice ale mașinii (ex: 8, 12 sau 16 nuclee CPU).
- **MaxThreads:** În .NET 8 pe arhitectură x64, limita superioară maximă configurată este de aproximativ **32.767 de thread-uri**.
- *În proiectul nostru:* În clasa `Worker.cs`, avem **4 workeri independenți** lansați ca task-uri de fundal, iar distribuția mesajelor către abonați se face prin `Parallel.ForEach`, care alocă automat thread-uri din .NET ThreadPool conform capacității CPU-ului.

---

## 9. Cum începem prezentarea proiectului?

### • „Când prezentăm începem cu problema”
**Cum formulezi introducerea în fața profesorului:**  
> *„Bună ziua. Problema fundamentală pe care o rezolvă proiectul nostru este **comunicarea asincronă, fiabilă și decuplată între sisteme distribuite eterogene** (scrise în limbaje diferite: C# și Python).*  
> *Într-un sistem punct-la-punct clasic, dacă receptorul este offline sau lent, emițătorul se blochează, iar datele se pierd. Soluția noastră este un **Message Broker** de înaltă performanță bazat pe șablonul Publish/Subscribe, care oferă persistență la cădere, conversie automată a formatelor XML/JSON și scalare prin Worker Pool concurent.”*
