# Testing

## The check: `tools/check.sh`

Run it after every change and before every commit. It stops at the first failure:

1. `dotnet build` (nullable on, warnings are errors).
2. `godot --headless --import`, failing on any error line.
3. Smoke test:
   - (a) The main scene boots headless for 300 frames with no errors.
   - (b) A scripted run (`-- --smoke`, `scripts/debug/SmokeDriver.cs`) presses run, jump and dash through the real input actions. It must print `SMOKE OK`: moved ≥ 10 m, full jump ≥ 2.9 m, every base state entered, never left Z = 0.
   - (c) Room check (`-- --room MechanicsLab --validate-rooms`, `scripts/debug/RoomValidator.cs`; run from the lab so no live room with machines sits beside the rooms it loads: that combination intermittently crashed the engine in a GC finalizer): every scene in `scenes/rooms/` must print no `ROOM CHECK` error and the run `ROOMS OK`. It checks the grid (block edges 0.5 m, kit piece origins 0.25 m), axis-aligned blocks, spawn and exits inside the camera bounds, exit requirements, exit targets that exist and lead back, and at least one photo spot. Then the **map** of the rooms in the game (`Room.InGame`; the arena and the lab are not): the rooms placed from their exits must line up around every loop and not overlap beyond 6 m at doorways (`MapLayout`); every room must be reachable from the start room, a `MapGoal` must be reachable, and no reachable state (room + abilities owned) may be a softlock (`MapSolver`, which prints the path to any softlock); it prints `MAP OK` with the achievable ability orders (with all five abilities on the map, fewer than 24 of 120 fails). Last, the **geometry check** (`ReachPlanner`): the base moveset is simulated with the live tuning (run, full and cut jumps, walk-offs, air dash, the body as a box colliding with the blocks) from every way into each room; an exit that needs nothing must be reached from some way in, and an exit that needs an ability must not be reached from a way in that needed none. Then the **whole game's draft** ([map-draft.md](map-draft.md), `MapDraft`): it must meet its targets (no softlock, every room reachable, each ability on one relic, ≥ 24 of 120 orders, each ability at ≥ 2 positions, 40–60 rooms, each law break on every route to the goal and in story order), and agree with the built rooms: each built room's exits (target and requirement), sealed exits (requirement) and relics must be the draft's ways and relics for that room; it prints `DRAFT OK`. `tools/map.sh` (or `-- --room MechanicsLab --validate-rooms --map-report`) also prints the whole graph, the room placements, every order, and the draft's ways and orders.
   - (d) Species check (`-- --room MachineArena --validate-species`, `scripts/debug/SpeciesValidator.cs`): every `resources/machines/*.tres` loads, its id is its file name, it has a behavior, its parts, chains, drivers and legs name existing joints (and no driver poses a leg joint), and its creed line is in `resources/text/lines.tres`. Then each species lives alone in the arena for 4 s without the player and 6 s with the player running, jumping and dashing next to it; any error, or a machine leaving the room, fails. It prints one line per species (layer, hostile or not, behavior, the acts seen) and `SPECIES OK` with the count per layer, the non-hostile count and likely duplicates (same behavior and same body).
   - (e) Travel check (`-- --validate-travel`, `scripts/debug/TravelValidator.cs`): every open exit of every room is crossed (the player put in its area, moving out); the player must arrive in the target room, the room must be placed on the map, and the player must stay there a second (no bounce back). Prints `TRAVEL OK`.
   - (f) Grip check (`-- --room MechanicsLab --grip-check`, `scripts/debug/GripCheck.cs`): Mag-grip through the real inputs in the mechanics lab: stick to a wall, climb, turn under a slab, wrap its end onto its top, hang under a block and wrap onto it, leap off, let go. Prints `GRIP OK`.
   - (g) Combat check (`-- --room MechanicsLab --combat-check`, `scripts/debug/CombatCheck.cs`): lingering before the lab's Votary gets the pilgrim struck; swings break it; a deadly blow brings the pilgrim back whole at the spawn with the Votary renewed; resting at the shrine heals and makes it the return point (a second death comes back there); walking into the relic takes its ability. Prints `COMBAT OK`.
4. xUnit tests in `tests/TheDeep.Tests`.

