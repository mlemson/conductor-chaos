# Agentinstructies — eerst lezen

**Gebruikersvoorkeur:** lever nieuwe, gecontroleerde wijzigingen rechtstreeks als gerichte commits op `main` en push die direct; een PR is daarvoor niet verplicht. Behoud bestaande issues, controles en lokale wijzigingen. Bestaande PR's of uiteenlopende branches niet automatisch samenvoegen en nooit geforceerd pushen. Deze voorkeur gaat vóór de PR-verplichtingen hieronder.

**CRITICAL — MULTIPLAYER FIRST:** Conductor Chaos is co-op first. Shared objects, train, door, NPCs and collision outcomes remain server-authoritative. **A short host+remote check is required when shared network behavior changes and it is feasible**; documentation, local art and local UI do not require a multiplayer/Play-test. Never claim a test was run if it was not. See the [small-task playbook](Docs/AI/SMALL_TASK_PLAYBOOK.md) for lean checks.

## Verplichte werkafspraak: klein, zuinig, geen dubbel werk

- **START HERE:** [small-task playbook](Docs/AI/SMALL_TASK_PLAYBOOK.md) en [kleine issues per thema](Docs/AI/SMALL_TASK_INDEX.md). **Eén issue = één afgebakende feature/asset en één PR**. Een grote issue met `<!-- conductor-chaos-epic -->` is alleen een overzicht, nooit een opdracht om alles tegelijk te bouwen.
- Lees eerst de **huidige code** en open PR's, niet alleen de oudere statusdocumenten. Er bestaat al een `TrainPlayground` met NGO-physics, 3D-assets en UI. **Loopbug #44 is inmiddels afgesloten**: inspecteer die wijziging eerst en bouw geen tweede loopfix zonder nieuwe reproduceerbare bug.
- **Minimale controle per wijziging:** art/docs = import of inspectie; lokale UI = compile/één actie als haalbaar; gedeelde state = één host+remote rooktest waar uitvoerbaar; een lange Play/latency/6-player-test alleen als afzonderlijke QA-issue dat vraagt. Laat de gebruiker gamefeel handmatig testen. Schrijf `NOT RUN — user Play-test` wanneer niet getest.
- **Stel een korte vraag bij wezenlijke keuzes** (controls, feel, onverwachte art-richting, externe diensten/licenties, destructieve wijzigingen, conflicterende agents). Kleine technische keuzes zelf maken en kort rapporteren.
- Voer geen herhaalde volledige Unity imports/Blender-renders uit voor een kleine issue. Rapporteer maximaal vijf regels, zonder grote logs of analyse.

## Verplichte live taakregistratie (alle agents)

- **Begin niet met implementeren zonder een GitHub Issue** voor het werk. Kijk eerst naar het [live ontwikkelbord](Docs/PROJECT_BOARD.md) en [werkafspraken](Docs/DEVELOPMENT_WORKFLOW.md). Maak zo nodig een issue met P0/P1/P2, doel en toetsbare criteria.
- Claim je taak via assignee of een draft PR met `Refs #nummer`; meld de actieve issue in je werkbeschrijving. Andere agents mogen die taak niet stilzwijgend dupliceren.
- Bij betekenisvolle commits/update: werk de issue/PR bij met werkelijke voortgang, relevante tests, resterende TODO's en blockers. Commit ≠ gereed. Een groene compilatie ≠ multiplayer-validatie.
- Maak een gerichte PR en vermeld `Refs #nummer` tijdens werk; gebruik `Closes #nummer` alleen wanneer de volledige issue na review en merge kan worden gesloten.
- **Wijzig `Docs/PROJECT_BOARD.md` niet handmatig.** Het bord is afgeleid van issues/PR's en wordt door GitHub Actions opnieuw gegenereerd na merges en issue-/PR-wijzigingen. Bij workflowproblemen blijft de issue de bron van waarheid.
- Houd [ROADMAP.md](Docs/ROADMAP.md) en [TESTPLAN.md](Docs/TESTPLAN.md) alleen inhoudelijk bij wanneer scope, testbewijs of afhankelijkheden daadwerkelijk wijzigen.

## Veilig committen en synchroniseren

