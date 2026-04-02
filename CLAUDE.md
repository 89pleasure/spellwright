# Citybuilder – Projektkontext für Claude Code

Diese Datei liegt im Projektstamm und gibt Claude Code den vollständigen Kontext
über alle bisher getroffenen Designentscheidungen. Lies sie zu Beginn jeder Session.

---

## Projekt-Übersicht

| Parameter | Wert |
|---|---|
| Projekttyp | PC City-Builder / Wirtschafts- & Politiksimulation |
| Engine | Unity 6 (DOTS/ECS + GameObject Hybrid) |
| Sprache | C# |
| Plattform | Desktop – Windows / macOS / Linux |
| Entwickler-OS | CachyOS (Arch-basiert), KDE Plasma, Wayland, AMD Radeon 7900 XT |
| IDE | JetBrains Rider |
| GDD | `docs/GDD_v0.2.md` |

---

## Vision

> Du bist Bürgermeister einer wachsenden Metropole. Du baust keine tote Kulisse –
> du steuerst ein lebendiges System aus Bürgergruppen mit unterschiedlichen Zielen,
> die dich abwählen können wenn du ihre Bedürfnisse ignorierst.
> Verkehr, Wohnraum, Arbeitsplätze und Politik sind untrennbar verwoben.

**Emotionaler Kern:** Organisches Wachstum beobachten – die Stadt lebt ihr eigenes Leben.
**Zielgruppe:** Mid-Core (Cities Skylines 1 Komplexität, aber mit politischer Tiefe).
**USP:** Politischer Druck durch Bürgergruppen mit natürlichen Zielkonflikten. Wahlen sind verlierbar.

---

## Engine-Entscheidung: Unity 6 DOTS/ECS Hybrid

### Warum Unity 6 (nicht Godot 4)
- 200.000+ Einwohner mit individueller Pendler-Simulation → ECS unumgänglich
- Entwickler hat CS2-Modding-Erfahrung in Unity
- Burst Compiler für paralleles Pathfinding auf allen CPU-Kernen
- Godot 4 wäre bei diesem Simulations-Scope an Performance-Grenzen gestoßen

### Hybrid-Ansatz
- **ECS (DOTS):** Bürger-Entitäten, Fahrzeuge, Pendler-Pathfinding, Simulation
- **GameObjects:** UI, Kamera, statische Gebäude, Terrain
- **Burst Compiler:** Alle rechenintensiven Jobs (Bewegung, Pathfinding, Demand-Ticks)

---

## Simulations-Architektur (KRITISCH – vor jeder Implementierung lesen)

### Drei parallele Mechanismen

#### 1. Frame Loop (ECS Jobs)
Für alles was sich jeden Frame ändert. Vollständig parallelisiert.
- Fahrzeug- und Fußgängerbewegung
- Rendering-Updates

#### 2. Discrete Event Simulation (DES)
**Kernprinzip:** Arbeit proportional zur Anzahl der *Veränderungen*, nicht der Entitäten.
Die Simulation schläft zwischen Events – kein Polling.

Implementierung: **Priority Queue (Min-Heap)** sortiert nach Spielzeit (`GameTime`).

```csharp
// Events sind Structs – kein Heap-Allocation, keine GC-Last
public readonly struct TrafficAccidentEvent : ISimulationEvent {
    public readonly Entity Vehicle;
    public readonly int RoadSegmentId;
    public readonly float GameTime;
    public readonly int CascadeDepth; // Schutz gegen Endlosschleifen
}

// Pro Frame: alle Events bis zur aktuellen Spielzeit abarbeiten
while (queue.Peek().GameTime <= currentGameTime) {
    var evt = queue.Dequeue();
    eventBus.Publish(evt);
}
```

DES verwaltet:
- Pendler-Routenentscheidungen (Heimat → Arbeit → Heimat)
- Satisfaction-Änderungen pro Bürger
- Wirtschaftliche Zustandsänderungen
- Politik-Events und Wahlen
- Alle Ereignisse aus dem Event-Katalog (s.u.)

#### 3. Dirty Flag System
Pathfinding wird **nur** neu berechnet wenn relevante Straßenknoten als `dirty` markiert wurden.
- Straße gebaut/abgerissen → betroffene Knoten `dirty = true`
- Nur Bürger deren gecachte Route durch dirty-Knoten führt rechnen neu
- Ziel: ~2.000 statt 200.000 Pathfinding-Queries pro Straßenänderung

---

## Event-Bus Architektur (KRITISCH)

