# Document Tehnic de Argumentare - Agent de Mesagerie (Partea 1)

Acest document răspunde direct la cerințele din "Partea 1" a caietului de sarcini și oferă justificările tehnice pentru deciziile de arhitectură.

## 1. Protocolul de Transport: De ce TCP și nu UDP? (Cerința 2.1)

Alegerea protocolului TCP (Transmission Control Protocol) în detrimentul UDP (User Datagram Protocol) a fost o decizie critică pentru asigurarea fiabilității sistemului.

**Argumente Pro-TCP:**
- **Garanția Livrării (Reliability):** TCP garantează că toate pachetele ajung la destinație prin mecanisme de Acknowledgment (ACK) și retransmisie automată în caz de pierdere.
- **Ordonarea Datelor (Sequencing):** Fragmentele unui payload JSON sau XML mare ar putea ajunge dezordonate. TCP le reasamblează automat în ordinea corectă, asigurând că deserializarea nu eșuează.
- **Controlul Fluxului și al Congestiei:** TCP ajustează rata de transfer pentru a nu copleși brokerul sau subscriberul.

**De ce nu UDP?**
UDP trimite datagrame „fire-and-forget”. Dacă o singură datagramă se pierde sau este coruptă, întregul mesaj XML/JSON devine malformat. Într-un sistem de mesagerie critic (precum date financiare sau notificări stricte), pierderea unui mesaj este inacceptabilă. UDP ar fi fost potrivit doar dacă implementam telemetrie (senzori) sau streaming video, unde pierderea unui frame este tolerată.

## 2. Numărul de Canale și Structura Comunicației (Cerința 1.2)

Am implementat un model bazat pe **Multiplexare prin Topicuri Logice pe o singură conexiune fizică**.

**Arhitectura Aleasă:**
Fiecare client deschide o *singură conexiune TCP persistentă* către broker. Peste această conexiune fizică bidirecțională, se creează un număr infinit, variabil, de *canale logice unidirecționale* bazate pe **Topicuri** (ex. `sports`, `news`, `cats`).

**Argumentație tehnică:**
1. Dacă am fi alocat un port TCP (canal fizic) separat pentru fiecare tip de mesaj, sistemul de operare și firewall-ul ar fi fost copleșite (File Descriptor Exhaustion).
2. Prin folosirea unui singur socket cu parsarea de metadata (Topic), brokerul realizează un *Publish-Subscribe (Pub/Sub) 1-la-N* extrem de scalabil.
3. Este abordarea standard din industrie (folosită de AMQP în RabbitMQ și de Kafka).

## 3. Politici de Livrare și Gestionarea Eșecurilor (Cerința 1.4)

Pentru a asigura reziliența, am implementat următoarele politici:
- **Dead-Letter Queue (DLQ):** Orice mesaj care nu poate fi procesat (payload corupt, deserializare eșuată) nu este pierdut, ci este izolat în `storage/dead_letter.journal` pentru audit. Emițătorul primește `ERROR#malformed_payload`.
- **Idempotența la abonare:** Clienții nu pot să se aboneze de două ori pe același topic, prevenind dublarea mesajelor (`INFO#already_subscribed#<topic>`).
- **Durable Subscriptions (Replay):** Dacă un client pică și revine, are opțiunea ca la re-abonare (`subscribe#topic`) să ceară replay istoric pentru mesajele pe care le-a ratat, extrase din `messages.journal`. Alternativ, poate cere `subscribe#topic#live` pentru a primi doar mesaje noi.

## 4. Modelul Concurent: Worker Pool (Cerința 2.2)

În loc să folosim anti-șablonul "Thread per Request" (care ar cauza supraîncărcarea memoriei la mii de mesaje), am implementat un **Worker Pool / Cron Dispatcher** (`Worker.cs`):
- Păstrăm mesajele temporar într-o coadă thread-safe (`ConcurrentQueue<Payload>`).
- 4 fire de execuție independente (Workeri) așteaptă asincron notificări.
- Workerii distribuie mesajele concurent folosind `Parallel.ForEach`. Astfel, dacă un client procesează greu, el nu va bloca livrarea mesajelor către ceilalți clienți (I/O Non-blocant asigurat).

## 5. Conversia Datelor și Nivelul Abstract

- **Adapter Pattern (Nivel Abstract de Rutare):** Emițătorul poate trimite un fișier XML. Brokerul îl înțelege, îl reține abstract ca `Payload`, și îl livrează unui client C# ca JSON (deoarece clientul C# a specificat `format#json`). Deserializarea pe broker nu alterează datele de business, doar structura de transport (Cerința 1.1).
- **Content Enricher:** Sistemul central asigură injectarea automată de metadata (GUID pentru trasabilitate, Timestamp UTC, Broker Node ID), element necesar la integrarea sistemelor distribuite.