- Controleer vóór wijzigingen en vóór een commit de huidige branch, upstream en volledige Git-status, inclusief staged en niet-gevolgde bestanden. Vergelijk zo nodig met de actuele doelbranch; een lokale branch kan achterlopen.
- Behoud bestaande wijzigingen en staging van de gebruiker. Werk bij voorkeur op een aparte branch of in een geïsoleerde werkkopie en voeg alleen bestanden toe die bij de opdracht horen, inclusief noodzakelijke Unity-meta-bestanden. Gebruik geen ongerichte `git add .` of `git add -A` in een gemengde werkmap.
- Bekijk vóór de commit de volledige staged bestandslijst en diff. Sluit `work/`, `.vscode/`, builds, tijdelijke bestanden, ingebedde repositories en geheimen uit, tenzij de gebruiker die expliciet wil delen. Ignore-regels verwijderen niet wat al staged of gevolgd is.
- Bij onverwachte bestanden, een achterlopende branch of mogelijke conflicten: onderzoek en behoud alle lokale wijzigingen; voer geen destructieve reset, clean, geforceerde push of onveilige pull uit. Maak een veilig, controleerbaar voorstel en vraag alleen om hulp als de volgende stap daadwerkelijk geblokkeerd of onduidelijk is.
- Kan de agent niet committen of pushen, geef dan de gebruiker een concrete, zo beperkt mogelijke opdracht met de juiste projectmap, branch en verwachte uitkomst; controleer na afloop het resultaat. Laat de gebruiker geen algemene add- of commitopdracht uitvoeren zonder eerst de inhoud te controleren.

## Start elke taak

1. Lees **de gekozen kleine issue**, dit document en het [korte playbook](Docs/AI/SMALL_TASK_PLAYBOOK.md); voeg hooguit één of twee echt relevante guides toe. Niet alle documentatie elke keer.
2. Inspecteer echte bestanden en recente PR's; lange historische status/roadmap is mogelijk verouderd.
3. Behoud bestaande inhoud; maak precies wat de gekozen issue vraagt en niet stiekem vijf extra onderdelen.
4. Leg alleen relevante gewijzigde gedrag, tests en eventuele blockers vast; laat ongeteste Play-ervaring aan de gebruiker.

## Art direction en scope

- De gekozen conceptrichting is vastgelegd in [gamevisie](Docs/GAME_VISION.md) en [visuele referenties](Docs/References/README.md). De originele [conceptafbeelding](Docs/References/conductor-chaos-concept.png) staat in Git en is leidend voor kwaliteit en stijl. Gebruik de gemeten kleurramps uit de [stijlkaart](Docs/References/STYLE_PALETTE.svg) / [palette.json](Docs/References/palette.json); verzin geen losse tinten.
- Leid nieuwe assets af van boonvormige cartoonkarakters, zachte ronde low-poly volumes (warm licht, koele blauw-violette schaduw) en simpele regionale reizigerstreinen. De kleine locomotief op het conceptbord is sfeer, geen blauwdruk; UI/menus hebben geen posterlay-out nodig.
- **Geen dubbele vlakken (z-fighting)** in nieuwe of gewijzigde modellen: controleer met `python ArtSource/Tools/zfight.py <glb>` en volg de regel in de [art style guide](Docs/ART_STYLE_GUIDE.md) (sectie G).
- Eerst een tweespeler technische proef en één kleine art-test; geen ongevraagde hele treinlijn, levelgenerator of inhoudelijke uitbreidingen.

## UI-richtlijn (verplicht voor UI-agents)

- Lees [UI Style Guide](Docs/UI_STYLE_GUIDE.md) voordat je menu, lobby, knoppen, HUD, iconen of interactiefeedback maakt.
- **Primair visueel:** de originele [UI Style Guide (beeld 1)](Docs/References/UI/01-ui-style-guide.png) voor componenten/knoppen en [Main Menu & Lobby (beeld 3)](Docs/References/UI/03-main-menu-lobby.png) voor menucompositie. Controleer de [referentie-index en huidige bestandsstatus](Docs/References/UI/README.md). Beide PNG's worden via issue #127 toegevoegd; zolang ze ontbreken, gebruik de tekstgids en doe niet alsof je de afbeelding hebt gezien. De drukke full-HUD van beeld 2 is **niet** leidend. Houd de UI mat, compact, leesbaar, zonder AI-glow/glassy effect.
- Gebruik de bestaande uGUI-bouwstenen in `Assets/Scripts/UI/` en de bestaande kleurbron `Docs/References/palette.json`; maak geen tweede los UI-thema. Houd presentatie lokaal en puur; gedeelde data uit de server/Netcode.
- **Bestaande UI-iconen zijn nu aangesloten:** gebruik de goedgekeurde PNG's via `ChaosUISpriteCatalog` (`Assets/Resources/ChaosUISpriteCatalog.asset`) en `ChaosUI.Action(..., iconKey: "...")`. De 24 PNG's staan onveranderd in `Assets/Art/UI/Icons/`. Geen tweede Sprite/Resources-kopie, geen nieuwe getekende pictogrammen zonder noodzaak en geen gebakken Engelse tekst als live knoplabel. Bij importproblemen is er `Tools > Conductor Chaos > UI > Refresh Approved Icons`.
- Geen schijnfunctionaliteit: de echte Lobby/Relay/route-, coal-, integrity- en ticketsystemen ontbreken nog. Toon ze alleen als gelabelde demo in de menuproef, nooit als echte online of gameplaystatus.
- Bij veranderingen aan de physics-playground: HUD mag het camera-/muis-/gripsysteem niet kapen. Geen dubbele EventSystem of niet-functionerende permanente inputprompts. Test host en remote client waar relevant.
- Documenteer wat in Unity zichtbaar en getest is en wat alleen een conceptcomponent is. Gebruik de aparte UI-preview als art-review, niet als bewijs dat online multiplayer af is.

