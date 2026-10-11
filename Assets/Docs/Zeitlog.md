# Zeitlog – KI-Implementierung Chaining Chronicle

Zweck: Nachweis für UF1 (Effizienz) und UF3 (Architekturmetriken), Anforderung M3/M4.

**Messweise:** Wanduhrzeit zwischen Prompt und fertigem Ergebnis (KI-Laufzeit inkl. Tool-Aufrufen). Review-, Lese- und Korrekturzeit des Autors wird **separat** erfasst, damit KI-Zeit und menschliche Zeit unterscheidbar bleiben.

| Nr | Datum | Bereich | Aufgabe | Prompt-Kurzfassung | KI-Zeit | Review/Korrektur (Autor) | Ergebnis | Notizen |
|---|---|---|---|---|---|---|---|---|
| 1 | 02.10.2026 | Dokumentation | GDD und Methodik-PPT einlesen | Rahmenbedingungen genannt, Dokumente in `Assets/Docs` | n. g. | – | Zusammenfassung im Chat | Keine Messung (Vorbereitung) |
| 2 | 02.10.2026 | Setup | Unity-MCP-Zugriff testen | "Teste Zugriff auf Unity über MCP" | n. g. | – | Lesezugriff ok (Szenen, Console) | Console zeigt ältere Auth-Warnung (Reauth lief durch) |
| 3 | 02.10.2026 | Architektur | Architekturkonzept aus GDD | "Erstelle ein Architekturkonzept basierend auf dem GDD" | ca. 2 min (14:39–14:41) | offen | `Architekturkonzept_Chaining_Chronicle.md` v0.1 | 10 KI-Annahmen in Abschnitt 11, noch nicht vom Autor bestätigt |

| 4 | 02.10.2026 | Architektur | Annahmen A1–A10 vom Autor geprüft, Entscheidungen ins Konzept eingearbeitet (v0.2) | Antworten zu A1–A10 | n. g. (in Zeile 5 enthalten) | Review Autor ca. 1 h 45 min (14:41–16:28, geschätzt, enthält Pausen) | Konzept v0.2, neue Annahmen A11–A14 | A4 und A6 vom Autor verworfen (2 von 10 Annahmen falsch), A1/A3 präzisiert (2), 6 bestätigt |
| 5 | 02.10.2026 | Programmarchitektur | Fundament: Assemblies, `GameEvents`, `IRng`/`SeededRng`, `GameFlowMachine`, `GameFlow`, `InputRouter`, `Bootstrapper`, Szenen Boot/Hub/Stage | "Starte mit dem Fundament und lege die CLAUDE.md an" | ca. 5 min (16:28–16:33) | offen | 12 neue Dateien (Core 5, Game 5, Tests 2) + 4 asmdef, 3 Szenen, `CLAUDE.md`. 18 EditMode-Tests grün, Boot → Hub im Play Mode verifiziert | 1 Datei gelöscht (Template-Skript). Abweichung: Input im Code statt `.inputactions`. Ein wertloser Test wurde selbst wieder entfernt. Play-Mode-Wechsel trennt MCP kurz |

| 6 | 02.10.2026 | Architekturkonzept | A11–A14 vom Autor geprüft | Antworten zu A11–A14, "Baue jetzt das Stage System weiter" | n. g. (in Zeile 7 enthalten) | n. g. (kurz) | Konzept v0.3: alle vier bestätigt bzw. präzisiert (A12) | A14: neuer Erweiterungspunkt für Zugreihenfolge-Effekte |
| 7 | 02.10.2026 | Gameplay-Systeme | **Stage System** (Grid, Generator, Session, Gegnerbewegung, Pathfinder, Views, Controller, Content, Szenen) inkl. Dokumentation | "Baue jetzt das Stage System weiter" | ca. 23 min (16:41–17:04) inkl. Diagnose und Dokumentation | offen | Funktionsfähiger Ablauf Hub → Stage → Treppe → Stage 2 und Gegnerkontakt → Kampfplatzhalter → Flucht, 94 Tests grün | Größter Zeitanteil: Diagnose der unsichtbaren Sprites (B1) und Play-Mode-Tests im nicht fokussierten Editor (B2). Details in Konzept Abschnitt 13 |
| 8 | 11.10.2026 | Gameplay-Systeme | **Fehlerbehebung Stage System:** Pivot der Bodentiles (B5), Blickrichtung mit Idle je Richtung (A23, B7), Geh-Animation (B6, A24) | Drei visuelle Fehler benannt: Pivot, fehlende Idle-Animationen je Blickrichtung, Geh-Animation zeigt nur ein Bild und wird von Idle unterbrochen | ca. 12 min (12:49–13:01) inkl. Dokumentation | offen | Pivot aller 173 Sprites einheitlich Mitte, 8 Animator-Zustände (Idle und Gehen je Richtung), Zyklus läuft über Schritte durch. 102 Tests grün (+8), im Play Mode geprüft (Bildfolge je Frame, Screenshot der Stage) | Die Fehler B6 und B7 lagen im KI-Code, B5 in den Eingangsdaten. Zwei Entscheidungen der KI (A23, A24) vom Autor bestätigt, ebenso A15–A22 und die Dokumentation (D6, D7, B5–B8). Echtzeit und Optik (Tempo des Zyklus) per MCP nicht beurteilbar, bitte einmal in der Stage ansehen |

