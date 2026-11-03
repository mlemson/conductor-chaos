# Conductor Chaos

Een warme, speelse co-opgame in ontwikkeling: 1–6 spelers proberen samen een treinreis te voltooien. Singleplayer moet mogelijk blijven. Werknaam; ontwerp en omvang kunnen veranderen.

**Status:** fundament en documentatie. Er is nog geen gameplay, scène, kaart, netwerkverbinding of speelbare build. De Unity-basis is samengesteld uit de lokaal geïnstalleerde Unity URP-template en moet nog in de editor worden geopend en gevalideerd. Unity 6000.5.7f1 is de beoogde editorversie; package-resolutie en licentie zijn nog niet gecontroleerd.

## 🚂 Live roadmap en samenwerken

[**Bekijk het ontwikkelbord**](Docs/PROJECT_BOARD.md) · [**Openstaande taken**](https://github.com/mlemson/conductor-chaos/issues) · [**Samenwerkingsregels**](Docs/DEVELOPMENT_WORKFLOW.md)

Het bord toont automatisch *te doen*, *bezig*, *review*, *geblokkeerd* en *klaar* zodra [de GitHub-workflow](.github/workflows/update-development-board.yml) in `main` is samengevoegd. De daadwerkelijke status komt uit GitHub Issues en gekoppelde PR's; niet uit losse commitberichten.

## UI en menu proef

[**UI-stijlgids (afbeeldingen 1 en 3)**](Docs/UI_STYLE_GUIDE.md) · [**Zo open je de interactieve Unity-menu-preview**](Docs/UI_TESTING.md)

De runtime HUD in `TrainPlayground` toont alleen echte netwerkproefinformatie. Het losse hoofdmenu/lobby-preview wordt eenmalig in Unity aangemaakt via `Tools > Conductor Chaos > UI > Create or Open Menu Preview`. De lobby is nog geen werkende online sessie; het menu kan de lokale physics-proef wel starten. Niet als afgeronde gameplay-UI presenteren voordat de editorchecks zijn uitgevoerd.

## Begin hier

1. [Agentinstructies](AGENTS.md) en [projectstatus](Docs/AI/UnityProjectContext.md).
2. [Architectuur en netwerkafspraken](Docs/ARCHITECTURE.md), inclusief autoriteit en de bewegende trein.
3. [Eerste technische proef en taakvolgorde](Docs/ROADMAP.md), plus [echte ontsporingen en netwerkphysics](Docs/DERAILMENT_PHYSICS.md) voor toekomstige implementatie.
4. [Multiplayer-testplan](Docs/TESTPLAN.md) en [risicoregister](Docs/RISKS.md).
5. [Gamevisie en ontwerpprioriteiten](Docs/GAME_VISION.md), [art style guide](Docs/ART_STYLE_GUIDE.md), [visuele referenties](Docs/References/README.md) en [assetchecklist](Docs/ASSET_CHECKLIST.md).

## Eerstvolgende agenttaak

Gebruik de afgebakende [P0-01 prompt voor de volgende agent](Docs/AI/NEXT_AGENT_TASK.md). Begin niet met allerlei gameplay voordat de Unity-import is gevalideerd.

## Project openen

Open de hoofdmap in Unity Hub met **6000.5.7f1**, laat packages oplossen en lees Console en Package Manager. Het project is nog niet met de editor gevalideerd; werk [setup-taak P0-01](Docs/ROADMAP.md) af voordat je daarop vertrouwt. Er staan bewust geen scènes in Build Settings. De URP-configuratiebestanden zijn technische instellingen uit de Unity-template, geen spelassets. Netcode, Relay en Lobby zijn nog niet geïnstalleerd of geconfigureerd.

## Eerste doel

Een technische test met twee verbonden spelers in één bewegende wagon, één gedeelde deur en één oppakbare koffer. Beide spelers bedienen de deur, dragen de koffer en botsen met elkaar; de server blijft de enige bron van gedeelde waarheid. Eerst betrouwbaar in solo/host + client, daarna pas uitbreiden naar 4 en uiteindelijk 6 spelers.

Voor bijdragen: [CONTRIBUTING.md](CONTRIBUTING.md). Houd concrete status en testbewijs actueel; vink geplande functies nooit af omdat alleen documentatie bestaat.
