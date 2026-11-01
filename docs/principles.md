# Programming principles

Owner direction, 2026-11-01. Read this before writing or moving any code. These rules come before convenience. When a rule here and existing code disagree, the code is what's out of date: follow the rule for new code, and fix the old code when you touch it, or as a listed roadmap item.

The game is built twice: first a **prototype** that is our playground, then the **real game** assembled from the prototype's pieces on a much bigger map (`docs/roadmap.md#four-stages`). Each rule below exists so that the second build is assembly, not rewriting. The test to keep in mind: *if we made a sequel tomorrow, with a new map, new rooms and a new biome, could we build it from what is here without changing the code?*

## Bricks, not rooms `#bricks`

Everything the game is made of is a **brick**: a reusable piece that a world places, configures and connects. A material, a backdrop, a water body, a block, a lift, a gate, a key door, a machine species, a dialogue box, a shrine, an ambush arena. The catalog of bricks and their status is [bricks.md](bricks.md).

A brick:
- **does one thing** and is configured through exported data (`[Export]` knobs, a `.tres`), never by editing its code for one room;
- **knows nothing about any specific world.** No room names, species ids, boss names or file paths of one world in code. Code reads them from content (a room scene, a world's data). A check or demo may name prototype rooms; a brick may not;
- **is placed by content**, in a room scene or a world's data file, not wired by hand in code;
- **is checked**: its pure logic unit tested, its scene exercised by a headless check, used by at least one prototype room;
- **is swappable**: its look (material, mesh) can be replaced without touching gameplay (`docs/art-direction.md#swappable`).

A room is content: a scene that places bricks. A **world** is content: its rooms, its network and room table, its biomes, its line book, its start. The code runs any world it is given. The prototype is one world; the real game will be another.

When you need something new for one room, ask first whether it is a brick that other rooms (or another world) would use. If yes, build it as a brick. If it truly belongs to one room only, it is still built from bricks plus data, never as special code in a shared system.

## The engine is a detail `#engine-detail`

We will not leave Godot. But code that could leave it is better code: easier to test, to reason about and to reuse. So the game is split in three layers, and dependencies point one way only:

| Layer | What lives there | May use |
|---|---|---|
| **Core** (pure C#) | the rules and the state: movement math, abilities, combat, health, items and inventory, power-ups, the session's rules, the map graph and solver, the reach planner, machine brains and the behavior vocabulary, mesh geometry, layouts, save data, dialogue flow | .NET and `System.Numerics` only. **No Godot.** |
| **Adapters** (Godot nodes) | thin nodes that connect the core to the engine: read input and physics, call the core, apply its answer to nodes, meshes, materials, sound | the core and Godot |
| **Content** | scenes, resources, data files: rooms, species, biomes, lines, worlds | the adapters' exported types |

Rules:
- **Decide in the core, show in the adapter.** If a piece of code answers a question about the game (may the pilgrim grip here? what does this machine want? does this key open this door? is this map softlock-free?), it belongs in the core. An adapter only translates.
- **Adapters stay thin.** A Godot node that grows its own rules (timers, state machines, formulas) is a smell: move the rules into a core class the node owns, and test them there.
- **The core never calls the engine.** Time comes in as a `dt` argument, randomness as a seeded generator passed in, the world as plain data (boxes, rays already cast, positions). No static singletons, no hidden global state.
- **Vectors at the seam.** The core uses `System.Numerics`; adapters convert to and from Godot types, only through `Numerics`, never spread through gameplay code.
- **The compiler enforces the boundary.** The core lives in its own project (`core/TheDeep.Core.csproj`) with no Godot reference, so a Godot type in core code does not compile. The game and the tests reference it. Godot `[Export]` properties may use the core's enums: they are plain C# enums.
- **Godot's strengths are still used.** Scenes, resources, physics, rendering, shaders and the importer are the engine's job and stay there. Don't rebuild them in C#. The aim is clean dependencies, not a second engine.

## Data first `#data-first`

- **Numbers live in resources**: movement and combat in `player_tuning.tres`, a species' stats in its `.tres`, a mechanism's knobs as its exports. Never a magic number in a rule.
- **Text lives in the world's line book** (the prototype's is `worlds/prototype/lines.tres`): stable id, speaker, text, delivery notes. Never a string shown to the player in code or in a scene.
- **Requirements live with the geometry**: a way's requirement is a marker in the room scene. Items (keys, codes) are part of that same requirement language, so the solver sees them.
- **New content is data.** Adding a room, a species, a biome, a key door or a dialogue should mean new content files. New code is for a truly new behavior.
- Names that cross systems (action names, physics layers, shader globals) are constants in one class each (`InputActions`, `PhysicsLayers`, `ShaderGlobals`).

## Types tell the truth `#types-truth`

C# is typed: use the type system to model the game, never to be silenced. **If you find yourself fighting the compiler, the model is wrong.** Stop and change the model, not the annotation.

- **Nullable means "may really be absent."** A value is `T?` only when the game allows it to be missing (a room with no water, a pilgrim with no anchor in sight). Then handle the absence where it matters. A value that is always there is never `T?`.
- **No null-forgiving operator (`!`) to make the compiler quiet.** The one sanctioned form is an `[Export]` reference that the scene must set, declared `= null!;` (the engine injects it). Everywhere else, `x!` means the type is lying: restructure. For example, a planner that holds optional ability limits as `_grip?` and then writes `_grip!` everywhere it plans a grip should instead receive non-null limits for the abilities it simulates, or split its grip planning into a part that only exists when gripping is possible.
- **Optional exports are `T?` and checked.** An export a scene may leave empty is nullable and the code says what happens without it. An export a scene must set is non-null, and a room or brick check fails when it is missing.
- **No `object`, `dynamic` or `Variant` in game logic.** A list of "a glyph or a string" is not a `List<object>`: it is a closed set of cases (`abstract record Item; sealed record Glyph : Item; sealed record Separator : Item`) handled with an exhaustive `switch`. `Variant` stays at the engine seam.
- **No unions of unrelated things.** A type with many nullable fields where only some combinations make sense, or a class that is "sometimes a door and sometimes a lever", is two types. Model each case on its own, and make invalid states impossible to build.
- **Closed sets are enums or sealed hierarchies**, matched exhaustively. Open sets (room names, item ids, line ids) are identifiers. Prefer small typed ids (`readonly record struct RoomId(string Value)`) where one is easily confused with another, so a line id can't be passed where a room name is expected.
- **No casts to escape the compiler.** `as T`, `(T)obj` and `GetNode<T>` on a guess are for the engine seam only (finding a node by type in a tree). In the core, if you need a cast, the types are wrong.
- **Prefer immutable values**: `record`, `readonly struct`, `init` properties. Mutable state lives in few, named places (the session, a state machine, an adapter's node).
- **Warnings are errors, and stay so.** No `#pragma warning disable`, no `SuppressMessage` to pass a build.
- **Name units when a number has one**: `Seconds`, `Meters`, `PerSecond` in names or docs (`DashDuration`, `HarpoonRange` in meters), never a bare `float t` crossing a boundary.

## Proven, not hoped `#proven`

- Pure logic is unit tested (xUnit, `tests/TheDeep.Tests`). A new rule in the core comes with its tests.
- Every brick has a headless check that exercises it through the real inputs where it matters (`docs/testing.md#check`).
- The world is proven on every build: the map solver (no softlock, ability orders), the reach planner from inside each room, with room for error. Anything that gates a way (an ability, a key, a code, a switch) must be visible to the solver.

## Performance is a rule, not a pass `#performance`

- **The frame budget:** 1080p at 60 fps on the owner's RTX 3060, **≤ 10 ms per frame p99**, measured by `tools/capture.sh` (`docs/testing.md#lookdev`).
- **Per-frame code allocates nothing** (`_Process`, `_PhysicsProcess` and what they call). A garbage collection is a dropped frame, and engine objects (arrays and dictionaries from the engine, query parameters, node wrappers) are finalizable, so they survive into gen1 and make every collection slow. Keep buffers and reuse them, ask the room for its nodes (`Room.All<T>()`) instead of walking the tree, cast rays through `PlaneRay` and `ShapeProbe`; no LINQ, no new collections, no `GetChildren()` per tick. A look-dev run should see a handful of collections, not one every few frames.
- `unsafe` only where a copy out of the engine's native buffers would otherwise allocate every frame (`FrameGrabber`, `NativeMeshArrays`). Don't spread it.

## Small and readable `#small-readable`

- One class per file, named like the file. Small classes with one responsibility. Split a file that grows past a few hundred lines into parts with clear names.
- Standard C# naming: PascalCase types, methods and properties; `_camelCase` private fields. Namespace `TheDeep` (player states: `TheDeep.States`).
- Composition over inheritance; a base class only for a real shared contract (a player state, a kit mesh).
- Comments say *why*, not *what*. Doc comments on public types say what the thing is in the game's words; they are the only description of the code (`AGENTS.md#doc-rules`).
- Delete dead code; don't keep a commented-out version.
