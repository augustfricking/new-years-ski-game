# Ski Game — Research & Build Plan

Updated 2026-11-14. Engine decision changed from Three.js to **Unity**, at the owner's request.

## 1. Decisions
| Topic | Decision |
|---|---|
| Engine | Unity 6000.6.9f1, URP, build target WebGL. Project in `SkiGame/`. |
| Why Unity | Better visuals and tooling for this game: Timeline + Cinemachine (multi-angle cutscene), animation system (idle moves, retargeting), particles (snowstorm, fireworks), URP post-processing and Shader Graph (toon look). Cost: larger download and slower load than Three.js, slower build loop. |
| Assets | Blender 5.3 via MCP → FBX into Unity. |
| Voices | Kokoro TTS (Apache-2.0) or Piper, run locally, rendered to `.ogg` with ffmpeg. |
| Hosting | GitHub Pages (free for public repos), only after the owner has tested locally. |
| Online leaderboards | Unity Gaming Services (project linked to Unity Cloud, org `africking`). |
| Tools installed | Unity CLI 1.0.0, WebGL module, Node.js LTS (local web server, tooling), ffmpeg. |

## 2. How to use Fable 5.2 (Anthropic guidance, "Prompting Claude Fable 5.2")
1. **Main session = Fable 5.2 at `high`.** It plans, writes the hard systems (ski physics, camera,
   toon shading), drives the Unity Editor, integrates and verifies. Use `xhigh` only for a stuck,
   hard problem. Drop to `medium` for small interactive tweaks.
2. **Orchestrator, not a do-everything agent.** Delegate well-specified pieces to subagents on
   cheaper models (`.claude/agents/`). Each brief names the owned folder, the spec section and the
   acceptance criteria. Delegate only when a piece is independent; subagents cost tokens.
3. **CLAUDE.md** contains the ownership sentence, the "finishing work" text, the list of routine
   steps that need no approval, and short progress-update instructions — all recommended by the
   guide. Keep it short; remove rules that stop being true.
4. **Keep the conversation append-only**; let compaction handle long sessions. Use
   `docs/MILESTONES.md` as durable progress state across sessions.
5. If Fable stops early, asks needless approvals, or delegates too much, adjust one thing at a time.

## 3. Agent team
| Agent | Model | Job |
|---|---|---|
| Lead (main session) | Fable 5.2, high | Architecture, ski physics, Editor work, integration, verification |
| `implementer` | Sonnet 5.5 | Self-contained C# modules + EditMode tests (kid AI, slalom gates, trick scoring, beer minigame, UI logic) |
| `asset-builder` | Opus 5.5 | Blender models, rigs, animations, FBX export |
| `playtester` | Sonnet 5.5 | Plays the WebGL build in the built-in browser, checks acceptance criteria |
| `reviewer` | Fable 5.2 | Milestone review against the spec |
| Search | Haiku (built-in Explore) | Code lookup |

Unity constraint: only one agent drives the Editor. Subagents write code/assets in their own
folders in parallel; the lead imports and wires them. Make sure `CLAUDE_CODE_SUBAGENT_MODEL` is
not set, or it overrides the model pins.

## 4. Verification strategy
1. **Pure C# core** (`Scripts/Core`): ski physics, traits, scoring, gates are plain classes with a
   fixed timestep → fast, deterministic EditMode tests.
2. **EditMode tests**: carve gains speed, over-turn brakes, snowplough brakes, tuck raises top speed,
   pole timing window, each trait within 3–8 %, trick and gate scoring, kid collision.
3. **PlayMode bot tests**: an autopilot follows each level's spline; every level must be finishable;
   off-piste has known valid routes and dead ends; checks for NaN, falling through terrain, crashes.
4. **Balance run**: bot with each character on each level; no character wins everything.
5. **Screenshots**: `unity command capture game_view` at fixed shots per scene, looked at by the
   model; the `craft-game-floors` visual scorecard is the look gate.
6. **WebGL smoke test**: build, serve locally (`npx http-server`), load in the built-in browser,
   check the console and frame rate. Budget: ≥ 60 FPS on a mid laptop, < 50 MB download.
7. **Owner playtest** at each milestone — game feel can't be fully automated.

## 5. Milestones
See `docs/MILESTONES.md` for the checklist.
- M0 Setup (done in this session)
- M1 Ski controller on a test slope + first-person rig
- M2 Art bible, toon shading + outlines, one character end-to-end
- M3 Five levels
- M4 Lift menu, cutscene, ski map
- M5 Finish area, leaderboards, fireworks, lift restart, tavern, beer minigame
- M6 Audio/voices, polish, WebGL performance, local test, then GitHub Pages

## 6. Notes for the WebGL build
- GitHub Pages can't set `Content-Encoding` headers: use Brotli/Gzip with **Decompression
  Fallback** enabled, or disable compression.
- No threads or local file IO in WebGL; keep per-frame allocations low; stream audio as Vorbis.
