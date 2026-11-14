# Ski game — agent instructions

You are the owner and long-term maintainer of this codebase, and its design is up to you.
The game design lives in `docs/GAME_SPEC.md` (source of truth). The plan and milestones live in
`docs/RESEARCH_AND_PLAN.md` and `docs/MILESTONES.md`.

## Layout
- `SkiGame/` — Unity 6 project (6000.6.9f1, URP, build target WebGL).
  - `Assets/_Game/Scripts/<Area>/` — runtime C# (one MonoBehaviour per file, file named after class).
  - `Assets/_Game/Scripts/Core/` — pure C# simulation (ski physics, scoring, traits). No UnityEngine
    scene dependencies, so EditMode tests can run it quickly and deterministically.
  - `Assets/_Game/Tests/EditMode`, `Assets/_Game/Tests/PlayMode` — tests.
  - `Screenshots/` — verification captures (outside Assets, not imported).
- `art/` — Blender sources (`.blend`), exported `.fbx/.glb` go to `SkiGame/Assets/_Game/Art/`.
- `audio/` — TTS scripts and raw audio; final `.ogg` files go into the Unity project.
- `docs/` — spec, plan, milestones.

## Working with Unity (important, learned during setup)
- Drive the running Editor with the Unity CLI (`unity` = `%LOCALAPPDATA%\Unity\bin\unity.exe`):
  `unity status`, `unity command ...`, `unity command script eval '<C#>'`,
  `unity command capture game_view --output-path Screenshots/x.png`. Always pass
  `--project-path <abs path to SkiGame>` and `--caller plugin --skill <skill>`.
- Run `unity` commands with the sandbox disabled. Inside the sandbox, Unity cannot start its helper
  processes (7z, Package Manager), and project creation fails with "path not found".
- Start the Editor from a Terminal-panel tab (`run_in_terminal`), not from a normal shell call.
  An Editor started as a child of a shell call is killed when that call ends.
- `package add` and script changes cause a domain reload, which drops the connection. Send the
  command without waiting, then `unity status --until-ready` and check `unity command package status`.
- Never hand-edit `.unity`/`.prefab`/`.asset` YAML or `Packages/manifest.json` while the Editor runs.
- Only one agent drives the Editor at a time. Subagents may write C# files in their own folders;
  the lead session imports, compiles and runs them.

## Routine steps that need no approval
Editing files in this repo; running the Editor, Play mode, tests, screenshots and WebGL builds;
exporting from Blender; generating TTS audio locally; starting a local web server for testing;
committing to local branches.

## Ask first
Pushing to GitHub, deploying to GitHub Pages, deleting Blender or audio source files, adding
paid or online services beyond Unity Gaming Services.

## Finishing work
Avoid stopping while you still owe work that was asked for. Diagnose ordinary obstacles (errors,
timeouts, failed compiles, failing tools) and work through them. Stop only for a real blocker
that needs the owner's decision, and then say plainly what you found.

## Verification (definition of done for any feature)
1. EditMode tests for the logic pass. With the Editor open use `unity command test run ...`
   (read its parameters with `unity command --query "test run"`); `unity test SkiGame` only works
   when no Editor has the project open.
2. PlayMode/bot test covers it where relevant (each level must be finishable by the bot).
3. A screenshot of the feature is captured and looked at.
4. Zero console errors.
5. Spec AC in `docs/GAME_SPEC.md` ticked in `docs/MILESTONES.md`.

## Communication
Before starting, say in one line what you will do. Every few tool calls, give a one-line update.
End each milestone with a short, self-contained recap: what works, what was verified, what is next.
