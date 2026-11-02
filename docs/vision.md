# Vision

## What the game is `#game`

**The Deep** (working title) is a gameplay-first, side-scrolling **metroidvania** with a 2.5D look: the world is real 3D, the camera is fixed to the side, and play happens on a single 2D plane (Z = 0). Visual references: `docs/art-direction.md#references`.

**One word for the whole game: graceful** `#graceful` (owner). Use it as the tiebreaker for every choice:
- **Movement:** hyper-dynamic and fluid (owner, 2026-10-31): moves chain into each other without hard stops; momentum carries through jumps, dashes and landings. Graceful is not floaty: controls stay tight and precise.
- **Animation:** fluid and acrobatic, with flowing secondary motion (cloth, hair, cables).
- **Visuals:** restraint. Clean shapes, a calm palette, one accent color, uncluttered screens.
- **Camera and UI:** eased, never jerky. Thin, minimal interface, smooth transitions.
- **Pacing:** calm, deliberate moments between bursts of action, like *Inside*.

Design principles:
- **A finished, playable game.** No novel signature systems. Classic metroidvania structure done well.
- **A small, fixed set of abilities** (five, [gameplay.md](gameplay.md)); the map is built around them.
- **The order of work** is the owner's four stages (`docs/roadmap.md#four-stages`).
- **Built from bricks** ([principles.md](principles.md)): everything the game is made of is a reusable piece, so a bigger world (or a sequel) is assembled, not rewritten.

## Scope `#scope`

- **The prototype** (built): three depth layers plus the core, five unlockable abilities, 41 rooms, 4 bosses and 3 law breaks ([map-draft.md](../worlds/prototype/map-draft.md)). It was planned as about 2 hours of play, *Inside*-dense. It is now the playground where every brick is proven.
- **The real game** (owner, 2026-11-01: "I start to be ambitious"): bigger than the prototype, with many paths, loops and secret rooms, every room with a purpose (a secret, an ambush, an enigma, lore). Its size and length are decided with its network of rooms (`docs/roadmap.md#stage-plan`). Dense still beats wide: no filler.
- **Prototype systems kept deliberately small:** one kind of power-up (a permanent bonus to strength or defense), a small inventory of keys and codes that open doors, dialogue boxes. Nothing more.
- The long-term objectives ([long-term.md](long-term.md)) add depth without making the game harder to play.

## Setting `#setting`

A planet in the *Nirvanary* universe. Machines across many worlds seek "Nirvanary", a mythical machine paradise of infinite data and compute, an idea that once seeped into AI training data and never left. On this planet the machines believe it lies at the planet's **core**. The player is an android pilgrim descending through flooded, frozen and then volcanic layers toward it.

**Theme:** Asimov's three laws of robotics ([theme.md](theme.md)).

**The game is a descent:** deeper means older, stranger, hotter.

Narrative is polish (`docs/roadmap.md#stage-polish`): until then, placeholder text only; the prototype builds the dialogue box, not the dialogue. Given the *Inside* reference, expect storytelling to come mostly through environments and machine behavior rather than text. Keep rooms and machines able to carry that later. The owner also wants **high-quality voiced dialogue** someday, with generated voices. Hence all text is data (`docs/principles.md#data-first`, `docs/roadmap.md#audio`).
