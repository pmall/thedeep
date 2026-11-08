# The Deep — agent guide

A 2.5D side-scrolling **metroidvania** (Godot .NET, C#) about an android pilgrim descending toward a planet's core ([docs/vision.md](docs/vision.md)). The coding agent builds everything by editing text files; the owner only plays and gives feedback. **Never ask the owner to use the Godot editor.**

**This file is the only agent instruction file** <a id="agents-file"></a>. Claude Code reads `AGENTS.md` natively, but only when there is no `CLAUDE.md`, `.claude/CLAUDE.md` or `CLAUDE.local.md` in the project or a folder above it, and any of those silently replaces it. Create none of them; if one appears above the project, tell the owner. Topic detail lives in `docs/`, pointed to from here.

## Every session <a id="session"></a>

1. Read this file and [docs/state.md](docs/state.md) (where things stand), then [docs/roadmap.md](docs/roadmap.md) for the current stage's items.
2. Read the docs below that match the task. Don't read them all up front.
3. Work on the current stage's first unfinished item, or what the owner asks for. For any visual change, capture before and after and look at the images (`docs/art-direction.md#judge-images`).
4. Every turn that changed files ends with the fast check; a code session ends with the full check (`AGENTS.md#code-session-end`). Keep `godot --path .` working: it is the owner's launch command.
5. Update [docs/state.md](docs/state.md), and `docs/bricks.md` when a brick changes status: replace what changed, never append a session log.
6. Hand over with **one command that reaches the new content with the abilities it needs**, and what to try there (`docs/testing.md#playtest`). Never hand over a command that drops the owner somewhere they can't move.

When the owner changes direction, rewrite the docs it touches so none of them contradicts it. Don't stack the new rule on top of the old one.

## Working principles <a id="working-principles"></a>

These hold for every task. [docs/principles.md](docs/principles.md) applies them to this game and its stack; it never restates them.

- **End of a code session** <a id="code-session-end"></a>. A session that changed code ends with the full check (`docs/testing.md#check`): the compiler, the linters and formatters, the headless checks and the tests. Never make a check pass by disabling a rule or skipping a check without writing down why.
- **Automated checks** <a id="checks"></a>. A formatter, a linter, the compiler and the tests, each the standard tool of its language; the formatter covers every file the project keeps, docs included, so a file's layout never depends on who last wrote it. Two commands (`docs/testing.md#check`): the **fast check** formats, lints and builds, and ends every turn that changed files, so problems are caught while they're small; the **full check** runs everything (the headless checks, the unit tests) and ends a code session. Formatting is the formatter's job, never fixed by hand.
- **Capabilities first, interfaces second** <a id="capabilities-first"></a>. What the game can do (its rules, its state, its map, its planner) is written once as plain code that knows nothing of how it's reached; the engine, the checks and the tools are thin layers on top. Below the rules, one layer wraps each data source (save files, a world's data), and only it knows how and where data is stored. Capabilities take everything as arguments (time, randomness, the world as data). This is what lets the game be tested without the engine, a second world be built with no code change, and a tool reuse a rule. The test: could a small script use the rules directly? Applied here as `docs/principles.md#engine-detail`.
- **Truthful types** <a id="truthful-types"></a>. Types say exactly what the game allows: nullable only when really absent, a union only when the value really takes several shapes; expected failures are in the types, unexpected ones are raised. Never weaken a type to quiet the compiler: having to fight it means the model is wrong. In a dynamic language (the Python tools) this holds for its annotations and their checker. Applied here as `docs/principles.md#types-truth`.
- **Every fact lives in one place**, so a change touches as few places as possible (`AGENTS.md#doc-rules`).
- **Knowledge lives in the repo**, not in an agent's memory, so every agent and every machine sees the same truth (`AGENTS.md#no-memory`). When the docs and the code disagree, the code wins and you fix the docs.
- **Git** <a id="git"></a>. The agent manages git and the remote (`git@github.com:pmall/thedeep.git`). It commits each coherent, passing unit of work with a message that says why, and pushes. It never commits secrets and never rewrites pushed history.
- **Working with the owner** <a id="owner"></a>. Product decisions are the owner's; the owner has delegated taste, tuning, look and scope within the roadmap to the agent (`AGENTS.md#orchestrate`). Technical decisions and the quality of the work are the agent's. When a technical choice touches the owner (cost, an account, a step only they can take), say so plainly. When a request conflicts with these principles, say so and propose a better way.

