# Codeconventies

## Mappen en assemblies

Alle eigen code staat onder `Cities/Assets/Scripts/<Systeem>`. Elke map is één assembly definition. Een assembly mag alleen naar lagere lagen verwijzen; `AssemblyLayeringTests` bewaakt dat.

| Laag | Assembly | Inhoud | Mag verwijzen naar |
| --- | --- | --- | --- |
| 1 | `Cities.Core` | ID's, wiskunde, logging, configuratie, hulpmiddelen | — |
| 2 | `Cities.Simulation` | Simulatieklok, job-scheduling, command-bus, agents | Core |
| 3 | `Cities.World` | Tegels, terrein, water, wegennet, zones, gebouwdata | Core, Simulation |
| 4 | `Cities.Gameplay` | Camera, invoer, tools, spelregels | Core, Simulation, World, Input System |
| 5 | `Cities.Presentation` | Rendering, UI, audio, visuele effecten | alle runtime-lagen, URP |
| 6 | `Cities.Editor` | Editor-vensters, importers, inspectors (alleen editor) | alles |

Tests staan onder `Cities/Assets/Tests/EditMode` (`Cities.Tests.EditMode`) en `Cities/Assets/Tests/PlayMode` (`Cities.Tests.PlayMode`). Elke runtime-assembly geeft de testassemblies toegang tot `internal` via `AssemblyInfo.cs`.

Een nieuw systeem dat niet in een bestaande laag past, krijgt een eigen assembly; zet de laag erbij in deze tabel en in `AssemblyLayeringTests`.

## Namespaces

De namespace volgt de map: `Cities.<Laag>[.<Submap>]`, bijvoorbeeld `Cities.World.Roads`.

## Naamgeving

- Types, methoden, properties, events, constanten en `static readonly`-velden: `PascalCase`.
- Interfaces: `IPascalCase`.
- Private velden: `_camelCase`; geserialiseerde velden zijn `[SerializeField] private` met dezelfde naam.
- Lokale variabelen en parameters: `camelCase`.
- Jobs eindigen op `Job`, systemen op `System`, ScriptableObject-definities op `Definition`.
- Code en identifiers in het Engels; commentaar en documentatie in het Nederlands.

## Stijl

`.editorconfig` in de root legt de opmaak vast: vier spaties, LF, accolades op een eigen regel, altijd accolades, `using` buiten de namespace en expliciete toegangsmodifiers.

## Simulatiecode

- Simulatiedata zijn blittable structs in `NativeArray`/`NativeList` en worden verwerkt door Burst-jobs; geen managed allocaties in de hot path.
- Simulatielogica hangt niet af van `Time.deltaTime` maar van de vaste simulatiestap.
- Deterministische logica krijgt minstens één EditMode-test.

### Determinisme

Zelfde wereldseed en zelfde commands geven dezelfde stad, ook na opslaan en laden.

- Willekeur gaat via `Cities.Core.Randomness`, nooit via `UnityEngine.Random`, `System.Random`, `Guid` of de klok. `DeterminismSourceTests` controleert dat voor `Simulation` en `World` (MonoBehaviours uitgezonderd).
- Een systeem meldt zijn stroom eenmalig aan met `random.Register("Systeem.Doel")` en haalt elke tick een generator op met `tick.Random(stroom).At(tick.Tick)`. In parallelle jobs gebruik je `At(tick.Tick, index)` per object, zodat de volgorde van de werkthreads niet uitmaakt. Geef de `RandomSource` mee aan de job; bewaar geen generator over ticks heen.
- Itereer in simulatiecode niet over `Dictionary`/`HashSet` als de volgorde de uitkomst bepaalt; gebruik een gesorteerde of geïndexeerde lijst.
- Gehele getallen zijn op elk platform gelijk. Zwevendekommagetallen zijn gelijk binnen een build en platform; voor een uitkomst die tussen platforms gelijk moet zijn (zoals een gedeelde seed) gebruik je gehele getallen.
- De seed staat in de save (sectie `RAND`), komt anders uit `-seed <getal>` of de inspector van `SimulationHostBehaviour` en wordt bij de start gelogd. `StateChecksum` vergelijkt twee runs per sectie en wijst de eerste die uiteenloopt aan.

