# UI-referenties — originele ontwerpen uit de ontwerpchat

De twee belangrijkste UI-referenties voor **Conductor Chaos** zijn de oorspronkelijke concepten uit de chat. Bewaar ze hier als **originele PNG's** en gebruik ze tijdens ontwerp en implementatie. Herteken ze niet stilzwijgend met een nieuwe image-generator; de user koos juist deze twee richtingen.

| Prioriteit | Bestandsnaam | Waarvoor gebruiken |
| --- | --- | --- |
| **1 — UI-stijlgids** | `01-ui-style-guide.png` | Knoppen, states, tabbladen, iconen, menu-onderdelen, matte materialen en kleurverhoudingen |
| **3 — hoofdmenu/lobby** | `03-main-menu-lobby.png` | Compositie van startscherm, achtergrond met treinwereld, lobbykaarten, routekaart en settings |
| 2 — uitgebreide gameplay-HUD | *niet geselecteerd als hoofdreferentie* | Hooguit voor losse ideeën; **niet** de volle lay-out kopiëren |

## Status van de afbeeldingen

**Beide originele PNG's staan nu daadwerkelijk in de repository.** Ze zijn de leidende UI-ontwerpen voor nieuwe agents. De losse iconen uit de ontwerpen staan in [Assets/Art/UI/Icons](../../../Assets/Art/UI/Icons/) en zijn in de Unity-menu's gekoppeld via [ChaosUISpriteCatalog](../../../Assets/Scripts/UI/ChaosUISpriteCatalog.cs). De 20 samengestelde knop-PNG's blijven referenties.

Open de oorspronkelijke beelden rechtstreeks:
- [UI Style Guide — afbeelding 1](01-ui-style-guide.png)
- [Main Menu / Lobby — afbeelding 3](03-main-menu-lobby.png)

## Regels voor toekomstige agents

1. Lees [UI_STYLE_GUIDE.md](../../UI_STYLE_GUIDE.md) en bekijk **beide echte PNG's** voordat je menu, HUD, iconen of knoppen visueel wijzigt.
2. Behoud de matte, speelse low-poly-cartoonstijl. Geen neon/glow, glasachtige UI, overdaad aan randen of willekeurige AI-UI-elementen.
3. Zie de tekst, fictieve spelers, routes en stats op de afbeeldingen als **conceptinhoud**. Implementeer geen nep-lobby of nep-spelstatus omdat die in de mockup staat.
4. Houd de HUD tijdens gameplay juist *beperkt*: de treinwereld moet zichtbaar blijven. Gebruik beeld 1 voor componenten, beeld 3 voor menu/lobby.
5. Hergebruik `Assets/Scripts/UI/` en het bestaande [palet](../palette.json). Voeg geen parallel UI-framework toe.
6. Als een conceptdetail technisch moeilijk of onduidelijk is, maak eerst één kleine variant en leg een concrete A/B-keuze voor. Geen hele UI-refactor zonder afstemming.
7. Gebruik de [schriftelijke UI-richtlijnen](../../UI_STYLE_GUIDE.md) naast deze beelden. Referenties tonen soms fictieve spelers/routes; koppel alleen echte gameplaydata.

De [algemene game-artposter](../conductor-chaos-concept.png) blijft aanvullend leidend voor personages, de treinwereld en sfeer. Gebruik die niet als vervanging van de specifieke UI-schermen.
