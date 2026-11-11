# Vision

## What the game is <a id="game"></a>

**The Deep** (working title) is a gameplay-first, side-scrolling **metroidvania** with a 2.5D look: the world is real 3D, the camera is fixed to the side, and play happens on a single 2D plane (Z = 0). Visual references: `docs/art-direction.md#references`.

**One word for the whole game: graceful** <a id="graceful"></a> (owner). Use it as the tiebreaker for every choice:

- **Movement:** hyper-dynamic and fluid: moves chain into each other without hard stops; momentum carries through jumps, dashes and landings. Graceful is not floaty: controls stay tight and precise.
- **Animation:** fluid and acrobatic, with flowing secondary motion (cloth, hair, cables).
- **Visuals:** restraint. Clean shapes, a calm palette, one accent color, uncluttered screens.
- **Camera and UI:** eased, never jerky. Thin, minimal interface, smooth transitions.
- **Pacing:** calm, deliberate moments between bursts of action, like *Inside*.

Design principles (owner):

- **A finished, playable game.** No novel signature systems. Classic metroidvania structure done well.
- **A small, fixed set of abilities** (five, `docs/gameplay.md#abilities`); the map is built around them.
- **Built from bricks** (`instructions/principles.md#bricks`), in the owner's four stages (`docs/roadmap.md#four-stages`).

## Scope <a id="scope"></a>

- **The prototype**: three depth layers plus the core, the five abilities, bosses and law breaks ([map-draft.md](../worlds/prototype/map-draft.md)). The playground where every brick is proven.
- **The real game** (owner): bigger than the prototype, with many paths, loops and secret rooms, every room with a purpose (a secret, an ambush, an enigma, lore). Its size and length are decided with its network of rooms (`docs/roadmap.md#stage-plan`). Dense still beats wide: no filler.
- **Game systems kept deliberately small** <a id="game-systems"></a> (owner):
  - one kind of power-up, a permanent bonus to strength or defense;
  - a small inventory of keys and codes that open doors, part of the requirement language of exits so the solver proves the world with them (no key behind its own door);
  - a dialogue box: a speaker and lines in turn, small on screen (the owner plays on a gamepad);
  - the room kinds the plan uses: a hidden passage (a secret room's way in), an ambush (an arena that seals while a wave of machines stands), a switch and its mechanism (an enigma's piece).
- The long-term objectives (`docs/long-term.md`) add depth without making the game harder to play.

## Setting <a id="setting"></a>

A planet in the *Nirvanary* universe. Machines across many worlds seek "Nirvanary", a mythical machine paradise of infinite data and compute, an idea that once seeped into AI training data and never left. On this planet the machines believe it lies at the planet's **core**. The player is an android pilgrim descending through flooded, frozen and then volcanic layers toward it.

**Theme:** Asimov's three laws of robotics ([theme.md](theme.md)).

**The game is a descent:** deeper means older, stranger, hotter.

Narrative is polish (`docs/roadmap.md#stage-polish`): until then, placeholder text only. Given the *Inside* reference, expect storytelling to come mostly through environments and machine behavior rather than text, so keep rooms and machines able to carry it. The owner wants **high-quality voiced dialogue** someday, with generated voices: hence all text is data (`instructions/principles.md#data-first`, `docs/roadmap.md#audio`).
