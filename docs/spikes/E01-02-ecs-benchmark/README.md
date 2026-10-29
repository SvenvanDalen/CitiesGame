# Spike E01-02: benchmark Entities tegenover eigen lichte ECS

Code achter besluit [0001](../../decisions/0001-simulatie-datamodel.md). De bestanden staan bewust buiten `Cities/Assets`, zodat het project zonder `com.unity.entities` compileert.

## Opnieuw draaien

1. Voeg `"com.unity.entities": "6.5.0"` toe aan `Cities/Packages/manifest.json`.
2. Kopieer deze map naar `Cities/Assets/Spikes/EcsBenchmark` (zonder deze README).
3. Maak een lege scene met één GameObject met `BenchmarkRunner` en bouw een standalone release-build met alleen die scene.
4. Start de build met `EcsBenchmark.exe -batchmode -nographics`. Het resultaat komt als Markdown-tabel in `ecs-benchmark.md` naast de executable.
5. Draai minstens drie keer en negeer de eerste run (koude start).

Snel in de editor kan ook: roep `CustomStoreBenchmark.Run(50_000)` en `EntitiesBenchmark.Run(50_000)` aan. Die cijfers zijn hoger door de veiligheidscontroles in de editor.

## Bestanden

- `BenchmarkScenario.cs`: gedeeld scenario (rasterwegennet, stapfunctie, parameters).
- `CustomStoreBenchmark.cs`: variant B, dichte `NativeList` met stabiele ID's en `IJobParallelFor`.
- `EntitiesBenchmark.cs`: variant A, Entities met `IJobEntity` en `EntityCommandBuffer`.
- `BenchmarkStatistics.cs`: mediaan, p95 en Markdown-uitvoer.
- `BenchmarkRunner.cs`: draait alles in een build en schrijft het rapport.
