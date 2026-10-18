# Architekturkonzept – Chaining Chronicle (KI-Implementierung)

| | |
|---|---|
| Basis | `GDD_Chaining_Chronicle` v1.0 (06.03.2026) |
| Engine | Unity 6000.3.8f1, URP 2D, Input System 1.18, 2D Tilemap, Test Framework 1.6 |
| Erstellt von | Claude Code (Sonnet 5.5), Prompt: "Architekturkonzept basierend auf dem GDD" |
| Stand | 11.10.2026, Version 0.6 (Combat System umgesetzt: A25–A37 neu und ungeprüft, D8–D13, B9–B11) |
| Zweck | Methodik-Schritt "Architektur vor Umsetzung" (Folie 15), Grundlage für UF3 |

Das Dokument beschreibt Ziele, Struktur, Verantwortlichkeiten und Reihenfolge. Es enthält bewusst keinen Implementierungscode. Alle Stellen, an denen das GDD nichts oder Mehrdeutiges sagt, sind in Abschnitt 11 als **Annahmen** gelistet (Material für UF4).

---

## 1. Architekturziele

Abgeleitet aus GDD Abschnitt 9 und den nicht funktionalen Requirements der BA:

| ID | Ziel | Begründung |
|---|---|---|
| Z1 | **Spiellogik ohne Unity-Abhängigkeit testbar** | Combat/Chaining/Stage-Generierung sind der Kern (GDD 1.1, 2.2.5). EditMode-Tests ohne Szenen. |
| Z2 | **Datengetriebene Inhalte** (Skills, Gegner, Power-ups, Biome) | GDD 2.2.5 TODO-Liste (Statuseffekte, Klassen, Multiplikatoren) muss ohne Codeumbau erweiterbar sein. |
| Z3 | **Klare Verantwortung je System, einseitige Abhängigkeiten** | Folie 15: "Zuweisung von technischer Verantwortung". Messbar über Änderungsauswirkung. |
| Z4 | **Reproduzierbarkeit** | Seed-basierte Zufälligkeit, damit Stages und Kämpfe in Tests deterministisch sind (BA-Anforderung M4). |
| Z5 | **Leichtgewichtig** | GDD 7.1: läuft auf jedem PC ohne dedizierte GPU. Keine externen DI-/Event-Frameworks. |
| Z6 | **MVP-Fokus** | Alles aus 9.1 (Must) vollständig, 9.2 (Should) schlank, 9.3 (Could) nur als Erweiterungspunkt. |

Nicht-Ziele: Story, Tutorial, Währung/Shops, Multiplayer, Meta-Progression (laut GDD nicht im MVP). Es werden jedoch Erweiterungspunkte vorgesehen (Abschnitt 10).

---

## 2. Schichtenmodell und Assemblies

Drei Schichten, Abhängigkeiten zeigen nur nach unten. Technisch durch Assembly Definitions erzwungen, nicht durch Konvention.

```
┌──────────────────────────────────────────────────────────┐
│  CC.Game   (Presentation / Unity)                        │
│  MonoBehaviours, Tilemap-Rendering, UI, Audio, Input     │
├──────────────────────────────────────────────────────────┤
│  CC.Data   (Definitionen, ScriptableObjects)             │
│  SkillDef, EnemyDef, PowerUpDef, StatusDef, BiomeDef     │
├──────────────────────────────────────────────────────────┤
│  CC.Core   (reine Spiellogik, kein UnityEngine)          │
│  Grid, StageGenerator, Pathfinding, Combat, Chaining,    │
│  EnemyBrain, RunState, Scoring                           │
└──────────────────────────────────────────────────────────┘
```

| Assembly | Referenziert | Enthält `UnityEngine`? |
|---|---|---|
| `CC.Core` | nichts (`noEngineReferences: true`) | nein |
| `CC.Data` | `CC.Core` | ja (ScriptableObject), nur als Datenhalter |
| `CC.Game` | `CC.Core`, `CC.Data`, Unity-Pakete | ja |
| `CC.Tests.EditMode` | `CC.Core`, `CC.Data` | nur Test-Framework |
| `CC.Tests.PlayMode` | alle | ja (Smoke-Tests, späte Phase) |

`CC.Data` konvertiert ScriptableObjects in unveränderliche Core-Strukturen (`SkillDef` → `Skill`). `CC.Core` kennt daher kein ScriptableObject und keine Asset-Referenzen.

---

## 3. Ordnerstruktur

```
Assets/
  Scripts/
    Core/            (CC.Core)
      Grid/          GridPos, Direction, TileGrid, Pathfinder
      Stage/         StageGenerator, StageLayout, Room, StageRules
      Combat/        CombatSession, Combatant, Skill, ChainResolver, StatusEffect
      Ai/            IEnemyBrain, FsmEnemyBrain, StageEnemyMover
      Run/           RunState, Party, PowerUpInventory, Score
      Util/          IRng, SeededRng, GameEvents
    Data/            (CC.Data) Definitions + Converter
    Game/            (CC.Game)
      Boot/          Bootstrapper, SceneFlow
      Stage/         StageView, PlayerController, EnemyView, InteractableView
      Combat/        CombatView, ChainCounterView, CombatLogView
      Ui/            Hud, Menus, Leaderboard
      Audio/         AudioService
      Persistence/   JsonLeaderboardStore
      Input/         InputRouter
  Content/           ScriptableObject-Instanzen (Skills, Enemies, PowerUps, Biomes)
  Export/            vorhandene Sprites/Audio/Animationen der Baseline (unverändert)
  Scenes/            Boot, Hub, Stage
  Tests/EditMode, Tests/PlayMode
  Docs/
```

---

## 4. Laufzeitarchitektur

### 4.1 Szenen und Spielfluss (GDD 1.4, 2.2.7)

