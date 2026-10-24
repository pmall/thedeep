# Long-term objectives — "The Deep"

This file describes two long-term objectives for the game. They are **not** built in the first steps; the current focus is the graphical side and base movement. But every architectural choice made from the start must keep them possible and cheap to add later. When writing `AGENTS.md`, include a short summary of this file and a link to it.

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
- **Machines are assembled from modular parts, in code.** Part categories: chassis, locomotion (legs, treads, fins, rotors), sensors (eyes, antennas), tools (claws, drills, censers) and ornaments (religious plating, prayer wheels, cables). A species is a choice of parts, plus scale, proportions and palette. This keeps the art cost to a library of parts, not one model per species. **This part library belongs to the graphical phase,** so building it early is on the critical path.
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
- **The solver is pure C# logic** in a separate test project (xUnit), with no Godot dependency, so it runs fast inside `dotnet test`. It explores every reachable state, then checks guarantee 1 and computes guarantee 2.
- **Geometry sanity check.** Base movement limits (maximum jump height, jump plus dash distance) come from `player_tuning.tres`. A check measures the gaps and heights at each marked connection, and fails if a connection marked "base moveset" is physically beyond those limits, or if an unmarked gap is crossable. This catches requirements that have drifted from the real level geometry.
- **Debug view:** a command that prints the map graph and the achievable orders, so the designer can see the effect of a change immediately.

### Automated checks (added to `tools/check.sh` once there is a map)

- The solver runs on every build. A softlock fails the build and prints the exact state and path that leads to it.
- The order-freedom count is printed. Falling below the target fails the build.
- The geometry sanity check fails the build on any mismatch.

---

## What this means right now

- Do not build either system during the initial steps.
- Do: keep enemies and rooms data-driven, keep pure logic free of Godot types where practical (so it can be unit-tested), and keep all movement limits in `player_tuning.tres`. Both objectives depend on these.
- During the graphical phase, prototype the modular machine-part look early (Objective 1). It's both an art task and the foundation of the bestiary.
