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
- Willekeur gaat via de deterministische RNG (E01-11), nooit via `UnityEngine.Random`.
- Deterministische logica krijgt minstens één EditMode-test.

## UI

- Schermgebonden UI is UI Toolkit met het thema uit `Assets/UI` (zie besluit 0002). Gebruik de tokens (`var(--color-…)`, `var(--space-…)`) en componentklassen (`c-…`), en in code `UiClasses` en de `City…`-elementen.
- Eén `PanelSettings`: `Assets/UI/CityPanelSettings.asset`.
- Teksten in de UI zijn Nederlands.

## Invoer

- Alle acties staan in `Assets/Input/CityControls.inputactions`, per map (`Camera`, `Game`, ...). Lees acties via dat asset; geen `Keyboard.current` of hardgecodeerde toetsen in spelcode.
- Elke knopactie en elk deel van een composite heeft twee slots (primair en alternatief); een leeg slot krijgt pad `""`. Een nieuwe actie krijgt een Nederlandse naam in `ControlLabels`; een test controleert dat.
- `ControlBindingsBehaviour` laadt de toetsen van de speler uit `persistentDataPath/Settings/controls.json` en zet de maps aan. Herbinden gaat via `InputControlBindings`; een toets die al in gebruik is ruilt met de andere actie. Escape is vast: het annuleert herbinden en opent het menu.

## Opslaan

- Elk systeem met status levert een `ISaveSection` (id van vier hoofdletters, versie vanaf 1) en wordt toegevoegd in `SaveGameBehaviour`. Zie besluit 0003.
- `Read` verandert niets aan het spel: het leest, controleert en geeft een `Action` terug die de status toepast. Ongeldige inhoud geeft `reader.Corrupt(...)`.
- Lagen en grote arrays gaan met `WriteShuffled`/`ReadShuffled`; losse waarden met `Write`/`Read…`. Bij een wijziging van de inhoud gaat `Version` omhoog en blijven oude versies leesbaar.

## Logging en profiling

- Elk systeem logt via een eigen `SystemLog` met de systeemnaam als categorie: `private readonly SystemLog _log = new SystemLog("Traffic");`. Gebruik geen `Debug.Log` in simulatiecode.
- Gebruik de generieke overloads (`_log.Debug("Route {0}", id)`), zodat een uitgeschakeld niveau niets opmaakt. In Burst-jobs wordt niet gelogd.
- Logniveaus stel je in met een `LogConfiguration`-asset op de host, of bij het opstarten met `-loglevel Traffic=Debug,*=Warning`.
- De scheduler geeft elk systeem automatisch een profiler-marker `Sim.<naam>`; `Sim.Tick` en `Sim.Commands` meten de tick en het werk op de main thread. Jobs met een eigen marker gebruiken het voorvoegsel `Sim.<systeem>.`.

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

## Git

- Eén branch per backlogitem: `feature/<ID>-<korte-naam>`, bijvoorbeeld `feature/E01-03-simulation-clock`.
- Een PR verwijst naar het issue met `Closes #<nummer>` en wordt pas gemerged als de SonarQube-analyse geen relevante bevindingen meer heeft.
- Commitberichten in het Engels, in de gebiedende wijs.