## UI

- Schermgebonden UI is UI Toolkit met het thema uit `Assets/UI` (zie besluit 0002). Gebruik de tokens (`var(--color-…)`, `var(--space-…)`) en componentklassen (`c-…`), en in code `UiClasses` en de `City…`-elementen.
- Eén `PanelSettings`: `Assets/UI/CityPanelSettings.asset`.
- Teksten in de UI zijn Nederlands.

## Invoer

- Alle acties staan in `Assets/Input/CityControls.inputactions`, per map (`Camera`, `Game`, ...). Lees acties via dat asset; geen `Keyboard.current` of hardgecodeerde toetsen in spelcode.
- Elke knopactie en elk deel van een composite heeft twee slots (primair en alternatief); een leeg slot krijgt pad `""`. Een nieuwe actie krijgt een Nederlandse naam in `ControlLabels`; een test controleert dat.
- `ControlBindingsBehaviour` laadt de toetsen van de speler uit `persistentDataPath/Settings/controls.json` en zet de maps aan. Herbinden gaat via `InputControlBindings`; een toets die al in gebruik is ruilt met de andere actie. Escape en de linkermuisknop zijn vast. Escape annuleert herbinden, breekt een tool af en opent anders het menu; links klikken plaatst.

## Tools

- Een bouw- of bewerkingstool implementeert `ITool` (`Cities.Gameplay.Tools`): elk frame maakt hij uit de muis (`ToolInput`) een `ToolPreview` met positie, draaiing, maat en eventueel de reden waarom het niet mag. De `ToolController` voert alleen een geldige preview uit.
- Escape breekt eerst een lopende stap af (`CancelStep`), en zet anders de tool uit. Pas zonder actieve tool opent Escape het menu.
- Snappen op het raster gaat via `GridSnap`; `PlacementTool` doet raster, kwartslagen, kaartrand en helling, en vraagt de rest (bezetting) aan een `IPlacementTarget`.
- `ToolGhostBehaviour` tekent de preview: het model doorschijnend en de footprint op de grond, blauw als het mag en rood als het niet mag. `ToolbarBehaviour` toont de reden. Testscene: `Scenes/Tests/Tools.unity`.
- Een tool met `RepeatsWhileHeld` (zoals een penseel) werkt elk frame zolang de knop ingedrukt is; `ToolInput.DeltaTime` geeft de frametijd.
- Terraformen gaat met `TerraformTool` (omhoog, omlaag, vlak, glad, naar hoogte) via `TerrainBrush`. Het penseel meldt de gewijzigde hoekpunten met `NotifyHeightChanged`, en de terreinshader tekent de rand via `_CityBrush`. Testscene: `Scenes/Tests/Terrain.unity`.

## Terrein

