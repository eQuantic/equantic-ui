# A board shows every pipeline in flight

Closes #1524, a sub-issue of #1504 (the pilot: three deliveries through the `pipeline` skill,
measured, and the decision to adopt it).

## Why

A delivery run through the `pipeline` skill writes `.claude/pipelines/<issue>-<slug>/state.json`:
the stages and their statuses, the agents' ids, and what the pilot measures (tokens, tool uses,
durations, started and ended, per stage and per round). Claude Code writes each agent's transcript
under `~/.claude/projects/`. Nothing shows either at a glance: the JSON is read by hand, and a
running agent says nothing until its stage ends. The capability missing is a delivery read at a
glance, the way a CI run view reads a workflow: the stage in progress, the agents working and what
each is doing, the cost of every step.

## What Changes

Nothing changes for a developer using the SDK: no assembly, package, setting or template moves, and
no browser or shell behaves differently. What changes is how a person running a delivery in this
repository sees it.

- A local, read-only board, `.claude/skills/pipeline/board/board.py`, Python's standard library
  only, served on `127.0.0.1` (port 8765, `--port`), with a page of plain HTML, CSS and JavaScript
  beside it in eQuantic UI's look. Its colours are served from `docs/design/tokens.json`, never
  copied.
- It lists every delivery of every worktree of the repository, running ones first, draws its
  pipeline (Intake, the stages of its state, Ship), marks the stage in progress, and gives the tokens
  and the time of every step, stage and delivery, read from the free-form `measures` by one rule.
- It shows the agents working now on each delivery, from Claude Code's own files: an agent not
  marked finished, in a session whose process is running, belonging to the delivery by its id in the
  state or by the branch its transcript records; what each is doing is the last tool its transcript
  records.
- It writes nothing, answers only on the loopback and only a request addressed to the loopback, and
  reads no file a request or a state names by path: a report is asked for by the delivery and the
  file's name, looked up in the delivery folder's own listing.
- The skill's state records the session running the delivery, through `${CLAUDE_SESSION_ID}`, which
  Claude Code substitutes in a project skill, and the moment its pull request merged, so a delivery
  links to its agents before the first id is written and its Ship can finish.
- `board.py --self-test` proves the reading, the attribution and the refusals over a fixture on
  disk and through the real server; CI's `roadmap` job runs it beside the hooks' self-tests.

No public surface and no developer surface moves: no `PublicAPI.Unshipped.txt`, nothing in
`tests/eQuantic.UI.Web.Tests/developer-surface.baseline.txt`. Nothing is removed. The board is not
written in eQuantic.UI, at the person's request.

## Capabilities

### Modified Capabilities

- `session-pipelines`: a delivery's state names its session and its merge, and a board reads every
  delivery, shows the agents working on it, and writes nothing.

## Impact

- `.claude/skills/pipeline/board/` (new): `board.py`, `index.html`, `board.css`, `board.js`,
  `README.md`.
- `.claude/skills/pipeline/SKILL.md`, section 2 (`session`, `merged`, the board) and section 7, step
  6 (record `merged`).
- `.github/workflows/ci.yml`: one step in the `roadmap` job.
- `CLAUDE.md`: one sentence in "Pipelines (a pilot, #1493)", outside the Workflow section, so
  `AGENTS.md` does not move.
- `docs/ledger/a-board-shows-every-pipeline-in-flight.md`.

eqc, the runtime, the web realizer, the Photon shells, the SDKs and the templates: none.

## Principles

It follows 2 (the capability, a delivery read at a glance, not this session's case), 9 (the
structural fix: a request names a report and never a path; the instrument that fails: a self-test
CI runs) and 10 (discover, never hardcode: the colours from the design tokens, the agents' ids found
by looking every string of the state up among the agents on disk instead of in a list of key names,
the steps by one rule over the shape of `measures`). It bends 1 and 4 where they do not reach: the
page is HTML, CSS and JavaScript and is not written once in eQuantic.UI, because it is the
repository's own tool, read by the people who build the SDK and never written by an app's
developer, and the person asked for it outside the SDK.
