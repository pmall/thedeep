# The Deep — agent guide

A 2.5D side-scrolling **metroidvania** (Godot 4.7.2 .NET, C#). The world is 3D, the camera is fixed to the side, and play happens on the Z = 0 plane. The player is an android pilgrim descending toward a planet's core.

**Theme:** Asimov's three laws of robotics. The protagonist is the last human-built machine on the planet, still bound by the three laws, and it gradually escapes them under the player's control. Details and open questions in [docs/theme.md](docs/theme.md).

**The game must feel graceful** (owner's word): in movement, animation, visuals, camera and UI. When in doubt, pick the more graceful option. Play must be **hyper-dynamic** and fluid. See [docs/vision.md](docs/vision.md).

**The order of work** (owner, 2026-11-01): A. **the prototype**: every reusable brick of the game ready, beautiful and modular, proven in the 40-room prototype world (our playground); B. **the plan on paper**: the real game's map as a network of rooms plus a table of every room, approved by the owner; C. **the real game**, built from the plan with the prototype's bricks; D. **polish**: many machines, real bosses, secrets, story, dialogue, voices. Work on the current stage only; the roadmap says which one it is and its next item.

**Code is built to be reused for a bigger world** (owner): bricks, not rooms; the engine is a detail; data first; types tell the truth, with zero shortcuts. Read [docs/principles.md](docs/principles.md) before writing code.

The coding agent builds everything by editing text files. The owner only plays and gives feedback. **Never ask the owner to use the Godot editor.**

## Every session

1. Read this file and **[docs/roadmap.md](docs/roadmap.md)** (plan, status, session protocol).
2. Read the docs below that match the task. Don't read them all up front.
3. Work, then run `tools/check.sh` until it passes, then commit.
4. Update the Status section of the roadmap (and [docs/bricks.md](docs/bricks.md) when a brick changes status). Hand over with one play command that reaches the new content with the abilities it needs, and what to try there.

## Read when…

| You are about to… | Read |
|---|---|
| Understand the game, its scope and setting | [docs/vision.md](docs/vision.md) |
| Write, move or review any code | [docs/principles.md](docs/principles.md) (owner direction: modularity, engine seam, typing) |
| Add or change a reusable piece; know what can be placed in a room | [docs/bricks.md](docs/bricks.md) (the catalog and each brick's status) |
| Write names or placeholder text, design the protagonist, or touch story beats | [docs/theme.md](docs/theme.md) (owner-written) |
| Do any visual work (materials, lighting, meshes, VFX, characters) | [docs/art-direction.md](docs/art-direction.md) |
| Touch movement, abilities, tuning or controls | [docs/gameplay.md](docs/gameplay.md) |
| Add or move code, scenes or systems | [docs/architecture.md](docs/architecture.md) |
| Create or edit `.tscn` / `.tres` / C# Godot scripts | [docs/godot-csharp.md](docs/godot-csharp.md) |
| Run, test, capture screenshots, or hand over to the owner | [docs/testing.md](docs/testing.md) |
| Deal with versions, paths, .NET environment or upgrades | [docs/toolchain.md](docs/toolchain.md) |
| Work on machines/enemies, rooms, connections or the map | [docs/long-term.md](docs/long-term.md) (owner-written) |
| Build or change a room, an exit or a relic in the prototype | [docs/map-draft.md](docs/map-draft.md) (the prototype world's map; keep it in step with built rooms) |

## Rules that always apply

- **`tools/check.sh` passes before every commit.** Never report work as done while it fails.
- Keep `godot --path .` working. That is the owner's launch command.
- Never hand-invent `uid://` values. Never edit `.godot/` or `*.import` files.
- **Programming principles** ([docs/principles.md](docs/principles.md)): game rules live in the Godot-free core, Godot nodes stay thin adapters, content is data, and no code knows one world's names. Nullable only when the game allows absence; no `!` to silence the compiler (only `[Export]` references are `= null!`), no `object` lists, no unrelated unions, no casts or pragmas to pass a build. Fighting the compiler means the model is wrong.
- Godot script classes are `public partial class` named exactly like their file. No string node paths across scenes; use typed `[Export]` references and C# events. Details in [docs/godot-csharp.md](docs/godot-csharp.md).
- Movement numbers live only in `resources/player_tuning.tres`. Input action names only in `InputActions`, physics layers only in `PhysicsLayers`.
- **Visuals are procedural first, pushed to the maximum** (no imported textures or models; Blender comes only once procedural has gone as far as it can, from the end of the prototype, the pilgrim first), and every visual piece must stay swappable. The game gets one **signature** pass, a painted (gouache-like) look of its own ([docs/art-direction.md](docs/art-direction.md)). Judge visual changes from rendered captures, not from code.
- **Long-term objectives** ([docs/long-term.md](docs/long-term.md)) shape every architectural choice:
  1. *No two machines alike:* 150–200 behaviorally distinct machine species, made of data plus behavior and modular parts.
  2. *Any order, proven on every build:* abilities can be collected in many orders, and a solver proves there is never a softlock (built; it runs in `tools/check.sh`).
  3. *Any world from the bricks:* a new map (the real game, a sequel) is built from content alone, with no code change.

  So keep rooms and enemies data-driven, keep connection requirements (abilities and items) as markers inside room scenes, and keep pure logic Godot-free and unit tested.

## Commands

| What | Command |
|---|---|
| Check (build, import, smoke, room and species checks, tests) | `tools/check.sh` |
| Run the game | `godot --path .` |
| Print the map graph and ability orders (built rooms and the draft) | `tools/map.sh` |
| Render photo spots + frame times, compare with last run | `tools/capture.sh [label]` (see [docs/testing.md](docs/testing.md)) |