- De hoogte staat in `WorldMap.Height`: één waarde per hoekpunt van het celraster (8 m). Tegels en chunks delen hun randhoekpunten, dus elke tegel heeft zijn eigen heightmap van 257 × 257 hoekpunten zonder naad met de buren.
- Wie hoogtes wijzigt, meldt dat met `WorldMap.NotifyHeightChanged(rect)` (hoekpunten, max exclusief). `TerrainRendererBehaviour` bouwt dan alleen de geraakte chunks opnieuw; laden uit een save meldt de hele kaart.
- De terreinmesh bestaat uit chunks van 32 × 32 cellen (256 m) met vier detailniveaus (32, 16, 8 en 4 vlakjes per zijde). Randen tegen een grovere buur worden op diens lijn gelegd; een rok onder elke rand vangt kieren op zolang een buur nog niet herbouwd is.
- `TerrainShape` maakt het procedurele landschap (laagland, heuvels, bergrug, rivierdal) met vaste seed; `TerrainGeneratorBehaviour` vult er een nieuwe kaart mee. Testscene: `Scenes/Tests/Terrain.unity`.
- De terreinshader (`Cities/Terrain`) mengt vijf lagen: gras, zand, rots, akker en bebouwde grond. Per cel staat in de splatmap (`_CitySplatMap`) hoeveel zand, rots, akker en bebouwd er ligt; gras is de rest. `TerrainSplat` leidt dat af uit helling, hoogte, water en `WorldMap.GroundCover`. Wie de grondbedekking wijzigt, meldt dat met `WorldMap.NotifyGroundCoverChanged(rect)` (cellen).
- De terreintexturen zijn procedureel: pas `Scripts/Editor/Terrain/TerrainTextureBuilder.cs` aan en bouw opnieuw via **Cities > Terrein > Texturen bouwen**.
- Bomen staan als bosdichtheid per cel in `WorldMap.Trees` (0–255; alleen die kaart wordt opgeslagen). `TreePlacement` leidt daar de bomen deterministisch uit af. Wie de dichtheid wijzigt, meldt dat met `WorldMap.NotifyTreesChanged(rect)`. Plant of kap met `TreeBrush`; `TreeBrush.ClearForBuilding` bebouwt de grond en kapt de bomen, en `PrefabPlacementBehaviour` doet dat met een kaart erbij vanzelf. Op bebouwde grond staan nooit bomen.
- `TreeRendererBehaviour` tekent de bomen per regio van 1 km. In de verte zijn het impostors in één draw per regio en soort (`Cities/TreeImpostor`, via `RenderMeshPrimitives`). Dichtbij komt per boom het volle model of de eenvoudige kroon, met schaduw.

## Modellen

- Modellen zijn realistisch op ware grootte (meters, lokaal -Z is de straatkant, de oorsprong ligt midden op de kavel op maaiveld) en hebben geen colliders; picking gaat via de picklagen.
- Procedurele modellen worden gebouwd door editor-code onder `Scripts/Editor/Models` (menu **Cities > Modellen**). Pas de bouwcode aan en bouw opnieuw; bewerk de gegenereerde assets onder `Assets/Art` niet met de hand.
- Bestaande meshes worden bij opnieuw bouwen via de Mesh-API gevuld, niet met `EditorUtility.CopySerialized`: anders tekent de GPU tot een herstart de oude vorm. Foto's voor impostors maak je met synchrone shadercompilatie (`ShaderUtil.allowAsyncCompilation = false`).

## Opslaan

- Elk systeem met status levert een `ISaveSection` (id van vier hoofdletters, versie vanaf 1) en wordt toegevoegd in `SaveGameBehaviour`. Zie besluit 0003.
- `Read` verandert niets aan het spel: het leest, controleert en geeft een `Action` terug die de status toepast. Ongeldige inhoud geeft `reader.Corrupt(...)`.
- Lagen en grote arrays gaan met `WriteShuffled`/`ReadShuffled`; losse waarden met `Write`/`Read…`. Bij een wijziging van de inhoud gaat `Version` omhoog en blijven oude versies leesbaar.

## Wegennet

