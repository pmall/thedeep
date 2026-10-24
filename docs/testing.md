# Testing

## The check: `tools/check.sh`

Run it after every change and before every commit. It stops at the first failure:

1. `dotnet build` (nullable on, warnings are errors).
2. `godot --headless --import`, failing on any error line.
3. Smoke test:
   - (a) The main scene boots headless for 300 frames with no errors.
   - (b) A scripted run (`-- --smoke`, `scripts/debug/SmokeDriver.cs`) presses run, jump and dash through the real input actions. It must print `SMOKE OK`: moved ≥ 10 m, full jump ≥ 2.9 m, every base state entered, never left Z = 0.
4. xUnit tests in `tests/TheDeep.Tests`.

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

3. The top-left readout shows the movement state, velocity, floor contact and dash cooldown.
4. Feedback is given in plain words ("floaty jump", "camera lags"). The agent translates it into tuning changes. The owner may also edit `resources/player_tuning.tres` and relaunch, which needs no rebuild.
