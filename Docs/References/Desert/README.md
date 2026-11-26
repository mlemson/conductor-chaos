# Woestijn — levels 6 en 7

Conceptbladen (aangeleverd door @mlemson, november 2026): `desert-concept-01..04.webp`. Rode gelaagde kloven met
een turquoise rivier en watervallen, zandkoppen op de rotsen, tafelbergen en hoodoos in de verte, houten
schraagbruggen, een rotsboog, windmolens, een goudmijn met lantaarns en ertskarretjes, cactussen met roze bloemen,
koeienschedels, gele bochtborden en een bizonbord. Latere bladen voegden toe: een stormende bizonkudde (met
kalveren) in een stofwolk, een kapotte schraagbrug, een losgeslagen mijnkarretje bij een wissel, een zandstorm met
tumbleweeds en een steenlawine achter bochtborden.

## Kleuren

Groep **Woestijn** in `Docs/References/palette.json`, gemeten uit deze bladen: `desert_rock` (rode kloofrots met
violette schaduw), `desert_band` (lichte zandsteenlaag), `desert_sand`, `desert_flower`. Cactus = `dino_green`,
bizon = `leather` + `cab_interior` (manen), kalf = `safari_hat`. Kliffen krijgen horizontale lagen via `_Strata`
in `StylizedRamp.shader` (alleen woestijnmaterialen).

## Assets

* `ArtSource/Environment/Desert/build_desert_kit.py` → `Assets/Art/Models/Environment/Desert/` (cactussen, planten,
  rotsen, hoodoos, rotsboog, tafelbergen, schedel, tumbleweed, windmolen met draaiende `Rotor`, mijningang,
  goudkarretje, mijntakel, bizonbord, bochtbord, vlag).
* `ArtSource/Animals/Bison/build_bison.py` → `Bison`, `Bison_Calf` (prefabs via *Build Animal Prefabs*).
* `ArtSource/Textures/build_ground_textures.py desert` → `T_Desert_*` (terreinlagen `TL_Desert_*`).

## Levels

* **Level 6 — Door de rode kloof** (Fernwood → Redrock, ± 650 m): vlakte met windmolens, rode wand met
  steenlawine, richel langs de diepe rode kloof, kapotte rail vóór de schraagbrug over het droge ravijn, rotsboog.
* **Level 7 — De goudmijnkloof** (Redrock → Goldspur, ± 850 m): twee kuddes, slingerrichel langs de goudkloof met
  bochtborden, twee schraagbruggen met kapotte rail, zandstorm met tumbleweeds, mijnwissel met mijnspoor naar de
  goudmijn en een losgeslagen ertskarretje van het laadperron.

Bouwen: *Tools > Conductor Chaos > Level 6 bouwen (woestijn)* / *Level 7 bouwen (goudmijn)*.

## Uitdagingen

* **Bizonstormloop** (server-autoritatief, `ServiceIncidents.Herd` + `PlaygroundAnimal`): de kudde wacht onzichtbaar
  en duikt pas op als de trein bij zijn huidige snelheid ongeveer tegelijk bij de oversteek zou zijn; dan stormt
  hij in galop met een stofwolk over het spoor. Geen HUD-melding vooraf. Wie remt laat de stroom voorbij; wie te
  hard doorrijdt (binnen ~3,5 s bij de oversteek) krijgt bizons die op de rails blijven staan: wegduwen (zwaar, met
  meer spelers) of weglokken met hooi.
* **Steenlawine** van de rode wanden, **tumbleweeds** in de zandstorm, **losgeslagen mijnkarretje**: zelfde
  mechaniek als de steenlawine (obstakels die de crew van het spoor duwt).
* **Kapotte rail** vóór elke schraagbrug, **scherpe bochten** op de richels (vol gas = ontsporen), **wisselstoringen**.
