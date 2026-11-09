# Visuele referentie voor agents

De gekozen conceptafbeelding is de **primaire referentie** voor characters, kleurgebruik, licht en speelse uitstraling. Het doel is dat de game qua kwaliteit en stijl zo dicht mogelijk bij dit beeld komt.

| Bestand | Wat |
|---|---|
| [conductor-chaos-concept.png](conductor-chaos-concept.png) | De originele conceptafbeelding (1536×1024). Altijd eerst bekijken. |
| [STYLE_PALETTE.svg](STYLE_PALETTE.svg) | Stijlkaart v2: kernprincipes, licht/schaduw per materiaal, 48 kleurramps met bronuitsnede, vormentaal personages, URP-vertaling. |
| [palette.json](palette.json) | Dezelfde ramps machine-leesbaar, voor scripts, materialen en agents. |
| [UI/README.md](UI/README.md) | Geselecteerde **UI-designs (beeld 1 en 3)**, exacte PNG-bestandsnamen en uploadstatus. Leidende referentie voor menu, knoppen, lobby en HUD-onderdelen. |
| [Environment/](Environment/) | Conceptbladen `station-concept.png` (stationnetje) en `platform-kit-concept.png` (perronkit). Kleuren gemeten met [tools/measure_station_ramps.py](tools/measure_station_ramps.py) → palette-groep *Station & perron*. Uitwerking: `Docs/Art/Station_*.png`. |
| [Characters/](Characters/) | Conceptbladen van de personagevarianten: `elderly-passenger-concept.png`, `tourist-concept.png`, `rail-mechanic-concept.png`, `station-master-concept.png`, `business-traveler-concept.png`, `child-passenger-concept.png`, `baggage-helper-concept.png`, plus `conductor-ingame.png` (huidige conducteur in Unity). Kleuren daaruit gemeten met [tools/measure_character_ramps.py](tools/measure_character_ramps.py) → palette-groep *Reizigers & crew*. Uitwerking: `Docs/Art/*_DesignSheet.png`. |
| [water-reference.png](water-reference.png) | Doel-look voor meren/rivieren (issue #110): helder turquoise ondiep, diep azuur, wit schuimrandje langs de oever, lichte golfstreepjes. Kleuren hieruit gemeten staan in `ArtSource/Tools/unity_assets.py` (`WATER_DEFAULTS`). |
| [tools/build_style_card.py](tools/build_style_card.py) | Genereert SVG en JSON uit één bron. Pas ramps alleen hier aan. |

Uitgewerkte designs (ontwerpblad conducteur, art check bij de wagondeur, vergelijking met dit concept) staan in [Docs/Art](../Art/).

Alle kleuren zijn **gemeten** in de conceptafbeelding (per materiaalgebied, percentielen van schaduw naar licht), niet geschat. Regels staan in de [art style guide](../ART_STYLE_GUIDE.md).

## Wat wél overnemen
- Boonvormige poppetjes (hoofd en romp één vorm) met grote witte ogen, kleine puntpupillen en een simpele ovale mond; expressieve, net wat stijve beweging.
- Zachte, ronde, chunky 3D-vormen met afgeronde randen; zachte facetten op rots en landschap.
- Warm licht met **koele blauw-violette schaduwen**; nooit grijs of zwart in de schaduw.
- Geschilderde verlopen op grote vlakken, weinig textuur, geen realistische reflecties.
- Treinrood, marineblauw, goud, perkament/crème; blauwgroene dennen tegenover geelgroen gras; blauw-violette bergen.
- Simpele iconen en perkament-achtige, sticker-UI.

## Wat níet letterlijk overnemen
- De afbeelding toont een kleine rode locomotief; de eerste gameplay gaat over een **regionale reizigerstrein** met werkende deuren en een begaanbaar interieur, in dezelfde vormentaal en livreikleuren.
- De poster is promotiemateriaal. Imiteer niet alle tekstvakken of de volle opmaak in de HUD.
- Beelden zijn inspiratie, geen technische maattekening.

## Praktische art check
Maak eerst een Unity-screenshot met één conducteur naast of in een deur van een eenvoudige wagon. Leg die naast de conceptafbeelding en controleer: silhouet en proporties, oogvorm, materiaalglans, warm licht/koele schaduw, kleurhiërarchie en leesbaarheid van interacties. Laat de art check goedkeuren voordat er tientallen varianten gemaakt worden.

## Stijlkaart bijwerken
```
python Docs/References/tools/build_style_card.py
```
Vereist Python 3 + Pillow. Schrijft `STYLE_PALETTE.svg` en `palette.json` opnieuw zodat kaart en data gelijk blijven.
