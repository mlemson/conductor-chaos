# Conductor Chaos — UI design system v1

**Status:** eerste implementeerbare UI-basis (niet de definitieve game-interface). De gekozen **originele** UI-mockups uit de ontwerpchat zijn [afbeelding 1 — UI Style Guide](References/UI/01-ui-style-guide.png) en [afbeelding 3 — Main Menu + Multiplayer Lobby](References/UI/03-main-menu-lobby.png). Zie de [referentie-index](References/UI/README.md) voor de actuele uploadstatus: de twee PNG's worden via issue #127 toegevoegd. De drukke full-screen HUD uit afbeelding 2 is *niet* leidend. Voor de algemene gamewereld blijven [de conceptposter](References/conductor-chaos-concept.png) en het [palet](References/palette.json) aanvullende bronnen.

## Uitstraling

**Game UI, geen AI-poster:** rustig en compact; matte, warme, eenvoudige oppervlakken. Leesbare labels en een paar illustratieve details, maar geen verloopglans, glow, gouden omlijning om alles heen, transparante sci-fi glasplaten of overdreven 3D-knoppen. Een zichtbare vlakke rand en weinig schaduw is genoeg. De wereld moet meer aandacht krijgen dan de HUD.

**Referentieprioriteit**
1. [Afbeelding **1**](References/UI/01-ui-style-guide.png) bepaalt componenten, knoppen, iconen, kleuren en hiërarchie.
2. [Afbeelding **3**](References/UI/03-main-menu-lobby.png) bepaalt menucompositie, lobbykaarten, routes en instellingen.
3. De oorspronkelijke conceptposter bepaalt de algemene warme cartoon-art direction.
4. Afbeelding **2** toont mogelijke informatie maar is te vol om als standaard HUD te kopiëren.

## Kleuren

Gebruik [palette.json](References/palette.json) als bron; de UI-kleuren hieronder zijn een **subset van de bestaande ramps**, geen nieuw los kleurensysteem:

| Token | Kleur | Gebruik |
| --- | --- | --- |
| Ink | `#351B13` | Tekst op lichte knoppen |
| Paper | `#F4DCC0` | Labels, secundaire knoppen, doelkaartjes |
| Muted | `#DAC3B2` | Secundaire tekst |
| Charcoal | `#302629` | Menuachtergrond, diepe panelen |
| Slate | `#403E4B` | Neutrale interfacepanelen |
| Navy | `#34335B` | Kleine donkere accenten |
| Train red | `#C54D3E` | Gevaarlijke/negatieve actie en waarschuwingen |
| Red dark | `#8D3432` | Randen en onderliggende vlakken |
| Gold | `#F6BC33` | Eén primaire actie tegelijk en gerichte interactieve hints |
| Warm light | `#FEECA1` | Klein licht-/state-accent |
| Green | `#8B9F3B` | Geslaagde/positieve status |

## Typografie en geometrie

- Grote menutitel **28–36 px**, hoofdacties **20–24 px**, normale UI **16–20 px**, metadata **14–16 px** op een 1600×900 referentiecanvas. Schaal via CanvasScaler; geen microscopische informatie.
- Koppen mogen ronder en speelser zijn; bodytekst is neutraal. Nooit het decoratieve posterlettertype in alle teksten forceren.
- Kleurvlakken vlak en mat, hoekradius meestal 8–14 px; normale border 1–2 px en vrijwel geen zware schaduw.
- Knoppen voldoende groot (minstens ca. 44 px hoog bij normale resolutie), hover/pressed/disabled duidelijk, acties niet op kleur alleen herkennen.
- Iconen zijn **eenvoudige vaste symbolen** voor sleutel, deur, ticket, gereedschap, trein, speler en waarschuwing. Eén consistente set; bij gebrek aan assets eerst tekstlabels gebruiken. Geen willekeurige emoji- of 3D-iconmix.
- UI moet leesbaar zijn tegen warme én koelblauwe landschappen. Gebruik gedempte donkere panelen achter HUD-tekst als het contrast tekortschiet.

## Componenten

### Primaire knop
Goud met donkere tekst. Alleen de hoofdactie van het huidige scherm (bijv. lokaal testen, starten als het netwerk klaar is). Geen pulserende halo.

