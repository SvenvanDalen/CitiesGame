# Vragen bij E03-05 Watersimulatie

E03-05 is overgeslagen tot deze vragen beantwoord zijn. E03-06 (waterbronnen en afvoer) bouwt erop voort en wacht ook. Deze PR is tijdelijk en wordt gesloten zodra de antwoorden er zijn.

**Klaar als:** rivieren, meren en zee met waterhoogte, stroomrichting en kustlijn die meebeweegt met terrein. Het item is XL en zegt "opsplitsen bij start".

## Wat er nu is

- `WorldMap.WaterDepth`: één waarde per hoekpunt (8 m), 2305 × 2305 = 5,3 miljoen punten. De laag wordt opgeslagen, maar niets vult hem.
- Het gegenereerde terrein heeft een rivierdal maar geen zee: het laagland ligt rond 40 m.
- `docs/PERFORMANCE.md` heeft geen tickbudget voor water. De dichtstbijzijnde regel is "Overig (vervuiling, grondwaarde, rampen)" met 0,5 ms.

## Vragen

1. **Opsplitsen.** Mijn voorstel is drie items in M1:
   - **E03-05a Waterstroming:** een shallow-water-simulatie (virtual pipes) in een Burst-job, met waterhoogte en stroomsnelheid per punt. De zee is een vast peil langs de kaartrand. Opslaan en laden horen erbij.
   - **E03-05b Waterweergave:** een watershader met kleur naar diepte, normals die met de stroom meebewegen en schuim langs de oever.
   - **E03-05c Kust volgt terrein:** terraformen werkt meteen door in het water. Zand langs de oever bestaat al in de terreinshader.

   A. Zo opsplitsen. B. Anders, namelijk …

2. **Resolutie en budget.** Het hele raster van 8 m elke tick doorrekenen is voor de CPU te zwaar.
   - A. Een waterraster van 32 m (577 × 577 punten), rond 0,3 ms per stap, 10 stappen per speelseconde. Het water wordt voor de weergave naar 8 m geïnterpoleerd. Er komt een nieuwe budgetregel "Water (E03-05)" van 0,6 ms, af van de reserve.
   - B. Een raster van 16 m (1153 × 1153). Dat is nauwkeuriger bij smalle rivieren, maar rond 1,2 ms per stap.
   - C. 8 m, alleen rond de actieve waterpartijen. Dat is het meest precies, maar ook het meest complex.

   Mijn voorkeur: **A**.

3. **Zee en rivier op de standaardkaart.** Moet de generator een kustlijn maken (zee langs één kaartrand, peil 0 m) en een rivier die van buiten de kaart instroomt met een vast debiet?
   - A. Ja, allebei.
   - B. Alleen de rivier.
   - C. Geen van beide; water komt pas via bronnen (E03-06).

   Mijn voorkeur: **A**.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
