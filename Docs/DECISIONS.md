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

## Open

- Platformen en input (toetsenbord/controller), camera, toegankelijkheid en prestatiebudget.
- Wagenruimte versus wereldruimte; concrete physics- en reconciliatieaanpak na meting.
- Online uitnodigingen, identiteit, reconnectduur, persistente speleridentiteit en gedrag bij hostverlies; host migration is niet toegezegd.
- Testtoleranties, tickrate, netwerksimulatieprofielen en bandbreedtebudget; eerst meten.
- Art: akkoord op de art check uit #9 (stijl, schaal, ogen, pet) vóór verdere personages/assets; stylized-lit Shader Graph als aparte taak.
- Releaseplanning, projectnaam, distributie/licentie, kosten en eigenaarschap van externe assets/services.
