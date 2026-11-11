# Testing

## The checks: `tools/check.sh --fast` and `tools/check.sh` <a id="check"></a>

When to run them is `AGENTS.md#checks`. The script's header says what each one runs; both stop at the first failing step.

- **The fast check** <a id="fast-check"></a>, `tools/check.sh --fast`: about 10 s, no Godot. It checks the doc citations and the stable-image guard (`docs/art-direction.md#stable-image`), applies every formatter of `docs/toolchain.md#format-tools`, lints with the safe fixes applied, type-checks the Python tools and builds. What it reformats belongs to the change.
- **The full check** <a id="full-check"></a>, `tools/check.sh`: it changes no file. The doc citations (`tools/docrefs.py`) and the stable-image guard, every file formatted and lint-clean, the build (warnings are errors, and the build holds the C# to `.editorconfig` and the analyzers), the headless import (every shader compiled), then every headless check and the unit tests (`tests/TheDeep.Tests`, xUnit over the core) at once, one Godot per check. When formatting or lint fails, run the fast check and finish by hand only what a linter asks for.

The headless checks are one driver each in `scripts/debug/`; each driver's docblock says what it proves. They cover every room and the map (solver, geometry, the network and its room table, the line book), every species, every exit crossed, each ability through the real inputs, each game system in place, the fight, the encounters, a whole playthrough, and the sequel world as a fixture (`--world sequel`). Logs go to `.godot/check/`.

**It stays fast** <a id="check-fast"></a> (owner): about 45 s on the desktop. Each step prints its time; when it grows, cut the slowest step's cost, never a check.

A check that prints its passing line and then crashes or reports a leak while Godot quits is a rare race between the .NET finalizers and the engine's shutdown (`Main._ExitTree` guards it): rerun once and keep the log. A failure that comes back is real.

**Files no formatter covers** <a id="unformatted"></a>: Godot's own files (`.tscn`, `.tres`, `project.godot`, `.import`, `.uid`), which the engine rewrites in its own layout; the shaders, which have no standard formatter and are held by the headless import instead; the world tables (`.tsv`); the build files.

`godot --path .` runs the last *built* assembly; it does not compile. The check builds, so after a passing check the owner's launch command is up to date.

## Commands <a id="commands"></a>

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
| Re-bake the materials' texture sets after changing a pattern (`SurfaceBake`), then import them | `godot --headless --path . -- --bake-surfaces && godot --headless --import` |
| Print the map (built rooms and the network: ways, placements, orders) and draw it | `tools/map.sh` |
| Where the geometry check's planner stops in a room | `godot --headless --path . -- --room MechanicsLab --validate-rooms --reach-report <Room>` (`--abilities …`, `--slack-report` for the clumsy pilgrim) |
| Look-dev captures and frame times | `tools/capture.sh [label]` (`docs/testing.md#lookdev`; `--world sequel` for another world's rooms) |
| UI screens to PNGs (windowed) | `godot --path . --disable-vsync -- --room Undercroft --ui-shot <dir>` |
| Windowed capture check (needs a GPU; after touching capture code) | `godot --path . -- --capture-test` (add `--disable-vsync` before `--` for the raw cost) |

The full list of arguments is in `Main`; each tool's options are in its header.

## Seeing the game <a id="lookdev"></a>

Headless runs render nothing. For any visual change, render before and after and look (`docs/art-direction.md#judge-images`):

```
tools/capture.sh before-fog        # before the change (optional label)
# … make the change …
tools/capture.sh after-fog         # renders, then compares with the previous run
```

Each run opens a small window and needs the GPU to itself (`docs/toolchain.md#gpu`): about 8 minutes for every room, under a minute for one. While working, capture only the rooms the change touches (`--rooms`); a change shared by many rooms captures them all once before and once at the end, and iterates on the one or two rooms that show the problem (owner: captures cost time and tokens). A run writes `lookdev/<date>_<time>[-label]/` (git-ignored): every photo spot, the motion and interaction reels as contact sheets (`MotionReel` lists them), frame times per room (`perf*.json`), and `compare/sheet.png`, the before and after of every spot with the share of pixels that changed. **Look at `compare/sheet.png`.** Runs are deterministic (fixed 60 fps game time), so any visible change in the sheet is real.

Reading `perf*.json` against the budget (`docs/principles.md#performance`): a slow frame with a garbage collection and a normal GPU time is garbage; one with a long GPU time is the GPU (a room reloaded after a fall builds its shaders anew, a frame or two of 40–70 ms under the veil).

**Photo spots** are `PhotoSpot` markers in each room scene. Add one for anything a visual change should be judged on; keep a spot's camera out of the foreground layer unless that is the point.

For a raw frame sequence, Movie Maker works; `--disable-vsync` matters, or the compositor throttles a background window to about 1 fps:

```
godot --path . --disable-vsync --fixed-fps 60 --quit-after 240 --write-movie /path/to/out/f.png -- --smoke
```

## Owner captures (F12 / F10) <a id="owner-captures"></a>

In every build: **F12** (or the pad's Share) saves a screenshot, **F10** starts and stops a video, both in `captures/` (git-ignored) with a `.txt` of the game state beside them (`OwnerCapture`).

When the owner says "look at the last screenshot/video": `ls -t captures | head`, read the `.txt` to reproduce (room, position, abilities), and look at the PNG, or pull frames from the MP4 with `ffmpeg -i X.mp4 -vf fps=2 out/f%03d.png`.

## Owner playtest <a id="playtest"></a>

What the owner does to test a build. Remind them of this when handing over:

1. `godot --path .` resumes from the last shrine rested at; `-- --new` starts over; the hand-over's command (`AGENTS.md#session`) goes straight to the new content.
2. Controls are on screen in debug builds, for the keyboard or the pad in hand (`docs/gameplay.md#controls`). F1–F5 toggle the abilities, F6 cycles the biome, F7 shows the debug readout. The room's name and the pilgrim's position in it sit top right, so remarks and screenshots say where they were made.
3. To watch the machines: `godot --path . -- --room MachineArena`.
4. To report something: F12 (or a video with F10), then "look at the last screenshot".
5. Feedback is given in plain words ("floaty jump", "camera lags"); the agent turns it into tuning. The owner may also edit `resources/player_tuning.tres` and relaunch, with no rebuild.
