# Ski game ("Last Run") — agent instructions

You are the owner and long-term maintainer of this codebase, and its design is up to you.
The game design lives in `docs/GAME_SPEC.md` (source of truth). The plan and milestones live in
`docs/RESEARCH_AND_PLAN.md` and `docs/MILESTONES.md`. The look is defined in `docs/ART_BIBLE.md`.

## How the game is built (read this first)
- **Code-first.** `SkiGame/Assets/_Game/Scenes/Main.unity` is empty. `GameApp` boots after scene
  load and builds everything at run time: world, characters, UI, audio. There are no prefabs.
- **Core vs Game.** `Scripts/Core` (assembly `Ski.Core`) is the deterministic simulation: ski
  physics (`SkiSim`), courses (`PisteCourse`, `OffPisteCourse`), rules (`RunSession`), the kid
  crowd, the bot, leaderboards, the beer game. `Scripts/Game` (`Ski.Game`) renders and plays it:
  `TerrainBuilder`, `VillageBuilder`, `PlayerRig`, `Person` (posable procedural skier), scenes
  (`LiftScene`, `MapScene`, `TavernScene`), UI (UI Toolkit, `Resources/UI/Game.uss`), audio
  (`Synth` makes every sound from maths; voices are `.ogg` files in `Resources/Voices`).
- **Look.** `Palette.cs` is the only source of colours. Everything lit uses the `Ski/Toon` shader
  (`Resources/Shaders`); `Ski/InkEdges` is a full-screen pass that draws the comic ink lines from
  depth and normals, so every shader that should be outlined needs a DepthNormals pass. Meshes come
  from `MeshBuilder` (`Loft` for organic shapes) and `Props`. People are `Person` (poses are joint
  angles in `Pose`); the player is a `Person` driven by `PlayerRig`.
- **Around a run.** `TerrainBuilder` (snow, forest, gates), `MountainBuilder` (flanks with the
  other runs, lift line, off-piste tracks), `VillageBuilder`, `LiveCrowd` (other players).
- **Characters.** Bodies are sculpted in Blender by script (`tools/blender/`, see its README) and
  stored as `Resources/Models/<name>.bytes`; `SkinModel` reads them and `Person` wears them. Kids
  still use the simple shapes. Rebuild with `sh tools/blender/build_all.sh` after changing the
  script; the CC0 base meshes are downloaded into `tools/blender/_downloads` (not in git).
- **Online.** `OnlineBoards` (Unity Gaming Services, anonymous sign-in, free tier). It must never
  block or break local play: every call falls back to the local boards. The world boards are
  live and hold real players' results: nothing is sent from the Editor (`SendFromEditor` stays
  false), from the bot, or with an implausible value (`Courses.Plausible`). Never teleport a
  player past the finish with submissions enabled. No seeded rivals, no bot ghosts.
- **Music and voice.** The DJ's songs are made locally by `tools/music/make_songs.py` (ACE-Step
  1.5, MIT; setup in `tools/music/README.md`) into `Assets/StreamingAssets/Music` and streamed by
  `Songs`; `AudioDirector.Music("song:<id>")` plays one. Voice chat is `VoiceChat` +
  `Plugins/WebGL/LastRunVoice.jslib` (WebRTC via PeerJS 1.5.4 from unpkg); it does nothing in
  the Editor, so test it in a WebGL build.
- **Shop.** A house in every village (`ShopStage`, `ShopPanel`). `Unlocks` (Core) holds the items
  and their challenges; every item belongs to one character (`Unlock.Who`), progress is counted
  per character (`SaveData.ProgressOf`); `PersonLook.Apply(Outfit)` dresses a character;
  `OutfitPaint` prints patterns; `GearModels.DesignOf` paints skis and boards.
- **Settings.** `Dialogs` (sections Audio, Graphics, Controls, Online); `GraphicsQuality.Apply`
  sets the render pipeline from `SaveData.Quality` and `RenderScale`.
- **Fonts.** Barlow Semi Condensed (OFL) for text, Bangers for buttons and headings.
- **Physics** is used only for the tavern's ragdolls (`Ragdoll`); everything else is `SkiSim`.
- **Live players.** `LiveRooms` (Multiplayer sessions used as rooms, player data only, no game
  networking). Nothing is sent from the Editor or the bot (`LiveRooms.SendFromEditor`). Test
  only in the private room `test` with `tools/diag/live_test.cs` + `live_standin.cs` (a second
  signed-in player inside the Editor). Replays of board rows are watched, never raced.
