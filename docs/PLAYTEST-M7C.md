# Speelverslag M7c Finetuning (E27-13 en E27-17)

> **Eerlijk vooraf:** deze ronde is gebouwd en gecontroleerd zonder een interactieve Unity-sessie. De logica is gedekt door EditMode-tests
> (`tools/test.ps1 -Suite Fast`, 2436 geslaagd) en de balans is nagelopen met de kale simulatie (`BalanceSimulationTests`). Een speelsessie
> van 30 minuten in de build met screenshots per categorie is **niet** gedaan; die staat onder "Nog te doen met de hand".

## Balans (E27-13)

Normaal, normale snelheid, een beginner die bouwt zodra de vraag het toelaat (zelfde script als `docs/PLAYTEST-30MIN.md`):

| Minuut | Week | Inwoners | Woningcapaciteit | Banen | Saldo | Vraag wonen | Vraag werk |
|---|---|---|---|---|---|---|---|
| 2 | 2 | 84 | 84 | 104 | 980.228 | 0,01 | -1,00 |
| 4 | 4 | 180 | 192 | 104 | 976.556 | -0,52 | 0,00 |
| 7 | 6 | 288 | 300 | 164 | 978.384 | 0,20 | 0,00 |
| 9 | 8 | 408 | 420 | 254 | 988.952 | 0,35 | 0,00 |
| 11 | 10 | 552 | 564 | 328 | 1.014.434 | 0,74 | -1,00 |
| 14 | 12 | 684 | 696 | 418 | 1.068.990 | 0,70 | 0,00 |
| 16 | 14 | 828 | 828 | 478 | 1.144.286 | 0,03 | 0,00 |
| 18 | 16 | 960 | 972 | 568 | 1.229.942 | 0,45 | 0,00 |
| 21 | 18 | 1.104 | 1.104 | 642 | 1.349.226 | 0,07 | 0,00 |
| 23 | 20 | 1.236 | 1.248 | 732 | 1.482.870 | 0,38 | 0,00 |
| 25 | 22 | 1.368 | 1.380 | 822 | 1.638.874 | 0,89 | -1,00 |
| 28 | 24 | 1.524 | 1.536 | 926 | 1.807.952 | 0,91 | -0,50 |

Conclusies: de stad groeit gestaag, het saldo blijft positief en de rustige start (08:00, geen directe druk) werkt. Het startkapitaal
(1.000.000) is voor de eerste halfuur ruimer dan nodig: het saldo stijgt vanaf week 8 alleen maar. Er is **niets aan de getallen veranderd**
zonder echte speelervaring als maatstaf; verlagen van het startkapitaal naar ongeveer 500.000 is de eerste kandidaat zodra er een
speelsessie is.

## Wat M7c heeft veranderd

| Item | Resultaat |
|---|---|
| E27-01 | Werkbalk met categorieën (tabs) in plaats van één rij knoppen |
| E27-02 | Sneltoetsen (R Z U V O I G P K, cijfers 1-9) en tooltips |
| E27-03 | Kosten op de knop, grijs als niet te betalen |
| E27-04 | Ctrl+Z / Ctrl+Y voor zones en wegen |
| E27-05 | Korte rechtsklik annuleert de tool |
| E27-06 | Q/E wisselen het wegtype tijdens het tekenen; snappen, live lengte/hoek/helling/kosten, doortekenen en eenrichting bestonden al |
| E27-07 | Zone-overlay opent vanzelf met de zonetool; "Strook langs weg" (diepte 4) is de standaard; celaantal bij de cursor |
| E27-08 | Bereikcirkel van politie, brandweer, kliniek en school tijdens het plaatsen |
| E27-09 | Kantelen en scrollen omkeren, cameragevoeligheid; sprong naar een plek (`CameraFocus`) |
| E27-10 | Infokaart bij de cursor; klik zet de kaart vast (live) tot klik op leeg terrein of Escape |
| E27-11 | Weeksaldo naast het geld; meldingen met een plek springen er bij een klik naartoe |
| E27-12 | Opties-scherm (beeld, geluid, besturing, gameplay) met Toepassen en Standaard, naast het toetsenscherm |
| E27-16 | F1 toont alle sneltoetsen, hint voor de eerste stap |

## Nog te doen met de hand (openstaande verbeterpunten)

- **E27-17 rooktest:** 30 minuten spelen in een build zonder fouten, met screenshots van elke categorie.
- **E27-14 visueel:** belichting, mist, schaduwafstand, wegtextuur, waterkleur en dakvariatie zijn niet vergeleken met een referentie.
- **E27-15 verkeer:** drukte- en flow-overlay en wachtende reizigers bestaan in het model; afstellen van het rijgedrag en het controleren van
  voertuigen die door elkaar rijden vraagt een draaiende scène.
- **E27-08 modellen:** nutsvoorzieningen, diensten, haltes en industrie zijn nog procedurele blokken; alleen het bereik is nieuw.
- **E27-10:** pictogrammen boven problemen bestaan (`Presentation/Problems`); de infokaart toont gegevens van de bestaande inspectiebronnen
  (gebouw, weg), burgers en voertuigen alleen als hun bron aan `InspectTool` hangt.
- **E27-12:** de volumes per bus (muziek, omgeving, effecten, interface) worden opgeslagen, maar alleen het hoofdvolume is aan Unity gekoppeld
  zolang er geen AudioMixer in de scène hangt. Kwaliteit stelt nu de schaduwafstand in.
- **E27-09:** schermrand-scrollen volgt de optie; dubbelklik-focus op een gebouw is niet aangesloten (de sprongfunctie bestaat).
- **Startkapitaal** verlagen (zie boven) na echt spelen.
- `Main.unity` is niet opnieuw gebouwd na deze commits: de infokaart maakt zichzelf aan (`HoverInfoBootstrap`) en de opties zitten in het bestaande
  instellingenscherm, dus de scène hoefde niet te veranderen.
