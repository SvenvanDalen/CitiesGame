# Backlog: City Builder in Unity (privégebruik)

Oct 29, 2026 · @Sven vd

## Uitgangspunten en technische keuzes

Dit backlog dekt alle systemen van een eigen stadsbouwer in Unity, verdeeld in 25 epics en negen mijlpalen, zodat je na elke mijlpaal iets speelbaars hebt. Omdat het voor privégebruik is, ontbreken releasezaken zoals lokalisatie, achievements, platformcertificering en storefront-integratie.

- **Engine:** Unity 6 LTS met URP, alleen pc (Windows; macOS/Linux optioneel).
- **Simulatie:** data-georienteerd. Burgers, voertuigen en gebouwen zijn structs in NativeArrays, verwerkt met Burst-jobs op een vaste simulatiestap (bijv. 10 Hz) die losstaat van de framerate. In E01 valt na een spike de keuze tussen Entities (DOTS) en een eigen lichte ECS; aanbeveling is Entities voor agents (tienduizenden stuks) en gewone MonoBehaviours voor UI en tools.
- **Rendering:** GPU instancing of Entities Graphics voor agents en props, met LOD en culling per tegel.
- **Kaart:** opgedeeld in tegels (einddoel 9×9 tegels van ca. 2 km). Je start met 3×3 bouwbare tegels en schaalt op in E23.
- **Eigen ontwerp:** eigen spelnaam, eigen modellen, geluiden en icoontjes, en eigen invulling van de mechanica; geen assets of ontwerpen van bestaande spellen overnemen.
- **Realisme:** grote stadsbouwers zijn het werk van een groot team over jaren. M0 t/m M4 geeft al een spel dat als stadsbouwer aanvoelt; alles daarna is verdieping.

## Werkwijze en schalen

Elk item is zo gesneden dat het in één Git-branch af te ronden is; items met schatting XL splits je bij de start op.

- **ID:** E\<epic>-\<nummer>, bijvoorbeeld E04-07.
- **MS:** de mijlpaal (M0 t/m M8) waarin het item thuishoort; dat is meteen de prioriteit.
- **Est:** S = tot 1 werkdag, M = 2 tot 3 dagen, L = ca. 1 week, XL = 2 weken of meer.
- **Klaar als:** het item werkt in een testscene, geeft geen Burst- of job-fouten, haalt het performancebudget uit E23 en overleeft opslaan en laden (zodra E22-01 bestaat). Deterministische logica krijgt minstens één EditMode-test.

## Roadmap en mijlpalen

Negen mijlpalen, in volgorde; elke mijlpaal heeft een poort die je moet halen voordat de volgende begint.

1. **M0 Fundament:** project, architectuur, simulatiekern, camera, debugtools. Poort: een lege wereld met camera, simulatieklok en save/load-skelet draait stabiel.
2. **M1 Wereld en wegen:** terrein, water, natuur, wegen. Poort: je vormt terrein en legt een wegennet aan met kruispunten, bruggen en tunnels.
3. **M2 Eerste stad:** zones, gebouwen, stroom, water, riool, afval. Poort: gebouwen verschijnen, groeien en raken verlaten zonder voorzieningen.
4. **M3 Levende stad:** burgers, voertuigen, verkeer, economie en begroting. Poort: inwoners reizen naar werk en winkels, files ontstaan, belasting en budget werken.
5. **M4 Volwaardige stad (MVP):** stadsdiensten, openbaar vervoer, landwaarde, milieu. Poort: een stad van 50.000 inwoners is zinvol door te spelen met duidelijke feedback.
6. **M5 Diepgang:** districten, beleid, industrie, handel, buitenverbindingen, toerisme, rampen, progressie.
7. **M6 Presentatie:** info-views, statistieken, volledige UI, visuals, weer, audio.
8. **M7 Schaal en gereedschap:** content-pipeline, editors, spelmodi, performance voor de volledige kaart.
9. **M8 Uitbreidingen:** uitbreidingsthema's uit E25.

## E01 Project, architectuur en simulatiekern

Deze epic legt de kern vast waarop elk ander systeem draait; vrijwel alles hoort in M0.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E01-01 | Unity-project opzetten | Unity 6 LTS, URP, Git met LFS, assembly definitions per systeem, codeconventies; project bouwt schoon | M0 | S |
| E01-02 | Spike: Entities of eigen ECS | Besluitdocument met benchmark van 50.000 bewegende agents op je eigen hardware | M0 | M |
| E01-03 | Simulatieklok | Vaste stap, pauze, 1x/2x/3x snelheid, spelkalender (dag, maand, jaar) instelbaar | M0 | M |
| E01-04 | Wereld-datamodel | Tegelgrid met celgebaseerde lagen (hoogte, water, zone, vervuiling, landwaarde) als NativeArrays | M0 | L |
| E01-05 | ID-systeem | Stabiele IDs met generatienummer voor gebouwen, nodes, segmenten, burgers, voertuigen | M0 | M |
| E01-06 | Command-bus | Tools sturen commands (bouw, sloop, upgrade) naar de simulatie; elke command is te valideren | M0 | M |
| E01-07 | Spatial partitioning | Grid of quadtree voor buurtzoekopdrachten en picking, zonder allocaties | M0 | M |
| E01-08 | Job-scheduling | Simulatiesystemen draaien in vaste volgorde met afhankelijkheden; geen main-thread-blokkades | M0 | L |
| E01-09 | Data-gedreven configuratie | Gebouwen, voertuigen, beleid en kosten staan in ScriptableObjects of JSON; wijzigen zonder code | M0 | M |
| E01-10 | Logging en profiler-markers | Elk systeem heeft een eigen profiler-marker en logniveau | M0 | S |
| E01-11 | Deterministische RNG | Zelfde seed en input geven dezelfde uitkomst, voor tests en bugreproductie | M1 | M |
| E01-12 | Undo/redo van bouwacties | Laatste 20 bouwacties zijn terug te draaien, inclusief kosten | M2 | L |

## E02 Camera en invoer

