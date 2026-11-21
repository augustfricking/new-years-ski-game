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
- [x] The six boards exist in the cloud project (created by the owner)

## M8 — Playtest round 2 (owner feedback, 15 November 2026) ✅
- [x] World boards live (six boards created by the owner). Only real people: no seeded rivals on
      any board, no computer skiers among the ghosts. Players need no account (anonymous device
      profile). Nothing is ever sent from the Unity Editor, from the autopilot, or with a result
      no run could give; such rows are also hidden when a board is shown.
- [x] World board on the ski map card before a run; world record banner and fireworks
- [x] Tavern: left click punches. Guests fly back, tumble, bounce off walls and tables, knock
      each other over, lie there, get up and walk back. Guests carry real players' names when
      the boards have any (they are not connected live).
- [x] Look: pen strokes for folds, creases and face lines (`MeshBuilder.Stroke`, `Person.Ink`),
      squared jaws, drawn brows, lashes and lips, stubble, heavier outlines
- [x] Ski goggles with the real outline: one wrapped lens, two lobes and a nose notch
- [x] Comic fonts: Bangers (headings) and Comic Neue (text), both OFL, in `Resources/Fonts`
- [x] Hints cut to a word or two; the ghost setting says what it does
- [x] Poles only below about 22 km/h
- [x] Snow park: a clean landing from big air no longer crashes (softer landings on Blue)
- [x] Riding switch: landing backwards is allowed, controls and camera stay as they are,
      tricks started switch score 30 % more, landing switch 15 % more
- [x] Snowboarder pushes with a little sideways hop
- [x] Black: crossed poles at the bumps, no flags
- [x] Map: thin edge highlight again; the off-piste line is drawn from the top
- [x] Green: everyone skis on through the finish to the lift; people walk, talk and glide about
      the village on every slope
- [x] Sounds: the wailing "scream", the sliding "wooo" cheer and the howling wind are gone
      (a swish, applause and a steadier rush instead); the menu music opens in the major

## M9 Sculpted characters, live players, replays (playtest round 2, part 2)
- [x] Bodies sculpted in Blender from Blender Studio's Human Base Meshes (CC0), by script
      (`tools/blender/build_hero.py`), pushed into comic proportions per character: bigger
      head, eyes and hands, harder jaw and cheekbones, a nose with a clear shape
- [x] Clothes lifted off the body: puffer for the crowd; Jonte baggy (loose anorak, kangaroo
      pocket, wide pants), Erik eighties (diagonal stripes), Linn futuristic (moulded panels,
      offset zip, knee panels), Maja a one-piece suit with a belt
- [x] Closed fists that hold the poles; face expressions as blend shapes (blink, talk, smile, brows)
- [x] `SkinModel` loads the models; `Person` wears them on its own joints, so every pose works
- [x] Cleaner ink on sculpted surfaces: fine forms only get lines up close, lighter shadows
- [x] Helmets: one hard shell over the ears, rim, vents, padding just showing underneath
- [x] Live players (`LiveRooms`, `LiveCrowd`): everyone on a slope or in the tavern shares a
      room; others ski past as named ghosts and are their solid selves once past the finish.
      Played back three seconds late from small updates every two seconds.
- [x] Tavern: other players walk about; punches send them flying on every screen, and you get
      knocked flat when someone hits you
- [x] Replays: rows on the world boards can be watched (WATCH on the map card, number keys on
      the results card). No replay ghosts on the slopes any more.
- [x] Verified against the real room service with a second signed-in player in the Editor
      (`tools/diag/live_standin.cs`), in a private test room

## M10 Shop, DJ, walkman, voice (playtest round 3)
- [x] Ink: one crisp, hard-edged line per silhouette (no outline shell on people), thinner
      inner lines; harder shadow shapes; carved-plane shading makes the bodies a little boxy
- [x] Carving: lean grows half as fast inside the carve band and eases off when you let go
      there, so taps hold a carve; held too long it still over-turns. Turns about 13 % tighter.
