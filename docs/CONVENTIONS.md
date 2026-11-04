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

## Screenshot-modus

- F11 zet de screenshot-modus aan: `ScreenshotBehaviour` verbergt alle `UIDocument`s en de bouwghost, zet de tools uit en geeft `CityCameraBehaviour.FreeLook` ruimere grenzen (`ScreenshotMode.FreeLook`: kantelen van 1° tot 89,9°, vanaf 3 m afstand). F12 maakt een opname op minstens `_targetWidth` pixels (`ScreenshotMode.SuperSize`, hoogstens 16384 per zijde) in `persistentDataPath/Screenshots`. Een nieuw schermvullend UI-element hoeft niets te doen zolang het in een `UIDocument` zit. Testscene: `Scenes/Tests/Camera.unity`.

## Tools

- Een bouw- of bewerkingstool implementeert `ITool` (`Cities.Gameplay.Tools`): elk frame maakt hij uit de muis (`ToolInput`) een `ToolPreview` met positie, draaiing, maat en eventueel de reden waarom het niet mag. De `ToolController` voert alleen een geldige preview uit.
- Escape breekt eerst een lopende stap af (`CancelStep`), en zet anders de tool uit. Pas zonder actieve tool opent Escape het menu.
- Snappen op het raster gaat via `GridSnap`; `PlacementTool` doet raster, kwartslagen, kaartrand en helling, en vraagt de rest (bezetting) aan een `IPlacementTarget`.
- `ToolGhostBehaviour` tekent de preview: het model doorschijnend en de footprint op de grond, blauw als het mag en rood als het niet mag. `ToolbarBehaviour` toont de reden. Testscene: `Scenes/Tests/Tools.unity`.
- Een tool met `RepeatsWhileHeld` (zoals een penseel) werkt elk frame zolang de knop ingedrukt is; `ToolInput.DeltaTime` geeft de frametijd.
- Terraformen gaat met `TerraformTool` (omhoog, omlaag, vlak, glad, naar hoogte) via `TerrainBrush`. Het penseel meldt de gewijzigde hoekpunten met `NotifyHeightChanged`, en de terreinshader tekent de rand via `_CityBrush`. Testscene: `Scenes/Tests/Terrain.unity`.

## Camera

- `CameraBookmarks` bewaart vijf standen (`CameraRig`): F5 tot en met F9 roepen er een terug (gedempt), met Ctrl erbij slaan ze de huidige stand op. De toetsen staan in de map `Camera` en zijn herbindbaar. `CameraBookmarksSection` (`CAMB`) neemt ze mee in de save; hang `CityCameraBehaviour` aan `SaveGameBehaviour._camera`.

## Volgcamera

- Burgers en voertuigen worden gevolgd via `IFollowTarget` (geldigheid, positie, richting, ooghoogte). `FollowCameraController` rekent de stand uit (`Chase` achter het doel, `FirstPerson` op ooghoogte, rondkijken met de rechtermuisknop) en `CityCameraBehaviour.Follow/StopFollowing` zet hem aan. Valt het doel weg, dan gaat de vrije camera verder vanaf de laatste stand. Een toets om te wisselen komt zodra burgers/voertuigen een selectie hebben (E02-04 picking).

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
- Snelwegknooppunten (E04-11): `InterchangeGeometry` (Simulation/Network) plant drie presets (`Diamond` = op- en afritten, `Cloverleaf`, `Turbine`) als knooppunten en Bezier-wegen in lokale coördinaten (snelweg langs X, kruisende weg langs Z over een brug van 5,5 m, helling max. 6%); `Interchange.Build` zet dat in het net en de `InterchangeTool` plaatst het met één klik (draaien in stappen van 15°). Er is geen eigen opslag: alles zit in `NETW`. De snelweg is één tweerichtingssegment, dus de ramps vertrekken vanaf de hartlijn (gescheiden rijbanen volgen later); de turbine is vereenvoudigd.
- Verkeersregels (E04-13): `TrafficRules` bewaart per arm (segment bij een knooppunt) alleen wat de speler afwijkend instelt: een eigen voorrangsregel (`RightOfWay`) of een verbod in te rijden; sectie `TRFC`. `RightOfWayResolver.Resolve` geeft de geldende regel (instelling, anders rotonde, anders de drukkere weg heeft voorrang, anders rechts voor links). `LanePathfinder` mijdt een arm met verbod in te rijden als hij de regels meekrijgt. De snelheidslimiet per segment zet je met `RoadNetwork.SetSpeedLimit`.
- Bouwfeedback: een `ToolPreview` heeft `InvalidReason` (rode ghost en de reden in de werkbalk), `Info` en `Cost` (hele munteenheden); de werkbalk toont de kosten achter de uitleg. Alleen de wegtool vult `Cost` al (lengte x kosten per meter); gebouwen volgen met de economie (E11).

