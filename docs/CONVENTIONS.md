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

## Tests en dekking

- Logica staat in gewone C#-klassen; MonoBehaviours (`*Behaviour.cs`) en editor-code zijn dunne lijm die de logica aanroept.
- CI draait alle EditMode-tests die geen Unity-runtime nodig hebben met `dotnet test` en meet de dekking. SonarCloud eist minstens 80% dekking op nieuwe code.
- `Cities/Assets/Scripts/Editor/**` en `*Behaviour.cs` tellen niet mee voor de dekking, omdat ze alleen in Unity te draaien zijn. Ze krijgen waar zinvol een PlayMode-test.
- Een EditMode-test die Unity-API's met native code aanroept (bijvoorbeeld `GameObject`, `Physics`), hoort in PlayMode of moet in CI met `Assert.Ignore` worden overgeslagen.

## Git

- Eén branch per backlogitem: `feature/<ID>-<korte-naam>`, bijvoorbeeld `feature/E01-03-simulation-clock`.
- Een PR verwijst naar het issue met `Closes #<nummer>` en wordt pas gemerged als de SonarQube-analyse geen relevante bevindingen meer heeft.
- Commitberichten in het Engels, in de gebiedende wijs.
