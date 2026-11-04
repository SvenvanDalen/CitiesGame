# Audit hot paths (E23-03)

Doel: wat elke tick of elk frame draait, zet na opwarmen niets op de managed heap en is Burst-compatibel waar het zware rekenwerk zit.

## Gemeten (geen allocaties)

`Tests/EditMode/Scheduling/HotPathAllocationTests.cs` (UnityRuntime, draait lokaal in Unity) bewaakt:

| Pad | Status |
|-----|--------|
| `LaneTraffic.Step` | geen allocaties |
| `Congestion.Occupancy` | geen allocaties |
| `Idm.Acceleration` | geen allocaties |
| `DiffusionStep.Next` / `Neighbours` | geen allocaties (Burst-job `DiffusionField.Step` gebruikt ze) |
| `SimulationTiers.Visit` / `ShouldStep` / `TierAt` | geen allocaties |
| `SystemTimings.Record` (na eerste sample) | geen allocaties |
| `SpatialGrid` / `PointGrid` bouwen en queryen | geen allocaties (eigen tests) |

## Bekende allocaties (bewust nog niet opgelost)

| Pad | Reden | Aanpak |
|-----|-------|--------|
| `RoutePlanner.Find` | `Dictionary`, `HashSet`, `SortedSet` en het resultaatpad per route | `RouteCache` (E23-05) voorkomt de meeste aanroepen; Burst-A* volgt als routes per tick het budget raken |
| `TelemetryWriter.OnTick` | tekstregels (alleen elke N ticks) | acceptabel: buiten de tick-hot-path |
| `LanePathfinder.FindRoute` | lijst met lanes per route | valt weg bij hergebruik via de cache |

## Burst

Zware, regelmatige lussen (terrein, diffusie, ruimtelijke rasters) draaien al als Burst-jobs. Managed modellen met kleine data per agent (IDM, rechten van overgang) blijven managed: ze zijn goedkoop en hebben geen Burst nodig.

## Werkwijze

Een nieuw systeem op de tick-path krijgt een test in `HotPathAllocationTests`; een bekende uitzondering komt in de tabel hierboven met reden.
