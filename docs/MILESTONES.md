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
- [x] GitHub Pages deploy: https://augustfricking.github.io/new-years-ski-game/ (branch `gh-pages`
      holds the contents of `SkiGame/Builds/WebGL` plus `.nojekyll`; rebuild, then replace and push)

## M7 — Playtest round 1 (owner feedback, November 2026) ✅
Look
- [x] Comic ink lines: a full-screen depth/normal edge pass (`Ski/InkEdges`) on top of the toon shader
- [x] Sculpted characters (`Person` v2): lofted torso and limbs, egg-shaped head, eyes with lids and
      irises, brows, lips, helmets, hair, goggles with strap, proper boots and gloves
- [x] Painted piste map (`MapPainter`): shaded mountain, forest, cleared pistes, lift, village, legend
- [x] World text is depth-tested (`Ski/Text3D`): signs no longer show through the map
- [x] Goggle frame at screen resolution

Player
- [x] The player is a full posed body (`PlayerRig` drives a `Person`): nothing interpenetrates when
      looking down; the snowplough V never crosses, the tips just meet
- [x] Third-person camera (default on Blue, **C** toggles anywhere) and a trick list on the HUD
- [x] Pole push on the left mouse button, sweet spot at the top of the ring; mouse look without a click
- [x] Pause silences the ski sounds; carve/skid sound follows the edge with no delay

Slopes
- [x] Green: 43 kids and parents, each wobbling, balancing, waving and looking about
- [x] Blue: 1190 m with a big-air line, halfpipe with assisted walls
- [x] Red: 34 gates in rhythm sections (open, rhythm, flush, delay), bends and steepness vary
- [x] Black: more bends; pits and rollers are painted in shadow and flagged with orange poles
- [x] Off-piste: three ways down (The Chute, The Tangle, The Meadows), 8 dead ends, "back to the
      fork" returns above the fork, rocks cannot be skied through, old ski tracks hint at the routes
- [x] The whole mountain is visible from the village: forested flanks with the other four runs
- [x] A chairlift with pylons, chairs and riders runs beside every piste

Lift and cutscene
- [x] Poles rest across the lap, the board hangs from the front foot: nothing overlaps on the chair
- [x] Every cutscene shot shows the faces; the skip key is shown on the letterbox
- [x] Expressive voices (Chatterbox TTS, local), skiers below bank and flex through their turns

Tavern
- [x] Beer-hall tables with benches: guests seated on both sides, some dancing on the tables
- [x] Layered animated fire, stone chimney breast, candles
- [x] An original sing-along song (the requested recording is copyrighted and is not used)
- [x] Behind the bar: a painting of Erik and Linn at midnight, rendered from the real characters
- [x] Beer game: 14 gulps, faster; the level drops per gulp, spills fly on a miss; the hall cheers;
      a chugging board with the regulars

Sound
- [x] Separate volumes: music, wind and ambience, effects, voices; wind is two wandering layers
- [x] Soft menu clicks; quiet music while skiing

Ghosts and boards
- [x] Ghosts on every slope, with names above them and a cap in the settings: your own best run,
      live rivals, and (when online) other players' runs from the world board
- [x] Device profile: anonymous sign-in to Unity Gaming Services gives one profile per browser or
      device; a name can be set in the settings
- [x] Online board client with local fallback (`OnlineBoards`); board configs in `Assets/_Game/Cloud`
- [ ] The six boards must be created in the cloud project once (owner: see README, "World boards")

## Known gaps and ideas
- World boards need the one-time cloud deploy above; until then every board is local.
- Ghosts of other players are their recorded runs, not a live connection: truly simultaneous
  play would need a relay service.
- Rival times and trick values are tuned from bot runs, not from human play.
- The default UI font is used; a comic display font would lift the look (needs a download).
- Cameras are hand-written (player rig, cutscene shots). Cinemachine was tried on paper and left
  out: every camera here is driven by the simulation each frame, which a few lines do directly.
- The new sounds and voices were checked by analysis, not by ear.