Een prettige camera en een solide tool-systeem bepalen hoe het spel aanvoelt; dit staat vroeg op de lijst omdat alle bouwtools erop leunen.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E02-01 | Vrije camera | Pannen, zoomen, roteren en kantelen met muis en WASD; edge-scroll optioneel | M0 | M |
| E02-02 | Zoom naar cursor en limieten | Zoom richt op het punt onder de cursor; camera botst niet met terrein en heeft min/max hoogte | M0 | S |
| E02-03 | Input System met rebinding | Alle acties hebben een herbindbare toets; instellingen blijven bewaard | M0 | M |
| E02-04 | Picking | Raycast op terrein, wegen, gebouwen, burgers en voertuigen met prioriteitsvolgorde | M1 | M |
| E02-05 | Tool-framework | Actieve tool, preview-ghost, snappen, Esc om te annuleren, rood bij ongeldig | M1 | M |
| E02-06 | Volgcamera | Camera volgt een burger of voertuig; eerstepersoonsmodus op straatniveau | M6 | M |
| E02-07 | Camerabladwijzers | Locaties opslaan en terugroepen met sneltoets | M6 | S |
| E02-08 | Screenshot-modus | UI verbergen, vrij roteren, hoge-resolutie opname | M6 | S |

## E03 Terrein, water en natuur

Terrein en water zijn de eerste visuele indruk en het zwaarste datamodel van M1; water vraagt de meeste tijd.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E03-01 | Heightmap-terrein | Eén heightmap per tegel, chunked mesh met LOD, naadloos tussen tegels | M1 | L |
| E03-02 | Terrein-shader | Splatmap met gras, zand, rots, akker en bebouwde grond; overgangen zijn zacht | M1 | M |
| E03-03 | Terraform-tools | Omhoog, omlaag, vlak, glad, naar hoogte; penseelgrootte en -sterkte instelbaar | M1 | L |
| E03-04 | Grondverzet | Grond verplaatsen kost geld en heeft een bron of put nodig; kosten verschijnen in preview (na het geldmodel E11-01) | M3 | M |
| E03-05 | Watersimulatie | Rivieren, meren en zee met waterhoogte, stroomrichting en kustlijn die meebeweegt met terrein | M1 | XL |
| E03-06 | Waterbronnen en afvoer | Je plaatst bronnen en afvoerpunten; debiet is instelbaar | M1 | L |
| E03-07 | Bomen | Bomen staan als instances op een dichtheidskaart; kappen en planten werkt; bomen verdwijnen onder gebouwen | M1 | L |
| E03-08 | Natuurlijke grondstoffenlagen | Kaartlagen voor olie, erts, vruchtbare grond en bos, zichtbaar in een overlay | M2 | M |
| E03-09 | Tegels ontgrendelen | Tegels koop je voor geld of bevolking; buitenste grenzen blokkeren bouwen | M3 | M |
| E03-10 | Grondvervuilingslaag | Per cel een vervuilingswaarde die E12 vult en leest | M4 | S |
| E03-11 | Fauna | Vogels, vissen en kuddes als ambient leven zonder simulatielast | M6 | M |
| E03-12 | Heightmap-import | PNG of RAW inlezen met schaal en waterniveau; voorbeeldkaart gemaakt | M7 | M |

## E04 Wegen en infrastructuur

Het wegennet is het skelet van de stad en het lastigste onderdeel van M1; kruispunten en bruggen nemen het grootste deel van de tijd.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E04-01 | Netwerkmodel | Nodes, segmenten (Bezier-splines), lanes met richting en types, connecties tussen lanes | M1 | XL |
| E04-02 | Aanleg-tool | Recht, curve en vrije lijn; snappen aan bestaande wegen, hoek en lengte; lengte en kosten in tooltip | M1 | L |
| E04-03 | Segment-mesh | Weg volgt spline, past terrein aan, houdt texturen per lane; update bij wijziging in minder dan 50 ms | M1 | L |
| E04-04 | Kruispunten | Automatische geometrie voor 3 tot 8 armen, afrondingen, zebrapaden | M1 | XL |
| E04-05 | Wegtypes | Data-gedreven set: dorpsweg, tweebaans, vierbaans, laan, snelweg, eenrichting, fietspad, voetpad | M1 | L |
| E04-06 | Bruggen en tunnels | Hoogteregeling, pijlers, tunnelmond, kosten; kruisen van water en bergen | M1 | XL |
| E04-07 | Sloop-tool | Weg, node of segment verwijderen met bevestiging en gedeeltelijke terugbetaling | M1 | S |
| E04-08 | Upgraden en richting omkeren | Wegtype wisselen zonder aansluitingen te breken; eenrichting omkeren | M2 | M |
| E04-09 | Segmenten splitsen en samenvoegen | Node plaatsen in segment en netjes verwijderen | M2 | M |
| E04-10 | Verhoogde wegen en viaducten | Wegen boven maaiveld met pijlers en aansluiting op de grond | M2 | L |
| E04-11 | Snelweg-knooppunten | Op- en afritten, klaverblad en turbine-opties met voorbeeldpresets | M2 | L |
| E04-12 | Rotondes | Rotonde-tool met straal en aantal armen; voorrang correct | M3 | L |
| E04-13 | Verkeersregels per kruispunt | Voorrang, stop, verboden in te rijden, snelheidslimiet per segment | M3 | L |
| E04-14 | Parkeerplekken langs de weg | Per wegtype aan of uit; vaste plekken in mesh en data | M3 | M |
| E04-15 | Fiets- en voetpadnetwerk | Aparte lanes en losse paden met eigen routering | M3 | M |
| E04-16 | Onderhoud en wegslijtage | Wegconditie daalt met verkeer; onderhoud kost geld en voertuigen | M5 | M |
| E04-17 | Aanlegondersteuning | Raster, hoeksnappen, vaste lengte en hoogtemeting tijdens het tekenen | M2 | S |

## E05 Zonering en gebouwen