- [x] Ski shop by every village (and SHOP on the map): colours, printed patterns, headgear and
      suits per friend; locked items show their challenge (goalie mask: 10 kids in one run;
      viking: stein in 8 s; boxer: 25 knockouts; gold suit: all five runs; crown: a
      1000-point trick; tiger, flames, banana suit, unicorn...). Outfits show to live players.
- [x] Seven original songs (Techno, EDM, Pop, Country, Hip hop, Classical, Rock), generated
      locally with ACE-Step 1.5 (MIT, `tools/music`), streamed from StreamingAssets
- [x] DJ on the tavern stage with a dancing crowd; walk up and press E to request a song
- [x] Walkman on a tavern table: pick it up and your song plays everywhere outside the
      tavern; M next song, N on/off, both in the settings
- [x] Voice chat: hold V to talk, louder the closer people are, everyone at your tavern table
      as if side by side; also on the lift and map. Peer to peer in the browser (PeerJS).
- [x] Sit down at the tavern tables (E)
- [x] Beer game: 60 s limit, R restarts, Esc leaves (E no longer leaves)
- [x] Mouse hints are a drawn mouse with the left button lit
- [x] You cannot ski through anyone in the village; replays knock kids over instead of passing
      through them
- [x] Cutscene: arms rise over the lift's safety bar instead of through it; gentler arm twists
- [x] Village people waiting about fidget: stamping, waving, stretching, laughing

## M11 Slope items
- [x] Three shop items per slope, earned by your best result there: a print for a solid run,
      headgear for a good one, a whole suit for a near-perfect one (candy / bunny ears /
      gingerbread; graffiti / snapback / rainbow; race stripes / aero helmet / race suit;
      skulls / devil horns / midnight; pines / antlers / yeti). Targets come from the best
      human runs on the world boards, not from the bot (`Unlocks.SlopeGoals`).
- [x] A replayed skier who runs into a kid goes down too, as in a real run

## M12 Owner's playtest list (round 4)
- [x] The ski shop is a red house in every village (ski onto its pad); the SHOP button is gone
- [x] Sounds asked for too often are held back (no machine-gun clicks); songs end by the clock
- [x] Shop: one friend at a time, only their own items; every item belongs to one friend;
      patterns, headgear, suits and skis/boards all clickable; dragging holds the turntable
      still; gear stands on the turntable, not through it
- [x] Fifteen ski and board designs to unlock (wood, neon, zebra, lava, galaxy, candy cane,
      ice, diamond, racing, chrome, gold, camo, wolf, aurora...), plus gear colours
- [x] Faces and bodies more their own (Erik long and angular, Jonte round-faced and solid,
      Maja big-eyed and small, Linn long-necked with tilted eyes); hard ink creases back
- [x] Barlow for all text except the comic buttons and headings (Bangers)
- [x] Graphics settings (low / medium / high, resolution), 60 fps cap, far people skip frames
- [x] After a crash, 3 s of blinking in which you cannot crash again
- [x] Voice badge at the bottom of the screen; tavern chatter sound removed
- [x] Settings in sections: Audio, Graphics, Controls, Online
- [x] Controls drawn as a keyboard: WASD, the mouse to the right, space below, the rest in a list
- [x] Beer game: the space tip alone in the middle, R and Esc set apart
- [x] Eight new songs with sung parts for men and women, goofier lyrics; an after-ski
      singalong with a crowd; a Vivaldi/Mozart-style piece with soprano and bass; repeat a song
- [x] Tavern: jump onto benches, tables and the bar; an accessible toilet (wide open door,
      wheelchair sign, rails, low sink, tilted mirror, emergency cord, flush); steins show
      their glass; the DJ's lights sit on the booth (they were the stripes in the middle of
      the room); punches land five ways (jab, hook, uppercut, body blow, haymaker) and throw
      people as ragdolls into tables, walls and each other
- [x] Leaving the tavern puts you outside its door in the village (lift and shop are there)
- [x] The lift runs up the middle of the piste map: on your left down Parken, on your right down
      Slalombacken (crossing over the finish to the station); from the other runs only its
      village station shows
