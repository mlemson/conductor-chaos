# Samenwerken aan Conductor Chaos — levend ontwikkelbord

De **GitHub Issues zijn de bron van waarheid**. Het [visuele ontwikkelbord](PROJECT_BOARD.md) wordt hiervan automatisch afgeleid. **Agents werken uit één [klein issue](AI/SMALL_TASK_INDEX.md) volgens het [zuinige playbook](AI/SMALL_TASK_PLAYBOOK.md)**. Brede epics (gemarkeerd met `<!-- conductor-chaos-epic -->`) dienen alleen voor overzicht en tellen niet mee in het voortgangscijfer. Deze aparte pagina bevat de werkwijze; verander geen automatisch gegenereerde statussen met de hand.

| Waar | Wat vind je? |
| --- | --- |
| [Ontwikkelbord](PROJECT_BOARD.md) | Statuskolommen, aantallen, mijlpalen, eigenaren en PR's |
| [Issues](https://github.com/mlemson/conductor-chaos/issues) | Functionele taken, subwerk, acceptatiecriteria, eigenaarschap |
| [Pull requests](https://github.com/mlemson/conductor-chaos/pulls) | Actief codewerk, reviews en testbewijs |
| [ROADMAP.md](ROADMAP.md) | Technische fasen en onderlinge afhankelijkheden |
| [TESTPLAN.md](TESTPLAN.md) | Verificatie van functies en multiplayer |

## Statussen

- **Te doen:** open issue zonder eigenaar of gekoppelde open PR.
- **Bezig:** iemand is toegewezen, of er bestaat een draft PR met `Refs #nummer`, of label `status:in-progress`.
- **Review:** er bestaat een gekoppelde open PR die geen draft is, of label `status:review`.
- **Geblokkeerd:** label `status:blocked` (heeft voorrang op andere open statussen).
- **Klaar:** issue is gesloten; idealiter doordat de PR met `Closes #nummer` na review in `main` is gemerged.

**Een commit, groene build of gedeeltelijke implementatie mag een taak niet automatisch op Klaar zetten.** Gebruik bij onafgemaakt werk `Refs #nummer`. Gebruik `Closes #nummer` alleen als de PR de volledige acceptatiecriteria afdekt. GitHub sluit gekoppelde issues na merge in de standaardbranch; niet al tijdens het committen.

## Werkafspraken voor mensen én agents

1. **Begin altijd bij een issue.** Maak indien nodig een nieuwe via het formulier *Roadmap task* en geef fase P0/P1/P2, onderdeel, doel en toetsbare acceptatiecriteria.
2. **Reserveer de taak:** wijs iemand toe; als een AI-agent geen assignbare GitHub-identiteit heeft, vermeld dat in de issue en open een draft PR met `Refs #nummer`.
3. **Werk in een kleine branch**, bijvoorbeeld `feature/5-moving-wagon`. Eén PR per samenhangende taak.
4. **Werk de issue/PR bij als er iets wezenlijks verandert:** voortgang en blockers kort vermelden. Test **alleen relevant gewijzigd gedrag**; gedeelde netwerkstate: één gerichte host/remote proef als uitvoerbaar. Statische modellen/docs vereisen geen Play-test. Gebruiker kan feel-testen.
5. **Laat het verschil zien tussen code en oplevering:** rapporteer testomgeving, bewijs, beperkingen en wat nog open staat. De agent verandert de projectstatus niet naar klaar zonder test en review.
6. **Review en merge:** zet een draft PR pas op ready als de criteria zijn afgedekt. Sluit dan via merge of na expliciete beoordeling de issue; het bord werkt zichzelf bij.
7. **Nieuwe taken:** voeg ze toe als nieuwe issues, niet alleen als tekst in een agentantwoord. Dit houdt het werk zichtbaar voor iedereen.

De bestaande startissues zijn gemarkeerd met bijvoorbeeld `<!-- conductor-chaos-roadmap phase=P0 area=Multiplayer -->`; het script herkent ook een fase-prefix in de titel zoals `[P1]` en het GitHub-issueformulier.

## Automatisch bijwerken bij wijzigingen

De [GitHub Actions-workflow](../.github/workflows/update-development-board.yml) voert [dit script](../scripts/generate_development_board.py) uit bij issue-/PR-updates, commits naar `main`, en op verzoek. De workflow gebruikt alleen metadata van GitHub Issues en Pull Requests. Hij bouwt het [ontwikkelbord](PROJECT_BOARD.md) opnieuw en commit **uitsluitend wanneer de inhoud daadwerkelijk is veranderd**. De code wordt altijd vanuit `main` uitgevoerd, niet vanuit de code in een aangeleverde PR.

- De workflow kan pas draaien nadat de PR met deze bestanden in `main` is gemerged.
- GitHub Actions moet aanstaan; het `GITHUB_TOKEN` moet `contents: write` mogen gebruiken.
- Bij een beschermde `main` kunnen automatische bot-commits worden geblokkeerd. Controleer dan de Actions-log en gebruik een toegestane update-PR-workflow.
- GitHub Issues blijft altijd bruikbaar, ook als de dashboard-workflow tijdelijk faalt.
- Let op de gewenste zichtbaarheid: deze repository was bij laatste inspectie **public**. Zet inhoud niet onbedoeld op GitHub Pages of in andere openbare publicaties.

## Optioneel: echte versleepbare GitHub Projects-weergave

Het ingebouwde [GitHub Projects](https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects) is de mooiste oplossing voor drag-and-drop, filters en tijdlijnen. Het kan niet met de huidige GitHub-connector automatisch worden aangemaakt. Je hoeft daarvoor maar één keer in de webinterface iets te doen:

1. Ga naar [Your Projects](https://github.com/users/mlemson/projects) en maak een **private** Project: `Conductor Chaos Development`.
2. Voeg de **kleine open issues** uit deze repository toe; [de actuele index](AI/SMALL_TASK_INDEX.md) vermeldt de relevante nummers. Gebruik epics alleen als overzicht.
3. Maak een **Board** met Status: `Todo`, `In Progress`, `In Review`, `Blocked`, `Done`. Gebruik daarnaast een **Table** en optioneel **Roadmap**.
4. Zet onder **Workflows** automatisch toevoegen van repository-issues aan, en zet gesloten issues op Done. Optioneel: velden **Priority (P0/P1/P2)**, **Area**, **Iteration** en **Target date**.
5. Zorg dat het project privé blijft. GitHub Projects-statusvelden en het Markdown-board zijn twee aparte presentatielagen; alleen de issue/PR-status is gemeenschappelijke waarheid. Configureer aparte Projects-workflows voor custom velden als je die wilt synchroniseren.

[GitHub Projects ingebouwde workflows](https://docs.github.com/en/issues/planning-and-tracking-with-projects/automating-your-project/using-the-built-in-automations) · [Issue koppelen aan een PR](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue).
