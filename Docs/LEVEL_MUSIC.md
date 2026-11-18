# Muziek in Conductor Chaos

Issues #221 (getimede levelmuziek) en #225 (menu/intro en vervolgnummer).

## Afspeelvolgorde

1. **Startmenu, lobby, introductie/training en briefing:** `RustynailExpress2.mp3` speelt **volledig en in een loop**. Er is geen automatische fade-out na 9 seconden. Bij een overgang tussen deze scènes blijft de muziek doorspelen via de bestaande persistente `PlaygroundMenu` met één `ServiceMusic`-component.
2. **Tijdens de dienst:** de originele track begint kort na de diensttimer. Het tempo stijgt van 0,87× naar 1,18× naarmate de resterende tijd afneemt. Bij 2:27,84 mengt de speler in 0,64 seconde over naar de muzikaal vergelijkbare passage op 0:42,69.
3. **Op het resultatenscherm:** `ChugChugAlong.mp3` speelt door in een loop.
4. **Bij terugkeer naar het menu of de introductie:** de menu-/intro-track begint opnieuw. Bij verlaten van de relevante scènes stopt de soundtrack.

De diensttimer komt van de server. Muziek speelt lokaal af en gebruikt de gesynchroniseerde serverklok om bij instappen of hervatten een afspeelpositie te kiezen. De bestaande muziek- en masterbediening in het instellingenmenu blijven behouden.

## Verwachte bestanden

Plaats de audiobestanden als Unity-assets onder `Assets/Resources/Music/` met exact deze namen:

- `RustynailExpress2.mp3` — aangeleverd als **Rustynail Express 2.mp3** (3:34).
- `ChugChugAlong.mp3` — aangeleverd als **Chug Chug along!.mp3** (2:43).

De twee nieuwe binaire MP3-bestanden kunnen niet rechtstreeks vanuit de chatbijlagen via de GitHub-connector geüpload worden. Voeg de twee MP3's en bijbehorende `.meta`-bestanden daarom **lokaal** toe en laat Unity deze importeren. Het importpakket kan ook de `.meta`-bestanden bevatten. Pas na bevestiging dat publieke distributie is toegestaan, horen ze in de **openbare** GitHub-repository.

## Controle in Unity

1. Open `TrainPlayground`: hoor Rustynail Express 2 langer dan 9 seconden in startmenu/lobby. Controleer opnieuw na overgang naar de introductie en briefing.
2. Start een dienst. Luister naar de tempoversnelling, de overgang tussen de twee passages en het resultaatnummer.
3. Controleer pauze, mute, muziekvolume en terugkeer naar het menu. Controleer bij co-op ook het instappen van een tweede client terwijl de muziek al speelt.
