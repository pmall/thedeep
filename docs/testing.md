# Testing

## The checks: `tools/check.sh --fast` and `tools/check.sh` `#check`

Two commands (`AGENTS.md#checks`); the script's header says what each one runs, and both stop at the first failing step.

- **The fast check** `#fast-check`, `tools/check.sh --fast`: about 10 s on the desktop, no Godot. It formats the whole tree (it applies every formatter of `docs/toolchain.md#format-tools`), lints with the safe fixes applied, type-checks the Python tools (`AGENTS.md#truthful-types`) and builds. It ends every turn that changed files (`AGENTS.md#session`), and what it reformats belongs to that change.
- **The full check** `#full-check`, `tools/check.sh`: what the definition of done means (`AGENTS.md#done`). It changes no file. In order: the doc citations (`tools/docrefs.py`, `AGENTS.md#doc-rules`); every file formatted and lint-clean, verified; the build (warnings are errors, and the build itself holds the C# to the layout and style rules of `.editorconfig` and to the analyzers, `docs/toolchain.md#format-tools`); the headless import (every shader compiled); then every headless check and the unit tests (`tests/TheDeep.Tests`, xUnit over the core; `VideoEncoderTests` run the real `ffmpeg`) all at once, one Godot per check, each reporting its result and its time. When formatting or lint fails, run the fast check, look at what it changed and finish the change by hand only where a linter asks for it.

Logs go to `.godot/check/`. A check that prints its passing line and then crashes or reports a leak while Godot quits is the .NET finalizers racing the engine's shutdown, a rare flake under the check's load that was never reproduced on demand; the game guards against it by freeing its nodes and finalizing their C# wrappers before the engine tears down (`Main._ExitTree`). If one still happens, rerun once and keep its log for the cause: a failure that comes back is real.

**Files no formatter covers** `#unformatted`, each for its reason: Godot's own files (`.tscn`, `.tres`, `project.godot`, `.import`, `.uid`), which the engine writes and rewrites in its own layout, so a formatter would fight it; the shaders (`.gdshader`, `.gdshaderinc`, `.glsl`, `.glslinc`), for which no formatter is standard (Godot has none for its shading language), held instead by the headless import, which compiles each one; the world tables (`.tsv`), data whose only layout is a tab between fields; the build files (`.csproj`, `.sln`, `Directory.Build.props`), a few lines written by the SDK and Godot.

**It stays fast** `#check-fast` (owner, 2026-11-02), without checking less: about 45 s on the desktop (it was three minutes). Each step prints its time; when it grows, cut the slowest. The longest are the room check (`WorldCheck`), the travel check (`TravelValidator`) and the playthrough (`PlaythroughCheck`).

The headless checks, one driver each in `scripts/debug/`; the driver's docblock says what it proves and what it prints:

| Check | Driver |
| -- | -- |
| The main scene boots idle for 300 frames, then the scripted smoke run | `SmokeDriver` |
| Every room of the world, then the map: layout, solver, geometry, inside, room for error, the network and its room table; then its line book: every line the code and its content name | `RoomValidator` (proof in the core's `WorldCheck`; the table: `RoomTable`; the lines: `LineCheck`) |
| Every machine species, alone in the arena | `SpeciesValidator` |
| Every open exit crossed low, mid and high in its opening, then back | `TravelValidator` |
| The ledge assist and each ability through the real inputs, in the mechanics lab | `GripCheck`, `HarpoonCheck`, `DrillCheck`, `BallastCheck` |
| A key and its locked door in place (the Sacristy's), a code and its seal (the Pyre's); power-ups taken (in the Sacristy); a conversation (the Narthex's Lamplighter); the Pyre's ambush; the Sacristy's hidden passage into the Cell; heat and Heat plating at the Basalt's slag channel | `KeyCheck`, `PowerUpCheck`, `DialogueCheck`, `AmbushCheck`, `SecretCheck`, `HeatCheck` |
| Combat and the session in the lab; the fight moveset (the chain, the hit stop, the down strike's bounce, the dash strike) | `CombatCheck`, `FightCheck` |
| Bosses, law breaks, lifts and the rooms where abilities are proven in place | `EncounterCheck` |
| The whole run, a new game to the end along the solver's route | `PlaythroughCheck` |
| The sequel world (`docs/roadmap.md#sequel-test`), a fixture: its rooms and map (from the Strongroom, its room without machines), every exit crossed, the whole run | `RoomValidator`, `TravelValidator`, `PlaythroughCheck` with `--world sequel` |

`godot --path .` runs the last *built* assembly; it does not compile. The check builds, so after a passing check the owner's launch command is up to date.

## Commands `#commands`

| What | Command |
| -- | -- |
| Fast check (format, lint, build) | `tools/check.sh --fast` |
| Full check (verified format and lint, build, import, headless checks, tests) | `tools/check.sh` |
| Build only | `dotnet build TheDeep.sln` |
| Unit tests only | `dotnet test tests/TheDeep.Tests/TheDeep.Tests.csproj` |
| Run the game (the prototype) | `godot --path .` |
| A new game | `godot --path . -- --new` |
| Start in a room, with the abilities and items a route there needs | `godot --path . -- --room <Name>` (`--abilities all`, `none` or `MagGrip,Drill` says otherwise; `--items SacristyKey` adds items) |
| Another world | `godot --path . -- --world <name>` (`worlds/<name>/world.tres`; every check takes it too): `--world sequel --new` plays the sequel world |
| Start with another biome | `godot --path . -- --biome volcanic` |
| One screenshot of a room's start, then quit (to captures/, as F12) | `godot --path . -- --room Undercroft --shot` |
| Print the map (built rooms and the network: ways, placements, orders) | `tools/map.sh` |
| Where the geometry check's planner stops in a room | `godot --headless --path . -- --room MechanicsLab --validate-rooms --reach-report <Room>` (`--abilities …`, `--slack-report` for the clumsy pilgrim) |
| Look-dev captures and frame times | `tools/capture.sh [label]` (`docs/testing.md#lookdev`; `--world sequel` for another world's rooms) |
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

Each run opens a small window (it needs the GPU to itself, `docs/toolchain.md#gpu`): about 8 minutes for every room, under a minute for one. While working, capture only the rooms the change touches (`--rooms`); a full run is for a change that reaches every room. A run writes `lookdev/<date>_<time>[-label]/` (git-ignored): every photo spot, the reels as contact sheets (`Motion.png` for animation, `Machines.png` for machines, `Combat.png` (strikes on the lab's Votary), `Strikes.png` (the chain, the down strike and the dash strike in the open, shot every tick or two), `Grip.png`, `Harpoon.png`, `Swim.png` (swimming and the dive), `Moments.png` (resting at a shrine), and the interactables used: `Door.png` (the Narthex's locked door opening for its key), `Taking.png` (the Sacristy's power-up taken, its false wall found), `Pickup.png` (the Scriptorium's key taken), `Lever.png` (the Kiln's lever pulled, its gate rising); `MotionReel`), frame times per room (`perf*.json`), and `compare/sheet.png`, the before/after of every spot with the share of pixels that changed. **Look at `compare/sheet.png`.** Runs are deterministic (fixed 60 fps game time), so any visible change in the sheet is real.

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
2. Controls are on screen in debug builds, for the keyboard or the pad in hand (`docs/gameplay.md#controls`). F1–F5 toggle the abilities, F6 cycles the biome, F7 shows the debug readout. The room's name and the pilgrim's position in it sit top right, so remarks and screenshots say where they were made.
3. To watch the machines: `godot --path . -- --room MachineArena`.
4. To report something: F12 (or a video with F10), then "look at the last screenshot".
5. Feedback is given in plain words ("floaty jump", "camera lags"); the agent turns it into tuning. The owner may also edit `resources/player_tuning.tres` and relaunch, with no rebuild.
