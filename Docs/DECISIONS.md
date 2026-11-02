# Ontwerpbesluiten en open vragen

## Vastgelegd als richting — 2026-11-02

- Multiplayer-first, host/server-authoritatieve gedeelde wereld; solo blijft mogelijk. Eerste bewijs met twee spelers, later vier en uiteindelijk zes.
- Echte physics-ontsporing is een expliciet productvereiste: de trein kan met botsingen of kapot spoor van de rails raken en wagons/spelers/cargo bewegen daarna fysiek op één server-authoritatieve waarheid. Normale rit kan efficiënt spoorgeleid zijn; techniek nog te valideren. Zie [DERAILMENT_PHYSICS.md](DERAILMENT_PHYSICS.md).
- Stabiele netwerkphysics en leesbaarheid vóór realistische simulatie. Richting is NGO + Relay/Lobby; definitieve versies, services en inrichting volgen na verificatie.
- Unity 6000.5.7f1 gekozen omdat deze lokaal aanwezig is. Projectstructuur afgeleid van lokaal aanwezige URP-template; import nog te valideren.
- Eerste technische proef: bewegende wagon, twee spelers, gedeelde deur en koffer. Deze oplevering bouwt deze proef nog niet.
- Project en stijl zijn afzonderlijk van bestaande SPOOR/Rijssen-werkzaamheden.
- Art direction: de [conceptafbeelding](References/conductor-chaos-concept.png) staat in Git en is leidend voor kwaliteit en stijl. Kleuren zijn gemeten in die afbeelding en vastgelegd als ramps in [stijlkaart v2](References/STYLE_PALETTE.svg) en [palette.json](References/palette.json). Personages: boonvorm, grote witte ogen, geen neus/wenkbrauwen. Licht: warm key-light, koele blauw-violette schaduw, koele rim. URP-startwaarden in de [art style guide](ART_STYLE_GUIDE.md).

## Art proof conducteur + wagon — 2026-11-02 (issue #9, voorstel ter review)

- **Art-bronnen als code** in `ArtSource/` (Python + numpy): reproduceerbaar uit Git, zonder DCC-licentie of -installatie. Unity krijgt gegenereerde FBX's en materialen; Blender kan de `.glb`'s openen. Handmatig verder modelleren in Blender blijft mogelijk, maar dan wordt dat de nieuwe bron.
- **Schaal:** conducteur 1,25 m incl. pet. Deur 1,70 m hoog en 1,15 m breed, gelijkvloers instappen (vloer 0,10 m boven het perron). Interactiepunten liggen op ca. 0,6–0,8 m (slot 0,72 m), omdat de korte armpjes niet hoger reiken.
- **Ogen:** het concept is leidend. Elk oog is ≈ 37% van de lijfbreedte en de ogen raken elkaar. De eerdere tekst "samen 35–40%" is gecorrigeerd in de stijlgids en de stijlkaart.
- **Rig:** starre poppen-hiërarchie met pivots per gewricht, geen skinning, voor P0. Dat is eenvoudig, stabiel, netwerkvriendelijk (alleen transforms) en past bij de stijve, klungelige animatiestijl. Herzien zodra animaties dat vragen.
- **Expressies** als wisselbare mond-nodes (neutraal, verbaasd, blij), plus pupilpositie en petkanteling. Geen blendshapes in P0.
- **Trein in facetstijl** (na review): zoals de locomotief in het concept bestaat de wagon uit afgeschuinde blokken met vlakke vlakken, zodat elk vlak een eigen toon krijgt (boven licht, zijkant middel, kop donker). Gladde, plastic afrondingen lazen als speelgoed van één kleur. De regionale wagon (7,2 m) vervangt de eerdere losse wagonhoek.
- **Deur:** rode schuifdeur met raampjes en een donker kader; geel blijft gereserveerd voor interactie (slot, grepen, drempel).
- **Personage-proporties** (na review): de jas loopt klokvormig iets wijder uit dan het gezicht, de pet kantelt naar achter, en armen, handen, benen en voeten zijn dikker en groter, zoals in het concept.
- **Nieuwe ramp `mouth`** (mondholte en tong), gemeten in het concept en toegevoegd aan `palette.json`.
- **Geschilderde variatie** wordt per vertex gebakken in de vertexkleur (R occlusie, G randlicht, B verfvlekken), niet in textures. Daarvoor zijn geen UV's of textures nodig, het blijft licht en is reproduceerbaar; de stylized-lit shader moet de vertexkleur lezen.
- **Doel-look** is vastgelegd met een ramp-shader-render. De Unity-tegenhanger (stylized-lit Shader Graph) is een vervolgtaak en is nog niet gemaakt.

