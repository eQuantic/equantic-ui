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

A new issue is filed with its points, given against what was delivered before it, and is created with
`scripts/new-issue.py`, which refuses one that is not whole (see "What an issue needs" below):

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
seven, so a burst in the board's first days does not promise the rest. On 2026-11-11, after three
days that delivered 258 points, the window was measured against shorter ones and kept: 7 days gave
39 points a day, 14 gave 30, 28 gave 31. The same window says how the pace was shared among the
epics, which the report shows beside each lane as what the sessions did; the plan does not divide
by it (see below).

## What is left

The work left is every open issue with no open sub-issue, at its points, or at the mean its type
delivered while nobody has pointed it. An open parent whose sub-issues are all closed is work of its
own: a sub-issue on this board is not always a slice of its parent, since a feature also collects the
defects found around it, and those can all be closed while the feature is not done. The report lists
those parents, and a person closes the ones that are done. The script never closes an issue.

## The plan

Each epic is a lane, and the issues no epic holds share a lane of their own. The pace goes where the
board says the work matters, not where it went before:

- A lane ranks by the issue it works next: first by the milestone that issue's work belongs to, the
  earliest due date first, then by its epic's **Priority** (the organization's issue field: Urgent,
  High, Medium, Low; an epic with none, and the lane of the issues no epic holds, after Low).
- Each day the lanes that rank first take the whole pace and share it equally. A lane that runs out of
  work, or whose next issue ranks lower, leaves the rest of its part to the lanes that rank next, the
  same day. So a milestone's work comes before anything else, then the highest priority's, and a
  lower priority is worked only when nothing above it is left.
- Inside a lane the work goes depth first, one issue at a time: among siblings, what carries the
  earliest milestone, then what has started, then priority, then the issue number. A milestone with a
  date is a promise, so its work goes ahead of work begun outside it.

Until 2026-11-11 each lane worked at the share of the pace it delivered over the window. That plans
the past: when the priorities change, every session moves to the new work and the plan keeps giving
it the old share. On that day all of them were on 1.0 and the bug sweep, and the plan gave 1.0 18%
of the pace, which put it on 2026-12-03 against 2026-11-15 with the pace on it.

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
an open one shown In progress or In review in the sprint holding today, since someone is on it now,
and any other open one in the sprint the plan finishes it in; it moves when the plan moves. The
report lists the issues shown under way for a week or more with no pull request, which the current
sprint holds although nobody may be on them, for a person to move back or leave. A parent whose
work is open below it spans sprints, so its pieces are what a sprint holds. The report says what each
past sprint delivered and what each coming one is planned to hold, in points. Until mid-August 2026
the work went to main as commits, with no pull request and no issue, so points cannot measure it: the
report also gives each sprint the lines a person wrote on main, from the repository's history with
the files a tool writes left out, which every sprint of the project has. The pace the forecast divides
by is the board's issues alone.

## What an issue needs

The plan counts an issue by its type, the epic above it and its points, and gives a lane its rank by
the epic's priority, so an issue is created with all of them or not at all:

- a type: Epic, Feature, User Story, Task or Bug;
- an epic: its Priority, and nothing above it;
- every other issue: an open parent under an epic, and its points, 1, 2, 3, 5, 8 or 13. Work bigger
  than 13 is a feature or a story created with `--split`, which carries no points of its own, and its
  slices, created next with it as their parent.

`scripts/new-issue.py` creates an issue with its type, parent, milestone, assignee and priority in
one request, puts it on the board with its points and status, reads it back, and fails naming
whatever did not land; it refuses, before any request, an issue that lacks one of them. In a session,
a hook (`.claude/hooks/issues-are-born-whole.py`) refuses the other ways of creating an issue and
names the script. An issue created in the browser reaches no hook, so the report lists every open
issue the plan cannot count as it is, with what it lacks.

```bash
python3 scripts/new-issue.py --type Task --parent 160 --points 5 --title 'What it delivers' --body-file body.md
python3 scripts/new-issue.py --type Epic --priority High --title 'What it is for' --body-file body.md
```

## Milestones

A milestone waits for its open issues and their sub-issues, and the report gives it three dates:

- **the plan's**, where the lanes above finish its last issue;
- **a range**, from a thousand runs of the same plan with what is uncertain drawn from the board:
  each day's capacity from the days of the window, and each issue's cost from what its type cost
  when delivered, scaled to its points when a person gave it some. Half the runs finish by the first date, 85% by the second;
- **focused**, the day it would land with all the pace and nothing else. It sets aside the order
  between its issues, a slice that needs the one before it included, so it is a bound, never a plan.
  The plan's date reaches it when the milestone's work ranks first, as the earliest milestone's does.

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
