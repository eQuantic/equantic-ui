# The board's Roadmap: real dates and a measured forecast

The Roadmap view of the [board](https://github.com/orgs/eQuantic/projects/11) shows what happened and
what is likely to happen, never a plan drawn once and left to age. `scripts/roadmap.py` computes it
from the board and writes it back, and the Workflow section of `CLAUDE.md` says when to run it:
whenever the work moves. Without `--apply` it only reports. `scripts/roadmap_model.py` is the
arithmetic below in code, held by `scripts/roadmap_model_test.py`, which CI runs.

```bash
python3 scripts/roadmap.py           # the report: what would change, and the forecast behind it
python3 scripts/roadmap.py --apply   # writes it to the board
```

## Points

Work is counted in points, in the board's `Estimate` field: 1, 2, 3, 5, 8 or 13. An issue bigger than
13 is a parent, split into slices that carry their own points, and the report lists every open issue
pointed past 13 until it is. The points live on the issue a pull request closes; a parent's are its
sub-issues'.

A new issue is filed with its points, given against what was delivered before it:

| Points | Lines a person wrote | Delivered examples |
|---|---|---|
| 1 | under 200 | a release's bump (#1023), a sorted dictionary's `Contains` (#928) |
| 2 | under 400 | `EmailRenderer` takes the message's language (#942), the embedded bun moves a version (#854) |
| 3 | under 800 | deep links on the phones (#777), the 1.0 contract names its assemblies (#899) |
| 5 | under 1,600 | the phones' clipboard (#776), the code editor's snippets (#1013), a ledger entry is a file (#892) |
| 8 | under 3,200 | passkeys in the browser (#1017), multiple cursors (#1012), the keymap is data (#1010) |
| 13 | under 6,400 | a theme ships its faces (#1055), the cell editor (#983), server events on a typed topic (#638) |

The points are an estimate made before the work, and are not corrected after it: what the forecast
divides by must look like the estimates still ahead.

A delivered issue nobody pointed gets the points its size says, from the column above: the lines a
person wrote in the merged pull requests that closed it, a pull request's lines shared equally among
the issues it closes. Lines a tool writes are not work and are left out: the public API analyzer's
`PublicAPI.*.txt`, an icon pack's generated `*Icons.cs`, the pins regenerated behind an environment
variable (`*.fixture.json`, `*.baseline.txt`, `__transpiled__/`, `Goldens/`), `docs/LEDGER.md`, which
the release folds, the imported design handoff and the lockfiles. The scale doubles at each step, as
Fibonacci roughly does, and its middle is where the median delivered issue fell: that is how the
board's first six weeks were pointed on 2026-10-25, and how an issue delivered without points is
pointed since. Past 6,400 lines history says 21. On an open issue, 21 says it is too big for one
pull request and is split before it starts; the report lists it until it is.

## The pace

The pace is the points delivered over the last 28 days, per day. The days are calendar days, because
the weekend is when most pull requests merge here, and the window is never divided by fewer than
seven, so a burst in the board's first days does not promise the rest. The same window says how the
pace was shared among the epics.

## What is left

The work left is every open issue with no open sub-issue, at its points, or at the mean its type
delivered while nobody has pointed it. An open parent whose sub-issues are all closed is work of its
own: a sub-issue on this board is not always a slice of its parent, since a feature also collects the
defects found around it, and those can all be closed while the feature is not done. The report lists
those parents, and a person closes the ones that are done. The script never closes an issue.

## The plan

Each epic is a lane, and the issues no epic holds share a lane of their own.

- The lanes that delivered in the window work at the share of the pace they had. A lane that runs out
  of work hands its place to the next lane waiting, the one holding the earliest milestone's work
  first, then by the epic's priority and its number; with none waiting, the lanes still working share
  it.
- Inside a lane the work goes depth first, one issue at a time: among siblings, what has started,
  then what carries the earliest milestone, then priority, then the issue number.

Epics and features get dates. What started has its real start: the earlier of its first pull
request (its first commit, or its opening) and the first day the board moved it to In progress, read
from the issue's history so a later move to In review does not date it again, else the day it closed. What was
delivered has its real target, the day it or its last sub-issue closed. The rest takes the plan's
dates. An epic, a feature or a user story shows In progress once a sub-issue is in progress, or once
some of its work is done and some is open, and a closed issue shows Done.

## Sprints

The board's Iteration field holds the sprints: two weeks each, every other Tuesday, on the grid of
the board's first iteration (2026-09-15). The project is older than its board, so the grid reaches back
to the sprint holding the repository's first day, 2026-01-22, which is Sprint 1, and forward as far as
the plan reaches. The script names them Sprint N in that order.
They are buckets the plan fills, not commitments. A delivered issue sits in the sprint it closed in,
an open one in the sprint the plan finishes it in, and it moves when the plan moves. A parent whose
work is open below it spans sprints, so its pieces are what a sprint holds. The report says what each
past sprint delivered and what each coming one is planned to hold, in points. Until mid-August 2026
the work went to main as commits, with no pull request and no issue, so points cannot measure it: the
report also gives each sprint the lines a person wrote on main, from the repository's history with
the files a tool writes left out, which every sprint of the project has. The pace the forecast divides
by is the board's issues alone.

## Milestones

A milestone waits for its open issues and their sub-issues, and the report gives it three dates:

- **the plan's**, where the lanes above finish its last issue;
- **a range**, from a thousand runs of the same plan with what is uncertain drawn from the board:
  each day's capacity from the days of the window, and each issue's cost from what its type cost
  when delivered, scaled to its points when a person gave it some. Half the runs finish by the first date, 85% by the second;
- **focused**, the day it would land with all the pace and nothing else. It sets aside the order
  between its issues, a slice that needs the one before it included, so it is a bound, never a plan.

When the milestone has a due date, the report says whether the plan passes it.

## What it does not know

- **New work.** The forecast counts the board as it stands, and the board grows: the report says how
  many issues and points it gained over the same window. A milestone whose scope grows lands later
  than its date says.
- **Work that is not on the board.** A feature whose slices are not filed counts only what is. That
  is why a plan's slices go on the board as sub-issues, and why an issue past 13 points is split.
- **Order between issues.** A dependency or a decision someone owes is invisible to it: an issue
  waiting on one is forecast as if it were not.
- **Its own youth.** The first weeks of a board move the pace a lot, and so the dates.