n. g. = nicht gemessen.

## Kennzahlen Fundament (für Folie 16)

| Metrik | Wert |
|---|---|
| Neue Dateien (`.cs`) | 10 (Core 5, Game 5) + 2 Testdateien |
| Neue Assembly Definitions | 4 |
| Bestehende Klassen verändert | 0 (nur Template-Skript gelöscht) |
| Gesamtzahl Typen im Produktivcode danach | 10 (davon 2 Enums, 1 Interface) |
| Tests | 18, alle grün |

## Kennzahlen Stage System (für Folie 16)

Zählung mit Skript (Typen per Regex, daher ca.). Fundament plus Stage System zusammen.

| Metrik | Fundament | nach Stage System | Differenz durch Stage System |
|---|---|---|---|
| Quelldateien Produktivcode (`.cs`) | 10 | 36 (Core 19, Game 13, Data 3, Editor 1) | +26 |
| Neue Dateien Tests | 2 | 10 | +8 |
| Typen Produktivcode | 10 | ca. 53 | ca. +43 |
| Zeilen Produktivcode | n. g. | ca. 2440 | n. g. |
| Zeilen Testcode | n. g. | ca. 1290 | n. g. |
| Bestehende Klassen verändert | – | 3 (`InputRouter`: `HeldMove`, `GameFlow`: Run/StartRun/ReturnToHub, `GameEvents`: 4 Ereignisse) | 3 von 10 |
| Neue Assembly Definitions | 4 | 5 (+ `CC.Editor`) | +1 |
| EditMode-Tests | 18 | 94 | +76 |
| Szenen | 3 (Boot, Hub, Stage) | 3 | 0 (Hub und Stage verdrahtet) |
| Content-Assets (per `ContentBuilder`) | 0 | 8 Animator Controller, 11 Clips, 6 `EnemyDef`, 3 `BiomeDef`, 1 `GameContent` | |

Zeitverlauf der KI-Arbeit am Stage System, nach Zeitstempeln der Werkzeugaufrufe (zur Einordnung der 23 min):

| Phase | Uhrzeit | Dauer |
|---|---|---|
| Assets sichten (Tile-Sheet, Sprites, Anim-Metadaten) | 16:41–16:45 | ca. 4 min |
| Core und Core-Tests schreiben, kompilieren, 84 Tests | 16:45–16:51 | ca. 6 min |
| Data-, Game- und Editor-Code, Kompilieren, 94 Tests-Vorbereitung | 16:51–16:53 | ca. 2 min |
| Content bauen, Szenen verdrahten, Play-Mode-Prüfung, Fehlersuche (B1–B3), Neubau, Tests | 16:53–17:02 | ca. 9 min |
| Dokumentation (Konzept, Log, CLAUDE.md) | 17:02–17:04 | ca. 2 min |

Beim ersten Kompilieren liefen alle 84 Core-Tests ohne Korrektur. Fehler traten nur bei den Inhalten und der Testumgebung auf (B1–B3).

## Kennzahlen Fehlerbehebung Stage System (11.10.2026, Zeile 8)

