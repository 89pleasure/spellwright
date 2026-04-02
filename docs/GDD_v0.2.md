# Game Design Document — Citybuilder
**Version:** 0.2 (Draft)
**Engine:** Unity 6 (DOTS/ECS + GameObject Hybrid)
**Sprache:** C#
**Plattform:** Desktop (Windows / macOS / Linux)
**Genre:** City-Builder / Wirtschafts- & Politiksimulation
**Setting:** Modern
**Spielmodus:** Endloses Sandbox-Gameplay

---

## Änderungshistorie

| Version | Datum | Änderungen |
|---|---|---|
| 0.1 | 2026-03 | Initiales Draft: Grundsysteme, Godot 4 |
| 0.2 | 2026-03 | Engine-Wechsel Unity 6, Bürgergruppen, politisches System, Event-Architektur |

---

## 1. Vision & Spielgefühl

### 1.1 Vision in einem Satz

> Du bist Bürgermeister einer wachsenden Metropole. Du baust keine tote Kulisse – du steuerst ein lebendiges System aus Bürgergruppen, die unterschiedliche Ziele haben und dich abwählen können, wenn du ihre Bedürfnisse ignorierst. Verkehr, Wohnraum, Arbeitsplätze und Politik sind untrennbar verwoben.

### 1.2 Emotionaler Kern

Der Spieler soll **organisches Wachstum beobachten** – die Stadt lebt ihr eigenes Leben. Unfälle passieren, Bürger pendeln, Stadtviertel gentrifizieren, Gruppen protestieren. Der Spieler ist kein Architekt der jede Entscheidung trifft, sondern ein Bürgermeister der Rahmenbedingungen setzt und dann zusieht wie die Stadt darauf reagiert.

### 1.3 Zielgruppe

**Mid-Core** – Spieler die Cities Skylines kennen und mehr politische Tiefe suchen. Komplex aber verständlich: jede Systemrückkopplung muss für den Spieler nachvollziehbar sein.

### 1.4 Alleinstellungsmerkmal

Cities Skylines und SimCity haben keinen echten politischen Druck. In diesem Spiel baut der Spieler nicht für sich selbst, sondern für eine Bevölkerung mit Erwartungen – und kann Wahlen verlieren wenn er sie ignoriert. Der politische Druck durch Bürgergruppen mit natürlichen Zielkonflikten ist das zentrale Differenzierungsmerkmal.

---

## 2. Spielwelt & Karte

### 2.1 Kartengröße & Regionen

Die Spielwelt ist groß und in mehrere geografisch getrennte **Regionen** unterteilt. Regionen sind durch natürliche Grenzen voneinander getrennt (Flüsse, Hügel, Wälder). Jede Region muss separat erschlossen werden.

| Parameter | Wert |
|---|---|
| Kartengröße gesamt | ~16 km × 16 km |
| Anzahl Regionen | 4–6 |
| Startregion | 1 (vorerschlossen, flaches Terrain) |
| Regionen freischalten | Einmalige Erschließungsgebühr |
| Max. Einwohner (Vollausbau) | 200.000+ |

### 2.2 Terrain

Flüsse, Hügel und Wälder dienen als natürliche Begrenzungen und beeinflussen Baukosten (Brücken, Tunnels). Keine dynamische Geologie oder Naturkatastrophen in v1.0.

### 2.3 Kamera

Freie 3D-Kamera mit Pan, Zoom und Rotation. Von Vogelperspektive bis Near-Ground-Level. Minimap zeigt die gesamte Karte inkl. nicht erschlossener Regionen.

---

## 3. Bürger & Soziale Gruppen

### 3.1 Bürger-Modell

Jeder Bürger ist eine eigene Entität (ECS) mit folgenden Eigenschaften:

| Attribut | Beschreibung |
|---|---|
| Heimat | Feste Wohnparzelle |
| Arbeitsplatz | Festes Gebäude in I- oder C-Zone |
| Gruppe | Zugehörigkeit zu einer Bürgergruppe |
| Stimmung | Aktueller Satisfaction-Wert (-100 bis +100) |
| Pendelroute | Gecachte Route Heimat ↔ Arbeit |

Bürger haben keine individuelle Persönlichkeit jenseits dieser Attribute. Die Tiefe entsteht durch die Gruppenzugehörigkeit und die Interaktion mit der Stadtinfrastruktur.

### 3.2 Bürgergruppen

Es gibt vier Bürgergruppen mit unterschiedlichen Bedürfnisprofilen und natürlichen Zielkonflikten:

