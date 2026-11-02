# Agentinstructies — eerst lezen

**CRITICAL – MULTIPLAYER FIRST:** This is a cooperative multiplayer game, not a singleplayer game with multiplayer added later. Every new gameplay feature, interaction, physics object, animation and train system must be designed and tested for networked play. Never introduce local-only gameplay state for shared interactions. Preserve existing multiplayer functionality when modifying code. Test changes with at least two connected players whenever possible.

## Verplichte live taakregistratie (alle agents)

- **Begin niet met implementeren zonder een GitHub Issue** voor het werk. Kijk eerst naar het [live ontwikkelbord](Docs/PROJECT_BOARD.md) en [werkafspraken](Docs/DEVELOPMENT_WORKFLOW.md). Maak zo nodig een issue met P0/P1/P2, doel en toetsbare criteria.
- Claim je taak via assignee of een draft PR met `Refs #nummer`; meld de actieve issue in je werkbeschrijving. Andere agents mogen die taak niet stilzwijgend dupliceren.
- Bij betekenisvolle commits/update: werk de issue/PR bij met werkelijke voortgang, relevante tests, resterende TODO's en blockers. Commit ≠ gereed. Een groene compilatie ≠ multiplayer-validatie.
- Maak een gerichte PR en vermeld `Refs #nummer` tijdens werk; gebruik `Closes #nummer` alleen wanneer de volledige issue na review en merge kan worden gesloten.
- **Wijzig `Docs/PROJECT_BOARD.md` niet handmatig.** Het bord is afgeleid van issues/PR's en wordt door GitHub Actions opnieuw gegenereerd na merges en issue-/PR-wijzigingen. Bij workflowproblemen blijft de issue de bron van waarheid.
- Houd [ROADMAP.md](Docs/ROADMAP.md) en [TESTPLAN.md](Docs/TESTPLAN.md) alleen inhoudelijk bij wanneer scope, testbewijs of afhankelijkheden daadwerkelijk wijzigen.

## Start elke taak

1. Lees [status](Docs/AI/UnityProjectContext.md), [gamevisie](Docs/GAME_VISION.md), [architectuur](Docs/ARCHITECTURE.md), [roadmap](Docs/ROADMAP.md), relevante guide en recente wijzigingen.
2. Inspecteer de echte projectbestanden, packages en Editor-status; documenten zijn een momentopname, geen bewijs dat een feature bestaat.
3. Behoud bestaande inhoud en los geen andere taken stilzwijgend op. De huidige opdracht is een fundament: voeg geen gameplay, scènes, map of spelassets toe tenzij expliciet gevraagd.
4. Markeer onbekenden, ontbrekende tests en risico's eerlijk. Werk status, taakacceptatie en beslissingen bij wanneer die daadwerkelijk veranderen.

## Art direction en scope

- De gekozen conceptrichting is vastgelegd in [gamevisie](Docs/GAME_VISION.md) en [visuele referenties](Docs/References/README.md). De volledige originele conceptafbeelding is nog niet in Git geüpload; doe niet alsof agents die al kunnen zien. Gebruik de bestaande [stijlkaart](Docs/References/STYLE_PALETTE.svg) en vraag om/gebruik de afbeelding zodra die beschikbaar is.
- Leid nieuwe assets af van ronde cartoonkarakters, matte low-poly volumes en simpele regionale reizigerstreinen. De kleine locomotief op het conceptbord is sfeer, geen blauwdruk; UI/menus hebben geen posterlay-out nodig.
- Eerst een tweespeler technische proef en één kleine art-test; geen ongevraagde hele treinlijn, levelgenerator of inhoudelijke uitbreidingen.

## Ontwerpregels

- De host/server valideert en beslist over gedeelde toestand, inclusief trein, deur, objecten en events; clients sturen intenties, geen vertrouwde uitkomsten. Lokale UI en cosmetische feedback mogen lokaal zijn.
- Test met host én remote client. Een host die iets ziet is geen multiplayerbewijs. Dek gelijktijdige verzoeken, late join, disconnect en opnieuw verbinden af waar relevant; zeg expliciet als een test ontbreekt.
- **CRITICAL — REAL DERAILMENT:** de trein moet bij bijvoorbeeld kapotte rails, botsingen of te hoge snelheid **echt fysiek kunnen ontsporen**; dit is geen cutscene of animatie-only. Plan rail-guided normal travel zonder crash-onmogelijk te maken en een host/server-autoritatieve overgang naar dynamische physics. Lees [DERAILMENT_PHYSICS.md](Docs/DERAILMENT_PHYSICS.md) voordat je trein, spoor, koppelingen, spelersphysics of botsingen ontwerpt. Voer geen uitgebreide derailmentsimulatie uit tijdens P0-01: eerst P0, dan een afzonderlijke test.
- Kies voor stabiele, uitlegbare physics; definieer wereldruimte versus treinruimte. Gebruik geen aparte simulaties als autoritatieve waarheid. Voorspelling/correctie pas na meting.
- Houd systemen klein, verantwoordelijkheden en afhankelijkheden duidelijk; geen globale gameplay-singletons als verborgen bron van waarheid. Gebruik bestaand idioom nadat implementatie ontstaat.
- Commit Unity `.meta`-bestanden, scenes, settings en manifest/lock waar van toepassing; nooit Library, builds, logs, tokens of servicegeheimen. Wijzig geen GUID's zonder noodzaak.
- Geen features als 'gereed' presenteren zonder relevante tests; vermeld omgeving, rollen, resultaten en beperkingen. Bewaar commerciële assets/licenties buiten Git tenzij distributie expliciet is toegestaan.

## Oplevering per taak

Beschrijf wijziging, autoriteit/state, scenario's die zijn getest, concrete resultaten, resterende risico's en volgende taak. Raadpleeg [testplan](Docs/TESTPLAN.md) en [risico's](Docs/RISKS.md).
