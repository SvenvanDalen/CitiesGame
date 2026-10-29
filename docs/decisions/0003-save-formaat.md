# 0003 Save-formaat: eigen binair bestand met secties

- **Status:** aangenomen
- **Datum:** 2026-10-29
- **Backlogitem:** E22-01

## Vraag

Hoe slaat het spel de volledige wereldstatus op (netwerk, gebouwen, agents, lagen), zodat saves klein en snel zijn, beschadiging opvalt en oude saves na updates blijven werken?

## Besluit

Een eigen binair bestand (`.ctysave`) met een kop en daarna één blok per **sectie**. Elk systeem met status levert zijn eigen sectie via `ISaveSection`. JSON, BinaryFormatter en de Unity-serializer worden niet gebruikt.

| Aspect | Gekozen | Alternatief |
| --- | --- | --- |
| Grootte | Binair met Deflate: lege kaart ≈ 0,1 MB, heuvelachtige volle kaart 11 MB (53 MB in het geheugen) | JSON: meer dan 10× zo groot, traag voor miljoenen cellen |
| Snelheid | Lagen als één blok bytes (`WriteShuffled`), zonder object per cel | Per waarde serialiseren |
| Versies | Versienummer per sectie; nieuwe systemen voegen secties toe zonder oude saves te breken | Eén versie voor alles |
| Veiligheid | Geen typen of code uit het bestand; alleen bekende secties met grenzen op elke lengte | BinaryFormatter: onveilig en verouderd |

## Indeling

Alle getallen zijn little-endian.

```
Kop          "CTYS" | bestandsindeling u16 (1) | vlaggen u16 (0) | aantal blokken i32
Blok (×n)    id 4×ASCII | versie i32 | opslag u8 (0 ruw, 1 Deflate) | lengte i32 | opgeslagen lengte i32 | CRC-32 i32 | inhoud
```

- Het eerste blok is altijd `META` (stadsnaam, spelversie, opslagmoment, tick, datum, speeltijd), ongecomprimeerd, zodat een lijst van saves alleen de kop hoeft te lezen.
- De CRC-32 (IEEE) is berekend over de ongecomprimeerde inhoud. Een beschadigd of afgekapt bestand geeft altijd een `SaveFormatException`.
- Kleine blokken (onder 64 bytes) en blokken die niet kleiner worden, staan ongecomprimeerd.

## Laden is alles of niets

`SaveFile.Read` leest en controleert eerst alle secties. Elke sectie geeft een toepassing terug, en die worden pas uitgevoerd als de hele save goed is. Een kapotte save laat het lopende spel dus ongewijzigd. Onbekende secties worden overgeslagen. Secties die in de save ontbreken (bijvoorbeeld van een systeem dat nieuwer is dan de save) worden gemeld en niet aangeraakt.

## Secties nu

| Id | Inhoud | Klasse |
| --- | --- | --- |
| `META` | Metadata | `SaveMetadata` |
| `CLCK` | Tick, snelheid en kalenderinstellingen; openstaande commands vervallen | `SimulationClockSection` |
| `WMAP` | Afmetingen en alle lagen van de wereldkaart; een andere kaartgrootte wordt geweigerd | `WorldMapSection` |

ID-allocators (`IdAllocator.WriteTo` en `IdAllocatorState.Read`) bewaren generaties en de volgorde van vrije plekken. Na laden worden zo dezelfde ID's uitgedeeld als zonder opslaan. Netwerk, gebouwen en agents krijgen hun sectie zodra ze bestaan (E04, E05, E07, E08); elk nieuw systeem met status levert er een.

## Meting

Volledige kaart (9×9 tegels, 2304×2304 cellen) met heuvels en een gezoneerde wijk, in de editor (Mono):

| Variant | Bestand | Opslaan |
| --- | --- | --- |
| Ruwe floats, Deflate Optimal | 17,0 MB | 3,8 s |
| Byte-vlakken, Deflate Optimal | 11,1 MB | 2,5 s |
| **Byte-vlakken, Deflate Fastest** (gekozen) | **11,1 MB** | **1,8 s** (compressie); 2,8 s totaal |

Laden duurt 0,8 s. Byte-vlakken (eerst alle eerste bytes, dan alle tweede, ...) zetten de gelijke exponentbytes van een glad verlopende laag naast elkaar. Daardoor wordt het bestand een derde kleiner en Optimal levert niets extra op.

## Gevolgen

- Een systeem met status implementeert `ISaveSection` en wordt in `SaveGameBehaviour` toegevoegd. Wie de inhoud wijzigt, verhoogt `Version` en blijft oude versies lezen (migratie: E22-06).
- Opslaan blokkeert nu de hoofdthread (ongeveer 3 s op een volle kaart in de editor). Autosave (E22-03) moet de data kopiëren en op een achtergrondthread comprimeren en schrijven. Het budget komt in E23-01.
- Saves staan in `Application.persistentDataPath/Saves`. Schrijven gaat via een tijdelijk bestand, zodat een crash of volle schijf de vorige save heel laat.