### Grundregeln
1. **Kein System referenziert ein anderes direkt** – alles läuft über den Event Bus
2. **Events sind read-only Structs** – Handler bekommen eine Kopie, nie eine Referenz
3. **Schichtenarchitektur** – Events fließen NUR nach unten:

```
Spieler-Input      →  feuert Events, empfängt NIE
       ↓
Infrastruktur      →  Straßen, Strom, Wasser
       ↓
Simulation         →  Verkehr, Wirtschaft, Bürger-Satisfaction
       ↓
Politik            →  Approval, Wahlen
       ↓
Rendering          →  empfängt Events, feuert NIE
```

**Verboten:** Rendering-System feuert Simulation-Event. Infrastruktur-Event feuert direkt Politik-Event.

### Schutzmechanismen gegen unkontrollierte Kaskaden

```csharp
public class EventBus {
    private const int MAX_CASCADE_DEPTH = 10; // konfigurierbar

    public void Publish<T>(T evt) where T : ISimulationEvent {
        // 1. Max Cascade Depth
        if (evt.CascadeDepth > MAX_CASCADE_DEPTH) {
            Debug.LogWarning($"Cascade depth exceeded: {typeof(T)}");
            return;
        }
        // 2. Deduplication: gleiches Event, gleiche Entität, gleiche Spielzeit → skip
        if (_processedThisFrame.Contains((evt.SourceEntity, typeof(T), evt.GameTime)))
            return;

        foreach (var handler in GetHandlers<T>())
            handler.Handle(evt);
    }
}
```

### Event-Katalog (v1.0)

| Event | Auslöser | Typische Folge-Events |
|---|---|---|
| `TrafficAccidentEvent` | Fahrzeugkollision | `RoadBlockedEvent`, `EmergencyDispatchedEvent`, `CitizenMoodChangedEvent` |
| `FireEvent` | Zufällig / Überlastung | `EvacuationStartedEvent`, `BuildingDamagedEvent`, `FireSpreadEvent` |
| `PowerFailureEvent` | Kapazitätsüberschreitung | `PowerLostEvent` (N Gebäude), `SatisfactionChangedEvent` |
| `CitizenDiedEvent` | Alter / Unfall | `HousingFreedEvent`, `WorkplaceFreedEvent` |
| `CrimeEvent` | Niedrige Satisfaction | `CitizenFleeingEvent`, `PropertyValueChangedEvent` |
| `ElectionTriggeredEvent` | Alle 4 Spieljahre | `ApprovalAggregatedEvent`, `MayorReelectedEvent` / `MayorDefeatedEvent` |

---

## Bürger-Modell (ECS)

Jeder Bürger ist eine ECS-Entität. Komponenten:

```csharp
public struct CitizenComponent : IComponentData {
    public Entity HomeBuilding;       // feste Wohnparzelle
    public Entity WorkBuilding;       // fester Arbeitsplatz
    public CitizenGroup Group;        // Enum: Worker / MiddleClass / Entrepreneur / Eco
    public int Satisfaction;          // -100 bis +100
    public bool RouteIsDirty;         // Pathfinding neu berechnen?
}

public enum CitizenGroup {
    WorkerClass,      // Bedürfnis: günstige Mieten, Jobs, ÖPNV
    MiddleClass,      // Bedürfnis: Schulen, Parks, gute Straßen
    Entrepreneur,     // Bedürfnis: niedrige Steuern, Gewerbefreiheit
    Environmental     // Bedürfnis: Grünflächen, niedrige Emissionen
}
```

### Satisfaction-Formel

```
satisfaction = 0
+ 30   Straßenanbindung vorhanden
+ 20   Stromversorgung vorhanden
+ 20   Wasserversorgung vorhanden
+ 15   Nachbar-Balance stimmt
+ 10   Gruppenspezifisches Bedürfnis erfüllt
- 10   pro fehlender Infrastrukturstufe
- 20   Industrie direkt benachbart (nur Wohnzone)
- 15   Überbevölkerung
```

---

## Politisches System

### Approval & Wahlen

```csharp
// Approval pro Gruppe: 0–100%
// Aggregiert aus Satisfaction aller Bürger der Gruppe

float electionResult = groups.Sum(g => g.Approval * g.PopulationShare);

if (electionResult > 0.5f)  → Wiederwahl
else                        → Abwahl (Game Over)
```

- Wahlen alle 4 Spieljahre
- Countdown im UI sichtbar ab 1 Jahr vorher
- Natürliche Zielkonflikte erzeugen Druck ohne Scripting

