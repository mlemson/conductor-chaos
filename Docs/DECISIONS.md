# Ontwerpbesluiten en open vragen

## Vastgelegd als richting — 2026-11-02

- Multiplayer-first, host/server-authoritatieve gedeelde wereld; solo blijft mogelijk. Eerste bewijs met twee spelers, later vier en uiteindelijk zes.
- Echte physics-ontsporing is een expliciet productvereiste: de trein kan met botsingen of kapot spoor van de rails raken en wagons/spelers/cargo bewegen daarna fysiek op één server-authoritatieve waarheid. Normale rit kan efficiënt spoorgeleid zijn; techniek nog te valideren. Zie [DERAILMENT_PHYSICS.md](DERAILMENT_PHYSICS.md).
- Stabiele netwerkphysics en leesbaarheid vóór realistische simulatie. Richting is NGO + Relay/Lobby; definitieve versies, services en inrichting volgen na verificatie.
- Unity 6000.5.7f1 gekozen omdat deze lokaal aanwezig is. Projectstructuur afgeleid van lokaal aanwezige URP-template; import nog te valideren.
- Eerste technische proef: bewegende wagon, twee spelers, gedeelde deur en koffer. Deze oplevering bouwt deze proef nog niet.
- Project en stijl zijn afzonderlijk van bestaande SPOOR/Rijssen-werkzaamheden.

## Open

- Platformen en input (toetsenbord/controller), camera, toegankelijkheid en prestatiebudget.
- Wagenruimte versus wereldruimte; concrete physics- en reconciliatieaanpak na meting.
- Online uitnodigingen, identiteit, reconnectduur, persistente speleridentiteit en gedrag bij hostverlies; host migration is niet toegezegd.
- Testtoleranties, tickrate, netwerksimulatieprofielen en bandbreedtebudget; eerst meten.
- Releaseplanning, projectnaam, distributie/licentie, kosten en eigenaarschap van externe assets/services.
