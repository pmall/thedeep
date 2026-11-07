# The Deep — agent guide

A 2.5D side-scrolling **metroidvania** (Godot .NET, C#) about an android pilgrim descending toward a planet's core ([docs/vision.md](docs/vision.md)). The coding agent builds everything by editing text files; the owner only plays and gives feedback. **Never ask the owner to use the Godot editor.**

## Every session `#session`

1. Read this file and [docs/roadmap.md](docs/roadmap.md) (direction, status, the current stage's items).
2. Read the docs below that match the task. Don't read them all up front.
3. Work on the current stage's first unfinished item, or what the owner asks for. For any visual change, capture before and after and look at the images (`docs/art-direction.md#judge-images`).
4. `tools/check.sh` passes (`docs/testing.md#check`), then commit. Never report work as done while it fails. Keep `godot --path .` working: it is the owner's launch command.
5. Update `docs/roadmap.md#status`, and `docs/bricks.md` when a brick changes status: replace what changed, never append a session log.
6. Hand over with **one command that reaches the new content with the abilities it needs**, and what to try there (`docs/testing.md#playtest`). Never hand over a command that drops the owner somewhere they can't move.

When the owner changes direction, rewrite the docs it touches so none of them contradicts it. Don't stack the new rule on top of the old one.

## Orchestrating `#orchestrate`

The main session orchestrates and subagents do the work (owner, 2026-11-07), so the main session's context stays small over a long session.
- **One subagent per item**, or per well-bounded part of one. Its prompt names the item's id, the docs to read, the files in play, what done means, and that it runs `tools/check.sh` and commits only its own files. It reports back in a few lines: what changed, the commit, what it could not do, and any question for the owner.
- **Small tasks, small contexts.** A long subagent wastes as much context as a long main session. Give each one a task done in one short sitting (one species, one room's fix, one bug), cite the doc sections it needs (ids, not whole docs), and set a budget: which captures to judge by (only the spots and reels the change touches) and how many tries before it commits what is good and reports what is left. Logs are grepped, never read whole.
- **Builders run one at a time.** A subagent that builds, checks or captures has the working tree to itself: `tools/check.sh` and `tools/capture.sh` see the whole tree, and captures need the GPU to themselves (`docs/toolchain.md#gpu`). Read-only research (finding code, gathering open questions) may run beside it.
- **The main session reads reports**, and the docs it needs to decide. It doesn't read code or logs, and looks at a capture only to judge a visual change itself.
- **Stop at the owner's questions.** When what can be done without the owner is done, stop and hand over one exhaustive list of well-defined questions, each answerable on its own: what is asked, the options, the default until answered, and where to look. Don't go on working on guesses.

## Read when… `#read-when`

| You are about to… | Read |
|---|---|
| Understand the game, its scope and setting | [docs/vision.md](docs/vision.md) |
| Write, move or review any code | [docs/principles.md](docs/principles.md) (owner direction: bricks, the engine seam, data, typing) |
| Add or change a reusable piece; know what can be placed in a room | [docs/bricks.md](docs/bricks.md) (the catalog and each brick's status) |
| Write names or placeholder text, design the protagonist, or touch story beats | [docs/theme.md](docs/theme.md) (owner-written) |
| Do any visual work (materials, lighting, meshes, VFX, characters) | [docs/art-direction.md](docs/art-direction.md) |
| Touch movement, abilities, tuning or controls | [docs/gameplay.md](docs/gameplay.md) |
| Find where code lives, add a folder or a system | [docs/architecture.md](docs/architecture.md) |
| Create or edit `.tscn` / `.tres` / C# Godot scripts or shaders | [docs/godot-csharp.md](docs/godot-csharp.md) |
| Run, test, capture screenshots, or hand over to the owner | [docs/testing.md](docs/testing.md) |
| Deal with versions, paths, .NET environment or upgrades | [docs/toolchain.md](docs/toolchain.md) |
| Work on machines, rooms, connections or the map | [docs/long-term.md](docs/long-term.md) (owner-written) |
| Build or change a room, an exit or a relic in the prototype | [worlds/prototype/map-draft.md](worlds/prototype/map-draft.md) (the prototype world's network; keep it in step with built rooms) |
| Change the sequel world (the check's second world) | [worlds/sequel/network.md](worlds/sequel/network.md) (its network; keep it in step with its rooms) |

## Writing docs `#doc-rules`

- **Every instruction lives in this repository** `#no-memory`. The owner works on several machines, so nothing may depend on one machine: never use the agent's own memory (it is off in `.claude/settings.json`) or any file outside the project. What is worth remembering goes into the doc it belongs to, in the same commit.
- **Every fact lives in one place.** Other places cite it; they never restate it. Before adding a sentence, look for where it already lives.
- **Code is described only in its docblocks** (`///` on C# types and members, the header comment of a shader, a script or a tool). Docs hold what code can't say: the owner's direction, rules and the reasons behind them, design intent, status, workflows. A doc may name a type as the place to start reading, never describe what it does or how.
- **Sections are cited by id, never by number or title.** A heading (or a list item or table row others cite) ends with a stable id in backticks, as every heading in [docs/principles.md](docs/principles.md) does. Cite it as `docs/principles.md#types-truth`, from docs and code alike (paths from the project root). Ids are unique across the project, keep their meaning when a heading is reworded or moved, and are never reused. Don't number sections or list items others refer to.
- **No history.** What was done, when and in which session is in `git log`. Docs say how things are; an owner decision keeps its date.
