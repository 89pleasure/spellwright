# CityBuilder – Kontext für Claude Code

PC City-Builder / Wirtschafts- & Politiksimulation in Unity 6 (DOTS/ECS Hybrid), C#, JetBrains Rider.
GDD: `docs/GDD_v0.2.md`

---

## Coding-Konventionen

- Events als `readonly struct` mit `ISimulationEvent` Interface
- Kein System referenziert ein anderes direkt – alles über den Event Bus
- Jeder Handler prüft `CascadeDepth` bevor er neue Events feuert
- Pathfinding läuft ausschließlich auf `WorkerThreadPool` – nie auf Main Thread
- ECS-Komponenten haben nur Daten, keine Logik
- Systeme haben nur Logik, greifen nur über Komponenten auf Daten zu
- **Explizite Typen** – kein `var`: `RoadNode node = ...` statt `var node = ...`
- **Unity-teure Methoden cachen** – `Camera.main`, `GetComponent<>()`, `FindObjectOfType<>()` nie in `Update()`/`FixedUpdate()`; einmal in `Awake()`/`Start()` in ein privates Feld cachen
- **Expression body** für alle Methoden/Properties mit einem einzelnen Statement: `private void OnDestroy() => GameServices.Shutdown();`

## Out of Scope (v1.0 – nicht implementieren, nicht designen)

Produktionsketten · ÖPNV · Bildung/Gesundheit/Sicherheit als Services · Wetter/Jahreszeiten · Multiplayer · Modding-API

---

## Detaillierte Dokumentation

- [Simulations-Architektur & Kernsysteme](.claude/architecture.md) – Engine-Entscheidung, ECS, Event Bus, Bürger-Modell, Kernsysteme, Rendering
- [Game Design](.claude/game-design.md) – Vision, Politisches System, natürliche Konflikte
- [Gotchas & Pitfalls](.claude/gotchas.md) – MeshCollider-Winding, LSP False Positives, bekannte Fallstricke
- [Implementierungsreihenfolge](.claude/roadmap.md)