## Water

- `Cities.World.Water.ShallowWater` is de pure rekenkern (virtuele pijpen, per rij); de Burst-jobs in `WaterSimulation` roepen dezelfde code aan. Pas de fysica alleen daar aan, zodat CI-tests haar dekken.
- De simulatie rekent op een grof raster van 32 m (vier cellen per waarde) met een vaste stap van 0,1 s; `WorldMap.WaterDepth` (8 m) is daarvan afgeleid en wordt om de vijf stappen ververst. De echte toestand (diepte en stromen) staat in sectie `WATR`.
- De zee is een vast waterpeil langs de randen in `SeaEdges`; bronnen en afvoerpunten (E03-06) zijn debieten in m³/s in `WaterSources` (negatief is afvoer), geplaatst met `WaterSourceTool` en opgeslagen in `WATR`.
- Het water wordt getekend door `WaterRendererBehaviour`: blokken van 32 × 32 cellen die om beurten uit `WorldMap.Height` en `WaterDepth` worden gebouwd (`WaterMeshBuilder`, per punt diepte en stroomrichting), met shader `Cities/Water` (dikte uit scènediepte, breking en absorptie, meestromende rimpels, schuim, Fresnel-reflectie; vereist depth en opaque texture in de URP-asset). Blokken zonder water bestaan niet.
- Budget: 0,6 ms per tick voor het watersysteem op de referentiemachine (nog te meten in Unity).

## Fauna

- Vogels, vissen en herten zijn sfeerleven, geen simulatie: ze hebben geen toestand, staan niet in de save en kosten geen simulatietijd. `FaunaGroups.Spawn` legt per vak van 256 m uit wereldseed en vaknummer vast welke groepen er wonen (meeuwen boven water, kraaien boven land, scholen in water van 2,5 m of meer, kuddes reeën op open land); `FaunaMotion.TryPose` rekent de stand van een dier uit de tijd. Een dier dat op een plek komt waar het niet kan (vis op het droge, ree in het water of in het bos) is er dan niet; terrein of water aanpassen werkt dus meteen door.
- De kaart komt via `IFaunaHabitat` (`WorldMapHabitat` leest de lagen), zodat de regels in CI getest zijn. Willekeur gaat via `SeedHash`.
- `FaunaRendererBehaviour` rekent alleen de groepen rond de camera (vogels 1,8 km, herten 600 m, vissen 120 m) en tekent ze met GPU-instancing; modellen staan als gewone geometrie in `FaunaShapes`. Testscene: `Scenes/Tests/Fauna.unity`.

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

## Wind

- `WindModel.At(seed, tick)` (Simulation/Fields) geeft de wind als `Wind` (richting in radialen, snelheid in m/s, `Strength` 0 tot 1). De wind is een zuivere functie van wereldseed en tick: er is geen status en dus geen eigen save-sectie; de seed zit al in `RAND`. Hij wisselt vloeiend om de `KnotTicks` rond een heersende richting per wereld.
- `Wind.Vector` (lengte = kracht) is de invoer van `FireSpread.Step`; `Wind.CellsPerStep(max)` is de invoer voor `DiffusionSettings` van de milieulagen.

## Zone-specialisaties

- Een gebouw met `BuildingSpec.Specialization` (bijvoorbeeld `eco`, `it-cluster`, `tourist-shopping`) groeit alleen waar een beleidsmaatregel die inschakelt via het effect `zone.specialization.<naam>` met factor boven 1 (`ZoneSpecialization`). Die factor is ook het gewicht tegenover gewone gebouwen. Een nieuwe specialisatie is dus alleen data: een gebouwdefinitie en een beleidseffect.
- `BuildingSpawner.Policies` en `DistrictOf` koppelen het `PolicyBook` en de districtindeling; zonder beleid groeien alleen gewone gebouwen. De status zit in sectie `PLCY`, er is geen eigen sectie nodig.

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

## Gezondheid en sterfte

- `CitizenHealthModel` (Simulation/Citizens) is zuiver: de gezondheidsscore van een burger beweegt per jaar naar een doel dat daalt door lucht-, water- en bodemvervuiling en hoge leeftijd en stijgt door zorg binnen bereik (`HealthEnvironment`). Zonder zorg of met overvolle bedden (`Beds`, `Patients`, `BedLoad`) loopt de sterftekans op (`CareFactor`, maximaal 2x). `Yearly` voegt dat samen met `CitizenLife.AgeOneYear`. Er is geen eigen status; `Citizen.Health` zit al in de burgers-opslag.

