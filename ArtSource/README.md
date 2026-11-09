# ArtSource — reproduceerbare art-bronnen

Bronbestanden voor 3D-assets en ontwerpbladen. Dit staat bewust **buiten `Assets/`**, zodat Unity
het niet importeert. De Unity-assets in `Assets/Art/` worden hieruit gegenereerd.

> **Status (issue #9, P0 art proof):** conducteur-basismodel + regionale wagon (blockout) + ontwerpbladen.
> De renders tonen de **doel-look** (ramp-shader). Een in-engine Unity-screenshot is nog **NOT RUN**.

## Wat staat waar

| Pad | Wat |
|---|---|
| `Characters/Conductor/build_conductor.py` | Conducteur-basismodel, parametrisch opgebouwd uit maten van het concept |
| `Characters/character_kit.py` | Gedeeld kit voor personagevarianten: zelfde lijf/ogen/monden/armen als de conducteur + oppervlakte-helpers (zomen, banden, gordels, decals om het lijf), expressies en export |
| `Characters/ElderlyPassenger/build_elderly_passenger.py` | Oudere passagier: krullend grijs haar, baret met bloem, brilletje, sjaal, pruimkleurige jas, draagtas |
| `Characters/Tourist/build_tourist.py` | Toerist: safarihoed met zonnebril, hawaïhemd met print, korte broek, rugzak met slaapmatje en fles, kaart + kaartje |
| `Characters/RailMechanic/build_rail_mechanic.py` | Spoormonteur: bouwhelm met lamp, vastberaden oogleden, overall met reflectieband, bretels, gereedschapsgordel, handschoenen |
| `Characters/StationMaster/build_station_master.py` | Stationschef: stationspet met embleem, krulsnor + witte wenkbrauwen, uniformjasje met gouden biezen, gilet, zakhorloge |
| `Characters/BusinessTraveler/build_business_traveler.py` | Zakenreiziger: colbert met das, kuif met bril, schoudertas met papieren, rolkoffer (`Prop_Suitcase`), zweetdruppels |
| `Characters/ChildPassenger/build_child_passenger.py` | Ondeugend kind (80% schaal): achterstevoren pet, hoodie, rugzak met dino, papieren vliegtuigje (`Prop_Plane`), scheve grijns |
| `Characters/BaggageHelper/build_baggage_helper.py` | Bagagehelper (tuinbroek, pet, handschoenen) + losse `BaggageTrolley` met koffers en `Handle`-anker |
| `Train/RegionalWagon/build_regional_wagon.py` | Regionale wagon in facetstijl (deur, slot, grepen, ramen, draaistellen, koplampen) + art-check-decor |
| `Train/Locomotive/build_locomotive.py` | Locomotief v2.1 (ronde ketelneus, rookkastring met lamp, gewelfd dak, sierlijsten, drijfstang) — issue #22. Cabine v3: brede lessenaar met startknop, vuurdeur, hittemeter (`HeatNeedle`), verticale trekhendel in een sleuf met pijlenbordje, plafondlamp (`CabLamp`); geen kachelpijp en kolenbak meer |
| `Train/CattleWagon/build_cattle_wagon.py` | Veewagen: rode kooi uit één stuk om een lattenwand met echte kieren, schuifdeuren, gewelfd dak, interieur (stro, hooibalen), doorloopbaar — issue #25 |
| `Train/Tender/build_tender.py` | Kolenwagen direct achter de loc (rode bak, gouden rand, panelen met klinknagels, kolenberg, ladder, sluitlampen) — issue #22. Open kolenbak vol losse brokken (`CoalStock` › `CoalChunk*`, rustposities uit `coal_pile.json`, opnieuw te maken met `settle_coal.py` + pybullet) en `CoalScoopPoint`; geen vaste kolenberg meer |
| `Environment/build_environment_kit.py` | Modulaire natuurkit (issue #30): bomen, struik, rotsen, klifmodules + klifclusters (v2), grasheuvel, stilstaande watervallen, verre bergen, wolken; elk asset een eigen FBX + GLB, gedeelde materialen |
| `Environment/build_track_kit.py` | Spoorkit (issue #53): rechte stukken, bochten R20 15°/30° links/rechts, stootblok, kapot stuk (herstelbaar met de moersleutel: `Broken_Bits`/`Fixed_Rail`/`Repair_Bolt`, #215), houten schraagbrug, waarschuwingsbord en slagboom. Sockets voor aansluiten; prefabs + Track Builder in Unity (`Conductor Chaos > Art > Track`) |
| `Environment/build_switch_kit.py` | Spoorwissel (issue #214): `Track_Switch_20m_A/B` (Unity: Left/Right), rechtdoor 20 m + S-bocht naar een parallel spoor 5,36 m opzij; tongen (`Blade_S/D`), trekstang, puntstuk, wisselbok met hendel en richtingsschijf, kapotte delen en `Repair_Bolt`. Unity: `TrackSwitch` + `RepairPoint`; TrainDrive volgt de gekozen route en ontspoort op een kapotte wissel |
| `Textures/build_ground_textures.py` | Tegelbare grondtextures voor het Unity-terrein (gras, bloemetjes, grond, stenen pad, rots), issue #38 |
| `Tools/meshkit.py` | Kleine numpy-toolkit: lathe/superellips, afgeronde box, ellipsoïde, capsule, profielextrusie (ketel/dak); export naar FBX (ASCII) en glTF (.glb) |
| `Tools/spatial_fallback.py` | NumPy-terugval voor de straalzoekopdracht bij het bakken van vertexkleur als SciPy ontbreekt |
| `Tools/implicit_body.py` | Bouwt het lijf met broek en voeten als één verbonden oppervlak met een echte opening tussen de benen |
| `Tools/unity_assets.py` | Schrijft URP Lit-materialen (`.mat`) en `.meta`-bestanden met stabiele GUID's |
| `Render/render.html`, `render.cjs` | Headless stijlrender (three.js + Playwright) met de stylized ramp-shader |
| `Render/scenes/*.json` | Camera's, poses en expressies van alle renders |
| `Render/make_sheets.py` | Stelt `Docs/Art/*.png` samen |
| `Environment/Station/` | Stationnetje + perronkit + props (`build_station_kit.py`; gebouw, perronmodules, props en gedeelde helpers in `station_*.py`) |
| `Render/station_scenes.py`, `station_sheets.py` | Renderscènes, Unity-voorbeeldlayout (`Assets/Art/Layouts/StationExample.json`) en ontwerpbladen van het station |
| `Render/character_scenes.py`, `character_sheets.py` | Renderscènes en ontwerpbladen van de personagevarianten (+ crew-lineup en scène bij de trein) |
| `Exports/` | `.glb`-versies (voor Blender/viewers) en `*.materials.json` (ramps per materiaal) |
| `build_all.py` | Alles in één keer: modellen → renders → bladen |

Gegenereerde uitvoer:

- `Assets/Art/Models/Characters/Conductor/Conductor.fbx` (+ `.meta`)
- `Assets/Art/Models/Characters/{ElderlyPassenger,Tourist,RailMechanic,StationMaster,BusinessTraveler,ChildPassenger,BaggageHelper,BaggageTrolley}/*.fbx` (+ `.meta`) + `Assets/Art/Materials/Characters/<naam>/*.mat`
- `Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/Tender/Tender_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Train/CattleWagon/CattleWagon_Blockout.fbx` (+ `.meta`)
- `Assets/Art/Models/Environment/{Trees,Rocks,Cliffs,Mountains,Sky}/*.fbx` + `Assets/Art/Materials/Environment/*.mat`
- `Assets/Art/Materials/...` URP Lit-materialen (middentoon van elke ramp)
- `Docs/Art/ElderlyPassenger_DesignSheet.png`, `Tourist_DesignSheet.png`, `RailMechanic_DesignSheet.png`, `StationMaster_DesignSheet.png`, `BusinessTraveler_DesignSheet.png`, `ChildPassenger_DesignSheet.png`, `BaggageHelper_DesignSheet.png`, `Characters_Lineup.png`, `Characters_InScene.png`
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
- **Stationnetje & perronkit** (issue #182, `Environment/Station/`): `Station_Building` (begaanbaar: hal met loket en wachtruimte, luifel; vloer op perronhoogte; knopen `Door` + `Col_Door` met scharnier, `RoofFrame`, `Light_*`-ankers), perronmodules `Platform_*` en 16 props `Prop_*`. Perronmodules gebruiken het spoorraster: pivot = hart spoor op maaiveld, lopen langs +X vanaf x = 0, perron aan +Z, bovenkant 1,05 m (= bodem wagonbak), voorrand 1,65 m (loc steekt onder perronhoogte 1,58 m uit; `Tests/test_station_kit.py` controleert dat met de echte treinmodellen). Eindstukken bestaan als `_Start`/`_End`. Kleuren: palette-groep *Station & perron* (gemeten met `Docs/References/tools/measure_station_ramps.py`) plus bestaande ramps. Unity: `Conductor Chaos > Art > Station` maakt prefabs (Col_* -> MeshCollider, Light_* -> puntlicht) en een voorbeeldscène.
- **Hoofdstation met briefingruimte** (issue #207, `Environment/Station/main_station.py` + `main_station_props.py`): `Station_Main` = kopstation met klokgevel, twee (dichte) vleugels en perronluifel; de hal (9,1 × 8,5 m binnen) is de briefingruimte. Knopen: `Door_L`/`Door_R` (dubbele deur, scharnier op de Y-as, elk met `Col_Door_*`), `Col_Main`, `Light_*` (lantaarns, waaierraam, luifel, `Light_Pendant_1..6`), naambord `Sign_Name` (WHISTLETON) en routebord `Sign_Route` (ROUTE), en lege gameplay-ankers `Spawn_Briefing`, `Briefing_Board`, `Briefing_Seat_1..8`, `Spot_Coffee`, `Spot_Lockers`. Losse props voor hergebruik: `Prop_Lockers`, `Prop_RouteBoard`, `Prop_NoticeBoard`, `Prop_CoffeeCounter`, `Prop_CoatStand`, `Prop_TicketBooth`, `Prop_Banner`, `Prop_PendantLamp`. Bouwen: `python ArtSource/Environment/Station/build_station_kit.py main briefing`.
- **Gereedschap** (issues #215, #216, `Environment/Station/tool_props.py`, bouwen: `build_station_kit.py tools`): `Prop_Wrench` (moersleutel, `Wrench_Head`), `Prop_TicketPunch` (kaartjestang, `Punch_Jaw`) als beweegbare netwerkprops met `RepairWrench`/`TicketPunch`, en `Prop_Ticket` (kaartje, `Ticket_Hole`) voor `PassengerTicket`. In de TrainPlayground: `Tools > Conductor Chaos > Place Wrench, Ticket Punch & Tickets`.
- **Aanpasbare stationsborden** (issue #209): knopen `Sign_*` (met `_Icon` en `_Text`) in `Station_Main`, `Station_Building` en `Prop_StationSign`. De bordletters staan als losse meshes in `Assets/Art/Models/Environment/SignGlyphs/StationGlyphs.fbx` (kaphoogte 1, leesbaar); `StationGlyphs.json` bevat letterbreedtes en de maten van elk bord. In Unity zet de prefabbouwer `StationSign` (Assets/Scripts/Stations) op elk bord: naam intypen of een voorbeeldnaam kiezen, de tekst wordt opgebouwd zoals `station_board_parts()`.
- **Personagevarianten** (oudere passagier, toerist, spoormonteur, stationschef, zakenreiziger, kind, bagagehelper) gebruiken exact het lijf, de ogen, monden en armen van de conducteur via `Characters/character_kit.py`; alleen kleding, haar/hoofddeksel en props verschillen. Hiërarchie: `<Naam> › Body › (kleding/haar, Eye_L/R › Pupil_L/R, EyeClosed_L/R, Lid_L/R, Blink_L/R, Mouth_Neutral|Surprised|Happy|Flat|Hmm|Smile (kind ook Grin), hoofddeksel, Arm_L/R › Hand_L/R › Prop_*)`, plus `Leg_L/R`. Standaard verborgen: alle monden behalve één (monteur `Mouth_Flat`, stationschef `Smile`, kind `Grin`, bagagehelper `Happy`, anderen `Mouth_Neutral`), `EyeClosed_*`, `Blink_*` (knipperen), `Lid_*` (monteur, stationschef en kind: zichtbaar) en `Prop_Sweat`. Kleuren: palette-groep *Reizigers & crew*, gemeten in `Docs/References/Characters/*-concept.png` met `Docs/References/tools/measure_character_ramps.py`. De Unity-showcase (`Conductor Chaos > Art > Build Showcase Scene`) zet ze naast het spoor, met `CharacterFace` (oogstand/mond wisselen, af en toe knipperen); voor andere scènes: `Tools > Conductor Chaos > Characters > Add Face (Blinking) To Scene Characters`. De conducteur heeft dezelfde oogknopen. `python ArtSource/Tests/test_characters.py` controleert knopen, zichtbaarheid, palette-ramps, grondcontact, budget, de bagagekar en (voor de nieuwe personages) dubbele vlakken.
- `python ArtSource/Tests/test_conductor_finish.py` controleert de vrije gouden band en de aansluiting van mouwen op het uniform.
- Hiërarchie conducteur: `Conductor › Body › (Neckline, Collar, Buttons, Eye_L/R › Pupil_L/R, Mouth_Neutral|Surprised|Happy, Cap, Arm_L/R › Hand_L/R › Prop_Key)`, plus lege `Leg_L/R`-ankers.
  Standaard verborgen: `Mouth_Surprised`, `Mouth_Happy`, `Prop_Key`.
- **Uit één stuk:** gebruik `meshkit.frame_ring` (kader met verstekhoeken), `meshkit.hollow_box` (holle bak met dagkanten) en `meshkit.prism` (stapvormig profiel). Laat geen balken stomp tegen elkaar eindigen; een lijst die op een andere lijst aansluit, eindigt daarin.
- Doorloopbaar (issues #24 en #155): het opstapje achter de cabinedeur zit in de `Frame`-mesh. De kolenwagen heeft een vast voordek op cabinevloerhoogte. De dekken, `Coal` en de kopse doorgangen van de wagon blijven afzonderlijk toegankelijk.
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