## Orchestrating <a id="orchestrate"></a>

The main session orchestrates and subagents do the work (owner, 2026-11-07), so the main session's context stays small over a long session.

- **One subagent per item**, or per well-bounded part of one. Its prompt names the item's id, the docs to read, the files in play, what done means, and that it runs the full check and commits only its own files. It reports back in a few lines: what changed, the commit, what it could not do, and any question for the owner.
- **Small tasks, small contexts.** A long subagent wastes as much context as a long main session. Give each one a task done in one short sitting (one species, one room's fix, one bug), cite the doc sections it needs (ids, not whole docs), and set a budget: which captures to judge by (only the spots and reels the change touches) and how many tries before it commits what is good and reports what is left. Logs are grepped, never read whole.
- **One deliverable per subagent** <a id="one-deliverable"></a> (owner, 2026-11-08). Measuring a problem, finding its causes and fixing each cause are separate subagents, run in turn, each started from the previous one's few-line report. A brief that needs numbered phases is several briefs. The main session holds the sequence; no subagent holds a whole item.
- **Interrupted, never resumed** <a id="fresh-handover"></a> (owner, 2026-11-08). A subagent cut off (a usage limit, a crash) is not resumed: its whole context would be reloaded cold. A fresh subagent takes over from the working tree, with a short brief of what is left. So a subagent commits each passing step as it goes, and a cut-off loses little.
- **Builders run one at a time.** A subagent that builds, checks or captures has the working tree to itself: `tools/check.sh` and `tools/capture.sh` see the whole tree, and captures need the GPU to themselves (`docs/toolchain.md#gpu`). Read-only research (finding code, gathering open questions) may run beside it.
- **The main session reads reports**, and the docs it needs to decide. It doesn't read code or logs, and looks at a capture only to judge a visual change itself.
- **Decide, then ask only what blocks** (owner, 2026-11-07). The agent makes every choice it can: taste, tuning, look, scope within the roadmap. It records each one where it belongs, marked as decided under the owner's delegation so the owner can overturn it, and builds on it. It asks only what it genuinely can't continue without: the owner's play, feel and sign-off, or something only the owner can provide (an account, a tool). When nothing is left but that, stop and hand over those questions, each answerable on its own, with where to look.
- **Converge.** Work toward the current stage's done bar and stop there; don't polish past it. A nitpick goes on its stage's list, not into the session.

## Read when… <a id="read-when"></a>

| You are about to… | Read |
| -- | -- |
| Pick up where the last session stopped | [docs/state.md](docs/state.md) |
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

## Writing docs <a id="doc-rules"></a>

- **Every instruction lives in this repository** <a id="no-memory"></a>. The owner works on several machines, so nothing may depend on one machine: never use the agent's own memory (it is off in `.claude/settings.json`) or any file outside the project. What is worth remembering goes into the doc it belongs to, in the same commit.
- **Every fact lives in one place.** Other places cite it; they never restate it. Before adding a sentence, look for where it already lives.
- **Code is described only in its docblocks** (`///` on C# types and members, the header comment of a shader, a script or a tool). Docs hold what code can't say: the owner's direction, rules and the reasons behind them, design intent, status, workflows. A doc may name a type as the place to start reading, never describe what it does or how.
- **Sections are cited by id, never by number or title.** A heading (or a list item or table row others cite) ends with a stable id written as an empty anchor, `<a id="…"></a>`, invisible once rendered, as every heading in [docs/principles.md](docs/principles.md) does. Cite it as `docs/principles.md#types-truth`, from docs and code alike (paths from the project root). Ids are unique across the project, keep their meaning when a heading is reworded or moved, and are never reused. Don't number sections or list items others refer to.
- **No history.** What was done, when and in which session is in `git log`. Docs say how things are; an owner decision keeps its date.