### Natürliche Konflikte (Beispiele)
- Steuererhöhung: Mittelstand `+`, Unternehmer `−`
- Autobahn durch Grünfläche: Mittelstand `+`, Umweltbewusste `−`
- Sozialwohnungen: Arbeiterklasse `+`, Unternehmer `−`
- Industriegebiet: Arbeiterklasse `+` (Jobs), Umweltbewusste `−`

---

## Kernsysteme (Übersicht)

### Straßennetzwerk
- Freies Spline-basiertes System (kein Grid)
- Intern: Node-Graph (Kreuzungen = Knoten, Abschnitte = Kanten)
- Parcel-Erzeugung: 40m Tiefe automatisch links/rechts beim Zeichnen
- Leitungen (Strom/Wasser) verlaufen automatisch entlang Straßen

### Zonensystem
- R (Wohn) / C (Gewerbe) / I (Industrie)
- 5 Ausbaustufen pro Parzelle – Gebäude wachsen/verfallen automatisch
- Zonenabstand-Regeln: I neben R → Satisfaction-Malus

### Stromversorgung
- Flood-Fill entlang Straßengraph vom Kraftwerk
- Kapazitätswarnung bei 80%, Stopp bei 100%

### Wasserversorgung
- Druckradius (Circle-of-Influence) statt manuellem Rohrnetz
- Wasserwerk: 800m Radius / Wasserturm: 400m Radius

### Budget
- Startkapital: 100.000 Credits
- Bankrott → 5 Minuten Erholungszeit → sonst Game Over
- Steuersatz pro Zonentyp: 0–30% in 5%-Schritten

---

## Rendering-Optimierungen

| Technik | Anwendung |
|---|---|
| `DrawMeshInstanced` | Alle Gebäude gleichen Typs: 1 Draw Call |
| Chunk Streaming | Nur sichtbare Chunks im Speicher |
| Rendering-LOD | 3 Stufen: <200m / 200–800m / >800m Billboard |
| Simulation-LOD Agenten | Voll (sichtbar) / Vereinfacht (nah) / Statistisch (weit) |

---

## Out of Scope für v1.0

Folgendes wird **nicht** implementiert – nicht anfassen, nicht designen:
- Produktionsketten / Warenfluss
- Öffentlicher Nahverkehr
- Bildung, Gesundheit, Sicherheit als Services
- Wetter / Jahreszeiten
- Multiplayer
- Modding-API

---

## Empfohlene Implementierungsreihenfolge

1. **Projektstruktur & Ordner** aufsetzen
2. **Event Bus** als erstes – alles andere baut darauf auf
3. **Straßen-Graph** (Node-basiert, ohne Rendering)
4. **Parcel-Erzeugung** entlang Splines
5. **Zonen-Zuweisung** + Gebäude-Platzhalter
6. **Bürger-ECS** – Entitäten mit Heimat + Job + Gruppe
7. **Pendler-Simulation** + Dirty Flag Pathfinding
8. **Strom-Flood-Fill**
9. **Wasser-Druckradius**
10. **Demand-Tick** + Gebäudewachstum
11. **Satisfaction** + Approval
12. **Wahlsystem**
13. **Budget & Bankrott**
14. **Rendering** + LOD
15. **UI / HUD**

---

## Wichtige Coding-Konventionen

- Events immer als `readonly struct` mit `ISimulationEvent` Interface
- Keine direkten System-zu-System-Referenzen – immer Event Bus
- Jeder Handler prüft `CascadeDepth` bevor er neue Events feuert
- Pathfinding läuft ausschließlich auf `WorkerThreadPool` – nie auf Main Thread
- ECS-Komponenten haben nur Daten, keine Logik
- Systeme haben nur Logik, greifen nur über Komponenten auf Daten zu
- **Immer explizite Typen** – kein `var`, stattdessen z.B. `RoadNode node = ...` statt `var node = ...`
- **Unity-teure Methoden cachen** – `Camera.main`, `GetComponent<>()`, `FindObjectOfType<>()` niemals in `Update()`/`FixedUpdate()` aufrufen; einmal in `Awake()`/`Start()` in ein privates Feld cachen
- **Expression body** für alle Methoden/Properties mit einem einzelnen Statement – `private void OnDestroy() => GameServices.Shutdown();` statt Block-Body

---

## Weiterführende Dokumente

- `docs/GDD_v0.2.md` – vollständiges Game Design Document
- `docs/GDD_v0.1.md` – erstes Draft (Godot 4, veraltet – nur als Referenz)