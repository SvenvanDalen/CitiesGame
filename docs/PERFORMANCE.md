# Performancebudgetten

Doelen voor framerate, ticktijd, geheugen en opslaan per bevolkingsgrootte (E23-01). Elk systeem moet binnen deze budgetten passen; de benchmark-scenes (E23-02) meten ze bij elke mijlpaal opnieuw. De getallen staan ook in code (`Cities.Core.Diagnostics.PerformanceBudgets`), zodat metingen en debug-overlays (E24-02) ertegen kunnen toetsen. Wijzig ze altijd op beide plekken.

## Hardware

| | Minimale specificatie | Aanbevolen specificatie |
| --- | --- | --- |
| Processor | 6 kernen / 12 threads, bijvoorbeeld Ryzen 5 3600 of Core i5-10400 | 8 kernen of meer, bijvoorbeeld Ryzen 7 5800X, Core i7-12700 of Core Ultra 7 255H |
| Geheugen | 16 GB | 32 GB |
| Videokaart | 6 GB, bijvoorbeeld GTX 1660, RX 5600 XT of Arc A580 | 8 GB, bijvoorbeeld RTX 3070 of RX 6700 XT |
| Opslag | SSD | NVMe-SSD |
| Resolutie | 1920×1080, preset Gemiddeld | 2560×1440, preset Hoog |

De **referentiemachine** voor CPU-metingen is de ontwikkelmachine (Core Ultra 7 255H, 16 threads, 32 GB). Daarop zijn ook de ECS-benchmark (besluit 0001) en de save-meting (besluit 0003) gedaan. Zij valt onder de aanbevolen specificatie.

## Meetvoorwaarden

- Standalone release-build (geen development build, geen editor), IL2CPP zodra de build-pipeline er is; tot dan Mono.
- Snelheid 3× (30 ticks per seconde), camera op 600 m hoogte boven het drukste deel van de stad, 30 s opwarmen en daarna 60 s meten.
- **Framerate**: gemiddelde fps, en de 1% traagste frames mogen hooguit 20% onder het doel zitten.
- **Ticktijd**: mediaan en p95 van de wandtijd van een tick (alle jobs, kritiek pad), via de profiler-markers `Sim.Tick` en `Sim.<systeem>` (E01-10). De p95 mag hooguit 50% boven het budget zitten.
- **Geheugen**: `Total Reserved Memory` van het proces na 60 s, zonder videogeheugen.

## Budgetten per bevolking

| Inwoners (tot) | Fps aanbevolen | Fps minimaal | Tick (ms) | Simulatie op main thread (ms/frame) | Geheugen (MB) | Save (MB) | Opslaan (s) | Laden (s) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10.000 | 60 | 30 | 2 | 0,5 | 2.000 | 20 | 0,5 | 5 |
| 50.000 | 60 | 30 | 4 | 0,8 | 2.500 | 30 | 0,75 | 8 |
| 100.000 | 60 | 30 | 6 | 1,0 | 3.000 | 45 | 1,0 | 12 |
| 250.000 | 50 | 30 | 10 | 1,5 | 4.000 | 80 | 1,5 | 20 |
| 500.000 | 40 | 25 | 14 | 2,0 | 6.000 | 140 | 2,0 | 30 |
| 1.000.000 (stretchdoel, E23-10) | 30 | 20 | 16 | 3,0 | 9.000 | 250 | 3,0 | 45 |

Toelichting:

- **Tick**: op 3× komt er elke 33,3 ms een tick. Een tick mag hooguit de helft daarvan kosten, zodat de jobs de rest van het frame (rendering, UI) niet verdringen en er ruimte is voor pieken. Haalt een stad het budget niet, dan laat de klok ticks vallen (`MaxTicksPerUpdate`) en loopt 3× in de praktijk trager. Dat is een bug, geen instelling.
- **Simulatie op main thread**: alleen plannen van jobs, sync points en commands. Het rekenwerk zelf draait in Burst-jobs op de worker threads.
- **Opslaan**: de tijd tot de speler verder kan. Het kopiëren van de status gebeurt tussen twee ticks; comprimeren en schrijven daarna op de achtergrond (E22-03). Het bestand blijft onder de genoemde grootte.
- **Laden**: van het kiezen van de save tot de stad speelbaar is, inclusief het opbouwen van rendering-data.

## Allocaties

