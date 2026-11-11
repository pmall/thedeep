# Network — the sequel world

The sequel world's network file (`Network` in [world.tres](world.tres)): the sequel test's world (`docs/roadmap.md#sequel-test`), 7 rooms on one layer, Brine (`resources/biomes/brine.tres`, a variant of the frozen layer), built from the prototype's bricks and nothing else. It stays as a fixture the check loads (`instructions/testing.md#check`). The `map` blocks below are parsed by `MapDraft` (its docblock has the format) and held to the built rooms like the prototype's network (`worlds/prototype/map-draft.md#draft-markers`): the rooms' markers are the truth, and this file follows them. Names and text are placeholders.

```map
start Quay
goal Beacon
target orders 1         # two abilities, Mag-grip then the Harpoon: one order
target positions 1
target rooms 5 8
```

## The shape <a id="sequel-shape"></a>

`(X)` a way needing X, `[X]` a relic, `→` one way.

```
            Hold[H] (key) ─(H)→ Weir (lever) ─(gate)─ Beacon goal
             ↑(M)  ↓ drop          ↓ back onto the ledge
Strongroom ─(key)─ Quay ─ Locks ─ Chantry[M]
```

M Mag-grip, H Harpoon; (key) the Strongroom key, on the Hold's ledge; (gate) the sluice gate the Weir's lever raises.

Its rooms, what each is for and what happens there: [rooms.tsv](rooms.tsv).

```map
room Quay Brine
room Strongroom Brine
room Locks Brine
room Chantry Brine
room Hold Brine
room Weir Brine
room Beacon Brine

way Quay <-> Strongroom : StrongroomKey          # the locked door in the Quay's west wall
powerup Strongroom strength                      # on a dais
way Quay <-> Locks
way Locks <-> Chantry
relic Chantry MagGrip
way Locks -> Hold : MagGrip                      # up the iron wall to the high doorway
way Hold -> Locks                                # back out onto the wall's top, and the drop
relic Hold Harpoon
item Hold StrongroomKey                          # on the high ledge
way Hold -> Weir : Harpoon                       # the rings up to the ledge and its doorway
way Weir -> Hold                                 # back out onto the ledge
way Weir <-> Beacon : WeirLever                  # the sluice gate, raised by its lever
item Weir WeirLever                              # the lever, by the gate
```