## Locomotief — 2026-11-02 (issue #22, voorstel ter review)

- **v2 (art review @mlemson, ontwerpblad v2):** een rondere ketelneus (rechte zijkanten en een ronde bovenkant) met een goudoranje sierlijst en een inzetpaneel; een geïntegreerde achthoekige rookkastring met vier gouden bouten en een achthoekige lens; een gewelfd donkerbruin dak; stijlen met voet en kapiteel; een gouden sierlijst op de borstwering; een donker frame met rode bovenrand en gouden hoeken; blokbuffers; een gouden drijfstang. Overal zachte ronde afrondingen in plaats van harde facetten. Ongeveer 28k driehoeken.
- **v2.1 (review @mlemson):** bufferbalk smaller (2,0 m in plaats van frame-breed), lager en ronder met gouden hoekkussens en ronde blokbuffers. Meer vorm aan de onderkant: frameliggers tussen de wielen, een ronde buik onder de ketel, stoomcilinders op wielashoogte met zuigerstang. Wielen bijna dubbel zo breed (0,22 m). Neus 0,65 m korter. Cabine 0,4 m langer (2,6 m) met een **kooloventje**: `Firebox` met de losse onderdelen `FireboxDoor` (scharnier) en `Regulator` (snelheidshendel), plus een drukmeter, een kachelpijp en een `CoalBin`. Het dak is een los onderdeel (`CabRoof`), zodat het bij een camera van bovenaf verborgen kan worden. Ongeveer 38k driehoeken. Stoken en snelheid worden later een gedeelde, host-autoritatieve taak; de onderdelen zijn alleen ankers.
- **Verftextuur + panelen (review @mlemson):** één tegelbare textuur `T_PaintStrokes` (R grote vlekken, G zachte penseeltoetsen, B korrel; lineaire data, geen kleur). De shader projecteert hem triplanair in objectruimte en verschuift daarmee de positie in de kleurramp, dus er is geen UV-uitvouw nodig en kleuren blijven uit `palette.json` komen. Daarbij komen donkerder geschilderde inzetpanelen (`M_Loco_Panel`) op de cabinewanden. De textuur geldt voor alle assets (conducteur en wagon zijn opnieuw gerenderd).
- **Kolenwagen (tender, voorbeeld @mlemson):** een kleine wagen van 3,4 m direct achter de loc. Hij heeft een rode bak met een gouden bovenrand, gouden hoekstrips en een gouden onderrand, twee geschilderde panelen per zijkant en één achter, met gouden klinknagels. Bovenop ligt een berg gefacetteerde kolen (`Coal`). Verder: een donker afgerond onderstel, twee assen met brede wielen en rode naven, een gouden ladder voorin aan de perronkant, twee gloeiende sluitlampen en een bufferbalk met koppeling. Ongeveer 23k driehoeken. `Coal` is later het anker voor het stoken (gedeeld, host-autoritatief); nu alleen art.
- v1 (vervangen): locomotief naar de concept-loc: open cabine met dikke afgeschuinde stijlen en een dik dak (personages leunen eruit), een lage brede kap met grote facetten en één donkerder inzetpaneel, een achthoekige koplamp met gloeiende lens, buitenliggende wielen met drijfstang. Bewust weinig onderdelen, elk afgewerkt.
- Cabinevloer op 0,74 m en borstwering op 1,36 m, zodat de conducteur (1,25 m) over de kap kijkt en op de borstwering leunt.
- Geschilderde textuur: naar boven gerichte vlakken zijn duidelijk lichter, brede zachte verfvlekken in plaats van fijne korrel, en een lens die van een geel midden naar een oranje rand verloopt. Dit is aangepast in de gedeelde stylized shader en geldt dus voor alle assets.
- `meshkit.merge` behoudt nu per vlak of het een facet is (plat) of glad. Voorheen werden samengevoegde facet-onderdelen per ongeluk glad gerenderd.

## Open

- Platformen en input (toetsenbord/controller), camera, toegankelijkheid en prestatiebudget.
- Wagenruimte versus wereldruimte; concrete physics- en reconciliatieaanpak na meting.
- Online uitnodigingen, identiteit, reconnectduur, persistente speleridentiteit en gedrag bij hostverlies; host migration is niet toegezegd.
- Testtoleranties, tickrate, netwerksimulatieprofielen en bandbreedtebudget; eerst meten.
- Art: akkoord op de art check uit #9 (stijl, schaal, ogen, pet) vóór verdere personages/assets; stylized-lit Shader Graph als aparte taak.
- Releaseplanning, projectnaam, distributie/licentie, kosten en eigenaarschap van externe assets/services.