- [x] Grade symbols as on piste maps: Slalombacken a wide rectangle, Ravinen a warning triangle
- [x] No floating glowing panes at the top; the summit closes the gap behind the start; no grey
      band ahead on the slopes; rocks off-piste are solid even when you fall
- [x] Turns about 13 % sharper again; carving still the fastest way down
- [x] More kids and families on Barnbacken
- [x] Bot runs are not on the world boards (ArbitraryExpandingHelicopter is a real player)

## M13 One mountain (round 5)
- [x] Park: W and S turn front and back flips by hand in the air (A and D spin); held from the snow
      they stay a tuck or a brake until pressed again; Q and E pick grab variants; 3 is a stalefish
- [x] The cutscene plays once per browser tab: changing skier goes straight to the map
- [x] The lift chairs run round the cable while you ski, riders going up
- [x] Snowboard boots on the snowboarder; on the lift the board hangs from his turned-out foot,
      boot in the binding
- [x] All five runs come down into the one village: from any of them the other four fan out
      across the flanks as clearings in the forest and curve in to the plaza, with their markers
      and a signpost each
- [x] One shared live world: everyone in one room (24 at most), each saying where they are;
      live players on other runs are shown on those runs, everyone past a finish in the shared
      village; voice only connects people near each other
- [x] When the world is full: a line, first come first served (WORLD FULL · QUEUE #n), playing
      alone meanwhile; tested with a stand-in filling a one-place test world

## M14 Cloud saves (round 6)
- [x] The save (progress, unlocks, outfits, best runs, local boards, settings) is kept in Unity
      Cloud Save too (free tier), newest wins, uploads at most every 30 s
- [x] Accounts (username and password, free) in Settings > Online so progress follows you to any
      device. Needs "Username & Password" switched on in the Unity Dashboard (Authentication >
      Identity providers); until then the game says so and keeps saving to the device's profile
- [x] Queue: "YOU ARE #n IN LINE (OF m)", updated every two seconds
- [x] Every run has its own finish arch at the bottom in its colour with its name, the others'
      arches stand where they come into the village
- [x] Cutscene hands: a hand that would go through the bar holds it, hands never meet inside each
      other, the cold beat cups hands at the mouth (arms bent to the spot by `Person.Reach`)

## M15 Round 7
- [x] Rare colours and loud prints earned by achievements, one of each per friend: Jonte's ducks
      (100 kids in all) and toxic green (50 knockouts), Maja's hearts (15 DJ songs) and neon pink
      (a 2000-point trick), Erik's lightning (110 km/h) and electric blue (stein in 6 s), Linn's
      leopard (100 runs) and ultraviolet (25 clean runs: no crash, no missed gate). Locked swatches
      show a lock and say what earns them
- [x] UNLOCKED card: slides in at the bottom left when something is earned (what, how, whose, "new
      in shop", a picture of the colour or print), several queue up, each leaves after 5 s; the shop
      tags earned things NEW until it is closed; "NEW IN SHOP" sits under the time in the village
- [x] Stuck interface sound: clicks and hovers only answer a real press or a moving pointer
- [x] Songs: every caption starts with its lead singer (male or female, voice described), and a
      guest of the other voice sings a passage (ACE-Step repaint)
- [ ] Characters remade to match the round-3 concept art (separate session; owner approves first)

## Known gaps and ideas
- Voice chat needs a free public signalling server (PeerJS) and peer-to-peer connections,
  which some strict networks block. It was not tested with two real microphones.
- The songs were checked by measurement (length, loudness, spectrum), not by ear: whether
  the vocals came out well needs a listen.
- Live players cost room-service traffic: about 10 GiB a month is free. A full room of 16
  for an hour is roughly 0.3 GiB. If the game grows, watch the Unity Dashboard.
- Rival times and trick values are tuned from bot runs, not from human play.
- Cameras are hand-written (player rig, cutscene shots). Cinemachine was tried on paper and left
  out: every camera here is driven by the simulation each frame, which a few lines do directly.
- The new sounds and voices were checked by analysis, not by ear.
