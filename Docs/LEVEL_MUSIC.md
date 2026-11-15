# Muziek in Conductor Chaos

Issues #221 (getimede levelmuziek) en #225 (menu/intro en vervolgnummer).

## Afspeelvolgorde

1. **Startmenu, lobby, introductie/training en briefing:** `RustynailExpress2.mp3` speelt **volledig en in een loop**. Er is geen automatische fade-out na 9 seconden. Bij een overgang tussen deze scènes blijft de muziek doorspelen via de bestaande persistente `PlaygroundMenu` met één `ServiceMusic`-component.
2. **Tijdens level 1:** zodra de server de diensttimer start (crew op vertrekperron), schakelt de soundtrack naar de bestaande versneld getimede `RustynailExpress_<duur>.mp3`. De juiste versie hangt af van de crewgrootte:
   - Solo: `RustynailExpress_300.mp3` (300 seconden).
   - Twee spelers: `RustynailExpress_270.mp3` (270 seconden).
   - Drie spelers: `RustynailExpress_240.mp3` (240 seconden).
   - Vier tot zes spelers: `RustynailExpress_210.mp3` (210 seconden).
3. **Na het volledige levelnummer:** `ChugChugAlong.mp3` begint bij de afloop van de levelclip en blijft loopen zolang diezelfde dienst/resultaatscène open is. Hierdoor valt de muziek niet stil als de leveltimer op precies dezelfde tijd afloopt. Als een dienst vroeg eindigt, speelt het eerste nummer door totdat de speler een volgende dienst of het menu opent; pas daarna kan het vervolgnummer hoorbaar worden.
4. **Bij terugkeer naar het menu of de introductie:** de menu-/intro-track begint opnieuw. Bij verlaten van de relevante scènes stopt de soundtrack.

De leveltiming en keuze voor 300/270/240/210 seconden zijn **server-authoritative**. Muziek is uitsluitend lokale audio. Late clients zoeken via de gesynchroniseerde NGO-serverklok de juiste positie in de level- of vervolgtrack. De bestaande muziek- en masterbediening in het instellingenmenu blijven behouden.

## Verwachte bestanden

Plaats de audiobestanden als Unity-assets onder `Assets/Resources/Music/` met exact deze namen:

- `RustynailExpress2.mp3` — aangeleverd als **Rustynail Express 2.mp3** (3:34).
- `ChugChugAlong.mp3` — aangeleverd als **Chug Chug along!.mp3** (2:43).
- De bestaande vier `RustynailExpress_<duur>.mp3` blijven ongewijzigd.

De twee nieuwe binaire MP3-bestanden kunnen niet rechtstreeks vanuit de chatbijlagen via de GitHub-connector geüpload worden. Voeg de twee MP3's en bijbehorende `.meta`-bestanden daarom **lokaal** toe en laat Unity deze importeren. Het importpakket kan ook de `.meta`-bestanden bevatten. Pas na bevestiging dat publieke distributie is toegestaan, horen ze in de **openbare** GitHub-repository.

## Controle in Unity

1. Open `TrainPlayground`: hoor Rustynail Express 2 langer dan 9 seconden in startmenu/lobby. Controleer opnieuw na overgang naar de introductie en briefing.
2. Start level 1 door het vertrekperron op te lopen: de juiste leveltrack begint. Test pauze, mute, music volume en het terugkeren naar het menu.
3. Blijf tot het einde van Rustynail Express in de level-/resultaatscène: Chug Chug Along begint daarna. Controleer dat het nummer niet bij iedere `Update`-frame opnieuw start en dat het bij terugkeer naar het menu stopt.
4. Voor multiplayer: controleer bij host plus tweede client of beide de getimede leveltrack volgen. Dit is een **lokale audioverandering**; er wordt geen nieuwe netwerkstate toegevoegd.

**Automatische Unity compile/Play-test: NOT RUN** — Unity Editor en de Windows-projectmap zijn in deze omgeving niet beschikbaar. De C#-wijziging is statisch nagekeken; het afspelen moet nog in Unity worden bevestigd.
