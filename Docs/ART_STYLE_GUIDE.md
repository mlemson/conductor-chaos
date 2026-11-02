# 1. Art Style Guide – Conducteur / Train Co-op Game

**Visuele referentie:** de [conceptafbeelding](References/conductor-chaos-concept.png) is leidend; zie ook [referentie-instructies](References/README.md), de [stijlkaart v2](References/STYLE_PALETTE.svg) en [palette.json](References/palette.json). Alle kleurwaarden in deze gids zijn gemeten in die afbeelding. Deze gids is geen instructie om letterlijk de kleine locomotief van het beeld te kopiëren: de eerste gameplay betreft een begaanbare regionale reizigerstrein met deuren. Zie ook de [gamevisie](GAME_VISION.md).


## Werknaam stijl
**Stylized Cozy Low-Poly Cartoon**

## Korte stijlomschrijving
Een warme, speelse 3D-artstijl met simpele, ronde vormen, duidelijke silhouettes, matte materialen en een lichte klungelige charme. De wereld voelt toegankelijk en grappig, maar kan later ook spannend worden door bruggen, tunnels, bergen en chaotische situaties.

De stijl moet:
- **simpel** ogen
- **mooi leesbaar** zijn
- **niet te realistisch** worden
- **niet te futuristisch** aanvoelen
- ruimte geven aan **humor, physics en chaos**

---

## A. Kernprincipes

### 1. Leesbaarheid boven detail
Alles moet in één oogopslag begrijpelijk zijn:
- waar de deur zit
- wat interactief is
- waar gevaar zit
- wie conducteur / machinist / passagier is

### 2. Vorm is belangrijker dan textuur
De stijl komt vooral uit:
- silhouette
- kleurvlakken
- proporties
- lighting

Niet uit:
- gedetailleerde textures
- realistische oppervlakken
- kleine decoratieve rommel

### 3. Simpel, maar niet saai
Modellen mogen eenvoudig zijn, maar moeten karakter hebben door:
- goede proporties
- leuke vormen
- kleine herkenbare details
- humor in houding en animatie

### 4. Warm en charmant
De game moet visueel uitnodigend blijven, ook als de gameplay chaotisch wordt.

---

## B. Visuele identiteit

## Algemene sfeer
- gezellig
- licht komisch
- speels
- helder
- klein beetje klungelig
- bergachtig / avontuurlijk later in de game

## Stijlwoorden
Gebruik deze woorden als richting:
- chunky
- rounded
- playful
- readable
- matte
- cozy
- low-poly
- slightly clumsy
- friendly
- clean

---

## C. Character design rules

## Algemene vormtaal
Characters zijn:
- compact
- rond / chunky
- eenvoudig
- licht overdreven

## Proporties
Richtlijn:
- hoofd = relatief groot
- lichaam = klein en compact
- armen/benen = kort en simpel
- handen = eenvoudig, geen losse vingers nodig
- gezicht = minimaal detail

## Lichaamsvorm (uit het concept)
- boonvormig capsule-lijf: hoofd en romp zijn één doorlopende vorm, geen nek
- ca. 2 koppen hoog; hoofd ≈ 45% van de totale hoogte
- stompe armen zonder ellebogen, ronde mitten-handen
- korte blokvormige benen met brede schoenen

## Gezicht
- grote, bolle, ronde witte ogen (`#FBF0E3`) die elkaar **raken**; elk oog ≈ 35–40% van de lijfbreedte (samen ≈ 75%), licht verticaal ovaal; gemeten in het concept en zo gebouwd in het [conducteur-basismodel](Art/Conductor_DesignSheet.png)
- kleine, licht verticale pupillen (`#0D0B0C`, ≈ 25% van de oogbreedte) iets naar binnen/omhoog gericht, met één klein glimlicht
- mond: simpele ovale opening (mondholte en tong: ramp `mouth` in palette.json, `#50221A` → `#C15648`), geen tanden
- geen neus, oren of wenkbrauwen
- expressie via:
  - oogrichting en pupilgrootte
  - mondvorm (dicht, open, O-vorm)
  - hele lichaamshouding en hoofdkanteling