- `RoadNetwork` (Simulation/Network) bevat knooppunten, segmenten (kubische Bézier) en lanes met stabiele ID's; hoogstens acht armen per knooppunt en vijftien lanes per segment. Rechtsrijdend: lane-offset is positief rechts van de segmentrichting.
- Lanes en verbindingen worden afgeleid uit het profiel (`LaneProfiles`) en `LaneConnector`; alleen knooppunten, segmenten en lanes gaan in de save (sectie `NETW`), verbindingen worden bij het laden herbouwd.
- Bij een wijziging van het net: gebruik `AddSegment`/`RemoveSegment` zodat verbindingen en losse knooppunten meegaan; luister op `SegmentChanged`. De overlay `Lanegraaf` toont het geheel, de testscene is `Scenes/Tests/Network`.
- De `RoadTool` (Gameplay/Tools) legt wegen aan; de rekenkunde (snappen, bochten, controles) staat in `RoadPlanner` en is in CI getest. Het voorbeeld tekent de overlay `Aanleg`; `ToolPreview.Info` is de korte uitleg (lengte en koers) in de werkbalk.
- Het wegdek: `RoadMeshBuilder` (World/Roads) maakt per segment een mesh met een submesh per `RoadSurface` (asfalt, fietspad, stoep, stoeprand, strepen); `RoadRendererBehaviour` herbouwt gewijzigde segmenten binnen een tijdbudget (een segment kost ca. 1 ms). `RoadTerrainShaper` legt het terrein vlak en kapt bomen bij het aanleggen. Materialen en texturen: **Cities > Modellen > Wegen bouwen**.
- Kruispunten (knooppunt met 3 tot 8 armen): `RoadJunctionBuilder` rekent uit hoever elke weg van het midden ophoudt (afgeronde hoeken van 6 m) en maakt het mesh met stoep, stoeprand en zebrapaden; `RoadJunctions` koppelt dat aan het netwerk en `RoadRendererBehaviour` tekent het per knooppunt. De afsnijding volgt altijd uit het net, er wordt niets van opgeslagen.
- Wegtypes zijn definities (`road.<naam>`, `RoadTypeDefinition` onder `Assets/Data/RoadTypes`): rijstroken per richting, eenrichting, fietspad, stoep, snelheidslimiet en kosten per meter. `RoadTypeSpec.Profile()` maakt er het dwarsprofiel van; de `RoadTool` legt wegen aan met `Use(id, spec)` en de werkbalk toont een knop per type uit de catalogus.
- Wegen slopen doet de `DemolishTool` (Gameplay/Tools): een klik kiest een weg of kruispunt, een tweede klik bevestigt. De economie (E11) betaalt terug door te luisteren op `Demolished` (aantal segmenten en gesloopte lengte).
- Bouwfeedback: een `ToolPreview` heeft `InvalidReason` (rode ghost en de reden in de werkbalk), `Info` en `Cost` (hele munteenheden); de werkbalk toont de kosten achter de uitleg. Alleen de wegtool vult `Cost` al (lengte x kosten per meter); gebouwen volgen met de economie (E11).

## Water

- `Cities.World.Water.ShallowWater` is de pure rekenkern (virtuele pijpen, per rij); de Burst-jobs in `WaterSimulation` roepen dezelfde code aan. Pas de fysica alleen daar aan, zodat CI-tests haar dekken.
- De simulatie rekent op een grof raster van 32 m (vier cellen per waarde) met een vaste stap van 0,1 s; `WorldMap.WaterDepth` (8 m) is daarvan afgeleid en wordt om de vijf stappen ververst. De echte toestand (diepte en stromen) staat in sectie `WATR`.
- De zee is een vast waterpeil langs de randen in `SeaEdges`; bronnen en afvoerpunten (E03-06) zijn debieten in m³/s in `WaterSources` (negatief is afvoer), geplaatst met `WaterSourceTool` en opgeslagen in `WATR`.
- Het water wordt getekend door `WaterRendererBehaviour`: blokken van 32 × 32 cellen die om beurten uit `WorldMap.Height` en `WaterDepth` worden gebouwd (`WaterMeshBuilder`, per punt diepte en stroomrichting), met shader `Cities/Water` (dikte uit scènediepte, breking en absorptie, meestromende rimpels, schuim, Fresnel-reflectie; vereist depth en opaque texture in de URP-asset). Blokken zonder water bestaan niet.
- Budget: 0,6 ms per tick voor het watersysteem op de referentiemachine (nog te meten in Unity).

## Logging en profiling

- Elk systeem logt via een eigen `SystemLog` met de systeemnaam als categorie: `private readonly SystemLog _log = new SystemLog("Traffic");`. Gebruik geen `Debug.Log` in simulatiecode.
- Gebruik de generieke overloads (`_log.Debug("Route {0}", id)`), zodat een uitgeschakeld niveau niets opmaakt. In Burst-jobs wordt niet gelogd.
- Logniveaus stel je in met een `LogConfiguration`-asset op de host, of bij het opstarten met `-loglevel Traffic=Debug,*=Warning`.
- De scheduler geeft elk systeem automatisch een profiler-marker `Sim.<naam>`; `Sim.Tick` en `Sim.Commands` meten de tick en het werk op de main thread. Jobs met een eigen marker gebruiken het voorvoegsel `Sim.<systeem>.`.
- Elk systeem past binnen de budgetten uit `docs/PERFORMANCE.md` (in code: `PerformanceBudgets`). Een regressie van meer dan 10% op een budgetregel blokkeert het mergen.

