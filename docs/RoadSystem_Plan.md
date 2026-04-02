# Straßensystem – Implementierungsplan

**Status:** In Arbeit
**Letzte Aktualisierung:** 2026-04-02
**Bezug:** CLAUDE.md Schritt 3 (Straßen-Graph) und Schritt 4 (Parcel-Erzeugung)

---

## Architektur-Übersicht

Das System ist in drei strikt getrennte Schichten gegliedert:

```
┌──────────────────────────────────────────────────────────┐
│  Schicht 3 – Rendering                                   │
│  RoadRenderer, IntersectionRenderer                      │
│  Unity-spezifisch: MeshFilter, MeshRenderer, Materials   │
├──────────────────────────────────────────────────────────┤
│  Schicht 2 – Mesh-Berechnung                             │
│  RoadProfile, RoadMeshBuilder, IntersectionMeshBuilder   │
│  Gibt Plain-Arrays zurück (float3[], int[]) – kein Unity │
├──────────────────────────────────────────────────────────┤
│  Schicht 1 – Daten                                       │
│  RoadGraph, RoadNode, RoadSegment, BezierCurve           │
│  Kein Unity, kein Mesh – nur Mathematik und Graph-Logik  │
└──────────────────────────────────────────────────────────┘
```

**Erweiterbarkeit:** Neue Straßentypen (Radweg, Autobahn, etc.) entstehen durch neue
`RoadProfile`-Assets – kein Code muss geändert werden.

---

## Kreuzungs-Konzept (zentrale Design-Entscheidung)

Die Kreuzung ist **kein Segment**. Sie ist eine eigene Mesh-Einheit die dem `RoadNode` gehört.

```
Segment A  ───────[clip]          [clip]─────── Segment B
                        ↘        ↗
                    ┌──────────────┐
                    │ Intersection │  ← eigenes Mesh, gehört dem Node
                    └──────────────┘
                        ↗        ↘
Segment C  ───────[clip]          [clip]─────── Segment D
```

**Ablauf:**
1. Jedes Segment wird entlang seiner Bezier-Kurve bis `TrimmedEndT` extrudiert → offene Kante
2. Die Randpunkte (links/rechts) am Clip-Ende werden **aus der Kurven-Mathematik** berechnet:
   `BezierCurve.Evaluate(TrimmedEndT)` + Rechtsvektor × halbe Profilbreite
3. `IntersectionMeshBuilder` sammelt alle Randpunkte aller anliegenden Segmente
4. Polygon aus diesen Punkten (Reihenfolge via `OrderedSegmentIds`) → Ear-Clipping → Mesh

Die Intersection-Generierung ist damit **unabhängig vom Road-Mesh** – beide nutzen die
gleiche Kurven-Mathematik, aber keine gemeinsamen Mesh-Objekte.

**Wann wird ein Intersection-Mesh erzeugt?**
- Node hat ≥ 3 Segmente: immer
- Node hat 2 Segmente: nur wenn Winkel zwischen ihnen > 15° (sonst nahtloser Übergang)

---

## Schritt 1 – BezierCurve Utility

**Datei:** `Assets/_CityBuilder/Infrastructure/Roads/BezierCurve.cs`
**Schicht:** 1 – Daten
**Status:** ⬜ Nicht begonnen

Statische, Burst-kompatible Mathematik-Klasse. Keine Unity-Objekt-Abhängigkeiten.
Fundament für Mesh-Extrusion, Segment-Splitting und Intersection-Randpunkte.

### API

```csharp
public static class BezierCurve
{
    // Position auf der Kurve bei Parameter t ∈ [0,1] (de Casteljau)
    public static float3 Evaluate(float3 p0, float3 p1, float3 p2, float3 p3, float t)

    // Normalisierte Tangente bei t – für Rechtsvektor der Mesh-Extrusion
    public static float3 EvaluateTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)

    // Kurve bei Parameter t aufteilen → Kontrollpunkte beider Hälften
    public static void SplitAt(
        float3 p0, float3 p1, float3 p2, float3 p3, float t,
        out float3 leftP1, out float3 leftP2,
        out float3 rightP1, out float3 rightP2)

    // Arc-Length LUT: 128 kumulierte Distanzwerte; lut[0]=0, lut[127]=Gesamtlänge
    public static float[] BuildArcLengthLUT(float3 p0, float3 p1, float3 p2, float3 p3, int samples = 128)

    // Arc-Length s → Parameter t (Binärsuche + lineare Interpolation)
    public static float ArcLengthToT(float[] lut, float s)
}
```