- **Hints and labels** are one to three words. The owner finds longer ones over-explained.
- **Flow.** `GameApp`: lift menu -> cutscene -> ski map -> run -> village -> lift ride or tavern.
- One MonoBehaviour per file, file named after the class.

## Layout
- `SkiGame/` — Unity 6 project (6000.6.9f1, URP, build target WebGL).
  - `Assets/_Game/Scripts/Core`, `Scripts/Game`, `Editor`, `Tests/EditMode`, `Resources`.
  - `Screenshots/` — verification captures (outside Assets, not in git).
  - `Builds/WebGL` — web build output (not in git).
- `tools/` — scripts that drive the Editor; `tools/diag` — one-off C# snippets for `script eval`.
- `audio/make_voices.py` — generates the cutscene voices with Chatterbox TTS (local, free).
- `docs/` — spec, plan, milestones, art bible.

## Working with Unity (learned the hard way)
- The Editor runs **headless** (batch mode, no `-quit`) in a Terminal-panel tab. Start it with
  `run_in_terminal`, never from a normal shell call (that Editor is killed when the call ends):
  `& '<Unity.exe>' -batchmode -projectPath '<abs path to SkiGame>' -logFile '<abs path>/Logs/editor-headless.log'`
- Drive it with the wrappers in `tools/` (PowerShell tool, sandbox disabled):
  - `tools/compile.ps1` — refresh, recompile, print compile errors.
  - `tools/test.ps1 [filter]` — run EditMode tests, print failures and test logs.
  - `tools/play.ps1 -Slope Green|Blue|Red|Black|OffPiste|None -Who Erik -Seconds 5 -Shot name [-Keep] [-NoBot]`
    enters Play mode, starts a run with the bot driving (or stays on the menu with `None`) and
    saves `SkiGame/Screenshots/<name>.png`. Always look at the image afterwards.
  - `tools/pose.ps1 -Slope X -What brake|carve|air|kids|park|pipe|offpiste -Shot name` freezes a
    situation; `tools/cutshots.ps1 -Beats 9,11` captures cutscene beats; `tools/diag/*.cs` are
    one-off views (face close-up, tavern, pits, the look back up the mountain, ghosts);
    `tools/diag.ps1 -File <view> -With "<args>" -Shot name` runs one on the menu scene.
  - WebGL build: `tools/u.ps1 command build run -- --target WebGL --output-path "Builds/WebGL" --confirm true`,
    then poll `tools/u.ps1 command build status` (it takes ten minutes or more).
  - `tools/u.ps1 <unity cli args>` — the Unity CLI pinned to this project, for example
    `tools/u.ps1 command script eval '<C#>'` or `... script eval --file <absolute .cs path>`.
- Unity commands fail inside the sandbox (helper processes cannot start): disable it for them.
- Right after a recompile the Editor is briefly unreachable (exit code 12): wait and retry.
- Batch mode opens no scene: `ProjectSetup.OpenMain()` opens Main and sets a 1280x720 game view.
  `Ski.EditorTools.ProjectSetup.Run()` re-applies render pipeline, quality and player settings.
- Never hand-edit `.unity`/`.prefab`/`.asset` YAML or `Packages/manifest.json` while the Editor runs.
- Only one agent drives the Editor at a time. Subagents may write C# files in their own folders;
  the lead session imports, compiles and runs them.
- Shell note: put at most one heredoc in a Bash call; use the Write tool for new files.

## Routine steps that need no approval
Editing files in this repo; running the Editor, Play mode, tests, screenshots and WebGL builds;
generating TTS audio locally; starting a local web server for testing; committing and pushing to
`origin main` (the owner asked for regular pushes).

## Ask first
Deploying to GitHub Pages, deleting audio or art source files, downloading third-party assets
(fonts, models, sounds). No paid tools or services, ever.

## Finishing work
Avoid stopping while you still owe work that was asked for. Diagnose ordinary obstacles (errors,
timeouts, failed compiles, failing tools) and work through them. Stop only for a real blocker
that needs the owner's decision, and then say plainly what you found.

## Verification (definition of done for any feature)
1. EditMode tests for the logic pass (`tools/test.ps1`).
2. The bot can still finish every slope (`LevelTests.BotFinishesTheSlope`).
3. A screenshot of the feature is captured with `tools/play.ps1` and looked at.
4. Zero console errors.
5. The matching line in `docs/MILESTONES.md` is ticked.

## Communication
Before starting, say in one line what you will do. Every few tool calls, give a one-line update.
End each milestone with a short, self-contained recap: what works, what was verified, what is next.