- **Simulatietick**: 0 bytes GC-allocatie in de hot path (E23-03). Tests met `Is.Not.AllocatingGCMemory()` bewaken dat per systeem.
- **Frame**: gemiddeld hooguit 1 KB GC-allocatie per frame tijdens het spelen. Het openen van een paneel of het laden van een stad telt niet mee.

## Verdeling binnen een frame (100.000 inwoners, 60 fps, 16,7 ms)

| Onderdeel | Main thread (ms) | Opmerking |
| --- | --- | --- |
| Simulatie | 1,0 | Plannen en commands; de tick zelf draait in jobs |
| Gameplay en tools | 1,0 | Camera, invoer, previews van bouwtools |
| UI | 1,5 | UI Toolkit, alleen zichtbare panelen bijwerken |
| Rendering (CPU) | 6,0 | Culling, instancing (E23-06), schaduwen |
| Audio en overig | 0,5 | |
| Reserve | 6,7 | Voor pieken en zwakkere processors |

De GPU mag op de aanbevolen specificatie hooguit 14 ms per frame kosten op 1440p Hoog. Op de minimale specificatie is dat 30 ms op 1080p Gemiddeld. Videogeheugen: hooguit 4 GB op de minimale en 6 GB op de aanbevolen specificatie.

## Verdeling van de tick (100.000 inwoners, 6 ms)

| Systeem | Budget (ms) |
| --- | --- |
| Burgers en levensloop (E07) | 1,2 |
| Pathfinding (E08) | 1,5 |
| Verkeer en voertuigen (E08) | 1,5 |
| Economie en diensten (E09–E11) | 0,8 |
| Nutsvoorzieningen (water, stroom; E06) | 0,5 |
| Overig (vervuiling, grondwaarde, rampen) | 0,5 |

Bij grotere steden groeien deze aandelen naar verhouding. Pathfinding en verkeer schalen het slechtst en krijgen bij 500.000 inwoners samen hooguit 60% van de tick.

## Geheugen (100.000 inwoners, 3.000 MB)

| Onderdeel | MB |
| --- | --- |
| Engine, shaders, modellen en texturen (CPU-kant) | 1.400 |
| Wereldkaart (alle lagen, besluit 0003) | 55 |
| Wegennet en gebouwen | 300 |
| Burgers en voertuigen | 400 |
| Pathfinding-caches | 200 |
| Instance-data voor rendering | 300 |
| UI, audio en overig | 200 |
| Reserve | 145 |

## Overige doelen

| Moment | Budget |
| --- | --- |
| Opstarten tot hoofdmenu | ≤ 15 s (SSD) |
| Nieuwe stad tot speelbaar | ≤ 10 s |
| Bouwtool-preview na muisbeweging | Binnen hetzelfde frame |
| Picking (wat ligt onder de muis) | ≤ 0,5 ms per frame in een release-build, deel van "Gameplay en tools"; in de editor ≤ 1 ms (Mono en veiligheidscontroles) |
| Reactie op een toets of klik | ≤ 2 frames |

## Huidige stand

| Meting | Resultaat | Budget | Status |
| --- | --- | --- | --- |
| Agents bewegen, 250.000 (besluit 0001, release-build) | 0,18 ms per tick | Deel van 10 ms | Ruim binnen |
| Volle kaart opslaan (besluit 0003, editor) | 2,8 s, op de main thread | 0,5 s tot verder spelen | **Te traag**: E22-03 verplaatst comprimeren en schrijven naar de achtergrond |
| Volle kaart laden (besluit 0003, editor) | 0,8 s | 5 s | Binnen |
| Picking in een stad met 100.000 gebouwen, 60.000 wegstukken, 50.000 voertuigen en 100.000 burgers (E02-04, editor) | 0,48 ms per pick | 1 ms in de editor | Binnen; `SpatialPickLayerTests` bewaakt het |
| Volle kaart save-grootte (heuvels, besluit 0003) | 11,2 MB | 20 MB | Binnen |

## Werkwijze

- Elk nieuw systeem krijgt een eigen profiler-marker (`Sim.<naam>`) en wordt gemeten in de benchmark-scenes van 10.000, 100.000 en 500.000 inwoners (E23-02).
- Een regressie van meer dan 10% op een budgetregel is een bug en blokkeert het mergen.
- Bij elke mijlpaal wordt de tabel *Huidige stand* bijgewerkt.
