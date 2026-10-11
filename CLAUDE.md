# CLAUDE.md – Chaining Chronicle (KI-Implementierung)

Kontext: Bachelorarbeit. Chaining Chronicle wird nach dem GDD mit Claude Code neu implementiert, um Stärken und Schwächen einer KI-getriebenen Architektur zu untersuchen. Ein Roguelike mit rundenbasiertem Kampf und Chaining-Mechanik, Unity 6000.3.8f1, URP 2D, nur PC.

## Maßgebliche Dokumente (vor größeren Änderungen lesen)

- `Assets/Docs/GDD_Chaining_Chronicle.txt` – Anforderungen (Textfassung der .docx)
- `Assets/Docs/Architekturkonzept_Chaining_Chronicle.md` – Architektur, **verbindlich**
- `Assets/Docs/Zeitlog.md` – Zeitmessung und Prompt-Protokoll
- `Assets/Docs/RPG Spielearchitektur mittels KI.pptx` – Methodik, Forschungsfragen UF1–UF4

## Architekturregeln (Kurzfassung, Details im Architekturkonzept)

1. Drei Assemblies, Abhängigkeiten nur nach unten: `CC.Core` (reine Logik, **kein `UnityEngine`**) ← `CC.Data` (ScriptableObject-Definitionen) ← `CC.Game` (Views, UI, Audio, Input).
2. Views enthalten keine Spielregeln. Core sendet Ereignisse über `GameEvents`, Views hören zu.
3. Zufall nur über `IRng` (`SeededRng`), nie `UnityEngine.Random` oder `System.Random` in Core.
4. Inhalte (Skills, Gegner, Power-ups, Biome) sind ScriptableObject-Assets, keine neuen Klassen.
5. Kein `FindObjectOfType`/Singleton in Spiellogik. Einzige Ausnahme: `Services` für Boot-Services (GameFlow, InputRouter, später Audio, Leaderboard).
6. Spiellogik ist ereignisgetrieben, kein Spielzustand in `Update()`.
7. Stage und Kampf liegen in **einer** Szene (`Stage`). Der Kampf ist eine Ansicht darüber. Das Spiel startet immer in `Boot`.
8. Kein neues Package, kein DI-/Event-Framework ohne Rücksprache.

## Spielregeln, die der Autor festgelegt hat (Entscheidungen zu den Annahmen)

- Gegner ziehen auf der Stage nur, nachdem sich der Spieler bewegt hat (1 Spielerschritt = 1 Gegnerzug). Grid und Rundenbasis wie Mystery Dungeon.
- Im Kampf zieht der Charakter mit dem höchsten Speed zuerst, dann der nächstschnellere. Das gilt für alle Beteiligten gemeinsam, es gibt **keinen** Spielervorteil beim Start.
- Der eigene Charakter darf an der Kette teilnehmen, aber die Kette ist begrenzt (nicht unendlich).
- Flucht ist möglich. Bei Flucht verschwindet der Gegner von der Stage.
- Sieg: Gegner wird entfernt. Niederlage: Run endet (Permadeath).
- Flucht gelingt garantiert, bis weitere Balancing-Entscheidungen fallen (`fleeChance` 1.0). Die Kettenobergrenze gilt je Kette (Startwert 5). Bei Speed-Gleichstand zieht die Spieler-Party zuerst. Follow-ups sind Bonusaktionen im Zug des Auslösers, Ketten ändern die Zugreihenfolge grundsätzlich nicht. Später können Angriffseffekte die Reihenfolge ändern, `TurnOrder` braucht dafür Modifikatoren.
- **Noch ungeprüfte KI-Annahmen A15–A22** (Stage System: Treppe per Interact-Taste, Pickups blockieren und werden von vorne eingesammelt, blockierter Schritt kostet keinen Zug, Verfolgungsreichweite 7, Skalierung, Biome, Gegnerfreischaltung): siehe Architekturkonzept Abschnitt 11. Dem Autor zur Bestätigung vorlegen.
- Weitere Partymitglieder kommen als Fund auf der Stage, Namen aus festem Pool. Hub ist ein Menübild, kein begehbarer Raum. Stages sind Räume + Gänge, seed-basiert. Leaderboard-Eintrag pro beendetem Run, Anzeige Top-10.

## Arbeitsweise (Pflicht wegen der Methodik der BA)

- **Zeitlog führen:** Jede abgeschlossene Aufgabe bekommt eine Zeile in `Assets/Docs/Zeitlog.md` (Datum, Bereich, Prompt-Kurzfassung, KI-Zeit, Ergebnis, Notizen). Zeit per `date` messen.
- **Token-Export:** Nach jedem Meilenstein `tools/Export-Tokenlog.ps1 -SkipLastPrompt` ausführen (PowerShell, im Projektroot). Es schreibt `Assets/Docs/Tokenlog.csv` und gibt die Summen aus. Die Zusammenfassung im Abschnitt "Token-Nutzung" des Zeitlogs danach aktualisieren.
- **Annahmen dokumentieren:** Wenn das GDD oder ein Prompt etwas offen lässt, **keine stille Entscheidung**. Entweder nachfragen oder die Annahme im Architekturkonzept (Abschnitt 11) mit neuer Nummer festhalten (Material für UF4).
- **Abweichungen** vom Architekturkonzept sofort im Konzept und im Zeitlog vermerken.
- **Nicht über den Auftrag hinausgehen:** Nur das angefragte System bauen. GDD-TODOs (Finisher, Klassen, Meta-Progression) sind Erweiterungspunkte, keine Pflicht.
- Dateien im GDD-Rahmen halten (S1). Engine-spezifische Features (Tilemap, Input System, ScriptableObjects) bevorzugen und im Zeitlog erwähnen (S2).
- Commits nur auf Wunsch des Autors. Kleine Commits pro System.

