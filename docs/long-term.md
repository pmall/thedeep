# Long-term objectives — "The Deep"

Two objectives every architectural choice keeps possible and cheap to grow. They share one idea: the game should hold something a solo developer could not realistically build by hand, but a coding agent can. Not one complex system: **a very large number of simple, well-made pieces, plus automated proof that they all fit together.** The game itself stays simple to play. The third, a new world from the bricks alone, is `docs/principles.md#bricks`.

______________________________________________________________________

## No two machines alike <a id="machines-distinct"></a>

The Deep is inhabited by **150–200 distinct machine species** by release (a solo-made game typically has 15–30). Each species is a small pilgrim with its own visible behavior and its own version of the faith. The deeper the layer, the older and stranger the machines. They are grown in polish (`docs/roadmap.md#stage-polish`); until then the species are placeholders.

**What counts as a distinct species** <a id="distinct-species"></a>: a player watching it for ten seconds can tell its behavior apart from every other species. A new color, size or mesh on an existing behavior is a *variant*: welcome, but it doesn't count. This rule prevents fake variety.

The kind of behaviors intended:

- Prays in place and only attacks if the player passes in front of it.
- Hoards scrap and flees with it when approached.
- Follows the player down at a distance and never attacks.
- Sleeps until the floor shakes (a dash landing nearby), then charges.
- Repairs other machines the player has damaged.
- Migrates downward in groups, ignoring the player unless blocked.

Roughly a third of species are non-hostile: the world feels inhabited, not just populated with obstacles.

How it is built:

- **A species is data plus one behavior**: a `MachineSpecies` resource names its layer, parts, stats, palette and behavior. Most behaviors combine blocks of a shared vocabulary (senses, movement patterns, actions); a species may have its own short script when its behavior needs it.
- **Machines are assembled from shared parts** (`docs/art-direction.md#shared-parts`), so the art cost is a library of parts, not one model per species. Their colors follow `docs/art-direction.md#machine-palette`.
- **A bestiary**: each species has an entry (name, layer, a creed line, the behaviors the player has observed) that fills in as the player watches the machine, not just by breaking it.
- **The checks** load every species, run each alone in an arena, count species per layer and flag likely duplicates (same behavior, same body).
- **How to grow**: start with about 10 clearly different behaviors and get them right and fun, then grow layer by layer. 60 truly distinct species beat 200 lookalikes.

______________________________________________________________________

## Any order, proven on every build <a id="any-order"></a>

The player can collect the five unlockable abilities in many different orders, and the game **never softlocks**, whatever the order. A solver checks it on every build, not hand-testing. Sequence breaks are part of the design, not bugs.

- **No softlock, ever** <a id="no-softlock"></a>. From every state the player can reach (room, way in, abilities owned), the ending is still reachable. One-way drops, collapsing paths and locked doors all count.
- **Order freedom, measured** <a id="order-freedom"></a>. The solver counts how many of the 120 ability orders are achievable. The target is **at least 24**, with every ability obtainable in at least two positions of the order. Full freedom (120) would mean every ability is reachable from the start with the base moveset, gates locking only optional areas and the final descent: valid, but less classic.

How it is held:

- **Requirements live with the geometry**: each way between rooms (and each route inside a room that matters) is a marker in the room scene carrying its requirement over abilities and items (`MagGrip | Harpoon`, `Drill & Ballast`) and whether it is one way. The map graph is read from the markers.
- **The solver is pure core code**, unit tested. A softlock fails the build with the state and path that lead to it; the order count is printed, and falling under the target fails the build.
- **The geometry check** walks each room from each way in with the moveset and the abilities owned (movement limits from `player_tuning.tres`, and a clumsier pilgrim for room for error), so a marker that has drifted from the real geometry fails the build.
