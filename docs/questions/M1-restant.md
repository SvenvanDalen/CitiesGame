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