## Unity-Zugriff (MCP `ai-game-developer`)

- Skripte schreiben, dann `assets-refresh` aufrufen (löst Kompilierung aus) und `console-get-logs` (Error) prüfen.
- Tests: `tests-run` mit `testMode: EditMode`. Alle offenen Szenen müssen gespeichert sein.
- Szenen und GameObjects über die `scene-*` und `gameobject-*` Werkzeuge. Nach Szenenänderungen `scene-save`.
- Der Wechsel in den Play Mode (Domain Reload) trennt die MCP-Verbindung kurz. Danach `list_engine_instances` und erneut versuchen (ca. 10 s warten).
- **Play-Mode-Tests per MCP:** Ist der Editor nicht fokussiert, tickt der Player Loop kaum und das Input System verwirft Tasten. Frames deshalb mit `EditorApplication.isPaused = true` und je Aufruf einem `EditorApplication.Step()` erzeugen. Eingaben simulieren, indem die C#-Events des `InputRouter` (`MoveRequested`, `InteractPressed`, `ConfirmPressed`, `CancelPressed`) per Reflection ausgelöst werden. `stepSeconds` des `StageController` auf 0 setzen, damit Animationen sofort enden. Immer vom `Boot`-Szene aus starten.
- **Screenshots:** Es gibt kein Game-View-Werkzeug. Kamera per `script-execute` in eine `RenderTexture` rendern, als PNG in das Scratchpad speichern und mit Read ansehen.
- `console-get-logs` zeigt immer Stacktraces. Mit `logTypeFilter`, `lastMinutes` und kleinem `maxEntries` eingrenzen. Die Exception `TargetParameterCountException` aus `com.unity.ide.visualstudio` ist bekannt und harmlos.
- Inhalte (Animator Controller, Clips, `EnemyDef`, `BiomeDef`, `GameContent`) entstehen über das Menü **Chaining Chronicle > Build Content** (`Assets/Scripts/Editor/ContentBuilder.cs`), nie von Hand. Die Baseline-Clips in `Assets/Export` zeigen wegen abweichender GUIDs kein Sprite, der Builder erzeugt korrigierte Kopien in `Assets/Content/Animations`. Wiederholbar.
- Namespaces: `CC.Core.*`, `CC.Data.*`, `CC.Game.*`, `CC.Tests.*`. Code-Bezeichner Englisch, Dokumentation Deutsch.

## Teststrategie

- Jede Core-Klasse bekommt EditMode-Tests in `Assets/Tests/EditMode` (Spiegelstruktur zu `Scripts/Core`).
- Ein Systemabschluss gilt erst, wenn alle EditMode-Tests grün sind und die Console keine Fehler zeigt.

## Aktueller Stand

- Fundament fertig: Assemblies, `GameEvents`, `IRng`/`SeededRng`, `GameFlowMachine`, `GameFlow`, `InputRouter`, `Bootstrapper`, Szenen `Boot`/`Hub`/`Stage` (in den Build Settings).
- **Stage System fertig** (Core: `TileGrid`, `DistanceMap`, `Pathfinder`, `StageGenerator`, `StageValidator`, `StageRules`, `StageSession`, `ChaseOrWanderMover`, `RunState`. Game: `StageView`, `StageController`, `PlayerController`, `EntityViews`, `StageCameraFollow`. Data: `EnemyDef`, `BiomeDef`, `GameContent`). 94 EditMode-Tests grün, Ablauf Hub → Stage → Treppe → nächste Stage und Gegnerkollision → Kampf → Flucht im Play Mode geprüft.
- Platzhalter, die ersetzt werden: `TempCombatStub` (Combat), `StageDebugOverlay` und `HubController`-IMGUI (UI). Siehe Architekturkonzept 13.3.
- Pickups werden eingesammelt und als `PowerUpCollected` gemeldet, haben aber noch keinen Effekt (Inventar kommt mit Combat/Party).
- Nächster Meilenstein: **Combat System** (`Combatant`, `Skill`, `CombatSession`, `TurnOrder`, `ChainResolver`, `FsmEnemyBrain`, XP, Party, `CombatView`). Der Einstieg `EnemyEngaged` und das Entfernen über `StageController.ResolveEngagement` sind vorhanden.

## Vorhandene Assets (Baseline, nicht verändern)

`Assets/Export/` enthält Sprites, Animationen und Audio der Baseline-Implementierung (Charakter, Gegner, Effekte, Tiles, MP3-Clips). Wiederverwenden, nicht neu erzeugen.
`Assets/Plugins/NuGet` und `Assets/Editor/ForceSonarMainProject.cs` gehören zum MCP-Plugin bzw. zum SonarQube-Setup, nicht anfassen.