## Movement en physics-feel (verplicht voor locomotion-/physics-agents)

- Lees de [physics-based movementrichtlijn](Docs/PHYSICS_MOVEMENT.md) bij iedere wijziging aan spelerbeweging, balans, ragdoll, armen/grip, camera of botsingen in een rijdende trein. Referentie: *Gang Beasts* / *Human: Fall Flat* met **directere besturing**; responsiviteit gaat boven chaos.
- Werk verder op de bestaande physics-playground en charactercontroller. **Geen parallel movement-/gripsysteem** bouwen; gebruik de bestaande kleine issues (#28, #36, #50, #51, #56 en relevante gripissues) en voeg alleen ontbrekende afgebakende taken toe.
- Klein contact moet meestal niet tot vallen leiden; grote impacts mogen echte fysieke gevolgen hebben. Trainruimte, serverautoriteit, camera-comfort en beperkte testkosten zijn onderdeel van de definitie van klaar. Richtwaarden in de guide zijn tuningvoorstellen, geen reeds bewezen instellingen.

## Ontwerpregels

- De host/server valideert en beslist over gedeelde toestand, inclusief trein, deur, objecten en events; clients sturen intenties, geen vertrouwde uitkomsten. Lokale UI en cosmetische feedback mogen lokaal zijn.
- **Bij wijzigingen aan gedeelde netwerkstate**: test zo mogelijk exact één relevante interactie op host én remote client. Alleen gelijktijdige verzoeken, late join, disconnect, netwerkstress of zes spelers testen als het issue **die specifieke gedraging** verandert. Meld eerlijk wanneer test ontbreekt.
- **CRITICAL — REAL DERAILMENT:** de trein moet bij bijvoorbeeld kapotte rails, botsingen of te hoge snelheid **echt fysiek kunnen ontsporen**; dit is geen cutscene of animatie-only. Plan rail-guided normal travel zonder crash-onmogelijk te maken en een host/server-autoritatieve overgang naar dynamische physics. Lees [DERAILMENT_PHYSICS.md](Docs/DERAILMENT_PHYSICS.md) voordat je trein, spoor, koppelingen, spelersphysics of botsingen ontwerpt. Voer geen uitgebreide derailmentsimulatie uit tijdens P0-01: eerst P0, dan een afzonderlijke test.
- Kies voor stabiele, uitlegbare physics; definieer wereldruimte versus treinruimte. Gebruik geen aparte simulaties als autoritatieve waarheid. Voorspelling/correctie pas na meting.
- Houd systemen klein, verantwoordelijkheden en afhankelijkheden duidelijk; geen globale gameplay-singletons als verborgen bron van waarheid. Gebruik bestaand idioom nadat implementatie ontstaat.
- Commit Unity `.meta`-bestanden, scenes, settings en manifest/lock waar van toepassing; nooit Library, builds, logs, tokens of servicegeheimen. Wijzig geen GUID's zonder noodzaak.
- Geen features als 'gereed' presenteren zonder relevante tests; vermeld omgeving, rollen, resultaten en beperkingen. Bewaar commerciële assets/licenties buiten Git tenzij distributie expliciet is toegestaan.

## Oplevering per taak

Maximaal vijf korte regels: **gedaan, gerichte controle of NOT RUN, handmatige check voor gebruiker, open blocker, PR/issue**. Alleen bij een wijziging aan gedeelde state één zin over server-authoriteit. Niet bij iedere kleine taak het hele TESTPLAN of ROADMAP herschrijven.