Zones zijn het groeimechanisme van de stad: je tekent, de simulatie bouwt; dit is het hart van M2.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E05-01 | Zone-data | Cellen naast wegen (tot 4 diep) krijgen type en dichtheid: wonen, winkels, industrie, kantoor; laag en hoog | M2 | L |
| E05-02 | Zone-tool | Verfpenseel, rechthoek, vullen, wissen; zone volgt de weg en toont bebouwbare cellen | M2 | M |
| E05-03 | Vraagmodel (RCI) | Vraagmeters voor wonen, werken en winkelen berekend uit leegstand, banen en inkomsten | M2 | L |
| E05-04 | Gebouwspawner | Kiest gebouw per zone, dichtheid, niveau en grootte; gebouw kijkt naar de weg; geen overlap | M2 | XL |
| E05-05 | Gebouwcapaciteit | Woningen, banen per opleidingsniveau, winkelcapaciteit en productie per gebouw | M2 | M |
| E05-06 | Bouwfase | Bouwtijd met steigers of bouwanimatie; kosten en doorlooptijd configureerbaar | M2 | M |
| E05-07 | Handmatig plaatsen | Diensten en monumenten plaatsen met snappen aan weg en terrein, draaibaar | M2 | M |
| E05-08 | Gebouwvariatie | Minstens drie varianten per zone, dichtheid en niveau; kiest op willekeur met seed | M2 | L |
| E05-09 | Gebouwniveaus | Niveau 1 tot 5 met upgradevoorwaarden: landwaarde, diensten, opleiding, weinig vervuiling | M3 | L |
| E05-10 | Verval en sloop | Verlaten gebouwen worden gesloopt en laten puin achter; de grond is daarna weer bebouwbaar | M3 | L |
| E05-11 | Bouwblokken | Rijtjes, vrijstaand en blokken met gedeeld erf op basis van bouwvolume | M3 | M |
| E05-12 | Zone-specialisaties | Eco-woonwijk, IT-cluster, toeristisch winkelen en dergelijke, aan beleid gekoppeld | M5 | L |
| E05-13 | Nachtverlichting gebouwen | Ramen lichten op afhankelijk van bewoning en tijd | M6 | S |

## E06 Nutsvoorzieningen

Stroom, water, riool en afval zijn de eerste gebouwafhankelijkheden; zonder hen worden gebouwen onbewoonbaar en verlaten.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E06-01 | Elektriciteitsnet | Netwerkmodel met aansluiting via leidingen of nabijheid; capaciteit en verbruik per tick | M2 | L |
| E06-02 | Basiscentrales | Kolen, wind en zon met productie, kosten en vervuiling per type | M2 | L |
| E06-03 | Stroomtekort | Vraag boven aanbod geeft uitval per wijk, verlichting valt weg, gebouwen verliezen geluk | M2 | M |
| E06-04 | Hoogspanning | Pylonen met bereik over water en berg; transformatoren | M2 | M |
| E06-05 | Waterleiding | Ondergronds netwerk met capaciteit; zichtbaar in een ondergrond-weergave | M2 | L |
| E06-06 | Waterinname en zuivering | Pomp uit rivier of grondwater, toren en zuiveringsinstallatie; waterkwaliteit is een waarde | M2 | L |
| E06-07 | Riolering | Afvoerleidingen, uitlaat en zuivering; ongezuiverd afval vervuilt stroomafwaarts | M2 | L |
| E06-08 | Waterdruk en opslag | Watertoren en reservoir vangen pieken op; tekort geeft uitval | M3 | M |
| E06-09 | Afvalproductie | Gebouwen produceren afval, vullen een opslag en roepen een ophaalvoertuig | M2 | L |
| E06-10 | Afvalverwerking | Stortplaats, verbrandingsoven, recycling en composteerinstallatie met eigen kosten en effecten | M4 | L |
| E06-11 | Vulling en sluiting stortplaats | Vol dan dicht, restvervuiling op de grond | M4 | M |
| E06-12 | Grondwater | Beperkte grondwaterlaag die door industrie vervuild raakt | M4 | M |
| E06-13 | Meer stroombronnen | Gas, olie, waterkracht, geothermie, kernenergie, zonnetoren, afvalenergie, batterijen | M5 | XL |
| E06-14 | Netbeheer | Afzonderlijke netwerk-overlays en capaciteitsinspectie per net | M4 | M |

## E07 Burgers en demografie

Burgers geven de stad leven en vragen werk, huizen en diensten; het model moet licht blijven omdat er honderdduizenden van zijn.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E07-01 | Burgermodel | Struct met leeftijd, opleiding, gezondheid, geluk, huishouden, woning en werk; compact in geheugen | M3 | L |
| E07-02 | Levenscyclus | Geboorte, opgroeien, werken, pensioen en overlijden volgens configureerbare kansen | M3 | L |
| E07-03 | Huishoudens | Gezinnen delen een woning, verhuizen bij tekort aan ruimte of werk, kinderen blijven bij ouders | M3 | L |
| E07-04 | Migratie | Aantrekkelijkheid van de stad (werk, landwaarde, diensten, belasting) stuurt in- en uitstroom | M3 | L |
| E07-05 | Banen en matching | Werkzoekenden worden op opleiding en afstand aan vacatures gekoppeld; werkloosheid wordt gemeten | M3 | L |
| E07-06 | Dagritme | Slapen, werk of school, winkelen, vrije tijd; reisdoel volgt uit het schema | M3 | L |
| E07-07 | Agents met level of detail | Zichtbare voetgangers dichtbij, statistische burgers ver weg; geen zichtbare sprongen bij wisselen | M3 | XL |
| E07-08 | Onderwijsniveaus | Basis, middelbaar en universiteit bepalen banen en inkomen | M4 | L |
| E07-09 | Gezondheid en sterfte | Ziektekans door vervuiling en diensten; ziekenhuiscapaciteit en sterfte | M4 | M |
| E07-10 | Geluksmodel | Gewogen factoren: werk, belasting, vervuiling, geluid, veiligheid, diensten, reistijd | M4 | L |
| E07-11 | Demografische statistieken | Leeftijdspiramide, werkloosheid, opleiding en inkomensverdeling | M6 | M |
| E07-12 | Burger-inspector | Klik op een burger voor huis, werk, route, stemming en levensfase | M6 | M |

## E08 Verkeer en voertuigen

