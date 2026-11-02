# Architectuur — ontwerp, nog niet geïmplementeerd

## Doel en schaal

Co-op voor **1–6 spelers** inclusief host; solo blijft mogelijk. Eerste P0-bewijs: host + één aparte client. P1 demonstreert vier spelers; zes is het productdoel en moet daarna expliciet gevalideerd worden. Netcode for GameObjects met Unity Transport en Relay/Lobby is de voorkeursrichting, afhankelijk van compatibiliteit en configuratie; geen component daarvan is al ingebouwd. Een host is voor de sessie de autoriteit; host migration en duurzaam reconnect zijn geen belofte.

## Verantwoordelijkheden

| Domein | Verantwoordelijkheid | Gedeelde waarheid |
| --- | --- | --- |
| Session | identiteit, connectie, lobby/relay, lifecycle | server bepaalt toelating en sessiestatus |
| Train | routepositie, snelheid, rijstatus en referentieruimte | server simuleert en publiceert toestand |
| Player | input, locomotie, botsingen en spelerstatus | server valideert inputs en simuleert gedeelde gevolgen |
| Interaction | bereik, bevoegdheid, prioriteit, uitvoering | server accepteert of weigert intenties atomair |
| Items | identiteit, vrij/gedragen, houder en physics | één server-eigen toestandsovergang per item |
| World/events | passagiers, taken en gebeurtenissen | server bepaalt resultaten; clients tonen presentatie |
| Presentation | camera, audio, VFX, HUD, interpolatie | lokaal afgeleid; geen beslissende gameplaystatus |

Indeling is een ontwerpvoorstel, geen bestaande API. Maak pas assemblies/mappen wanneer ze echte verantwoordelijkheden hebben. Voorkom cyclische afhankelijkheden; presentatie hangt af van systeemcontracten en niet andersom.

## Interactieprotocol

Client selecteert een kandidaat en verzendt intentie met doel en actie. Server controleert verbonden identiteit, bestaan/spawn, afstand/zicht, toestand, rechten, cooldown en optionele volgorde/versionering. Server voert één overgang uit, verspreidt de actuele toestand en bevestigt/weigert; UI verwerkt afwijzing. Gelijktijdige aanvragen voor dezelfde koffer leveren één winnaar en geen dubbele houder. Maak acties idempotent waar herhaling mogelijk is; ruim referenties op bij despawn/disconnect. Vertrouw client-positie of een client-claim niet blind.

## Bewegende trein en physics

- Begin met één server-authoritatieve simulatie en expliciete trein-relative positie/rotatie voor spelers en losse voorwerpen. Beschrijf wat gebeurt bij instappen, uitstappen, wagonwissel, oppakken en loslaten. Vergelijk dit met een stabiele wereldruimte-variant in de P0-proef voordat een definitieve keuze wordt vastgelegd.
- Beweeg physics-objecten via het gekozen physics-tijdschema; leg parent/ownership-overgangen vast. Voorkom dubbele toepassing van treinverplaatsing en botsingen tussen een voorspeld object en de serverwaarheid.
- Clients interpoleren zichtbare snapshots; bepaal met metingen of voorspelling nodig is. Houd serverphysics leidend; realistische krachten zijn ondergeschikt aan een stabiele, begrijpelijke uitkomst. Botsingen en schommelingen moeten voor alle spelers consistent zijn, niet bit-identiek.
- Meet jitter, penetratie, correcties en latentie bij beweging, remmen en stilstand. Definieer haalbare toleranties tijdens de proef; nog geen onbewezen prestatiedoelen claimen.

## Sessie en herstel

Initieer services en authenticatie zonder geheimen in Git. Scheid aanmaken/joinen, transport, spawn en gameplay-ready. Server stuurt bij late join een samenhangende actuele snapshot; actuele toestanden mogen niet alleen uit vluchtige events bestaan. Bij wegvallende speler ruimt server diens interactieclaims op en laat/plaatst voorwerpen veilig los. Reconnect, timeouts, hostverlies en migratie vereisen expliciete productbeslissingen en tests; tot die tijd netjes afsluiten en duidelijke foutmelding. Singleplayer gebruikt dezelfde autoritatieve code met lokale host of aantoonbaar gelijkwaardige route.

## Acceptatie

Elke gedeelde feature specificeert eigenaar, bron van waarheid, validatie, synchronisatie, late-join/disconnectgedrag en testbewijs voor host en client. Zie [testplan](TESTPLAN.md) en [besluiten](DECISIONS.md).
