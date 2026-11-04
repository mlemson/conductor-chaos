# ArtSource — reproduceerbare art-bronnen

Bronbestanden voor 3D-assets en ontwerpbladen. Dit staat bewust **buiten `Assets/`**, zodat Unity
het niet importeert. De Unity-assets in `Assets/Art/` worden hieruit gegenereerd.

> **Status (issue #9, P0 art proof):** conducteur-basismodel + regionale wagon (blockout) + ontwerpbladen.
> De renders tonen de **doel-look** (ramp-shader). Een in-engine Unity-screenshot is nog **NOT RUN**.

## Wat staat waar

| Pad | Wat |
|---|---|
| `Characters/Conductor/build_conductor.py` | Conducteur-basismodel, parametrisch opgebouwd uit maten van het concept |
| `Train/RegionalWagon/build_regional_wagon.py` | Regionale wagon in facetstijl (deur, slot, grepen, ramen, draaistellen, koplampen) + art-check-decor |
| `Train/Locomotive/build_locomotive.py` | Locomotief v2.1 (ronde ketelneus, rookkastring met lamp, gewelfd dak, sierlijsten, drijfstang) — issue #22 |
| `Train/CattleWagon/build_cattle_wagon.py` | Veewagen: rode kooi uit één stuk om een lattenwand met echte kieren, schuifdeuren, gewelfd dak, interieur (stro, hooibalen), doorloopbaar — issue #25 |
| `Train/Tender/build_tender.py` | Kolenwagen direct achter de loc (rode bak, gouden rand, panelen met klinknagels, kolenberg, ladder, sluitlampen) — issue #22 |
| `Environment/build_environment_kit.py` | Modulaire natuurkit (issue #30): bomen, struik, rotsen, klifmodules + klifclusters (v2), grasheuvel, stilstaande watervallen, verre bergen, wolken; elk asset een eigen FBX + GLB, gedeelde materialen |
| `Environment/build_track_kit.py` | Spoorkit (issue #53): rechte stukken, bochten R20 15°/30° links/rechts, stootblok, kapot stuk, houten schraagbrug, waarschuwingsbord en slagboom. Sockets voor aansluiten; prefabs + Track Builder in Unity (`Conductor Chaos > Art > Track`) |
| `Textures/build_ground_textures.py` | Tegelbare grondtextures voor het Unity-terrein (gras, bloemetjes, grond, stenen pad, rots), issue #38 |
| `Tools/meshkit.py` | Kleine numpy-toolkit: lathe/superellips, afgeronde box, ellipsoïde, capsule, profielextrusie (ketel/dak); export naar FBX (ASCII) en glTF (.glb) |
| `Tools/spatial_fallback.py` | NumPy-terugval voor de straalzoekopdracht bij het bakken van vertexkleur als SciPy ontbreekt |
| `Tools/implicit_body.py` | Bouwt het lijf met broek en voeten als één verbonden oppervlak met een echte opening tussen de benen |
| `Tools/unity_assets.py` | Schrijft URP Lit-materialen (`.mat`) en `.meta`-bestanden met stabiele GUID's |
| `Render/render.html`, `render.cjs` | Headless stijlrender (three.js + Playwright) met de stylized ramp-shader |
| `Render/scenes/*.json` | Camera's, poses en expressies van alle renders |
| `Render/make_sheets.py` | Stelt `Docs/Art/*.png` samen |
| `Exports/` | `.glb`-versies (voor Blender/viewers) en `*.materials.json` (ramps per materiaal) |
| `build_all.py` | Alles in één keer: modellen → renders → bladen |

Gegenereerde uitvoer:

- `Assets/Art/Models/Characters/Conductor/Conductor.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/Tender/Tender_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/CattleWagon/CattleWagon_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Environment/{Trees,Rocks,Cliffs,Mountains,Sky}/*.fbx` + `Assets/Art/Materials/Environment/*.mat`
- `Assets/Art/Materials/...` URP Lit-materialen (middentoon van elke ramp)
- `Docs/Art/Conductor_DesignSheet.png`, `ArtCheck_Wagon.png`, `ArtCheck_vs_Concept.png`, `Locomotive_DesignSheet.png`, `CattleWagon_DesignSheet.png`, `Environment_DesignSheet.png`

## Opnieuw bouwen

```
python ArtSource/build_all.py --models   # alleen FBX/GLB/.mat (Python 3 + numpy)
python ArtSource/build_all.py            # + renders en ontwerpbladen
```

Voor de snelste generatie van vertexkleuren wordt SciPy gebruikt; als dat niet is geïnstalleerd gebruikt meshkit de meegeleverde NumPy-terugval, die trager kan zijn. De oudere instructie dat alleen NumPy nodig was beschreef de SciPy-afhankelijkheid niet volledig.

Voor renders: Node.js met Playwright (Chromium) en three.js **r187** in `ArtSource/Render/three` (staat in `.gitignore`):

```
git clone --depth 1 --branch r187 https://github.com/mrdoob/three.js ArtSource/Render/three
```

Pas vormen of maten aan in de `build_*.py`-scripts, niet in de FBX. Kleuren komen altijd uit
`Docs/References/palette.json`; nieuwe tinten eerst meten en toevoegen via
`Docs/References/tools/build_style_card.py`.

## Conventies

- Y omhoog, het model kijkt naar **+Z**, 1 unit = **1 meter**. De linkerkant van een personage ligt op +X.
- **Rigstatus:** de armen blijven aparte nodes en de sleutel kan aan `Hand_L` worden bevestigd. Per arm zijn mouw en hand nu één oppervlak met twee materialen. Lijf, broekspijpen en donker schoeisel vormen samen één verbonden `Body`-mesh. `Leg_L/R` zijn voorlopig lege plaatsaanduidingen: draaien van die nodes animeert de benen niet. Voor loopanimaties moet nog een huid-/bottenrig of een andere passende oplossing worden ontworpen en getest.
- **Conceptstudie v5:** de klep en kroon van de pet bedekken de gouden band niet meer. De mouwen sluiten onder de uniformrand aan; handen hebben meer geleidelijke afronding. De tweede knoop is kleiner en donkerder en het strookje erachter is afgerond. De ronde benen en opening uit v4 blijven behouden. De oudere ontwerpbladen en v2/v3/v4-vergelijkingen tonen vorige modelversies; zie `Docs/Art/Conductor_ConceptStudy_v5.png` voor de huidige voorlopige vormvergelijking. Unity-import en materiaalweergave moeten nog worden getest; de meshdichtheid moet voor meerdere personages worden beoordeeld.
- `python ArtSource/Tests/test_conductor_body.py` controleert onder meer of het lijf een enkele gesloten mesh is en de ruimte tussen de benen echt open is.
- `python ArtSource/Tests/test_conductor_finish.py` controleert de vrije gouden band en de aansluiting van mouwen op het uniform.
- Hiërarchie conducteur: `Conductor › Body › (Neckline, Collar, Buttons, Eye_L/R › Pupil_L/R, Mouth_Neutral|Surprised|Happy, Cap, Arm_L/R › Hand_L/R › Prop_Key)`, plus lege `Leg_L/R`-ankers.
  Standaard verborgen: `Mouth_Surprised`, `Mouth_Happy`, `Prop_Key`.
- **Uit één stuk:** gebruik `meshkit.frame_ring` (kader met verstekhoeken), `meshkit.hollow_box` (holle bak met dagkanten) en `meshkit.prism` (stapvormig profiel). Laat geen balken stomp tegen elkaar eindigen; een lijst die op een andere lijst aansluit, eindigt daarin.
- Doorloopbaar (issue #24): de cabine-achterdeur, de kolenwagen (`FallPlate`, dekken, `Coal`) en de kopse doorgangen van de wagon zijn de ankers voor latere colliders.
- **Prop Painter (Unity, issue #35):** *Conductor Chaos > Art > Prop Painter* schildert natuur-assets met een kwast over de map.
  - Presets: Bomen, Rotsen, Kliffen, Water, Achtergrond en Bos + rotsen. Het palet heeft gewichten en je kunt modellen erin slepen.
  - Instellingen: straal, aantal per stip, minimale afstand, willekeurige schaal en hoogte, draaistap (kliffen 90°), volg ondergrond, inzakken en raster.
  - Bediening: LMB = schilderen, Shift+LMB = gummen (alleen geschilderde objecten), `[` `]` = kwastgrootte. Alles kan met Undo terug.
  - Objecten komen onder `PaintedProps/<categorie>` als prefab-instanties.
  - Met *Collider toevoegen* kun je daarna bomen óp kliffen schilderen.
- Regionale wagon: `DoorLeaf_L/R` schuiven langs lokale X (dicht = +0,475 / −0,475). `KeyLock` (0,72 m) en
  `DoorSill` zijn de interactie-ankers voor P0-05.
- **Geschilderde variatie** (zodat niets één effen kleur is) is per vertex gebakken in de vertexkleur
  (`Scene.bake_shading`): **R** = occlusie (hoeken, onder randen, onderkant iets donkerder), **G** = randlicht op
  afrondingen, **B** = verfvlekken (fBm, 0,5 = neutraal; staat in de render **uit**, `noise=1` zet hem aan ter vergelijking).
  De shader verschuift hiermee de positie in de kleurramp. **Low-poly:** trein en decor hebben harde normalen per vlak
  (`Scene.facet_all`), zodat elk vlak één effen tint krijgt. De conducteur blijft glad (boonvorm).
  **Vlakken en vuil:** de shader legt een 3D-Voronoi over het model: elke cel krijgt een licht andere tint.
  Treinen worden naar onderen vuiler, per cel. Dit gaat via shaderhints per materiaal (`Scene.style_all`):
  `cells`, `cellSize`, `grime` en `grimeY` (staan ook in `*.materials.json`). In Unity wordt dit een Voronoi in de
  Shader Graph (objectruimte). In Unity leest de toekomstige Shader Graph dit via de
  *Vertex Color*-node. URP Lit negeert vertexkleuren.
- Materialen heten `M_<Asset>_<Deel>`. In `*.materials.json` staan per materiaal de ramp (5 tinten),
  `rampShift` en shaderhints (`flat`, `noShadow`, `emissive`) voor de toekomstige stylized-lit Shader Graph.

## Bekende beperkingen (eerlijk)

- **FBX is ASCII 7.4.** Een onafhankelijke parser (three.js FBXLoader) leest geometrie, hiërarchie, pivots en schaal
  correct. **Unity-import is nog niet getest.** Blender importeert geen ASCII-FBX; gebruik daar de `.glb`.
- Verborgen nodes zijn in de FBX gemarkeerd met `Visibility = 0`. FBXLoader negeert dat; Unity zou het moeten lezen
  via *Import Visibility*. Te controleren bij de eerste import.
- De `.mat`- en `.meta`-bestanden zijn met de hand geschreven YAML (URP Lit, GUID `933532a4…`). Unity vult
  ontbrekende velden aan. Roze materialen betekenen een verkeerde shaderreferentie: wijs dan *Universal Render
  Pipeline/Lit* toe.
- URP Lit met alleen een basiskleur benadert de look maar haalt hem niet: de warme/koele ramp, de rim en de
  vlakke ogen vragen de geplande stylized-lit Shader Graph (zie `Docs/ART_STYLE_GUIDE.md`, Lighting).

