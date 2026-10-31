# Asset-richtlijnen (E21-01)

Voor elk model dat in het spel komt, of het nu procedureel (Editor-builder) of uit een DCC-pakket komt. Stijl en materialen: zie `ART-DIRECTION.md`.

## Schaal en assen

- 1 Unity-eenheid is 1 meter. Een wereldcel is 8 m × 8 m (`WorldDimensions.Default`).
- Y is omhoog, +Z is noord, +X is oost. Rotatie in het spel gaat in kwartslagen met de klok mee (`Footprint.QuarterTurns`); de voorkant van een gebouw kijkt in het model naar +Z.
- Schaal in de transform is altijd 1; maatwerk zit in de mesh.

## Pivot en footprint

- Pivot op de grond, in het midden van de footprint. Het model ligt op y = 0.
- De footprint is een heel aantal cellen (`Footprint.Width` × `Depth`, minstens 1 × 1). Het model past binnen die rechthoek (breedte = cellen × 8 m), met 0,5 m marge aan elke zijde voor dakrand en stoep.
- Bomen en props die geen cel innemen hebben geen footprint; de pivot staat op de voet van de stam.

## Mesh

- Eén mesh per model met submeshes per materiaal; maximaal 8 submeshes.
- Normalen en UV's aanwezig, tangenten niet nodig (`Cities/Surface` leidt ze af). UV's in meters (1 UV-eenheid = 1 m) zodat een bakstenen gevel op elke maat dezelfde steengrootte heeft.
- Geen onzichtbare achterkanten of binnenruimtes. Binnenwerk kost driehoeken en schaduw voor niets.
- Driehoekbudget per model op LOD0: huis tot 8.000, groot gebouw tot 30.000, voertuig tot 6.000, boom tot 4.000.

## LOD's

- LOD0 (dichtbij), LOD1 (±40 % van de driehoeken), LOD2 (±10 %); voor bomen daarna een impostor (`Cities/TreeImpostor`).
- Overgangen op schermhoogte 40 % / 15 % / 3 %; onder 3 % wordt het model niet meer getekend.
- Alle LOD's delen pivot en materialen; alleen de mesh verschilt.

## Materialen

- Wegen en gebouwen: `Cities/Surface`, terrein `Cities/Terrain`, water `Cities/Water`. Geen URP Lit voor nieuwe stadsmaterialen.
- Per model hoogstens 8 materialen; hergebruik de bestaande materialen in `Art/` voor hetzelfde type oppervlak.
- Ramen en lantaarns zetten hun lichtmasker in `_EmissionMap`, zodat ze 's nachts oplichten.

## Colliders

- Geen MeshCollider op gebouwen: klikken gaat via de footprint-bezetting (`FootprintOccupancy`) en de grond-raycast.
- Alleen als iets echt aangeklikt moet worden zonder footprint, een BoxCollider op de omhullende.

## Opleveren

- Mappen: `Art/<Categorie>/<Model>/` met prefab, mesh en materialen samen.
- Prefab heet als het model; de root heeft geen scripts. Alles wat het spel nodig heeft (footprint, kosten) staat in de `GameDefinition`, niet op de prefab.
- Procedurele modellen komen uit een builder in `Scripts/Editor/Models/`; wijzig het model daar en bouw opnieuw, niet met de hand in de asset.
