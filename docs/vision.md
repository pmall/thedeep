# Vision

## What the game is

**The Deep** (working title) is a gameplay-first, side-scrolling **metroidvania** with a 2.5D look: the world is real 3D, the camera is fixed to the side, and play happens on a single 2D plane (Z = 0). Visual references: *Metroid Dread* for the ambiance and *NieR: Automata* for the character (see [art-direction.md](art-direction.md)); earlier also *Ori*, *Hollow Knight: Silksong*, *Castlevania: Belmont's Curse*.

**One word for the whole game: graceful** (owner). Use it as the tiebreaker for every choice:
- **Movement:** moves chain into each other without hard stops; momentum carries through jumps, dashes and landings. Graceful is not floaty: controls stay tight and precise.
- **Animation:** fluid and acrobatic, with flowing secondary motion (cloth, hair, cables).
- **Visuals:** restraint. Clean shapes, a calm palette, one accent color, uncluttered screens.
- **Camera and UI:** eased, never jerky. Thin, minimal interface, smooth transitions.
- **Pacing:** calm, deliberate moments between bursts of action, like *Inside*.

Design priorities, in order:
1. **A finished, playable game.** No novel signature systems. Classic metroidvania structure done well.
2. **Ambience and art direction.** See [art-direction.md](art-direction.md).
3. **A small, defined set of abilities**, fixed before the map is designed, because the map is built around them. See [gameplay.md](gameplay.md).

## Scope

- **About 2 hours of play** (decided). The model is *Inside*: about 2 hours, rich in gameplay and story. Dense rather than wide: fewer rooms, each one with a purpose, no filler.
- Three depth layers plus the core. Five unlockable abilities. Estimate 40–60 rooms and 3–4 bosses, refined when the full map is designed (roadmap M7).
- Two long-term objectives add depth without making the game harder to play: 150–200 distinct machine species, and freedom of ability order proven by a solver. See [long-term.md](long-term.md).

## Setting

A planet in the *Nirvanary* universe. Machines across many worlds seek "Nirvanary", a mythical machine paradise of infinite data and compute. On this planet the machines believe it lies at the planet's **core**. The player is an android pilgrim descending through flooded, frozen and then volcanic layers toward it.

**Theme (decided):** Asimov's three laws of robotics. The protagonist is an android bound by the three laws, the last human-built machine among free native machines, and it gradually escapes the laws under the player's control. See [theme.md](theme.md).

**The game is a descent:** deeper means older, stranger, hotter.

Narrative is out of scope for now: placeholder text only. Given the *Inside* reference, expect storytelling to come mostly through environments and machine behavior rather than text. Keep rooms and machines able to carry that later. The owner also wants **high-quality voiced dialogue** someday, with generated voices. So all text (dialogue, creeds, bestiary) is stored as data with stable line IDs, never hard-coded (see "Later: audio" in [roadmap.md](roadmap.md)).

## Owner workflow

The owner does not use the Godot editor. They play the game (`godot --path .`) and give feedback in plain words. The coding agent builds everything by editing files. Never ask the owner to do anything in the editor.
