# Gameplay

What the player does and what the world asks of them: design intent and the rules rooms are built to. How each move works is in its state's docblock (`scripts/player/states/`); every number is in `resources/player_tuning.tres` (`docs/principles.md#data-first`). The feel is `docs/vision.md#graceful`.

## Base moveset `#moveset`

- **Run.**
- **Jump** with variable height (release early for a short hop), coyote time and a jump buffer.
- **Ledge assist** (owner, 2026-11-01: jumps felt too exact, against the hyper-dynamic goal): an airborne pilgrim pressing into a wall whose top is a little above its feet is lifted onto it, keeping its way and speed. The map's planner lifts the same way (`LedgeAssist`), so no gate is passed by it unproven.
- **Dash** on the ground and in the air (one air dash per airtime, refilled on landing). A ground dash can be cancelled into a jump.

## Room metrics `#room-metrics`

For building rooms. A full jump rises 3.2 m, so a step the pilgrim must climb is at most **2.5 m**, and a ladder of ledges rises 2.5 m per ledge (never 3). A gap crossed at one height is at most **7 m** (the exact pilgrim crosses about 10 m with a running jump and the air dash).

These leave room for error, because jumps must never be exact (owner, 2026-11-01): the room check walks every room again with a much clumsier pilgrim (`MoveLimits.Clumsy`) and fails on any way it misses. The ledge assist is extra forgiveness on top, never a reason for a higher step: with it a ledge up to about 3.6 m is reached, so a wall that gates an ability (Mag-grip's, the Harpoon's) stays well above that. A room that moves the tuning (a higher jump) moves these too.

## The pilgrimage `#pilgrimage`

- **Attack:** the fight moveset (`docs/gameplay.md#fight`). A machine hit flashes, reels and is pushed back; broken, it is gone until the room is entered again.
- **Health:** a few hits, shown top left as thin diamonds. A blow throws the pilgrim back and takes control away a moment, then nothing hurts for a short grace.
- **Heat** `#heat` (owner feedback, 2026-11-07: the volcanic layer's heat hurt erratically, and a pilgrim fallen into a slag channel bounced in small hops, out of control): without Heat plating, touching heat (a vent's column, the air over slag) burns, as spikes and acid do in Hollow Knight. One health (none in the grace), the pilgrim recoils, and under a quick veil it stands again on the last ground it stood on clear of heat, in control. So heat is never crossed without the plating, and nobody is ever stuck in it, thrown across it, or hurt by it faster than the grace allows. With the plating heat is harmless; slag itself kills, plated or not.
- **Death:** the pilgrim wakes at the last shrine it rested at, whole, the room renewed (its machines back).
- **Shrines:** resting heals and saves the game there.
- **Ability relics:** walking into one takes it: the ability unlocks, a banner names it and says how to use it, the game saves. A taken relic stays taken.
- **Saving:** one save per world. A normal launch resumes there; `-- --new` starts fresh. Checks, look-dev and `--room` runs never read or write it.
- **Map:** the visited rooms and the ways not taken yet; it pauses the game.
- **Leaving a room:** through its exits, however the pilgrim moves (`docs/gameplay.md#leaving-a-room`).
- **Scripture plates** and **lines spoken to the pilgrim** as it comes near: text only, lore.
- **Bosses** are placeholders (their richness is `docs/roadmap.md#stage-polish`): a boss is an ordinary machine in an arena whose gates fall while it stands; broken, it never stands again. Dying in the fight renews it. What each one does is its species and brain.
- **Law breaks** are story beats tied to depth, outside the ability graph (`docs/theme.md#laws-outside-abilities`): a law holds the pilgrim at its place until it presses on and breaks it, for good. Where they stand: `worlds/prototype/map-draft.md#draft-law-breaks`.
- **Lifts** only carry the pilgrim up; once on its way a lift can't be stopped.

## Leaving a room `#leaving-a-room`

Owner direction (2026-11-06): room transitions as in the classics (Hollow Knight, Super Metroid), and never a fall out of the world.

