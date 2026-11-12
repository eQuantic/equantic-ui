---
name: pipeline
description: Deliver a change through its registered pipeline - the stages in order, the agent, model and effort of each, the gate and the shipping steps. Use when a request is a delivery (implement or add a feature, fix a bug, refactor, write or change documentation) or a review of a pull request or branch, and to resume a delivery whose state is under .claude/pipelines/.
argument-hint: "[issue number or the request] [high-effort]"
---

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

One folder per delivery, `.claude/pipelines/<issue>-<slug>/`, which git ignores:

- `state.json`: the issue, the branch, the pipeline and whether it is high-effort with the reason,
  the OpenSpec change's name, the pull request, the STAGES COPIED from the registry (name, agent,
  status `pending|in_progress|completed|failed|not_applicable`, the agent's id for a resume, the
  report's path), the gate's round, Copilot's round, the last CI run and the commit it saw.
- `<stage>.md`: each stage's report, written by its agent.

Write the state before the first stage and at every transition. It exists so a compacted or a new
session resumes without rereading anything: read it first when one is there. It is a convenience of
one checkout. The truth is what was pushed: the issue and the board, the commits, the checkboxes of
the OpenSpec change's `tasks.md`, the pull request.

## 3. Intake, by you

As the Workflow's "Every change starts from an issue on the board" and "Branches" say. In order:
search for the issue before creating one; an issue assigned to someone else, labelled
`good first issue` or already In progress is that person's, so stop and say so; create a missing
one with `scripts/new-issue.py`; move it to In progress; `git fetch`, then the branch from
`origin/main`; run the `roadmap` skill. Set no git identity: on a laptop it is the person's own.

## 4. Stages

For each stage of the pipeline: mark it `in_progress`, spawn its agent, end your turn to collect,
read the few lines it answers, mark it `completed` or `failed`. A failed stage stops the pipeline:
report it with the report's path.

**What an agent is given**, and nothing else: the issue number; the OpenSpec change's name, when
there is one; what it may write (its write boundary); the report's path; the proof commands of its
area. It answers in fifteen lines at most, with the report's path. Never paste a file, a test log
or a diff into a prompt or ask for one back: name the path.

**The engineer** is one agent, and the area's row gives its rules, model and effort, passed as the
Agent call's `model` and `effort`:

| Area | Rules it reads first | Model, effort | Proves with |
|---|---|---|---|
| compiler, generators, codegen | `.github/instructions/compiler.instructions.md` | opus, high | `tests/eQuantic.UI.Compiler.Tests`, and `tests/eQuantic.UI.Conformance.Tests` with `EQ_REQUIRE_JS=1` |
| runtime, web realizer, server | `.github/instructions/runtime.instructions.md` | sonnet, high | `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, `tests/eQuantic.UI.Web.Tests`, `tests/eQuantic.UI.Server.Tests` |
| vocabulary, components, charts | `.github/instructions/vocabulary.instructions.md` | sonnet, high | `tests/eQuantic.UI.Web.Tests` and `tests/eQuantic.UI.Native.Engine.Tests`: both targets |
| native track | `.github/instructions/native.instructions.md` | opus, high | `tests/eQuantic.UI.Native.Engine.Tests`; the Apple parts are the macOS run's |
| SDKs, tasks, templates, packaging, CI, scripts | `CLAUDE.md`: "What the developer's csproj says", "Development Workflow", "Package Architecture" | opus, high | the suites that hold the SDK's targets (`tests/eQuantic.UI.Web.Tests`, `tests/eQuantic.UI.Native.Engine.Tests`), a script's `--self-test`, and the consumer path of the wiki's BuildFlow page |

Run a test PROJECT, or a filter of one, never the whole solution: that is CI's, and it takes a
quarter of an hour here.

**One writer in the tree.** Agents that only read run in parallel, in one message. Agents that
write run one at a time, in the session's own tree, in the order the dependency gives: the
vocabulary before the realizers, a realizer's C# and its TypeScript twin by the same engineer. No
agent is isolated in a worktree: one starts from the default branch, so it would not see the
proposal, and the wiki guards pass in silence inside one. Nobody but you commits or pushes.

**Whoever wrote it does not verify it.** The prover is never the engineer, and it runs alone: its
A/B takes the fix out of the tree for a while.

**A fix goes back to its author.** Send the finding to the engineer that wrote the code (a message
to its id, from the state) and do not spawn a new one, which would read everything again.

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

Then `prover`, alone, over the final tree. Two rounds of the gate at most: a defect still standing
after the second stops the delivery, with the reports, for the person.

## 7. Ship, by you

The steps are the Workflow's "Pull requests"; this is their order here.

1. Push; open the pull request from `.github/pull_request_template.md`, closing the issue; read the
   body back; the issue moves to In review.
2. When the diff touches the native track, the SDKs, packaging, `Directory.Build.props` or a
   sample: ask for the macOS jobs on the branch now, since packing and the samples run only there.
3. Copilot's loop. A subagent reads the review threads and answers with the sorted findings, never
   the raw threads. Each round with a defect: the author fixes, the prover runs its A/B again, ONE
   push, the threads answered and resolved, the next round asked for.
4. Archive the OpenSpec change after the last round, so a scenario a round changed is edited once.
5. Queue the pull request. Wait for CI and for the merge in the background, never by polling in
   your own turns.
6. Only once the pull request is MERGED: merge the wiki branch into the wiki's master, move the
   issue to Done, run the `roadmap` skill. Being queued is not being merged: a queue discards.

## 8. Stop and ask

The issue is someone else's. A capture needs a Mac. A security finding is high. The gate did not
converge in two rounds, or Copilot's third round still names a defect. The request needs a release.
A step needs a right this account does not have.
