# Vragen bij de laatste M1-items

Deze items zijn overgeslagen omdat er bij het begin te veel onduidelijk was of omdat ze afhangen van iets dat nog niet bestaat. Graag beantwoorden als comment op de PR; daarna pak ik ze op. Dit bestand verdwijnt met deze tijdelijke PR.

## E04-06 Bruggen en tunnels (#38)

Klaar als: hoogteregeling, pijlers, tunnelmond, kosten; kruisen van water en bergen. Schatting XL, "opsplitsen bij start".

1. **Water bestaat nog niet** (E03-05 wacht op antwoorden in #301). Mag ik bruggen eerst alleen over terrein en dalen bouwen en water later toevoegen, of wacht je liever op E03-05?
2. **Opslag.** Een segment heeft nu alleen een hartlijn en lanes. Voor een brug of tunnel komt er een eigenschap bij (bijvoorbeeld `Elevation { Ground, Bridge, Tunnel }` plus het stuk waarover). Mag dat in de save-sectie `NETW` (versie ophogen), en is het per segment of per stuk van een segment?
3. **Hoogteregeling.** Hoe steil mag een oprit zijn (graden of procent), en moet de weg zelf een brug worden zodra het terrein meer dan een vaste hoogte onder de weg ligt, of kiest de speler dat?
4. **Kosten.** Bruggen en tunnels zijn duurder dan gewone weg, maar de economie (E11) is M3. Toon ik alleen de kosten per meter in de preview, zoals bij E04-05, of wil je dat ze al worden afgeboekt?
5. **Modellen.** Pijlers, brugdek, tunnelmond: procedureel (zoals de wegen) of wil je losse modellen?
6. **Splitsen.** Voorstel: eerst E04-06a (hoogteprofiel en dek op het wegmesh, zonder pijlers), dan E04-06b (pijlers en tunnelmond). Akkoord?

## E19-01 Art-richting en materialenset (#210)

Klaar als: één stijl vastgelegd; shaders voor terrein, water, wegen en gebouwen.

1. **Stijl.** Welke kant op: realistisch (zoals de wegen en bomen nu), of gestileerd? Heb je referenties?
2. **Shaders.** De huidige materialen zijn URP Lit met procedurele texturen. Wat moet een eigen shader meer kunnen (bijvoorbeeld terrein met meerdere lagen en hoogte- of hellingmenging, nat wegdek, gebouwen met ramen die 's nachts oplichten)? Shader Graph of HLSL?
3. **Omvang.** Is "vastgelegd" een korte tekst in `docs/` met paletten en regels, of ook een materiaalbibliotheek in het project?

## E19-02 Watershader (#211)

Klaar als: golven, diepte, schuim en reflecties, goedkoop genoeg voor de volledige kaart.

1. **Hangt af van water.** Er is nog geen water (E03-05). Mag ik de shader los bouwen op een testvlak, of wacht ik tot de wateropzet vaststaat?
2. **Reflecties.** Planar reflections, screen-space of een reflection probe? Wat is het budget (ms per frame op welke hardware)?
3. **Diepte en schuim.** Uit de diepte van het terrein onder het water (hoogtekaart) of uit een aparte laag?

## E24-04 Sim-inspector (#259)

Klaar als: live data van elk object (burger, voertuig, gebouw) in een paneel.

1. **Er zijn nog geen burgers of voertuigen in de simulatie**, alleen definities en geplaatste gebouwen. Wat moet het paneel nu laten zien: alleen gebouwen en wegsegmenten, met een uitbreidbaar contract (`IInspectable`) voor wat later komt?
2. **Selecteren.** Klikken op een object in de wereld (picking) of kiezen uit een lijst?
3. **Waar.** Als overlay in de werkbalk-UI of als apart debugpaneel naast `DebugRegistry`?

## E04-11 Snelweg-knooppunten (#43)

Klaar als: op- en afritten, klaverblad en turbine-opties met voorbeeldpresets.

1. **Wat is een preset?** Een kant-en-klare bundel wegstukken die ik op één punt neerzet (kruising van twee wegen, met alle ramps), of een tool die je een bestaand kruispunt omzet? Hoeveel vrijheid heeft de speler (straal, aantal armen, welke hoek)?
2. **Welke typen precies?** Klaverblad en turbine staan erin; moeten ook ruit (diamant), trompet en de losse op- en afrit (op een bestaande snelweg) erbij? Wat noem jij "voorbeeldpresets": een vaste lijst of ook door de speler op te slaan?
3. **Hoogte.** Klaverblad en turbine hebben een kruisende weg op een andere hoogte nodig, dus bruggen, pijlers of tunnels (E04-06, ook overgeslagen) en verhoogde wegen (E04-10: nu alleen pijlers als data). Wacht ik daarop, of mag ik de ramps eerst plat op de grond leggen met alleen het dek op een vaste hoogte?
4. **Rijrichting en lanes.** Rechts rijden is aangenomen; hoeveel rijstroken hebben de ramps en gebruiken ze de bestaande `Highway`-wegtype of een eigen "ramp"-type met lagere snelheid?
5. **Verkeer en voorrang.** Voegstroken, vlechtvakken en voorrang bij de ramps hangen af van de verkeersregels (E04-13, M3). Ramps nu alleen als geometrie, of ook al met een eerste voegregel?
6. **Splitsen.** Voorstel: eerst de geometrie van een preset (ramps als wegstukken, getest op vorm en aansluiting), daarna de tool en de UI. Akkoord?

## E21-03 Asset-register (#231)

Klaar als: registry of Addressables waarmee gebouwen, voertuigen en props worden gevonden en geladen.

1. **Registry of Addressables?** Addressables staat niet in het project. De bestaande `DefinitionCatalog` vindt al elke definitie op ID en verwijst naar het prefab (gebouwen, centrales). Is dat het register, of wil je echt Addressables (asynchroon laden, bundels, geheugenbeheer)? Addressables voegt een pakket, een groepsindeling en asynchrone aanroepen toe aan alles wat nu een prefab direct gebruikt.
2. **Voertuigen en props.** Er zijn nog geen voertuigmodellen en geen prop-definities (alleen bomen). Moet het register nu ook die typen kennen, of pas als de definities er zijn?
3. **Wat is "laden"?** Alles bij het starten laden, of per gebouw pas als het nodig is (streaming bij zoomen)? Dat bepaalt of een synchrone opzoeklijst genoeg is.

## E21-04 Placeholder-set (#232)

Klaar als: lowpoly gebouwen, voertuigen en burgers voor elk type zodat alles testbaar is.

1. **Lowpoly of realistisch?** Je vroeg dat modellen meteen realistisch zijn, niet demo. Een placeholder-set is per definitie grof. Mag deze set dus gewoon de bestaande gegenereerde gebouwen zijn (20 gebouwen en 3 centrales), of wil je echt een aparte, grovere set die elk type dekt?
2. **Welke typen?** Er zijn nog geen voertuig- of burgerdefinities, en ook geen burgers of voertuigen in de simulatie. Welke voertuigtypes (auto, bus, vrachtwagen, tram, trein, hulpdiensten) en welke burgervarianten (leeftijd, geslacht, kleding) bedoel je met "elk type"?
3. **Hoe maak ik ze?** Procedureel in code (zoals de gebouwen), via Blender of via een generator. Procedureel kan lowpoly voertuigen goed, mensen veel moeilijker; een generator kost credits. Wat heeft de voorkeur?
4. **Wachten op de definities?** Voertuigen horen bij E07 (OV en verkeer) en burgers bij E08. Moeten de modellen daar mee worden gemaakt zodra die definities bestaan?

## E23-02 Benchmark-scenes (#240)

Klaar als: vaste teststeden van 10k, 100k en 500k inwoners met automatische meting.

1. **Wat is een inwoner?** Er zijn nog geen burgers in de simulatie. Tellen inwoners hier als huishoudens uit `BuildingSpec` (huishoudens maal een gemiddelde gezinsgrootte), of wacht dit op echte burgers (E08)?
2. **Hoe komt de stad er?** Een vooraf gegenereerde stad (wegen, zones en gebouwen uit een vaste seed, in code of als opgeslagen bestand) of een scene die tijdens het laden bouwt? 500k inwoners zijn tienduizenden gebouwen; mag het opbouwen tijd kosten, en past het in de huidige kaart?
3. **Wat meten we?** Alleen framettijd en tick-tijd per systeem (bestaande `PerformanceBudgets`), ook geheugen en laadtijd? Welk resultaat geldt als "mislukt": een vast budget of een vergelijking met de vorige run?
4. **Waar draait het?** In Unity (PlayMode, handmatig of met de MCP) of ook in CI? CI heeft geen Unity en geen GPU, dus alleen de simulatie zonder rendering kan daar.
5. **Splitsen.** Voorstel: eerst een generator die een stad van N huishoudens uit een seed bouwt (testbaar), daarna de meetscene.
