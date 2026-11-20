# Conductor Chaos

Een warme, speelse co-opgame in ontwikkeling: 1–6 spelers proberen samen een treinreis te voltooien. Singleplayer moet mogelijk blijven. Werknaam; ontwerp en omvang kunnen veranderen.

**Status (20 november 2026).** `main` bevat een depot, een gezamenlijke introductie, vijf dienstritten en een online sessie met Unity Relay. De game is in ontwikkeling. De actuele issues en het testplan geven aan welke onderdelen nog worden uitgewerkt of getest.

## 🚂 Live roadmap en samenwerken

[**Bekijk het ontwikkelbord**](Docs/PROJECT_BOARD.md) · [**Openstaande taken**](https://github.com/mlemson/conductor-chaos/issues) · [**Samenwerkingsregels**](Docs/DEVELOPMENT_WORKFLOW.md)

Het bord toont automatisch *te doen*, *bezig*, *review*, *geblokkeerd* en *klaar* via [de GitHub-workflow](.github/workflows/update-development-board.yml). De daadwerkelijke status komt uit GitHub Issues en gekoppelde PR's; niet uit losse commitberichten.

## Aan de slag

1. Installeer Git LFS en voer `git lfs install` uit voordat je de repository kloont. Voer in een bestaande checkout `git lfs pull` uit als de grote scènes niet zijn opgehaald. Levels 2 t/m 5 staan in Git LFS.
2. Open de hoofdmap van de Git-checkout in Unity Hub met **6000.6.8f1**. Laat Unity de packages importeren en controleer de Console.
3. Open `Assets/Scenes/TrainPlayground.unity` en druk op Play voor het menu. Kies solo of maak een online sessie en deel de Relay-code. Online spelen vereist een gekoppeld Unity Cloud-project met Relay.
4. Maak een Windows-build via **File > Build Profiles**. De standaardscènelijst begint met `TrainPlayground`, gevolgd door de introductie en levels 1 t/m 5. De losse `ChaosMenuPreview` is bedoeld voor UI-ontwikkeling.

De [UI-stijlgids](Docs/UI_STYLE_GUIDE.md) en [UI-testinstructies](Docs/UI_TESTING.md) beschrijven de losse menuproef.

## Begin hier

1. [Agentinstructies](AGENTS.md) en [projectstatus](Docs/AI/UnityProjectContext.md).
2. [Architectuur en netwerkafspraken](Docs/ARCHITECTURE.md), inclusief autoriteit en de bewegende trein.
3. [Eerste technische proef en taakvolgorde](Docs/ROADMAP.md), plus [echte ontsporingen en netwerkphysics](Docs/DERAILMENT_PHYSICS.md) voor toekomstige implementatie.
4. [Multiplayer-testplan](Docs/TESTPLAN.md) en [risicoregister](Docs/RISKS.md).
5. [Gamevisie en ontwerpprioriteiten](Docs/GAME_VISION.md), [art style guide](Docs/ART_STYLE_GUIDE.md), [visuele referenties](Docs/References/README.md) en [assetchecklist](Docs/ASSET_CHECKLIST.md).

## Eerstvolgende agenttaak

**Kies één [kleine uitvoertaak](Docs/AI/SMALL_TASK_INDEX.md)**. Lees [tokenzuinige agentregels](Docs/AI/SMALL_TASK_PLAYBOOK.md), check actuele PR's en inspecteer eerst de bestaande code. De [oude P0-01 prompt](Docs/AI/NEXT_AGENT_TASK.md) is historisch; niet opnieuw de baseline bouwen alleen omdat een oud document dat zegt.

Voor bijdragen: [CONTRIBUTING.md](CONTRIBUTING.md). Houd concrete status en testbewijs actueel; vink geplande functies nooit af omdat alleen documentatie bestaat.
