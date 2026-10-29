# CitiesGame

Een stadsbouwgame. Dit is het begin van de repository.

## Structuur

- `Cities/`: het Unity-project (Unity 6000.5 LTS, URP). Open deze map in Unity Hub.
- `docs/BACKLOG.md`: backlog met epics en mijlpalen; elk item staat ook als GitHub-issue.
- `docs/CONVENTIONS.md`: mappen, assemblies, naamgeving en werkwijze.
- `ci/sonar/`: projecten waarmee CI de scripts compileert voor de SonarQube-analyse.

Binaire assets (modellen, texturen, audio) gaan via Git LFS, zie `.gitattributes`. Draai na het clonen eenmalig `git lfs install`.
