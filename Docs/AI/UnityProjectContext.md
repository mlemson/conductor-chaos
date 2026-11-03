# Unity-projectcontext

Laatst opgesteld: 2026-11-02. Dit document is een momentopname; verifieer de huidige bestanden en Editor voordat je werkt.

## Bevestigd in repository

- Projectroot: de map met `Assets`, `Packages` en `ProjectSettings`.
- `ProjectSettings/ProjectVersion.txt` specificeert Unity **6000.5.7f1**. De bestanden zijn afgeleid van de lokaal aanwezige officiële 3D Cross Platform-template `17.0.14`; versiecompatibiliteit is nog niet in de editor getest.
- `Packages/manifest.json` bevat URP, Input System en Test Framework. Zie `Packages/packages-lock.json` voor de door Unity opgeloste versies. Runtimegedrag is niet geverifieerd.
- URP-configuratie staat in `Assets/Settings` en ProjectSettings. Geen scènes in `EditorBuildSettings.asset`; geen gamecode of prefabs.
- Art proof (#9): `Assets/Art/Models/...` bevat een conducteur-basismodel en een regionale wagon (blockout) (gegenereerde ASCII-FBX's), met URP Lit-materialen in `Assets/Art/Materials/...`. Deze bestanden komen uit `ArtSource/` en zijn **nog niet in de Unity Editor geïmporteerd** (testplan A01). Locomotief (#22): `Assets/Art/Models/Train/Locomotive/`, eveneens niet in de Editor geïmporteerd (testplan A02).
- Geen Netcode for GameObjects, Transport, Relay of Lobby vermeld in manifest: multiplayer is **ontworpen, niet geïmplementeerd**. Geen assemblies of tests aangemaakt.
- Een Unity MCP-tool is beschikbaar in de huidige Codex-omgeving; koppeling met dit nieuwe project en Editorstatus zijn niet geverifieerd. Geen MCP-provider toevoegen of vervangen zonder passende toestemming.

## Nog vast te stellen

Editor open/import/compile; packageversies en compatibiliteit; URP-renderer en inputinstellingen in editor; doelplatformen, exacte netwerkpakketten en serviceconfiguratie; startupflow; test- en buildresultaten; performancebudget. Zie P0-01 en P0-02 in [roadmap](../ROADMAP.md).

## Bronnen

`ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `Packages/manifest.json`, `Assets/Settings/`, [architectuur](../ARCHITECTURE.md). Laatst geanalyseerde commit: nog niet bepaald bij opstellen.

## Validatie 2026-11-03 — werk in uitvoering (Refs #2)

- Unity 6000.5.7f1 op Windows: de oude templateversies van Input System, AI Navigation, Timeline, Version Control en Visual Scripting veroorzaakten compileerfouten. De manifestversies zijn aangepast en de lockfile is afkomstig van de daaropvolgende Unity-resolutie.
- Een afzonderlijke projectkopie op `C:/Users/mauri/Documents/Codex/ConductorChaos` heeft de eerste assetdatabase-import voltooid. De Editor opende zonder Safe Mode; de Console toonde op dat moment nul fouten en nul waarschuwingen, terwijl achtergrondtaken nog liepen. De kopie bevat ook de afzonderlijke conducteur-draft uit PR #21: dit is geen validatie van de exacte Git-hoofdtak.
- Het langere pad onder `Documents/Codex/2026-11-02/...` gaf importfouten voor URP-bestanden; een kort projectpad voorkwam de geobserveerde padfouten. Controleer opnieuw bij een verse Git-checkout.
- Een verse Git-checkout van deze branch is geopend in Unity op `C:/Users/mauri/Documents/Codex/ConductorChaosGit`. De eerste assetdatabase-import voltooide zonder gevonden C#-compileerfouten of padfouten; de Editor opende buiten Safe Mode. De Console toonde 0 fouten en 0 waarschuwingen terwijl drie achtergrondtaken nog liepen. Visuele beoordeling en volledige testplan T01 blijven open.
- Unity vroeg tijdens de eerste import om een URP-materiaalupgrade. Die is uitgevoerd in de Git-checkout en heeft bestaande materialen en instellingen lokaal gewijzigd. Deze gegenereerde wijzigingen zijn behouden maar nog niet inhoudelijk beoordeeld of in deze PR opgenomen; controleer ze op samenloop met art-PR #21 voordat je ze commit. De aparte projectkopie is niet aan Git gekoppeld.
- Geen multiplayer, Play Mode, player build of host/client-test uitgevoerd. Openstaande Unity-importtaken en alle meldingen opnieuw controleren voordat P0-01 als afgerond wordt aangemerkt.
