# Speelverslag eerste 30 minuten (E26-15)

> **Let op:** dit verslag komt uit een kale simulatie zonder Unity (`BalanceSimulationTests`), niet uit echt spelen met de camera en de
> werkbalk. Een script bouwt zoals een beginnende speler: woningen, industrie en winkels zodra de vraag het toelaat, en na een paar weken
> extra diensten en parken. Het toetst de balans van startkapitaal, vraag en groei. Een echte speelsessie in Unity moet dit nog bevestigen.

Instellingen: moeilijkheid Normaal, normale snelheid (10 ticks per seconde, 100 ticks per dag), 30 minuten spelen is ruim 25 spelweken.


## Wat de simulatie liet zien

- **Bootstrapprobleem gevonden en opgelost.** Zonder banen komt er niemand wonen, en zonder mensen is er geen vraag naar werk. De stad bleef
  daardoor op nul inwoners staan. `CityPopulation.BootstrapJobs` houdt de vraag naar werk op 0,5 totdat er 100 banen zijn.
- **Zonder diensten stagneert de stad.** Met alleen woningen en banen blijft de aantrekkingskracht neutraal en stopt de groei rond de 320
  inwoners. Dat is bedoeld: de speler moet diensten, parken en openbaar vervoer neerzetten. Daarom geeft `CityHints` die tips.
- **Startkapitaal** (1.000.000 bij Normaal) is ruim genoeg voor het eerste halfuur; het saldo daalt eerst licht door de bouwkosten en
  groeit daarna met de belasting. Een centrale (120.000) en drie diensten (samen 470.000) passen binnen het budget.
- **Niet afgesteld:** de gewichten van `MigrationBoost`, de belastingtarieven en de dienstenkosten zijn schattingen. Echt speelwerk moet
  uitwijzen of de stad te snel of te langzaam groeit.

## Hints bij de eerste acties

`CityHints.Next` kiest de belangrijkste volgende stap: geld raakt op, begin met weg en zones, banen nodig, diensten vanaf 150 inwoners,
bushaltes vanaf 600 inwoners. De hint staat onderin het scherm en verdwijnt zodra de stap gezet is.
