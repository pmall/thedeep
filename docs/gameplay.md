# Gameplay

**Feel: graceful** ([vision.md](vision.md)). Moves chain without hard stops and momentum carries through transitions, while control stays tight and precise.

## Base moveset (implemented)

- **Run**
- **Jump** with variable height (release early for a short hop), coyote time and a jump buffer (0.15 s each)
- **Ledge assist** (owner, 2026-11-01: jumps felt too exact, against the hyper-dynamic goal): airborne and pressing into a wall whose top is at most `LedgeAssistHeight` (0.6 m) above the feet, the pilgrim is lifted onto it, just past the edge, keeping its way and speed (at least 60% of a run); the visible body eases up from where it was (`Player.LedgeAssist`, from the jump and fall states; not from a dash). The map's planner does the same, so no gate is jumped by it unproven.
- **Dash** on the ground and in the air (one air dash per airtime, refilled on landing), with a short cooldown. A ground dash can be cancelled into a jump.

All numbers live in `resources/player_tuning.tres` (see "Tuning" below).

**Room metrics** (for building rooms): a full jump rises 3.2 m, so a step the pilgrim must climb is at most **2.5 m**; a ladder of ledges rises 2.5 m per ledge (never 3). A gap crossed at one height is at most **7 m** (the exact pilgrim crosses about 10 m with a running jump and the air dash). These leave room for error, because jumps must never be exact (owner, 2026-11-01): the room check walks every room again with a much clumsier pilgrim (jumps 18% lower, dashes 35% shorter, runs 20% slower, never taking off from a ledge's very end, no ledge assist; it climbs 2.5 m and crosses 7.5 m) and fails on any way it misses ([testing.md](testing.md)). The ledge assist is extra forgiveness on top, never a reason for a higher step: with it a ledge up to about 3.6 m is reached, so a wall that gates an ability (Mag-grip's, the Harpoon's) stays well above that (the room check proves it). A room that moves the tuning (a higher jump) moves these too.

## Combat, health and the pilgrimage (M6)

- **Attack** (J / X, pad X): a quick swing, a pale crescent sweeping from above to the front (`PlayerAttack`, timing in `Swing`, pure). It never stops the pilgrim: momentum carries through. Upright and on Mag-grip surfaces. A press during the recovery is buffered, so swings chain. Each machine the hit window reaches takes one hit per swing (`MachineDirector` resolves hits against each machine's body box): it flashes, reels (its brain stunned 0.35 s, a wind-up lost) and is pushed back; when its health (`MachineSpecies.Health`) is gone it folds down and is gone until the room is entered again.
- **Health:** `MaxHealth` hits (5), shown top left as thin diamonds. A machine's blow (`MachineSpecies.Damage`) takes health, throws the pilgrim back and up, takes control away for `HurtStun`, then nothing hurts for `HurtGrace` (the body shimmers).
- **Death:** the veil falls slowly; the pilgrim wakes at the last shrine it rested at, whole, the room renewed (its machines back).
- **Pilgrim shrines** (save points, `scenes/props/Shrine.tscn`): standing at one shows "Rest"; **E** (pad Y) rests: full health, the glyphs flare, the game is saved there.
- **Ability relics** (`scenes/props/AbilityRelic.tscn`): walking into one takes it: the pilgrim stands still while the core drifts in, the ability unlocks, a banner names it and says how to use it, and the game saves. A taken relic stays taken.
- **Saving:** `user://save.json` (`SaveData`, pure; on Linux `~/.local/share/godot/app_userdata/The Deep/save.json`): the last shrine rested at, abilities, relics taken, the map. A normal launch resumes there; `godot --path . -- --new` starts fresh (the save is overwritten at the first rest). Checks, look-dev and `--room` runs never read or write it.
- **Map** (M / Tab, pad LB): the visited rooms, placed from the exits crossed; pauses the game.
- **Leaving a room:** walk (run, dash, drift) out through a doorway; standing in one, or dropping into one from above, never travels. Hatches in floors and ceilings are crossed whatever the way.
- **Scripture plates** (`scenes/props/ScripturePlate.tscn`, the Scriptorium): standing at one shows "Read"; **E** (pad Y) shows its words in the banner. Lore only.
- **Bosses** (placeholders until the character pass, roadmap phase 4): a boss stands in an arena (`BossSeal`). Stepping in drops the gates behind and ahead (`Portcullis`), and its name shows; breaking it raises them, and it never stands again (saved at once). Dying in the fight returns to the last shrine, the fight renewed. **The Precentor** (Chapterhouse, `resources/machines/precentor.tres`, `ConductorBrain`): beats time until it sees the pilgrim, then stalks it and brings its baton down after a long wind-up (0.75 s), lunging into the blow; every third strike a quicker one (0.4 s wind-up) follows in the same direction. Hits never stagger it or push it far (`StaggerTime` 0, `Heft` 0.15: species data), so standing and swinging loses about half the time; the slow recovery after a blow (1.1 s) is the opening, and the pilgrim passes through machines, so slipping behind it during the wind-up dodges. 24 hits.
- **The Archivist** (Vault, `resources/machines/archivist.tres`, `SwoopBrain`; a placeholder until the character pass): a reliquary the size of a cabinet under a great rotor, trailing seven cables of lights. It wakes when the pilgrim comes within 18 m (seen or not), drifts high over it (6 m above, to one side), and every 2.2 s stops dead and pitches back (the wind-up, 0.75 s), then dives at 15 m/s through where the pilgrim is and 2.5 m past, striking whatever it touches; it hangs low and still for 1.5 s after (the opening), then climbs back. Every third dive it turns and dives again at once (0.45 s wind-up). Hits never stagger it. 22 hits. Two shelves in the arena to dodge onto.
- **Lines spoken to the pilgrim** (`Proclamation`, an area): coming near shows a speaker and a line in the banner, once per visit (the Hierarch's order at the forbidden door). Text only.
- **Law breaks** (story beats, [theme.md](theme.md); outside the ability graph): **the third law** stands at the Brink's lip as a veil of glyphs the pilgrim cannot walk through, its words rising as the pilgrim nears ("A robot must protect its own existence."). Pressing on toward the drop strains against it (the words tremble and brighten); after 2.6 s of pressing (letting go slips back at half the rate) the law breaks: the veil scatters, the pilgrim holds still a breath, a banner, and the way is open for good (saved at once). Then the jump, one way down. **The second law** stands at the Sanctum's forbidden door the same way: the Hierarch (a priest-machine on the Precentor's body that never strikes) orders the pilgrim back as it comes near, and the law's words ("A robot must obey the orders it is given.") hold it at the door until it presses through.
- **Lifts** (`scenes/props/Lift.tscn`, the Counterweight): an iron cage on a rope, its weight on the other end. Standing in it at its lower stop lets go of the brake after a breath (0.45 s; the camera shudders): it gathers speed, rises at 4.5 m/s while the weight sinks past, and eases into its upper stop. Once on its way it can't be stopped; stepping or jumping off leaves it to finish the climb, and left empty at the top for 3 s it sinks slowly back to wait. It never carries the pilgrim down. The numbers are `[Export]`s on the lift (they are a mechanism's, not the pilgrim's).
- **Falling out of a room** (below its camera bounds, a drop into a room not built yet) returns the pilgrim to its last shrine.
- All combat numbers are in `player_tuning.tres` (Attack, Hurt groups), next to the movement ones.

## Unlockable abilities

These gate the map. Every ability is one simple state or rule: no systemic or emergent mechanics.

| Ability | What it does | What it gates |
|---|---|---|
| **Mag-grip** | Walk on **metal** walls and ceilings. Rock cannot be gripped. | Vertical shafts and ceilings made of metal |
| **Harpoon** | Fire at an anchor point and zip to it | Gaps with anchor points |
| **Drill** | Break cracked rock | Cracked rock walls and floors |
| **Ballast** | In water, sink (hold down) or float (up / jump) | Exits under water, under walls that reach into it |
| **Heat plating** | Passive: heat (vents, the air over slag) no longer burns | Vents and molten mouths (the Chimney, the Pyre, the way to the core) |

The player can collect them in many orders, and that freedom is proven by a solver ([long-term.md](long-term.md), objective 2).

**Mag-grip (implemented, M6).** `GripState` with pure geometry in `GripMath` (unit tested). Metal is the physics layer only (`world_metal`, set by `Block.Surface`). The room check simulates it (`ReachPlanner` with `GripLimits`): a way Mag-grip opens must be reached by gripping the room's metal.
- **Attach:** press into a metal wall (on the ground or in the air), or rise into a metal ceiling (a jump). The body turns about its center so it swings out from the wall; its up becomes the surface normal (`UpDirection` and the body's rotation about Z, so the collider turns with it).
- **Move:** held input along the surface (up/down on a wall, left/right under a ceiling, in screen terms) at `GripSpeed`. **Pull away** from the surface to let go, **jump** to leap off (away from a wall at `GripLeapAwaySpeed` and up at `GripLeapUpFactor` of a jump; downward from a ceiling), **dash** to let go and dash. `GripRegrabDelay` keeps the same surface from catching the player straight back.
- **Corners snap** (a later pass may smooth them): a metal wall ahead turns the player onto it (inner corner); a surface's end wraps the player around the edge onto the end face (outer corner); reaching a floor stands it up. After a corner, the input that led there keeps meaning "keep going" until it changes, so holding up climbs a wall, turns under the ceiling and walks along it. A rock face ahead just stops the player; a rock end face lets go. **Only metal holds:** where the metal ends and rock goes on in the same plane, the player lets go (until 2026-11-01 it slid on along the rock: down the Descent's flooded gate, through its water and into the Sluice without Ballast). **Water lets go too:** walked down into swimmable water, once the body's center is deeper than `WaterEnterDepth` (`GripMath.Drowned`) the player lets go and swims, so deep water stays Ballast's gate even beside metal. The planner places no grip pose on rock or drowned.
- **The visible body eases** into each new pose (`PlayerVisual.CarryOver`): the collider snaps, the body follows in about a tenth of a second. Animation sees the body's own frame, so walking on a wall plays the run cycle; the coat and braid still hang toward the world's floor.
- A grip surface refills the air dash. Travelling through an exit while gripping stands the player up first.
- Checked headless in `scenes/rooms/MechanicsLab.tscn` (`GripCheck`, in `tools/check.sh`); look-dev shoots a grip reel there (`Grip.png`).

**Harpoon (implemented, M8).** `HarpoonState`, aiming in `HarpoonAim` (pure, unit tested), the eye and the line in `HarpoonRig` (in Main).
- **Anchors** are mooring rings placed in rooms (`scenes/props/HarpoonAnchor.tscn`): the node's origin is where the pilgrim's center is pulled to. They are only a position (no collision); keep a body's height of air below one.
- **Sight:** each tick the rig picks the anchor the pilgrim would fire at: within `HarpoonRange` of the body's center (not nearer than `HarpoonMinDistance`), in clear sight (a ray on the solid layers). Holding up or down **aims** (only anchors within about 70° of the held direction); otherwise anchors ahead beat anchors above, which beat anchors behind, nearer first. The anchor just zipped to is skipped until the pilgrim stands or grips somewhere. The anchor in sight brightens.
- **Fire** (K / V, pad B or LT): from the ground, the air, a dash or a Mag-grip surface. The line flies out at `HarpoonThrowSpeed` while the pilgrim hangs, slowing; once it bites, the pull gathers speed (`HarpoonZipAcceleration`) up to `HarpoonZipSpeed` until the body's center reaches the anchor. Then it lets go with `HarpoonExitSpeedFactor` of the pull's speed and at least `HarpoonExitLift` upward (unless the pull went steeply down), and the air dash is back. Jump or dash during the pull cancel into them, keeping momentum; a pull blocked by a solid lets go where it stands.
- Checked headless in the mechanics lab (`HarpoonCheck`, in `tools/check.sh`): sight, fire, pull, release, chaining to a second anchor, blocked sight, range, jump cancel. The room check's geometry pass also zips between each room's anchors (`ReachPlanner` with `HarpoonLimits`): a way the Harpoon opens must be reached that way.

**Drill (implemented, M8).** One rule on the dash: with the Drill, a dash bores through **cracked rock** and carries on, and **down + dash** dives (`DiveState`): straight down at `DiveSpeed` until the pilgrim lands (at most `DiveMaxTime`), boring through cracked floors on the way. From the air the dive spends the air dash like a dash; from the ground only standing on cracked rock (down + dash on sound rock is a plain dash). Landing from a dive is a hard landing (it wakes sleepers). Numbers in `player_tuning.tres` (Drill group).
- **Cracked rock** is a `Block` with `Cracked`: always on the rock layer (never grippable), shown by `assets/materials/cracked.tres` (deep fissures webbing the face; rock.gdshader's `fracture`) unless its look is overridden (the Coldstacks' plug of cracked ice). The moving body tests ahead each tick (`Player.Bore`, from any state whose `Bores` is true: the dash and the dive), so the dash never stops against it. It shatters (`Shatter`: chunks of its own material flung along the way, a dust cloud from `scenes/fx/Rubble.tscn`), the camera shakes, and it stays broken: `Session` remembers it at once, like a relic.
- Checked headless in the mechanics lab (`DrillCheck`, in `tools/check.sh`), against cracked blocks it places itself. The geometry check removes cracked rock for ways the Drill opens.

**Water and Ballast (implemented, M8).** Water the pilgrim swims in is a `WaterBody` with `Swim` (its play-plane region, from the surface down `Depth`, is an area on the `water` layer). `SwimState` with pure motion in `WaterMotion` (unit tested; the map's planner uses the same functions).
- **Without Ballast the pilgrim floats.** Feet deeper than `WaterEnterDepth` under the surface (and not rising) swim: a plunge keeps `SplashKeep` of its downward speed, dips and bobs back; the body rides its float line, feet `FloatDepth` under the surface (head and shoulders above), easing toward it at `FloatStiffness` per meter (at most `RiseSpeed` up, `SinkSpeed` down), the water changing its vertical speed by at most `WaterAcceleration` per second. Swimming across at `SwimSpeed`. A jump near the float line (within `WaterJumpWindow`, or wading above it) leaves the water as a full jump. A dash works in water; the air dash refills on entering it. Wading into water too shallow to swim stands the body up again.
- **Ballast** fills and blows tanks: in water, **holding down** fills them (`Player.Sunk`): the body sinks at `SinkSpeed` and walks the bottom; **up or jump** blows them and it rises back to float. The tanks stay as they are until changed, and empty whenever the pilgrim leaves the water. Sunk, the Drill's dive works under water (down + dash on cracked rock); floating, the surface catches a dive.
- Water gates: an exit at the bottom of water (or under a wall that reaches into it) needs Ballast. The geometry check swims every room's water with the same motion (floating for every planner; sinking and rising anywhere connected for Ballast).
- Checked headless in the mechanics lab (`BallastCheck`, in `tools/check.sh`), in a tank it places itself.

**Heat plating (implemented, M8).** Heat is a `HeatZone` (`scripts/props/HeatZone.cs`): a box on the play plane rising from its node (a vent's column of hot air, the air over slag), drawn as shimmering haze (`heat_haze.gdshader`) with embers (`scenes/fx/Embers.tscn`) and a glow. Without Heat plating, touching it burns: a blow (`Damage`, thrown back out the way the pilgrim came, the grace period); while the grace lasts it still throws the pilgrim out unhurt (`Player.Repel`), so heat is never crossed without the plating. With Heat plating it is harmless (passive). Slag rivers are heat over a fall out of the room: falling in is the end, plated or not. The geometry check treats heat as space no planner enters without Heat plating, and an exit Heat plating opens must be reached by a planner that walks through it. Checked headless in the lab (`HeatCheck`, in `tools/check.sh`).

Abilities are flags in `AbilitySet` (`Player.Abilities`). In debug builds, F1–F5 toggle them.

## Tuning

- **All movement numbers** (speeds, accelerations, jump height and time to apex, jump cut, gravity multipliers, coyote time, jump and dash buffers, dash distance, duration and cooldown, air dashes) live in `resources/player_tuning.tres` (a `PlayerTuning` resource). Never hard-code them.
- These numbers are also the movement limits that the map's geometry check will read ([long-term.md](long-term.md)).
- Gravity and take-off speed are derived from `JumpHeight` and `JumpTimeToApex`. A full held jump reaches slightly more than `JumpHeight` (about 3.36 m for 3.2) because of the lighter apex gravity.
- Camera and visual feel numbers are `[Export]`s on their scenes (`scenes/camera/SideCamera.tscn`, `PlayerVisual`).

## Controls

Input actions are defined in `project.godot` and mirrored in `scripts/core/InputActions.cs`. Never use string literals for them elsewhere.

| Action | Keyboard | Gamepad |
|---|---|---|
| `move_left` / `move_right` | A / D, ← / → | Left stick, d-pad |
| `move_up` / `move_down` (in water with Ballast: rise / sink) | W / S, ↑ / ↓ | Left stick, d-pad |
| `jump` | Space, Z | A |
| `dash` (with the Drill, down + dash dives) | Shift, C | RB, RT |
| `attack` | J, X | X |
| `harpoon` | K, V | B, LT |
| `interact` (rest at a shrine) | E | Y |
| `map` | M, Tab | LB |
| `capture_screenshot` | F12 | Share / Misc |
| `capture_video` (start/stop) | F10 | — |
| `debug_respawn` | R | Back |
| `debug_toggle_{mag_grip,harpoon,drill,ballast,heat_plating}` | F1–F5 | — |
| `debug_cycle_biome` | F6 | — |
| `debug_toggle_hud` (state readout and the debug keys; the room name and position show in debug builds without it) | F7 | — |
| `debug_toggle_manual` (hides the gameplay controls) | F8 | — |

**On-screen controls** (debug builds; `ControlsManual`, owner 2026-11-01: gameplay and debug apart, and small). The **gameplay controls**, bottom left, always: each gameplay action's main button and what it does (the ability moves too: dive, sink and rise, Mag-grip), each button lit while held. They show the keyboard while the keyboard was used last and the gamepad while the pad was (a stick counts past half way), read from the input map, keys by the layout's labels (on AZERTY the physical WASD shows Z Q S D, and `map`'s physical M shows `,`). An ability's rows stay faint until it is held. The **debug keys**, top left with the F7 readout: each key, then the pad button where there is one (respawn: View; screenshot: Share). Words are `ui.manual.*` in the line book; add a row to `ControlsManual.Gameplay` or `.Debug` when an action is added.
