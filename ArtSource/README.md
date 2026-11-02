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
| `Train/Tender/build_tender.py` | Kolenwagen direct achter de loc (rode bak, gouden rand, panelen met klinknagels, kolenberg, ladder, sluitlampen) — issue #22 |
| `Tools/meshkit.py` | Kleine numpy-toolkit: lathe/superellips, afgeronde box, ellipsoïde, capsule, profielextrusie (ketel/dak); export naar FBX (ASCII) en glTF (.glb) |
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
- `Assets/Art/Materials/...` URP Lit-materialen (middentoon van elke ramp)
- `Docs/Art/Conductor_DesignSheet.png`, `ArtCheck_Wagon.png`, `ArtCheck_vs_Concept.png`, `Locomotive_DesignSheet.png`

## Opnieuw bouwen

```
python ArtSource/build_all.py --models   # alleen FBX/GLB/.mat (Python 3 + numpy)
python ArtSource/build_all.py            # + renders en ontwerpbladen
```

Voor renders: Node.js met Playwright (Chromium) en three.js **r187** in `ArtSource/Render/three` (staat in `.gitignore`):

```
git clone --depth 1 --branch r187 https://github.com/mrdoob/three.js ArtSource/Render/three
```

Pas vormen of maten aan in de `build_*.py`-scripts, niet in de FBX. Kleuren komen altijd uit
`Docs/References/palette.json`; nieuwe tinten eerst meten en toevoegen via
`Docs/References/tools/build_style_card.py`.

## Conventies

- Y omhoog, het model kijkt naar **+Z**, 1 unit = **1 meter**. De linkerkant van een personage ligt op +X.
- **Starre poppen-rig:** elk onderdeel is een eigen node met een pivot op het gewricht (heup, schouder, pet, ogen).
  Animatie = transforms roteren. Dit past bij de stijve, klungelige stijl en is eenvoudig te synchroniseren.
  Er is geen skinning; die volgt alleen als een animatie erom vraagt.
- Hiërarchie conducteur: `Conductor › Body › (Collar, Buttons, Eye_L/R › Pupil_L/R, Mouth_Neutral|Surprised|Happy, Cap, Arm_L/R › Hand_L/R › Prop_Key)`, `Leg_L/R`.
  Standaard verborgen: `Mouth_Surprised`, `Mouth_Happy`, `Prop_Key`.
- **Uit één stuk:** gebruik `meshkit.frame_ring` (kader met verstekhoeken), `meshkit.hollow_box` (holle bak met dagkanten) en `meshkit.prism` (stapvormig profiel). Laat geen balken stomp tegen elkaar eindigen; een lijst die op een andere lijst aansluit, eindigt daarin.
- Doorloopbaar (issue #24): de cabine-achterdeur, de kolenwagen (`FallPlate`, dekken, `Coal`) en de kopse doorgangen van de wagon zijn de ankers voor latere colliders.
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
