# Game vision — Conductor Chaos

> **Status:** ontwerpintentie, niet geïmplementeerde gameplay. De [art style guide](ART_STYLE_GUIDE.md) en [technische roadmap](ROADMAP.md) blijven afzonderlijke bronnen.

## Pitch
Een humoristische **coöperatieve conducteur-game** waarin 1–6 spelers een treinreis proberen te laten slagen. De eerste rit lijkt eenvoudig: kaartjes controleren, met een **echte sleutelinteractie** deuren bedienen, reizigers helpen en veilig vertrekken. Door fysiek onhandige bediening, botsingen en onderling gestuntel ontstaan steeds chaotischere situaties. Later leiden bergtrajecten, tunnels, bruggen en defecten tot avontuurlijker samenwerking. **Begin klein; laat chaos organisch toenemen.**

## Vijf spelpijlers

1. **Conducteur eerst:** de unieke activiteit is werken *in en rond* een reizigerstrein. Geen generieke treinbestuursimulator of spoorbouwgame. Een machinistenrol of treinbediening kan later aanvullend worden.
2. **Simple to understand, hard to execute:** weinig intuïtieve acties, fysiek geloofwaardige maar expres klungelige uitvoering. Moeilijkheid komt van timing, gewicht, beweging en interactie met anderen — niet van onduidelijke keybinds.
3. **Multiplayer-first:** gedeelde, server-authoritatieve wereld, gelijktijdige taken, leuke botsingen, geen onverklaarbare desync. Solo moet ook kunnen; **2 spelers eerst bewijzen**, later 4 en maximaal 6 testen.
4. **Van gezellig naar spannend:** begin met normale stations en passagiers; schaal daarna op naar meer tijdsdruk, technische problemen, moeilijkere routes, afgronden, bruggen en tunnels. Elke stap bouwt voort op bestaande handelingen.
5. **Charmante, lichte 3D:** chunky kleine personages met grote expressieve ogen, ronde simpele volumes, matte materialen, beperkt palet en warm licht. Speels en compact, niet glossy/realistisch/futuristisch.

## Belangrijke interacties
- Conducteur gebruikt een zichtbare sleutel in een slot om een deur te openen, te houden of vrij te geven. **Niet** een tweede generieke knop die exact hetzelfde doet als de sleutel; animatie en rechten consistent houden, ook onder beweging.
- Reizigers staan/zitten, bieden tickets aan, reageren op controle, blokkeren soms de doorgang.
- Spelers lopen door de schuddende wagon; botsingen met spelers en koffers leveren verrassingen, geen onspeelbare netwerkfouten.
- Voorwerpen fysiek hanteren; fouten zijn grappig, maar controls blijven voorspelbaar en consequent.

## Fases en gates

| Fase | Doel | Acceptatie |
| --- | --- | --- |
| P0 technisch | 2 spelers, bewegende testwagon, één gedeelde deur, één koffer | Bewezen host + client, geen dubbele objects of blijvende drift |
| P0 art proof (parallel) | 1 simpele conducteur + 1 wagonhoek + warme belichting | Vergelijk twee Unity-screenshots met [art direction](ART_STYLE_GUIDE.md); geaccepteerd vóór massaproductie |
| P1 eerste leuke loop | Kleine halte, passagiers, tickets, sleutel, vertrek en één incident | 10 minuten begrijpelijke co-op met grappige emergente situaties |
| P2 bergavontuur | Bruggen, tunnels, hoogte, meerdere soorten incidenten | Bestaande interacties werken ook op de lastigere route |

## Niet doen
- Geen grote contentgenerator, hele bergmap, complete physics-simulatie of tientallen assets voordat P0 is getest.
- Geen realistische treinbesturing die de conducteurrol verdringt.
- Geen opzettelijke lag of onleesbare controls als bron van humor.
- Geen losstaande implementaties van interactie die alleen bij de host werken.
- Geen conceptart letterlijk als 3D-bouwtekening behandelen: referentie voor **look-and-feel**, niet voor exacte locomotief, topologie of UI-tekst.

## Open ontwerpbesluiten
Camera (first-person/third-person), exacte treinsoort, of en wanneer een speler machinist wordt, input/controllerondersteuning en doelplatform moeten bewust worden gekozen na een kleine speelbare proef. Tot die tijd geen architectuur vastzetten op één onbewezen presentatiekeuze.
