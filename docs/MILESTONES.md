# Milestones

Durable progress state. Update after each verified step. Acceptance criteria are in `GAME_SPEC.md`.

## M0 — Setup ✅
- [x] Unity CLI, Editor 6000.6.9f1, WebGL module, Node.js, ffmpeg
- [x] `SkiGame/` URP project; Pipeline, Cinemachine, Timeline, Splines, Test Framework,
      Authentication, Leaderboards packages; cloud-linked
- [x] Build target WebGL; baseline post-processing (tonemapping, bloom, vignette)
- [x] CLAUDE.md, subagents, spec, plan
- [ ] Owner confirms the crouch key (spec says "A", which conflicts with turning)

## M1 — Ski controller
- [ ] Pure C# ski physics + EditMode tests (carve, skid, snowplough, tuck, poles, jump)
- [ ] Character traits as data + tests
- [ ] First-person rig: goggle frame, mouse look, visible skis/hands/poles, V-shape on brake
- [ ] Test slope; owner playtest of feel

## M2 — Art
- [ ] `docs/ART_BIBLE.md` (style statement + palette)
- [ ] Toon shader + outline pass in URP
- [ ] One character modelled, rigged, animated, in Unity

## M3 — Levels
- [ ] Green (4 kid types) · [ ] Blue (park, tricks, scoring) · [ ] Red (slalom) · [ ] Black
      (downhill, pits) · [ ] Off-piste (maze)
- [ ] Bot finishes every level; balance run

## M4 — Intro
- [ ] Lift menu loop · [ ] Character select · [ ] Cutscene with storm + dialogue · [ ] Ski map

## M5 — Finish area
- [ ] Results + leaderboards while skiing · [ ] High-score fireworks · [ ] Lift restart
- [ ] Tavern · [ ] Beer minigame

## M6 — Ship
- [ ] Voices + audio · [ ] Performance pass · [ ] Owner local test · [ ] GitHub Pages deploy