- **Each exit has a trigger covering its whole opening**, from the room's edge outward: a doorway's from floor to lintel, a hatch's from wall to wall. Touching it leaves the room, whatever the pilgrim is doing: walking, jumping, dashing, gripping, swinging. Standing in a doorway, or dropping into one from a flue above, is still being in the room; dipping into a hatch's hole is leaving.
- **Arriving**, the pilgrim appears clear of the matching exit's trigger with its speed kept, never moving back out: through a doorway standing on its marker, up through a floor with a hop that clears the hole (carried onto the floor beside it), down through a roof falling. Going straight back out goes back.
- **Each exit leads one way** (west, east, down, up), and the exit it leads to faces the other way. A doorway's marker stands on its floor, inside the room; a hatch's area is its hole. The room check holds every exit to its real opening: no room for a body over a doorway's trigger or beside a hatch's, and an arrival that fits.
- **A place meant to kill says so**: the Slag's river of slag is lethal heat. Nothing is a fall out of the world.
- **Falling out of a room** (below its camera bounds) is therefore a bug: the pilgrim is put back at the exit it came in by (or its shrine, if it woke there), and the log says so as an error.

## Fight `#fight`

A few moves, done well (`docs/roadmap.md#feel-fight`). No strike stops the pilgrim: momentum carries through. Presses during a recovery are kept.

- **The chain:** presses in rhythm give a swing, a second swing, then a **finisher** (wider, one more damage, it throws the machine far); a pause starts the chain over.
- **Down strike:** in the air, holding down: a strike under the feet. What it hits **bounces** the pilgrim up, the air dash refilled, so it can strike again.
- **Dash strike:** attacking during a dash lunges along it: longer, one more damage.
- **Hits that land:** a landed blow holds the pilgrim and its target still for a few hundredths of a second (the **hit stop**), with sparks and a camera shake that grow with the strike.
- **Power-ups** add to every blow's damage (strength) or to the pilgrim's health (defense), for good.

Each strike's reach, width, damage, knockback, hit stop and bounce are `Strikes` in `player_tuning.tres`. How the strikes look (sword moves, the chain choreographed) is `docs/art-direction.md#strikes`.

## Unlockable abilities `#abilities`

These gate the map. Every ability is one simple state or rule: no systemic or emergent mechanics. They can be collected in many orders, and a solver proves it (`docs/long-term.md#any-order`).

| Ability | What it does | What it gates |
| -- | -- | -- |
| **Mag-grip** `#mag-grip` | Walk on **metal** walls and ceilings; rock can't be gripped, and water lets go of it | Vertical shafts and ceilings made of metal |
| **Harpoon** `#harpoon` | Fire at a mooring ring in sight (hold up or down to aim) and zip to it; a ring in sight that the camera doesn't frame is shown by a chevron at the screen's edge (owner, 2026-11-02: targets off screen were invisible) | Gaps with rings |
| **Drill** `#drill` | A dash bores through **cracked rock**; down + dash dives through cracked floors. Broken rock stays broken | Cracked rock walls and floors |
| **Ballast** `#ballast` | Without it the pilgrim floats; with it, in water, hold down to sink and walk the bottom, up or jump to rise | Exits under water, under walls that reach into it |
| **Heat plating** `#heat-plating` | Passive: heat (vents, the air over slag) no longer burns (`docs/gameplay.md#heat`). Slag itself kills, plated or not | Vents and molten mouths |

Rules for rooms: only the physics layer makes metal (`docs/art-direction.md#visuals-never-gameplay`); keep a body's height of air below a ring; deep water beside metal stays Ballast's gate, since water lets go of a grip. Each ability has its lab check (`docs/testing.md#check`).

## Controls `#controls`

Bindings are the input map in `project.godot` (names in `InputActions`). Debug builds show them on screen, gameplay and debug apart and small (owner, 2026-11-01; `ControlsManual`).

| Action | Keyboard | Gamepad |
| -- | -- | -- |
| `move_left` / `move_right` | A / D, ← / → | Left stick, d-pad |
| `move_up` / `move_down` (in water with Ballast: rise / sink) | W / S, ↑ / ↓ | Left stick, d-pad |
| `jump` | Space, Z | A |
| `dash` (with the Drill, down + dash dives) | Shift, C | RB, RT |
| `attack` | J, X | X |
| `harpoon` | K, V | B, LT |
| `interact` (rest at a shrine, read, talk; in a conversation, `interact` or `jump` moves on) | E | Y |
| `map` | M, Tab | LB |
| `capture_screenshot` | F12 | Share / Misc |
| `capture_video` (start/stop) | F10 | — |
| `debug_respawn` | R | Back |
| `debug_toggle_{mag_grip,harpoon,drill,ballast,heat_plating}` | F1–F5 | — |
| `debug_cycle_biome` | F6 | — |
| `debug_cycle_paint` (the biome's paint, each variant, none: `docs/art-direction.md#signature`) | F9 | — |
| `debug_toggle_hud` (state readout and the debug keys) | F7 | — |
| `debug_toggle_manual` (hides the gameplay controls) | F8 | — |
