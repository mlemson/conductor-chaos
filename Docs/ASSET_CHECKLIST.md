# 2. Unity Asset Checklist – Conducteur Game

> **Let op:** 'Must Have' hieronder betekent nodig voor de volledige gewenste game, **niet** dat alles vóór de eerste technische P0-test gebouwd moet worden. De P0-test gebruikt tijdelijke geometrie, terwijl één kleine art-direction test parallel kan plaatsvinden. Zie [gamevisie](GAME_VISION.md) en [roadmap](ROADMAP.md). Gebruik de [visuele referenties](References/README.md) voor art-consistentie.


Hieronder een praktische checklist.  
Ik deel hem op in **Must Have**, **Should Have Later** en **Nice to Have**.

---

# A. Core project setup

## Must Have
- [ ] Unity project in **URP**
- [ ] basis scene structuur
- [ ] foldersysteem netjes opgezet
- [ ] material library
- [ ] prefab structuur
- [ ] input system keuze
- [ ] layer/tag systeem
- [ ] lighting test scene
- [ ] style reference board in project/documentatie

## Aanbevolen mappen
- [ ] `Art/Models/Characters`
- [ ] `Art/Models/Environment`
- [ ] `Art/Models/Props`
- [ ] `Art/Materials`
- [ ] `Art/Textures`
- [ ] `Animations`
- [ ] `Prefabs`
- [ ] `Scenes`
- [ ] `UI`
- [ ] `Scripts`
- [ ] `Audio`

---

# B. Character assets

## Must Have
### Playable / core roles
- [ ] conducteur basismodel
- [ ] machinist basismodel
- [ ] monteur/werkman basismodel
- [ ] passagier basismodel

### Character visuals
- [ ] basis rig
- [ ] simpele gezichtsstijl
- [ ] uniform variant
- [ ] helm variant
- [ ] pet variant
- [ ] rugtas / bagage varianten
- [ ] kleurvariaties outfits

### Basisanimaties
- [ ] idle
- [ ] walk
- [ ] run
- [ ] turn / pivot
- [ ] interact
- [ ] pick up
- [ ] carry object
- [ ] use door
- [ ] stumble / lose balance
- [ ] reaction / surprise

## Should Have Later
- [ ] specifieke passagierstypes
- [ ] oudere passagier
- [ ] toerist
- [ ] boze passagier
- [ ] kind / familiefiguur indien passend
- [ ] extra accessoires
- [ ] emote animaties
- [ ] seated poses
- [ ] fall / ragdoll-achtige reactie

---

# C. Train assets

## Must Have
### Exterior
- [ ] locomotief/front car
- [ ] passagierswagon
- [ ] wagon connector
- [ ] wiel/bogie simplificatie
- [ ] deur exterieur
- [ ] ramen
- [ ] koplampen / signaallampen

### Interior
- [ ] wagon interior shell
- [ ] stoelen module
- [ ] stoelenrij links
- [ ] stoelenrij rechts
- [ ] middenpad
- [ ] deur binnenzijde
- [ ] bagagerek
- [ ] handgrepen/stangen
- [ ] cabine / bestuurdersruimte basic

### Interactieve treinonderdelen
- [ ] deurmechanisme
- [ ] sleutel-slot interactiepunt
- [ ] noodrem / knop
- [ ] eenvoudige cockpit controls
- [ ] koppelstuk / wagonverbinding
- [ ] signaalpaneel / lampje

## Should Have Later
- [ ] meerdere wagonvarianten
- [ ] goederenwagon
- [ ] servicedeel / onderhoudswagon
- [ ] open wagon / speciale challenge wagon
- [ ] kapotte deur variant
- [ ] losse panelen / reparatiepunten

---

# D. Environment assets

## Must Have
### Stations
- [ ] klein station gebouw
- [ ] perron module
- [ ] bankje
- [ ] lantaarn
- [ ] stationsbord
- [ ] afvalbak
- [ ] hekwerk simpel
- [ ] wachthokje

