# Risico's en mogelijke bugs

Dit zijn hypotheses, geen vastgestelde bugs. Maak bij bevestiging een reproduceerbare melding met versie, rollen, stappen, resultaat, bewijs en ernst.

| Prioriteit | Risico / symptoom | Onderzoek en preventie |
| --- | --- | --- |
| P0 | Dubbele beweging: speler glijdt of vliegt weg op bewegende wagon | vergelijk wereld- en treinruimte, parenting en physics-ticks; T04–05 |
| P0 | Host ziet deur open, client dicht | autoritatieve toestand en late-join-snapshot; T06/10 |
| P0 | Koffer dubbel opgepakt of verdwijnt | atomische serverclaim en cleanup bij disconnect; T07–09 |
| P0 | Client triggert interactie buiten bereik of te vaak | server valideert identiteit, afstand en toestand; negatieve tests |
| P0 | Physics divergentie bij latency/packet loss | snapshots/interpolatie meten; geen onafhankelijke waarheden; T12 |
| P0 | Unity-template/packages niet compatibel of URP ontbreekt | schone import en Package Manager/Console inspecteren; P0-01 |
| P1 | Laatkomer mist event of ziet verkeerde houder | state snapshot i.p.v. alleen events; T10 |
| P1 | Host stopt en sessie blijft hangen | disconnectdetectie, cleanup, heldere fout; T11 |
| P1 | Botsingen schieten door of jitteren bij remmen | fixed timestep, snelheden, correcties en contacts meten; T04–05 |
| P1 | 4–6 spelers overschrijden bandbreedte of CPU | profiel op doelplatform, geen aannames over kosten; T13 |
| P1 | Joincode/credentials of commerciële assets lekken | geen secrets in Git/logs; licenties en toegangsinstellingen controleren |
| P2 | Onleesbare interacties of te veel visuele ruis | silhouettes, contrast, animaties en UI testen met echte spelers |
