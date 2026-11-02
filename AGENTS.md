# Agentinstructies — eerst lezen

**CRITICAL – MULTIPLAYER FIRST:** This is a cooperative multiplayer game, not a singleplayer game with multiplayer added later. Every new gameplay feature, interaction, physics object, animation and train system must be designed and tested for networked play. Never introduce local-only gameplay state for shared interactions. Preserve existing multiplayer functionality when modifying code. Test changes with at least two connected players whenever possible.

## Start elke taak

1. Lees [status](Docs/AI/UnityProjectContext.md), [architectuur](Docs/ARCHITECTURE.md), [roadmap](Docs/ROADMAP.md), relevante guide en recente wijzigingen.
2. Inspecteer de echte projectbestanden, packages en Editor-status; documenten zijn een momentopname, geen bewijs dat een feature bestaat.
3. Behoud bestaande inhoud en los geen andere taken stilzwijgend op. De huidige opdracht is een fundament: voeg geen gameplay, scènes, map of spelassets toe tenzij expliciet gevraagd.
4. Markeer onbekenden, ontbrekende tests en risico's eerlijk. Werk status, taakacceptatie en beslissingen bij wanneer die daadwerkelijk veranderen.

## Ontwerpregels

- De host/server valideert en beslist over gedeelde toestand, inclusief trein, deur, objecten en events; clients sturen intenties, geen vertrouwde uitkomsten. Lokale UI en cosmetische feedback mogen lokaal zijn.
- Test met host én remote client. Een host die iets ziet is geen multiplayerbewijs. Dek gelijktijdige verzoeken, late join, disconnect en opnieuw verbinden af waar relevant; zeg expliciet als een test ontbreekt.
- Kies voor stabiele, uitlegbare physics; definieer wereldruimte versus treinruimte. Gebruik geen aparte simulaties als autoritatieve waarheid. Voorspelling/correctie pas na meting.
- Houd systemen klein, verantwoordelijkheden en afhankelijkheden duidelijk; geen globale gameplay-singletons als verborgen bron van waarheid. Gebruik bestaand idioom nadat implementatie ontstaat.
- Commit Unity `.meta`-bestanden, scenes, settings en manifest/lock waar van toepassing; nooit Library, builds, logs, tokens of servicegeheimen. Wijzig geen GUID's zonder noodzaak.
- Geen features als 'gereed' presenteren zonder relevante tests; vermeld omgeving, rollen, resultaten en beperkingen. Bewaar commerciële assets/licenties buiten Git tenzij distributie expliciet is toegestaan.

## Oplevering per taak

Beschrijf wijziging, autoriteit/state, scenario's die zijn getest, concrete resultaten, resterende risico's en volgende taak. Raadpleeg [testplan](Docs/TESTPLAN.md) en [risico's](Docs/RISKS.md).
