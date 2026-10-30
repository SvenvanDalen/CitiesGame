# Vragen bij E24-03 Console en cheats

E24-03 is overgeslagen tot deze vragen beantwoord zijn. Deze PR is tijdelijk en wordt gesloten zodra de antwoorden er zijn.

**Klaar als:** geld, ontgrendelen, tijd versnellen, spawnen en teleporteren via een console.

Van die vijf kan alleen een deel nu echt gebouwd worden:

| Cheat | Bestaat het onderliggende systeem? |
| --- | --- |
| Tijd versnellen | Ja: `SimulationClock`/`SimulationSpeed` (E01) |
| Teleporteren | Ja: `CameraController` (E02-01) |
| Geld | Nee: het geldmodel is E11-01 (M3) |
| Ontgrendelen | Nee: tegels ontgrendelen is E03-09 (M3), alles-ontgrendeld zit in E11-05 (M3) |
| Spawnen | Nee: er zijn nog geen burgers of voertuigen als entiteiten (E04/E07) |

## Vragen

1. **Nu bouwen of uitstellen?**
   - A. Nu de console bouwen (invoerveld, geschiedenis, autocomplete, `help`) met een uitbreidbaar commandoregister en de commando's die nu kunnen: `tijd`, `snelheid`, `pauze`, `teleport`, `overlay`, `opslaan`/`laden`. `geld`, `ontgrendel` en `spawn` komen in E11-01, E03-09 en E04/E07, en die issues krijgen een regel "registreert console-commando" in hun Klaar-als.
   - B. E24-03 verplaatsen naar M3, als het geldmodel en ontgrendelen bestaan.
   - C. Nu ook een voorlopig saldo en een ontgrendel-vlag bouwen, zodat alle vijf werken. Dat loopt vooruit op E11-01 en E03-09 en moet later waarschijnlijk worden omgebouwd.

   Mijn voorkeur: **A**.

2. **Openen van de console:** de `~`/`` ` ``-toets (gangbaar, maar op sommige toetsenborden lastig) of F1? De toets is daarna sowieso te wijzigen in het besturingsscherm.

3. **Cheats in releasebuilds:** alleen in development builds en de editor, of ook in releases achter een instelling (bijvoorbeeld "Cheats toestaan", met een markering in de save)?

🤖 Generated with [Claude Code](https://claude.com/claude-code)
