# Design

## Context

The pilot's state is free-form: three delivered states differ in shape (a flat stage with tokens as
strings, `round1 → {preship, code_review, sorted…}`, `first_run`/`resumed_run`, a stage recorded in
`measures` and absent from `stages`). Claude Code writes, per subagent,
`<config>/projects/<project>/<session>/subagents/agent-<id>.jsonl` and `agent-<id>.meta.json`, and,
per running session, `<config>/sessions/<pid>.json`. `<config>` is `CLAUDE_CONFIG_DIR`, `~/.claude`
by default. None of these is a documented contract; what follows was read on this host, Claude Code
2.1.323.

No row of `docs/FLUTTER-PARITY.md` applies and none of Flutter's three trees is involved: this is
tooling. Its nearest relative is Flutter's DevTools, a page served on the loopback that reads a
running program's published state and changes nothing; the board takes that shape.

## Goals and non-goals

Goals: every delivery of every worktree; its pipeline with the stage in progress; the agents working
now with what each is doing; tokens and time per step, stage and delivery; read-only; standard
library only; eQuantic UI's look.

Non-goals: writing a state, starting or stopping an agent, reaching the network (GitHub is a link,
never a call), redefining what the pilot measures, and being written in eQuantic.UI.

## Decisions

### Where it lives: beside the skill

`.claude/skills/pipeline/board/`. The board is the pipeline skill's instrument, reads only that
skill's state, and goes with the pilot if #1504 drops it; a skill bundles scripts by design.
Measured: `scripts/ci-changes.sh` classes `.claude/skills/pipeline/board/board.py` as
`scope=docs` (`code=false`) and `tools/pipelines-board/board.py` as `scope=full`, which would run
the build, conformance, the Mac jobs and the samples for a Python file none of them builds. The
`roadmap` job, which always runs, holds the self-test either way.

### The session and the merge

`SKILL.md` section 2 asks for `session` (this session's id, written by `${CLAUDE_SESSION_ID}`, and
written again by a session that resumes the delivery) and `measures.merged` (section 7, step 6).
Read in the 2.1.323 binary, not executed: skill text loaded from `skills`, `plugin`, `managed`,
`bundled` and `commands_DEPRECATED` gets `${CLAUDE_SESSION_ID}` replaced; text from
`syncedSkills`, `mcp`, `memoryStore` and `attachedFolder` does not. Task 4.1 runs it. An earlier
session's agents stay linked through their ids, so `session` stays one string.

### The normalising rule

Under `measures.stages.<stage>`:

- an object carrying `tokens` or `duration_ms` is a STEP, named by its key path below the stage
  (`round1 › code_review`), or by the stage itself when the stage carries them; its other keys
  (`verdict`, `findings`, `outcome`) are its notes;
- an object with a step beneath it is a group, walked; an object with none beneath it is a NOTE at
  its path (`round1 › sorted`);
- a count (`tokens`, `tool_uses`, `duration_ms`) is an integer or a string of digits, never a
  boolean (`True` is an `int` in Python);
- a stage's tokens are the sum of its steps'; its time is `ended − started`, or `now − started`
  while it is in progress, and unknown otherwise;
- a stage in `measures.stages` and not in `stages` is drawn after the registry's stages, before Ship,
  and counted;
- a delivery's tokens are the sum of its stages'; its time runs from `measures.started` to the last
  `ended`, or to now while a stage is pending or in progress.