## Basismodel conducteur (P0 art proof, issue #9)
Maatvoering, gemeten in het concept en vastgelegd in het [ontwerpblad](Art/Conductor_DesignSheet.png) en het [art-check-beeld](Art/ArtCheck_Wagon.png):
- totaal **1,25 m** incl. pet; lijf 0,56 m breed (superellips-doorsnede, iets platte voorkant); kraaglijn 0,53 m; ogen op 0,835 m
- pet = silhouet-sleutel: bolle kroon ≈ 1,3× lijfbreedte, gouden band met rond embleem, donkere klep die net boven de ogen valt
- handen ≈ oog-grootte; schoudels op 0,49 m → **interactiepunten (sloten, knoppen) op ca. 0,6–0,8 m** zodat de korte armpjes erbij kunnen
- bron en regeneratie: [`ArtSource/`](../ArtSource/README.md); Unity-model `Assets/Art/Models/Characters/Conductor/Conductor.fbx`

## Variatie in personages
Werk vanuit **1 basismodel** met variaties:

Voorbeelden:
- conducteur: pet, uniform, badge
- machinist: iets ander uniform / werkuitstraling
- monteur: helm, gereedschap, hesje
- passagier toerist: rugtas, felle jas
- forens: neutrale kleding
- oudere passagier: bril, sjaal, andere houding

## Silhouette-regel
Elk type character moet herkenbaar zijn aan zijn vorm, niet alleen aan kleur.

---

## D. Animation style rules

## Animatiestijl
Animaties mogen licht onhandig en overdreven zijn:
- iets te grote bewegingen
- net niet perfecte balans
- beetje wiebel / sway
- lichte slapstick

## Belangrijke animatie-eigenschappen
- duidelijk en leesbaar
- snel herkenbaar
- niet te vloeiend/glad
- functioneel én grappig

## Voorbeelden
- lopen in een schuddende trein
- bijna uit balans raken
- koffer optillen met moeite
- deur openhouden met sleutel
- bukken om ticket op te rapen
- struikelen / stoten
- handen uitstrekken bij remmen

---

## E. Omgevingsstijl

## Omgevingen
Omgevingen bestaan uit simpele grote vormen met weinig micro-detail.

### Basisregels
- grote duidelijke volumes
- geen overmatig kleine props
- herkenbare landmarks
- alles moet op afstand leesbaar blijven

### Stations
- klein, vriendelijk, herkenbaar
- bankjes, lantaarns, bordjes, afvalbakken
- simpele architectuur
- rustig kleurgebruik

### Spoor / landschap
- duidelijke spoorlijn
- grote leesbare bruggen
- eenvoudige tunnels
- bergen en rotsen in gestileerde vormen
- bomen als simpele volumes

### Interieur trein
- duidelijk gangpad
- grote simpele stoelen
- handgrepen / stangen
- duidelijke deurmechanieken
- bagagerekken eenvoudig en stevig leesbaar

---

## F. Props

Props moeten:
- chunky zijn
- direct herkenbaar zijn
- weinig detail hebben
- gameplay ondersteunen

Voorbeelden:
- sleutel
- fluit
- kaartjes
- scanner
- koffers
- gereedschap
- kratten
- signaallamp
- brandblusser
- hendels en bedienknoppen

### Regel
Een prop moet al herkenbaar zijn als silhouette, nog vóór textuur of detail.

---

## G. Materialen

## Materiaalrichting
- mat tot satijn: zacht "klei/vinyl-speelgoed"-gevoel, brede zachte highlight, geen scherpe spiegeling
- **low-poly met effen vlakken** (besluit @mlemson): elk vlak/facet heeft één effen tint, bepaald door zijn richting (boven licht, zijkant midden, onder donker). Variatie komt van **grote vormen**: facetten, afschuiningen en ingezette panelen, niet van ruis, vlekken of penseeltextuur. Alleen een zachte occlusie in hoeken en naar de onderkant (vertexkleur R) en lichte randjes (G). Trein en decor: harde normalen; personages blijven glad. Zie [ArtSource](../ArtSource/README.md)
- geschilderde **inzetpanelen**: een iets donkerder vlak (dezelfde ramp, lagere positie) zonder lichte rand, zoals op de concept-loc. Gebruik ze spaarzaam op grote wanden, eventueel met gouden klinknagels
- koele rimlight langs de randen (zie Lighting)
- geen heavy realism

## Vermijd
- hoge metallic
- scherpe reflecties
- drukke normal maps
- fotorealistische oppervlaktes

## Toegestaan
- subtiele gradients
- lichte noise
- heel simpele textuurdetails
- zachte AO
- clean surfaces

---

## H. Kleurgebruik

