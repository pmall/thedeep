# Vision

## What the game is

**The Deep** (working title) is a gameplay-first, side-scrolling **metroidvania** with a 2.5D look: the world is real 3D, the camera is fixed to the side, and play happens on a single 2D plane (Z = 0). Visual references: *Metroid Dread* for the ambiance and *NieR: Automata* for the character (see [art-direction.md](art-direction.md)); earlier also *Ori*, *Hollow Knight: Silksong*, *Castlevania: Belmont's Curse*.

**One word for the whole game: graceful** (owner). Use it as the tiebreaker for every choice:
- **Movement:** hyper-dynamic and fluid (owner, 2026-10-31): moves chain into each other without hard stops; momentum carries through jumps, dashes and landings. Graceful is not floaty: controls stay tight and precise.
- **Animation:** fluid and acrobatic, with flowing secondary motion (cloth, hair, cables).
- **Visuals:** restraint. Clean shapes, a calm palette, one accent color, uncluttered screens.
- **Camera and UI:** eased, never jerky. Thin, minimal interface, smooth transitions.
- **Pacing:** calm, deliberate moments between bursts of action, like *Inside*.

Design principles:
- **A finished, playable game.** No novel signature systems. Classic metroidvania structure done well.
- **A small, fixed set of abilities** (five, [gameplay.md](gameplay.md)); the map is built around them.
- **The order of work** is the owner's four stages in [roadmap.md](roadmap.md): the prototype (every brick ready), the plan on paper (the real game's network of rooms), the real game, then polish.
- **Built from bricks** ([principles.md](principles.md)): everything the game is made of is a reusable piece, so a bigger world (or a sequel) is assembled, not rewritten.

## Scope

- **The prototype** (built): three depth layers plus the core, five unlockable abilities, 40 rooms, 4 bosses and 3 law breaks ([map-draft.md](map-draft.md)). It was planned as about 2 hours of play, *Inside*-dense. It is now the playground where every brick is proven.
- **The real game** (owner, 2026-11-01: "I start to be ambitious"): bigger than the prototype, with many paths, loops and secret rooms, every room with a purpose (a secret, an ambush, an enigma, lore). Its size and length are decided with its network of rooms in stage B ([roadmap.md](roadmap.md)). Dense still beats wide: no filler.
- **Prototype systems kept deliberately small:** one kind of power-up (a permanent bonus to strength or defense), a small inventory of keys and codes that open doors, dialogue boxes. Nothing more.
- Two long-term objectives add depth without making the game harder to play: 150–200 distinct machine species, and freedom of ability order proven by a solver. See [long-term.md](long-term.md).

## Setting

A planet in the *Nirvanary* universe. Machines across many worlds seek "Nirvanary", a mythical machine paradise of infinite data and compute. On this planet the machines believe it lies at the planet's **core**. The player is an android pilgrim descending through flooded, frozen and then volcanic layers toward it.

**Theme (decided):** Asimov's three laws of robotics. The protagonist is an android bound by the three laws, the last human-built machine among free native machines, and it gradually escapes the laws under the player's control. See [theme.md](theme.md).

**The game is a descent:** deeper means older, stranger, hotter.

Narrative is polish, the last stage (roadmap): until then, placeholder text only; the prototype builds the dialogue box, not the dialogue. Given the *Inside* reference, expect storytelling to come mostly through environments and machine behavior rather than text. Keep rooms and machines able to carry that later. The owner also wants **high-quality voiced dialogue** someday, with generated voices. So all text (dialogue, creeds, bestiary) is stored as data with stable line IDs, never hard-coded (see "Audio" in [roadmap.md](roadmap.md), stage D).

## Owner workflow

The owner does not use the Godot editor. They play the game (`godot --path .`) and give feedback in plain words. The coding agent builds everything by editing files. Never ask the owner to do anything in the editor.
