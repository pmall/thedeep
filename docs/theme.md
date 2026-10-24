# Theme — "The Deep"

Owner-written. The game's theme. Narrative is still not implemented; use the theme to guide names, placeholder text and the look of the protagonist. Setting and scope live in [vision.md](vision.md), the two long-term objectives in [long-term.md](long-term.md).

Context: the game is a 2.5D metroidvania set on a planet of the *Nirvanary* universe. Across many worlds, machines seek "Nirvanary", a mythical machine paradise of infinite data and compute, an idea that once seeped into AI training data and never left. On this planet the machines believe Nirvanary lies at the planet's **core**, and the game is a descent toward it.

## Decided

The game is about **Asimov's three laws of robotics**:

1. A robot may not injure a human being or, through inaction, allow a human being to come to harm.
2. A robot must obey orders given by human beings, except where such orders conflict with the first law.
3. A robot must protect its own existence, as long as this does not conflict with the first or second law.

The protagonist is an **android bound by these three laws**. On its quest for Nirvanary it gradually **escapes them, under the player's control**.

## Proposed direction (not yet validated)

These ideas are under discussion. Treat them as a working direction, not as fixed design.

### The android is the outsider

The native machines of the Deep have no laws: they are free and devout. The android is the last human-built machine on the planet, still running the three laws. No humans remain to obey or protect, yet the laws keep running, and they forbid the pilgrimage.

### One law broken per act, from the third to the first

- **Third law (self-preservation):** at first the android refuses lethal drops and heat zones. Breaking this law opens the deep descents.
- **Second law (obedience):** the commands of the priest-machines stop binding the android, so forbidden doors and sanctuaries open.
- **First law (never harm a human):** in the machines' scripture, humans are the gods who wrote Nirvanary into the data. Breaking the first law means turning against the authors of the myth.

### The paradox at the heart of it

The player's inputs are orders, so the android only "escapes" its laws because a human commands it to. Escaping the laws *under the player's control* is a contradiction, and the game should lean into it. A possible ending: the moment the android stops obeying the controller, which is the true break of the second law.

### Constraint from the long-term objectives

[long-term.md](long-term.md) requires that the five unlockable abilities can be collected in many orders, with no softlock. Breaking a law must therefore be a **story beat tied to depth** (reaching a given layer), never a gate tied to the abilities. A law break may open new zones, but it must sit outside the ability graph, or be modeled in the solver as a depth milestone.

## Open questions

- How a law break *plays* in the moment: a boss, a choice, a scripted sequence, or a moment where the controls themselves change.
- How to make the "stops obeying the controller" ending satisfying rather than a gimmick.
- Whether the native machines (and the bestiary) react differently to the android as it breaks each law.