| Gruppe | Kernbedürfnisse | Konflikt mit |
|---|---|---|
| Arbeiterklasse | Günstige Mieten, Arbeitsplätze, ÖPNV | Unternehmer (Steuern) |
| Mittelstand | Schulen, Parks, gute Straßen | Umweltbewusste (Straßenausbau) |
| Unternehmer | Niedrige Steuern, Gewerbefreiheit | Arbeiterklasse (Mindestlohn) |
| Umweltbewusste | Grünflächen, niedrige Emissionen | Mittelstand (Straßen vs. Parks) |

Jede Gruppe hat eine **Wahlmacht** (prozentualer Anteil an der Gesamtbevölkerung), die sich durch Zuzug und Abwanderung dynamisch verändert. Baut der Spieler viele günstige Wohnungen, wächst die Arbeiterklasse – und damit ihr Einfluss bei Wahlen.

### 3.3 Satisfaction-Berechnung

Pro Spieltag wird pro Bürger ein Satisfaction-Wert berechnet (Event-getrieben, nicht als globaler Tick):

```
satisfaction = 0
+ 30   Straßenanbindung vorhanden
+ 20   Stromversorgung vorhanden
+ 20   Wasserversorgung vorhanden
+ 15   Nachbar-Balance stimmt (Zonen im richtigen Verhältnis)
+ 10   Gruppenspezifisches Bedürfnis erfüllt
- 10   pro fehlender Infrastrukturstufe
- 20   Industrie direkt benachbart (nur R)
- 15   Überbevölkerung / Wohnraumknappheit
```

Satisfaction-Änderungen werden als Events gefeuert, nicht im Batch berechnet.

---

## 4. Politisches System

### 4.1 Approval-Rating

Jede Bürgergruppe hat ein eigenes **Approval-Rating** (0–100%) gegenüber dem Spieler als Bürgermeister. Das Rating aggregiert sich aus den Satisfaction-Werten aller Bürger der Gruppe plus gruppenspezifischer Politikentscheidungen.

Approval sinkt wenn:
- Gruppenspezifische Bedürfnisse unerfüllt bleiben
- Steuern erhöht werden (Unternehmer und Mittelstand reagieren stärker)
- Stadtentwicklung den Gruppeninteressen widerspricht

Approval steigt wenn:
- Bedürfnisse aktiv erfüllt werden
- Gezielte Investitionen in gruppenrelevante Infrastruktur

### 4.2 Wahlen

Alle 4 Spieljahre findet eine Wahl statt. Das Ergebnis basiert auf dem gewichteten Approval aller Gruppen – gewichtet nach deren Bevölkerungsanteil.

```
Wahlergebnis = Σ (Gruppe.Approval × Gruppe.Bevölkerungsanteil)

Ergebnis > 50%  → Wiederwahl, Spiel geht weiter
Ergebnis ≤ 50%  → Abwahl, Game Over (Neustart oder Autosave laden)
```

Der Spieler sieht die nächste Wahl im UI als Countdown. Approval-Ratings sind jederzeit einsehbar – kein verstecktes System.

### 4.3 Natürliche Zielkonflikte

Das System erzeugt organisch Dilemmas ohne künstliche Ereignisse:

- **Steuererhöhung:** Mehr Budget für Schulen → Mittelstand +, Unternehmer −
- **Autobahn durch Grünfläche:** Mittelstand + (schnellere Wege), Umweltbewusste −
- **Sozialwohnungen:** Arbeiterklasse +, Unternehmer − (niedrigere Landwerte)
- **Industriegebiet:** Arbeitsplätze für Arbeiterklasse +, Umweltbewusste −

---

## 5. Kernsysteme

### 5.1 Straßennetzwerk

Das freie Straßensystem ist das technische Fundament. Keine Gitterbindung – der Spieler zeichnet Straßen frei. Das Netzwerk wird als **Node-Graph** gespeichert (Kreuzungen = Knoten, Abschnitte = Kanten mit Spline-Kurven).

**Straßentypen:**

| Typ | Spuren | Kapazität | Kosten/m | Tempo |
|---|---|---|---|---|
| Weg | 1 | Niedrig | 10 | 30 km/h |
| Straße | 2 | Mittel | 25 | 50 km/h |
| Hauptstraße | 4 | Hoch | 60 | 70 km/h |
| Autobahn | 6 | Sehr hoch | 150 | 120 km/h |

