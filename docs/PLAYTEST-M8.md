# Speelrapport M8 (#534) – Goede speelbare versie

Dit rapport zegt eerlijk wat gecontroleerd is en wat niet.

## Wat is geverifieerd (tests, CI, Sonar)

- Alle M8-onderdelen hebben unit tests in `Tests/EditMode` (seizoenen, ongevallen, winter, hittegolf, klimaat, vervoer, hints, timelapse, voorzieningen).
- CI en Sonar zijn groen op de PR's #535, #536 en #537 (nieuwe code ≥ 80% dekking, 0 issues).
- De gevolgen die het spel moeilijker maken (ongevallen, winter, hittegolven, klimaateffecten) staan **standaard uit** in `ExpansionHostBehaviour`.
- Balans op papier (`CityVenues`): elke voorziening kost 60 per dag onderhoud; de opbrengsten zijn bescheiden en hangen af van inwoners en weer. Een lege stad heeft dus geen kosten, en een voorziening zonder bezoekers kost geld. De wekelijkse melding "venues" laat winst of verlies zien.
- Ongevallen: kans per controle is maximaal 0,2% bij een volle weg, max. 8 tegelijk, opruimen na 90 s. De hooktest (`RoadTrafficTests`, categorie `UnityRuntime`) controleert dat de hook stil blijft als het model uit staat en begrensd blijft als het aan staat.

## Wat NIET is geverifieerd

- Het spel is in deze sessie **niet daadwerkelijk gespeeld** in Unity. Weggladheid, groeistabiliteit en moeilijkheid zijn niet met een speelsessie gemeten.
- Tests met `[Category("UnityRuntime")]` draaien niet in de Sonar-CI; ze moeten lokaal in de Unity Test Runner (EditMode).
- `Main.unity` moet opnieuw gebouwd worden zodat de 24 voorzieningen in de scène staan (zie hieronder).

## Zelf doen

1. Open het project in Unity, kies menu **Cities ▸ Bouw hoofdscène**, en sla de scène op.
2. Draai in de Test Runner alle EditMode-tests (ook `UnityRuntime`).
3. Speel 30 minuten volgens `PLAYTEST-30MIN.md` en let op:
   - Wegen lopen vloeiend door bochten en kruisingen.
   - De bevolking groeit stabiel, zonder schommelingen of instorten.
   - Het saldo van de voorzieningen is na een paar weken niet structureel zwaar negatief.
   - Zet ongevallen, winter en hittegolf aan en kijk of het nog speelbaar blijft.
4. Optioneel: stash "pre-M8 werkboom" bevat eerder onafgemaakt werk (Main.unity en Inter-fonts) van branch `feature/E27-01-werkbalk-categorieen`. Terughalen kan met `git stash pop`; dat botst waarschijnlijk met een herbouwde `Main.unity`, dus kies bewust.