| Szene | Inhalt |
|---|---|
| `Boot` | Bootstrapper, persistente Services (Audio, Input, Leaderboard), lädt `Hub` |
| `Hub` | Kleiner Hub zum Ansehen des Leaderboards (GDD 3.2.1), Run starten, Charaktername eingeben |
| `Stage` | Wiederverwendete Szene für jede Stage. Stage-Ansicht und Kampfansicht liegen in **derselben** Szene |

Kampf ist keine eigene Szene. Die Stage-Szene bleibt geladen, die `CombatView` (eigene Kamera/Canvas) wird darübergelegt. Dadurch gehen Positionen, Gegnerzustand und Stage nicht verloren und es entfällt Zwischenspeichern.

Zentraler Zustandsautomat (`GameFlow`, in `CC.Game`):

```
Boot → Hub → [Run starten] → Exploring ⇄ Combat → Exploring → … → GameOver → Hub
                                  │                                 ▲
                                  └── Stairs erreicht → neue Stage ─┘ (Stage n+1)
```

- `Exploring → Combat`: Spielerfeld == Gegnerfeld (GDD 1.4).
- `Combat → Exploring`: Gegner besiegt, Gegner wird aus der Stage entfernt.
- `Combat → GameOver`: Spieler/Party besiegt (Permadeath), Score ins Leaderboard.
- Optionales Pausenmenü nur in `Exploring` (GDD 9.3 Could).

### 4.2 Kommunikation

