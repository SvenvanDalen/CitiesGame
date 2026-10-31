# Art-richting en materialen (E19-01)

## Stijl

Realistisch, gedempt en Nederlands/West-Europees van sfeer: fysisch plausibele materialen (PBR), geen cartoonkleuren, geen
harde outlines. Licht en weer doen het werk: zon, schaduw, nat wegdek en verlichte ramen 's avonds geven leven, niet
verzadigde kleuren.

## Shaders

Alles is URP-HLSL, handgeschreven, één pass voor kleur plus ShadowCaster en DepthOnly.

| Onderdeel | Shader | Opmerking |
|---|---|---|
| Terrein | `Cities/Terrain` | splat per laag, zand langs de oever |
| Water | `Cities/Water` | dikte uit scènediepte, breking, schuim; vereist depth en opaque texture (alleen PC-asset) |
| Wegen, gebouwen | `Cities/Surface` | URP-Lit-eigenschappen (`_BaseMap`, `_BaseColor`, `_BumpMap`, `_Smoothness`, `_Metallic`, `_EmissionMap`) plus nat en nacht |
| Bomen op afstand | `Cities/TreeImpostor` | |

Nieuwe materialen voor wegen of gebouwen gebruiken `Cities/Surface`, geen URP Lit, anders missen ze nat en nacht.

## Globale waarden

Het weer en de dag-nachtcyclus zetten deze met `Shader.SetGlobalFloat`; zonder zetten is het droog en dag.

- `_CityWetness` (0-1): wegdek en daken worden donkerder (`_WetDarken`) en glanzen (`_WetSmoothness`), in plassen eerst.
- `_CityNight` (0-1): de `_EmissionMap` (ramen, lantaarns) licht op, schaalt met `_EmissionColor`.

## Palet

Albedo blijft tussen 0,04 (asfalt) en 0,85 (verse verf); nooit zuiver zwart of wit.

| Materiaal | Albedo (sRGB) | Gladheid droog |
|---|---|---|
| Asfalt | 45-60 grijs | 0,2 |
| Trottoir/beton | 140-160 grijs | 0,15 |
| Bakstenen gevel | 150, 80, 65 | 0,1 |
| Dakpan | 90, 60, 55 | 0,25 |
| Glas | 60, 80, 90 | 0,9 |
| Markering | 220 wit, 230 geel | 0,2 |

## Regels

- Geen eigen texturen groter dan 1024 voor één gebouwtype; herhaal liever kleine, tegelbare texturen.
- Meshes zonder tangenten: de normal map wordt in de shader afgeleid, dus geen tangent-afhankelijke effecten.
- Geen transparantie op wegen en gebouwen (alleen het water is transparant).