### Spooromgeving
- [ ] rechte rails module
- [ ] bocht rails module
- [ ] wissel / junction visual
- [ ] seinpaal
- [ ] simpele paaltjes / borden
- [ ] ballast / ondergrond
- [ ] tunnel ingang
- [ ] brug segment
- [ ] viaduct segment

### Natuur
- [ ] rots 1
- [ ] rots 2
- [ ] rots 3
- [ ] heuvel / terrein chunks
- [ ] boom type 1
- [ ] boom type 2
- [ ] struik
- [ ] grasvlak / ground variation

## Should Have Later
- [ ] bergwanden
- [ ] sneeuwvariant
- [ ] regen/storm ambience props
- [ ] beek/waterval
- [ ] dorpse huisjes
- [ ] waarschuwingshekken
- [ ] omgevallen rots / obstacle assets
- [ ] houten brugvarianten
- [ ] cliff edge details

---

# E. Props & interactables

## Must Have
### Conducteur tools
- [ ] conducteurssleutel
- [ ] kaartjescanner
- [ ] fluit
- [ ] checklijst / clipboard
- [ ] zaklamp (indien relevant)

### Passenger / wagon props
- [ ] koffer klein
- [ ] koffer groot
- [ ] rugtas
- [ ] doos / krat
- [ ] ticket / kaartje
- [ ] verloren voorwerp
- [ ] gereedschapskist

### Repair / problem props
- [ ] moersleutel
- [ ] brandblusser
- [ ] kabel/slang
- [ ] lek-punt / stoom-effect anchor
- [ ] kapot paneel
- [ ] barricade / waarschuwingsbord

## Should Have Later
- [ ] dieren op spoor
- [ ] losse lading
- [ ] emmers / schoonmaakitems
- [ ] signaallantaarn
- [ ] generator / power unit
- [ ] noodpakket

---

# F. UI assets

## Must Have
- [ ] hoofdmenu layout
- [ ] knopstijl
- [ ] checkbox / objective style
- [ ] icoonset basis
- [ ] interactieprompt visual
- [ ] warning pop-up style
- [ ] inventory/held item slot
- [ ] task list panel
- [ ] pause menu
- [ ] basic settings menu

### Nodige iconen
- [ ] ticket
- [ ] sleutel
- [ ] fluit
- [ ] gereedschap
- [ ] passagier
- [ ] deur
- [ ] waarschuwing
- [ ] brug
- [ ] tunnel
- [ ] snelheid
- [ ] trein
- [ ] checkmark / fout / alert

## Should Have Later
- [ ] route map UI
- [ ] level progression cards
- [ ] multiplayer player indicator
- [ ] emote wheel
- [ ] result screen
- [ ] unlock / progression UI

---

# G. VFX

## Must Have
- [ ] stoom / kleine rook
- [ ] stof puff
- [ ] deur open/sluit feedback
- [ ] kleine impact puff
- [ ] vonkjes voor defect
- [ ] warning blink
- [ ] rem-effect visueel
- [ ] interact highlight

## Should Have Later
- [ ] sneeuw
- [ ] regen
- [ ] wind gusts
- [ ] rotsval stof
- [ ] tunnel atmosphere
- [ ] lamp flicker
- [ ] grotere failure effects

---

# H. Audio placeholder checklist
Niet direct art, maar wel handig voor de slice.

## Must Have
- [ ] voetstappen
- [ ] deurgeluid
- [ ] sleutel draaien
- [ ] scanner beep
- [ ] fluit
- [ ] trein rijden
- [ ] remmen
- [ ] ambient station
- [ ] warning sound
- [ ] passenger chatter placeholder

---

# I. Scenes checklist

## Must Have – eerste vertical slice
- [ ] main menu scene
- [ ] art style test scene
- [ ] small station scene
- [ ] wagon interior gameplay test
- [ ] short route exterior test

## Should Have Later
- [ ] bridge scene
- [ ] tunnel scene
- [ ] mountain route scene
- [ ] bad weather scene
- [ ] chaos challenge scene

