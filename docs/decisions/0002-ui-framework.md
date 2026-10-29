# 0002 UI-framework: UI Toolkit met een eigen designsysteem

- **Status:** aangenomen
- **Datum:** 2026-10-29
- **Backlogitem:** E18-01

## Vraag

Bouwt het spel zijn schermen (HUD, werkbalken, panelen, instellingen, statistieken) met UI Toolkit of met uGUI?

## Besluit

Alle schermgebonden UI gebruikt **UI Toolkit** (UXML + USS) met één eigen thema. uGUI wordt niet gebruikt. Labels in de wereld, zoals gebouwnamen en waarschuwingsiconen boven gebouwen, komen in E17/E18 met een eigen instanced renderer en niet met UI-elementen per object.

| Aspect | UI Toolkit | uGUI |
| --- | --- | --- |
| Schaal: lijsten van duizenden regels (statistieken, wachtrijen) | Gevirtualiseerde `ListView`/`MultiColumnListView` | Zelf poolen; veel GameObjects |
| Thema | USS-variabelen en klassen; één wijziging werkt overal door | Prefab-varianten per stijl |
| Layout | Flexbox, schaalt met resolutie via PanelSettings | RectTransform-ankers |
| Tekst | TextCore met SDF-lettertypes | TextMeshPro |
| Data binding | Runtime binding op C#-eigenschappen (Unity 6) | Handmatig |
| Editor-tools | Zelfde technologie als de inspectors in `Cities.Editor` | Anders dan de editor |
| Nadeel | Geen schaduwen in USS; minder voorbeelden voor wereld-UI | — |

## Designsysteem

- **Tokens** (`Assets/UI/Styles/Tokens.uss`): kleuren, ruimte (veelvouden van 4 px), hoeken, lettergroottes en hoogtes. De zonekleuren worden gedeeld met de info-views, zodat een kleur overal hetzelfde betekent.
- **Componenten** (`Assets/UI/Styles/Components.uss`, BEM-namen `c-…`): knoppen (primair, secundair, ghost, gevaar, klein, icoon, geselecteerd), panelen met kop, inhoud en voettekst, werkbalk, badges, meters, tooltip en de huisstijl voor ingebouwde velden (toggle, slider, dropdown, tekstveld).
- **Lettertype**: Inter (SIL Open Font License) in Regular, Medium, SemiBold en Bold, als dynamische SDF-fontassets. De licentie staat naast de bestanden.
- **C#** (`Cities.Presentation.UI`): `CityButton`, `CityPanel` en `CityMeter` als `[UxmlElement]`, bruikbaar in UXML en code. `UiClasses` bevat alle klassennamen als constanten.
- **Thema en schaal**: `CityTheme.tss` op `CityPanelSettings.asset`, met referentieresolutie 1920×1080 en schaling half op breedte, half op hoogte.
- **Voorbeeld**: `Assets/Scenes/Tests/UiShowcase.unity` toont alle componenten.

## Gevolgen

- Nieuwe UI gebruikt de tokens en componenten. Kleuren en maten worden niet in UXML hardgecodeerd, behalve layoutmarges.
- Iconen (werkbalk, meldingen) worden eigen ontwerpen in een latere stap. Knoppen hebben tot dan tekst.
- Bij het opbouwen van UI uit code eindigen alle elementen in een container met `c-root`, zodat lettertype en kleuren gelden.
