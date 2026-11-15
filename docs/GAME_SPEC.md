# Game Spec — "Last Run" (working title)

The source of truth for what to build. Written from the owner's design brief. Each section ends with
**acceptance criteria (AC)** that tests and playtests check against. If something here conflicts
with code, this file wins unless the owner says otherwise.

## 0. Overall
- 3D, runs in a desktop browser (Unity 6 → WebGL), hosted on GitHub Pages after local testing.
- Art style: stylised comic-book look — cel/toon shading, bold ink outlines, clear character
  detail, moody dusk palette. Original characters and assets only (inspired by the
  "graphic-novel adventure game" look, not copying any game).
- Gameplay is first person, with a subtle ski-goggle frame around the screen.
- Theme: New Year's. The day ends with an après-ski party.

## 1. Main menu — the chair lift (looping)
- Static front-on camera of a 4-seat chair lift moving up the mountain, carrying four friends:
  - **Boy 1 — snowboarder**
  - **Girl 1 — skiblades** (short skis)
  - **Boy 2 — carving skis**
  - **Girl 2 — twin-tips**
- Everything alive: lift moves, background trees scroll past, skiers in the slopes below, falling
  snow, small idle animations (adjusting goggles, zipping jacket, kicking skis, looking around).
- The scene loops seamlessly until a character is selected. Menu items: select character
  (4 choices, showing each one's trait), settings, quit/credits.

AC: loop runs indefinitely without a visible seam; ≥ 4 distinct idle animations; selecting any
character starts the transition.

## 2. Intro cutscene (30–60 s, skippable)
- On selection: menu text, settings and buttons fade out; camera moves smoothly into the scene.
- Cuts between several camera angles (Timeline + Cinemachine).
- Voiced dialogue (local TTS: Kokoro or Piper) between the four friends:
  - general ski talk;
  - this is the last run of the day before the après-ski party at the bottom;
  - juicy relationship drama (creative freedom — keep it PG-13 and fun).
- A snowstorm rolls in during the scene; visibility drops; the friends get nervous, then panic
  a bit when visibility is very bad.
- They get off at the top. Transition to first person of the **selected** character opening a
  paper ski map, which becomes the next menu.
- A skip button/key jumps straight to the ski map.

AC: 30–60 s long; ≥ 4 camera angles; subtitles shown; storm intensity visibly ramps; skip works at
any moment; ends on the ski map in first person of the chosen character.

## 3. Ski-map menu (level select)
- An illustrated piste map held in first person. Five runs, drawn with the standard European
  colours and symbols:
  - **Green** (beginner, green circle)
  - **Blue** (blue square)
  - **Red** (red)
  - **Black** (black diamond)
  - **Off-piste** (orange/yellow dashed, warning symbol)
- Hovering a run shows its name, goal and best time/score. Clicking starts the level.
- After a skipped cutscene, the game lands here.

## 4. Controls (all levels)
| Input | Action |
|---|---|
| W (hold) | Crouch into a tuck → faster acceleration and higher top speed. (Owner's decision: W.) |
| A / D | Turn left / right. Holding leans the skis further: a short hold carves and gains speed, a long hold skids and brakes. |
| S | Snowplough (skis make a V) → brake |
| R | Put me back on the course (time penalty) |
| Esc | Pause, settings |
| Left mouse button (timed presses; Shift also works) | Pole push at start/low speed. A rhythm meter; timing window tightens as speed rises, until speed is high enough that poles are no longer needed. |
| Space | Jump |
| 1–5 | Tricks (grabs, spins, flips); some combine with WASD for harder variants and more points |
| Mouse | Look around (head look, independent of skiing direction within limits) |

First-person view: looking down shows skis, hands/gloves and poles; snowplough visibly makes the
V; crouch lowers the view; poles animate on a push. **C** switches between first and third person (the snow park starts in third person). The mouse looks around without clicking first.

AC: each of the above has a unit test on the physics model and is visible in first person.

## 5. Characters and traits
Each has one subtle advantage (about 3–8 %, noticeable but never dominant). No character should be
best on every level; it should depend on how the player skis.
| Trait | Effect |
|---|---|
| Tuck speed | Slightly higher top speed while crouched |
| Pole rhythm | Slightly wider pole-push timing window |
| Carver | More speed gained from a correct carve |
| Tight turner | Tighter turns with less speed loss, but slightly lower top speed |
Assign one trait to each of the four friends at design time (fitting their gear; e.g. the
carving-ski guy = Carver).

AC: an automated balance run shows that no single character has the best result on all five levels.

## 6. Levels
All four pistes have dense trees along the sides, so the player can't leave the run.

### Green — the kids' slope
- Gentle gradient, crowded with children and parents. Goal: reach the bottom as fast as possible
  without hitting anyone (a hit = crash + time penalty).
- Child types: (a) fast straight-liner, barely turns, screaming; (b) slow traverser crossing the
  whole slope, with or without turns; (c) child on a harness/leash with a parent behind;
  (d) ski-school line — an instructor followed by a row of kids.

### Blue — snow park
- Steeper; jumps (small/medium/large kickers), a halfpipe, rails/boxes for grinds, built like a
  real terrain park (lines of features, with knuckle/landing zones).
- Goal: score points. Points for height, trick difficulty (1–5 plus WASD modifiers), grinds and
  clean landings; a bad landing gives zero points for that trick or a crash.
- Trick points appear and count **only in this level**.

### Red — slalom
- Steeper still; classic slalom course of red/blue gates. Pass each gate on the correct side.
- Goal: fastest time. A missed or wrong-side gate adds a large time penalty.

### Black — downhill
- Steepest. Pure downhill race with subtle pits and bumps; hitting them at high speed = crash.
- Tucking (keep pressing) is essential; steer at the same time.

### Off-piste
- No tree walls. Open mountain with many kinds of trees, rocks, bushes, drops and uneven ground.
- Static obstacles that act as a labyrinth: a few routes reach the bottom, more routes end at
  cliffs or tree traps you can't get out of. Goal: fastest time.

AC (all levels): an automated bot can finish each level; off-piste has ≥ 2 valid routes and more
dead ends than valid routes; crash and reset work.

## 7. Finish area (bottom village)
- On finishing: result and leaderboard shown on screen while the player can still steer around.
- **New high score on any slope → fireworks over the bottom of that slope** (New Year's theme),
  with sound.
- **Lift base:** ski into it to restart → very short lift cutscene → fade to the ski map.
- **Timber tavern:** ski to its door to enter the après-ski — crowd, music, chatter in the
  background. Go to the bar to play the **beer-drinking minigame** (design: a timing/rhythm
  chug or a sliding-pint game; keep it light-hearted).

AC: fireworks trigger only on a new personal or global best; restart and tavern both reachable
from the finish area.

## 8. Leaderboards
- Local best times/scores, with eight rival skiers per slope to beat.
- Online leaderboards (Unity Gaming Services, free tier) are an optional later step.

## 9. Audio
- Wind and ski-on-snow sounds that depend on speed, carve and skid; lift sounds; crowd and music
  in the tavern; fireworks; voiced cutscene dialogue (local open TTS), with subtitles.
