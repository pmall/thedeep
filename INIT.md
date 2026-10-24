# Project init brief — 2.5D metroidvania (working title: "The Deep")

This file tells Claude Code how to set up the project from an empty folder. Read it fully, then do the **Init tasks** at the end. Afterwards, turn the stable parts of this brief into the project's `AGENTS.md`.

## 1. What the game is

A gameplay-first, side-scrolling **metroidvania** with a 2.5D look: the world is real 3D, the camera is fixed to the side, and play happens on a single 2D plane. Visual references: *Ori*, *Hollow Knight: Silksong*, *Castlevania: Belmont's Curse*.

Design priorities, in order:

1. **A finished, playable game.** No novel signature systems. Classic metroidvania structure done well.
2. **Ambience and art direction.**
3. **A small, defined set of abilities**, fixed before the map is designed, because the map is built around them.

### Setting

The game is set on a planet in the *Nirvanary* universe. Machines across many worlds seek "Nirvanary", a mythical machine paradise of infinite data and compute. On this planet the machines believe Nirvanary lies at the planet's **core**. The player is a machine pilgrim descending through flooded, frozen and then volcanic layers toward it. **The game is a descent:** deeper means older, stranger, hotter.

Narrative is out of scope for now. Use placeholder text only.

### Art direction (for when real assets arrive)

- Dark and heavy, not pastel.
- The palette tracks depth: cold teal and rust near the surface, shifting to deep red and ember orange near the core.
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals.
- Three depth layers: a blurred foreground, the play plane, and a hazy backdrop with parallax.
- Use placeholder geometry (boxes, capsules, flat colors) until the movement feels good.

## 2. Moveset

### Base moveset (available from the start)

- Run
- Jump, with variable height (release early for a short hop) and coyote time
- **Dash**, which works on the ground and in the air, with a short cooldown

### Unlockable abilities (these gate the map)

| Ability | What it does | What it gates |
|---|---|---|
| **Mag-grip** | Walk on **metal** walls and ceilings. Rock surfaces cannot be gripped. | Vertical shafts and ceilings made of metal |
| **Harpoon** | Fire at an anchor point and zip to it | Gaps with anchor points |
| **Drill** | Break cracked rock | Cracked rock walls and floors |
| **Ballast** | Toggle between sinking and floating in water | Flooded layers |
| **Heat plating** | Passive: survive heat-vent zones | Late-game areas near the core |

Every ability should be one simple state or rule. Avoid systemic or emergent mechanics.

**Mag-grip implementation note.** Rotating the player around corners is the hard part. Use raycasts toward the surface plus `up_direction` aligned to the surface normal, and make corner transitions snap rather than flow at first. Smooth them only once everything else works. Metal versus rock must be a data property (a physics layer or group), never inferred from visuals.

## 3. Tech stack

- **Godot 4, .NET edition** (the "Godot Engine - .NET" download from godotengine.org, not the standard build). Latest stable 4.x. The .NET binary is usually named like `Godot_v4.x-stable_mono_linux.x86_64`; find or ask for its path, and use it everywhere this brief says `godot` (a `godot` symlink or a `GODOT` variable in the scripts is fine). Record the exact version in `AGENTS.md`.
- **C#** on the .NET SDK version that this Godot release requires (check the Godot docs for that version; install it with the system package manager if `dotnet --version` is missing or too old).
- `.csproj` uses `Godot.NET.Sdk` at **exactly** the Godot version in use. Let Godot generate the project and solution (`godot --headless --build-solutions --quit`) rather than writing them by hand, then add:
  - `<Nullable>enable</Nullable>`
  - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- 3D scene with a locked side camera: an orthographic or narrow-FOV perspective `Camera3D` looking down −Z.
- The player is a `CharacterBody3D` locked to the Z = 0 plane (`AxisLockLinearZ`, plus clamping in code). All gameplay physics stays on X/Y.
- Renderer: Forward+.
- Git from day one.

## 4. Who does what

The coding agent builds the whole game by editing files. The owner does not use the Godot editor; they only play the game and give feedback. Never ask the owner to do something in the editor.

- `.tscn`, `.tres`, `.cs` and `project.godot` are text files. Edit them directly.
- **Do not hand-invent `uid://` values.** When creating a new scene or resource, either omit the uid or let headless Godot generate it (`godot --headless --editor --quit`).
- Never edit `.godot/` or `*.import` files. They are generated.
- Keep scenes small and composed, one responsibility each.
- **Godot C# rules the compiler won't always flag:**
  - Every script class is `public partial class`, and its name matches its file name exactly (`Player.cs` → `class Player`), or Godot can't attach it.
  - Custom resources (like the tuning resource) are `[GlobalClass] public partial class X : Resource` with `[Export]` properties.
  - After adding or renaming a class, rebuild before referencing it from a scene.