### Algorithmus-Details

**de Casteljau (Evaluate):**
```
Q0 = lerp(P0, P1, t),  Q1 = lerp(P1, P2, t),  Q2 = lerp(P2, P3, t)
R0 = lerp(Q0, Q1, t),  R1 = lerp(Q1, Q2, t)
S  = lerp(R0, R1, t)   ← Ergebnis
```

**SplitAt – Kontrollpunkte:**
```
leftP1  = Q0,  leftP2  = R0   (linke Hälfte endet bei S)
rightP1 = R1,  rightP2 = Q2   (rechte Hälfte startet bei S)
```

**Arc-Length LUT:** 128 t-Werte auswerten, Abstände kumulieren.
~512 Bytes pro Segment. Lookup: O(log 128).

---

## Schritt 2 – RoadSegment Kurvendaten

**Datei:** `Assets/_CityBuilder/Infrastructure/Roads/RoadSegment.cs`
**Schicht:** 1 – Daten
**Status:** ⬜ Nicht begonnen

### Neue Felder

```csharp
public class RoadSegment
{
    // bestehende Felder unverändert...

    // Bezier: P0 = NodeA.Position, P3 = NodeB.Position (nicht dupliziert)
    public float3 ControlPointA;   // P1
    public float3 ControlPointB;   // P2
    public float[] ArcLengthLUT;   // 128 Samples, vorberechnet
    public float TotalArcLength;

    // Clipping: wo das Mesh beginnt/endet (Lücke für Intersection-Mesh)
    public float TrimmedStartT = 0f;
    public float TrimmedEndT   = 1f;

    // Split-Tracking
    public int ParentSegmentId = -1;
}
```

### Standardkontrollpunkte (gerade Straße)

```csharp
ControlPointA = math.lerp(nodeAPos, nodeBPos, 1f / 3f);
ControlPointB = math.lerp(nodeAPos, nodeBPos, 2f / 3f);
```

Bezier degeneriert zur geraden Linie → kein Sonderfall nötig.

---

## Schritt 3 – RoadNode OrderedSegmentIds

**Datei:** `Assets/_CityBuilder/Infrastructure/Roads/RoadNode.cs`
**Schicht:** 1 – Daten
**Status:** ⬜ Nicht begonnen

Wird für Intersection-Mesh (Polygon-Reihenfolge) und Parcel-Generierung (Block-Traversal) benötigt.

### Neues Feld

```csharp
public readonly List<int> OrderedSegmentIds = new();  // sortiert CW von Nord
```

Nach jeder Segment-Änderung am Node via `RoadGraph.RebuildOrderedSegments(nodeId)` neu sortieren.
Sortierkriterium: `atan2(dir.x, dir.z)` des Segment-Anfangsvektors vom Node aus.

---

## Schritt 4 – Segment-Splitting

**Datei:** `Assets/_CityBuilder/Infrastructure/Roads/RoadGraph.cs`
**Schicht:** 1 – Daten
**Status:** ⬜ Nicht begonnen

### Wann nötig

Nutzer beginnt/endet neue Straße **auf einem bestehenden Segment** (nicht an einem Node).

### Algorithmus

```
Input: Segment S, Schnittpunkt P, Parameter t_split

1. Neuen Node X bei P erzeugen
2. Alte Kontrollpunkte sichern
3. BezierCurve.SplitAt(t_split) → (leftP1, leftP2), (rightP1, rightP2)
4. S anpassen:  S.NodeB = X.Id, S.ControlPointB = leftP2, LUT neu bauen
5. Neues Segment S2: X → altes NodeB, ControlPointA = rightP1, ControlPointB = rightP2
   S2.ParentSegmentId = S.Id, LUT neu bauen
6. X.SegmentIds = { S.Id, S2.Id }, OrderedSegmentIds neu bauen
7. Dirty Flags: X, S.NodeA, S2.NodeB
```

---

## Schritt 5 – RoadProfile

**Datei:** `Assets/_CityBuilder/Infrastructure/Roads/RoadProfile.cs`
**Schicht:** 2 – Mesh-Berechnung
**Status:** ⬜ Nicht begonnen