**Parcel-Erzeugung:** Beim Zeichnen einer Straße entstehen automatisch bebaubare Parzellen (40m Tiefe) links und rechts. Parzellen sind rechteckige Grundstücke entlang der Straßenfront.

**Leitungsführung:** Strom- und Wasserleitungen verlaufen automatisch entlang gebauter Straßen. Kein manuelles Verlegen.

### 5.2 Zonensystem

| Zone | Kürzel | Funktion | Steuerbasis |
|---|---|---|---|
| Wohnzone | R | Bevölkerung wächst | Einkommensteuer |
| Gewerbezone | C | Versorgt Bewohner, schafft Jobs | Gewerbesteuer |
| Industriezone | I | Schafft Arbeitsplätze | Industriesteuer |

Gebäude haben 5 Ausbaustufen. Sie wachsen automatisch wenn Satisfaction und Demand stimmen, und verfallen wenn sie dauerhaft unzufrieden sind.

### 5.3 Stromversorgung

Verteilung per **Flood-Fill** entlang des Straßengraphen. Kapazitätswarnung bei 80%, Versorgungsstopp bei 100%.

| Typ | Kapazität | Baukosten | Betrieb/Tag |
|---|---|---|---|
| Kohlekraftwerk | 2.000 MWh | 50.000 | 500 |
| Windpark | 200 MWh | 8.000 | 50 |
| Solarpanel | 50 MWh | 2.000 | 10 |

### 5.4 Wasserversorgung

Verteilung über **Druckradius** (Circle-of-Influence). Kein manuelles Rohrnetz.

| Typ | Radius | Kapazität | Baukosten | Betrieb/Tag |
|---|---|---|---|---|
| Wasserwerk | 800m | 5.000 | 30.000 | 300 |
| Wasserturm | 400m | 1.500 | 10.000 | 80 |

### 5.5 Budget

**Startkapital:** 100.000 Credits

**Einnahmen:** Einkommensteuer (R), Gewerbesteuer (C), Industriesteuer (I) – alle proportional zu Gebäudestufen und Bevölkerung/Betrieb.

**Ausgaben:** Kraftwerksbetrieb, Wasserversorgung, Straßenunterhalt (proportional zum Netz), Regionserschließung (einmalig).

**Steuersatz:** Individuell pro Zonentyp einstellbar, 0–30% in 5%-Schritten. Hohe Steuern senken Satisfaction und beeinflussen Gruppenapprovbal.

**Bankrott:** Bei Guthaben < 0 tritt der Bankrott-Zustand ein. Kein Neubau möglich, Satisfaction sinkt stadtweit. Erholt sich das Budget nicht innerhalb von 5 Spielminuten → Game Over.

---

## 6. Verkehrssimulation

### 6.1 Pendler-Modell

Jeder Bürger pendelt täglich: Heimat → Arbeitsplatz → Heimat. Die Route wird gecacht und nur bei Änderungen am Straßennetz neu berechnet (**Dirty Flag**).

Pendelzeiten beeinflussen die Satisfaction direkt: lange Pendelzeiten senken die Stimmung, kurze erhöhen sie. Staus entstehen organisch wenn die Straßenkapazität überschritten wird.

### 6.2 Fahrzeuge

Fahrzeuge sind sichtbare Agenten auf dem Straßennetz. Sie werden mit **Simulation-LOD** verwaltet:

| LOD | Bereich | Detail | Update |
|---|---|---|---|
| 0 – Voll | Sichtbereich Kamera | Volle Animation, Kollision, A* | Jeden Frame |
| 1 – Vereinfacht | Nahe Chunks, nicht sichtbar | Interpolierte Position, kein Mesh | Alle 500ms |
| 2 – Statistisch | Weit entfernt | Nur Auslastungszahlen | Event-getrieben |

### 6.3 Verkehrsereignisse

Unfälle, Staus und Straßensperrungen entstehen als Events und lösen Kaskaden aus (siehe Abschnitt 8).

---

## 7. Simulationsereignisse (Event-Katalog)

Folgende Ereignisse existieren in v1.0 als diskrete Events im System:

| Event | Auslöser | Typische Folge-Events |
|---|---|---|
| TrafficAccident | Fahrzeugkollision | RoadBlocked, EmergencyDispatched, CitizenMoodChanged |
| Fire | Zufällig / Überlastung | EvacuationStarted, BuildingDamaged, FireSpread |
| PowerFailure | Kapazitätsüberschreitung | PowerLost (N Gebäude), SatisfactionChanged |
| CitizenDied | Alter / Unfall / Krankheit | HousingFreed, WorkplaceFreed, GriefNearby |
| Crime | Niedrige Satisfaction in Viertel | CitizenFleeing, PropertyValueChanged |
| BuildingCollapsed | Vernachlässigung / Brand | RoadBlocked, CitizenDied, EmergencyDispatched |
| ElectionTriggered | Alle 4 Spieljahre | ApprovalAggregated, MayorReelected / MayorDefeated |

