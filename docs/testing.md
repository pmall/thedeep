# Testing

## The check: `tools/check.sh`

Run it after every change and before every commit. It stops at the first failure:

1. `dotnet build` (nullable on, warnings are errors).
2. `godot --headless --import`, failing on any error line.
3. Smoke test:
   - (a) The main scene boots headless for 300 frames with no errors.
   - (b) A scripted run (`-- --smoke`, `scripts/debug/SmokeDriver.cs`) presses run, jump and dash through the real input actions. It must print `SMOKE OK`: moved ≥ 10 m, full jump ≥ 2.9 m, every base state entered, never left Z = 0.
4. xUnit tests in `tests/TheDeep.Tests`.

The unit tests include `VideoEncoderTests`, which run the real `ffmpeg` (a required tool, see [toolchain.md](toolchain.md)).

Logs go to `.godot/check/`. Never report work as done while the check fails.

`godot --path .` runs the last *built* assembly; it does not compile. The check builds, so after a passing check the owner's launch command is up to date.

Other commands:

| What | Command |
|---|---|
| Build only | `dotnet build TheDeep.sln` |
| Unit tests only | `dotnet test tests/TheDeep.Tests/TheDeep.Tests.csproj` |
| Run the game | `godot --path .` |

## Seeing the game (visual verification)

Headless runs render nothing. To get real frames, use Movie Maker mode (it opens a window briefly):

```
godot --path . --disable-vsync --fixed-fps 60 --quit-after 240 --write-movie /path/to/out/f.png -- --smoke
```

- `--disable-vsync` matters. With vsync on, the compositor throttles a background window to about 1 fps and the run looks hung.
- Write into a fresh directory each time rather than deleting old frames.
- Look at the frames (and compare before and after) for any visual change. Roadmap M0 turns this into `tools/capture.sh` with named photo spots and frame-time measurement.

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
   | Respawn | R | Back |
   | Screenshot | F12 | Share |
   | Start/stop video | F10 | — |

3. The top-left readout shows the movement state, velocity, floor contact and dash cooldown.
4. To report something, press F12 (or record with F10) and say "look at the last screenshot". Files land in `captures/`.
5. Feedback is given in plain words ("floaty jump", "camera lags"). The agent translates it into tuning changes. The owner may also edit `resources/player_tuning.tres` and relaunch, which needs no rebuild.
