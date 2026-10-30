# 0004 Deterministische willekeur: PCG32 met afgeleide stromen per tick

- **Status:** aangenomen
- **Datum:** 2026-10-30
- **Backlogitem:** E01-11

## Vraag

Hoe krijgt de simulatie willekeur (bestemmingen, leeftijden, branden, gebouwvarianten) zodat dezelfde seed met dezelfde commands dezelfde stad geeft, voor tests, bugreproductie en saves, terwijl systemen als parallelle Burst-jobs draaien?

## Besluit

Eén **wereldseed** per stad (`SimulationRandom`). Elke generator wordt afgeleid uit seed, **stroom** (gehashte naam zoals `"Verkeer.Bestemming"`), **tick** en eventueel **objectindex**. Er bestaat geen generator met toestand die over ticks heen leeft. De generator zelf is PCG32 (`DeterministicRandom`).

| Aspect | Gekozen | Alternatief |
| --- | --- | --- |
| Algoritme | PCG32 (XSH-RR): 16 bytes toestand, goede statistiek, sprongen in O(log n), referentie-uitvoer te testen | `Unity.Mathematics.Random` (xorshift32): zwakker en zonder stromen; `System.Random`/`UnityEngine.Random`: globale toestand, verschilt per runtime |
| Parallel | Generator per object: `At(tick, index)`. De volgorde van de werkthreads doet er niet toe | Gedeelde generator met lock, of vooraf getrokken getallen: traag of volgordeafhankelijk |
| Save | Alleen de seed (sectie `RAND`, 8 bytes). Samen met de tick uit `CLCK` loopt een geladen stad exact verder | Toestand van elke stroom opslaan: meer data, en elk nieuw systeem moet eraan denken |
| Afleiden | SplitMix64-menging van seed, stroom, tick en index; stroomnamen via FNV-1a, stabiel op elke runtime | `string.GetHashCode`: verschilt per proces |

## Gevolgen

- Een systeem roept `Register("Systeem.Doel")` één keer aan en gebruikt daarna `tick.Random(stroom).At(tick.Tick[, index])`. Twee namen met dezelfde hash geven direct een fout.
- Wie binnen één tick op meerdere plekken trekt, gebruikt aparte stromen of indices. Zo verandert een extra trekking op de ene plek niets aan de andere.
- `StateChecksum` hasht dezelfde secties als de save. Twee runs zijn per tick te vergelijken, en de eerste afwijkende sectie wijst het systeem aan dat uiteenloopt. E24-08 (input-recorder) bouwt hierop voort.
- Gehele uitkomsten zijn op elk platform gelijk. Zwevendekommagetallen zijn gelijk binnen een build en platform; een test bevestigt dat Burst-jobs en managed code exact dezelfde getallen geven.
- `DeterminismSourceTests` weigert `UnityEngine.Random`, `System.Random`, `Guid`, `DateTime.Now` en frametijd in `Simulation` en `World`.
