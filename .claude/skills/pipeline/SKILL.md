---
name: pipeline
description: Deliver a change through its registered pipeline - the stages in order, the agent, model and effort of each, the gate and the shipping steps. Use when a request is a delivery (implement or add a feature, fix a bug, refactor, write or change documentation) or a review of a pull request or branch, and to resume a delivery whose state is under .claude/pipelines/.
argument-hint: "[issue number or the request] [high-effort]"
disable-model-invocation: true
---

This is a PILOT (#1493, measured in #1504): it runs only when a person invokes it, and it measures itself (section 2)
so that adopting it is decided on numbers. Until then a delivery nobody asked to pipe runs as the
Workflow says, without it.

You are the ORCHESTRATOR of a delivery. You decide, delegate and keep the state; the agents under
`.claude/agents/` read the code and write it. This skill is the order of the work and who does each
part. It restates no rule: what a delivery must satisfy is the Workflow section of `CLAUDE.md`,
`docs/PRINCIPLES.md` and `docs/PROVING.md`, and where this file and those differ, those win.

A person's explicit instruction wins over this file: name the conflict in one line and do as asked.

## 1. Select

Read `.claude/skills/pipeline/pipelines.md` and take the pipeline of the request's KIND: `feature`,
`bugfix`, `refactor`, `docs` or `review`. The kind is what the change is; the AREA it touches adds
duties and chooses the engineer (the tables below), and is never a pipeline of its own: a bug in the
transpiler is `bugfix` with the compiler's duties.

A request that is none of these (a question, a look at a log, a release, which is the owner's call)
runs no pipeline. Once selected, the whole pipeline runs: a stage may be added, never skipped. When
a stage does not apply, the state says why in one line.

**High effort.** Score the change on four questions. One "high" raises the stage it concerns one
step; two or more, or the person asking for it, make the whole delivery high-effort.

| Question | High when |
|---|---|
| How far does it reach? | a `PublicAPI.Unshipped.txt` or the developer surface baseline moves, both targets change, or ten files or more |
| Can it be taken back? | a vocabulary enum, the hydration contract, a package's shape, what a published tool reads |
| Has it been done here? | a mechanism with no row in `docs/FLUTTER-PARITY.md` and no sibling in the tree |
| Does it guard something? | server actions, the client boundary, content security, signing, publishing, a hook |

High effort means: the architect at `xhigh` and the engineer on `opus` whatever the area; the design
written as two competing designs before one is chosen; and a gate finding kept only when a second
reviewer, given the finding and asked to refute it, fails to. It never means the `fable` alias,
which bills separately, or the Workflow tool, unless the person asks for either.

## 2. State

One folder per delivery, `.claude/pipelines/<issue>-<slug>/` inside the delivery's own worktree
(section 3), which git ignores:

- `state.json`: the issue, the branch, the worktree's absolute path (and the wiki's, when there is
  one), the pipeline and whether it is high-effort with the reason,
  the OpenSpec change's name, the pull request, the STAGES COPIED from the registry (name, agent,
  status `pending|in_progress|completed|failed|not_applicable`, the agent's id for a resume, the
  report's path), the gate's round, Copilot's round, the last CI run and the commit it saw.
- in the same file, what the pilot MEASURES, written as it happens and never reconstructed: when
  each stage started and ended, the tokens each agent's answer reports, how many times the session
  was compacted, every defect found after the pull request opened (the round, or the queue's exit,
  that found it), and for a macOS run asked for on the branch, whether it caught something and
  whether the queue's own run would have caught it too. Every stop for the person, with what they
  decided. Every queue exit, with its cause. An agent's reported duration counts its wall clock,
  so it is only a measure when the agent left nothing running behind it.
- `<stage>.md`: each stage's report, written by its agent.

Write the state before the first stage and at every transition, and only through its own file:
another delivery's state is never touched. It exists so a compacted or a new
session resumes without rereading anything: read it first when one is there. It is a convenience of
one worktree. The truth is what was pushed: the issue and the board, the commits, the checkboxes of
the OpenSpec change's `tasks.md`, the pull request.

## 3. Intake, by you

As the Workflow's "Every change starts from an issue on the board" and "Branches" say. In order:
search for the issue before creating one; an issue assigned to someone else, labelled
`good first issue` or already In progress is that person's, so stop and say so; create a missing
one with `scripts/new-issue.py`; move it to In progress; `git fetch`, then the delivery's
worktree (below); run the `roadmap` skill. Set no git identity: on a laptop it is the person's own.

**Each delivery has a worktree of its own.** The branch is created with its tree, from
`origin/main`, in a folder beside the clone named after it:

```bash
git worktree add -b <type>/<slug> ../<clone>.wt/<issue>-<slug> origin/main
```

Everything of the delivery happens there: every agent's work, every build, the state. The clone's
own checkout is never switched, and no delivery works in another's tree, so several deliveries run
on one machine at once, from one session or from several: a branch switched under a running
delivery was one of the pilot's findings. A wiki page the delivery changes is written in a worktree
of the wiki's clone beside it, `../<clone>.wt/<issue>-<slug>.wiki`, on the branch of the same name.
Once the pull request is merged, both worktrees are removed (`git worktree remove`) and the branch
deleted. Before deleting anything, look at it: a tree that holds uncommitted work is reported, not
removed.

**One build at a time, for the whole host.** Every build and test suite of every delivery runs
through the host's lock, `python3 scripts/one-build-at-a-time.py -- <command>`, which waits while
another holds it. A person who runs their suites through a lock of their own names it once, and the
state records it in place of this one. Every proof command handed to an agent is given already
wrapped.

## 4. Stages

For each stage of the pipeline: mark it `in_progress`, spawn its agent, end your turn to collect,
read the few lines it answers, mark it `completed` or `failed`. A failed stage stops the pipeline:
report it with the report's path.

**What an agent is given**, and nothing else: the issue number; the delivery's worktree, as an
absolute path, where it runs every command (`cd <tree> && …`) and reads and writes every file;
the OpenSpec change's name, when there is one; what it may write (its write boundary); the report's
path; the proof commands of its area, wrapped in the host's lock; and, for a large change, the files
the design names as the starting points, so it does not spend its turns finding them. It answers in fifteen lines at most, with the report's path. Never paste a file, a test log
or a diff into a prompt or ask for one back: name the path.

**The engineer** is one agent, and the area's row gives its rules, model and effort, passed as the
Agent call's `model` and `effort`. It runs once per area, unless the design says the areas compile
together (a visitor's new member breaks every realizer until each handles it): then one engineer,
on the strongest of their rows, does them in order.

| Area | Rules it reads first | Model, effort | Proves with |
|---|---|---|---|
| compiler, generators, codegen | `.github/instructions/compiler.instructions.md` | opus, high | `tests/eQuantic.UI.Compiler.Tests`, and `tests/eQuantic.UI.Conformance.Tests` with `EQ_REQUIRE_JS=1` |
| runtime, web realizer, server | `.github/instructions/runtime.instructions.md` | sonnet, high | `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, `tests/eQuantic.UI.Web.Tests`, `tests/eQuantic.UI.Server.Tests` |
| vocabulary, components, charts | `.github/instructions/vocabulary.instructions.md` | sonnet, high | `tests/eQuantic.UI.Web.Tests` and `tests/eQuantic.UI.Native.Engine.Tests`: both targets |
| native track | `.github/instructions/native.instructions.md` | opus, high | `tests/eQuantic.UI.Native.Engine.Tests`; the Apple parts are the macOS run's |
| SDKs, tasks, templates, packaging, CI, scripts | `CLAUDE.md`: "What the developer's csproj says", "Development Workflow", "Package Architecture" | opus, high | the suites that hold the SDK's targets (`tests/eQuantic.UI.Web.Tests`, `tests/eQuantic.UI.Native.Engine.Tests`), a script's `--self-test`, and the consumer path of the wiki's BuildFlow page |

Run a test PROJECT, or a filter of one, never the whole solution: that is CI's, and it takes a
quarter of an hour here.

**One build at a time, on the host.** Reading in parallel is cheap; building is what loads the
machine, and other deliveries and sessions share it. Every build and suite goes through the host's
lock (section 3), so deliveries run side by side and their builds queue. Agents of one delivery
that run in parallel read, and run nothing.

**One writer in each tree.** Agents that only read run in parallel, in one message. Agents that
write run one at a time in their delivery's worktree, in the order the dependency gives: the
vocabulary before the realizers, a realizer's C# and its TypeScript twin by the same engineer.
Writers of DIFFERENT deliveries run at the same time, each in its own tree. The Agent tool's own
`isolation: worktree` is not used: it branches from the default branch, so it would not see the
delivery's proposal; the delivery's worktree is the isolation. Nobody but you commits or pushes.

**Deliveries in parallel.** A session may run several deliveries at once, each with its tree and
its state, when they touch different files: two deliveries that would edit the same file run one
after the other, since their merge is a conflict nobody reviewed. Each turn, act on whichever
delivery's agent answered, by its state.

**Nothing left running.** An agent ends its turn with nothing of its own running in the background:
no wait loop, no watch, no server. A loop it leaves behind keeps its turn's clock running and its
wall time stops measuring anything.

**Every commit is pushed as it lands.** A branch that exists only on one disk is lost with the
disk. Commit each green unit and push it at once: a push outside `main` starts no CI before the
pull request exists, so the backup is free.

**Whoever wrote it does not verify it.** The prover is never the engineer, and it runs alone: its
A/B takes the fix out of the tree for a while.

**A fix goes back to its author**, a message to its id from the state, since a new agent would
read everything again, UNLESS the author's context is already large (above about 200 k tokens by
its reports) and the fix is small and located: then a fresh engineer, given the finding's exact
`file:line`, costs a fraction of resuming it (60 k against 400 k in the pilot).

## 5. Duties the diff brings

The docs stage and the gate both read this table against `git diff origin/main...HEAD`.

| When the diff | It also carries |
|---|---|
| is a delivery at all | one entry under `docs/ledger/`, as `docs/ledger/README.md` describes it |
| adds or changes a public C# signature | the declaration, by the ENGINEER (`scripts/public-api.sh`, or the build fails on RS0016), and the migration line an upgrading app needs |
| adds a csproj property, a configuration section or a template parameter | `tests/eQuantic.UI.Web.Tests/developer-surface.baseline.txt`, regenerated behind its variable |
| adds an `EQ` diagnostic, from any tool | its entry in `docs/DIAGNOSTICS.md` and its rows on the wiki |
| regenerates a pin behind an `EQ_UPDATE_*` variable | a regenerated diff that is exactly what the change intends: whitespace only for a layout, a baseline that only shrinks |
| moves a type or renames a member | a look at what each pin still sees, and the citations `DocsIndexTests` holds |
| changes a file named in `assets/readme/captures.json` | the picture taken again and looked at, as the Workflow's "Documentation and the ledger" says. A target that needs a Mac stops the delivery on any other host: say so |
| changes `src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang` | the generated shaders, by `scripts/generate-shaders.sh` |
| changes what a wiki page describes | the page in every language it is written in, on the wiki's branch of the same name, in a WORKTREE of the wiki's clone with `EQ_WIKI_DIR` pointing at it for every guard. The shared clone stays on master |

## 6. Gate

After the stages, before the pull request. In one message, in parallel, all reading:

- the `code-review` skill at `high`, which the Workflow's "Pull requests", step 1, asks for;
- `preship-reviewer`;
- `security-reviewer`, when the diff reaches the server, the client boundary of the compiler
  (`CLAUDE.md`, "Compiler Boundaries"), the SDKs, packaging, a workflow or a hook.

Sort every finding as the Workflow sorts Copilot's: a DEFECT is fixed by its author and proved; a
hardening, a documentation gap or a nit is fixed in the same push or becomes an issue under the
delivery's parent; a WRONG finding is answered with what shows so. A security finding rated high is
never written in an issue or a pull request: it goes to a private advisory, as
`docs/SECURITY-REVIEW.md` says, and the delivery stops for the person.

**A fix is held to its finding.** In every delivery of the pilot, a fix of one round made the next
round's defect, and on a component the gate did not converge: each round's fixes widened the
change. So a fix changes only what its finding names. A hardening or a nit that would change
behaviour, add a public member or reach a file the change did not touch becomes an issue under the
delivery's parent, never a commit of the round. Every round after the first reviews only the
previous round's delta (`<commit before the round>..HEAD`), and says first whether that delta did
more than its findings asked.

Then `prover`, alone, over the final tree. A test it finds a plausible wrong version passing is a
defect of the proof: a fresh engineer strengthens the TEST only, and the prover runs again the
variants it had listed, nothing else.

Two rounds of the gate at most: a defect still standing after the second stops the delivery, with
the reports, for the person. The exception is a defect whose fix the finding states exactly and
which touches only the lines of the previous round's own fix (a word, a clause): it is fixed and
recorded without the stop.

## 7. Ship, by you

The steps are the Workflow's "Pull requests"; this is their order here.

1. The branch is already pushed, commit by commit. Push the wiki's branch of the same name when
   the delivery wrote one (CI reads it for the docs guards); open the pull request from
   `.github/pull_request_template.md`, closing the issue; read the body back; the issue moves to In
   review. Then confirm CI STARTED on the head: a pull request that conflicts with `main` runs no
   workflow at all and nothing says so (`mergeable` reads `CONFLICTING`). Merge `main` into the
   branch, in its worktree, and prove the merged tree before pushing it.
2. When the diff touches the native track, the SDKs, packaging, `Directory.Build.props` or a
   sample: ask for the macOS jobs on the branch now, since packing and the samples run only there.
   Those runners are the scarcest thing the queue has, so the pilot counts what this run buys:
   record in the state what it caught, and whether the queue's own run would have caught it.
3. Copilot's loop, after confirming the request registered (`requested_reviewers` lists it, or a
   review arrives): from an account whose request does not register, the round cannot happen, and
   the state says so. A person's review is sorted and answered the same way. A subagent reads the
   review threads and answers with the sorted findings, never the raw threads. Each round with a defect: the author fixes, the prover runs its A/B again, ONE
   push, the threads answered and resolved, the next round asked for.
4. Archive the OpenSpec change after the last round, so a scenario a round changed is edited once.
5. Queue the pull request. Wait for CI and for the merge in the background, never by polling in
   your own turns; a wait loop reads an empty or failed answer as "ask again", never as "left the
   queue". When the queue removes it, read the failed job's annotations and log before anything
   else, and sort the exit: INFRASTRUCTURE (a runner lost or never acquired), a FLAKE (a test this
   diff does not reach, failing for a reason of its own), or a DEFECT. The first two are queued
   again with the evidence in the state; a flake seen twice gets an issue; a defect goes back to
   its author.
6. Only once the pull request is MERGED: merge the wiki branch into the wiki's master, move the
   issue to Done, run the `roadmap` skill, and remove the delivery's worktrees (section 3). Being
   queued is not being merged: a queue discards.

## 8. Stop and ask

The issue is someone else's. A capture needs a Mac. A round's fixes did more than its findings
asked, and taking them back is not obvious. A security finding is high. The gate did not
converge in two rounds, or Copilot's third round still names a defect. The request needs a release.
A step needs a right this account does not have.