- **The check step: `tools/check.sh`**, which fails on the first problem:
  1. `dotnet build` with warnings as errors. This is the compile step.
  2. Import the project headlessly (`godot --headless --import`).
  3. Smoke test: launch the main scene headless for a few hundred frames (`--quit-after`), and fail if the output contains `ERROR` or an unhandled exception.
  4. Unit tests (gdUnit4 for C#, or plain xUnit for pure logic) once there is logic worth testing.
- Run `tools/check.sh` after every change and before every commit. Never report work as done while it fails.
- Avoid code the compiler can't see: no string node paths (`GetNode("A/B")`) across scenes. Use typed `[Export] public NodeType X { get; set; }` references, or `GetNode<T>("%UniqueName")` within a scene. Connect signals with C# events (`button.Pressed += OnPressed;`), not `Connect("name", ...)`. Use the generated `SignalName.X` / `PropertyName.X` constants instead of string literals.
- The owner launches the game with `godot --path .`. Keep that command working at all times.

## 5. Project structure

```
project.godot
<ProjectName>.csproj
<ProjectName>.sln
AGENTS.md
CLAUDE.md               # one line: @AGENTS.md
.gitignore              # ignores .godot/, bin/, obj/ and export builds
scenes/
  Main.tscn             # boots the test room
  player/Player.tscn
  rooms/TestRoom.tscn
  camera/SideCamera.tscn
scripts/
  player/Player.cs
  player/states/        # one class per movement state (Idle, Run, Jump, Fall, Dash, Grip...)
  abilities/AbilitySet.cs    # which abilities are unlocked (simple flags)
  camera/SideCamera.cs
  tuning/PlayerTuning.cs     # [GlobalClass] resource holding all movement numbers
resources/
  player_tuning.tres    # instance of PlayerTuning
assets/                 # placeholder meshes/materials now, real art later
tools/
  check.sh              # build + import + smoke test (+ tests later)
```

`.gitignore`: ignore `.godot/`, `bin/`, `obj/` and export folders. **Do commit** `*.import` files: Godot 4 expects them in version control.

## 6. Conventions

- Standard C# naming: PascalCase types, methods and properties; `_camelCase` private fields. One class per file.
- A small explicit state machine for the player, with one state per class. No giant `_PhysicsProcess`.
- All tuning values (speeds, jump height, dash distance and cooldown, coyote time, jump buffer) live in `player_tuning.tres` (a `PlayerTuning` resource), never hard-coded.
- Physics layers, defined once in `project.godot` and named: `world_rock`, `world_metal`, `player`, `enemy`, `hazard`, `water`, `trigger`. Mirror them as constants in one C# static class.
- Input actions, defined in `project.godot`: `move_left`, `move_right`, `move_up`, `move_down`, `jump`, `dash`, `attack`, `interact`. Support both keyboard and gamepad. Mirror the action names as constants in one C# static class; no string literals elsewhere.
- Abilities are unlocked through `AbilitySet` flags, with a debug key or cheat to toggle each one while testing.

## 7. Init tasks (do these now)

### Phase A — everything that needs the owner, done first

Do every step that could need the owner's input or approval **at the start, in one go**, so the owner can then walk away while Phase B runs unattended. After Phase A, do not ask the owner anything until the final report.

1. **Ask all questions at once**, in a single message, with a sensible default for each:
   - The project folder name and C# project name (default: `TheDeep`).
   - Whether to create a GitHub remote (default: no, local git only).
2. **Install the toolchain into the user's home directory, without sudo where possible:**
   - .NET SDK: if `dotnet --version` is missing or older than the version this Godot release requires, install it with Microsoft's `dotnet-install.sh` into `~/.dotnet` and add it to `PATH` in the shell profile.
   - Godot .NET edition: if missing, download the latest stable 4.x **.NET** Linux build from the official Godot download (godotengine.org or the official GitHub releases), unzip it into `~/.local/opt/godot/`, and symlink the binary to `~/.local/bin/godot`.
   - If anything truly needs sudo (for example a missing system library), run it now, while the owner is present to approve it.
3. **Verify the toolchain:** `godot --version` reports a `.mono`/.NET build, `dotnet --version` matches, and a throwaway headless `godot --headless --quit` runs cleanly.
4. **Trigger every permission prompt now:** run once each command Phase B will need (`git`, `dotnet build`, `godot --headless`, writing into the project folder), so any approvals happen while the owner is there.
5. Tell the owner: "Setup done, you can leave. I'll report when the first playable build is ready."

### Phase B — unattended build

6. Create the Godot 4 C# project with the structure above, set Forward+, define the input actions and physics layers, and generate the `.csproj`/`.sln`. Enable nullable and warnings-as-errors.
7. Initialize git with a correct `.gitignore`, and commit. Commit again after each step below that passes `tools/check.sh`.
8. Build `TestRoom.tscn`: a greybox room with flat floors, a few platforms, one tall shaft, and a mix of rock blocks and metal blocks (colored differently: rock grey-brown, metal blue-grey).
9. Build the player with the **base moveset only**: run, variable jump with coyote time and jump buffer, and dash. Use a capsule placeholder.
10. Build the side camera: smooth follow, slight look-ahead in the movement direction, and room-bounds clamping.
11. Add a background layer of out-of-focus boxes behind the play plane, so the 2.5D depth reads from the start.
12. Create `tools/check.sh` as described in section 4, run it, and fix everything until it passes.
13. Write `AGENTS.md` from sections 1–6 of this brief, plus the confirmed Godot and .NET versions, their install paths, and the commands to build, check and run. Also create a `CLAUDE.md` containing only `@AGENTS.md`, so Claude Code loads it whether or not it reads `AGENTS.md` natively.
14. If something blocks Phase B, make the most reasonable choice, note it, and keep going. Stop only if the game cannot be built at all.
15. **Final report:** what was built, the one command to launch it, any choices made without asking, and which tuning values in `player_tuning.tres` are worth adjusting after the first playtest.

**Not in this step:** unlockable abilities, enemies, combat, save system, and the map. Those come once base movement feels good.
