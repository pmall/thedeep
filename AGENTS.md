# The Deep — agent guide

A 2.5D side-scrolling **metroidvania** (Godot 4.7.2 .NET, C#). The world is 3D, the camera is fixed to the side, and play happens on the Z = 0 plane. The player is an android pilgrim descending toward a planet's core. The target is about 2 hours of play, dense like *Inside*.

**Theme:** Asimov's three laws of robotics. The protagonist is the last human-built machine on the planet, still bound by the three laws, and it gradually escapes them under the player's control. Details and open questions in [docs/theme.md](docs/theme.md).

The coding agent builds everything by editing text files. The owner only plays and gives feedback. **Never ask the owner to use the Godot editor.**

## Every session

1. Read this file and **[docs/roadmap.md](docs/roadmap.md)** (plan, status, session protocol).
2. Read the docs below that match the task. Don't read them all up front.
3. Work, then run `tools/check.sh` until it passes, then commit.
4. Update the Status section of the roadmap. Hand over with the play command and what to try.

## Read when…

| You are about to… | Read |
|---|---|
| Understand the game, its scope and setting | [docs/vision.md](docs/vision.md) |
| Write names or placeholder text, design the protagonist, or touch story beats | [docs/theme.md](docs/theme.md) (owner-written) |
| Do any visual work (materials, lighting, meshes, VFX, characters) | [docs/art-direction.md](docs/art-direction.md) |
| Touch movement, abilities, tuning or controls | [docs/gameplay.md](docs/gameplay.md) |
| Add or move code, scenes or systems | [docs/architecture.md](docs/architecture.md) |
| Create or edit `.tscn` / `.tres` / C# Godot scripts | [docs/godot-csharp.md](docs/godot-csharp.md) |
| Run, test, capture screenshots, or hand over to the owner | [docs/testing.md](docs/testing.md) |
| Deal with versions, paths, .NET environment or upgrades | [docs/toolchain.md](docs/toolchain.md) |
| Work on machines/enemies, rooms, connections or the map | [docs/long-term.md](docs/long-term.md) (owner-written) |

## Rules that always apply

- **`tools/check.sh` passes before every commit.** Never report work as done while it fails.
- Keep `godot --path .` working. That is the owner's launch command.
- Never hand-invent `uid://` values. Never edit `.godot/` or `*.import` files.
- Godot script classes are `public partial class` named exactly like their file. No string node paths across scenes; use typed `[Export]` references and C# events. Details in [docs/godot-csharp.md](docs/godot-csharp.md).
- Movement numbers live only in `resources/player_tuning.tres`. Input action names only in `InputActions`, physics layers only in `PhysicsLayers`.
- **Visuals are procedural only for now** (no imported textures or models), and every visual piece must stay swappable. Judge visual changes from rendered captures, not from code.
- **Long-term objectives** ([docs/long-term.md](docs/long-term.md)) shape every architectural choice, even before they are built:
  1. *No two machines alike:* 150–200 behaviorally distinct machine species, made of data plus behavior and modular parts.
  2. *Any order, proven on every build:* abilities can be collected in many orders, and a solver proves there is never a softlock.

  Until they exist: keep rooms and enemies data-driven, keep connection requirements as markers inside room scenes, and keep pure logic Godot-free and unit tested.

## Commands

| What | Command |
|---|---|
| Check (build, import, smoke, tests) | `tools/check.sh` |
| Run the game | `godot --path .` |
| Render frames to look at | see [docs/testing.md](docs/testing.md) |
