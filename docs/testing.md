# Testing

## The check: `tools/check.sh` `#check`

Run it after every change and before every commit. In order, stopping at the first failure: the doc citations (`tools/docrefs.py`, `AGENTS.md#doc-rules`), the build (warnings are errors), the headless import (every shader compiled); then every headless check and the unit tests (`tests/TheDeep.Tests`, xUnit over the core; `VideoEncoderTests` run the real `ffmpeg`) all at once, one Godot per check, each reporting its result and its time. Logs go to `.godot/check/`.

**It stays fast** `#check-fast` (owner, 2026-11-02), without checking less: about 40 s on the desktop (it was three minutes). Each step prints its time; when it grows, cut the slowest. The longest are the room check (`WorldCheck`), the travel check (`TravelValidator`) and the playthrough (`PlaythroughCheck`).

The headless checks, one driver each in `scripts/debug/`; the driver's docblock says what it proves and what it prints:

| Check | Driver |
|---|---|
| The main scene boots idle for 300 frames, then the scripted smoke run | `SmokeDriver` |
| Every room of the world, then the map: layout, solver, geometry, inside, room for error, the network | `RoomValidator` (proof in the core's `WorldCheck`) |
| Every machine species, alone in the arena | `SpeciesValidator` |
| Every open exit crossed | `TravelValidator` |
| The ledge assist and each ability through the real inputs, in the mechanics lab | `GripCheck`, `HarpoonCheck`, `DrillCheck`, `BallastCheck`, `HeatCheck` |
| A key and its locked door in place (the Sacristy's); power-ups taken (in the Sacristy); a conversation (the Narthex's Lamplighter) | `KeyCheck`, `PowerUpCheck`, `DialogueCheck` |
| Combat and the session in the lab | `CombatCheck` |
| Bosses, law breaks, lifts and the rooms where abilities are proven in place | `EncounterCheck` |
| The whole run, a new game to the end along the solver's route | `PlaythroughCheck` |

`godot --path .` runs the last *built* assembly; it does not compile. The check builds, so after a passing check the owner's launch command is up to date.

## Commands `#commands`

| What | Command |
|---|---|
| Check (build, import, headless checks, tests) | `tools/check.sh` |
| Build only | `dotnet build TheDeep.sln` |
| Unit tests only | `dotnet test tests/TheDeep.Tests/TheDeep.Tests.csproj` |
| Run the game (the prototype) | `godot --path .` |
| A new game | `godot --path . -- --new` |
| Start in a room, with the abilities a route there needs | `godot --path . -- --room <Name>` (`--abilities all`, `none` or `MagGrip,Drill` says otherwise) |
| Another world | `godot --path . -- --world <name>` (`worlds/<name>/world.tres`; every check takes it too) |
| Start with another biome | `godot --path . -- --biome volcanic` |
| One screenshot of a room's start, then quit (to captures/, as F12) | `godot --path . -- --room Undercroft --shot` (with `--paint NAME` to compare styles) |
| Paint with another style, or not at all (`docs/art-direction.md#signature`) | `godot --path . -- --paint poster` (`none` for the raw render) |
| Print the map (built rooms and the network: ways, placements, orders) | `tools/map.sh` |
| Where the geometry check's planner stops in a room | `godot --headless --path . -- --room MechanicsLab --validate-rooms --reach-report <Room>` (`--abilities …`, `--slack-report` for the clumsy pilgrim) |
| Look-dev captures and frame times | `tools/capture.sh [label]` (`docs/testing.md#lookdev`; `--paint NAME` for another paint style) |
| UI screens to PNGs (windowed) | `godot --path . --disable-vsync -- --room Undercroft --ui-shot <dir>` |
| Windowed capture check (needs a GPU; after touching capture code) | `godot --path . -- --capture-test` (add `--disable-vsync` before `--` for the raw cost) |

The full list of arguments is in `Main`; each tool's options are in its header.

## Seeing the game `#lookdev`

Headless runs render nothing. For any visual change, render before and after and look (`docs/art-direction.md#judge-images`):

```
tools/capture.sh before-fog        # before the change (optional label)
# … make the change …
tools/capture.sh after-fog         # renders, then compares with the previous run
```

Each run opens a small window for about a minute (it needs the GPU to itself, `docs/toolchain.md#gpu`) and writes `lookdev/<date>_<time>[-label]/` (git-ignored): every photo spot, the reels as contact sheets (`Motion.png` for animation, `Machines.png` for machines, `Combat.png`, `Grip.png`, `Harpoon.png`; `MotionReel`), frame times per room (`perf*.json`), and `compare/sheet.png`, the before/after of every spot with the share of pixels that changed. **Look at `compare/sheet.png`.** Runs are deterministic (fixed 60 fps game time), so any visible change in the sheet is real.

Reading `perf*.json` against the budget (`docs/principles.md#performance`): a slow frame with a garbage collection and a normal GPU time is garbage; one with a long GPU time is the GPU (a room reloaded after a fall builds its shaders anew, a frame or two of 40–70 ms under the veil).

**Photo spots** are `PhotoSpot` markers in each room scene. Add one for anything a visual change should be judged on; keep a spot's camera out of the foreground layer unless that is the point.

For a raw frame sequence, Movie Maker works; `--disable-vsync` matters, or the compositor throttles a background window to about 1 fps:

```
godot --path . --disable-vsync --fixed-fps 60 --quit-after 240 --write-movie /path/to/out/f.png -- --smoke
```

## Owner captures (F12 / F10) `#owner-captures`

In every build: **F12** (or the pad's Share) saves a screenshot, **F10** starts and stops a video, both in `captures/` (git-ignored) with a `.txt` of the game state beside them (`OwnerCapture`).

When the owner says "look at the last screenshot/video": `ls -t captures | head`, read the `.txt` to reproduce (room, position, abilities), and look at the PNG, or pull frames from the MP4 with `ffmpeg -i X.mp4 -vf fps=2 out/f%03d.png`.

## Owner playtest `#playtest`

What the owner does to test a build. Remind them of this when handing over:

1. `godot --path .` resumes from the last shrine rested at; `-- --new` starts over; the hand-over's command (`AGENTS.md#session`) goes straight to the new content.
2. Controls are on screen in debug builds, for the keyboard or the pad in hand (`docs/gameplay.md#controls`). F1–F5 toggle the abilities, F6 cycles the biome, F9 cycles the paint, F7 shows the debug readout. The room's name and the pilgrim's position in it sit top right, so remarks and screenshots say where they were made.
3. To watch the machines: `godot --path . -- --room MachineArena`.
4. To report something: F12 (or a video with F10), then "look at the last screenshot".
5. Feedback is given in plain words ("floaty jump", "camera lags"); the agent turns it into tuning. The owner may also edit `resources/player_tuning.tres` and relaunch, with no rebuild.