| Metrik | Wert |
|---|---|
| Neue Quelldateien (`.cs`) | 2 (`SpritePivotNormalizer`, `PlayerVisualsTests`) |
| Geänderte Quelldateien | 3 `.cs` (`ContentBuilder`, `EntityViews`, `StageController`) + 2 `.asmdef` |
| Geänderte Zeilen in `Assets/Scripts` und `Assets/Tests` (git diff, ohne neue Dateien) | +199 / -35 |
| Geänderte Klassen | 3 (`PlayerView` neu geschrieben, `StageController`: `Update`, `BuildStage`, `Execute`, `ContentBuilder`: Clip- und Controller-Aufbau) |
| Geänderte Baseline-Dateien (`.meta` in `Assets/Export`) | 2 |
| Neue / geänderte Clips in `Assets/Content/Animations` | 4 neu (`idleUp`, `idleLeft`, `idleRight`, `walkRight`), 3 geändert |
| Spieler-Animator | 4 → 8 Zustände, 1 Parameter (`walkSpeed`) |
| EditMode-Tests | 94 → 102 (+8, alle grün) |

Bildfolge der Geh-Animation nach der Behebung (60 Bilder/s, Schritt 0,14 s, 5 Schritte nach rechts, danach Stopp, Drehen nach oben, ein Schritt nach links):
`walkRight` Sprites 14, 15, 14, 16, 14, 15, 14, 16 ... ohne Idle dazwischen und ohne Neustart je Schritt. Danach `idleRight` (14), `idleUp` (8), `walkLeft` (11, 12), `idleLeft` (11). Vorher: nur Sprite 14 oder 11 je Schritt, danach Idle.

## Offene Punkte aus Zeile 3 (für Metriken Folie 16)

- Ist-Werte "Zeit" und "Änderungsauswirkung" der KI-Architektur werden erst nach der Umsetzung (Klassenanzahl, geänderte Dateien) belegbar.
- Annahmen A1–A10 vom Autor bestätigen oder korrigieren. Korrekturen als Abweichungen in UF4 festhalten.

## Token-Nutzung

Export: `Assets/Docs/Tokenlog.csv`, erzeugt mit `tools/Export-Tokenlog.ps1 -SkipLastPrompt` aus dem Sitzungsprotokoll von Claude Code (Stand 02.10.2026, 18:2x, Prompts 1–8 von 8 abgeschlossenen). Der Export wird bei jedem Meilenstein wiederholt und überschreibt die Datei.

| Prompt | Inhalt | Dauer | API-Aufrufe | Output-Tokens | Eingabe gesamt (verarbeitet) |
|---|---|---|---|---|---|
| 1 | Rahmenbedingungen, GDD und PPT lesen | 0,4 min | 3 | 2.247 | 151.211 |
| 2 | Unity-MCP testen | 0,3 min | 4 | 936 | 264.527 |
| 3 | Architekturkonzept | 1,8 min | 5 | 13.869 | 463.258 |
| 4 | Fundament, CLAUDE.md, Entscheidungen A1–A10 | 5,4 min | 25 | 32.042 | 3.269.726 |
| 5 | Stage System, Entscheidungen A11–A14 | 22,8 min | 76 | 164.568 | 22.247.717 |
| 6–8 | .gitignore-Prüfung, Commits, Push | 3,3 min | 14 | 16.431 | 5.670.591 |
| **Summe** | | **34,0 min** | **127** | **230.093** | **32.067.030** |

Einordnung:
- **Output** zählt alle erzeugten Tokens inklusive der internen Überlegungen des Modells (Thinking).
- **Eingabe gesamt** ist die Summe über alle Modellaufrufe. Jeder Aufruf liest den gesamten Kontext erneut, 98,6 % davon aus dem Prompt-Cache (31,6 Mio gelesen, 0,45 Mio geschrieben, nur 254 Tokens ungecacht). Die Zahl misst verarbeitete Tokens, nicht den Umfang des Projekts. Der Kontext wuchs im Lauf der Sitzung, deshalb steigt die Eingabe je Aufruf (Prompt 5: im Mittel ca. 290.000 Tokens pro Aufruf).
- **Dauer** reicht vom Prompt bis zur letzten Antwort und enthält die Laufzeit der Werkzeuge, aber nicht die Wartezeit auf den nächsten Prompt.
- Das Stage System (Prompt 5) allein macht 72 % der Output-Tokens und 60 % der Aufrufe aus.
- Der Export enthält nur diese Sitzung. Wird eine neue Sitzung gestartet, muss `-Transcript` auf die neue Datei zeigen (Standard ist die neueste).
