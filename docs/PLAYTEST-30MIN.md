# Speelverslag eerste 30 minuten (E26-15)

> **Let op:** dit verslag komt uit een kale simulatie zonder Unity (`BalanceSimulationTests`), niet uit echt spelen met de camera en de
> werkbalk. Een script bouwt zoals een beginnende speler: woningen, industrie en winkels zodra de vraag het toelaat, en na een paar weken
> extra diensten en parken. Het toetst de balans van startkapitaal, vraag en groei. Een echte speelsessie in Unity moet dit nog bevestigen.

Instellingen: moeilijkheid Normaal, normale snelheid (10 ticks per seconde, 100 ticks per dag), 30 minuten spelen is ruim 25 spelweken.

| Minuut | Week | Inwoners | Woningcapaciteit | Banen | Saldo | Vraag wonen | Vraag werk |
|---|---|---|---|---|---|---|---|
| 2 | 2 | 84 | 84 | 104 | 980228 | 0,01 | -1,00 |
| 4 | 4 | 180 | 192 | 104 | 976556 | -0,52 | 0,00 |
| 7 | 6 | 288 | 300 | 164 | 978384 | 0,20 | 0,00 |
| 9 | 8 | 408 | 420 | 254 | 988952 | 0,35 | 0,00 |
| 11 | 10 | 552 | 564 | 328 | 1014434 | 0,74 | -1,00 |
| 14 | 12 | 684 | 696 | 418 | 1068990 | 0,70 | 0,00 |
| 16 | 14 | 828 | 828 | 478 | 1144286 | 0,03 | 0,00 |
| 18 | 16 | 960 | 972 | 568 | 1229942 | 0,45 | 0,00 |
| 21 | 18 | 1104 | 1104 | 642 | 1349226 | 0,07 | 0,00 |
| 23 | 20 | 1236 | 1248 | 732 | 1482870 | 0,38 | 0,00 |
| 25 | 22 | 1368 | 1380 | 822 | 1638874 | 0,89 | -1,00 |
| 28 | 24 | 1524 | 1536 | 926 | 1807952 | 0,91 | -0,50 |

## Wat de simulatie liet zien

- **Bootstrapprobleem gevonden en opgelost.** Zonder banen komt er niemand wonen, en zonder mensen is er geen vraag naar werk. De stad bleef
  daardoor op nul inwoners staan. `CityPopulation.BootstrapJobs` houdt de vraag naar werk op 0,5 totdat er 100 banen zijn.
- **Zonder diensten stagneert de stad.** Met alleen woningen en banen blijft de aantrekkingskracht neutraal en stopt de groei rond de 320
  inwoners. Dat is bedoeld: de speler moet diensten, parken en openbaar vervoer neerzetten. Daarom geeft `CityHints` die tips.
- **Startkapitaal** (1000.000 bij Normaal) is ruim genoeg voor het eerste halfuur; het saldo daalt eerst licht door de bouwkosten en
  groeit daarna met de belasting. Een centrale (120000) en drie diensten (samen 470000) passen binnen het budget.
- **Niet afgesteld:** de gewichten van `MigrationBoost`, de belastingtarieven en de dienstenkosten zijn schattingen. Echt speelwerk moet
  uitwijzen of de stad te snel of te langzaam groeit.

## Hints bij de eerste acties

`CityHints.Next` kiest de belangrijkste volgende stap: geld raakt op, begin met weg en zones, banen nodig, diensten vanaf 150 inwoners,
bushaltes vanaf 600 inwoners. De hint staat onderin het scherm en verdwijnt zodra de stap gezet is.
