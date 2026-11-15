# Milestones

Durable progress state. Update after each verified step. Acceptance criteria are in `GAME_SPEC.md`.
"Verified" means: tests pass, a screenshot was captured and looked at, no console errors.

## M0 — Setup ✅
- [x] Unity CLI, Editor 6000.6.9f1, WebGL module, Node.js, ffmpeg, Kokoro TTS (local venv)
- [x] `SkiGame/` URP project, packages, cloud link, WebGL target
- [x] CLAUDE.md, subagent definitions, spec, plan, art bible, tools (`tools/*.ps1`)
- [x] Crouch key decided by the owner: **W**

## M1 — Ski controller ✅
- [x] Pure C# ski physics + EditMode tests (carve, skid, snowplough, tuck, poles, jump, landings)
- [x] Character traits as data + tests (each a few percent, none dominant)
- [x] First-person rig: goggle frame, mouse look, visible skis/hands/poles, V-shape on brake
- [ ] Owner playtest of the feel (cannot be automated)

## M2 — Art ✅
- [x] `docs/ART_BIBLE.md` + `Palette.cs`
- [x] `Ski/Toon` cel shader with ink outline, banded dusk sky, fog, snow detail
- [x] Posable procedural characters (four friends, kids, crowd) with faces, blinking, talking

## M3 — Levels ✅
- [x] Green: bombers, traversers, harness kid + parent, ski-school snakes
- [x] Blue: kickers, boxes, rollers, halfpipe, tricks on 1–5 with W/A/S/D variants, scoring
- [x] Red: 32 slalom gates, missed-gate penalty
- [x] Black: steep downhill with pits and rollers, tuck
- [x] Off-piste: forest maze, 2 routes down, 7 dead ends (cliffs and thickets), checkpoints
- [x] Bot finishes every level (`LevelTests.BotFinishesTheSlope`); balance check passes

## M4 — Intro ✅
- [x] Lift menu: seamless loop, idle gestures, skiers below, falling snow, character cards
- [x] Cutscene: 17 voiced lines (~43 s), 8 camera angles, subtitles, storm, skippable
- [x] Ski map: first-person paper map, standard colours and symbols, hover info, click or 1–5

## M5 — Finish area ✅
- [x] Results + leaderboard while still skiing
- [x] New Year fireworks on a new record or improved personal best (sky darkens for the show)
- [x] Lift base: short ride cutscene, then the map
- [x] Tavern: crowd, music, fireplace, New Year decorations; walk in first person
- [x] Beer chug minigame at the bar (logic tested in Core)

## M6 — Ship
- [x] Synthesised sound effects, ambience and music; Kokoro voices for the cutscene
- [x] WebGL build (about 11 MB) loads and plays in the browser, input verified
- [x] Full-window web page with loading screen
- [ ] Owner local playtest
- [ ] GitHub Pages deploy (needs the owner's go-ahead; repo must be public for free Pages)

## Known gaps and ideas
- Online leaderboards (Unity Gaming Services) are not wired; boards are local with rival times.
- Rival times and trick values are tuned from bot runs, not from human play.
- The default UI font is used; a comic display font would lift the look (needs a download).
- Characters are procedural (rigid parts). Blender-sculpted heroes would be a visual upgrade.
- No lift line beside the pistes, no ski tracks hinting at the off-piste routes.
- Cameras are hand-written (first-person rig, cutscene shots), not Cinemachine.
