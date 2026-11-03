# Conductor Chaos — Physics-Based Character Movement

**Status:** ontwerp- en implementatierichtlijn, geen claim dat dit al in Unity werkt.  
**Referenties:** *Gang Beasts* (fysieke reacties, duwen, grappige balansverliezen) en *Human: Fall Flat* (actief terugveren, grijpen en voelbaar gewicht). Niet letterlijk de besturing of animaties kopiëren.  
**Doel:** een **snelle, bruikbare first-person-controller** met een zichtbaar en echt fysiek lichaam. Richtgevoel: **75% controle, 25% gecontroleerde chaos**; dit is een ontwerpmetafoor, geen percentage in de code.  
**Samen lezen met:** [AGENTS.md](../AGENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md), [DERAILMENT_PHYSICS.md](DERAILMENT_PHYSICS.md) en [TESTPLAN.md](TESTPLAN.md).

## 1. Wat de speler moet voelen

De besturing reageert direct, maar het lichaam heeft massa, impuls en een beperkte balans. Het hoofdlichaam kan iets achterlopen op beweging, de romp helt, armen zwaaien mee en de voeten proberen het zwaartepunt onder het lichaam te houden. Een schouderduw geeft een echte fysieke reactie, niet slechts een animatie. Bij een harde klap kan het personage struikelen of vallen; daarna kan het weer verder.

**De humor ontstaat door interactie met de omgeving.** Een conducteur loopt normaal door een smalle gang. Een collega stoot hem aan. Hij doet een corrigerende stap richting een stoel. De trein remt; een andere collega houdt zich vast aan de leuning. Niemand verliest voor een lichte aanraking langdurig de controle.

### Niet gewenst

- Een slappe full ragdoll tijdens iedere gewone stap.
- Voortdurend willekeurig struikelen, oncontroleerbare draaisnelheid of lange inputvertraging.
- Een stijve FPS-capsule met alleen een cosmetische sway-animatie.
- Een camera die ongefilterd met het fysieke hoofd meedraait.
- Losse clientfysica die per speler een andere belangrijke botsing of val beslist.
- Een tweede movement-, grabbing-, input- of deurcontroller naast de bestaande implementatie.

## 2. Aanbevolen architectuur: hybrid active ragdoll

**Eerst de actuele bestanden en scene inspecteren.** De repository bevat/in ontwikkeling zijn een physics-playground en conducteur- en armsystemen. Hergebruik die in plaats van een parallel systeem te bouwen. Projectcontext en documentatie kunnen achterlopen op de huidige code/PR's.

Begin met de **lichtste stabiele hybride** die de gewenste feel mogelijk maakt:

- **Primaire fysische body** (heup/romp): verplaatsing, grondcontact, hoofd-colliders, massa, botsingen en snelheid.
- **Balanscontroller:** een begrensde upright-/PD-regeling die naar een doelhouding streeft, maar impact kan laten doorwerken. Gebruik begrensde kracht/torque, damping en een dode zone tegen microjitter. Geen instant reset naar rechtop.
- **Secundaire bodypose:** romp, hoofd, armen en benen reageren met vertraging op versnelling, bochten, botsingen en grondcontact. Begin desnoods met gedeeltelijk aangedreven onderdelen; voeg alleen extra rigidbodies/joints toe voor tastbare gameplaywinst.
- **Handen/grip:** een fysiek of constraint-gebaseerd contactpunt met beperkte reikafstand, gripsterkte en loslaatgedrag. Voor kleine gameplayvoorwerpen zoals een kaartje/sleutel mag een vereenvoudigde hand-socket betrouwbaarder zijn.
- **Presentatie:** een gesmoothde first-person-camera en animatie die de fysieke stand afleiden, geen autoritatieve gameplaytoestand bepalen.