Verkeer is de zwaarste simulatielast en het onderdeel dat het spel zijn karakter geeft; pathfinding en kruispunten bepalen of het werkt.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E08-01 | Pathfinding | Burst-gebaseerde A\* of contraction hierarchies op de lanegraaf, met kosten voor afstand, snelheid en congestie | M3 | XL |
| E08-02 | Voertuig-agents | Voertuigen volgen lanes met volgafstand (bijv. IDM-model), versnellen en remmen geloofwaardig | M3 | XL |
| E08-03 | Lanewissel en kruispunt-reservering | Voertuigen wisselen van baan, wachten op ruimte en reserveren kruispuntdelen zonder deadlocks | M3 | XL |
| E08-04 | Verkeerslichten | Automatische fasen, handmatig aanpasbaar, per kruispunt aan of uit | M3 | L |
| E08-05 | Voorrangsregels | Stop, voorrang, rotonde en rechts-voor-links lopen via dezelfde reserveringslaag | M3 | L |
| E08-06 | Congestie | Snelheid per segment volgt dichtheid; files ontstaan en lossen op; deadlock-detectie met herstel | M3 | L |
| E08-07 | Voertuigtypes | Personenauto, bestelwagen, vrachtwagen, motor en fiets met eigen afmetingen en gedrag | M3 | M |
| E08-08 | Voertuig-LOD | Ver weg als vereenvoudigde punten, dichtbij volledig; pooling zonder allocaties | M3 | L |
| E08-09 | Vervoerskeuze | Burgers kiezen lopen, fiets, auto of OV op reistijd, kosten en bezit | M4 | L |
| E08-10 | Parkeren | Zoeken naar plek, parkeergarage, gebouwparkeren en straatparkeren; vol dan rondrijden | M4 | L |
| E08-11 | Goederenverkeer | Vrachtwagens leveren aan winkels en brengen productie naar buitenverbindingen | M4 | L |
| E08-12 | Voetgangers en fietsers | Eigen routering, oversteken bij zebrapaden en lichten, geen botsen met auto's | M4 | L |
| E08-13 | Verkeersdata | Flow-overlay, knelpuntdetectie, tellers per segment en gemiddelde reistijd | M4 | M |
| E08-14 | Verkeersbeheer | Tol, vrachtverbod, prioriteit voor OV en nooddiensten, snelheidslimieten | M5 | M |
| E08-15 | Ongelukken | Botsingen met stilstand en hulpdiensten als optie | M8 | M |
| E08-16 | Elektrische voertuigen | Laadstations en bereik, gekoppeld aan E25-03 | M8 | M |
| E08-17 | Verkeer rond evenementen | Evenementen veroorzaken tijdelijke pieken in auto- en OV-verkeer en parkeerdruk; extra bussen, afzettingen en eenrichtingsroutes zijn instelbaar | M5 | M |

## E09 Openbaar vervoer

Openbaar vervoer is een eigen laag bovenop het wegennet; begin met bus en breid uit naar rails, water en lucht.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E09-01 | Lijnensysteem | Haltes en lijnen tekenen, frequentie en voertuigaantal instellen, lijn bewaard en bewerkbaar | M4 | XL |
| E09-02 | Bus | Halte, busstation en depot; bussen volgen de lijn en laden passagiers | M4 | L |
| E09-03 | Overstappen | Passagiers plannen routes over meerdere lijnen, met wachttijd en overstapkosten | M4 | L |
| E09-04 | Tickets en lijnkosten | Prijs per lijn, onderhoudskosten en inkomsten per lijn zichtbaar | M4 | M |
| E09-05 | Tram | Rails op of naast de weg, tramhaltes, voorrang op kruispunten | M5 | L |
| E09-06 | Metro | Ondergrondse tunnels, stations met diepte, een aparte ondergrondse weergave | M5 | XL |
| E09-07 | Trein | Spoor, stations, passagiers- en goederentreinen, overwegen en wissels | M5 | XL |
| E09-08 | Waterverkeer | Veerboot, vrachtschip, havens en kanalen | M5 | L |
| E09-09 | Luchtvaart | Luchthaven met baan, terminal en gates, vliegtuigen, geluidsoverlast | M5 | XL |
| E09-10 | Fietsinfrastructuur en deelfietsen | Fietsstroken, fietspaden en verhuurpunten | M5 | M |
| E09-11 | OV-prioriteit | Busbaan en voorrang op kruispunten voor lijnen | M5 | M |
| E09-12 | Lijnstatistieken | Bezetting, wachttijd, winst en passagiersaantallen per lijn en per halte | M6 | M |
| E09-13 | Extra vervoersmodi | Kabelbaan, monorail, luchtschip en taxi, zie E25-02 | M8 | L |

## E10 Stadsdiensten

Alle diensten delen hetzelfde patroon van dekking en uitrukvoertuigen, dus bouw eerst het gedeelde framework en daarna de afzonderlijke diensten.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E10-01 | Dienstenframework | Dekking via bereik, dispatch van voertuigen naar meldingen, capaciteit en onderhoudskosten per gebouw | M4 | L |
| E10-02 | Brandweer | Brandstichting, branduitbreiding, blussen en brandgevaar per gebouw | M4 | L |
| E10-03 | Politie en criminaliteit | Criminaliteitswaarde per wijk, patrouilles, politiebureau en gevangenis | M4 | L |
| E10-04 | Gezondheidszorg | Kliniek, ziekenhuis en ambulance; zieken worden opgehaald en behandeld | M4 | L |
| E10-05 | Overlijden | Lijkwagen, begraafplaats en crematorium; opstapeling geeft ongenoegen | M4 | M |
| E10-06 | Onderwijs | Basisschool, middelbare school en universiteit met capaciteit en bereik | M4 | L |
| E10-07 | Parken en recreatie | Parken, speeltuinen, sportvelden en pleinen met landwaarde- en geluksbonus | M4 | M |
| E10-08 | Budgetknoppen per dienst | Budgetschuifregel van 50 tot 150 procent met effect op capaciteit en kosten | M4 | M |
| E10-09 | Dekkingsoverlays | Per dienst een kaart met dekking en onbediende gebouwen | M4 | M |
| E10-10 | Cultuur | Bibliotheek, theater, museum en bezienswaardigheden | M5 | M |
| E10-11 | Helikopters en speciale voertuigen | Politie-, brand- en ambulancehelikopter voor lange afstanden | M8 | M |
| E10-12 | Evenementen | Stadions, concertzalen en markten plannen evenementen met datum en bezoekersaantal; bezoekers komen uit de stad en van buiten, en geven geld uit | M5 | L |