## Definities (speldata)

- Gebouwen, voertuigen, beleid en kosten zijn definities: een `GameDefinition`-asset onder `Cities/Assets/Data/<Soort>`, te bewerken in de inspector.
- Het ID is stabiel en komt in opslagbestanden: `<soort>.<naam>` in kleine letters met koppeltekens, bijvoorbeeld `vehicle.city-bus`. Wijzig een ID niet meer na gebruik.
- Waarden staan in een serialiseerbare spec-struct (`VehicleSpec`, `BuildingSpec`, `PolicySpec`) met een `Validate`-methode. De asset-klasse is een dunne laag eromheen. Zo zijn regels en berekeningen ook in CI getest.
- `Assets/Data/DefinitionCatalog.asset` wordt automatisch bijgewerkt. **Cities > Definities > Catalogus bijwerken en valideren** meldt fouten.
- Geldbedragen zijn `long` in hele munteenheden.
- Overrides zonder editor gaan als JSON in `StreamingAssets/Config/Overrides/<id>.json` met dezelfde veldnamen als de asset, bijvoorbeeld `{ "_spec": { "MaxSpeedKmh": 70 } }`. Velden die niet genoemd worden, blijven staan; het ID is niet te wijzigen.

## Tests en dekking

- Logica staat waar mogelijk in gewone C#-klassen en structs. MonoBehaviours (`*Behaviour.cs`) en editor-code zijn dunne lijm die de logica aanroept.
- CI draait de EditMode-tests die geen Unity-runtime nodig hebben met `dotnet test` en meet de dekking. SonarCloud eist minstens 80% dekking op nieuwe code.
- Code die alleen met de Unity-runtime werkt (`NativeArray`, jobs, `ProfilerMarker`, MonoBehaviours, editor-code) telt niet mee voor de dekking. Zet zulke bronbestanden in `ci/sonar/coverage-exclusions.txt`, en hou de rekenregels daarbuiten (zoals `LayerLayout` naast `CellLayer`), zodat die wel gemeten worden.
- Tests die de Unity-runtime nodig hebben, krijgen `[Category("UnityRuntime")]`; CI slaat ze over en ze draaien in Unity. PlayMode-tests draaien altijd alleen in Unity.

### Tests draaien

| Opdracht | Wat | Duur |
| --- | --- | --- |
| `tools/test.ps1 -Suite Fast` | EditMode-tests zonder Unity-runtime met dotnet, gelijk aan CI | ± 1 min |
| `tools/test.ps1 -Suite EditMode` / `PlayMode` / `Unity` | Unity in batchmode | enkele minuten |
| `tools/test.ps1` | Alles | |

- `-Filter <naam>` beperkt tot een namespace of test, en `-Coverage` maakt bij Fast een dekkingsrapport. Resultaten en logs komen in `TestResults/`. Exitcode 0 betekent geslaagd, 1 mislukte tests, 2 een fout in de opzet.
- De Unity-suites kunnen niet draaien terwijl de editor het project open heeft. Gebruik dan het Test Runner-venster, of een tweede werkmap: `git worktree add ../CitiesGame-tests main` en daarna `tools/test.ps1 -Suite Unity -ProjectPath ../CitiesGame-tests/Cities`.
- **Bij elke commit**: CI draait de snelle tests bij elke push naar main en bij elke pull request, en zet de telling in de samenvatting van de run. Lokaal zet `tools/install-hooks.ps1` een pre-push-hook aan die de snelle tests eerst draait (overslaan: `git push --no-verify`). Unity-tests draaien niet in CI, want daarvoor is geen licentie in CI. Draai ze vóór elke pull request lokaal.

## Git

- Eén branch per backlogitem: `feature/<ID>-<korte-naam>`, bijvoorbeeld `feature/E01-03-simulation-clock`.
- Een PR verwijst naar het issue met `Closes #<nummer>` en wordt pas gemerged als de SonarQube-analyse geen relevante bevindingen meer heeft.
- Commitberichten in het Engels, in de gebiedende wijs.
