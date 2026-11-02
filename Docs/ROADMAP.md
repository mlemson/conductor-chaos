# Roadmap en taken voor agents

Alle taken zijn open. Afhankelijkheden en acceptatie zijn verplicht; tests met twee spelers zijn nodig voor netwerkgedrag. Mijlpalen beschrijven toekomstig werk, niet huidige functionaliteit.

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

## P2 — meer chaos en productie

- [ ] Gezamenlijke reparaties, complexere physics, gelijktijdige incidenten en extra wagons.
- [ ] Routes, omgeving, UI, audio, VFX en assets volgens [checklist](ASSET_CHECKLIST.md); prestaties en accessibility beoordelen.

## Volgende agent

Start met P0-01; rapporteer concrete Console- en packagebevindingen. Werk P0-02 daarna af. Voer P0-08 eventueel parallel uit na succesvolle import. Bouw pas op expliciete opdracht gameplay of assets. Bespreek ontwerponbekenden in [besluiten](DECISIONS.md), zonder openstaande taken als gereed te markeren.