Statt direkter Referenzen zwischen Systemen ein **typisierter Event-Bus** (`GameEvents`, statische C#-Events in `CC.Core.Util`, keine Reflection, kein Framework):

`FlowStateChanged`, `RunStarted`, `RunEnded` (im Fundament vorhanden). Folgen mit den jeweiligen Systemen: `StageStarted`, `PlayerMoved`, `EnemyEngaged`, `CombatStarted`, `SkillUsed`, `ChainExtended`, `DamageDealt`, `CombatEnded` (Ergebnis `Won`/`Fled`/`Lost`), `PowerUpCollected`.

Regel: Core **sendet** Events, Game **empfängt** sie (View, Audio, HUD). Views rufen Core-Methoden auf, nie umgekehrt. Audio und HUD sind damit reine Beobachter.

### 4.3 Zufall

Alle Zufallsentscheidungen über `IRng` (Standard `SeededRng`). Ein Run hat einen Master-Seed, abgeleitet pro Stage (`seed + stageNr`) und pro Kampf. Zweck: reproduzierbare Tests und Debugging.

---

## 5. Systeme und Verantwortlichkeiten

### 5.1 Grid und Stage (Must: Bewegung, Kollision, endlose Stages)

| Komponente | Verantwortung |
|---|---|
| `TileGrid` | Reine 2D-Daten (Wand/Boden/Treppe), Begehbarkeit, Belegung durch Entities |
| `StageGenerator` | Erzeugt Layout aus (Seed, Stage-Nr): Räume + Gänge, Startfeld, Treppe, Gegner- und Power-up-Platzierung. Garantiert Erreichbarkeit von Treppe (Flood-Fill-Validierung) |
| `StageRules` | Skalierung: Gegneranzahl und Werte in Abhängigkeit von Stage-Nr (GDD 4.1 "steigende Schwierigkeit") |
| `Pathfinder` | A\* auf `TileGrid`, genutzt von Gegnern |
| `StageView` | Zeichnet `StageLayout` in eine Unity-`Tilemap`, Palette per `BiomeDef` (Palette Swap, GDD 3.2.1) |
| `PlayerController` | Liest Input, ruft `StageSession.TryMove(dir)`, animiert Schritt |

Rundenmodell auf der Stage: **Gegner ziehen nur, nachdem sich der Spieler bewegt hat**, ein Spielerschritt löst genau einen Gegnerzug aus (Mystery-Dungeon-Prinzip, A1/A2, vom Autor bestätigt). Die Logik liegt in `StageSession` (Core). Die View animiert lediglich das Ergebnis.

### 5.2 Interaktion (GDD 2.2.3/2.2.4)

Eine kontextsensitive Taste. `InteractionResolver` (Core) prüft das Feld vor dem Spieler und liefert `IInteractable` zurück (Power-up, Treppe). Gegner sind keine Interaktion, sondern Kollision.

### 5.3 Kampf (Must, Kern)

Rundenbasiert, ereignisgesteuert, vollständig in `CC.Core`.

| Komponente | Verantwortung |
|---|---|
| `CombatSession` | Hält Teams, Zugreihenfolge, Phase (`AwaitingInput` / `Resolving` / `Ended`) |
| `Combatant` | HP, Stats (inkl. Speed), Skills, aktive Statuseffekte, Team |
| `Skill` | Aktion: Zielregel, Schaden/Heilung, optionaler **Status-Auftrag** (`appliesStatus`), optionale **Chain-Bedingung** (`followUpOn`) |
| `ChainResolver` | Kernmechanik: siehe 5.4 |
| `StatusEffect` | Verwaltet Dauer/Stapelung. Für MVP nur als Marker für Chaining, Erweiterung für DoT/Debuff vorbereitet |
| `TurnOrder` | **Alle** Kämpfer (Party und Gegner) in einer gemeinsamen Reihenfolge nach Speed absteigend. Der Schnellste zieht zuerst, dann der nächstschnellere (Entscheidung des Autors zu A4, kein Spielervorteil beim Start) |
| Flucht | `CombatSession.TryFlee()` beendet den Kampf mit Ergebnis `Fled`. Der Gegner wird von der Stage entfernt (Entscheidung zu A6). Erfolgswahrscheinlichkeit siehe A11 |

Der Spieler wählt die Aktion über `CombatView`, die `CombatSession.SubmitAction(...)` aufruft. Gegner bekommen ihre Aktion von `IEnemyBrain` (5.6). Die Session kennt den Unterschied nicht.

Kampfergebnis: `Won` (Gegner entfernt), `Fled` (Gegner entfernt), `Lost` (Run endet, Permadeath).

### 5.4 Chaining (Alleinstellungsmerkmal, GDD 1.1)

```
Aktion ausgeführt, Ziel hat Status S
   → ChainResolver prüft alle Combatants der aktiven Seite (Reihenfolge: Speed absteigend):
       hat ein Skill followUpOn == S und ist nutzbar (z. B. Wiederholungsbegrenzung)?
   → ja: Combatant darf Follow-up auslösen, ChainCounter++, neuer Status aktualisiert die Prüfung
   → nein: Kette endet, Zugwechsel
```

- `ChainCounter` lebt in der `CombatSession` und wird per Event an `ChainCounterView` gemeldet (GDD-TODO "ChainCounter in CombatScene").
- Der eigene Charakter darf ebenfalls chainen, aber die Kette ist **begrenzt** (Entscheidung zu A3). Die Obergrenze (`ChainConfig.max`) ist daher Pflicht und bricht jede Kette zuverlässig ab. Welche Zusatzregeln dazukommen (z. B. gleicher Skill nur einmal pro Charakter und Kette), ist offen.
- Finisher bei maximalem Zähler und die Skill-Begrenzung (GDD-TODO) sind als Regel-Strategien (`IChainRule`) vorgesehen und **konfigurierbar, nicht fest verdrahtet**. Pflicht ist die Basis-Kette (Status ↔ Follow-up) mit Obergrenze.
- Das Gegenstück gilt für Gegner (GDD: "Enemies basically work exactly like the player characters").

### 5.5 Run, Party, Progression (GDD 2.1, 2.2.6)

| Komponente | Verantwortung |
|---|---|
| `RunState` | Aktuelle Stage-Nr, Party, Inventar, Seed, Charaktername |
| `Party` | Mitglieder (Spieler + gefundene), Namen aus vordefiniertem Pool (GDD 3.3) |
| `PowerUpInventory` | Gefundene Rüstungen und Chaining-Skills, **Skill-Slots** je Charakter (einzige Ressource laut GDD 2.2.6) |
| `Score` | Höchste erreichte Stage-Nr (Highscore-Ansatz, GDD 2.1.1) |
| `ExperienceService` | XP für besiegte Gegner. Level-Ups heben Stats (GDD 2.1.5 "Risiko vs. XP") |

### 5.6 Gegner-KI (GDD 6.1)

Zwei getrennte Aufgaben, zwei Schnittstellen:

| Aufgabe | Schnittstelle | MVP-Implementierung |
|---|---|---|
| Entscheidung im Kampf | `IEnemyBrain.ChooseAction(CombatSession, Combatant)` | `FsmEnemyBrain`: Zustände `Aggressive` / `Heal` (HP < 30 % → Heil-Skills bevorzugen), Ziel in Kill-Range bevorzugt. **Fallback `RandomEnemyBrain`**, falls FSM zu komplex (GDD-Vorgabe) |
| Bewegung auf der Stage | `IStageEnemyMover.Step(StageSession, Enemy)` | Spieler in Sichtweite → `Pathfinder` folgt, sonst Patrouille / Zufallsschritt. Keine Wandkollision |

### 5.7 Persistenz (Must: persistentes Offline-Leaderboard, GDD 2.4)

`ILeaderboardStore` mit `Load()` / `Add(entry)`. MVP: `JsonLeaderboardStore` unter `Application.persistentDataPath`. Eintrag: Name, höchste Stage, Datum, Seed. Die Schnittstelle erlaubt später einen Online-Store (GDD 7.3), ohne Aufrufer zu ändern. Nichts anderes wird gespeichert (keine Run-Saves wegen Permadeath).

### 5.8 Input (GDD 5.2, 2.3)

`InputRouter` definiert die Aktionen (`Move`, `Interact`, `Menu`, `Navigate`, `Confirm`, `Cancel`) **im Code** und setzt je Spielzustand die aktive Action Map (`Exploration` / `Combat` / `Ui`). Rebinding über die Rebinding-API des Input Systems (GDD: "Mappings sollten angeboten werden").

*Abweichung von v0.1:* Dort war vorgesehen, das vorhandene `InputSystem_Actions.inputactions` umzubauen. Das Standard-Asset enthält Aktionen eines 3D-Charakters (Look, Jump, Crouch, Sprint), die das Spiel nicht braucht. Code-Definition ist kleiner und besser testbar. Das Asset bleibt unberührt und ungenutzt.

### 5.9 UI (GDD 5.1)

uGUI (vorhanden) mit schlanken Presentern. HUD: Exploration minimal (Benachrichtigungen), Kampf: HP-Anzeigen, Aktionsmenü, ChainCounter, optional Combat-Log (`CombatLogView` hört auf `SkillUsed`/`DamageDealt`). Statsmenü-Kontextfenster (GDD-TODO) ist eine Erweiterung.

### 5.10 Audio (GDD 5.3, 9.2 Should)

`AudioService` (Boot-Szene, `DontDestroyOnLoad`): Event → Clip-Mapping per `AudioCueTable` (ScriptableObject). Vorhandene Clips in `Assets/Export/Audio`. Musik je Stage über `BiomeDef.musicTrack`, keine eigene Kampfmusik im MVP.

---

## 6. Datenmodell (CC.Data, ScriptableObjects)

| Definition | Wichtige Felder | Nutzer |
|---|---|---|
| `SkillDef` | Name, Power, Zielregel, `appliesStatus`, `followUpOn`, Icon, Effekt-Animation, SFX | Combat |
| `StatusDef` | Id, Dauer, Stapelregel | Chaining |
| `EnemyDef` | Basisstats, Skills, Sprite/Animator, KI-Profil, Spawn-ab-Stage | StageGenerator, Combat |
| `PowerUpDef` | Typ (Rüstung / Skill), Werte, Skill-Referenz | Inventory |
| `BiomeDef` | Tile-Palette, Musik, Gegnerpool, Stage-ab | StageView, Generator |
| `ChainConfig` | Maximalwert, Regeln | ChainResolver |

Core-Typen werden nie von Assets referenziert, sondern erhalten Kopien über Konverter. Design-Änderungen erfolgen im Inspector ohne Codeänderung (Z2).

---

## 7. Abhängigkeitsregeln

1. `CC.Core` darf kein `UnityEngine` verwenden.
2. Views enthalten **keine** Spielregeln (nur Darstellung und Input-Weitergabe).
3. Systeme innerhalb Core kommunizieren über Interfaces oder `GameEvents`, nie über `FindObjectOfType`/Singletons. Einzige Ausnahme: Services in der Boot-Szene (Audio, Leaderboard) über schmalen `Services`-Locator.
4. Kein Eintrag in `Update()` ohne Notwendigkeit. Spiellogik ist ereignisgetrieben (rundenbasiert), Views animieren per Coroutine/Tween.
5. Neue Inhalte (Skill, Gegner, Power-up) = neues Asset, nicht neue Klasse.

---

## 8. Teststrategie (UF1/UF2: Debugging und Testing)

| Ebene | Beispiele |
|---|---|
| EditMode (Core) | Stage immer lösbar (Treppe erreichbar) für 1000 Seeds · Chain-Auflösung mit Status-Match / ohne Match · Zugreihenfolge nach Speed · FSM wählt Heilung < 30 % HP · Leaderboard (Serialisieren/Laden) |
| PlayMode (Smoke) | Boot → Hub → Stage lädt · Bewegung gegen Wand blockiert · Kollision startet Kampf |
| Manuell | Gamefeel, UI-Lesbarkeit, Audio |

Tests laufen über `tests-run` (Unity-MCP). Jeder Systemabschluss endet mit grünem EditMode-Lauf.

---

## 9. Umsetzungsreihenfolge (entspricht Meilensteinplan)

| Meilenstein | Inhalt | Abhängigkeiten |
|---|---|---|
| **Fundament** (vor 03.10.) | Assembly-Struktur, `GameEvents`, `IRng`, Boot-Szene, `GameFlow`-Gerüst, `InputRouter` | – |
| **M-Stage** (03.10.) | `TileGrid`, `StageGenerator`, `StageSession`, `Pathfinder`, `StageView`, `PlayerController`, Treppe, Power-up-Interaktion, Gegner-Bewegung, Stage-Skalierung | Fundament |
| **M-Combat** (14.11.) | `Combatant`, `Skill`, `CombatSession`, `TurnOrder`, `ChainResolver`, `FsmEnemyBrain`/`Random`, XP, Party, `CombatView` (rudimentär) | M-Stage (Kollisionsauslöser) |
| **M-UI** (12.12.) | HUD, ChainCounter, Combat-Log, Hub + Leaderboard, Namenseingabe, Pausenmenü, Audio-Zuordnung, Persistenz | M-Combat |
| **Finalisierung** (16.01.) | Balancing, Bugfixing, Messdaten, Dokumentation | alle |

Jedes System bekommt: EditMode-Tests, einen Eintrag im Zeitlog (`Zeitlog.md`) und dokumentierte Abweichungen von diesem Konzept.

---

## 10. Erweiterungspunkte (GDD-TODOs, nicht MVP-Pflicht)

| GDD-TODO | Vorbereitung |
|---|---|
| Angriffseffekte, die die Zugreihenfolge beeinflussen (Autor, zu A14) | `TurnOrder` bekommt eine Modifikatorliste (z. B. Speed-Änderung, Zug verschieben). Ketten selbst ändern die Reihenfolge nicht |
| Finisher bei maximalem ChainCounter | `IChainRule` + `ChainConfig.max` |
| Gleichen Skill pro Kette begrenzen | `IChainRule` |
| Gegner nutzen Heal-Skills | bereits in `FsmEnemyBrain` (HP-Schwelle) |
| Statuseffekte, Schadensmultiplikatoren | `StatusDef`, `DamageCalculator` mit Modifikatorliste |
| Klassenmodule (Statwachstum, Passives) | `ClassDef`-ScriptableObject, Reference in `Combatant` |
| Meta-Progression, Biome | `MetaProfile` neben Leaderboard-Store, `BiomeDef` bereits vorhanden |
| Online-Leaderboard | zweite Implementierung von `ILeaderboardStore` |
| Dev-Modus (GDD 2.5) | Debug-Overlay, Seed-Eingabe (Seed-Architektur bereits vorhanden) |

---

## 11. Annahmen der KI bei unklaren GDD-Stellen (für UF4)

Bei der Erstellung von Version 0.1 von der KI getroffen und anschließend vom Autor geprüft. Zur Dokumentation und späteren Abweichungsanalyse (UF4). Die Spalte "Prüfung" zeigt, ob die Annahme der KI bestätigt, präzisiert oder verworfen wurde.

| Nr | GDD-Stelle | Mehrdeutigkeit | Annahme der KI (v0.1) | Prüfung durch Autor |
|---|---|---|---|---|
| A1 | 1.4 "Enemies scouting the area" | Bewegen sich Gegner in Echtzeit oder rundenweise? | Rundenweise, ein Spielerschritt = ein Gegnerzug | **Präzisiert:** Gegner ziehen nur, nachdem sich der Spieler bewegt hat |
| A2 | 2.2.2 "similar to Mystery Dungeon" | Nur Grid oder auch Rundenmodell der Serie? | Beides übernommen | **Bestätigt** |
| A3 | 1.1 "other party members can join the chain" | Wer darf einsteigen: nur Partymitglieder oder auch derselbe Charakter? | Beide, Begrenzung als konfigurierbare Regel | **Präzisiert:** Der Charakter darf selbst chainen, aber nicht unendlich (Obergrenze Pflicht) |
| A4 | 2.2.5 "usually the player gets to start" | Wann nicht? | Spielerstart bleibt Standard, Ausnahme als Regel | **Verworfen:** Der schnellste Charakter im Kampf zieht zuerst, dann der nächstschnellere (gemeinsame Reihenfolge aller Kämpfer) |
| A5 | 2.2.1 Kollision mit Gegnern | Wird bei Kampfstart der Gegner entfernt? | Bei Sieg ja, bei Niederlage endet der Run | **Bestätigt** |
| A6 | 2.1.5 "fighting for XP" | Gibt es Flucht? | Nein, nicht im GDD | **Verworfen:** Flucht ist möglich, der Gegner verschwindet dabei von der Stage |
| A7 | 3.3 Party | Wie kommen weitere Partymitglieder ins Spiel? | Fund auf der Stage, Namen aus Pool | **Bestätigt** |
| A8 | 3.2.1 Hub | Begehbar? | Nein, Menü-Bild mit Leaderboard | **Bestätigt** |
| A9 | 4.1 "endless stages" | Generatorverfahren? | Räume + Gänge, seed-basiert | **Bestätigt** |
| A10 | 9.1 Leaderboard | Eintrag pro Run oder nur bei Highscore? | Pro beendetem Run, Top-10 | **Bestätigt** |

Folgeannahmen aus den Entscheidungen zu A1–A10. Aus Version 0.2 vorgeschlagen, vom Autor geprüft:

| Nr | Bezug | Frage | Annahme der KI | Prüfung durch Autor |
|---|---|---|---|---|
| A11 | A6 Flucht | Gelingt die Flucht immer? Kostet sie etwas? | Gelingt immer, kostet nichts, `fleeChance` (Standard 1.0) als Parameter | **Bestätigt:** Flucht bleibt garantiert, bis weitere Balancing-Entscheidungen fallen |
| A12 | A3 Kettenobergrenze | Welcher Wert? Pro Zug oder pro Kampf? | Pro Kette, Standardwert 5 in `ChainConfig` | **Präzisiert:** Die Obergrenze gilt je Kette. Der Wert 5 bleibt vorläufig |
| A13 | A4 Gleicher Speed | Wer zieht bei Gleichstand zuerst? | Spieler-Party vor Gegnern, dann feste Party-Reihenfolge | **Bestätigt** |
| A14 | A4 Chaining und Reihenfolge | Verändert ein Follow-up die Zugreihenfolge? | Follow-ups sind Bonusaktionen im Zug des Auslösers, Reihenfolge bleibt | **Bestätigt:** Ketten beeinflussen die Reihenfolge grundsätzlich nicht. Später können Angriffseffekte die Reihenfolge verändern (siehe Abschnitt 10) |

Neue Annahmen der KI beim Stage System (Meilenstein 03.10.). **Alle bestätigt** vom Autor am 11.10.2026:

| Nr | GDD-Stelle | Mehrdeutigkeit | Annahme der KI |
|---|---|---|---|
| A15 | 1.4 "stairs to the next stage", 2.2.3 | Wird die Treppe automatisch betreten oder aktiv benutzt? | Man läuft auf die Treppe und drückt die Interact-Taste. Kein automatisches Hinabsteigen, damit man sie versehentlich nicht auslöst |
| A16 | 2.2.3 "standing on an adjacent tile, facing the object" | Sind Power-ups begehbar? | Nein, Pickups blockieren die Bewegung. Eingesammelt wird vom Nachbarfeld, mit Blick auf das Objekt, per Interact-Taste |
| A17 | 2.2.2 grid-based, Mystery Dungeon | Kostet das Drehen oder ein Schritt gegen eine Wand einen Zug? | Nein. Ein blockierter Schritt dreht nur den Spieler, die Gegner ziehen nicht. Einsammeln und Gehen kosten je einen Zug |
| A18 | 6.1 "seek the player" | Ab wann verfolgen Gegner den Spieler? | Wenn ein Laufweg von höchstens 7 Schritten existiert, sonst wandern sie (50 % pro Zug ein Schritt). Begegnet ein Gegner dem Spieler, warten alle übrigen in diesem Zug |
| A19 | 4.1 "increasing difficulty", 3.2.1 palette swap | Wie steigt die Schwierigkeit, wie sehen Stages aus? | Stagebreite 36 + 2n (max 64), Höhe 26 + n (max 44). Gegner 2 + n/2 (max 14) mit Level n. Pickups 2 + n/5 (max 4). Biome (Meadow, Snow, Dungeon) wechseln alle 3 Stages und zyklieren. Platzhalter ohne GDD-Grundlage, Balancing später |
| A20 | 2.2.6 | Welche Pickup-Arten gibt es und wie oft? | Armor, ChainSkill, Recruit im Verhältnis 2:2:1. Der Effekt (Inventar, Party) kommt mit dem Combat-Meilenstein, jetzt wird nur eingesammelt und das Ereignis gesendet |
| A21 | 1.4 "enemies scouting the area" | Wie nah darf ein Gegner beim Start sein? | Mindestens 6 Laufschritte vom Startfeld. Die Treppe liegt im Raum mit der größten Laufentfernung zum Start |
| A22 | Baseline-Gegner | Welche Gegner gibt es ab welcher Stage? | Nach den vorhandenen Sprites: mouse (1), hooddude (2), evileye (3), walrus (4), livingarmour (5), magmaegg (7). Verteilung auf Biome ist Platzhalter |

Neue Annahmen der KI bei der Fehlerbehebung am Stage System (11.10.2026). **Beide bestätigt** vom Autor am 11.10.2026. Der Autor hat die Aufgabe "suche nach einer Lösung" gestellt, die konkrete Lösung ist eine Entscheidung der KI:

| Nr | Bezug | Mehrdeutigkeit | Annahme der KI |
|---|---|---|---|
| A23 | 2.2.3 "facing the object", Baseline-Grafiken | Die Baseline hat nur eine Idle-Animation (nach unten). Wie sieht der stehende Spieler in die anderen Richtungen aus? | Nach unten bleibt die vorhandene animierte Idle-Animation. Nach oben, links und rechts steht der Spieler in der neutralen Pose (erstes Bild) des jeweiligen Geh-Clips, als stehendes Einzelbild. Rechts nutzt den vorhandenen `walkRight`-Clip, es wird nicht mehr gespiegelt. Neue Grafiken wurden nicht erzeugt |
| A24 | 2.2.2 Rundenmodell, Geh-Animation | Wie lange dauert ein Geh-Zyklus im Verhältnis zum Schritt (0,14 s)? Wann geht der Spieler in Idle? | Ein Zyklus (Bein links, Mitte, Bein rechts, Mitte) umfasst zwei Schritte, also ein Fuß je Schritt (`stepsPerWalkCycle` = 2, einstellbar). Der Zyklus läuft über aufeinanderfolgende Schritte durch und wird nicht pro Schritt neu gestartet. Idle erst nach 0,06 s ohne weitere Eingabe |

Neue Annahmen der KI beim Combat System (Meilenstein 14.11., umgesetzt am 11.10.2026), **noch nicht** vom Autor geprüft. Alle Zahlenwerte sind Platzhalter ohne GDD-Grundlage und werden in der Finalisierung balanciert:

| Nr | GDD-Stelle | Mehrdeutigkeit | Annahme der KI |
|---|---|---|---|
| A25 | 2.1.1 "increasing difficulty", 6.1 | Wie stark sind Gegner und Held? | Held: 30 LP, ANG 5, VER 2, TMP 10, je Level +6/+1/+1/+1. Gegner (Level 1): Maus 14/3/0/12, Kapuzenmann 20/4/1/9, Böses Auge 18/5/0/8, Walross 38/5/3/5, Lebende Rüstung 34/6/5/4, Magmaei 28/8/2/7. Je Level +4 LP und +1 ANG. Gegnerlevel = Stagenummer (A19). Die Maus nutzt die Zufalls-KI, alle anderen die FSM |
| A26 | 2.1.5 "fighting for XP" | Wie viel XP, an wen? | Belohnung = `BaseXp` des Gegners mal sein Level, jeder lebende Charakter bekommt den vollen Betrag (keine Aufteilung). Schwelle zum nächsten Level: 20 mal aktuelles Level |
| A27 | 2.1.5 "worth it or too risky regarding their health pool" | Regenerieren Lebenspunkte? | Nein. LP bleiben über Kämpfe und Stages erhalten. Ein Level-Up heilt nur den Zuwachs der Max-LP, keine Vollheilung. Heilen geht nur mit Heil-Skills im Kampf (`Mend`) |
| A28 | 2.2.5 | Kosten Skills etwas (MP, Abklingzeit)? | Nein. Jeder Charakter hat pro Runde eine Aktion, die Wahl des Skills ist die einzige Entscheidung. Der Held startet mit Slash und Mend |
| A29 | 3.3, 8 | Wie sehen gefundene Partymitglieder aus? | Es gibt nur die Grafik des Helden. Rekruten nutzen ihren Animator und unterscheiden sich durch eine Farbtönung. Keine neuen Grafiken erzeugt |
| A30 | 2.2.6 Economy | Gibt es ein Inventar-Menü? Wie werden Power-ups verteilt? | Nein, die Verteilung ist automatisch. Rüstung: Verteidigung +1 für alle. Chain-Skill: Zufallsskill aus dem Pool an das erste Mitglied mit freiem Slot, das ihn noch nicht kennt (4 Slots je Charakter), sonst verfällt er. Rekrut: Typ und Name zufällig aus dem Pool, nicht bei voller Party |
| A31 | 1.1 "if the afflicted status matches" | Was löst eine Kette aus, wie lange hält ein Status? | Der Status, den die Aktion **gerade** auf das überlebende Ziel gelegt hat, nicht ein bestehender. Ein Status hält 3 Züge des Betroffenen, erneutes Auflegen frischt auf (kein Stapeln). Er hat sonst keine Wirkung (kein Schaden über Zeit) |
| A32 | 3.3 | Wie groß ist die Party? | Höchstens 4 Charaktere |
| A33 | A7 | Auf welchem Level kommt ein Rekrut? | Auf dem durchschnittlichen Level der Party (abgerundet, mindestens 1), mit der bisherigen Rüstung |
| A34 | 2.1 Permadeath | Was passiert mit gefallenen Partymitgliedern? | Sie bleiben für den Rest des Runs kampfunfähig (kein Wiederbeleben). Der Run endet, wenn alle gefallen sind |
| A35 | A3, A12, 1.1 | Was zählt die Kettenobergrenze (5)? Wer darf einsteigen? | Die Zahl der Aktionen der Kette **einschließlich** der auslösenden, also höchstens 5. Einsteigen darf der schnellste Lebende der handelnden Seite mit passendem Skill, auch der Auslöser selbst und derselbe Charakter mehrmals. Follow-ups sind Schadensskills gegen dasselbe Ziel. Die Zusatzregel "gleicher Skill nur einmal je Charakter" ist umgesetzt, aber standardmäßig aus |
| A36 | 2.2.1 | Wie viele Gegner kämpfen? | Nur der Gegner, mit dem der Spieler kollidiert ist, gegen die ganze Party. Weitere Gegner der Stage schließen sich nicht an |
| A37 | 2.2.5 | Schadensformel | Schaden = Skill-Power + Angriff des Nutzers − Verteidigung des Ziels, mindestens 1. Heilung = Power + Angriff/2, begrenzt auf die Max-LP. Kein Zufall, keine Fehlschläge |

---

## 12. Risiken

| Risiko | Gegenmaßnahme |
|---|---|
| Kampfregeln bei Chaining komplexer als erwartet | Basis-Kette zuerst, Regeln über `IChainRule` nachrüsten |
| Unity-spezifischer Aufwand (Szenen, Prefabs, Tilemap) schlecht über MCP/Text automatisierbar | Möglichst Logik in Core, Szenen minimal, Aufbau über MCP-Werkzeuge. Aufwand und Probleme im Zeitlog erfassen (UF2) |
| Kontextverlust bei wachsender Codebasis | Dieses Dokument und `CLAUDE.md` als dauerhafter Kontext, Systemübersicht aktuell halten |
| Zu feingliedrige Architektur für ein MVP | Nur drei Assemblies, keine DI-Frameworks, kein ECS |

---

## 13. Abweichungen und Befunde während der Umsetzung

Stand Combat System. Jeder Punkt ist ein Material für UF2 (Limitationen) oder UF3 (Architektur).

### 13.1 Abweichungen vom Konzept

| Nr | Konzept | Umsetzung | Grund |
|---|---|---|---|
| D1 | `Pathfinder` mit A\* (5.1) | Breitensuche (`DistanceMap`, `Pathfinder`) | Alle Schritte kosten gleich, BFS liefert dieselben kürzesten Wege, ist kürzer und wird zusätzlich für Erreichbarkeitsprüfungen genutzt |
| D2 | `PlayerController` ruft `StageSession.TryMove` (5.1) | `PlayerController` liefert nur Absichten (`PlayerIntent`), `StageController` führt sie aus und animiert | Trennt Eingabe (Puffer, Dauerlauf) von Ablaufsteuerung und Animation |
| D3 | `InputSystem_Actions.inputactions` umbauen | Aktionen im Code definiert (`InputRouter`) | siehe 5.8 |
| D4 | Zusätzlich zum Konzept | `StageValidator`, `DistanceMap`, `TurnResult`, `RunState`, `ContentBuilder` (Editor) | Notwendig für testbare Garantien (Treppe erreichbar), Animation der Views und reproduzierbaren Inhaltsaufbau |
| D5 | Kampfübergang erst im Combat-Meilenstein | Übergang `Exploring → Combat → Exploring` ist verdrahtet, der Kampf selbst ist der Platzhalter `TempCombatStub` | Damit Gegnerkollision, Entfernen des Gegners (Sieg und Flucht) und der Wechsel der Eingabe schon jetzt prüfbar sind |
| D6 | Baseline-Assets in `Assets/Export` bleiben unverändert (CLAUDE.md) | Die Importeinstellungen (Pivot) von `Tiles-Sheet.png` (16 Slices) und `SnowTile.png` wurden auf Mitte gesetzt. Nur die `.meta`-Dateien ändern sich, die Bilddateien nicht. Umsetzung durch `SpritePivotNormalizer`, aufgerufen vom `ContentBuilder` | Ausdrücklicher Auftrag des Autors (11.10.2026): alle Sprites verwenden denselben Pivot (B5) |
| D7 | Player-Animator mit `idle` und drei Geh-Zuständen (v0.3) | Acht Zustände: `idle` und `walk` je Richtung (Oben, Unten, Links, Rechts), Float-Parameter `walkSpeed`. Geh-Clips werden vom `ContentBuilder` auf gleichmäßige Phasen und genau eine Sekunde Länge normiert, Idle-Posen aus den Geh-Clips abgeleitet. `PlayerView` kennt `Face`, `Walk`, `StopWalking` | Behebung von B6 und B7, siehe A23 und A24 |
| D8 | Ein `PowerUpInventory`, `ExperienceService` und `Score` (5.5) | Kein Inventar. XP und Levelaufstieg in `PartyMember.GainXp`, Power-up-Wirkung in `PowerUpEffects` (A30). `Score` entfällt bis zum UI-Meilenstein (Leaderboard) | Ein Inventar ohne Auswahlmenü wäre eine leere Hülle. Die Logik ist trotzdem getrennt und getestet |
| D9 | `ChainConfig` als ScriptableObject (6) | `ChainConfig` ist eine Core-Klasse, die Werte (`chainMax`, `sameSkillOncePerCharacter`) liegen als Felder in `GameContent` | Zwei Werte rechtfertigen kein eigenes Asset |
| D10 | `StatusEffect` verwaltet Stapelregeln (5.3) | Status sind Marker mit Dauer, wiederholtes Auflegen frischt nur auf (A31). `StatusDef` enthält nur Name und Farbe, die Dauer steht am Skill | Für den MVP genügt es, Stapelregeln sind ein Erweiterungspunkt |
| D11 | `CombatView` als eigene Ansicht, `ChainCounterView` (5.3, 5.4) | Eine Klasse `CombatController` (IMGUI plus Sprites im Kamerabild). Der Kettenzähler ist Teil davon, keine eigene View. `TempCombatStub` ist gelöscht | Meilenstein verlangt "rudimentär", das HUD kommt im UI-Meilenstein (13.3) |
| D12 | `CombatSession` kennt Phase `Resolving` (5.3) | Die Session ist synchron. `Resolving` und `NotStarted` existieren nur als Zustand während eines Aufrufs. Statt Phasen-Ereignissen liefert jeder Aufruf eine Liste von `CombatEvent`, die die View abspielt | Einfacher zu testen als Callbacks, und die Animation bleibt aus dem Core |
| D13 | Core-Typen werden nie von Assets referenziert (6) | Zusätzlich `PartyMemberSpec`, `EnemySpec` als Core-Kopien von `PartyMemberDef` und `EnemyDef` sowie `IPartyCatalog`, das `GameContent` implementiert | Die Konverter-Regel aus 6, angewendet auf die neuen Definitionen |

### 13.2 Befunde

| Nr | Befund | Bedeutung |
|---|---|---|
| B1 | **Animationen der Baseline zeigen kein Sprite.** Die `.anim`-Dateien in `Assets/Export` referenzieren Sprites über eine Textur-GUID, die nicht zu den importierten Texturen dieses Projekts passt (nur die GUID weicht ab, die internen Sprite-IDs stimmen). Der Animator läuft, setzt aber `sprite = null`. | Entdeckt erst im Play Mode nach Diagnose (Controller, Binding-Pfad, GUID-Vergleich). Der Fehler lag in den Eingangsdaten, nicht im KI-Code. Lösung: `ContentBuilder` baut Kopien der Clips mit gleichem Timing gegen die aktuellen Sprites. Baseline-Dateien bleiben unverändert |
| B2 | **Testumgebung:** Ein nicht fokussierter Unity-Editor lässt den Player Loop im Play Mode praktisch stehen (Frame-Zähler bleibt), und das Input System verwirft Tastatureingaben. | Spieltests laufen nur mit `EditorApplication.Step()` und simulierten Events (Reflection auf `InputRouter`). Echte Tasten- und Echtzeittests sind per MCP nicht möglich und müssen vom Autor manuell geprüft werden |
| B3 | **Tile-Auswahl nach Sicht.** Die erste Auswahl der Boden-Dekoration nach Index enthielt Objekte (Topf, Wasserstelle), die wie Pickups aussahen. Erst ein beschriftetes Bild des Tile-Sheets klärte die Zuordnung. | Die KI kann Sprite-Inhalt nur über gerenderte Bilder beurteilen. Index-Annahmen aus Metadaten sind unzuverlässig |
| B4 | Die Debug-Ausgabe von `console-get-logs` liefert immer Stacktraces, auch mit `includeStackTrace: false`. | Erhöht den Kontextverbrauch bei der Diagnose (UF2) |
| B5 | **Pivot der Bodentiles.** Die Slices von `Tiles-Sheet.png` waren mit Alignment "unten links" importiert, `SnowTile.png` ebenso. Entities und alle anderen Sprites haben einen Mittelpunkt-Pivot. Ergebnis: Der Spieler stand um 0,5 Einheiten in x und y versetzt zum Boden. Alle übrigen 156 Sprites waren schon zentriert. | Ursache in den Eingangsdaten (Baseline), nicht im KI-Code. Das Stage System hat den Pivot beim Zeichnen der Tiles nicht geprüft, sondern nur Pixel pro Einheit ausgeglichen. Bei der Abnahme am 02.10. wurde der Versatz nicht bemerkt (UF2: visuelle Fehler bleiben ohne Bildprüfung unsichtbar). Test `AllExportedSpritesShareTheCentrePivot` sichert das nun ab |
| B6 | **Geh-Animation zeigte ein Bild.** Ein Schritt dauert 0,14 s, die Geh-Clips der Baseline 0,6 bis 1,4 s. Der Code rief pro Schritt `Animator.Play` auf (Neustart bei 0) und danach sofort Idle. Es erschien nur das erste Bild des Zyklus, zwischen den Schritten blitzte Idle auf. | Fehler im KI-Code. Die Prüfung am 02.10. kontrollierte nur, dass ein Sprite sichtbar ist (B1), nicht den zeitlichen Verlauf. Jetzt als Bildfolge pro Frame belegt (siehe Zeitlog, Kennzahlen Fehlerbehebung) |
| B7 | **Gespiegelter Clip statt vorhandenem `walkRight`.** `PlayerView` hat Rechts durch Spiegeln von `walkLeft` ersetzt, obwohl `walkRight.anim` mit eigenen Sprites existiert (Atlas-Zeile 5). | Eigene Annahme ("drei Richtungen") wurde nicht gegen die Assets geprüft. Fehler im KI-Code, beim Beheben von B6 entdeckt |
| B8 | **Länge von Sprite-Clips.** Unity setzt die Länge eines Sprite-Clips auf letzten Key plus ein Bild (bei 60 fps 1,0167 s statt 1 s). | Aufgefallen durch einen fehlgeschlagenen Test beim ersten Lauf. Der Schlusskey liegt nun ein Bild vor der Sekunde |
| B9 | **Kampfansicht nicht als Bild prüfbar.** Die IMGUI-Oberfläche (Menü, Lebensbalken, Log) erscheint nicht in einem Kamera-Render, und `ScreenCapture` liefert im nicht fokussierten Editor keine Datei. Geprüft sind nur Zustand, Log und die Figuren im Kamerabild. | Das Aussehen und die Lesbarkeit des Kampfmenüs sind unbelegt und müssen vom Autor angesehen werden (manuell, Konzept 8). Erweitert B2 |
| B10 | **Verschachtelte Coroutinen kosten ein Bild.** Ein `yield return` auf eine weitere Coroutine setzt erst im nächsten Frame fort, auch bei Wartezeit 0. Im angehaltenen Editor blieb ein Kampf deshalb mitten in der Wiedergabe hängen. | Der Kampf hat einen Sofortmodus (`actionSeconds` = 0), der gar nicht yieldet, analog zu `stepSeconds` auf der Stage. Danach liefen ganze Kämpfe in einem MCP-Aufruf |
| B11 | **Werkzeugumgebung:** Mehrere Heredocs mit Skript und Anführungszeichen in einem Shell-Aufruf scheiterten mehrfach (`unexpected EOF`). Dateien werden zuverlässig über das Schreibwerkzeug angelegt und per Skriptdatei gepatcht. Einzelne Testfehler lagen am Testcode selbst (geteilter Zustand zwischen Tests, `params` mit benanntem Argument), nicht an der Spiellogik | Kostet Iterationen, ändert aber das Ergebnis nicht (UF2) |

### 13.3 Temporäre Platzhalter (werden ersetzt)

| Klasse | Ersetzt durch | Meilenstein |
|---|---|---|
| `Game/Combat/CombatController` (IMGUI-Menü, Balken, Log) | HUD mit Kampfmenü, ChainCounter und Combat-Log | UI |
| `Game/Dev/StageDebugOverlay` (IMGUI) | HUD | UI |
| `Game/Hub/HubController` (IMGUI-Hinweis, Enter startet Run) | Hub mit Leaderboard und Namenseingabe | UI |