### Secundaire knop en tab
Crème met donkerbruine tekst. Actieve tab kan een korte rode onderrand hebben. Wees duidelijk over hover/disabled.

### Negatieve actie
Mat treinrood met lichte tekst, alleen voor daadwerkelijk negatieve acties (verwijderen, verlaten, stoppen). Waarschuwingen zijn niet altijd knoppen.

### Lobbykaart
Donker vlak, een duidelijke gebruikersnaam of lege slotstatus, klein avatarvlak, en een werkelijke ready-status pas nadat deze uit de sessiedata wordt gelezen. Geen fictieve online spelers. Scheid online lobby van lokale test.

### Routekaart
Beperkte grote afbeelding/miniatuur, naam, moeilijkheid en routekeuze. Alleen tonen als die route werkelijk selecteerbaar is. Een UI-proef mag voorbeeldroutes tonen als expliciet **PREVIEW ONLY**.

### Settings
Rijlabels links, slider/toggle rechts, tabs voor Audio / Controls / Accessibility zodra bijbehorende instellingen werken. Verberg onafgemaakte opties of markeer ze duidelijk als later.

### HUD
Alleen informatie die tijdens die handeling relevant is:
- Links boven: 1–3 concrete objectives, inklapbaar of contextueel.
- Rechts boven: maximaal één belangrijke status/waarschuwing tegelijk; nog geen brede meters voor systemen die niet bestaan.
- Midden onder: één contextuele interactieprompt bij focus/doel, niet drie permanente acties.
- Onderaan bij behoefte: één klein voorwerp-slot en actuele bediening.
- Multiplayerstatus compact en alleen werkelijk verbonden spelers; geen fictieve harten of schijn-healthbars.

## Game/technische regels

- Houd menu-presentatie, HUD-presentatie en spelstate gescheiden. De UI **leest** gevalideerde status maar beslist niet over trein-, passagiers-, health- of netwerkregels.
- Server beslist over gedeelde toestand. Een knop in een menu is niet hetzelfde als een gereed Netcode-lobby.
- Richt de UI schaalbaar in (1600×900 referentie; test minstens 1280×720 en 1920×1080). Pas op met ultrabreed en kleine laptops.
- Interacties moeten via het Input System werken en cursorlock van de physics-controller niet verstoren. Esc/cancel en controllerbediening worden verder uitgewerkt zodra de definitieve navigatieflow vastligt.
- Een waarschuwing mag vrolijk eruitzien, maar moet met contrast en tekst duidelijk blijven voor kleurblindheid.
- **Geen verzonnen gameplaystatus:** toon geen snelheid, kolenpercentage, keteldruk, integrity, tickets of klaarstaande vrienden voordat de echte systemen bestaan.
- Geen externe betaalde assets of fontbestanden zonder expliciete licentie/goedkeuring.

## Wat is nu al in de code?

- [ChaosUI.cs](../Assets/Scripts/UI/ChaosUI.cs): negen-sliced afgeronde matte panelen, knoppen, labels, slider, gedeeld palet en Input System UI-module.
- [ChaosMenuPreview.cs](../Assets/Scripts/UI/ChaosMenuPreview.cs): interactief menulayout, niet-functionele lobby duidelijk benoemd, routevoorbeelden, settings die master audio/mute daadwerkelijk aanpassen, knop die de **bestaande lokale physics-proef** start.
- [ChaosTrialHud.cs](../Assets/Scripts/UI/ChaosTrialHud.cs): automatisch zichtbare, **read-only** HUD in TrainPlayground met echte lokale Netcode-status en controls. Blokkeert geen muisklikken.
- [ChaosUIEditor.cs](../Assets/Editor/ChaosUIEditor.cs): maak/open afzonderlijke testscene via `Tools > Conductor Chaos > UI > Create or Open Menu Preview`.

## Wat bewust nog niet klaar is

Geen echte online lobby/uitnodigingen/ready-state, geen game-startflow voor multiplayer, geen crew-avatar-art, nog geen definitieve iconset, route-unlocks of definitieve train-HUD. Er is ook nog geen Unity Editor-import/visuele test uitgevoerd voor deze nieuwe UI-code. Maak ze niet af in deze ene design-PR: plan ze als afzonderlijke issues en sluit alleen wat werkelijk getest is.
