# Losse UI-sprites — Conductor Chaos

De twee UI-conceptbladen zijn opgesplitst in losse transparante PNG-sprites:

- **24 iconen** in [Icons/](Icons/) — ticket, sleutel, gereedschap, trein, kolen, meter, waarschuwing, chat, speler, lantaarn, deur, Play, Options, Close, Back, Audio, Controls, Accessibility, Route, Upgrades, Player Group, Invite, Ready en Quit.
- **20 knoppen/componenten** in [Buttons/](Buttons/) — primaire/secundaire/danger-buttons, compacte icoonknoppen, prompts, tooltip, objectives, waarschuwing, player badge en inventory slots.

**Importstatus:** de volledige set is als ZIP uit de ontwerpchat beschikbaar, met deze folderstructuur en `.meta`-bestanden. De GitHub-connector kan de binaire PNG's niet rechtstreeks uit die ZIP schrijven. **Deze README en de uitsnijtool staan al in GitHub; de PNG's zelf moeten nog worden geüpload naar deze branch.** Vink issue #129 pas af wanneer de binaire bestanden daadwerkelijk zichtbaar zijn.

## Bestandsstructuur

```text
ArtSource/UI/
  source-icons.png
  source-components.png
  extract_sprites.py
Assets/Art/UI/
  Icons/*.png (+ .png.meta)
  Buttons/*.png (+ .png.meta)
```

De sprites zijn afbeeldingen met transparantie, geen echte werkende knoppen. Ze zijn bedoeld als UI-grafiek bij de bestaande `Assets/Scripts/UI/` componenten. Gebruik voor interactieve knoppen een echte `Button` en voeg het sprite als afbeelding toe.

## Ontwerpkeuzes

- Behoud de warme, speelse 3D-stijl van de [originele UI Style Guide](../../../Docs/UI_STYLE_GUIDE.md).
- De beelden zijn **raster**. Een knoppensprite met gebakken tekst (zoals Play/Options) is vooral referentie of snelle mockup; voor dynamische knoppen/vertaling moet tekst in Unity bovenop een herbruikbare achtergrond staan.
- Crop-prijzen en alpha kunnen bij sommige AI-gegenereerde plaatjes nog een kleine rand/artefact bevatten. Controleer bij 100% schaal; importeer met `Sprite (2D and UI)`, `Preserve Aspect`, standaard bilinear filtering.
- Geen wijzigingen aan gameplay, multiplayer of huidige menu/UI-componenten nodig.

## Bron opnieuw uitsnijden

Na toevoegen van beide bronbladen:

```bash
python -m pip install Pillow
python ArtSource/UI/extract_sprites.py
```

De reeds uitgeknipte ZIP-sprites hebben extra handmatige alpha-schoonmaak gekregen. Gebruik deze waar mogelijk in plaats van opnieuw genereren.