## E11 Economie en begroting

Een eenvoudig geldmodel is al nodig in M3; de goederen- en handelsketens komen pas in M4 en groeien door tot M5.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E11-01 | Geldmodel | Saldo, inkomsten en uitgaven per spelweek, boekhouding per categorie | M3 | M |
| E11-02 | Belastingen | Tarief per zone en dichtheid; effect op vraag, migratie en geluk | M3 | L |
| E11-03 | Bouw- en onderhoudskosten | Elk object heeft aanlegkosten en onderhoud; sloop geeft gedeeltelijke terugbetaling | M3 | M |
| E11-04 | Begrotingspaneel | Overzicht per dienst met schuifregelaars en weekprognose | M3 | M |
| E11-05 | Moeilijkheidsinstellingen | Startgeld, kostenfactor, onbeperkt geld en alles-ontgrendeld | M3 | S |
| E11-06 | Leningen | Leningen met rente, limiet en aflossing; faillissement of cheat-modus kiesbaar | M4 | M |
| E11-07 | Bedrijven en productie | Bedrijven hebben vraag, productie, winst en lonen; sluiten bij structureel verlies | M4 | XL |
| E11-08 | Goederenketen | Grondstof naar industrie naar handel; tekorten geven lege winkels en minder inkomsten | M4 | XL |
| E11-09 | Overige inkomsten | Toerisme, tickets, unieke gebouwen, import en export | M5 | M |
| E11-10 | Inkomens en huisvesting | Inkomensklassen en huurprijs bepalen welke woningen waar groeien | M5 | M |
| E11-11 | Financiele grafieken | Inkomsten en uitgaven in de tijd per categorie | M6 | M |
| E11-12 | Conjunctuur | Optionele economische cycli met opbloei en recessie | M8 | M |

## E12 Landwaarde, milieu en vervuiling

Alle milieulagen zijn celgebaseerde kaarten met diffusie; bouw eerst een gedeeld diffusiesysteem en hang daar de lagen aan.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E12-01 | Diffusiesysteem | Generieke Burst-job die waarden over cellen verspreidt en laat vervallen, met wind-input | M4 | L |
| E12-02 | Landwaarde | Waarde per cel uit diensten, parken, water, OV en vervuiling; bepaalt gebouwniveau en huur | M4 | L |
| E12-03 | Luchtvervuiling | Bronnen: industrie, verkeer, kolencentrales; effect op gezondheid en landwaarde | M4 | L |
| E12-04 | Geluidsvervuiling | Bronnen: wegen, industrie, luchthaven; geluidswanden en afstand verminderen het effect | M4 | L |
| E12-05 | Grondvervuiling | Industrie en stortplaatsen vervuilen grond; sanering kost geld en tijd | M4 | M |
| E12-06 | Watervervuiling | Riooluitlaat en industrie vervuilen water; effect stroomafwaarts op waterinname en visserij | M4 | L |
| E12-07 | Wind | Windrichting en -sterkte als wereldwaarde, wisselend in tijd | M4 | M |
| E12-08 | Effecten | Vervuiling beinvloedt ziekte, geluk, landwaarde en leegstand | M4 | M |
| E12-09 | Bossen en natuur | Bossen verminderen vervuiling in hun omgeving | M5 | S |
| E12-10 | Uitstoot en klimaat | Totale CO2-uitstoot met optionele klimaateffecten | M8 | M |

## E13 Districten en beleid

Districten en beleid geven je sturing zonder nieuwe gebouwen; hou beleid volledig data-gedreven zodat een nieuwe regel geen code kost.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E13-01 | District-tool | Penseel op de kaart, naam en kleur per district; geen district is de hele stad | M5 | L |
| E13-02 | Districtstatistieken | Bevolking, banen, vervuiling en inkomsten per district | M5 | M |
| E13-03 | Beleidssysteem | Beleid heeft naam, kosten, effecten en geldt stadsbreed of per district; opslaan en laden werkt | M5 | L |
| E13-04 | Beleidslijst stad | Minstens 15 stadsbrede regels, zoals gratis OV, recycling, energiebesparing, waterbesparing, vrachtverbod, schoolbonus | M5 | L |
| E13-05 | Beleidslijst district | Minstens 15 districtsregels, zoals hoogbouwverbod, kleine bedrijven, nachtleven, industrie-specialisatie, gratis ziekenzorg | M5 | L |
| E13-06 | Specialisatiekoppeling | Districtsbeleid schakelt zonespecialisaties (E05-12) en industrietypes (E14-02) in | M5 | M |
| E13-07 | Beleidseffect-feedback | Tooltip toont wat elk beleid kost en oplevert, met dagelijkse bijdrage | M5 | S |

## E14 Industrie, handel, buitenverbindingen en toerisme

Deze epic koppelt je stad aan de wereld eromheen en geeft de industrie een echte rol; hij hangt sterk af van E08 en E09.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E14-01 | Delfstoffen en landbouw | Gebouwen op resource-lagen winnen olie, erts, hout en gewassen; uitputting optioneel | M5 | L |
| E14-02 | Gespecialiseerde industrie | Landbouw, bosbouw, erts en olie als apart industrietype met eigen gebouwen en productie | M5 | L |
| E14-03 | Verwerking en eindproducten | Verwerkende industrie maakt goederen uit grondstoffen; levering aan winkels | M5 | L |
| E14-04 | Buitenverbindingen | Snelweg, spoor, haven en luchthaven aan de kaartrand; verkeer, import en export lopen erdoor | M5 | L |
| E14-05 | Import en export | Goederen, stroom, water en afval kunnen worden gekocht en verkocht met prijs per eenheid | M5 | M |
| E14-06 | Havens en vrachtterminals | Overslag van schip en trein naar vrachtwagen en andersom met capaciteit | M5 | L |
| E14-07 | Toerisme | Bezoekers komen via buitenverbindingen, verblijven in hotels, bezoeken attracties en geven geld uit | M5 | L |
| E14-08 | Toeristische gebouwen | Attracties, hotels, stranden en wandelroutes met aantrekkingskracht | M5 | M |
| E14-09 | Handelsvoorkeuren | Prijzen en vraag van buiten wisselen; tegenvaller bij sluiten van een verbinding | M5 | M |

