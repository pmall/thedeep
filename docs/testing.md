# Testing

## The check: `tools/check.sh`

Run it after every change and before every commit. It stops at the first failure:

1. `dotnet build` (nullable on, warnings are errors).
2. `godot --headless --import`, failing on any error line.
3. Smoke test:
   - (a) The main scene boots headless for 300 frames with no errors.
   - (b) A scripted run (`-- --smoke`, `scripts/debug/SmokeDriver.cs`) presses run, jump and dash through the real input actions. It must print `SMOKE OK`: moved ≥ 10 m, full jump ≥ 2.9 m, every base state entered, never left Z = 0.
   - (c) Room check (`-- --room MechanicsLab --validate-rooms`, `scripts/debug/RoomValidator.cs`; run from the lab so no live room with machines sits beside the rooms it loads; after freeing each room it collects garbage and waits for the finalizers, since C# wrappers finalized on the GC thread while the next room was being set up crashed the engine (`gchandle.is_released()`), at times 4 runs in 5): every scene in `scenes/rooms/` must print no `ROOM CHECK` error and the run `ROOMS OK`. It checks the grid (block edges 0.5 m, kit piece origins 0.25 m), axis-aligned blocks, spawn and exits inside the camera bounds, exit requirements, exit targets that exist and lead back, and at least one photo spot. Then the **map** of the rooms in the game (`Room.InGame`; the arena and the lab are not): the rooms placed from their exits must line up around every loop and not overlap beyond 6 m at doorways (`MapLayout`); every room must be reachable from the start room, a `MapGoal` must be reachable, and no reachable state (room + abilities owned) may be a softlock (`MapSolver`, which prints the path to any softlock); it prints `MAP OK` with the achievable ability orders (with all five abilities on the map, fewer than 24 of 120 fails). Then the **geometry check** (`ReachPlanner`): the moveset is simulated with the live tuning (run, full and cut jumps, walk-offs, air dash, the body as a box colliding with the blocks, riding lifts from their lower stop) from every way into each room, and all five abilities with it: **Mag-grip** grips a metal wall it pushes into (walking, jumping, falling, not dashing) or a metal ceiling it rises into, then walks the whole metal surface connected there (along faces, round corners, onto a top) and leaps off walls or drops from ceilings anywhere on it; the **Harpoon** zips between the room's anchors; the **Drill** bores every cracked block it reaches; **Ballast** sinks and rises anywhere in the water connected to where it went in (every planner floats in water with the player's own water motion); **Heat plating** walks through heat zones (without it they are space no body enters). An exit that needs nothing must be reached from some way in, and an exit that needs an ability must not be reached from a way in that needed none; an exit an ability opens must be reached by the planner with it (plus what the way in guarantees) from some way in, each option of a requirement on its own (`MagGrip | Harpoon`: by gripping and by zipping), abilities needed together (`Ballast & Drill`) by all of them. Sealed exits (rooms not built yet) are held to their requirements too, so a gate is right before the room behind it exists. Last, the **inside check** (`MapSolver.SolveInside`, printing `INSIDE OK`): the solver again, but a room is no longer one place: from waking in the start room, every (room, way in, abilities owned) state reachable is walked by the planner from that way in, with those abilities; it takes the relics it reaches (walking again with them) and leaves only through the exits it reaches. No such state may be a softlock, the goal's `MapGoal` area must be walked into, and every relic taken. This catches what the room-level solver trusts: a relic behind a wall (the Reliquary's, until 2026-11-01), a drop into the part of a room that only an ability leads out of. It also demands **room for error**: every state is walked again by a clumsier pilgrim (`MoveLimits.Clumsy`: jumps 18% lower, dashes 35% shorter, runs 20% slower, no ledge assist, take-offs 0.3 m inside a ledge's ends), which must reach every way out, relic and goal the exact one reaches (on 2026-11-01, 3 m ladders in eight rooms failed it, the DrownedHall's among them; then, with the bar raised from 10/15/8% to 18/35/20%, the Coldstacks' 8 m well and the Crucible's hole up to the Quench). Runs the rooms in parallel (about 50 s). To see where a planner stops: `-- --room MechanicsLab --validate-rooms --reach-report <Room>` prints the ledges and exits each way in reaches; add `--abilities MagGrip,Drill` to walk every way in with those, `--slack-report` for the clumsy pilgrim's reach. Then the **whole game's draft** ([map-draft.md](map-draft.md), `MapDraft`): it must meet its targets (no softlock, every room reachable, each ability on one relic, ≥ 24 of 120 orders, each ability at ≥ 2 positions, 40–60 rooms, each law break on every route to the goal and in story order), and agree with the built rooms: each built room's exits (target and requirement), sealed exits (requirement) and relics must be the draft's ways and relics for that room; it prints `DRAFT OK`. `tools/map.sh` (or `-- --room MechanicsLab --validate-rooms --map-report`) also prints the whole graph, the room placements, every order, and the draft's ways and orders.
   - (d) Species check (`-- --room MachineArena --validate-species`, `scripts/debug/SpeciesValidator.cs`): every `resources/machines/*.tres` loads, its id is its file name, it has a behavior, its parts, chains, drivers and legs name existing joints (and no driver poses a leg joint), and its creed line is in `resources/text/lines.tres`. Then each species lives alone in the arena for 4 s without the player and 6 s with the player running, jumping and dashing next to it; any error, or a machine leaving the room, fails. It prints one line per species (layer, hostile or not, behavior, the acts seen) and `SPECIES OK` with the count per layer, the non-hostile count and likely duplicates (same behavior and same body).
   - (e) Travel check (`-- --validate-travel`, `scripts/debug/TravelValidator.cs`): every open exit of every room is crossed (the player put in its area, moving out); the player must arrive in the target room, the room must be placed on the map, and the player must still be there once it has landed and stood a second (no bounce back, and a drop through a hatch never falls on into a doorway below, like the Scriptorium's flue over the Undercroft's west door). Prints `TRAVEL OK`.
   - (f) Grip check (`-- --room MechanicsLab --grip-check`, `scripts/debug/GripCheck.cs`): the ledge assist, then Mag-grip, through the real inputs in the mechanics lab: a full jump at a rock step 3.6 m high lands on its top (the assist; without it the jump falls short); stick to a wall, climb, turn under a slab, wrap its end onto its top, hang under a block and wrap onto it, leap off, let go; then, at pieces it places in the lab, climb a pier's metal foot and let go where rock goes on above it, and walk down a metal wall into a tank of water and let go to swim. Prints `GRIP OK`.
   - (f2) Harpoon check (`-- --room MechanicsLab --harpoon-check`, `scripts/debug/HarpoonCheck.cs`): the Harpoon through the real inputs at the lab's two anchors: none in sight without the ability; the near one in sight with it; fire, pull, let go rising with the air dash back; chain to the second (the first skipped); none in sight behind the slab or out of range; a jump during the pull cancels into a jump. Prints `HARPOON OK`.
   - (f3) Drill check (`-- --room MechanicsLab --drill-check`, `scripts/debug/DrillCheck.cs`): the Drill through the real inputs, against a cracked wall and a cracked slab it places in the lab: without the Drill a dash stops at the wall and leaves it whole, and pressed against it the pilgrim says it would give to a bore (the gate hint); with it the dash bores through and is still a dash past it; down + dash on the slab dives through it to the floor; down + dash in the air dives and stops on sound rock; down + dash on sound rock is a dash. Prints `DRILL OK`.
   - (f4) Ballast check (`-- --room MechanicsLab --ballast-check`, `scripts/debug/BallastCheck.cs`): water and Ballast through the real inputs, in a tank of water it places in the lab: dropped in, the pilgrim splashes and settles floating on its float line; without Ballast, holding down keeps it floating, and the pilgrim says it is too light to sink (the gate hint); with Ballast, holding down sinks it to the bottom, where it stays when let go; up rises it back to float; a jump leaves the water. Prints `BALLAST OK`.
   - (f5) Heat check (`-- --room MechanicsLab --heat-check`, `scripts/debug/HeatCheck.cs`): at a column of heat it places in the lab: without Heat plating, running into it burns (health taken, thrown back out) and the pilgrim says so (the gate hint); with it, the pilgrim runs through unharmed. Prints `HEAT OK`.
   - (f6) Playthrough check (`-- --playthrough-check`, `scripts/debug/PlaythroughCheck.cs`): the whole run, from a new game to the end. It reads the built map from the room scenes, asks the solver for a shortest route from the start to the goal (`MapSolver.Route`) and plays it: each way crossed through its real exit (as the travel check does), only once the pilgrim owns what it needs; in each room on the way, the relics taken (walking into them), the laws broken (pressing on at the lip through the real inputs), the bosses fought (through the real inputs, kept alive); at the Heart, the end card. It prints the route as played and `PLAYTHROUGH OK`. Moving inside rooms is the geometry check's to prove; this proves the flow (abilities in route order, gates, bosses, laws, the end).
   - (g) Combat check (`-- --room MechanicsLab --combat-check`, `scripts/debug/CombatCheck.cs`): lingering before the lab's Votary gets the pilgrim struck; swings break it; a deadly blow brings the pilgrim back whole at the spawn with the Votary renewed; resting at the shrine heals and makes it the return point (a second death comes back there); walking into the relic takes its ability. Prints `COMBAT OK`.
   - (g2) Encounter check (`-- --room Chapterhouse --encounter-check`, `scripts/debug/EncounterCheck.cs`): the flooded layer's end through the real inputs. In the Chapterhouse: walking into the arena drops both gates; the Precentor strikes the pilgrim lingering before it; swings break it (the pilgrim kept alive: the rules, not the balance); the gates rise and the fight is remembered; entering again, it stays broken and the gates stay up. At the Brink: the third law holds the pilgrim at the lip, pressing on breaks it (remembered), and over the lip the jump falls on into Rimefall and lands on its shelf. In the Choir: walking past a sleeping Chorister wakes none; a hard landing beside one wakes it (and not the one 7 m away). In the Counterweight: walking into the cage lets go of its brake, and it carries the pilgrim up the whole shaft (the feet on its floor all the way, the real physics of a body riding a moving floor) into the Undercroft, onto the grate. In the Calving: Mag-grip climbs the girder up the ice cliff and its top leads into the Coldstacks. In the Bore: the shaft drops into the chamber, the relic gives the Drill, a dash bores the cracked wall (remembered), and full jumps climb the tunnel's steps back to the hall. In the Sounding: the drop into the well, floating, a jump onto the shelf: the relic gives Ballast; sunk under the well's wall, up the shaft beyond, out onto the east hall. In the Mere: sunk through a lead to the lake bed and down its shaft into the Underice; sunk clear of the shaft, then the tanks blown, back up into the Mere. In the Coldstacks: down + dash on the ice plug dives through it and drops into the Vault's pool; out of the pool and into the arena, both gates fall; the Archivist dives at the pilgrim below it; swings break it while it hangs low (the pilgrim kept alive); the gates rise, remembered. In the Sanctum: the Hierarch's order is heard coming near; the second law holds the pilgrim at the forbidden door; pressing on breaks it (remembered). Prints `ENCOUNTER OK`.
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

Each run renders every room of the game, the machine arena and the mechanics lab (`ROOMS` in `tools/capture.sh`; the first one's frame times go to `perf.json`), into a fresh `lookdev/<date>_<time>[-label]/` (git-ignored) with:
- `<Room>-<Spot>.png`: every **photo spot** of both rooms at 1920×1080, no HUD. The room's machines are reset before each spot, so they are caught in the same pose every run;
- `Motion.png`: the **motion reel** (test room only, `scripts/debug/MotionReel.cs`): idle, run start, run cycle, stop, turn, jump, dash off a ledge and a hard landing, shot close and laid out as a contact sheet (frames in `motion/`). Judge animation changes on it;
- `Combat.png`: the **combat reel** (mechanics lab): swings at a Votary until it breaks, then lingering in front of it: its wind-up, the blow, the pilgrim thrown back and shimmering;
- `Grip.png`: the **grip reel** (mechanics lab): onto a metal wall, up it, under the slab and around its end; a jump into a block's underside, along it and over; a leap off the wall;
- `Harpoon.png`: the **harpoon reel** (mechanics lab): the near ring in sight, the line flying out, the pull, the release, then on to the second ring;
- `Machines.png`: the **machine reel** (arena, same file): a Votary striking the pilgrim who steps in front of it, the Gleaner bolting, the Limpet waking to a landing, crawling down its reliquary and drilling, the Lamplighter following and swapping sides; frames in `machines/`. Judge machine looks and behaviors on it;
- `perf.json` (the first room) and `perf-<Room>.json` (each other room): frame, GPU and CPU render times (average, p99, worst) over a 900-frame scripted run with the gameplay camera, checked against the 10 ms budget; the garbage collections in the run (`gc`, by generation) and the 12 slowest frames with their GPU and render CPU times and the collections in them (`slowest`): a slow frame with a collection and a normal GPU time is garbage, one with a long GPU time is the GPU (a room reloaded after a fall builds its shaders anew). The run can fall out of a room: its reload, under the veil, shows as a frame or two of 40–70 ms;
- `run.txt` (commit, label), `godot.log` and `godot-MachineArena.log`;
- `compare/`: before/after images against the previous run (or `--against DIR`), one per spot plus `sheet.png` with all of them, and the share of pixels that changed. Look at `compare/sheet.png`.

`tools/capture.sh label --rooms "Slag Quench"` renders only those rooms (a quick look while building one). `godot --path . -- --room Undercroft --ui-shot <dir>` (windowed) renders a gate hint over the room with the gameplay controls for the keyboard, Jump held, and the debug keys (`hint.png`), the gameplay controls for the gamepad, Dash held (`manual-pad.png`), after crossing a few exits the map screen (`map.png`), then the end card (`end.png`): judge UI changes on them. `tools/capture.sh label --biome volcanic` renders with another `BiomeProfile` (from `resources/biomes/`). `tools/capture.sh --compare A B` rebuilds a sheet for two existing runs. Runs are deterministic (fixed 60 fps game time, vsync off): two runs of the same build are pixel-identical except for a few GPU particles (a few thousand pixels, shown as 0.0%), so any visible change in the sheet is real. The sheet needs `python3-pil` (installed).

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
2. Controls are on screen in debug builds (bottom left, for the keyboard or the pad in hand, on the owner's own keyboard layout) and in [gameplay.md](gameplay.md). The essentials (US key positions):

   | Action | Keyboard | Gamepad |
   |---|---|---|
   | Move | A / D or arrows | Stick or d-pad |
   | Jump (hold for height) | Space or Z | A |
   | Dash | Shift or C | RB or RT |
   | Attack | J or X | X |
   | Harpoon (once found) | K or V | B or LT |
   | In water, with Ballast: sink / rise | Down / Up | Stick down / up |
   | Rest at a shrine | E | Y |
   | Map | M or Tab | LB |
   | Respawn | R | Back |
   | Screenshot | F12 | Share |
   | Start/stop video | F10 | — |

3. Each hand-over gives **one command that reaches that session's new content with the abilities it needs**, and what to try there. A whole run: `godot --path . -- --new` (a new game from the Narthex). `-- --room <Name>` starts in `scenes/rooms/<Name>.tscn` and never reads or writes the save. It starts with the abilities a route there needs: the fewest a pilgrim can own arriving in that room, from the whole game's draft map (`MapSolver.Arrival`; a room's own relic is not counted, so it still lies there). The run prints them (`[start] Slag with MagGrip, Drill`). `-- --abilities all` (or `none`, or `MagGrip,Drill`) says otherwise, with or without `--room`. F1–F5 toggle Mag-grip, Harpoon, Drill, Ballast, Heat plating in debug builds.
4. To watch the machines: `godot --path . -- --room MachineArena`. Walk right from the start: three Votaries pray toward a shrine (whoever lingers in front of one is struck after a wind-up: run or dash past, or take the gallery over them), then a Gleaner picking at scrap (it flees), then a Limpet asleep on a metal block (land a jump near it to wake it). A Lamplighter floats near the start and follows you once it has seen you.
5. In debug builds, F6 cycles the room through the biome profiles (Flooded, Frozen, Volcanic, Core); `godot --path . -- --biome volcanic` starts with one.
6. The game resumes from the last shrine rested at; `godot --path . -- --new` starts over. In debug builds (`godot --path .` is one) the room's name and the pilgrim's position in it (meters, the room scene's own frame) sit in the top right corner, so the owner's remarks and F12 screenshots say where they were made, and the gameplay controls sit bottom left (keyboard or gamepad as last used, lit while held; F8 hides them); F7 adds the debug readout (movement state, velocity, floor contact, dash cooldown, abilities) and the debug keys.
7. To report something, press F12 (or record with F10) and say "look at the last screenshot". Files land in `captures/`.
8. Feedback is given in plain words ("floaty jump", "camera lags"). The agent translates it into tuning changes. The owner may also edit `resources/player_tuning.tres` and relaunch, which needs no rebuild.