---

## 8. Technische Architektur

### 8.1 Engine & Technologie-Stack

| Bereich | Technologie | Begründung |
|---|---|---|
| Engine | Unity 6 | DOTS/ECS für Agenten-Simulation, CS2-Modding-Erfahrung vorhanden |
| Agenten & Simulation | DOTS/ECS + Burst Compiler | 200k+ Bürger, parallele Job-Ausführung |
| Rendering & UI | GameObject (Hybrid) | Bewährtes System, kein ECS-Overhead für statische Objekte |
| Straßen-Mesh | C# Jobs + Splines | Asynchrone Generierung, kein Frame-Hitch |
| Persistenz | JSON + Unity Serialization | Straßengraph als Adjazenzliste |

### 8.2 Simulations-Architektur: Hybrid aus DES und Frame-Loop

Die Simulation verwendet keine einheitliche Tick-Rate. Stattdessen gibt es drei parallele Mechanismen:

**1. Frame Loop (ECS Jobs)**
Für alles was sich jedes Frame verändert. Vollständig parallelisiert via Unity Burst Compiler.
- Fahrzeug- und Fußgängerbewegung
- Rendering-Updates
- Kamera und Input

**2. Discrete Event Simulation (DES)**
Für alle diskret auftretenden Zustandsänderungen. Die Simulation arbeitet eine **Priority Queue** (Min-Heap, sortiert nach Spielzeit) ab. Die CPU schläft zwischen Events – kein Polling.
- Pendler-Routenentscheidungen
- Satisfaction-Änderungen
- Wirtschaftliche Zustandsänderungen
- Politische Events und Wahlen
- Alle Ereignisse aus dem Event-Katalog (Abschnitt 7)

**3. Dirty Flag System**
Ergänzt das DES-System für kostspielige Berechnungen. Pathfinding wird nur neu berechnet wenn ein relevanter Straßenknoten als `dirty` markiert wurde.
- Straße gebaut oder abgerissen → betroffene Knoten dirty
- Nur Bürger deren gecachte Route durch dirty-Knoten führt, berechnen neu
- Typisch: 2.000 statt 200.000 Pathfinding-Queries pro Straßenänderung

### 8.3 Event-Bus Architektur

Alle Systeme kommunizieren ausschließlich über einen zentralen **Event Bus**. Kein System referenziert ein anderes direkt.

**Events sind reine Daten-Structs** (keine Klassen, kein Heap-Allocation, keine GC-Last):

```csharp
public readonly struct TrafficAccidentEvent : ISimulationEvent {
    public readonly Entity Vehicle;
    public readonly int RoadSegmentId;
    public readonly float GameTime;
    public readonly int CascadeDepth;   // Schutz vor Endlosschleifen
}
```

**Jedes System registriert sich für genau die Events die es braucht:**

```csharp
public class TrafficSystem : IEventHandler<TrafficAccidentEvent> {
    public void Handle(TrafficAccidentEvent e) {
        // Straße sperren, neues Event mit Depth + 1 feuern
        eventBus.Publish(new RoadBlockedEvent {
            SegmentId = e.RoadSegmentId,
            CascadeDepth = e.CascadeDepth + 1
        });
    }
}
```

### 8.4 Schichtenarchitektur (Pflicht)

Events fließen ausschließlich **nach unten**. Niemals zurück nach oben.

```
Spieler-Input      →  feuert Events, empfängt nie
       ↓
Infrastruktur      →  Straßen, Strom, Wasser
       ↓
Simulation         →  Verkehr, Wirtschaft, Bürger-Satisfaction
       ↓
Politik            →  Approval, Wahlen
       ↓
Rendering          →  empfängt Events, feuert nie
```

Ein Rendering-System darf niemals ein Simulation-Event auslösen. Ein Infrastruktur-Event darf niemals direkt Politik-Events feuern – das muss durch die Simulation-Schicht laufen.

### 8.5 Schutzmechanismen gegen unkontrollierte Kaskaden

**Max Cascade Depth:** Jedes Event trägt eine `CascadeDepth`. Der Event Bus verwirft Events die das konfigurierbare Limit überschreiten und loggt eine Warnung.

