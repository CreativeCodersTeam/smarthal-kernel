# Home Assistant als API-Bridge für SmartHome-Geräte

**Analyse der Integrationsmöglichkeiten** — Stand: Juli 2026

## 1. Anforderungen

Home Assistant (HA) soll als Bridge zwischen einer eigenen Anwendung und den angebundenen SmartHome-Geräten dienen. Dafür werden drei Kernfähigkeiten benötigt:

| # | Anforderung | Beschreibung |
|---|-------------|--------------|
| A1 | **Geräte-Ermittlung** | Alle in HA registrierten Geräte/Entitäten inkl. Metadaten (Hersteller, Modell, Raum/Area, Fähigkeiten) abfragen |
| A2 | **Werte senden** | Zustände setzen bzw. Aktionen auslösen (Licht schalten, Thermostat stellen, …) |
| A3 | **Push bei Änderungen** | Aktive Benachrichtigung (kein Polling), wenn sich Gerätewerte ändern |

## 2. Grundlagen

### Datenmodell

Home Assistant unterscheidet mehrere Ebenen, die für die Geräte-Ermittlung relevant sind:

- **Entity** (`light.wohnzimmer`, `sensor.temperatur_bad`): kleinste steuer-/lesbare Einheit mit `state` und `attributes`. Ein physisches Gerät hat oft mehrere Entitäten.
- **Device**: physisches Gerät (Hersteller, Modell, Firmware), gruppiert mehrere Entitäten. Verwaltet in der *Device Registry*.
- **Area / Floor / Label**: räumliche bzw. logische Zuordnung (*Area Registry*).
- **Integration / Config Entry**: die Anbindung (Zigbee, Hue, Matter, …), über die Geräte hereinkommen.

Wichtig: Die klassische REST-API arbeitet fast ausschließlich auf **Entity-Ebene** (States). Die Registries (Devices, Areas, Entity-Metadaten) sind nur über die **WebSocket-API** zugänglich.

### Authentifizierung

Alle Varianten nutzen dasselbe Auth-System:

- **Long-Lived Access Token** (empfohlen für Server-zu-Server): im HA-Benutzerprofil erzeugbar, bis zu 10 Jahre gültig, als `Authorization: Bearer <token>` (REST) bzw. in der Auth-Phase (WebSocket) verwendet.
- **OAuth2** (für Apps mit eigenem Login-Flow gegen die HA-Instanz).

---

## 3. Variante 1: REST-API

