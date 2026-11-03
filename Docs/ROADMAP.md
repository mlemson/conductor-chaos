# Roadmap en taken voor agents

> **Projectstatus:** volg het [live ontwikkelbord](PROJECT_BOARD.md) en de individuele [GitHub Issues](https://github.com/mlemson/conductor-chaos/issues). De onderstaande vakjes beschrijven inhoudelijke test-/ontwikkelfasen, maar worden **niet automatisch** bij iedere commit bijgewerkt. Sluit taken pas af via geverifieerde issues/PR's. Zie [samenwerkingsafspraken](DEVELOPMENT_WORKFLOW.md).


**Let op:** dit is de oorspronkelijke, grove fasering. Inmiddels bestaat er gameplay-/artcode. [Kleine uitvoertaken met actuele nummers](AI/SMALL_TASK_INDEX.md) zijn voor agents leidend en [AGENTS.md](../AGENTS.md) schrijft een minimale passende test voor. Onderstaande checkboxes zijn **geen actuele featurestatus** en hoeven niet bij iedere kleine commit te veranderen.

## P0 — technisch fundament

- [ ] **P0-01 Unity-baseline:** open project in 6000.5.7f1, resolve packages, controleer URP/input/testframework, Console en ontbrekende references; commit gegenereerde package-lock en `.meta`-bestanden. Leg eventuele versieconflicten en reproduceerbare stappen vast. Acceptatie: schone import en compile of concrete blocker gedocumenteerd.
- [ ] **P0-02 Networkingkeuze:** verifieer compatibele versies voor NGO, Transport en Unity Multiplayer Services/Relay/Lobby; documenteer authenticatie, hosting, serviceproject, kostenlimieten en configuratie zonder geheimen. Installeer na compatibiliteitscontrole en verifieer compile. Acceptatie: reproduceerbare setup en besluit vastgelegd. Geen sessies claimen zonder test.
- [ ] **P0-03 Sessiepad:** lokale host en afzonderlijke client verbinden/verlaten; foutafhandeling, ownership en lifecycle. Acceptatie: herhaalbare tweespelerstest en solo-pad, bewijs in testplan.
- [ ] **P0-04 Bewegende wagon:** eenvoudige tijdelijke testgeometrie pas tijdens deze taak; server beweegt wagon, twee spelers bewegen en botsen erin. Test starten, stoppen, versnellen en rotatie. Acceptatie: beide clients zien dezelfde uitkomsten; meet drift en noteer grenzen.
- [ ] **P0-05 Gedeelde deur:** beide spelers kunnen intentie sturen; server valideert en synchroniseert open/dicht. Gelijktijdige verzoeken en late join gecontroleerd.
- [ ] **P0-06 Koffer:** server bepaalt vrij/gedragen/losgelaten; oppakken, dragen, doorgeven, vallen, botsing en vertrek van drager. Twee gelijktijdige verzoeken mogen geen dubbele houder opleveren.
- [ ] **P0-07 Netwerkproef:** herhaal P0-03–06 met gesimuleerde vertraging/verlies en twee afzonderlijke instanties; documenteer instellingen, platform, waarnemingen, faalgevallen en reproduceerbaarheid. Scherpe acceptatietoleranties op basis van meting afspreken.

- [ ] **P0-08 Art-direction proof (parallel met netwerkproef):** maak op een aparte taak/branch een minimale stylized conducteur en één wagonhoek, alleen nadat Unity import schoon werkt. Vergelijk in-engine screenshots met [gamevisie](GAME_VISION.md) en [visuele referenties](References/README.md); geen massaproductie vóór goedkeuring. Doel is een beeldtest, geen extra gameplay.

## P1 — co-op uitbreiden

- [ ] Vier spelers testen; taakverdeling, deurbediening en passagierscontrole tegelijk.
- [ ] Gedeelde passagiers, tickets, sleutels/scanner en missies na bewezen interactieprotocol.
- [ ] Hostvriendelijke online lobby, uitnodiging, foutmeldingen en waar mogelijk reconnect; singleplayer blijft leuk.
- [ ] Zes spelers als afzonderlijk schaaldoel testen, inclusief belasting en join/leave.

- [ ] **P1-physics proof (na P0):** laat één wagon gecontroleerd bij een breuk in het spoor écht fysiek ontsporen; behoud snelheid/impuls, test de effecten op twee spelers en een koffer en synchroniseer de eindpositie via host + remote client. Vergelijk begeleide beweging met overgang naar Rigidbody versus track constraints. Zie [derailment-ontwerp](DERAILMENT_PHYSICS.md) en T14–T17 in [testplan](TESTPLAN.md).

## P2 — meer chaos en productie

- [ ] Gezamenlijke reparaties, gelijktijdige incidenten en extra wagons.
- [ ] **P2-derailment-productie:** meerdere aangekoppelde wagons kunnen afzonderlijk kantelen/ontsporen of een koppeling breken; crashes bij bruggen, grote obstakels en beschadigd spoor; synchroniseer speler-/bagagephysics en test tot 6 spelers. Niet animatie-only. Zie [DERAILMENT_PHYSICS.md](DERAILMENT_PHYSICS.md).
- [ ] Routes, omgeving, UI, audio, VFX en assets volgens [checklist](ASSET_CHECKLIST.md); prestaties en accessibility beoordelen.

## Volgende agent

Kies één kleine, niet-toegewezen [GitHub Issue](https://github.com/mlemson/conductor-chaos/issues), begin bij de [issue-index](AI/SMALL_TASK_INDEX.md) en volg de [kleine-taakregels](AI/SMALL_TASK_PLAYBOOK.md). Controleer actuele scripts, scène en eerdere PR's vóór je iets opnieuw implementeert. Aan grote epics niet in één PR beginnen. Vraag alleen naar wezenlijke ontwerpbesluiten; laat gewone Play-/feel-tests waar passend aan de gebruiker.
