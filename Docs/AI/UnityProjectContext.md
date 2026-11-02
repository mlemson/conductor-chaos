# Unity-projectcontext

Laatst opgesteld: 2026-11-02. Dit document is een momentopname; verifieer de huidige bestanden en Editor voordat je werkt.

## Bevestigd in repository

- Projectroot: de map met `Assets`, `Packages` en `ProjectSettings`.
- `ProjectSettings/ProjectVersion.txt` specificeert Unity **6000.5.7f1**. De bestanden zijn afgeleid van de lokaal aanwezige officiële 3D Cross Platform-template `17.0.14`; versiecompatibiliteit is nog niet in de editor getest.
- `Packages/manifest.json` noemt URP 17.0.1, Input System 1.12.0 en Unity Test Framework 1.4.2. Dit zijn templateafhankelijkheden; oplossen/installatie en runtimegedrag zijn **niet geverifieerd**. Een lockfile zal door Unity worden gegenereerd; controleer en commit die.
- URP-configuratie staat in `Assets/Settings` en ProjectSettings. Geen scènes in `EditorBuildSettings.asset`; geen gamecode, prefabs of eigen spelassets.
- Geen Netcode for GameObjects, Transport, Relay of Lobby vermeld in manifest: multiplayer is **ontworpen, niet geïmplementeerd**. Geen assemblies of tests aangemaakt.
- Een Unity MCP-tool is beschikbaar in de huidige Codex-omgeving; koppeling met dit nieuwe project en Editorstatus zijn niet geverifieerd. Geen MCP-provider toevoegen of vervangen zonder passende toestemming.

## Nog vast te stellen

Editor open/import/compile; packageversies en compatibiliteit; URP-renderer en inputinstellingen in editor; doelplatformen, exacte netwerkpakketten en serviceconfiguratie; startupflow; test- en buildresultaten; performancebudget. Zie P0-01 en P0-02 in [roadmap](../ROADMAP.md).

## Bronnen

`ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `Packages/manifest.json`, `Assets/Settings/`, [architectuur](../ARCHITECTURE.md). Laatst geanalyseerde commit: nog niet bepaald bij opstellen.
