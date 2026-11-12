# The registry of pipelines

One pipeline per KIND of change. Copy the stages of the selected one into the delivery's
`state.json`. Every pipeline starts with Intake and, except `review`, ends with Ship, both done by
the orchestrator as `SKILL.md` says; the stages between them are these. "Readers" may run in
parallel; a stage that writes runs alone.

## feature

Triggers: implement, add, build, support, a new component, a new construct, an issue of type
Feature, User Story or Task that adds behaviour.

| # | Stage | Agent | Writes | Done when |
|---|---|---|---|---|
| 1 | Design and propose | `architect` | the OpenSpec change | the change validates strictly, and names the parts it reaches, the surface that moves and the order of the work |
| 2 | Implement | `engineer`, once per area, in the design's order | source, tests, the surface declarations | the area's proof commands pass, and each task of `tasks.md` is checked |
| 3 | Documentation | `docs-writer` | the duties of the diff | the docs guards pass |
| 4 | Gate | the reviewers, then `prover` | reports only | no defect stands |

A component: stage 1 answers how Flutter solves it and which words the vocabulary takes, and stage
2 runs the vocabulary first, then the web realizer with its TypeScript twin, then Photon, with
both targets driven before it is done. A translation rule: stage 1 MEASURES what .NET answers and
writes it as the scenario; stage 2 is a strategy on the IR with a conformance case run on both
sides.

## bugfix

Triggers: fix, bug, broken, regression, wrong output, an issue of type Bug.

| # | Stage | Agent | Writes | Done when |
|---|---|---|---|---|
| 1 | Diagnose | `debugger`, one per hypothesis when there are several, in parallel as readers | a failing test, and nothing else | the root cause is named with its evidence, and a test fails for that reason |
| 2 | Propose | `architect` | the OpenSpec change | the scenario the fix makes true is written, as small as the fix |
| 3 | Fix | `engineer` | the structural fix, at the shared place | the failing test passes, and every caller of what changed was read |
| 4 | Documentation | `docs-writer` | the duties of the diff | the docs guards pass |
| 5 | Gate | the reviewers, then `prover` | reports only | the test fails without the fix AND with the most plausible wrong one |

Several debuggers write nothing while they run in parallel: the test is written by the one whose
hypothesis held, afterwards. A gap in the SDK found on the way is fixed or filed, never worked
around (`docs/PRINCIPLES.md`, 14).

## refactor

Triggers: refactor, restructure, split, move, migrate onto, rename, an issue whose change keeps
behaviour.

| # | Stage | Agent | Writes | Done when |
|---|---|---|---|---|
| 1 | Reach and target | `architect` | the OpenSpec change | what moves, in which order, and whether a surface file moves is written |
| 2 | Implement | `engineer`, one module at a time | source and tests | every pin holds byte for byte, or its regenerated diff is exactly the intended one |
| 3 | Documentation | `docs-writer` | citations, the ledger, the duties of the diff | the docs guards pass |
| 4 | Gate | the reviewers, then `prover` | reports only | nothing a pin scanned before is out of its sight now |

The change sets `skip_specs: true` only when neither `PublicAPI.Unshipped.txt` nor the developer
surface baseline moves. When one does, it is a break of the preview: the proposal names what goes,
what replaces it and the migration line. No old shape is kept beside the new one.

## docs

Triggers: document, write the page, update the wiki, translate, a change of Markdown alone.

| # | Stage | Agent | Writes | Done when |
|---|---|---|---|---|
| 1 | Write | `docs-writer` | Markdown here, the wiki's pages in every language | the docs guards pass, the wiki's read from the branch's worktree |
| 2 | Gate | the `code-review` skill at `high`, and `preship-reviewer` | reports only | no defect stands |

No OpenSpec change when no behaviour changes. A page that describes behaviour nobody implemented is
not a docs change: it is a `feature`.

## review

Triggers: review this pull request, review this branch, check this change. Writes no code and
pushes to no branch that is not this delivery's.

| # | Stage | Agent | Writes | Done when |
|---|---|---|---|---|
| 1 | Read | `preship-reviewer`, `security-reviewer` when the diff asks for it, `architect` for the design's fit, in parallel | reports only | each answered |
| 2 | Prove | `prover`, in a worktree of the branch under review | nothing that is pushed | the proof the pull request claims was run |
| 3 | One report | the orchestrator | the report, for the person | findings sorted as defect, hardening or wrong, each with its evidence |

## Not in the registry yet

A release, which is the owner's call and has its own steps in `CLAUDE.md`, "Version Management".
A security audit of the whole SDK, which `docs/SECURITY-REVIEW.md` describes.