## E15 Rampen

Rampen zijn los van de kern uitschakelbaar; bouw eerst het gedeelde framework en voeg daarna elk type afzonderlijk toe.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E15-01 | Ramp-framework | Trigger (willekeurig of handmatig), waarschuwing, effect, nasleep en kosten via een gedeeld contract | M5 | L |
| E15-02 | Brandrampen | Grote branden in gebouwen en bossen die zich verspreiden met wind en dichtheid | M5 | L |
| E15-03 | Aardbeving | Schade of instorting afhankelijk van bouwvoorschrift en niveau | M5 | L |
| E15-04 | Tornado | Bewegende windbuis met schade langs een pad | M5 | L |
| E15-05 | Meteorietinslag | Impactkrater, brand en schade in straal | M5 | M |
| E15-06 | Tsunami en overstroming | Waterstijging volgt terrein; schade tot hoogtegrens | M5 | L |
| E15-07 | Sinkhole | Instortende grond met verdwijnende gebouwen en wegen | M5 | M |
| E15-08 | Storm en bliksem | Storm met stroomuitval, bliksem met branden | M5 | M |
| E15-09 | Waarschuwing | Sirenes, vroegwaarschuwingscentrum en radar geven tijd om te reageren | M5 | M |
| E15-10 | Evacuatie | Schuilplaatsen en evacuatiebussen; burgers volgen routes naar veilige zones | M5 | L |
| E15-11 | Hulpverlening | Reddingsteams, noodopvang en helikopters voor nasleep | M5 | L |
| E15-12 | Bouwvoorschriften en dijken | Aardbevingsbestendig bouwen en overstromingsdijken verminderen schade | M5 | M |
| E15-13 | Rampinstellingen | Aan of uit, frequentie en schadefactor per ramptype | M5 | S |
| E15-14 | Hitte en droogte | Extra rampen met effect op water, brand en energie | M8 | M |

## E16 Progressie: mijlpalen en ontgrendelingen

Progressie geeft het spel een gestage opbouw, maar omdat het voor jezelf is, krijgt een sandbox-optie dezelfde prioriteit als de campagne.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E16-01 | Bevolkingsmijlpalen | Ongeveer 12 mijlpalen met bevolkingsdrempels, beloning en melding | M5 | M |
| E16-02 | Ontgrendelsysteem | Gebouwen, tools en beleid ontgrendelen per mijlpaal; data-gedreven | M5 | M |
| E16-03 | Alles ontgrendeld | Instelling om alle mijlpalen en gebouwen direct beschikbaar te maken | M5 | S |
| E16-04 | Unieke gebouwen en monumenten | Ontgrendelen via voorwaarden (bevolking, diensten, geluk) met bonus voor de stad | M5 | L |
| E16-05 | Mijlpaalmelding | Duidelijke melding met uitleg wat nieuw beschikbaar is | M5 | S |
| E16-06 | Eigen doelen en scenario's | Doelen met voorwaarden en winnen of verliezen, zie E22-07 | M8 | L |

## E17 Info-views en statistieken

De speler stuurt op wat hij ziet; bouw het overlay-framework vroeg, want elk simulatiesysteem heeft een overlay nodig om te debuggen.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E17-01 | Overlay-framework | Kleur per cel, gebouw of segment met legenda en schaal; wisselen zonder hapering | M3 | L |
| E17-02 | Inspectie | Klik op gebouw, weg, burger of voertuig toont status, problemen en cijfers | M3 | L |
| E17-03 | Probleemiconen | Iconen boven gebouwen bij geen stroom, geen water, vol afval, brand en dergelijke | M3 | M |
| E17-04 | Overlays basis | Stroom, water, riool, afval, landwaarde, zones, verkeer | M3 | L |
| E17-05 | Overlays milieu en diensten | Vervuiling (lucht, geluid, grond, water), geluk, gezondheid, onderwijs, criminaliteit, brand, parkeren, wind | M4 | L |
| E17-06 | Overlays economie en wereld | Resources, districten, OV, toerisme, handel | M5 | M |
| E17-07 | Statistiekenpaneel | Bevolking, economie, diensten en verkeer met grafieken in de tijd | M6 | L |
| E17-08 | Historische data | Ringbuffers per statistiek met instelbare resolutie, zonder geheugenlek | M6 | M |
| E17-09 | Meldingen en adviseurs | Stadsmeldingen en hulpberichten met link naar probleemplek | M6 | M |
| E17-10 | Stadsberichten | Optionele berichtenstroom van burgers met korte reacties op gebeurtenissen | M6 | S |
| E17-11 | Flow-visualisatie | Animatie van verkeers- en OV-stromen op segmenten | M6 | M |

## E18 UI/UX

Start met een minimale UI in M2 zodat je kunt spelen, en breid per mijlpaal uit; een vast designsysteem voorkomt dat panelen uit elkaar groeien.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E18-01 | UI-framework en designsysteem | Keuze UI Toolkit of uGUI, themakleuren, lettertypes, standaardknoppen en panelen | M0 | M |
| E18-02 | Bouwfeedback | Kosten-preview, rode ghost bij ongeldig, foutmelding met reden | M1 | M |
| E18-03 | HUD | Geld, bevolking, datum en tijd, snelheidsknoppen en RCI-meter | M2 | M |
| E18-04 | Hoofdtoolbalk | Tabs voor wegen, zones, nutsvoorzieningen, diensten, OV, beleid, districten met subpanelen | M2 | L |
| E18-05 | Menu's | Hoofdmenu, pauzemenu, nieuwe stad, laden en opslaan | M2 | M |
| E18-06 | Budget- en beleidspanelen | Interactieve panelen die zijn gekoppeld aan E11 en E13 | M3 | L |
| E18-07 | Instellingen | Beeld, geluid, besturing en gameplay-opties, opgeslagen per gebruiker | M6 | M |
| E18-08 | Schaalbare UI | Werkt op 1080p tot 4K en ultrawide met DPI-schaal | M6 | S |
| E18-09 | Zoeken en lijsten | Zoek gebouwen op naam of type en spring ernaartoe | M6 | S |
| E18-10 | Helpsysteem | Contextuele hints en korte uitleg per tool | M8 | M |