## Overige inkomsten

- `Tourism` (Simulation/Economy) houdt trekpleisters bij (`Attraction`: toeristen per dag, entree, onderhoud; een unieke kan maar één keer staan). `Settle` boekt bestedingen en entree als `Other`-inkomst en het onderhoud als `Upkeep`; hotelbedden begrenzen de toeristen per week.
- `Trade` heeft per goed een import- en exportprijs. `Settle` krijgt per goed het overschot of tekort (van de aanroeper, tot de goederenketen E11-08 er is): export is inkomst, import een verplichte uitgave, beide onder `Trade`. Beide staan in sectie `INCM` (`OtherIncomeSection`).

## Vervoerskeuze

- `TravelModeChoice` (Simulation/Citizens, E08-09) kiest per rit lopen, fiets, auto of OV met de laagste gegeneraliseerde kosten (reistijd x tijdswaarde + geld, plus vaste tijd voor fiets en auto). De OV-tijd komt uit `TransitRouter`; fiets en auto vragen bezit uit `TravelOwnership` (afgeleid van huishouden, welvaart en seed, dus zonder eigen status of save-sectie). De smaakfactor komt uit `DeterministicRandom`; geef per burger en tick een verse generator mee.

## Parkeren

- `ParkingRegistry` (Simulation/Vehicles) bewaart de parkeerlots van de stad: straatplekken per segment (`ParkingSlots`), gebouwparkeren en garages (`ParkingKind`). Een lot heeft capaciteit en bezetting; een gesloopt gebouw of segment sluit zijn lots met `Close` zodat ID's stabiel blijven. Sectie `PARK`, via `RoadNetworkHostBehaviour.Parking`.
- Een voertuig zoekt met `ParkingSearch.TryPark`: het dichtstbijzijnde lot met een vrije plek binnen de straal rond de bestemming. Is alles vol, dan telt het een rondje en wordt de straal groter; na `MaxRounds` rondjes geeft het op. De voertuig-agent rijdt in de tussentijd rond en roept `Leave` aan als hij wegrijdt.

## Geanimeerde voertuigen en burgers

- Animatie loopt op de GPU, zonder skinning (E21-05). `WheelSpin` (Simulation/Animation) rekent de wielhoek uit afstand en wielstraal; `WalkCycle` laat de looppas oplopen met de afgelegde afstand gedeeld door de staplengte (`StrideMeters`), zodat voeten niet glijden. `AnimationPhase.Offset(id)` geeft elke burger een eigen, deterministische startfase.
- `AgentAnimationTracker` houdt per agent-id de wielhoek of looppas bij uit de verplaatsing sinds de vorige verversing (`Begin`, `Update` per zichtbare agent, `End` vergeet verdwenen agents). De stand is afgeleid en zit niet in de save.
- Voertuigen: `ModelMesh.TagWheel` markeert wielhoekpunten in UV2 (1, scharnier-y, scharnier-z); shader `Cities/CityAgent` draait ze om de X-as. Burgers: `CitizenBuilder` bakt één looppascyclus naar een textuur (`BakedClipLayout`: breedte = hoekpunten, hoogte = frames, RGBAHalf met de verplaatsing t.o.v. de rustpose); de shader mengt twee frames (`ClipSampler.Loop`) met keyword `_AGENT_CLIP`.
- `AgentRendererBehaviour` (Presentation/Agents) tekent één model met één `RenderMeshPrimitives` per submesh uit twee structured buffers (`_AgentPose`: x, y, z, draaiing; `_AgentAnim`: animatiestand, schaal). Aanroepen: `BeginFrame`, `Submit(id, positie, draaiing)` voor elke agent die `VehicleLod` (Full) of `AgentLod` kiest, `EndFrame`. Menu `Cities > Modellen > Voertuigen bouwen` en `Burgers bouwen` maken de renderer-prefabs in `Assets/Art/Agents`.

## Overlays