Beschreibt den Querschnitt einer Straße **deklarativ**. Kein Code-Änderung nötig um neue
Straßentypen hinzuzufügen – nur neues `RoadProfile`-Asset.

### Datenstruktur

```csharp
// Ein Streifen im Querschnitt (Fahrspur, Bürgersteig, Radweg, ...)
public struct ProfileStrip
{
    public float Width;          // Breite in Metern
    public float HeightOffset;   // Höhe relativ zur Fahrbahn (Bordstein = 0.15f)
    public StripType Type;       // Enum: Carriageway, Pavement, CycleWay, Kerb, ...
    public int MaterialIndex;    // Submesh-Index → welches Material im Renderer
}

[CreateAssetMenu]
public class RoadProfile : ScriptableObject
{
    public ProfileStrip[] Strips;   // von links nach rechts
    public float TotalWidth => /* Summe aller Strip.Width */
}
```

### Beispiel-Profile

```
Standard 2-spurig (7m):
  [ Bürgersteig 2m | Fahrspur 3.5m | Fahrspur 3.5m | Bürgersteig 2m ]

Später erweiterbar:
  [ Bürgersteig 2m | Radweg 1.5m | Fahrspur 3.5m | Fahrspur 3.5m | Radweg 1.5m | Bürgersteig 2m ]
  [ Fahrspur 3.5m | Fahrspur 3.5m | Fahrspur 3.5m | Fahrspur 3.5m ]   (Autobahn)
```

---

## Schritt 6 – RoadMeshBuilder

**Datei:** `Assets/_CityBuilder/Rendering/RoadMeshBuilder.cs`
**Schicht:** 2 – Mesh-Berechnung
**Status:** ⬜ Nicht begonnen

Nimmt `RoadSegment` + `RoadProfile`, gibt `RoadMeshData` (Plain-Arrays) zurück.
Kein `GameObject`, kein `MeshFilter` – nur Mathematik.

### Output

```csharp
public struct RoadMeshData
{
    public float3[] Vertices;
    public int[][]  Triangles;    // pro Submesh (Index = MaterialIndex)
    public float2[] UVs;
    public float3[] Normals;
}
```

### Algorithmus

```
1. N Samples gleichmäßig über Arc-Length zwischen TrimmedStartT und TrimmedEndT
2. Pro Sample:
   - Position P = BezierCurve.Evaluate(t)
   - Tangente T = BezierCurve.EvaluateTangent(t).normalized
   - Rechtsvektor R = cross(T, up)
   - Für jeden Strip im Profil: Vertex = P + R * strip.centerX + up * strip.height
3. Quads zwischen aufeinanderfolgenden Samples → Triangles pro Submesh
4. UV.v = arc_distance / TotalArcLength (für Fahrbahnmarkierungen)
```

---

## Schritt 7 – IntersectionMeshBuilder

**Datei:** `Assets/_CityBuilder/Rendering/IntersectionMeshBuilder.cs`
**Schicht:** 2 – Mesh-Berechnung
**Status:** ⬜ Nicht begonnen

Baut das Intersection-Mesh **aus Kurven-Mathematik**, unabhängig vom Road-Mesh.

### Randpunkt-Berechnung

```csharp
// Für jedes Segment S am Node:
float3 clipPos    = BezierCurve.Evaluate(..., S.TrimmedEndT);
float3 tangent    = BezierCurve.EvaluateTangent(..., S.TrimmedEndT).normalized;
float3 right      = math.cross(tangent, math.up());
float  halfWidth  = S.Profile.TotalWidth * 0.5f;

float3 leftEdge   = clipPos - right * halfWidth;
float3 rightEdge  = clipPos + right * halfWidth;
```

### Polygon-Aufbau

```
Reihenfolge via Node.OrderedSegmentIds (CW):
  Für Segment[0]: rightEdge, leftEdge
  Für Segment[1]: rightEdge, leftEdge
  ...
→ geschlossenes Polygon → Ear-Clipping → Mesh
```

### TrimmedT berechnen (Clipping-Radius)

```csharp
// Binärsuche auf ArcLengthLUT:
// Von Knotenende rückwärts bis Abstand zum Node ≥ intersectionRadius
float intersectionRadius = profile.TotalWidth * 0.5f;  // abhängig von Profilbreite
```

