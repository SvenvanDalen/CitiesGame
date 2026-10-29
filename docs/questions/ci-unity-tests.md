# Vraag: Unity-tests en dekking in CI

**Blokkeert:** E01-04 en alle latere items die `NativeArray`, Burst-jobs, `ProfilerMarker` of andere Unity-runtime-API's gebruiken. Hoort inhoudelijk bij E24-01 ("tests draaien … bij elke commit").

## Probleem

De SonarCloud-quality-gate eist minstens 80% dekking op nieuwe code. CI draait nu zonder Unity: de EditMode-tests lopen met `dotnet test`, wat werkt voor gewone C# (zoals de simulatieklok). Maar `NativeArray` en andere Unity-containers hebben de native Unity-runtime nodig. Buiten Unity faalt al `new NativeArray<int>(10, Allocator.Persistent)` met `TypeInitializationException`. Het wereld-datamodel (E01-04) en vrijwel alle simulatiecode krijgen in de huidige CI dus 0% dekking, en de gate blijft rood.

Eerder liep Unity in CI vast omdat de handmatig geactiveerde Personal-licentie (`.ulf`) aan deze pc gebonden is.

## Opties

**A. Unity in CI met accountgegevens (aanbevolen)**
GameCI activeert een Personal-licentie in de container met e-mail en wachtwoord van je Unity-account. CI draait dan de echte EditMode- en PlayMode-tests in Unity, met het Code Coverage-package, en geeft die dekking aan SonarCloud. Daarmee is ook E24-01 af.
- Nodig: GitHub-secrets `UNITY_EMAIL` en `UNITY_PASSWORD`. Het bestaande `UNITY_LICENSE` kan blijven staan. Tweestapsverificatie op het Unity-account werkt hier niet mee; mogelijk is een apart Unity-account zonder 2FA nodig.
- Nadeel: elke run duurt een paar minuten langer.

**B. CI blijft zonder Unity; native code telt niet mee voor dekking**
Klassen die alleen in Unity draaien (NativeArray-opslag, jobs) komen in vaste mappen die van de dekking zijn uitgesloten. Ze worden alleen lokaal in Unity getest. CI meet dan de dekking van de overige logica.
- Nadeel: een groot deel van de simulatie heeft geen gemeten dekking, en of de tests slagen controleert CI dan niet.

**C. De dekkingsvoorwaarde in SonarCloud verlagen of weghalen**
Kan alleen jij doen, in de quality gate van het project.

## Wat ik nu doe

Tot er een antwoord is, sla ik items over die Unity-runtime-API's nodig hebben, en ga ik verder met items die zonder kunnen: E01-06 command-bus, camera-logica, performancebudgetten.

Antwoord graag in een reactie op deze PR, of zet bij optie A de twee secrets en laat het weten. Deze PR wordt daarna gesloten zonder merge.