## E19 Visuals, dag en nacht, weer, seizoenen

Visuals krijgen in M1 een minimale versie (terrein en water) en worden in M6 pas echt uitgewerkt; houd alles instanced en data-gedreven zodat dat laat nog kan.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E19-01 | Art-richting en materialenset | Eén stijl vastgelegd; shaders voor terrein, water, wegen en gebouwen | M1 | L |
| E19-02 | Watershader | Golven, diepte, schuim en reflecties, goedkoop genoeg voor volledige kaart | M1 | L |
| E19-03 | Dag-nachtcyclus | Zon en maan, hemel, schaduwen en lichtkleur; snelheid gekoppeld aan spelklok | M6 | L |
| E19-04 | Weer | Regen, mist, wind en onweer met effect op zicht en optioneel op wegen | M6 | L |
| E19-05 | Gebouw-LOD en imposters | Drie LOD-niveaus en imposters voor verre gebouwen | M6 | L |
| E19-06 | Nachtverlichting | Straatlantaarns, koplampen en ramen lichten op volgens tijd | M6 | M |
| E19-07 | Post-processing | AO, bloom, kleurcorrectie en optionele tilt-shift met instelbare kwaliteit | M6 | M |
| E19-08 | Particle-effecten | Rook, vuur, stoom, stof en vonken met pooling | M6 | M |
| E19-09 | Props en vegetatie | Gras, struiken, rotsen en straatmeubilair als instances op dichtheidskaarten | M6 | M |
| E19-10 | Grafische kwaliteitsprofielen | Laag, middel, hoog en aangepast, wisselbaar in het spel | M6 | M |
| E19-11 | Wolken en wolkenschaduw | Bewegende wolken met zachte schaduw op de stad | M6 | S |
| E19-12 | Seizoenen | Bladkleuren en sneeuwlaag over het jaar, zie ook E25-01 | M8 | L |

## E20 Audio

Audio is klein in omvang maar bepaalt veel van de sfeer; de belangrijkste technische vraag is het beperken van gelijktijdige geluidsbronnen.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E20-01 | Audio-architectuur | Mixer met groepen voor muziek, ambient, effecten en UI; volumes in instellingen | M6 | S |
| E20-02 | Ambient per locatie | Stadsdrukte, natuur, water en wind afhankelijk van camerahoogte en omgeving | M6 | M |
| E20-03 | Voertuig- en treingeluid | Afstandsdemping, pooling en een maximum aantal gelijktijdige bronnen | M6 | M |
| E20-04 | UI-geluiden | Klik, bouw, sloop, fout en melding | M6 | S |
| E20-05 | Muziek | Afspeellijst met overgangen en variatie per dag, nacht of ramp | M6 | S |
| E20-06 | Sirenes en ramp-audio | Hulpdiensten en rampwaarschuwing met richtingsgevoel | M6 | S |
| E20-07 | Voice-budget | Harde limiet aan gelijktijdige 3D-bronnen met prioriteiten | M6 | M |

## E21 Content-pipeline en editors

Content is de grootste verborgen werklast van een stadsbouwer; een sterke pipeline en placeholders houden de simulatie testbaar zonder dat je eerst honderden modellen nodig hebt.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E21-01 | Asset-richtlijnen | Schaal, pivot, footprint, LOD's, materialen en colliders vastgelegd in een document | M2 | S |
| E21-02 | Gebouw-sjabloon | Prefab met grootte in cellen, zone, niveau, capaciteit en props; validatiescript | M2 | M |
| E21-03 | Asset-register | Registry of Addressables waarmee gebouwen, voertuigen en props worden gevonden en geladen | M2 | M |
| E21-04 | Placeholder-set | Lowpoly gebouwen, voertuigen en burgers voor elk type zodat alles testbaar is | M2 | M |
| E21-05 | Voertuig- en burgerpipeline | Modellen met geanimeerde wielen en lopen via GPU-animatie of vertex-baking | M3 | L |
| E21-06 | Procedurele gebouwen | Modulaire gevel-, dak- en hoekdelen die uit een footprint een gebouw bouwen | M7 | XL |
| E21-07 | Kaarteditor | Terrein schilderen, water en resources plaatsen, buitenverbindingen kiezen, opslaan als kaart | M7 | XL |
| E21-08 | Asset-importer | Eigen modellen binnen het spel laden en als gebouw instellen | M7 | XL |
| E21-09 | Data-folder | Gebouwen, beleid en kosten aanpassen via bestanden zonder nieuwe build | M7 | L |

## E22 Opslaan, spelmodi, instellingen

Het save-systeem begint als skelet in M0 en moet bij elk nieuw systeem meegroeien; breng het nooit achteraf aan.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E22-01 | Save/load | Volledige wereldstatus (netwerk, gebouwen, agents, lagen) in versieerd binair formaat met compressie | M0 | XL |
| E22-02 | Nieuwe stad | Kaartkeuze, moeilijkheid en startvoorwaarden | M2 | M |
| E22-03 | Autosave en slots | Autosave op interval, quicksave, meerdere slots met thumbnail | M3 | M |
| E22-04 | Sandbox-modus | Onbeperkt geld en alles ontgrendeld | M3 | S |
| E22-05 | Instellingen opslaan | Opties blijven bewaard tussen sessies | M6 | S |
| E22-06 | Save-migratie | Oude saves laden na schema-wijziging met versienummer en upgradepad | M7 | M |
| E22-07 | Scenario's | Kaarten met doelen, beperkingen en winvoorwaarden, zie E16-06 | M8 | L |
| E22-08 | Timelapse en replay | Opname van de stadsgroei als snelle replay | M8 | M |

## E23 Performance en schaalbaarheid

