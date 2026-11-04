# Conductor Chaos UI — source assets

De bronbladen zijn nu **daadwerkelijk onderdeel van deze repository**:

- [source-icons.png](source-icons.png) — verbeterd blad met 24 volledige pictogrammen (de eerdere sheet was deels afgesneden)
- [source-components.png](source-components.png) — UI-voorbeelden: knoppen, meldingen, slots en badges

De **gebruiksklare PNG's** staan in `Assets/Art/UI/Icons/` (24) en `Assets/Art/UI/Buttons/` (20). Gebruik de bestanden inclusief de Unity `.meta`-bestanden.

**Belangrijk:** het oorspronkelijke automatisch uitsnijscript is bewust vervangen door een *niet-destructieve controle-/previewtool*. De assets in `Assets/Art/UI/` zijn samengesteld en deels handmatig opgeschoond. Een naïeve crop van de bron zou achtergrondvlakjes, overlappingen of afgesneden randen opnieuw introduceren. De previewtool mag daarom de gepubliceerde sprites nooit overschrijven.

```bash
python -m pip install Pillow
python ArtSource/UI/extract_sprites.py
```

Het commando maakt een controlecontactblad in `ArtSource/UI/Review/` zonder assets te wijzigen. Dit is visuele conceptart (PNG), geen echte schaalbare SVG- of negen-slice-sprite. Voor nieuwe interacties blijf je gebruikmaken van de bestaande Unity UI-componenten en het kleurenpalet.