The unit tests include `VideoEncoderTests`, which run the real `ffmpeg` (a required tool, see [toolchain.md](toolchain.md)).

Logs go to `.godot/check/`. Never report work as done while the check fails.

`godot --path .` runs the last *built* assembly; it does not compile. The check builds, so after a passing check the owner's launch command is up to date.

Other commands:

| What | Command |
|---|---|
| Build only | `dotnet build TheDeep.sln` |
| Unit tests only | `dotnet test tests/TheDeep.Tests/TheDeep.Tests.csproj` |
| Print the map (built rooms and the whole game's draft: ways, orders) | `tools/map.sh` |
| Run the game | `godot --path .` |

## Seeing the game (visual verification)

Headless runs render nothing. For any visual change, use the look-dev captures (they open a small window for a few seconds):

```
tools/capture.sh before-fog        # before the change (optional label)
# … make the change …
tools/capture.sh after-fog         # renders, then compares with the previous run
```

Each run renders the drowned hall, the Narthex, the machine arena and the mechanics lab (`ROOMS` in `tools/capture.sh`; the first one's frame times go to `perf.json`), into a fresh `lookdev/<date>_<time>[-label]/` (git-ignored) with:
- `<Room>-<Spot>.png`: every **photo spot** of both rooms at 1920×1080, no HUD. The room's machines are reset before each spot, so they are caught in the same pose every run;
- `Motion.png`: the **motion reel** (test room only, `scripts/debug/MotionReel.cs`): idle, run start, run cycle, stop, turn, jump, dash off a ledge and a hard landing, shot close and laid out as a contact sheet (frames in `motion/`). Judge animation changes on it;
- `Combat.png`: the **combat reel** (mechanics lab): swings at a Votary until it breaks, then lingering in front of it: its wind-up, the blow, the pilgrim thrown back and shimmering;
- `Grip.png`: the **grip reel** (mechanics lab): onto a metal wall, up it, under the slab and around its end; a jump into a block's underside, along it and over; a leap off the wall;
- `Machines.png`: the **machine reel** (arena, same file): a Votary striking the pilgrim who steps in front of it, the Gleaner bolting, the Limpet waking to a landing, crawling down its reliquary and drilling, the Lamplighter following and swapping sides; frames in `machines/`. Judge machine looks and behaviors on it;
- `perf.json` (test room) and `perf-MachineArena.json`: frame, GPU and CPU render times (average, p99, worst) over a 900-frame scripted run with the gameplay camera, checked against the 10 ms budget;
- `run.txt` (commit, label), `godot.log` and `godot-MachineArena.log`;
- `compare/`: before/after images against the previous run (or `--against DIR`), one per spot plus `sheet.png` with all of them, and the share of pixels that changed. Look at `compare/sheet.png`.

`tools/capture.sh label --biome volcanic` renders with another `BiomeProfile` (from `resources/biomes/`). `tools/capture.sh --compare A B` rebuilds a sheet for two existing runs. Runs are deterministic (fixed 60 fps game time, vsync off): two runs of the same build are pixel-identical except for a few GPU particles (a few thousand pixels, shown as 0.0%), so any visible change in the sheet is real. The sheet needs `python3-pil` (installed).

**Photo spots** are `PhotoSpot` markers under `PhotoSpots` in each room scene (`scripts/rooms/PhotoSpot.cs`). The marker *is* the camera: X/Y is where it looks, Z its distance from the play plane (gameplay uses 22). It also sets where the player stands, which way they face, and an optional FOV. Depth of field is refocused on the play plane at the spot's distance. Keep a spot's camera out of the foreground layer (Z 6–9 in the test room) unless that is the point. Add spots for anything a visual change should be judged on; the test room has Start, CloseUp, Platforms, Materials (rock, metal and glyphs up close), Shaft, Wide, Hall (racks and a wreck from the floor) and Arcade (both tiers of the arcade, pipes, cables).

How it works: `-- --lookdev <dir> [--size WxH]` adds `LookDevDriver`, which renders at a fixed size whatever the window (viewport stretch mode), grabs the settled frame of each spot with `FrameGrabber`, then plays `DemoInput` while measuring. Movie Maker mode is not used: it would write every frame, while `--fixed-fps 60` gives the same deterministic timing.

For a raw frame sequence (an animation, say), Movie Maker still works:

```
godot --path . --disable-vsync --fixed-fps 60 --quit-after 240 --write-movie /path/to/out/f.png -- --smoke
```

`--disable-vsync` matters: with vsync on, the compositor throttles a background window to about 1 fps and the run looks hung.

## Owner capture keys (F12 / F10)

In every build, not only debug (`scripts/capture/OwnerCapture.cs`):

- **F12** (or the pad's Share button): screenshot. Saves `captures/<date>_<time>.png` at the project root (git-ignored) plus a `.txt` with the same name: time, room, player position, velocity, state, facing, abilities, camera position, FPS, window size, build.
- **F10**: start/stop video. Saves `captures/<date>_<time>.mp4` (H.264, 60 fps, max 1920 wide) plus a `.txt` with the state at the start, duration and frame counts. A pulsing red **REC** dot and timer show top right (they appear in the video). The MP4 is fragmented, so it stays playable even if the game crashes; closing the window mid-recording finishes the file.

When the owner says "look at the last screenshot/video": `ls -t captures | head`, read the `.txt` to reproduce (room, position, abilities), and look at the PNG, or pull frames from the MP4 with `ffmpeg -i X.mp4 -vf fps=2 out/f%03d.png`.

How it works: frames are copied back from the GPU asynchronously (`FrameGrabber`, no stall), into pooled buffers (no garbage), and piped to an `ffmpeg` process (`h264_nvenc`, falling back to `libx264`) by a writer thread. A wall clock (`VideoClock`) keeps the video in real time whatever the game's frame rate. Physics is fixed-step, so gameplay timing never depends on recording.

**Windowed capture check** (needs a GPU and a window, so it is not in `tools/check.sh`). Run it after touching capture code:

```
godot --path . -- --capture-test                    # realistic: vsync on
godot --path . --disable-vsync -- --capture-test    # raw cost
```

It runs the player around, presses F12 then records 10 s with F10, checks both files were saved, and prints frame times, render CPU/GPU times and GC counts without and with recording. Measured 2026-10-24 at 1600×900 (RTX 3060): with vsync on, every frame stayed at 16.67 ms while recording (0 missed frames). With vsync off, the average frame went from 1.8 to 2.2 ms (about 3 ms per grabbed frame, mostly native readback), and render CPU/GPU times were unchanged. If a heavy scene ever drops frames while recording, lower `OwnerCapture.VideoFps` to 30 in `scenes/ui/OwnerCapture.tscn`.

## Owner playtest

What the owner does to test a build. Remind them of this when handing over:

1. `cd ~/workspace/deep && godot --path .`
2. Controls are in [gameplay.md](gameplay.md). The essentials:

   | Action | Keyboard | Gamepad |
   |---|---|---|
   | Move | A / D or arrows | Stick or d-pad |
   | Jump (hold for height) | Space or Z | A |
   | Dash | Shift or C | RB or RT |
   | Attack | J or X | X |
   | Rest at a shrine | E | Y |
   | Map | M or Tab | LB |
   | Respawn | R | Back |
   | Screenshot | F12 | Share |
   | Start/stop video | F10 | — |

3. To watch the machines: `godot --path . -- --room MachineArena`. Walk right from the start: three Votaries pray toward a shrine (whoever lingers in front of one is struck after a wind-up: run or dash past, or take the gallery over them), then a Gleaner picking at scrap (it flees), then a Limpet asleep on a metal block (land a jump near it to wake it). A Lamplighter floats near the start and follows you once it has seen you. Hits only throw sparks for now: damage comes with M6.
4. In debug builds, F6 cycles the room through the biome profiles (Flooded, Volcanic draft). `godot --path . -- --biome volcanic` starts with one, and `-- --room <Name>` starts in `scenes/rooms/<Name>.tscn`.
5. The game resumes from the last shrine rested at; `godot --path . -- --new` starts over. F3 shows the debug readout (movement state, velocity, floor contact, dash cooldown, abilities).
6. To report something, press F12 (or record with F10) and say "look at the last screenshot". Files land in `captures/`.
7. Feedback is given in plain words ("floaty jump", "camera lags"). The agent translates it into tuning changes. The owner may also edit `resources/player_tuning.tres` and relaunch, which needs no rebuild.