**Endpoint:** `http://<host>:8123/api/` — dokumentiert unter [developers.home-assistant.io/docs/api/rest](https://developers.home-assistant.io/docs/api/rest/)

### Beschreibung

Klassische HTTP/JSON-API. Wichtige Endpunkte:

| Methode | Pfad | Zweck |
|---------|------|-------|
| GET | `/api/states` | Alle Entitäten mit aktuellem State + Attributen (→ A1, eingeschränkt) |
| GET | `/api/states/<entity_id>` | Einzelne Entität lesen |
| POST | `/api/states/<entity_id>` | State *in HA überschreiben* (kein Gerätebefehl!) |
| POST | `/api/services/<domain>/<service>` | Service aufrufen, z. B. `light/turn_on` (→ A2) |
| GET | `/api/services` | Verfügbare Services entdecken |
| GET | `/api/config`, `/api/events` | Instanz-Infos, Event-Typen |
| POST | `/api/template` | Jinja2-Template serverseitig auswerten |
| GET | `/api/history/period/...` | Historische Werte |

```bash
# Beispiel: Licht einschalten
curl -X POST http://ha.local:8123/api/services/light/turn_on \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"entity_id": "light.wohnzimmer", "brightness_pct": 60}'
```

**Achtung:** `POST /api/states/...` ändert nur die Repräsentation in HA, nicht das reale Gerät. Zum Steuern von Geräten immer *Services* aufrufen.

### Bewertung

| Vorteile | Nachteile |
|----------|-----------|
| ✅ Sehr einfach: jedes HTTP-fähige System kann sofort loslegen | ❌ **Kein Push** — Änderungen nur per Polling erkennbar (A3 nicht erfüllt) |
| ✅ Stabil, seit Jahren unverändert, gut dokumentiert | ❌ Keine Device-/Area-Registry: nur Entitäten, keine Hersteller-/Modell-/Raumdaten (A1 nur teilweise) |
| ✅ Ideal für gelegentliche, zustandslose Zugriffe und Skripte | ❌ Polling von `/api/states` bei vielen Entitäten ineffizient (großer Payload) |
| ✅ Services-Discovery über `/api/services` | ❌ Muss in `configuration.yaml` (`api:`) aktiviert sein (bei Standard-Setup gegeben) |

**Fazit:** Gut als Ergänzung (einfache Befehle, Health-Checks, Historie), allein aber unzureichend, weil A3 (Push) fehlt und A1 nur teilweise abgedeckt ist.

---

## 4. Variante 2: WebSocket-API ⭐ (Empfehlung)

**Endpoint:** `ws://<host>:8123/api/websocket` — dokumentiert unter [developers.home-assistant.io/docs/api/websocket](https://developers.home-assistant.io/docs/api/websocket/)

### Beschreibung

Die WebSocket-API ist die API, die das HA-Frontend selbst benutzt — sie ist damit die vollständigste Schnittstelle. Nach dem Verbindungsaufbau folgt eine Auth-Phase (`auth_required` → `auth` mit Token → `auth_ok`), danach werden JSON-Nachrichten mit fortlaufender `id` ausgetauscht.

**A1 — Geräte-Ermittlung (vollständig):**

```json
{"id": 1, "type": "get_states"}
{"id": 2, "type": "config/device_registry/list"}
{"id": 3, "type": "config/entity_registry/list"}
{"id": 4, "type": "config/area_registry/list"}
```

Damit lässt sich das komplette Modell aufbauen: Device (Hersteller, Modell) → zugehörige Entitäten → Area/Raum → aktueller State. Genau das, was das HA-Frontend auch tut.

**A2 — Werte senden:**

```json
{
  "id": 5,
  "type": "call_service",
  "domain": "climate",
  "service": "set_temperature",
  "service_data": {"temperature": 21.5},
  "target": {"entity_id": "climate.bad"}
}
```

**A3 — Push bei Änderungen (zwei Mechanismen):**

1. `subscribe_events` mit `event_type: state_changed` — liefert pro Änderung ein Event mit `old_state`/`new_state` (vollständige Objekte).
2. `subscribe_entities` — optimiert: initial ein Voll-Snapshot, danach nur **Diffs** (geänderte Attribute/States), mehrere Änderungen pro Event-Loop-Tick werden gebündelt. Deutlich weniger Bandbreite/CPU bei vielen Entitäten.
3. Ergänzend: `subscribe_trigger` für gezielte Trigger (z. B. nur eine Entität, Schwellwerte).

### Bewertung

| Vorteile | Nachteile |
|----------|-----------|
| ✅ **Erfüllt A1, A2 und A3 vollständig** in einer einzigen Verbindung | ❌ Höhere Implementierungskomplexität: Verbindungs­management, Reconnect, Re-Subscribe, Heartbeat (`ping`/`pong`), ID-Korrelation |
| ✅ Echtes Push-Verfahren mit geringer Latenz (gleiche API wie das HA-Frontend) | ❌ Teile der Registry-Kommandos (`config/*`) sind formal „intern/frontend" und nicht so streng versioniert wie die REST-API — Änderungen zwischen HA-Releases möglich (in der Praxis sehr stabil) |
| ✅ `subscribe_entities` mit Diff-Übertragung: effizient auch bei hunderten Entitäten | ❌ Zustandsbehaftete Verbindung: bei Verbindungsabriss müssen States neu synchronisiert werden |
| ✅ Registries (Devices, Areas, Labels) nur hier verfügbar | |
| ✅ Fertige Client-Bibliotheken für viele Sprachen (siehe Abschnitt 8) | |

**Fazit:** Die einzige Einzelvariante, die alle drei Anforderungen nativ erfüllt. Erste Wahl für eine Bridge-Anwendung.

---

## 5. Variante 3: MQTT Statestream / Eventstream

**Doku:** [MQTT Statestream](https://www.home-assistant.io/integrations/mqtt_statestream/)

### Beschreibung

HA publiziert bei aktivierter Integration jede Zustandsänderung auf einen MQTT-Broker:

- **Statestream:** pro Entität eigene Topics im Format `base_topic/<domain>/<entity>/state` bzw. `.../attributes/<attribut>` — feingranular abonnierbar.
- **Eventstream:** komplette HA-Events (inkl. `state_changed`) als JSON auf ein Topic; kann bidirektional zwischen zwei HA-Instanzen spiegeln.

```yaml
# configuration.yaml
mqtt_statestream:
  base_topic: homeassistant_states
  publish_attributes: true
  publish_timestamps: true
```

Damit ist **A3** über einen Standard-MQTT-Client erfüllt. **A2** ist über Statestream *nicht* möglich (reiner Einweg-Export) — Befehle müssten weiterhin über REST/WebSocket gehen, oder man baut eigene MQTT-Command-Topics mit HA-Automationen, die auf diese Topics reagieren. **A1** ist nur indirekt möglich (Retained Messages durchsuchen), ohne Device-/Area-Metadaten.

### Bewertung

| Vorteile | Nachteile |
|----------|-----------|
| ✅ Entkopplung über Broker: Bridge muss HA nicht direkt erreichen, Pub/Sub-Semantik, QoS, Retained Messages als „Last Known State" | ❌ Zusätzliche Infrastruktur: MQTT-Broker (z. B. Mosquitto) muss betrieben werden |
| ✅ Feingranulares Abonnieren einzelner Entitäten (Topic-Wildcards) | ❌ **A2 nicht abgedeckt** — Rückkanal muss separat gebaut werden (REST/WS oder Automations-Konstrukt) |
| ✅ MQTT-Clients existieren für praktisch jede Plattform, sehr leichtgewichtig | ❌ **A1 nur rudimentär** — keine Registry-Metadaten (Hersteller, Modell, Area) |
| ✅ Mehrere Konsumenten gleichzeitig ohne Mehrlast für HA | ❌ Konfiguration in HA nötig (YAML), kein reiner API-Ansatz |
| | ❌ Kein Request/Response-Muster; Fehler-Feedback fehlt |

**Fazit:** Attraktiv, wenn ohnehin ein MQTT-Broker vorhanden ist oder mehrere Systeme mitlesen sollen. Als alleinige Lösung unvollständig — sinnvoll als Push-Kanal in Kombination mit REST/WebSocket für Kommandos und Discovery.

---

## 6. Variante 4: Webhooks + Automationen (HA ruft die Bridge)

### Beschreibung

Umgekehrte Richtung: HA meldet Änderungen aktiv per HTTP an die eigene Anwendung.

- **Ausgehend (A3):** Eine HA-Automation mit Trigger `state` (oder Event-Trigger) ruft per `rest_command`/`notify` einen HTTP-Endpunkt der Bridge auf und übergibt `entity_id`, `old_state`, `new_state` als Template-Payload.
- **Eingehend (A2, begrenzt):** HA-Webhook-Trigger (`/api/webhook/<id>`) — die Bridge kann ohne Token einen Webhook aufrufen, der eine Automation (z. B. Service-Aufruf) auslöst.

### Bewertung

| Vorteile | Nachteile |
|----------|-----------|
| ✅ Kein dauerhafter Verbindungsaufbau; die Bridge braucht nur einen HTTP-Server | ❌ **Konfigurationslast in HA:** für generische „alle Änderungen"-Weiterleitung sind Automationen/Blueprints zu pflegen — fragil und schwer generisch zu halten |
| ✅ Funktioniert auch durch NAT/Firewall Richtung Bridge | ❌ A1 gar nicht abgedeckt |
| ✅ Webhooks eingehend: einfacher tokenloser Trigger-Kanal | ❌ Keine Zustellgarantie, kein Replay; verpasste Calls sind verloren |
| | ❌ Webhook-IDs sind das einzige „Secret" — Sicherheitsniveau niedriger als Token-Auth |

**Fazit:** Nur für punktuelle Benachrichtigungen geeignet (z. B. „Alarm ausgelöst"), nicht als generische Bridge-Basis.

---

## 7. Variante 5: Server-Sent Events (Legacy — nicht empfohlen)

Der frühere SSE-Endpunkt `/api/stream` wurde aus dem HA-Core **entfernt**. Es existieren nur noch Community-/HACS-Integrationen, die SSE nachrüsten. Für neue Entwicklungen nicht relevant; die WebSocket-API ist der offizielle Nachfolger für Streaming.

---

## 8. Variante 6: Fertige Client-Bibliotheken / Frameworks

Statt die Protokolle selbst zu implementieren, kann eine Bibliothek die WebSocket-/REST-Details kapseln. Auswahl (relevant, da dieses Projekt .NET-basiert ist):

### .NET

| Bibliothek | Ansatz | Bewertung |
|-----------|--------|-----------|
| **[NetDaemon](https://netdaemon.xyz/)** (V5) | Vollständiges Automations-Framework: Hosting-Model, DI, `HassModel` mit **Code-Generierung** typisierter Entitäten/Services, Rx-basierte State-Subscriptions über WebSocket | ⭐ Aktiv gepflegt, sehr komfortabel; erfüllt A1–A3. Bringt aber ein eigenes App-/Hosting-Modell mit — als reine Client-Lib etwas schwergewichtig, die Pakete `NetDaemon.Client`/`HassModel` sind jedoch auch standalone nutzbar |
| **[HassClient (vicfergar)](https://github.com/vicfergar/HassClient)** | Schlanker WebSocket-Client inkl. Registry-Zugriff (Devices, Areas, Users) | Deckt A1–A3 ab, aber geringe Aktivität/Verbreitung — Wartungsrisiko |
| **[HADotNet](https://github.com/qJake/HADotNet)** | Typisierter REST-Wrapper (`ClientFactory`, States/Services/History) | Nur REST → kein Push (A3 ✗); Projekt kaum noch aktiv |

### Andere Ökosysteme (Referenz)

- **Python:** `homeassistant_api` (REST+WS), `hass-websocket-client`; außerdem AppDaemon als Framework.
- **JavaScript/TypeScript:** [`home-assistant-js-websocket`](https://www.npmjs.com/package/home-assistant-js-websocket) — die offizielle Lib des HA-Frontends, Referenzimplementierung für `subscribe_entities`.
- **Node-RED:** `node-red-contrib-home-assistant-websocket` — Low-Code-Bridge, wenn grafische Flows gewünscht sind.

| Vorteile | Nachteile |
|----------|-----------|
| ✅ Auth, Reconnect, Typisierung, Registry-Handling fertig gelöst | ❌ Abhängigkeit von Dritt-Maintainern (bei HA-Breaking-Changes auf Updates angewiesen) |
| ✅ Deutlich schnellere Time-to-Market | ❌ Frameworks (NetDaemon) geben Architektur teilweise vor |
| ✅ NetDaemon: typsichere, generierte Entity-Klassen | ❌ Abstraktion kann exotische WS-Kommandos verdecken |

---

## 9. Vergleichsmatrix

| Variante | A1 Geräte-Ermittlung | A2 Werte senden | A3 Push | Komplexität | Zusatz-Infrastruktur |
|----------|:-------------------:|:---------------:|:-------:|:-----------:|:--------------------:|
| REST-API | ⚠️ nur Entitäten/States | ✅ | ❌ (Polling) | niedrig | keine |
| **WebSocket-API** | ✅ inkl. Device/Area-Registry | ✅ | ✅ (`subscribe_entities`) | mittel | keine |
| MQTT Statestream | ⚠️ rudimentär | ❌ (nur mit Zusatzkonstrukt) | ✅ | mittel | MQTT-Broker |
| Webhooks + Automationen | ❌ | ⚠️ begrenzt | ⚠️ nur konfigurierte Fälle | mittel (HA-seitig) | keine |
| SSE (`/api/stream`) | — | — | entfernt | — | — |
| Client-Lib (.NET: NetDaemon) | ✅ | ✅ | ✅ | niedrig–mittel | keine |

## 10. Empfehlung

1. **Primär: WebSocket-API** — einzige Schnittstelle, die alle drei Anforderungen nativ und mit geringer Latenz erfüllt:
   - Discovery beim Start über `config/device_registry/list`, `config/entity_registry/list`, `config/area_registry/list` + `get_states`.
   - Kommandos über `call_service`.
   - Push über `subscribe_entities` (Diff-basiert) oder `subscribe_events`/`state_changed`.
2. **Für .NET konkret:** `NetDaemon.Client` / `NetDaemon.HassModel` als Basis evaluieren, bevor ein eigener WebSocket-Client gebaut wird — Reconnect-Logik, Typisierung und Registry-Zugriff sind dort bereits gelöst. Fallback: eigener schlanker Client mit `System.Net.WebSockets.ClientWebSocket` (Protokoll ist einfach: JSON + laufende `id`).
3. **Optional ergänzend:**
   - REST-API für einfache administrative Zugriffe, Health-Checks und Historie.
   - MQTT Statestream, falls später mehrere entkoppelte Konsumenten mitlesen sollen.
4. **Robustheit einplanen:** Reconnect mit Backoff, Re-Subscribe nach Reconnect, vollständige Resynchronisation der States nach Verbindungsverlust, Umgang mit `unavailable`/`unknown`-States.

## 11. Quellen

- [WebSocket API — Home Assistant Developer Docs](https://developers.home-assistant.io/docs/api/websocket/)
- [REST API — Home Assistant Developer Docs](https://github.com/home-assistant/developers.home-assistant/blob/master/docs/api/rest.md)
- [Home Assistant WebSocket API (Integration)](https://www.home-assistant.io/integrations/websocket_api/)
- [MQTT Statestream — Home Assistant](https://www.home-assistant.io/integrations/mqtt_statestream/)
- [Liste aller WebSocket-Endpunkte (Community-Gist)](https://gist.github.com/mhagger/f1cc7844a7736bd5258d953e0a22b398)
- [Device Registry WebSocket-Kommandos (HA Core Source)](https://github.com/home-assistant/home-assistant/blob/dev/homeassistant/components/config/device_registry.py)
- [home-assistant-js-websocket (npm)](https://www.npmjs.com/package/home-assistant-js-websocket)
- [NetDaemon](https://netdaemon.xyz/) · [NetDaemon (GitHub)](https://github.com/net-daemon/netdaemon)
- [HassClient (vicfergar)](https://github.com/vicfergar/HassClient)
- [HADotNet](https://github.com/qJake/HADotNet)
- [hass2mqtt — C#/.NET WebSocket→MQTT-Beispiel](https://github.com/idatum/hass2mqtt)
