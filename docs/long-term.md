# Long-term objectives — "The Deep"

This file describes the long-term objectives for the game. Every architectural choice must keep them possible and cheap to grow. `AGENTS.md` carries a short summary of this file and a link to it.

Both objectives share one idea: the game should contain something a solo developer could not realistically build by hand, but a coding agent can. Not one complex system: **a very large number of simple, well-made pieces, plus automated proof that they all fit together.** The game itself stays simple to play.

Context: the game is a 2.5D metroidvania (Godot 4, C#) set on a planet of the *Nirvanary* universe, where machines believe a machine paradise lies at the planet's core. The player is a machine pilgrim descending toward it. Base moveset: run, jump, dash. Unlockable abilities: Mag-grip, Harpoon, Drill, Ballast, Heat plating.

---

## Objective 1 — No two machines alike

### Goal

The Deep is inhabited by **150–200 distinct machine species** by release (a solo-made game typically has 15–30). Each species is a small pilgrim with its own visible behavior and its own version of the faith. The deeper the layer, the older and stranger the machines.

### What counts as a distinct species

A species is distinct only if **a player watching it for ten seconds can tell its behavior apart** from every other species. A new color, size or mesh on an existing behavior is a *variant*, not a species. Variants are welcome but do not count toward the goal. This rule exists to prevent fake variety.

Examples of the kind of behaviors intended:

- Prays in place and only attacks if the player passes in front of it.
- Hoards scrap and flees with it when approached.
- Follows the player down at a distance and never attacks.
- Sleeps until the floor shakes (a dash landing nearby), then charges.
- Repairs other machines the player has damaged.
- Migrates downward in groups, ignoring the player unless blocked.

Roughly a third of species should be non-hostile. The world should feel inhabited, not just populated with obstacles.

### Architecture to put in place early

- **Species are data plus one behavior.** A `MachineSpecies` resource (`[GlobalClass]`, `.tres`) holds name, layer, parts, stats, palette and a reference to its behavior. Behavior is a C# class.
- **A shared behavior vocabulary.** Small reusable building blocks: senses (sees player, hears impact, detects nearby machines, detects depth), movement patterns (patrol, hover, wall-crawl on metal, swim, burrow), and actions (strike, flee, carry, repair, pray). Most species combine these blocks. A species may still have its own short custom script when its behavior needs it. That is allowed and expected.
- **Machines are assembled from modular parts, in code.** Part categories: chassis, locomotion (legs, treads, fins, rotors), sensors (eyes, antennas), tools (claws, drills, censers) and ornaments (religious plating, prayer wheels, cables). A species is a choice of parts, plus scale, proportions and palette. This keeps the art cost to a library of parts, not one model per species. **This part library is a prototype brick,** so building it early is on the critical path; the species themselves are grown in polish.
- **Palette by depth.** Species colors follow the game's depth palette: teal and rust near the surface, red and ember near the core.
- **Bestiary.** Each species has an entry: name, layer, a placeholder "creed" line, and the behaviors the player has observed. Entries fill in as the player watches the machine, not just by killing it.

### Automated checks (added to `tools/check.sh` once machines exist)

- Every `MachineSpecies` resource loads and references a valid behavior and valid parts.
- A test arena spawns each species headless for a few hundred frames, with and without the player nearby, and fails on any error or exception.
- A report lists the species count per layer and flags species whose behavior and parts both match another species, as likely duplicates.

### How to grow it

Start with about 10 species covering clearly different behaviors, and get them right and fun. Then grow by layer. Quality and distinctness come before count. 60 truly distinct species beat 200 lookalikes.

---

## Objective 2 — Any order, proven on every build

### Goal

The player can collect the five unlockable abilities in many different orders, and the game **never softlocks**, whatever the order. This is checked automatically on every build by a solver, not by hand-testing routes. Sequence breaks become part of the design instead of bugs.

### What exactly is guaranteed

1. **No softlock, ever (hard requirement).** From every state the player can reach (room plus set of abilities owned), the ending must still be reachable. One-way drops, collapsing paths and locked doors are all taken into account.
2. **Order freedom (measured, with a target).** The solver counts how many of the 120 possible ability orders are actually achievable and reports it. The default target is **at least 24 orders**, with every ability obtainable in at least two different positions in the sequence.

Note on full freedom: making all 120 orders possible means every ability must be reachable from the start with the base moveset alone. Gates would then only lock optional areas and the final descent. That's a valid design, but a less classic one. The target above can be raised to 120 later if the map is designed for it.

### Architecture to put in place early

- **Rooms are separate scenes; the map is a graph.** Each connection between rooms (and each meaningful route inside a room) is a **marker node placed in the room scene**. It carries its requirement as an expression over abilities (for example `MagGrip | Harpoon`, or `Drill & Ballast`) and a one-way flag. Requirements live next to the geometry they describe, never in a separate hand-maintained list.
- **An extractor** reads all room scenes and builds the map graph from the markers.
- **The solver is pure C# logic** in the core (`core/`, no Godot dependency), tested by the xUnit project, so it runs fast inside `dotnet test`. It explores every reachable state, then checks guarantee 1 and computes guarantee 2.
- **Geometry sanity check.** Base movement limits (maximum jump height, jump plus dash distance) come from `player_tuning.tres`. A check measures the gaps and heights at each marked connection, and fails if a connection marked "base moveset" is physically beyond those limits, or if an unmarked gap is crossable. This catches requirements that have drifted from the real level geometry.
- **Debug view:** a command that prints the map graph and the achievable orders, so the designer can see the effect of a change immediately.

### Automated checks (added to `tools/check.sh` once there is a map)

- The solver runs on every build. A softlock fails the build and prints the exact state and path that leads to it.
- Rooms are not trusted to be one place: the inside check walks each room from each way in with the abilities owned (the geometry planner simulates the moveset and all five abilities), so a state is (room, way in, abilities), and only the exits and relics reached from there count.
- The order-freedom count is printed. Falling below the target fails the build.
- The geometry sanity check fails the build on any mismatch.

---

## Objective 3 — Any world from the bricks

Added with the owner's direction of 2026-11-01 ([roadmap.md](roadmap.md), [principles.md](principles.md)).

### Goal

The game is built twice: a **prototype** (40 rooms, our playground) and then the **real game**, much bigger, from a network of rooms planned on paper. The second build must be assembly, not rewriting: every piece the prototype uses (stage looks, materials, backdrops, water, blocks, gates, mechanisms, machines, items, dialogue) is a reusable **brick**, and a new world is content alone. The owner's test: *"if we make a sequel, we make a new map, and all the tools are at my disposal: it becomes a puzzle and we implement it."*

### What exactly is guaranteed

1. **No world in the code.** No room name, species id, layer or file path of one world appears outside its content and its own checks. A world is data: its rooms, its network and room table, its biomes and layers, its line book, its start.
2. **The sequel test.** A small second world, made of content only, loads, plays and passes the same checks as the prototype. It stays in the check as a fixture.
3. **The plan is proven like the prototype.** The real game's network and room table are read by the code and checked on every build (the solver's guarantees, the table agreeing with the network) before and while its rooms are built.

### Architecture to put in place

- The core (rules, state, solver, brains, geometry) in its own Godot-free project; Godot nodes as thin adapters ([principles.md](principles.md)).
- A world definition that names a world's content; the prototype moved under it.
- Items (keys, codes) in the requirement language of exits, so the solver sees every gate.
- The brick catalog ([bricks.md](bricks.md)) kept current: what each brick is and whether it is ready.

---

## Where they stand (2026-10-31)

- **Objective 2 is built:** the extractor, the solver, the geometry check and the draft map run in `tools/check.sh`, and every room is held to them as it is built.
- **Objective 1 has its foundation:** the part library, species as data, the behavior vocabulary v0 and the machine checks (M5). Species are grown in stage D (polish) of [roadmap.md](roadmap.md); until then, the existing species are placeholders.
- **Objective 3 is the prototype's goal** (stage A of [roadmap.md](roadmap.md)): its last item is the sequel test.
- Both depend on keeping enemies and rooms data-driven, pure logic free of Godot types where practical (so it can be unit-tested), and all movement limits in `player_tuning.tres`.
