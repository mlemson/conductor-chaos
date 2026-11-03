# Uitvoerbare taken — Conductor Chaos

> Deze pagina is een **wegwijzer**, niet het voortgangsbord. De actuele status, eigenaar en PR staan in [GitHub Issues](https://github.com/mlemson/conductor-chaos/issues) en het [live ontwikkelbord](../PROJECT_BOARD.md). De brede issues zijn **epics**, geen opdrachten voor één agent.

## Aanbevolen volgorde

1. **Lopen eerst stabiel:** [bestaande bug #44](https://github.com/mlemson/conductor-chaos/issues/44) is inmiddels afgesloten; **controleer eerst de bestaande fix en bouw niets dubbel**.
2. **Een prop oppakken:** #46 → #47 → #48; #52 is een visueel prefab, #49 targetdetectie.
3. **De trein laat bewegen:** #53 → #54 → #55 → #56. Dan pas #57 en #58.
4. **Conducteur worden:** #61 → #62 → #63 en NPC #68 → #71 → #72 → #75.
5. **Spelervaring afmaken:** #59–60, #76–81 en #79/91; dieren en chaos later #82–90.

Elke nieuwe issue vraagt een **kleine PR en een passende minimale controle**: [lees het agentplaybook](SMALL_TASK_PLAYBOOK.md). Niet alles hoeft door de agent ge-Play-test te worden; stem gebruikersfeel af met de gebruiker.

## Deeltaken per onderwerp

### Speler & oppakken

Bovenliggende onderwerpen: [#28](https://github.com/mlemson/conductor-chaos/issues/28) · [#7](https://github.com/mlemson/conductor-chaos/issues/7) · [#44](https://github.com/mlemson/conductor-chaos/issues/44)

- **P0** [#46 — Grip: één los object vanaf de vloer kunnen oppakken](https://github.com/mlemson/conductor-chaos/issues/46)
- **P0** [#47 — Grip: object loslaten zonder blijven kleven](https://github.com/mlemson/conductor-chaos/issues/47)
- **P0** [#48 — Grip: twee spelers kunnen niet dezelfde koffer bezitten](https://github.com/mlemson/conductor-chaos/issues/48)
- **P0** [#49 — Interacties: één herbruikbare focusdetectie voor props](https://github.com/mlemson/conductor-chaos/issues/49)
- **P1** [#50 — Spelers kunnen elkaar licht fysiek aanstoten](https://github.com/mlemson/conductor-chaos/issues/50)
- **P1** [#51 — Speler kan na een val weer opstaan](https://github.com/mlemson/conductor-chaos/issues/51)
- **P1** [#52 — Prefab voor één testkoffer met collider en massainstellingen](https://github.com/mlemson/conductor-chaos/issues/52)
- **P1** [#67 — Interacteerbare props hebben één herkenbare feedback](https://github.com/mlemson/conductor-chaos/issues/67)

### Spoor, trein en stations

Bovenliggende onderwerpen: [#5](https://github.com/mlemson/conductor-chaos/issues/5) · [#11](https://github.com/mlemson/conductor-chaos/issues/11) · [#14](https://github.com/mlemson/conductor-chaos/issues/14)

- **P0** [#53 — Spoorasset: recht segment en bocht als Unity-prefabs](https://github.com/mlemson/conductor-chaos/issues/53)
- **P0** [#54 — Spoorroute: korte testbaan uit bestaande railstukken](https://github.com/mlemson/conductor-chaos/issues/54)
- **P0** [#55 — Treinbeweging: één wagon volgt testspoor](https://github.com/mlemson/conductor-chaos/issues/55)
- **P0** [#56 — Rijdende wagon: speler blijft op vloer bij rijden/remmen](https://github.com/mlemson/conductor-chaos/issues/56)
- **P1** [#57 — Cabinehendel: eenvoudige bediening van treinsnelheid](https://github.com/mlemson/conductor-chaos/issues/57)
- **P1** [#58 — Wagonkoppeling: twee delen visueel koppelen](https://github.com/mlemson/conductor-chaos/issues/58)
- **P1** [#59 — Station: één modulair perron met bank en bord](https://github.com/mlemson/conductor-chaos/issues/59)
- **P1** [#60 — Eerste rit: vertrekstation naar eindhalte](https://github.com/mlemson/conductor-chaos/issues/60)

### Sleutel, deuren en kolen

Bovenliggende onderwerpen: [#6](https://github.com/mlemson/conductor-chaos/issues/6) · [#24](https://github.com/mlemson/conductor-chaos/issues/24)

- **P0** [#61 — Conducteurssleutel: vast voorwerp en slotpunt](https://github.com/mlemson/conductor-chaos/issues/61)
- **P0** [#62 — Treindeur: open/dicht als gedeelde toestand](https://github.com/mlemson/conductor-chaos/issues/62)
- **P1** [#63 — Treindeur: openhouden en loslaten tijdens rijden](https://github.com/mlemson/conductor-chaos/issues/63)
- **P1** [#64 — Kolenwagen: toegankelijk laadpunt voor schep](https://github.com/mlemson/conductor-chaos/issues/64)
- **P1** [#65 — Vuurkist: deurtje openen en schep kolen afleveren](https://github.com/mlemson/conductor-chaos/issues/65)
- **P1** [#66 — Kolenverbruik: één begrijpelijke keteldruk-/brandstofwaarde](https://github.com/mlemson/conductor-chaos/issues/66)

### Passagiers, tickets en opdrachten

Bovenliggende onderwerpen: [#10](https://github.com/mlemson/conductor-chaos/issues/10)

- **P1** [#68 — NPC: één basis-passagier als herbruikbaar prefab](https://github.com/mlemson/conductor-chaos/issues/68)
- **P1** [#69 — NPC: passagier kan zitten of staan](https://github.com/mlemson/conductor-chaos/issues/69)
- **P1** [#70 — NPC: eenvoudige route door één wagon](https://github.com/mlemson/conductor-chaos/issues/70)
- **P1** [#71 — Tickets: geldig/ongeldig gegeven bij één NPC](https://github.com/mlemson/conductor-chaos/issues/71)
- **P1** [#72 — Kaartcontrole: één NPC-ticket lezen en aftekenen](https://github.com/mlemson/conductor-chaos/issues/72)
- **P1** [#73 — NPC zegt één korte contextzin bij interactie](https://github.com/mlemson/conductor-chaos/issues/73)
- **P1** [#74 — NPC: voeten op bank als kleine overtreding](https://github.com/mlemson/conductor-chaos/issues/74)
- **P1** [#75 — Opdracht: controleer drie passagiers](https://github.com/mlemson/conductor-chaos/issues/75)

### Menu, HUD, lobby en geluid

Bovenliggende onderwerpen: [#16](https://github.com/mlemson/conductor-chaos/issues/16) · [#13](https://github.com/mlemson/conductor-chaos/issues/13)

- **P0** [#76 — Hoofdmenu: Play en Terug naar de bestaande proefscène](https://github.com/mlemson/conductor-chaos/issues/76)
- **P1** [#77 — HUD: één contextuele interactieprompt](https://github.com/mlemson/conductor-chaos/issues/77)
- **P1** [#78 — HUD: teller voor actuele opdracht](https://github.com/mlemson/conductor-chaos/issues/78)
- **P1** [#79 — Lobby: toon echte aangesloten spelers in bestaand menu](https://github.com/mlemson/conductor-chaos/issues/79)
- **P1** [#80 — UI: knoppen en iconen op één schaal en juiste navigatie](https://github.com/mlemson/conductor-chaos/issues/80)
- **P1** [#81 — Geluid: vier eenvoudige feedbackmomenten](https://github.com/mlemson/conductor-chaos/issues/81)
- **P1** [#91 — Online uitnodigen: eenvoudige joincode of vriendenlink](https://github.com/mlemson/conductor-chaos/issues/91)
- **P2** [#92 — Optionele mijlpaalcheck: sessie met vier en later zes spelers](https://github.com/mlemson/conductor-chaos/issues/92)

### Wereld, dieren en incidenten

Bovenliggende onderwerpen: [#15](https://github.com/mlemson/conductor-chaos/issues/15) · [#12](https://github.com/mlemson/conductor-chaos/issues/12) · [#14](https://github.com/mlemson/conductor-chaos/issues/14)

- **P1** [#82 — Omgeving: korte houten spoorbrugmodule](https://github.com/mlemson/conductor-chaos/issues/82)
- **P1** [#83 — Omgeving: tunnelportaal als herbruikbaar prefab](https://github.com/mlemson/conductor-chaos/issues/83)
- **P1** [#84 — Dier: één cartoonkoe als speelbaar obstakel](https://github.com/mlemson/conductor-chaos/issues/84)
- **P1** [#85 — Koe op het spoor: eenvoudige stop/obstakelreactie](https://github.com/mlemson/conductor-chaos/issues/85)
- **P2** [#86 — Rotsval: één vallende rots als treinincident](https://github.com/mlemson/conductor-chaos/issues/86)
- **P1** [#87 — Kapotte rail: één herkenbaar beschadigd spoorsegment](https://github.com/mlemson/conductor-chaos/issues/87)
- **P1** [#88 — Één wagon kan van spoorgeleiding naar Rigidbody-physics](https://github.com/mlemson/conductor-chaos/issues/88)
- **P2** [#89 — Één ontsporing synchroniseren tussen host en client](https://github.com/mlemson/conductor-chaos/issues/89)
- **P2** [#90 — Koppeling kan breken bij hoge impact](https://github.com/mlemson/conductor-chaos/issues/90)

## Wat bewust géén losse taak hier is

- De bestaande art-/terrainissues (#22, #25, #30, #33, #35, #38, #40, #42) bevatten al (gedeeltelijk) werk. **Eerst huidige broncode en PR's inspecteren**, daarna alleen nieuwe ontbrekende details als mini-issue toevoegen.
- #2–#4 en #8 betreffen oorspronkelijke baseline/netwerkproef. Houd die als oudere controle-/infrastructuurissues aan; deze lijst is geen instructie om werk opnieuw uit te voeren.
- #39 heeft al een UI-foundation implementatie. Voor vervolgwerk gebruik #76–81 in plaats van een tweede menuframework.
- Geen massale testmatrix; een latere 4/6-player check staat als afzonderlijke optionele mijlpaal #92.
