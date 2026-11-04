# Losse UI-sprites — Conductor Chaos

**Online set:** 24 individuele PNG-iconen in [Icons](Icons/) en 20 samengestelde knop-/UI-voorbeelden in [Buttons](Buttons/), samen **44 PNG's**, allemaal met bijpassende Unity `.png.meta`-bestanden (Sprite/2D UI import).

De **bronbladen** staan in [ArtSource/UI](../../../../ArtSource/UI/) en de stijlreferenties in [Docs/References/UI](../../../../Docs/References/UI/). De twee originele ontwerpen zijn leidend; de referentiekaarten bepalen iconen, knoppen, kleuren, menu en lobby.

## Gebruik

- **Iconen:** kies in Unity het losse sprite, gebruik `Image` met `Preserve Aspect`.
- **Knoppen/UI-elementen:** dit zijn afbeeldingen, *niet* al werkende `UnityEngine.UI.Button`-componenten. Bouw gedrag en states met de bestaande `Assets/Scripts/UI/` code.
- **Tekst:** sommige grotere knopafbeeldingen bevatten ingebakken Engelse tekst. Gebruik die bij voorkeur alleen als visuele mockup; dynamische, vertaalbare tekst hoort een apart UI-tekstelement te zijn.
- **Kwaliteit:** AI-conceptart en overlap tussen onderdelen kunnen bij enkele complexere voorbeelden artefacten opleveren. Beoordeel de betreffende sprite op 100% vóór productiegebruik. De afzonderlijke iconen zijn op uniforme 256×256-canvas geplaatst; geen spritesheet-cropping meer nodig.
- **Niet-destructief:** draai het contactbladscript uit `ArtSource/UI/` voor review; het overschrijft geen geïmporteerde sprites.

De huidige gameplay-, menu- en multiplayercode is niet veranderd door deze assetupload.
