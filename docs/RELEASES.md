# Releases en auto-update

Bij elke merge naar `main` bouwt de workflow [`release.yml`](../.github/workflows/release.yml) een Windows-versie van het spel en publiceert die als GitHub Release. `CitiesLauncher.exe` controleert bij elke start of er een nieuwere release is en werkt het spel bij.

## Eenmalig instellen: Unity-licentie

De build draait in GitHub Actions met `game-ci/unity-builder` en heeft een Unity-licentie nodig. Zonder deze secrets faalt de workflow met de melding "Unity-licentie ontbreekt" (de SonarQube-check wordt niet geraakt).

Zet drie repository-secrets (Settings > Secrets and variables > Actions > New repository secret):

| Secret | Inhoud |
| --- | --- |
| `UNITY_EMAIL` | E-mailadres van je Unity-account |
| `UNITY_PASSWORD` | Wachtwoord van dat account |
| `UNITY_LICENSE` | De volledige inhoud van het `.ulf`-licentiebestand (Unity Personal), zie hieronder |

### Het .ulf-bestand voor Unity Personal maken (game-ci activation)

1. Ga in GitHub naar Actions en start (workflow_dispatch) of maak tijdelijk een workflow volgens de [game-ci-documentatie, "Personal license" (activation)](https://game.ci/docs/github/activation). Die workflow gebruikt `game-ci/unity-request-activation-file@v2` met `unityVersion: 6000.5.10f1` en uploadt een artifact `Manual Activation File` met een `.alf`-bestand.
2. Download het `.alf`-bestand en ga naar <https://license.unity3d.com/manual>.
3. Upload het `.alf`-bestand, kies Unity Personal en download het ontvangen `.ulf`-bestand.
4. Open het `.ulf` in een teksteditor en plak de hele inhoud in het secret `UNITY_LICENSE`.

Let op: Unity wijzigt het licentieproces nu en dan (voor Unity 6 is er ook een serial-gebaseerde route voor Pro/Plus, met `UNITY_SERIAL`). Als de activatie met `.ulf` niet slaagt, volg dan de actuele stappen op <https://game.ci/docs/github/activation> en pas de `env`-regels in `release.yml` aan.

## Wat de workflow doet

1. Job `launcher`: draait de launcher-tests (`tools/Launcher`) en publiceert `CitiesLauncher.exe` (win-x64, self-contained, een enkel bestand).
2. Job `build`: controleert de secrets, bouwt met `Cities.Editor.Build.BuildScript.PerformWindowsBuild` (scenes uit Build Settings, versie via `-buildVersion`), zipt het resultaat en maakt de release.

Versie: `0.<run_number>` van de workflow, tag `v0.<run_number>`. Het getal loopt per run op, dus de launcher kan versies numeriek vergelijken (0.10 is nieuwer dan 0.9). De releasenotes zijn het commitbericht van de merge. De Library-map wordt gecachet om bouwen te versnellen. Je kunt een release ook handmatig starten via Actions > Release > Run workflow.

De zip `CitiesGame-<versie>-win64.zip` bevat:

```
CitiesLauncher.exe     start-exe: update en start het spel
Game/Cities.exe        het spel (Unity-player)
Game/version.txt       geinstalleerde versie
Game/...               overige spelbestanden
```

## De launcher

Pak de zip uit en start `CitiesLauncher.exe`. Bij elke start:

1. Leest `Game/version.txt`.
2. Vraagt `https://api.github.com/repos/SvenvanDalen/CitiesGame/releases/latest` op (publiek, zonder token). Offline, rate limit (403/429) of een onverwacht antwoord: het spel start gewoon.
3. Is de release nieuwer (numerieke vergelijking), dan downloadt hij de zip met voortgang en controleert grootte en SHA-256 (de `digest` die GitHub bij de asset meldt).
4. Pakt uit naar een tijdelijke map (met controle op paden buiten de doelmap) en vervangt de bestanden per stuk via een tijdelijke kopie met rename; bij een fout wordt alles teruggezet.
5. Start `Game/Cities.exe`.

Veilig voor gebruikersdata: savegames en instellingen staan in `persistentDataPath` (`%USERPROFILE%\AppData\LocalLow\...`), buiten de installatiemap. Bestanden in de installatiemap die niet in de zip zitten blijven staan; alleen bestanden uit het manifest van de vorige installatie (`installed-files.txt`) die in de nieuwe zip ontbreken, worden verwijderd.

Zelf-update van de launcher: een draaiende exe is op Windows wel te hernoemen maar niet te overschrijven. Zit er een nieuwe `CitiesLauncher.exe` in de zip, dan wordt de draaiende hernoemd naar `CitiesLauncher.exe.old` en de nieuwe op zijn plek gezet; `.old` wordt bij de volgende start opgeruimd. Lukt dat niet, dan wacht een `.new`-bestand dat de volgende start wordt geactiveerd.

## Lokaal testen

```
dotnet test tools/Launcher/CitiesLauncher.slnx
```

De launcher-logica (versies, release-JSON, updatebesluit, uitpakken, installeren) staat in `tools/Launcher/CitiesLauncher.Core` met NUnit-tests in `CitiesLauncher.Tests`; `CitiesLauncher/Program.cs` is een dunne start. De tests draaien in CI via `launcher.yml` en `release.yml`. Het project hoort bij `tools/**` en telt dus niet mee in de Sonar-dekking van het spel.

## Niet end-to-end getest

De Unity-build en de release-stap kunnen alleen draaien met de licentie-secrets. Controleer na het instellen met een handmatige run (Actions > Release > Run workflow) en start daarna `CitiesLauncher.exe` uit de gedownloade zip.