## Algemene kleurstrategie
Gebruik een **beperkt en consistent kleurenpalet**. Alle waarden komen uit de [stijlkaart](References/STYLE_PALETTE.svg) / [palette.json](References/palette.json); elk materiaal heeft een ramp van 5 tinten (schaduw → licht). Gebruik de ramps, verzin geen losse tinten.

### Kernpalet (schaduw · midden · licht)
| Materiaal | Schaduw | Midden | Licht |
|---|---|---|---|
| Huid | `#BA7756` | `#DB9966` | `#EFB27C` |
| Conducteur-uniform & pet | `#1D1C2D` | `#3E3D70` | `#565191` |
| Goud (petband, knopen) | `#9D662F` | `#DA993A` | `#FCCF53` |
| Bouwhelm | `#B57326` | `#F5B038` | `#FDC443` |
| Veiligheidshesje | `#B5531F` | `#E2722B` | `#EA7931` |
| Muts passagier | `#5A457E` | `#7A59B0` | `#B17BDD` |
| Sjaal | `#783638` | `#A44543` | `#B64B48` |
| Treinrood | `#5E271F` | `#C54D3E` | `#FC7C69` |
| Koplamp / warm licht | `#F9DA7C` | `#FEECA1` | `#FFF8DA` |
| Lucht | `#6E83BA` | `#85A4EC` | `#98C4FC` |
| Bergrots | `#5D6EAC` | `#747FB3` | `#878EBC` |
| Dennen | `#2B3B37` | `#3D5655` | `#5B7A7A` |
| Gras & mos | `#566D3A` | `#8B9F3B` | `#ADBC4A` |
| Klifrots (nabij) | `#6A4C4B` | `#AC7F75` | `#CA957A` |
| Bielzen / schragen | `#885642` | `#DB945C` | `#F4B06C` |
| Water | `#6B8CB6` | `#8EC7EC` | `#ADD7ED` |
| Interieurhout | `#6C422A` | `#8D543A` | `#E59843` |
| Kratten & koffers | `#684029` | `#DE913F` | `#F9B44F` |
| Schaapwol | `#BDABC1` | `#DCC7C5` | `#FDF6E8` |

UI & logo: achtergrond `#33303B`, perkament `#F4DCC0` (schaduw `#CAA791`), inkt `#351B13`, logo-rood `#CD4C2C`, accentgeel `#FABF34`.

### Patronen uit het concept
- schaduwen schuiven naar blauw-violet, nooit naar grijs/zwart; alleen huid houdt een warme schaduw
- dennen zijn **blauwgroen**, gras is **geelgroen**: dat contrast hoort bij de stijl
- verre bergen en rotsen zijn blauw-violet; alleen nabije kliffen zijn warm
- witte materialen (sneeuw, wol, wolken) hebben lavendel schaduwen
- geen pure `#000000` of `#FFFFFF`, ook niet in pupillen of tekst
- hoofdtonen ≈ 45–70% saturatie; fel en licht alleen voor lampen, gevaar en interactie

## Richtlijnen
- gebruik kleur vooral voor hiërarchie en leesbaarheid
- niet te veel verzadigde kleuren tegelijk
- accentkleuren spaarzaam inzetten

## Kleurfuncties
- blauw = personeel / vertrouwen
- geel = interactie / waarschuwing
- rood = gevaar / probleem
- groen = veilige rustige startgebieden

---

## I. Lighting

## Doel
Licht moet warmte, sfeer en leesbaarheid geven.

## Richtlijnen
- 1 duidelijke directional light (zon), warm perzik, van linksboven
- zachte schaduwen, getint naar blauw-violet
- koel invullicht uit de lucht, warme kaatsing van de grond
- smalle koele rimlight op personages en trein
- atmosferisch perspectief: verder weg = blauwer en lichter
- geen harde fotorealistische contrasten

## Startwaarden Unity (URP)
| Instelling | Waarde | Toelichting |
|---|---|---|
| Directional light | `#FFE3BC`, intensiteit ≈ 1.2 | ≈ 40° van linksboven |
| Ambient (Gradient) | sky `#85A4EC`, equator `#C7D0F0`, ground `#6A4C4B` | koele vulling, warme bodem |
| Schaduwtint / toon-ramp | `#5D5A8C` | huid-shader houdt warme schaduw `#BA7756` |
| Fog | `#91A8E6`, lineair, start ver weg | alleen voor verre bergen |
| Rim light | `#B2C5F3`, sterkte ≈ 0.3 | smalle fresnel |
| Lampen | `#FEF2B0` emissive | lichte bloom (threshold ≈ 1.0, lage intensiteit) |

