# Kleine uitvoertaken voor agents — lees dit vóór een issue

**Doel:** snel één speelbare verbetering of één asset opleveren. Niet bij elke wijziging de hele game opnieuw testen, niet verdwalen in onderzoeken of documentatie. [GitHub Issues](https://github.com/mlemson/conductor-chaos/issues) zijn leidend; [brede onderwerpen](SMALL_TASK_INDEX.md) bevatten de deelissues.

## Zo start je een issue (kort)

1. Kies **één open klein issue**; sla over wanneer iemand anders eraan werkt (controleer #44: loopbug is inmiddels afgesloten; dubbel werk vermijden). Brede issues met `<!-- conductor-chaos-epic -->` zijn **geen uitvoeropdrachten**: kies een gekoppeld kind.
2. Check de actuele `main`, open PR's en bestaande scripts/assets. Het project is verder dan de oude P0-README: er bestaan **TrainPlayground**, een NGO-proef, UI-componenten en veel gegenereerde modellen. Bouw dit niet dubbel.
3. Lees **alleen** `AGENTS.md`, de issue, dit document en de 1–2 relevante bestanden. Lees specialistische documenten alleen indien nodig: [gamevisie](../GAME_VISION.md), [physics](../DERAILMENT_PHYSICS.md), [UI](../UI_STYLE_GUIDE.md), [art](../ART_STYLE_GUIDE.md), [physics playground](PHYSICS_PLAYGROUND.md). Niet alles standaard in één keer.
4. Kies een branch voor die ene issue; wijzig alleen relevante bestanden. Gebruik bestaande `ArtSource/` bij procedural assets, `Assets/Scripts/Playground` bij beweging/physics en `Assets/Scripts/UI/` bij HUD/menus. Houd de bestaande Unity 6000.5.7f1-config, NGO en .meta-GUID's.
5. Meld `Refs #nummer` in PR zolang het werk wordt beoordeeld. Gebruik `Closes #nummer` alleen als die éne uitvoertaak compleet is en door merge mag sluiten. Werk de issue bij bij een blocker of belangrijke ontwerpbeslissing, **niet** na iedere commit.

## Wat mag de agent zelf beslissen?

**Zelfstandig kiezen:** namen van private helpers, eenvoudige objectpositionering, hergebruik van kleur uit `palette.json`, klein technisch detail dat later zonder gevolgen aanpasbaar is. Schrijf de keuze kort bij oplevering.

**Vraag één gerichte vraag aan de gebruiker wanneer:**
- bediening/gamefeel wezenlijk verandert (toetsen, loopstijl, moeilijkheid, physics-gewicht);
- een model/visuele richting afwijkt van de conceptafbeelding, of je twijfelt tussen twee zichtbare opties;
- de taak een nieuw online dienstaccount/betaling, externe assetlicentie, ingrijpende projectwijziging of verlies van bestaand werk vereist;
- twee actieve agents dezelfde centrale subsystemen aanpassen, of een Issue een belangrijke ontwerpafhankelijkheid mist.

Vraag bij voorkeur **A/B + een aanbevolen standaard** en laat de rest van het werk niet stilvallen als een onafhankelijk deel al uitvoerbaar is. Vraag niet om toestemming voor iedere kleine implementatiekeuze.

## Testbeleid — zuinig, passend bij risico

| Werk | Standaardcheck door agent | Geen vereiste |
| --- | --- | --- |
| Documentatie, prefab- of artmodel | Pad, import/export, relevante `.meta`, eventueel **één** screenshot/preview | Geen game starten, geen volledige renderreeks |
| Lokale UI of code | Statische controle + compile **als Editor al draait/haalbaar is**; één gerichte interactiecheck indien nodig | Niet iedere knop en hele route afspelen |
| Spelerbeweging / grab / treininteractie | Eén korte gerichte test (lopen, oppakken of deur) **alleen voor wat gewijzigd is** | Geen complete playthrough of alle scènes |
| **Gedeelde netwerkstate** (pickup, wagon, NPC, deur, crash) | Als mogelijk **één host + één aparte client** voor precies de gewijzigde actie; anders duidelijk `NOT RUN` en een handmatige check | Geen 4–6 spelers, reconnect, stress, latency of alle combinaties bij elk issue |
| Fundamentele netwerk-/physicswijziging | Extra gerichte test wanneer het risico aantoonbaar groter is; noteer waarom | Geen herhaald importeren of urenlange matrix zonder expliciete QA-issue |

**Een lokale Play-test is niet voor elk issue verplicht.** Liever één juiste en aantoonbare check dan een groot hoeveelheid onnodige sessies. Als Unity niet toegankelijk is: geen verzonnen resultaten en ook niet proberen alle mogelijke checks alsnog met dure omwegen te simuleren. Geef **`NOT RUN — gebruiker kan handmatig Play-testen`** en 1–3 gerichte stappen.

De *gebruiker* mag de visuele feel/balans beoordelen; agents doen bij wijzigingen aan gedeelde state minimaal het noodzakelijke technische bewijs wanneer uitvoerbaar. Specifieke integratie-, schaal- en regressietests blijven aparte QA-issues en worden niet automatisch bij kleine taken gekopieerd.

## Performance en parallelle agents

- Gebruik de huidige Unity-editor als die beschikbaar is; **geen herhaald cold import/reload** en geen totale rebuild van alle Blender/FBX-assets voor één nieuwe prop.
- Eén issue = één PR. Vermijd files met tegelijk actieve andere agents; check issue assignee en PR's voor start. Werk gerichte, kleine wijzigingen af voordat je de volgende aanraakt.
- Verander geen unrelated systemen en herstel geen onbekende problemen er "even bij". Maak desnoods een **nieuw klein issue** voor iets wat je ontdekt.
- Pas geen door de gebruiker nog niet geaccepteerde gameplay- of designwijzigingen stilzwijgend aan.
- Markeer geen brede epic 'Klaar' omdat één kind gereed is. Het board telt kleine issues; epics zijn verzamelonderwerpen.

## Oplevering (maximaal 5 korte regels)

- **Gedaan:** wat is gebouwd, welke belangrijke bestanden?
- **Gecontroleerd:** 1 korte test + resultaat, of `NOT RUN` met reden.
- **Gebruiker kan testen:** 1–3 handelingen, alleen indien nodig.
- **Openstaand:** blocker/risico of `geen bekende`.
- **PR / issue:** link; geen logs dumpen en geen bredere vervolganalyse tenzij gevraagd.

Voorbeeld: `#53: recht + bocht prefab. FBX en .meta gecontroleerd; Unity Play NOT RUN. In Unity plaats twee segmenten naast elkaar om de aansluiting te beoordelen. PR #...`