---

## Schritt 8 – Renderer

**Datei:** `Assets/_CityBuilder/Rendering/RoadRenderer.cs` (refactorn)
**Datei:** `Assets/_CityBuilder/Rendering/IntersectionRenderer.cs` (neu)
**Schicht:** 3 – Rendering
**Status:** ⬜ Nicht begonnen

Nimmt `RoadMeshData` / Intersection-Polygon, erstellt/aktualisiert Unity-Objekte.

### Verantwortlichkeiten

- `RoadMeshData` → `Mesh.SetVertices` / `SetTriangles` (pro Submesh) / `SetUVs`
- `mesh.UploadMeshData(markNoLongerReadable: true)` nach Upload
- Material-Array aufbauen: Index 0 = Fahrbahn, Index 1 = Bürgersteig, ...
- `MeshRenderer.sharedMaterials` setzen (kein `.material` – vermeidet per-instance Kopien)
- Bei Segment-Änderung: nur betroffenes Segment + anliegende Intersection-Meshes neu bauen

---

## Schritt 9 – Parcel-Generierung

**Datei:** `Assets/_CityBuilder/Infrastructure/Roads/ParcelGenerator.cs`
**Schicht:** 1 – Daten (Parcels sind Daten, kein Mesh)
**Status:** ⬜ Nicht begonnen

### Block-Erkennung

```
Für jeden Node N, für jedes Segment S an N:
  Traversiere: nächstes Segment im Uhrzeigersinn via OrderedSegmentIds
  Bis Ausgangssegment wieder erreicht → Block-Polygon
Duplikate herausfiltern (jeder Block wird zweimal gefunden)
```

### Parcel-Subdivision (Offset + Rekursion)

```
1. Block-Polygon um Setback (5m) einwärts offsetten
2. Recursive Subdivide(Polygon):
   a. Fläche < 2.000 m²  →  eine Parcel
   b. Sonst: längste Kante, senkrecht teilen → 2 Teilpolygone → rekursieren
3. Pro Parcel: Mittelpunkt + Ausrichtung zur nächstgelegenen Straßenfront
```

### Parcel-Klasse

```csharp
public class Parcel
{
    public int Id;
    public float3[] Boundary;       // Polygon CCW
    public float AreaSqm;
    public float3 FrontDirection;   // Ausrichtung zur Straße
    public float3 Center;
    public ZoneType Zone;           // R / C / I
}
```

---

## Offene Fragen / Entscheidungen

| # | Frage | Status |
|---|---|---|
| 1 | Wie zieht der Nutzer Kurven? (Zwei-Klick gerade, Drag für Kontrollpunkte, oder Auto-Tangent?) | Offen |
| 2 | Intersection-Radius: fest (= halbe Profilbreite) oder manuell konfigurierbar? | Offen |
| 3 | Straight Skeleton statt Offset+Rekursion für Parcel-Gen bei starken Kurven? | Später |
| 4 | Mehrere RoadProfile pro Segment (z.B. Kreuzungsbereich breiter)? | Später |

---

## Aktueller Status

| Schritt | Beschreibung | Schicht | Status |
|---|---|---|---|
| 1 | BezierCurve Utility | Daten | ✅ Fertig |
| 2 | RoadSegment Kurvendaten | Daten | ✅ Fertig |
| 3 | RoadNode OrderedSegmentIds | Daten | ✅ Fertig |
| 4 | Segment-Splitting in RoadGraph | Daten | ✅ Fertig |
| 5 | RoadGraphService (T-Junction + Control Points) | Daten | ✅ Fertig |
| 6 | RoadProfile (ScriptableObject) | Mesh-Berechnung | ✅ Fertig |
| 7 | RoadMeshData + RoadMeshBuilder | Mesh-Berechnung | ✅ Fertig |
| 8 | RoadRenderer (prozedurales Mesh) | Rendering | ✅ Fertig |
| 9 | RoadNetworkDebugDrawer (Bézier-Kurven) | Rendering | ✅ Fertig |
| 10 | IntersectionMeshBuilder + IntersectionRenderer | Mesh-Berechnung / Rendering | ✅ Fertig |
| 11 | Parcel-Generierung | Daten | ⬜ Nicht begonnen |
