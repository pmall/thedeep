# Testing

## The checks: `tools/check.sh --fast` and `tools/check.sh` <a id="check"></a>

When to run them is `AGENTS.md#checks`. The script's header says what each one runs; both stop at the first failing step.

- **The fast check** <a id="fast-check"></a>, `tools/check.sh --fast`: about 10 s, no Godot. It checks the doc citations and the stable-image guard (`docs/art-direction.md#stable-image`), applies every formatter of `instructions/toolchain.md#format-tools`, lints with the safe fixes applied, type-checks the Python tools and builds. What it reformats belongs to the change.
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
| The ground rules' findings per room (headroom, steps and flush seams, coplanar faces, meshes across the play plane; report-only) | `godot --headless --path . -- --room MechanicsLab --validate-rooms 2>&1 \| grep GROUND` (`--world sequel --room Strongroom` for the sequel) |
| The Slag held in play: its routes, falls and edges, each snag an error | `godot --headless --path . --fixed-fps 60 --quit-after 15000 -- --room Slag --slag-check 2>&1 \| grep SLAG` |
| Look-dev captures and frame times | `tools/capture.sh [label]` (`instructions/testing.md#lookdev`; `--world sequel` for another world's rooms) |
| UI screens to PNGs (windowed) | `godot --path . --disable-vsync -- --room Undercroft --ui-shot <dir>` |
| Windowed capture check (needs a GPU; after touching capture code) | `godot --path . -- --capture-test` (add `--disable-vsync` before `--` for the raw cost) |

The full list of arguments is in `Main`; each tool's options are in its header.

## Placing a seam brick on an exit <a id="seam-brick"></a>

A way's two exits are framed by the same brick and `SeamProfile` (`docs/gameplay.md#seam-rule`): a `Doorway` on a west or east exit (`res://scenes/kit/Doorway.tscn`, `res://resources/seams/doorway.tres`), a `Hatch` on a down or up one (`Hatch.tscn`, `hatch.tres`). In each room's `.tscn`, grepped, never read whole:

1. Delete the exit's `Size` line; add the two ext_resources and `[node name="Doorway" parent="Exits/<Exit>" instance=ExtResource(...)]` (or `Hatch`) with `Profile = ExtResource(...)`, plus `Material` when the room's blocks there carry one, and `Surface = 1` where a Mag-grip climb runs up that wall (the wall between two framed openings then comes out flush with the frames' face, so the climb stays one metal face: `Undercroft/WallEast`).
2. Trim the room's own blocks out of the frame's span (`tools/seamblocks.py <scene> x0 x1 y0 y1` lists what reaches in). A doorway's spans marker.x ± 2.5 m, y −2..0 (sill) and 4..6 (lintel) from the marker; a hatch's, marker.x ± 3.5 m and marker.y ± 1 (the slab either side of the hole), its marker in the slab's middle: −1 in a floor at y −2..0.
3. A camera bound within 0.5 m past a doorway's frame moves to its end; a hatch's two rooms bound theirs at the slab's far face, so the seam check sees only the frame; where that far face would push a room into a neighbour on the map, its camera stops at the marker instead (`Slag`, under the Chimney).
4. A marker moved moves its room: a loop pins rooms (`MapLayout`), but both markers of one seam moved by the same amount is safe, and a dead-end room moves freely.
5. Delete the seam's lines from `seams.tsv`; run `godot --headless --path . -- --room MechanicsLab --validate-rooms 2>&1 | grep -E "ROOM CHECK|ROOMS OK"` (~15 s).
6. Capture each side from a photo spot about 12 m out facing the opening (`Narthex/EastDoor`, `DrownedHall/WestDoor`, `Nave/Hatch`, `Cistern/FlueTop`): `tools/capture.sh <label> --rooms "A B"`; finish with `tools/check.sh`.

A way water runs through takes `Flooded = true` on both its bricks: each room's swum `WaterBody` must reach the frame's outer face (the room check fails otherwise), and the brick draws the passage's front where the water's own front stops short (a pool beside a doorway keeps its front off the wall over it, ending at the marker: `Vault/Water`). A one-way drain (a flooded well sunk into, `Descent/Sluice`) takes `drain.tres`, its slab as thick as the floor between the two rooms' spaces; its grate is the lower room's own, at the hole's bottom (it holds the pilgrim under it, and an arrival-only exit's area may hold a solid), and the upper room, whose exit's area must stay clear, bounds its camera at the grate's top instead of the slab's underside, so the seam check never compares the grate. A lift whose cage rides up through a hatch takes `shaft.tres` (a 1 m collar in the floor's middle, the rest of the floor each room's own: a rider crossing up arrives at the collar's top, on the head lift that carries it `HeadRise` up to the floor; each camera stops at the collar's far face) on both ends, and each room's `Lift` names the exit as its `Through` (with `node_paths=PackedStringArray("Through")` on its node line): the lower room's lift keeps its own floor and is set in line under the hole, rising to the hand-off; the upper room's is the shaft's head, placed at the hand-off (`Undercroft/LiftHead`).

A doorway's lintel stands alone: a room ceiling or wall running on beside it at its height leaves a joint line, so it is cut back to the lintel's top (`Kiln`, the landing under the forbidden door), and a pocket between the lintel's top and the room's wall or roof is filled to one face with it (`Slag/EastWall`). A 6 m opening takes `portal.tres` (`Kiln/East`), another height its own profile. A `LockedDoor` takes the plain doorway: its gate stands in the frame and rises through the lintel into the room's own wall over it, as wide as the gate and at least 4.5 m over the lintel's top (`Kiln/EastWall`). The plain profiles don't cover a `FalseWall`.

A drop meant to read as bottomless (a chasm open wall to wall, `Brink/Down`) takes `chasm.tres` on the `Hatch` (its frame spans marker.x ± 12 m), its marker centered between the pit's walls so the slab either side is their feet: the pit never shows a floor, and the room below opens its roof as wide.

## Seeing the game <a id="lookdev"></a>

Headless runs render nothing. For any visual change, render before and after and look (`docs/art-direction.md#judge-images`):

```
tools/capture.sh before-fog        # before the change (optional label)
# … make the change …
tools/capture.sh after-fog         # renders, then compares with the previous run
```

Each run opens a small window and needs the GPU to itself (`instructions/toolchain.md#gpu`): about 8 minutes for every room, under a minute for one. While working, capture only the rooms the change touches (`--rooms`); a change shared by many rooms captures them all once before and once at the end, and iterates on the one or two rooms that show the problem (owner: captures cost time and tokens). A run writes `lookdev/<date>_<time>[-label]/` (git-ignored): every photo spot, the motion and interaction reels as contact sheets (`MotionReel` lists them), frame times per room (`perf*.json`), and `compare/sheet.png`, the before and after of every spot with the share of pixels that changed. **Look at `compare/sheet.png`.** Runs are deterministic (fixed 60 fps game time), so any visible change in the sheet is real.

Reading `perf*.json` against the budget (`instructions/principles.md#performance`): a slow frame with a garbage collection and a normal GPU time is garbage; one with a long GPU time is the GPU (a room reloaded after a fall builds its shaders anew, a frame or two of 40–70 ms under the veil).

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