ConfigurableJoints of een vergelijkbare opzet zijn opties, **geen verplichte herbouw**. Een volledige joint-per-bot ragdoll is geen uitgangspunt. Stel het zwaartepunt, massaverdeling, wrijving, solverbelasting en torque begrensd in; voorkom dat armen of de romp voortdurend de eigen grondbeweging blokkeren (regressie van #44).

### Bewegings- en interactiestatussen

Gebruik een eenvoudige **balans-/locomotiestatus** zoals Grounded, Moving, OffBalance, Stumbling, Falling, Ragdoll, Recovering en Airborne. Behandel Grabbing en Carrying liever als **orthogonale toestanden**: je kunt lopen terwijl je iets vasthoudt of uit balans raken terwijl je iets draagt. Definieer per toestand welke input nog mogelijk is, welke serverbeslissing nodig is en wanneer herstel plaatsvindt.

## 3. Lopen, draaien en stoppen

Bij richtinginput:
1. De verplaatsingsbesturing reageert direct en versnelt gedoseerd.
2. Romp en armen lopen visueel een fractie achter op de bewegingsrichting.
3. De voeten corrigeren naar de nieuwe looprichting; voetplaatsing mag in eerste instantie visueel/kinematisch zijn zolang grondcontact en botsingen kloppen.
4. Bij stoppen beweegt het bovenlichaam kort door en stabiliseert zonder extra knoppen.
5. Een scherpe bocht veroorzaakt korte overhelling naar buiten, **niet** een grote vertraging in looprichting.

### Startwaarden om te tunen (geen harde contracten)

| Eigenschap | Voorlopig bereik | Ontwerpbedoeling |
| --- | --- | --- |
| Lopen | 3,2–3,8 m/s | Vlot door een wagon bewegen |
| Rennen | 4,5–5,0 m/s | Sneller, iets kwetsbaarder voor impact |
| Versnellen naar loopsnelheid | 0,2–0,3 sec | Direct maar niet instant |
| Merkbare richtingsrespons | < 0,15 sec | Input voelt niet stroperig |
| Normale rompoverhelling | 3–7° | Subtiel gewicht |
| Scherpe richtingswissel | 8–12° | Korte zichtbare inertie |
| Herstel kleine verstoring | 0,3–0,7 sec | Speler kan doorlopen |
| Opstaan na val | Circa 1–2 sec | Tempo blijft hoog |

Maak waarden via bestaande tuning/config beschikbaar; gebruik ze niet als onvoorwaardelijke teleport-/animatietimers. Maak lopen in een smalle gang, om een stoel heen en langs een andere speler **eerst betrouwbaar**.

## 4. Impact en verlies van balans

**Reactie volgt uit werkelijke impuls, richting, contacthoogte, massa, snelheid en ondersteuning**. Drempels hebben hysterese zodat de toestand niet per frame wisselt. Geen willekeurige valtrigger als vervanging van fysieke oorzaak.

| Niveau | Oorzaak | Reactie | Controle |
| --- | --- | --- | --- |
| 1 — lichte aanraking | Schouder, stoel, langs een NPC | Romp/schouder geven mee, korte sway | Blijft volledig bestuurbaar |
| 2 — duw / stevig remmen | Andere speler rent tegen je, abrupte treinbeweging | Zichtbaar doorhellen, armreflex, 1–2 corrigerende stappen | Kort gedeeltelijk beperkt, niet vergrendeld |
| 3 — balans verloren | Duw te sterk of steun weg | Struikelen, vallen, contact met meubels/andere spelers | Tijdelijk ragdollachtig, dan herstel |
| 4 — extreme impact | Ontsporing, grote val of botsing | Overwegend dynamische ragdoll met behoud van momentum | Tijdelijk beperkt; camera blijft leesbaar |

Bij herstel:
- Eerst fysieke snelheid/contact/ruimte beoordelen en passende rechtoprichting kiezen.
- Niet door stoelen, muren of wagondelen teleporteren.
- Herstelanimatie of motoren mogen ondersteunen; herstel heeft een korte, begrijpelijke duur en blokkeert de speler niet onnodig.
- Kijk naar #51 voor de kleine uitvoertaak; niet alle valfuncties in één gigantische PR bouwen.

## 5. Armen, grip en objecten

Beide armen kunnen afzonderlijk bewegen. Ze reageren op lopen, impact en het gewicht van vastgehouden voorwerpen. Grijpen vereist **bereik, contact/gerichtheid en een geldig object**. Een grip ontstaat en verdwijnt via een expliciete gedeelde toestand, met een fysieke verbinding/constraint waar passend.

- Geen teleport van hand/object over grote afstand.
- Grip heeft een maximale kracht en kan bij te sterke belasting loskomen.
- Loslaten gebeurt ook bij inputverlies, disconnect, despanen en ongeldige interacties.
- Zware objecten beïnvloeden houding/balans; lichte voorwerpen blijven gemakkelijk hanteerbaar.
- Objecten kunnen tegen stoelen, deuren en mensen botsen.
- Spelers mogen elkaar later aan arm/schouder vasthouden en trekken, met beperkte krachten; niet zonder contact mensen lanceren.
- Houd de bestaande focus-/interactiearchitectuur en de conducteursleutel/treindeur leidend. Geen dubbele deurknop of tegenstrijdige grijpinput.

**Bestaande issues:** #36 (armen/rig/loslaten), #46 en #47 (grip op vloerobject en loslaten), #48 (één eigenaar), #49 (focusdetectie), #7 (netwerk-koffer), #61–#63 (sleutel/deur). Check de actuele implementatie voordat je één ervan oppakt.

## 6. De rijdende trein is geen statische vloer

Het meest risicovolle onderdeel: een speler staat in een bewegend, draaiend referentiekader. Met een constante snelheid moet lopen voelen alsof de trein stilstaat. Bij optrekken, remmen, bochten en ontsporing moeten **veranderingen** in beweging fysiek doorwerken.

- **Constante snelheid:** geen voortdurende achterwaartse drift door wereldsnelheid.
- **Optrekken:** lichaam helt kort achterover; kleine correctie blijft staanbaar.
- **Remmen:** romp beweegt voorwaarts, voorwerpen kunnen schuiven, spelers kunnen corrigeren of vallen.
- **Bocht:** zijwaartse belasting en romphelling; contact met leuningen kan helpen.
- **Ontsporing:** overgang naar werkelijke fysica met behoud van positie, lineaire en hoek-snelheid; spelers en objecten reageren op dezelfde autoritatieve gebeurtenis.

Leg expliciet vast of locomotie, contact en snapshots in **wereldruimte of wagonruimte** worden berekend. Transformeer posities, snelheden en impulsen correct bij instappen, uitstappen, wagonovergangen, grijpen, loslaten en guided-to-dynamic-overgang. Voorkom dubbele treinbeweging door parenting én platformvelocity tegelijk, tunneling, wrijvingsexplosies en netwerkcorrecties die een speler lanceren.

Architectuurkeuze nog niet stilzwijgend vastzetten: vergelijk een eenvoudige train-relative oplossing met wereldruimte in de bestaande proef. Volg [DERAILMENT_PHYSICS.md](DERAILMENT_PHYSICS.md) voor echte ontsporing. Relevante issues: #5 en #56 (rijdende wagon), #12 en #88–#90 (ontsporing).

## 7. First-person-camera en zichtbare body

De camera mag **niet** een direct child zijn dat alle ongefilterde ragdollrotaties overneemt.

- Kijkinput blijft direct, los van secundaire torso-/hoofdsway.
- Loopbob, tilt, impact-shake en roll zijn subtiel en instelbaar; bied een reduced-motion-optie.
- Bij vallen volgt de camera in positie/hoofdrichting op een gecontroleerde manier, zonder langdurig spinnen, door objecten clippen of extreme roll.
- Zorg dat de eigen armen en eventueel benen zichtbaar zijn zonder de camera te blokkeren; eigen colliders/mesh mogen niet door het hoofd snijden.
- Geen UI-, muis- of gripsysteem kapen tijdens een physics- of HUD-wijziging.

Cameracomfort is belangrijker dan fysiek exact het hoofd volgen.

## 8. NPC's en emergente humor

NPC's hoeven **niet** allemaal dure full ragdolls te zijn. Gebruik voor gewone passagiers een voordelige basis met houdingsreacties/een stapcorrectie; activeer meer fysica alleen bij relevante impact. Let op scenario's:

1. Twee conducteurs passeren elkaar in een gang; schouders raken zonder blokkade.
2. Tijdens ticketcontrole remt de trein; lichaam reageert maar ticketinteractie blijft begrijpelijk.
3. Iemand rent tegen een tweede speler; de tweede krijgt werkelijk impuls en kan tegen een stoel belanden.
4. Een speler grijpt een leuning vast; de trein beweegt, arm blijft binnen krachtlimiet vast en romp zwaait mee.
5. Iemand valt tegen een ander: de impact kan een kettingreactie geven, zonder scriptmatige valanimatie.

## 9. Multiplayer- en prestatiecontract

De **host/server is autoriteit** voor botsingsuitkomsten die gameplay beïnvloeden, balanskeuzes, vallen, interacties, voorwerpen en voertuigfysica. Clients sturen intenties; valideer bereik, snelheid, ownership, cooldown en toestand. Het is niet nodig iedere joint continu te repliceren als compacte bodypose plus status, belangrijke impulsen en cosmetische clientreacties volstaan.

- Lokale bediening mag voorspeld/gesmoothd worden voor responsiviteit, maar pas prediction/reconciliation toe **na metingen**; niet alvast een complexe tweede simulatie bouwen.
- Eén gedeelde uitkomst, geen pixel-identieke ragdolls vereist.
- Behandel gelijktijdig grijpen, grip die wegvalt bij disconnect, late join tijdens val en ownership-/wagonwissel expliciet.
- Update physics in het passende vaste tijdschema; filter kleine correcties en gebruik CCD alleen bij gemeten noodzaak.
- Beperk actieve joints/rigidbodies en NPC-simulatie. Profiel vóór grote optimalisatierondes.
- Documenteer wat enkel lokaal getest is en wat ook met host + echte remote client is gecontroleerd; een host-only proof is geen multiplayerbewijs.

## 10. Werk in kleine issues, niet als één implementatietaak

Dit document is een **ontwerpkader** voor bestaande en toekomstige deeltaken. [#28](https://github.com/mlemson/conductor-chaos/issues/28) is het overkoepelende physics-thema. **Start geen mega-issue om alle secties tegelijk te bouwen.** Maak alleen nieuwe kleine issues als een concrete, onafhankelijke ontbrekende taak nog niet bestaat.

1. **Basis locomotie & stabiliteit:** bestaande walking/colliders/rig inspecteren, #44 niet terugbrengen, responsief starten/draaien/stoppen en kleine sway tunen. Eén specifieke wijziging per PR.
2. **Balans op impact:** lichte duwen (#50), daarna pas herstel/val (#51), met echte impuls en begrensde controle.
3. **Bewegende wagon:** stabiele relatieve beweging (#56); remmen/bochten als losse volgende tuningstap indien nodig.
4. **Armen en grijpen:** bestaande issues #36/#46–#49, daarna draaggewicht en eventueel speler-speler-grip.
5. **NPC-reacties/visuele polish:** pas nadat één speler en één remote client stabiel spelen.
6. **Grotere impact/ontsporing:** apart van gewone locomotie; behoud host-autoritatieve physics.

Voor elke deelissue: beschrijf wat je in de bestaande code zag, scope, afhankelijkheden, beperkt testscenario en wat de speler zou moeten voelen. Houd code, configuratie en visuals gescheiden waar zinvol.

## 11. Kleine acceptatieset

Gebruik deze als ontwerpchecklist, **niet** als verplicht uit te voeren lange automatische testsuite bij iedere commit.

- [ ] Een speler loopt en draait prettig in een smalle treinwagon.
- [ ] Inputreactie is direct; romp/armen tonen vertraagde inertie.
- [ ] Een lichte botsing is voelbaar zonder structureel verlies van besturing.
- [ ] Een sterke impuls kan een val veroorzaken zonder willekeurig gedrag.
- [ ] Een gevallen speler kan weer overeind komen en verder.
- [ ] De camera blijft bruikbaar en geeft geen ongefilterde ragdollspin.
- [ ] Grijpen/loslaten en deursleutel blijven betrouwbaar.
- [ ] Rijden/remmen veroorzaakt geen permanente drift of dubbele wagonbeweging.
- [ ] Host en aparte client zien dezelfde belangrijke impact-/val-/gripuitkomst.
- [ ] Instelbare waarden en resterende feel-tuning zijn beschreven.

**Testbeleid:** alleen een gerichte smoke-test voor werkelijk veranderde logica; bij gewijzigde gedeelde physics zo mogelijk één host+remote-client-check. Als Unity/remote client niet beschikbaar is: noteer expliciet **NOT RUN — user Play-test**, niet doen alsof er is getest. Geen repetitieve volledige Play Mode-, build- of stresscycli zonder aanleiding. Subjectieve feel wordt door de gebruiker beoordeeld.

## 12. Afsluitende ontwerpregel

**Een betrouwbare controller met een wankel lichaam, niet een wankele controller met een betrouwbaar ogende animatie.** Normaal lopen blijft gemakkelijk; fysiek contact, scherpe bochten, remmen, vastpakken en incidenten zorgen voor de grappige chaos. Werk elke kleine stap uit in de bestaande Unity- en multiplayerarchitectuur; nooit een compleet tweede bewegingssysteem ernaast bouwen.
