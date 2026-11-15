# Levelmuziek: Rustynail Express

Issue #221.

## Werking
Level 1 start muziek uitsluitend wanneer de server de leveltimer start (crew betreedt vertrekperron). Er is geen levelmuziek tijdens de introductie, briefing, lobby of resultaten. De server legt bij timerstart de crew-duur vast. Clients starten lokaal op het bij de gesynchroniseerde servertijd passende punt. Er gaat geen audio over het netwerk.

Solo: 300 seconden, Assets/Resources/Music/RustynailExpress_300.mp3
2 spelers: 270 seconden, Assets/Resources/Music/RustynailExpress_270.mp3
3 spelers: 240 seconden, Assets/Resources/Music/RustynailExpress_240.mp3
4-6 spelers: 210 seconden, Assets/Resources/Music/RustynailExpress_210.mp3

De MP3's zijn nog niet aan Git toegevoegd: kopieer de bestanden eerst naar bovenstaande map. Unity importeert ze automatisch en maakt daarbij de .meta-bestanden. Voeg de MP3's en de .meta-bestanden pas aan de openbare GitHub-repository toe wanneer distributierechten voor deze muziek bevestigd zijn.

Het bestaande mastervolume en algemene Geluid aan/uit blijven bestaan; nieuw zijn Muziek aan/uit en Muziekvolume (standaard 65%), met PlayerPrefs-opslag.

## Handmatige Play-test
1. Start een solo-level vanaf TrainPlayground, controleer stilte in de briefing en muziekstart wanneer de speler het vertrekperron bereikt.
2. Controleer de juiste varianten bij solo, twee, drie, vier of meer spelers; controleer host en aparte remote client voor gelijktijdigheid.
3. Test tijdens het level de muziekknop en muziekslider; SFX mogen blijven klinken. Win, verlies en het verlaten van het level stoppen de muziek.

Unity-import/compile en host+remote Play-test: NOT RUN — Unity Editor en Windows-werkmap niet toegankelijk in deze sessie.