Measured over this host's states: #1445 1,214,486 tokens and 3,782 s, its Gate eight steps with
`round1 › sorted` and `round2 › sorted` as notes; #1522 481,630 tokens, Write 59,501, `Owner review
fix` 74,503 in `measures` only; #677 4,194,093 tokens and 44,898 s, its Gate twenty steps.

### Status and Ship

A delivery is `failed` when a stage failed, else `running` while a stage is pending or in progress,
else `shipping` until `measures.merged`, else `done`. A `review` pipeline has no Ship and is `done`
when its stages are. The Running tab shows `running`. Ship is completed with `merged`, waiting with
`pr_opened` or `queued` (its exits and the last CI listed), pending otherwise. A state written
before `merged` existed shows "merge not recorded": the board does not reconstruct a measure.

### Which agents are working

Measured: `finished` alone is not enough. Fifty metas on this host have none; forty-nine belong to
sessions that ended between 30 and 286 hours ago. The session registry holds a file per running
session only (three files, three live sessions), and its `procStart` equals field 22 of
`/proc/<pid>/stat` on all three. So an agent is working when its meta has no `finished` and its
session is in the registry with a live pid, of the same start time where `/proc` exists. With no
registry, liveness is shown as unknown, never hidden. The `.key` files beside the registry's hold a
peer token: the board reads only `<digits>.json` and serves only `live` and `status`.

Measured: one session ran #1445, #677 and #1522 in turn, so the session alone cannot attribute;
every transcript record carries `gitBranch`. A delivery's sessions are `state.session` and the
session folders of every string in the state that is an agent id on disk; a working agent of one of
them belongs to the delivery when its id is in the state or its transcript's last `gitBranch` is the
state's `branch`. Its stage is the stage entry that holds its id, else the stage in progress.

### Reading a transcript cheaply

Only a working agent's transcript is read; a finished one costs its meta. The tail: seek to the end
minus 256 KiB, drop the partial first line, parse backwards for the last assistant `usage`, the last
`tool_use`, the last `timestamp`, `gitBranch` and `message.model`; widen four times over while one
is missing, up to the whole file. The first record's timestamp, by `readline()`, is when it started.
Measured: the longest line is 151,748 bytes, so 64 KB can hold none; 256 KiB held both in all 33
transcripts of this project (the largest 5.8 MB).

Context now is `input + cache_read + cache_creation + output` of the last usage. Measured: it is
close to, not equal to, the Agent tool's reported total (83,596 against 84,430; 78,777 against
78,999), so it is labelled "context now" and never enters a recorded sum. The step is the tool and
its telling argument (a Bash call's `description`, else its `command`; a file tool's path relative
to the worktree; a search's pattern), cut at 120 characters.

### No value from outside reaches the file system as a path

Each is a key looked up in a listing the board made itself: worktrees from
`git worktree list --porcelain`, delivery folders from `os.listdir`, agents from a glob of file
names (`agent-<id>.meta.json`), sessions from the registry, a report's name from the delivery
folder's listing. A report is `GET /api/report/<delivery>/<file>`, served when `<file>` is in that
listing, ends in `.md` or is `state.json`, is a regular file, and its real path is inside the folder
(a symbolic link out is refused). Anything else is 404.

### The server

`ThreadingHTTPServer` on `127.0.0.1` only. A request whose `Host` is not `127.0.0.1:<port>` or
`localhost:<port>` is 403 whatever the path, which closes DNS rebinding. GET only. Static files come
from a fixed map: `/` (`index.html`), `/board.css`, `/board.js`, `/tokens.css`, `/logo-dark.svg`
and `/logo-light.svg` (the repository's `assets/logo-equantic-ui-white.svg` and
`assets/logo-equantic-ui.svg`). Every answer carries `Content-Security-Policy: default-src 'none';
script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; base-uri 'none';
form-action 'none'; frame-ancestors 'none'` and `X-Content-Type-Options: nosniff`, which is why the
page is three files and not one. `/api/pipelines` returns the normalised deliveries, their agents
and the server's `now`. Python 3.9 or later: timestamps are parsed with their `Z` replaced.

### The page

`/tokens.css` is written per request from `docs/design/tokens.json`: every leaf with `light` and
`dark` becomes `--<path below color, kebab-case>: light-dark(<light>, <dark>)`, the idiom the web
realizer already uses, under `color-scheme: light dark`. No hex value is copied. Layout: KPI tiles
(deliveries running, agents working now, tokens, wall time) over the tab's deliveries; tabs Running
and All; the list, running first; the detail: the graph (Intake, stages, Ship, each with its status
in text and icon, its time and tokens, the high-effort badge and its reason), the agents working now
(type, description, model and effort, context now, step, stage, running for, last activity), the
steps of the selected stage (by default the one in progress) with a total row and its report, the
notes (`pilot_findings`, `defects_after_pr`, `queue_exits`) and every report of the folder,
`state.json` among them, shown in a `<pre>`. Every text is set with `textContent`; an issue or pull
request link is built only from an integer and the `origin` remote parsed as
`https://github.com/<owner>/<repo>`. It polls every 3 s and pauses while the tab is hidden; a
spinner stops under `prefers-reduced-motion`.

### The self-test's fixture

`--self-test` builds it in a temporary folder and runs the real code over it, the server included,
on port 0:

- a repository with an empty commit and a second worktree on `feat/two` (the hook's own self-test
  does the same with `git -c user.name=t -c user.email=t@t`);
- `1-flat`: tokens and durations as strings, a Gate with two rounds, `sorted` notes and a `prover`,
  `pr_opened`, `queued` and a queue exit, no `merged`;
- `2-nested`: integer counts, a stage only in `measures`, `merged`;
- `3-running` in the second worktree: `session` set, one stage completed with `first_run` and
  `resumed_run`, one in progress with `started` only, one pending, a step with `"tokens": true`;
- `4-failed`: a failed stage; `5-broken`: a `state.json` that is not JSON;
- in a delivery folder: `design.md`, and `leak.md`, a symbolic link to a file outside holding a
  marker;
- a config folder: a live session (this process's pid, its start time from `/proc` where it
  exists) with agent B (unfinished, its id in `3-running`'s stage, a first record, then a line
  longer than 256 KiB, then a usage and a `Bash` tool use), agent C (unfinished, unlisted, branch
  `feat/two`) and agent D (unfinished, unlisted, another branch); a dead session (the pid of a child
  already reaped) with agent E (unfinished, listed in `3-running`); a finished agent A listed in
  `1-flat`; a `.key` file beside the registry holding a marker.

It asserts the sums, steps, notes, statuses and Ship of each state; B and C working on `3-running`
(B on its stage, C on the stage in progress), D and E on none; B's step, context and start; the
broken state listed with its error; `/api/report` for `design.md` 200, for `leak.md`,
`..%2Fstate.json`, an unknown delivery and an unknown file 404; `Host: evil.example` 403 on every
path; neither marker in any answer; `/tokens.css` holding the background's `light-dark()` as
`docs/design/tokens.json` gives it; the CSP on every answer.

## Risks

- Claude Code's files can change shape in any release. That breaks the live panel, not the
  deliveries; the fixture pins today's shapes and an unknown shape shows as unknown.
- The pilot's tokens are each answer's reported figure, which for a resumed agent includes its
  earlier context (#677's engineer: 276,544, then 312,424, 363,194, 392,437). The board sums what
  was recorded and redefines nothing; whether the pilot should measure consumption is #1504's call.
