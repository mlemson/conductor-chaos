# Testplan multiplayer en Unity

Nog niet uitgevoerd. Voor elke testrun: datum/commit, Unity/packageversies, platform, host/clientrollen, aantal instanties, transport, latency/jitter/loss, stappen, verwacht/werkelijk resultaat, logs en screenshots/video zonder geheimen. Registreer `PASS`, `FAIL`, `BLOCKED` of `NOT RUN`.

| ID | Scenario | Verwacht | Status |
| --- | --- | --- | --- |
| T01 | Verse checkout en Unity-import | packages resolve, geen compilefouten, URP actief, geen ontbrekende refs | NOT RUN |
| T02 | Solo als host | zelfde serverregels als online | NOT RUN |
| T03 | Host + aparte client verbinden/verlaten | beide zien dezelfde deelnemers en herstelbare status | NOT RUN |
| T04 | Twee spelers lopen/botsen in stilstaande én bewegende wagon | samenhangende botsingen, geen wegzakken, aanhoudende drift of dubbele verplaatsing | NOT RUN |
| T05 | Trein start/stopt/versnelt/draait | clients zien overeenkomstige trein en spelers; drift/correcties gemeten | NOT RUN |
| T06 | Beide spelers bedienen deur, ook gelijktijdig | één serverbesluit; toestand convergeert op alle clients | NOT RUN |
| T07 | Beiden pakken dezelfde koffer | exact één houder, andere aanvraag geweigerd of duidelijk afgehandeld | NOT RUN |
| T08 | Dragen/doorgeven/loslaten terwijl trein beweegt | één object, consistente positie en toestand; physics herstelt | NOT RUN |
| T09 | Client vertrekt tijdens dragen/interactie | geen verweesd object/claim; server ruimt op | NOT RUN |
| T10 | Late join tijdens beweging / deur open / object gedragen | actuele toestand wordt correct opgebouwd | NOT RUN |
| T11 | Reconnect, netwerkuitval en hostverlies | werkt volgens expliciete afspraak, anders nette fout en opruiming | NOT RUN |
| T12 | Vertraging, jitter en packet loss | geen blijvende divergentie; meet waar correctie onacceptabel wordt | NOT RUN |
| T13 | 4 spelers, later 6 | join/leave, simultane taken en belasting gemeten | NOT RUN |

Aanvullende tests: relevante EditMode-tests voor zuivere regels; PlayMode/integratietests voor sessies, race conditions en toestandsoverdracht. Automatiseer wanneer infrastructuur bestaat. Bij tests van echte online services: geen joincodes, account-ID's of tokens in publieke logs.
