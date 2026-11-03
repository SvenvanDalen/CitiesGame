# CitiesGame

Een stadsbouwgame. Dit is het begin van de repository.

## Structuur

- `Cities/`: het Unity-project (Unity 6000.5 LTS, URP). Open deze map in Unity Hub.
- `docs/BACKLOG.md`: backlog met epics en mijlpalen; elk item staat ook als GitHub-issue.
- `docs/CONVENTIONS.md`: mappen, assemblies, naamgeving en werkwijze.
- `docs/decisions/`: architectuurbesluiten; `docs/spikes/`: code van verkennende metingen.
- `ci/sonar/`: projecten waarmee CI de scripts compileert voor de SonarQube-analyse.

Binaire assets (modellen, texturen, audio) gaan via Git LFS, zie `.gitattributes`. Draai na het clonen eenmalig `git lfs install`.

## Claude Code veilig in auto-modus (devcontainer)

`.devcontainer/` zet de code in een container met dotnet 9, git, gh en Claude Code. Het uitgaande netwerk is beperkt tot GitHub, Anthropic, npm, NuGet en SonarCloud (`init-firewall.sh`), zodat Claude daarin zonder bevestigingen kan draaien.

1. Open de map in VS Code en kies "Reopen in Container" (of `devcontainer up --workspace-folder .`).
2. Log eenmalig in met `claude` en `gh auth login`; beide logins blijven bewaard in Docker-volumes.
3. Start `claude --dangerously-skip-permissions` in de container.

Let op: de repo is als bind-mount gekoppeld, dus Claude kan de bestanden daarin wijzigen (git is je vangnet). Unity zelf draait niet in de container; daar blijft `tools/test.ps1` op de host voor, en in de container draait `dotnet test ci/sonar/Cities.Sonar.Tests`.
