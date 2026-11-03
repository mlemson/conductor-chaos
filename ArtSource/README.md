# ArtSource — reproduceerbare art-bronnen

Bronbestanden voor 3D-assets en ontwerpbladen. Dit staat bewust **buiten `Assets/`**, zodat Unity
het niet importeert. De Unity-assets in `Assets/Art/` worden hieruit gegenereerd.

> **Status (issue #9, P0 art proof):** conducteur-basismodel + wagonhoek-blockout + ontwerpbladen.
> De renders tonen de **doel-look** (ramp-shader). Een in-engine Unity-screenshot is nog **NOT RUN**.

## Wat staat waar

| Pad | Wat |
|---|---|
| `Characters/Conductor/build_conductor.py` | Conducteur-basismodel, parametrisch opgebouwd uit maten van het concept |
| `Train/WagonCorner/build_wagon_corner.py` | Wagonhoek-blockout (deur, slot, ramen, interieurstukje) + art-check-decor |
| `Tools/meshkit.py` | Kleine numpy-toolkit: lathe/superellips, afgeronde box, ellipsoïde, capsule; export naar FBX (ASCII) en glTF (.glb) |
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
- `Assets/Art/Models/Train/WagonCorner/WagonCorner_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Materials/...` URP Lit-materialen (middentoon van elke ramp)
- `Docs/Art/Conductor_DesignSheet.png`, `ArtCheck_WagonCorner.png`, `ArtCheck_vs_Concept.png`

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
- **Conceptstudie v4:** het lijf loopt door in ronde benen met een echte opening ertussen; het petembleem zit op de band en de armen lopen geleidelijk door in de handen. De lichaamsvorm vloeit nu geleidelijk in de benen over en gebruikt gladde normalen afgeleid van het onderliggende oppervlak, om strepen bij de benen en randen onder de pet te verminderen. De oudere ontwerpbladen en v2/v3-vergelijkingen tonen vorige modelversies; zie `Docs/Art/Conductor_ConceptStudy_v4.png` voor een voorlopige vormvergelijking, niet voor een Unity- of shadergoedkeuring. De huidige hoge meshdichtheid is een prototype en moet voor meerdere personages worden beoordeeld.
- `python ArtSource/Tests/test_conductor_body.py` controleert onder meer of het lijf een enkele gesloten mesh is en de ruimte tussen de benen echt open is.
- Hiërarchie conducteur: `Conductor › Body › (Neckline, Collar, Buttons, Eye_L/R › Pupil_L/R, Mouth_Neutral|Surprised|Happy, Cap, Arm_L/R › Hand_L/R › Prop_Key)`, plus lege `Leg_L/R`-ankers.
  Standaard verborgen: `Mouth_Surprised`, `Mouth_Happy`, `Prop_Key`.
- Wagonhoek: `DoorLeaf_L/R` schuiven langs lokale X (dicht = +0,475 / −0,475). `KeyLock` (0,72 m) en
  `DoorSill` zijn de interactie-ankers voor P0-05.
- **Geschilderde variatie** (zodat niets één effen kleur is) is per vertex gebakken in de vertexkleur
  (`Scene.bake_shading`): **R** = occlusie (hoeken, onder randen, onderkant iets donkerder), **G** = randlicht op
  afrondingen, **B** = verfvlekken (fBm, 0,5 = neutraal). De shader verschuift hiermee de positie in de kleurramp;
  daarbovenop komt een fijne penseelruis in wereldruimte. In Unity leest de toekomstige Shader Graph dit via de
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