- Een overlay is een `IOverlaySource` (`Cities.Gameplay.Overlays`) met een `OverlayLegend` (titel, eenheid, bereik, `OverlayColorScale`) en een `Version` die oploopt als de gegevens veranderen. Een celoverlay (`ICellOverlaySource`) geeft één waarde per cel, een gebouw- of segmentoverlay (`IEntityOverlaySource`) vult een lijst `OverlayEntityValue` (lint van `From` naar `To`, gebouw = vierkant). `float.NaN` betekent geen gegevens en wordt niet getekend.
- Een nieuw systeem registreert zijn bron met `OverlayBehaviour.Controller.Register(...)`; `WorldLayerOverlays` doet dat voor hoogte, landwaarde, grondvervuiling en waterdiepte. Kleurverlopen: `Hot` (hoog is slecht), `Good` (hoog is goed), `Terrain`.
- `CellOverlayBaker` bakt een celoverlay in stukken (budget in cellen per stap; `OverlayBehaviour` geeft `_bakeBudgetMs` per frame) in een achterbuffer en wisselt pas als alles klaar is, zodat wisselen of verversen niet haperen. De terreinshader leest de textuur als `_CityOverlay` (`_CityOverlayParams.x` = dekking). Gebouw- en segmentoverlays worden een lint-mesh (`OverlayRibbonBuilder`). `OverlayPanel` toont de knoppen en de legenda.

## Districten

- `DistrictBook` (`Cities.World.Districts`) bewaart de districten: nummer 1 tot 255, naam (uniek, hoogstens 40 tekens) en kleur (`0xRRGGBB`). Nummer 0 is geen district, dus de hele stad; dezelfde nummering gebruikt `PolicyBook`. Een nieuw district krijgt het laagste vrije nummer.
- `DistrictMap` legt per blok van 4×4 cellen (32 m) vast bij welk district het hoort en houdt per district het aantal blokken bij (`BlockCount`, `Area`). `Paint` schildert een cirkel (0 wist), `RemoveDistrict` haalt een district uit de lijst en van de kaart, `AtPosition` zoekt het district op een wereldpositie. `Revision` loopt op bij elke wijziging.
- `DistrictSection` (`DIST`) neemt lijst en kaart mee in de save; `DistrictTool` (`IBrushTool`) is het penseel om te schilderen of te wissen.

## OV-prioriteit

- Een busbaan is een lane van type `LaneTypes.Bus` (zonder `Car`): `LaneProfiles.Road(..., busLanes: true)` maakt de buitenste rijstrook per richting een busbaan (minstens twee rijstroken per richting; bij een eenrichtingsweg met één strook een busstraat). `TransitPriority.MayUse` en `IsBusLane` beantwoorden wie erop mag.
- `TransitPriority` (Simulation/Transit, E09-11) bewaart welke lijnen en welke kruispunten OV-voorrang hebben (sectie `TPRI`, `TransitPrioritySection`). `Mark` zet `Approach.TransitPriority` op een aanrijding: `IntersectionRules` laat zo'n voertuig doorrijden tenzij de rotonde of een ander lijnvoertuig voorgaat; de reservering blijft de botsingsgarantie.
- `SignalPlan.TryTransitPriority` verlengt een bijna aflopend groen (hoogstens `TransitExtension`) of kapt het groen van de lopende fase in (niet onder `MinGreen`) voor de volgende groep; hoogstens één ingreep per ronde. `TransitPriority.RequestSignal` telt toegekende en geweigerde aanvragen.

## Metro

- `Metro` (Simulation/Transit, E09-06) bevat de rekenregels van de metro: perrondiepte `MinDepth` tot `MaxDepth` m onder het maaiveld, stationsafstand, maximale tunnelhelling (`MaxGrade`, 4%), `AccessTime` (lopen plus roltrap naar de diepte) en `TravelTime` (optrekken, topsnelheid, remmen). Net als `Tram` rijdt de metro nog niet met eigen snelheid in `TransitFleet`.
- `MetroNetwork` legt een perrondiepte op haltes van het `TransitNetwork`. Maak stations en metrolijnen met `AddStation` en `AddLine` zodat de regels gelden; een metrolijn is een gewone `TransitLine`. Sectie `METR` (`MetroSection`) bewaart alleen de dieptes; haltes en lijnen staan in `TRNS`. Verwijder stations met `TryRemoveStation`, niet met `TransitNetwork.TryRemoveStop`, anders blijft de diepte achter.
- `UndergroundView` (Gameplay/Overlays) is de schakelaar voor de ondergrondse weergave: soepele terreindekking (`GroundOpacity`), zichtbaarheid per diepte (`IsVisible`) en een diepte-tint. `UndergroundViewBehaviour` zet de dekking als shaderglobal `_CityGroundOpacity`.

## Trein