Aanpak: één gedeelde stylized-lit shader (Shader Graph) met zachte 2–3-staps lichtramp, schaduwtint en rim, plus per materiaal een basiskleur uit de ramp. Eerst valideren in de art-test (één conducteur + één wagon) voordat er meer assets komen. Dit zijn startwaarden: stem af door de Unity-screenshot naast de conceptafbeelding te leggen.

## Per type gebied
### Startgebieden
- warm
- helder
- uitnodigend

### Berggebieden
- iets koeler
- dramatischer
- nog steeds stylized

### Tunnels
- duidelijke overgang tussen donker en licht
- warme lampen / signalen
- spannender sfeer

### Gevaarsituaties
- knipperlicht / waarschuwingskleuren beperkt maar duidelijk
- niet te veel visuele ruis

---

## J. UI / HUD stijl

## Richting
UI moet voelen alsof het in dezelfde wereld thuishoort.

### Kenmerken
- afgeronde panelen
- simpele iconen
- lichte schaduw
- grote leesbare tekst
- speelse, niet futuristische uitstraling

### Vermijd
- glazen sci-fi HUD
- extreem minimalistisch zwart-wit
- technische sim-interface
- te veel kleine tekst

### Wel doen
- taaklijst met duidelijke iconen
- interactieprompts eenvoudig
- waarschuwingen kort en groot
- pictogrammen boven tekst waar mogelijk

---

## K. Camera / presentatie

## Camera moet:
- de humor ondersteunen
- interacties goed leesbaar houden
- chaos tonen zonder onduidelijk te worden

### In gameplay
- duidelijk zicht op handen/interacties
- goede framing in coupé
- niet te “realistisch shaky”
- als er chaos is, moet het nog speelbaar blijven

---

## L. Audio-visuele afstemming
Hoewel dit een art guide is, moet de visuele stijl aansluiten op:
- komische geluiden
- simpele feedback
- duidelijke signalen
- niet te zware “serieuze simulator”-toon

---

## M. Stijlregels in 1 lijst
Iedere artiest / developer moet deze regels volgen:

1. **Maak het leesbaar, niet druk**
2. **Gebruik simpele grote vormen**
3. **Hou materialen mat en stylized**
4. **Werk met een beperkt kleurenpalet**
5. **Characters zijn rond, simpel en expressief**
6. **Animatie mag klungelig en grappig zijn**
7. **Props moeten direct herkenbaar zijn**
8. **De wereld voelt warm en charmant**
9. **HUD/UI mag speels zijn, niet futuristisch**
10. **Gameplay-clarity gaat altijd vóór decoratieve detailrijkdom**

---

## N. Korte prompt voor andere artists/agents
Je kunt deze ook direct hergebruiken:

> Create art for a stylized cozy low-poly co-op train game with chunky bean-shaped cartoon characters (head and body one shape, big round white eyes, tiny pupils, no nose or eyebrows), rounded shapes, soft matte-to-satin materials, warm peach key light with cool blue-violet shadows and a subtle cool rim light, simple readable props, and a playful slightly clumsy tone. The world should feel charming and inviting, with clear silhouettes, limited color palettes, soft shadows, and low-detail but expressive environments. Avoid realism, avoid overly glossy materials, avoid futuristic UI, and prioritize readability, humor, and strong visual identity.

---
---

# 3. Multiplayer — MUST HAVE (P0)

Multiplayer-first: één gedeelde, server-authoritatieve wereld voor 1–6 spelers, met singleplayer als ondersteund pad. De eerste technische test heeft twee spelers; vier spelers is P1 en zes het beoogde maximum. Spelers moeten kunnen samenwerken en elkaar fysiek hinderen, taken gelijktijdig uitvoeren en dezelfde trein, deuren, passagiers en gebeurtenissen zien. Deuren, sleutels, scanners en gedeelde voorwerpen worden centraal gevalideerd. Voorwerpen kunnen worden opgepakt, gedragen, doorgegeven en losgelaten; treinbeweging en physics blijven consistent. Ontwerp uitnodigingen, disconnect en reconnect zorgvuldig. Multiplayer wordt vanaf de eerste speelbare versie getest.

Visuele interactie-, animatie- en physicsfeedback moet leesbaar blijven voor alle spelers. Klungelige beweging is gewenst; netwerkjitter en uiteenlopende werelden niet. Zie [architectuur](ARCHITECTURE.md), [roadmap](ROADMAP.md) en [testplan](TESTPLAN.md).
