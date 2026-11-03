# UI-voorbeeld testen (Unity 6000.5.7f1)

De code op deze branch is **nog niet in de echte Unity Editor gecompileerd of visueel geverifieerd**. Deze checklist voorkomt dat een alleen op GitHub gecontroleerde implementatie als werkend wordt gepresenteerd.

## Hoofdmenu en lobby-prototype

1. Open een schone checkout van de repository in Unity 6000.5.7f1 met de bestaande Input System + uGUI packages.
2. Laat Unity volledig importeren. Controleer de Console; herstel eventuele compilefouten vóór verdere visuele aanpassingen.
3. Kies `Tools > Conductor Chaos > UI > Create or Open Menu Preview`. Dit maakt een losse `Assets/Scenes/ChaosMenuPreview.unity` als die nog niet bestaat en voegt hem **als tweede** scene toe aan Build Settings; `TrainPlayground` blijft op buildindex 0.
4. Druk Play en controleer het **hoofdmenu, lobby-preview en HUD-voorbeeld** op 1280×720 en 1920×1080. Vergelijk visueel vooral met ontwerpafbeelding 1 en 3 uit de chat.
5. Klik `SETTINGS`; pas de master-volume-slider aan en activeer/deactiveer mute. Controleer de echte Unity-audiostatus.
6. Klik `LOBBY PREVIEW`, daarna `CHANGE`: alleen de voorbeeldroute en uitleg mogen veranderen. Er mag nergens staan dat echte vrienden joined/ready zijn.
7. Klik `PLAY LOCAL TRIAL`. De bestaande scène `TrainPlayground` moet laden en de proefhost starten. De daadwerkelijke online lobby/Relay zijn niet geïmplementeerd.

**Opmerking:** de showcase bevat UI-elementen als voorlopige, met code gegenereerde uGUI-layout. Voor definitieve gameplay zijn assets, controller-/keyboardnavigatie, toegankelijke schaal en merktypografie verdere aparte taken.

## In-game physics trial HUD

1. Open `Assets/Scenes/TrainPlayground.unity` en druk Play. Het script installeert de read-only trial HUD automatisch zodra de scène geladen is (niet tijdens batch/headless-tests).
2. Controleer dat linksboven alleen **PHYSICS PLAYGROUND**, **LOCAL HOST** (of client/offline) en de werkelijk bekende netwerkinformatie staan.
3. Controleer dat de kleine bediening onderaan klopt met de bestaande `PlaygroundPlayer` controls en dat muis-look, LMB/RMB grip, WASD, Space en Escape nog functioneren.
4. Draai de bestaande remote-client test met een aparte build. Bekijk de HUD in beide instanties. Alleen host toont het server-side connectieaantal; remote client toont gekoppelde client-status.
5. De HUD is informatief en blokkeert geen pointerevents. Controleer dat aan/afwezigheid ervan geen invloed heeft op physics, spawns of RPC's.
6. Er mogen **geen** fictieve kolen-, keteldruk-, treinintegriteit-, ticket- of routemeters verschijnen.

## Reviewcheck voor ontwerp

- Geen neon/glow, glaslagen of onnodige zware schaduwen.
- Eén opvallende primaire goudkleurige actie, veel rustigere secundaire knoppen.
- Matte crème knoppen op gedempt donker vlak, leesbare labels en consistente afstand.
- Korte labels, compacte HUD, geen full-screen HUD tijdens gameplay.
- UI mag geen afronding van de echte online multiplayer/lobby suggereren.
- Noteer in PR welke resoluties, inputvormen en Unity-versies daadwerkelijk zijn getest.

## Vervolg

Na Unity- en visuele review: commit de door de Editor gegenereerde menu-preview-scene en diens `.meta` indien geaccepteerd; bouw daarna pas een echte multiplayer-lobby aan de hand van server-/Relay-status en de toepasselijke GitHub-issues. Maak van `ChaosMenuPreview` geen productie-frontend zonder deze koppeling.