Stel in M0 budgetten vast en meet elke mijlpaal opnieuw; schaalproblemen in pathfinding en agents zijn pas goedkoop op te lossen als ze vroeg zichtbaar zijn.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E23-01 | Performancebudgetten | Doelen voor framerate, tick-tijd en geheugen per bevolkingsgrootte op papier | M0 | S |
| E23-02 | Benchmark-scenes | Vaste teststeden van 10k, 100k en 500k inwoners met automatische meting | M2 | M |
| E23-03 | Burst- en jobs-audit | Alle simulatiesystemen zonder GC-allocaties in de hot path; Burst-compatibele code | M4 | L |
| E23-04 | Gelaagde simulatie | Verre of onbezochte delen simuleren statistisch, nabije delen volledig | M4 | XL |
| E23-05 | Pathfinding-cache | Incrementele updates en gedeelde routes in plaats van volledige herberekening per agent | M4 | L |
| E23-06 | Instancing en BRG | Gebouwen, props en voertuigen via GPU-instancing of Batch Renderer Group | M6 | L |
| E23-07 | Tegelstreaming | Tegels worden gecull en lui geladen met een vast geheugenbudget | M7 | L |
| E23-08 | Geheugenoptimalisatie | Compacte datastructuren en pooling; geheugenprofielen per systeem | M7 | M |
| E23-09 | Laadtijden | Opslaan en laden van een grote stad binnen het gestelde budget | M7 | M |
| E23-10 | Volledige kaart | Stad over 81 tegels met 1 miljoen inwoners als stretchdoel | M7 | XL |

## E24 Testen en debugtools

Debugtools zijn geen luxe maar een voorwaarde: zonder zicht op lanes, agents en tick-tijden kost elk simulatieprobleem dagen.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E24-01 | Testframework en CI | EditMode- en PlayMode-tests draaien lokaal met een script en bij elke commit | M0 | M |
| E24-02 | Debug-overlays | Lanegraaf, ID's, cellenraster, agent-aantallen en tick-tijden per systeem | M0 | M |
| E24-03 | Console en cheats | Geld, ontgrendelen, tijd versnellen, spawnen en teleporteren via een console (na het geldmodel E11-01 en ontgrendelen E03-09) | M3 | S |
| E24-04 | Sim-inspector | Live data van elk object (burger, voertuig, gebouw) in een paneel | M1 | M |
| E24-05 | Crash- en corruptiecontrole | Logbestanden en checksum bij laden om kapotte saves te herkennen | M3 | S |
| E24-06 | Stresstest-generator | Genereert automatisch een grote stad voor benchmarks en regressietests | M4 | L |
| E24-07 | Telemetrie per systeem | Tick-tijden en aantallen naar een bestand voor trendanalyse | M4 | S |
| E24-08 | Input-recorder | Neemt acties op en speelt ze af om bugs reproduceerbaar te maken | M7 | L |

## E25 Uitbreidingen (alles M8)

Dit zijn uitbreidingsthema's als zelfstandige features; elk is te bouwen zodra de kern uit M0 t/m M7 staat. Multiplayer, workshop en platformintegraties staan bewust buiten scope.

| ID | Item | Klaar als | MS | Est |
| --- | --- | --- | --- | --- |
| E25-01 | Winter | Sneeuwweer, verwarming als nutsvoorziening, sneeuwruimen, wintersport en vorst op wegen | M8 | XL |
| E25-02 | Extra vervoersmodi | Monorail, kabelbaan, luchtschip, taxi en veerboot-routes met knooppunten | M8 | XL |
| E25-03 | Groene stad | Elektrische voertuigen en laadstations, windparken, groen beleid, milieugebouwen | M8 | L |
| E25-04 | Parken en attracties | Dierentuin, pretpark, natuurreservaat en wandelroutes met bezoekers en inkomsten | M8 | L |
| E25-05 | Industrie met opslag | Magazijnen, uitgebreide goederenketens en unieke fabrieken | M8 | XL |
| E25-06 | Campus | Universiteitsfaculteiten, sportteams, studentenleven en onderzoeksbonus | M8 | XL |
| E25-07 | Haven en visserij | Visserij, havenlogistiek en afvalverwerking met meer diepte | M8 | L |
| E25-08 | Luchthavens op maat | Meerdere terminals, cargo en vliegroutes met geluidsbeheer | M8 | XL |
| E25-09 | Pleinen en promenades | Plein-tool en aanpasbare promenades met winkelstraten | M8 | L |
| E25-10 | Financiele districten en hotels | Financieel kantoorcluster, beurs, hotels en conferenties | M8 | L |

## Risico's en aanbevolen bouwvolgorde

De vijf grootste risico's zijn pathfinding op schaal, kruispuntgeometrie, een save-systeem dat te laat komt, de hoeveelheid content en scope creep.

| Risico | Gevolg | Maatregel |
| --- | --- | --- |
| Pathfinding en agents te traag | Stad stokt boven 20.000 inwoners | Spike in E01-02, benchmark in E23-02 vanaf M2, gelaagde simulatie in E23-04 |
| Kruispunten en spline-meshes | Weken verloren aan randgevallen | Begin met 3- en 4-armige kruispunten; breid uit na de eerste speelbare versie |
| Save/load te laat | Elk systeem moet achteraf worden herschreven | Skelet in M0 en elk nieuw systeem levert zijn serialisatie mee (Klaar als) |
| Content-hoeveelheid | Simulatie wacht op modellen | Placeholder-set in E21-04 en procedurele gebouwen in E21-06 |
| Scope creep | Nooit een speelbare versie | Poort per mijlpaal; E25 pas na M7 |

Aanbevolen volgorde binnen de eerste speelbare versie (M0 t/m M4):

1. Project, simulatieklok en datamodel (E01), camera en tools (E02).
2. Terrein en water (E03) en een minimale watershader (E19-01, E19-02).
3. Wegennet met kruispunten (E04) en het save-skelet (E22-01).
4. Zones, gebouwspawner en placeholder-assets (E05, E21-01 t/m E21-04).
5. Stroom, water, riool en afval (E06) en de eerste overlays (E17-01 t/m E17-04).
6. Burgers, banen en migratie (E07), daarna verkeer (E08) en economie (E11).
7. Stadsdiensten, openbaar vervoer en milieu (E10, E09, E12) tot de MVP-poort van M4.