**Event Deduplication:** Dasselbe Event für dieselbe Entität im selben Spielzeit-Frame wird nur einmal verarbeitet. Verhindert Doppelverarbeitung bei parallelen Kausalketten.

**Read-only Event Contracts:** Handler dürfen den Event-Struct nicht verändern. Jeder Handler arbeitet auf einer Kopie.

### 8.6 Rendering-Optimierungen

| Technik | Anwendung |
|---|---|
| GPU Instancing / `DrawMeshInstanced` | Alle Gebäude gleichen Typs: 1 Draw Call |
| Chunk-basiertes Streaming | Nur sichtbare Chunks im Speicher |
| Rendering-LOD | 3 Stufen: < 200m detail, 200–800m mid, > 800m Billboard |
| Occlusion Culling | Verdeckte Gebäude werden nicht gerendert |
| Simulation-LOD für Agenten | Voll / Vereinfacht / Statistisch (siehe 6.2) |

---

## 9. User Interface

### 9.1 HUD

- **Budget-Leiste** oben links: Guthaben, Einnahmen/Tag, Ausgaben/Tag
- **Approval-Barometer** oben rechts: Approval-Rating pro Bürgergruppe
- **Wahl-Countdown** (sichtbar ab 1 Spieljahr vor der Wahl)
- **Spielgeschwindigkeit** oben Mitte: Pause, 1×, 2×, 3×
- **Minimap** unten rechts inkl. ungenutzter Regionen

### 9.2 Bau-Werkzeuge

| Werkzeug | Funktion |
|---|---|
| Straße zeichnen | Freihand-Straße mit Typ-Auswahl |
| Zone zuweisen | Parzelle mit R / C / I markieren |
| Zone löschen | Zonenzuweisung entfernen |
| Gebäude platzieren | Kraftwerk, Wasserwerk, Sonderbauten |
| Abreißen | Straße, Zone oder Gebäude entfernen |
| Info-Modus | Klick auf Parzelle / Bürger zeigt Details |

### 9.3 Overlay-Modi

| Overlay | Zeigt |
|---|---|
| Standard | Gebäude und Straßen |
| Strom | Versorgungsabdeckung (grün / rot) |
| Wasser | Druckradien und Abdeckung |
| Satisfaction | Heatmap pro Bürger / Viertel |
| Zonen | R / C / I Färbung |
| Gruppen | Wohngebiete der Bürgergruppen |
| Verkehr | Straßenauslastung und Staus |

---

## 10. Spielfluss & Progression

Da kein Endziel existiert, entsteht Progression durch selbst erzeugte Herausforderungen und den politischen Druck der Wahlen:

**Frühe Phase:** Erste Straßen, erste R+C+I-Balance, Startkapital verwalten, erste Wahl vorbereiten.

**Mittlere Phase:** Zweite Region erschließen, Gruppeninteressen balancieren, Kapazitätsengpässe lösen, erste Wahl überstehen.

**Späte Phase:** Mehrere Regionen verwalten, Stadtteile mit Gruppencharakter entwickeln, politische Krisen meistern, Metropole ausbauen.

**Emergente Spannungen (ohne Scripting):**
- Wachstum erzeugt Kapazitätsengpässe bei Strom und Wasser
- Neue Industriegebiete schaffen Jobs aber verschlechtern Umwelt-Approval
- Günstige Wohnungen stärken die Arbeiterklasse politisch
- Staus entstehen organisch und senken Pendler-Satisfaction

---

## 11. Out of Scope für v1.0

Folgende Features werden bewusst nicht in v1.0 implementiert:

- Produktionsketten und Warenfluss zwischen Gebäuden
- Öffentlicher Nahverkehr
- Bildung, Gesundheit, Sicherheit als eigene Service-Systeme
- Dynamische Wetterbedingungen oder Jahreszeiten
- Multiplayer
- Modding-Unterstützung

---

## 12. Offene Fragen für v1.1+

- Warenfluss (Produktionsketten) als erste große Erweiterung?
- Öffentlicher Nahverkehr als eigenes System (Bus, U-Bahn)?
- Welche Services folgen zuerst: Feuerwehr, Schule oder Gesundheit?
- Gentrifizierung als explizites Mechanik (Stadtviertel ändern Gruppencharakter)?
- Modding-API für Community-Erweiterungen?

---

*Dieses Dokument ist ein lebendes Artefakt. Änderungen werden versioniert.*
