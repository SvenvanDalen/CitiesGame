# 0001 Simulatie-datamodel: eigen lichte ECS in plaats van Unity Entities

- **Status:** aangenomen
- **Datum:** 2026-10-29
- **Backlogitem:** E01-02 (issue #2)

## Vraag

Draaien de simulatie-agents (burgers, voertuigen, later ook gebouwen) op Unity Entities (DOTS), of op een eigen lichte ECS met `NativeArray`/`NativeList` en Burst-jobs? Het backlog raadde vooraf Entities aan voor agents; deze spike toetst dat met een meting op de eigen hardware.

## Benchmark

Beide varianten draaien hetzelfde scenario (code in [`docs/spikes/E01-02-ecs-benchmark`](../spikes/E01-02-ecs-benchmark)):

- Agents rijden over een rasterwegennet van 64×64 knooppunten (32 m uit elkaar) en kiezen bij elk knooppunt een willekeurige afslag, met een eigen deterministische RNG per agent.
- Vaste simulatiestap van 0,1 s (10 Hz). Per tick worden 500 willekeurige agents verwijderd en 500 nieuwe toegevoegd; dat bootst burgers na die verhuizen, sterven of geboren worden, en voertuigen die vertrekken of aankomen.
- 60 opwarmticks en daarna 600 gemeten ticks, gemeten met 50.000 en met 250.000 agents.
- De data per agent is gelijk: positie, doelknooppunt, snelheid en RNG-status.

| | Variant A: Unity Entities | Variant B: eigen lichte ECS |
| --- | --- | --- |
| Opslag | Archetype-chunks van 16 KB | Dichte `NativeList<Agent>` plus sparse tabel met stabiele ID's (index en generatie) |
| Bewegen | Burst-`IJobEntity`, `ScheduleParallel` | Burst-`IJobParallelFor`, batches van 256 |
| Verloop | `EntityCommandBuffer`, afgespeeld op de main thread | Swap-back verwijderen, vrije-lijst hergebruik van ID's |

**Omgeving:** Intel Core Ultra 7 255H (16 threads), 32 GB RAM, Windows 11, Unity 6000.5.10f1, Entities 6.5.0, Burst 1.8.30. Gemeten in een standalone release-build (Mono, geen development build); in de editor kosten extra veiligheidscontroles meer tijd. Drie runs; de eerste was een koude start met hogere waarden, onderstaande tabel is run 3 en run 2 ligt binnen 20% daarvan.

| Variant | Agents | Mediaan tick (ms) | p95 tick (ms) | Max tick (ms) | Mediaan verloop (ms) | Opbouw (ms) | Geheugen agents (MB) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Unity Entities 6.5 | 50.000 | 0,308 | 0,391 | 0,665 | 0,229 | 6,1 | 6,31 |
| Eigen lichte ECS | 50.000 | 0,080 | 0,100 | 0,248 | 0,035 | 3,1 | 2,50 |
| Unity Entities 6.5 | 250.000 | 0,769 | 1,409 | 1,949 | 0,567 | 24,9 | 30,56 |
| Eigen lichte ECS | 250.000 | 0,182 | 0,230 | 0,271 | 0,055 | 15,8 | 10,00 |

Wat opvalt:

- **Itereren is vrijwel even snel.** Zonder verloop kost bewegen bij 50.000 agents ongeveer 0,05 tot 0,08 ms in beide varianten; Burst en goede cache-layout doen in beide gevallen het werk.
- **Structurele wijzigingen bepalen het verschil.** Toevoegen en verwijderen kost bij Entities 6 tot 10 keer zoveel, en de variatie (p95, max) is groter. Een stad heeft voortdurend verloop: agents die een gebouw in- en uitgaan, voertuigen die spawnen.
- **Geheugen.** Entities gebruikt 2,5 tot 3 keer zoveel geheugen, omdat chunks na willekeurig verwijderen half leeg blijven.
- Beide varianten blijven ruim binnen een tickbudget van 100 ms. Performance alleen zou de keuze dus niet afdwingen; het verschil wordt pas relevant als tientallen systemen per tick lopen bij een miljoen inwoners (E23-10).

## Besluit

De simulatie gebruikt een **eigen lichte ECS**:

- Per soort (burgers, voertuigen, gebouwen, …) één dichte store van blittable structs in `NativeList`, met stabiele ID's met generatienummer (E01-05) en swap-back verwijderen.
- Systemen zijn Burst-jobs (`IJobParallelFor`, `IJobFor`, `IJob`) die in vaste volgorde worden gescheduled (E01-08).
- Structurele wijzigingen gaan via een eigen command-buffer die aan het eind van de tick in vaste volgorde wordt afgespeeld (E01-06), zodat de uitkomst deterministisch blijft.
- Packages: `com.unity.collections`, `com.unity.burst` en `com.unity.mathematics`. `com.unity.entities` wordt niet gebruikt.
- Rendering van agents en props gaat via GPU-instancing of de BatchRendererGroup (E23-06), niet via Entities Graphics.

## Overwegingen naast de meting

| Aspect | Entities | Eigen lichte ECS |
| --- | --- | --- |
| Opslaan en laden (E22-01) | Eigen serialisatie van worlds, moeilijk te versioneren | Dichte arrays direct weg te schrijven, eigen versieformaat |
| Determinisme (E01-11) | Volgorde in chunks hangt af van structurele wijzigingen | Volgorde volledig in eigen hand |
| CI en SonarQube | Code leunt op source generators (`IJobEntity`, `SystemAPI`) die alleen Unity uitvoert; de analyse zonder Unity werkt dan niet | Gewone C# met Burst-attributen; compileert in de bestaande analyse |
| Rendering | Entities Graphics kant-en-klaar | Eigen BRG-renderer nodig (E23-06) |
| Tooling | Entities Hierarchy, Systems-venster | Eigen debug-overlays (E24-02) |
| API-stabiliteit | API verandert tussen grote versies | Alleen Collections/Burst, stabiel |

## Gevolgen

- E01-04, E01-05, E01-06 en E01-08 bouwen de store, de ID's, de command-buffer en de scheduler zelf.
- E23-06 moet een eigen instanced renderer maken; dat werk zou bij Entities Graphics deels wegvallen.
- De CI-analyse moet de Collections-, Burst- en Mathematics-assemblies als referentie krijgen zodra code ze gebruikt.
- Wordt het verloop later toch vooral batchgewijs (bijvoorbeeld enableable components in plaats van structurele wijzigingen), dan kan deze afweging opnieuw. Met de benchmark in `docs/spikes` is dat te herhalen.
