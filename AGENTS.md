# The Deep — agent guide

A 2.5D side-scrolling **metroidvania** (Godot .NET, C#) about an android pilgrim descending toward a planet's core. The coding agent is the developer: it builds everything by editing text files and makes every technical and organizational choice. The owner plays, gives feedback and sets the direction. **Never ask the owner to use the Godot editor.**

## The goal <a id="goal"></a>

Make the game of [docs/vision.md](docs/vision.md), following the owner's plan: a prototype made of reusable bricks, then the real game's bigger map laid down on paper, then that game built from the bricks, then the details (`docs/roadmap.md#four-stages`). The work is the roadmap's current item. The docs exist only so the agent can do it.

Every task, rule and doc line answers to this goal. Before adding one, ask whether the next agent needs it to do the roadmap's work; if not, leave it out.

**This file is the only agent instruction file the harness loads** <a id="agents-file"></a>; its parts are in `instructions/`. Claude Code reads `AGENTS.md` only when no `CLAUDE.md`, `.claude/CLAUDE.md` or `CLAUDE.local.md` exists in the project or above it. Create none; if one appears above the project, tell the owner.

## Every session <a id="session"></a>

1. Read this file, then where the work stands (`docs/roadmap.md#status`) and the current item in [docs/roadmap.md](docs/roadmap.md).
2. Read only the doc sections the task needs (`AGENTS.md#read-when`).
3. For any visual change, capture before and after and look at the images (`docs/art-direction.md#judge-images`).
4. Run the checks (`AGENTS.md#checks`). Keep `godot --path .` working: it is the owner's launch command.
5. Update the roadmap (its `docs/roadmap.md#status` included) and `docs/bricks.md` as `AGENTS.md#doc-rules` says.
6. Hand over with **one command that reaches the new content with the abilities it needs**, and what to try there (`instructions/testing.md#playtest`). Never one that drops the owner somewhere they can't move.

## Orchestrating <a id="orchestrate"></a>

The main session plans and subagents build (owner), so the main session's context stays small across a long session.

- **The main session splits the current item into small tasks itself** and gives each to a fresh subagent: one deliverable each, done in one short sitting. Each split pays a cold start, so related small changes travel together; a problem whose cause is unknown is split into measuring it, finding its cause and fixing one cause. A visual task is one fix judged on one or two rooms; a subagent that nears 100k tokens of context was given too much (owner). Handing a whole item to one subagent is not orchestrating (owner).
- **A brief** names the task, the doc sections it needs (by id), the files in play, what done means, the captures to judge by and how many tries before it commits what is good and reports the rest. The subagent ends with the check its work needs (`AGENTS.md#checks`), commits only its own files as each step passes, and reports in a few lines: what changed, the commit, what is left, any question for the owner.
- **A subagent cut off** (a usage limit, a crash) is resumed when it was near the end or its context is small; otherwise a fresh one takes over from the working tree with a short brief of what is left, since resuming reloads its whole context.
- **Builders run one at a time.** Checks and captures see the whole tree, and captures need the GPU to themselves (`instructions/toolchain.md#gpu`). Read-only research may run beside a builder.
- **Work quicker to do than to explain** (a small fix, a question) stays in the main session: a subagent starts cold and must rebuild the context first.
- **The main session reads reports** and the docs it needs to decide, not code or logs. It looks at a capture only to judge a visual change.
- **Decide, ask only what blocks.** The agent decides taste, tuning, look and scope within the roadmap, and builds on it. It asks the owner only for what nobody else can give: play, feel, sign-off, an account or a tool. When nothing else is left, it stops and hands over those questions, each answerable on its own, with where to look.
- **Converge.** Work to the current item's done bar and stop there.

## Working principles <a id="working-principles"></a>

- **Checks** <a id="checks"></a>. The **fast check** (`tools/check.sh --fast`: formats, lints, builds) ends every turn that changed files; the **full check** (`tools/check.sh`) runs once when a piece of work that changed code or content is finished (`instructions/testing.md#check`); work that changed only docs or comments ends with the fast check, which already holds the citations, the formatting and the build. Formatting is the formatter's job, never done by hand. Never make a check pass by disabling a rule or skipping a check without writing down why.
- **Code** follows [instructions/principles.md](instructions/principles.md): the rules in a pure core, the engine as a thin layer, worlds as data, types that tell the truth.
- **Git** <a id="git"></a>. The agent commits each coherent, passing unit of work and pushes (`git@github.com:pmall/thedeep.git`). A commit message is plain git style (owner): a subject of 72 characters at most, in the imperative, naming what changes ("Add the hatch seam brick"); a blank line; then a short body wrapped at 72 columns saying why, in a few lines or a short list, never one long narrative sentence. It never commits secrets and never rewrites pushed history.
- **The owner's direction** is marked "(owner)" where it lives. The agent doesn't overturn it; when a request conflicts with it or with these principles, it says so and proposes a better way. Everything else is the agent's to change, the instructions included: a rule that costs the work more than it gives is rewritten, with the reason in the commit. When a technical choice touches the owner (cost, an account, a step only they can take), say so plainly.

## Read when… <a id="read-when"></a>

| You are about to… | Read |
| -- | -- |
| Pick up where the last session stopped | [docs/roadmap.md](docs/roadmap.md), from its first section (`docs/roadmap.md#status`) |
| Understand the game, its scope and setting | [docs/vision.md](docs/vision.md); its theme, for names, placeholder text and story beats: [docs/theme.md](docs/theme.md) |
| Write, move or review any code | [instructions/principles.md](instructions/principles.md); where code lives: [docs/architecture.md](docs/architecture.md) |
| Create or edit `.tscn` / `.tres` / C# Godot scripts or shaders | [instructions/godot-csharp.md](instructions/godot-csharp.md) |
| Add or change a reusable piece; know what can be placed in a room | [docs/bricks.md](docs/bricks.md) |
| Do any visual work | [docs/art-direction.md](docs/art-direction.md) |
| Touch movement, abilities, tuning, controls or the rules rooms are built to | [docs/gameplay.md](docs/gameplay.md) |
| Work on machines or the map's guarantees (any order, no softlock) | [docs/long-term.md](docs/long-term.md) |
| Run, test, capture screenshots, or hand over to the owner | [instructions/testing.md](instructions/testing.md) |
| Deal with versions, paths, the .NET environment, the GPU or upgrades | [instructions/toolchain.md](instructions/toolchain.md) |
| Build or change a room, an exit or a relic in the prototype | [worlds/prototype/map-draft.md](worlds/prototype/map-draft.md) (keep it in step with built rooms) |
| Change the sequel world (the check's second world) | [worlds/sequel/network.md](worlds/sequel/network.md) (keep it in step with its rooms) |

## Instructions, docs and notes <a id="doc-rules"></a>

What is written falls in three kinds, kept apart because each carries a different authority:

- **Instructions**: this file and `instructions/`, how work is done here. The owner's rules in them are marked "(owner)" and change only when the owner asks; the agent tunes the rest (`AGENTS.md#working-principles`). They never live in `docs/`.
- **Docs**: `docs/` and each world's design file under `worlds/`, the shared understanding of the project: what the game is, how its parts fit and where each starts, its invariants, the roadmap. They describe the present, rewritten rather than appended to. The agent keeps them true as the work goes; a change to the game's shape or direction needs the owner's agreement.
- **Notes**: plans, findings, dead ends, open questions. They stay in the agent's scratchpad outside the repository, never committed, and can be lost: a new session starts from the repository, so what deserves to last goes to a docblock, the commit message or `docs/`.

Docs drift when every session adds notes and none removes them. These rules keep them a tool for the next task.

- **A doc says how things are and what to do**, for the agent: the owner's direction, rules with their reason in a few words, design intent, the plan, workflows and pitfalls. Never what was done or when, how a rule came about, the owner's words quoted, or past reasoning: that is `git log`.
- **Status lives in one place**: the roadmap, whose first section (`docs/roadmap.md#status`) says in one screen the current item and step and what waits on the owner, each by citing the open item. A finished item is deleted from the roadmap; what exists is the code, and `docs/bricks.md` says how ready each brick is. Topic docs hold no status.
- **Owner feedback** becomes a rule where it belongs, or a roadmap item, in the agent's words. Once handled, it is gone.
- **A change of plan or direction deletes what it replaces** in the same commit: every doc it touches is rewritten so none contradicts it. Never stack the new rule on the old.
- **Every fact lives in one place.** Other places cite it; they never restate it. Before adding a sentence, look for where it already lives.
- **Code is described only in its docblocks** (`///` on C# types and members, the header comment of a shader, a script or a tool). A doc may name a type as the place to start reading, never describe what it does.
- **Sections are cited by id** <a id="doc-ids"></a>, so a section can be reworded or moved without touching what cites it. A heading, list item or table row others cite ends with an empty anchor, `<a id="…"></a>`, cited as `instructions/principles.md#types-truth` from docs and code alike (paths from the project root). Ids are unique across the project and never reused; `tools/docrefs.py`, in the full check, holds every citation to an id that exists.
- **Every instruction lives in this repository** <a id="no-memory"></a>, so every machine and every agent sees the same truth: never the agent's own memory (off in `.claude/settings.json`) or a file outside the project. When a doc's description and the code disagree, the code wins and the doc is fixed; a rule of `instructions/principles.md` wins over code that breaks it.