---

# J. Vertical slice: minimale assetset
Als je klein wilt beginnen, dan zou ik **echt eerst alleen dit bouwen**:

## Characters
- [ ] conducteur
- [ ] 3 passagiersvarianten
- [ ] 1 machinist/werkman

## Train
- [ ] 1 wagon interior
- [ ] 1 wagon exterior
- [ ] 1 deur interactie
- [ ] 1 sleutel prop
- [ ] 1 scanner prop

## Environment
- [ ] 1 klein station
- [ ] 1 kort stuk spoor
- [ ] 1 simpele buitenomgeving
- [ ] 1 tunnel of 1 brug, niet beide meteen

## Props
- [ ] 2 koffers
- [ ] 1 doos
- [ ] 1 ticket
- [ ] 1 gereedschapsprop

## UI
- [ ] task list
- [ ] interact prompt
- [ ] warning popup
- [ ] basic menu

Dat is genoeg om te testen of de stijl en gameplay werken.

---

# K. Prioriteitensysteem voor productie

## Prioriteit 1 – eerst maken
- [ ] art style scene
- [ ] conducteur character
- [ ] wagon interior
- [ ] deur + sleutel interactie
- [ ] passagiers
- [ ] station test scene
- [ ] task UI

## Prioriteit 2
- [ ] exterior train
- [ ] korte route
- [ ] brug/tunnel
- [ ] problem props
- [ ] extra animaties

## Prioriteit 3
- [ ] bergwereld
- [ ] gevaren
- [ ] weersystemen
- [ ] meer wagons
- [ ] extra levelvariatie

---

# L. Kwaliteitsregels per asset
Elke nieuwe asset moet voldoen aan:

- [ ] silhouette is duidelijk
- [ ] past in kleurpalet
- [ ] niet te veel detail
- [ ] materiaal is mat/stylized
- [ ] schaal klopt met bestaande assets
- [ ] gameplayfunctie is duidelijk
- [ ] consistent met stijl van andere assets

---

# M. Korte opdracht voor een artist / AI agent
Als checklist-briefing:

> Build a cohesive stylized low-poly asset set for a cozy chaotic co-op train game. Prioritize readability, chunky shapes, matte materials, simple silhouettes, playful proportions, and warm lighting compatibility. Avoid realistic detail, avoid overly glossy materials, and keep all assets gameplay-readable and visually consistent with a charming cartoon train world.

---
---

# 3. Multiplayer — MUST HAVE (P0)

Multiplayer-first: één gedeelde, server-authoritatieve wereld voor 1–6 spelers, met singleplayer als ondersteund pad. De eerste technische test heeft twee spelers; vier spelers is P1 en zes het beoogde maximum. Spelers moeten kunnen samenwerken en elkaar fysiek hinderen, taken gelijktijdig uitvoeren en dezelfde trein, deuren, passagiers en gebeurtenissen zien. Deuren, sleutels, scanners en gedeelde voorwerpen worden centraal gevalideerd. Voorwerpen kunnen worden opgepakt, gedragen, doorgegeven en losgelaten; treinbeweging en physics blijven consistent. Ontwerp uitnodigingen, disconnect en reconnect zorgvuldig. Multiplayer wordt vanaf de eerste speelbare versie getest.

Visuele interactie-, animatie- en physicsfeedback moet leesbaar blijven voor alle spelers. Klungelige beweging is gewenst; netwerkjitter en uiteenlopende werelden niet. Zie [architectuur](ARCHITECTURE.md), [roadmap](ROADMAP.md) en [testplan](TESTPLAN.md).


**Prioriteitscorrectie:** De oorspronkelijke assetprioriteiten hieronder gelden na de P0-netwerkproef. Gebruik voor de proef uitsluitend tijdelijke geometrie zodra die taak expliciet wordt uitgevoerd. Multiplayer UI/status en feedback horen bij de eerste netwerkervaring; artwork is niet vereist om de techniek te valideren. Zie [roadmap](ROADMAP.md).
