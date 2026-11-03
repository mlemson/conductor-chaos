# Historisch: P0-01 Unity-baseline (niet standaard opnieuw uitvoeren)

> **Deze template is verouderd.** Er is inmiddels een Unity-physics-playground en lokale multiplayerproef. Kies voor nieuw werk één issue uit de [kleine taakindex](SMALL_TASK_INDEX.md) en lees het [playbook](SMALL_TASK_PLAYBOOK.md). Gebruik onderstaande tekst alleen als issue #2 nog een daadwerkelijk aangetoonde blocker bevat.

## Oude oorspronkelijke opdracht

### P0-01 Unity-baseline

> To use **now**. When complete, update or retire this template. Do not treat instructions here as proof of project status.

Work in the private repository `mlemson/conductor-chaos` on a new branch. Your sole deliverable is **P0-01** from [roadmap](../ROADMAP.md): validate the current Unity project with Unity 6000.5.7f1, its existing URP/Input System/Test Framework packages and editor settings.

## Read before editing
- [AGENTS.md](../../AGENTS.md)
- [Game vision](../GAME_VISION.md)
- [Project context](UnityProjectContext.md)
- [Architecture](../ARCHITECTURE.md)
- [Roadmap](../ROADMAP.md)
- [Test plan](../TESTPLAN.md)

## Steps
1. Clone/open the **actual** Git repository; confirm branch, Unity version, relevant manifest and current project tree. Never assume the local document copy is a Git checkout.
2. Open/import in Unity, resolve packages and capture **exact** errors/warnings. Inspect console, URP and input configuration. Check the package lock and tracked `.meta` assets.
3. Fix only genuine P0-01 blockers with the smallest justified changes. Do not update Unity or packages automatically, install Netcode, create gameplay scenes, content or character models as part of this task.
4. Validate again in Unity and record the environment and test result. If Unity cannot run, mark the task `BLOCKED` and explain precise checks still needed; do **not** claim successful compilation.
5. Update `Docs/AI/UnityProjectContext.md`, the P0-01 line in `Docs/ROADMAP.md`, and T01 in `Docs/TESTPLAN.md` **only according to actual evidence**.
6. Open one focused PR with changed files, rationale, test evidence, risks and any remaining blockers.

## Efficiency constraints
- One cold import followed by targeted diagnostics; avoid repeated editor reloads and unnecessary reimports.
- Change one subsystem per task, and keep PRs small and reviewable.
- Do not claim multiplayer, Unity import or a playable build works until separately proven.
- Preserve multi-player-first architecture and existing GitHub documentation.

## Done when
A fresh checkout imports and compiles in the real Unity editor with URP configured, and the status/test log contains evidence. If blocked, the blocker is reproducible, narrowly identified and documented. Next task P0-02 starts only afterward.