- `Train` (Simulation/Transit, E09-07) bevat de rekenregels van de trein: snelheid, optrekken, lengte en capaciteit per `TrainKind` (passagier of goederen), minimale spoor- en perronlengte, stationsafstand (`MinStationSpacing`, 1500 m), `TravelTime` en de sluitregel van een overweg (`ShouldClose`: dicht `CrossingWarningTime` voor aankomst tot de staart voorbij is). Net als `Tram` en `Metro` rijdt de trein nog niet met eigen snelheid in `TransitFleet`.
- `RailNetwork` is het spoornet: knopen, sporen (`AddTrack`), stations (`AddStation`), wissels, overwegen en treinen. Een knoop met drie sporen is een wissel: de stam is het spoor met het laagste ID, de rest zijn takken (`TrySetSwitch`, `NextTrack`); van tak naar tak kan niet en een verkeerd gelegde wissel blokkeert. `FindPath` houdt daar rekening mee. Een passagiersstation is ook een halte van het `TransitNetwork`; maak lijnen met `RailNetwork.AddLine` en verwijder stations met `TryRemoveStation`.
- `AddTrain` legt de wissels voor de route goed en weigert te lange treinen voor het perron; `Advance` laat treinen rijden (keren aan het eind) en werkt de overwegen bij (`IsClosed` wordt afgeleid, niet bewaard). Er is nog geen seinstelsel: twee treinen op één spoor botsen niet.
- Sectie `RAIL` (`RailSection`) bewaart knopen, sporen, stations, wissels, overwegen en treinen; haltes en lijnen staan in `TRNS`.

## Waterverkeer

- `WaterTraffic` (Simulation/Transit, E09-08) bevat de rekenregels van veerboot (`VesselKind.Ferry`) en vrachtschip (`VesselKind.CargoShip`): diepgang plus `KeelClearance`, breedte, snelheid, capaciteit, tijd aan de kade (`DwellTime`, `HandlingTime` met kranen), kanaalafmetingen (`CanalAdmits`: diep genoeg en drie keer de scheepsbreedte) en `DailyCapacity`. De schepen rijden nog niet als voertuig in `TransitFleet`.
- `WaterNetwork` legt havens (halte plus soort en kadediepte) en kanalen op het `TransitNetwork` en leest de waterdiepte via `IWaterDepthSource` (de wereld levert `WorldMap.WaterDepth`, tests een functie). Maak havens, kanalen en lijnen met `AddPort`, `AddCanal` en `AddLine`; een lijn heeft havens van één soort en elk stuk moet bevaarbaar zijn (kanaal of open water, bemonsterd elke `SampleStep` m). `RoundTripTime` en `DailyCapacity` geven de rondrit en capaciteit. Verwijder havens met `TryRemovePort`.
- Sectie `WATR` (`WaterSection`) bewaart havens en kanalen; haltes en lijnen staan in `TRNS`.

## Handelsvoorkeuren

- `TradePreferences` (Simulation/Economy, E14-09) laat per goed de prijs- en vraagfactor van buiten wekelijks schuiven met seedgebonden ruis (`DeterministicRandom` uit seed, goed en week) en terugtrekking naar 1. Een gesloten of verdwenen verbinding (via `ITradeConnections`, de abstractie voor E14-04/E14-05) is een tegenvaller voor de goederen van die `ConnectionKind`: hogere importprijs, lagere exportprijs en minder vraag, wegebbend per week. `Apply` zet de prijzen in `Trade`, `Tradeable` begrenst het te exporteren overschot. Status in sectie `TRDP`.

## Evenementen

- `EventPlanner` (Simulation/Events, E10-12) beheert locaties (`EventVenue`: stadion, concertzaal, markt, met capaciteit en kaartprijs) en geplande evenementen (`PlannedEvent`: datum uit `GameDate`, beginuur, aantrekkingskracht 1-100; één per locatie per dag). `Forecast` splitst het publiek in inwoners (promille van de bevolking via `Func<int>`) en gasten van buiten (aandeel van de capaciteit, begrensd door de `IOutsideConnections` van het bezoekersmodel, per dag gedeeld). `Settle(vandaag, week, ledger)` houdt de evenementen die aan de beurt zijn en boekt kaartjes (Overig), belasting op bestedingen (Belastingen) en organisatiekosten (Diensten); sectie `EVNT`.
- Voor het verkeer (E08-17): `IEventDemandSource.DemandOn(datum)` geeft per evenement `EventTrafficDemand` (beginuur, inwoners, gasten van buiten). De planner moet nog aan een dagtik en `SaveGameBehaviour` gehangen worden.
