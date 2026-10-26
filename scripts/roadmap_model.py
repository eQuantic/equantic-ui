"""The roadmap's arithmetic: what the board says happened, what is left, and when it is likely to land.

Pure: it takes Issue values and answers with plans, so its tests need no network. scripts/roadmap.py
reads the board into these values and writes the answer back, and docs/ROADMAP-FORECAST.md says why
each rule below is the rule.

Work is counted in points, the Fibonacci numbers of the board's Estimate field. An open issue carries
the points a person gave it; a delivered one nobody pointed gets the points the size of what it
delivered says (`points_for`), so the pace is always measured in the unit the plan is counted in.
"""
from __future__ import annotations

import datetime as dt
import random
import re
from collections import defaultdict
from dataclasses import dataclass, field, replace

# The pace is what the last four weeks delivered, in calendar days, since the weekend is when most
# pull requests merge here, and it is never divided by fewer than seven: a burst in the first days of
# the board must not promise the rest of it.
WINDOW_DAYS, MIN_WINDOW_DAYS = 28, 7
# What a forecast that has not finished by then reports as beyond reach rather than as a date.
HORIZON_DAYS = 3 * 365
STARTED = ("In progress", "In review")
PRIORITIES = ("Urgent", "High", "Medium", "Low")
# The levels the Roadmap view draws: an epic and its features. A story, a task or a bug moves its
# feature's bar, and dated on its own it would only add noise to every issue's history.
DATED = ("Epic", "Feature")
# The levels whose status the board reads from their sub-issues.
ROLLED_UP = ("Epic", "Feature", "User Story")
# The lane of the issues no epic holds.
NO_EPIC = 0
# A sprint's length, as the board's Iteration field has had it since its first sprint.
SPRINT_DAYS = 14
# The points a person may give an issue. Bigger than the last is a parent, split into slices.
POINTS = (1, 2, 3, 5, 8, 13)
# The size of delivered work, in lines a person wrote, below which each point applies: the scale
# doubles at each step, as Fibonacci roughly does, and its middle (3 points, under 800 lines) is where
# the median delivered issue of the board's first six weeks fell. Past the last step history says 21.
SCALE = ((200, 1), (400, 2), (800, 3), (1600, 5), (3200, 8), (6400, 13))
BEYOND_SCALE = 21
# Files a tool writes, whose lines say nothing about the work: the public API analyzer's declarations,
# an icon pack's generated catalog, pins regenerated behind an environment variable, the ledger the
# release folds, the imported design handoff and lockfiles.
GENERATED = tuple(re.compile(p) for p in (
    r"(^|/)PublicAPI\.(Shipped|Unshipped)\.txt$",
    r"^src/[^/]+/[^/]*Icons\.cs$",
    r"\.fixture\.json$", r"\.baseline\.txt$", r"\.verified\.",
    r"/__transpiled__/", r"/Goldens/",
    r"^docs/LEDGER\.md$",
    r"^docs/design/.*\.html$",
    r"(^|/)(bun\.lock|package-lock\.json|[^/]*\.lock\.json)$",
))


@dataclass(frozen=True)
class PullRequest:
    number: int
    opened: dt.date
    merged: dt.date | None
    # How many issues it closes, on the board or not: its work is shared among them.
    closes: int
    # The lines a person wrote in it, when they were read.
    written: int | None = None


@dataclass
class Issue:
    number: int
    title: str
    type: str
    opened: dt.date | None = None
    closed: bool = False
    completed: bool = False
    closed_on: dt.date | None = None
    status: str | None = None
    status_on: dt.date | None = None
    # The first day the board moved it to In progress or In review: the status's own date is its
    # latest change, so a move from In progress to In review would date the start again.
    moved: dt.date | None = None
    milestone: str | None = None
    priority: str | None = None
    points: float | None = None
    sprint: str | None = None
    sprint_id: str | None = None
    start: dt.date | None = None
    target: dt.date | None = None
    parent: int | None = None
    pulls: list = field(default_factory=list)
    typed: bool = True
    id: str = ""
    item: str | None = None
    children: list = field(default_factory=list)


@dataclass(frozen=True)
class Change:
    issue: Issue
    kind: str
    old: object
    new: object
    why: str


# ── The tree ─────────────────────────────────────────────────────────────────────────────────────────
def link(issues: dict) -> dict:
    """Hangs every issue under its parent, when the parent is on the board too."""
    for issue in issues.values():
        issue.children = []
    for issue in sorted(issues.values(), key=lambda i: i.number):
        if issue.parent in issues:
            issues[issue.parent].children.append(issue)
    return issues


def nodes(issue: Issue) -> list:
    return [issue] + [n for child in issue.children for n in nodes(child)]


def roots(issues: dict) -> list:
    return [i for i in issues.values() if i.parent not in issues]


def work(issue: Issue) -> list:
    """What is left to do under an issue: every open issue with no open sub-issue. An open parent
    whose sub-issues are all closed is work of its own, because a sub-issue on this board is not
    always a slice of its parent: a feature also collects the defects found around it, and those can
    all be closed while the feature itself is not done."""
    below = [w for child in issue.children for w in work(child)]
    if issue.closed or below:
        return below
    return [issue]


def lane_of(issues: dict, issue: Issue) -> int:
    """The epic an issue belongs to, which is the lane it is worked in, or NO_EPIC."""
    root = issue
    while root.parent in issues:
        root = issues[root.parent]
    return root.number if root.type == "Epic" else NO_EPIC


def milestone_of(issues: dict, issue: Issue) -> str | None:
    """An issue's milestone, or the nearest ancestor's: a feature in 1.0 takes its stories with it."""
    node = issue
    while node:
        if node.milestone:
            return node.milestone
        node = issues.get(node.parent)
    return None


# ── Points ───────────────────────────────────────────────────────────────────────────────────────────
def written(files: list) -> int:
    """The lines a person wrote among a pull request's changed files: `files` is [(path, lines)]."""
    return sum(lines for path, lines in files if not any(p.search(path) for p in GENERATED))


def lines_by_day(log: str) -> dict:
    """{day: lines a person wrote} from `git log --numstat --format=@%cd --date=short`: each commit's
    changed files under the day it landed, a binary file (its counts are "-") and the files a tool
    writes left out."""
    found, landed, files = defaultdict(int), None, []
    for line in log.splitlines() + ["@"]:
        if line.startswith("@"):
            if landed:
                found[landed] += written(files)
            landed, files = (dt.date.fromisoformat(line[1:]) if len(line) > 1 else None), []
        elif line.count("\t") >= 2:
            added, deleted, path = line.split("\t", 2)
            if added != "-":
                files.append((path, int(added) + int(deleted)))
    return dict(found)


def points_for(lines: float) -> int:
    for limit, points in SCALE:
        if lines < limit:
            return points
    return BEYOND_SCALE


def size(issue: Issue) -> float | None:
    """The lines written for an issue: its share of every merged pull request that closed it. None
    while one of them has not been read, or when none merged."""
    merged = [p for p in issue.pulls if p.merged and p.closes]
    if not merged or any(p.written is None for p in merged):
        return None
    return sum(p.written / p.closes for p in merged)


def delivered_points(issue: Issue) -> float | None:
    """A delivered issue's own points: the ones it was given, else the ones its size says. A parent's
    are its sub-issues', so the points a person gave it before it was split are not counted again: it
    delivers only what its own pull requests wrote."""
    if not (issue.closed and issue.completed):
        return None
    if issue.points is not None and not issue.children:
        return issue.points
    lines = size(issue)
    return points_for(lines) if lines else None


def unread(issues: dict) -> list:
    """The merged pull requests whose size a delivered issue still needs: one nobody pointed, and a
    parent, whatever points it carries, since a parent delivers its own pull requests' size."""
    return sorted({p.number for i in issues.values() if i.closed and i.completed and (i.points is None or i.children)
                   for p in i.pulls if p.merged and p.written is None})


def samples(issues: dict) -> dict:
    """What each type cost when it was delivered, in points, its sub-issues included. An issue whose
    work is still open below it has not finished costing, and one that delivered nothing measurable
    tells nothing."""
    found = defaultdict(list)
    for issue in issues.values():
        if issue.completed and all(n.closed for n in nodes(issue)):
            cost = sum(delivered_points(n) or 0 for n in nodes(issue))
            if cost > 0:
                found[issue.type].append(cost)
    return found


def pool(found: dict, type_: str) -> list:
    """The delivered costs an issue of a type is drawn from: its type's, an epic's falling back to a
    feature's as `estimates` does, and every type's when there is no history closer than that."""
    every = [cost for costs in found.values() for cost in costs] or [3.0]
    if type_ == "Epic":
        return found.get("Epic") or found.get("Feature") or every
    return found.get(type_) or every


def estimates(found: dict) -> dict:
    """The mean points of each type, for an open issue nobody pointed yet. A type with no history
    costs what an issue costs on average, and an epic with none what a feature does."""
    every = [cost for costs in found.values() for cost in costs]
    overall = sum(every) / len(every) if every else 3.0
    means = {t: sum(costs) / len(costs) for t, costs in found.items() if costs}
    means.setdefault("Feature", overall)
    means.setdefault("Epic", means["Feature"])
    return defaultdict(lambda: overall, means)


def cost_of(issue: Issue, cost: dict) -> float:
    return issue.points if issue.points is not None else cost[issue.type]


def remaining(issue: Issue, cost: dict) -> float:
    return sum(cost_of(w, cost) for w in work(issue))


# ── What happened ────────────────────────────────────────────────────────────────────────────────────
def started_on(issue: Issue) -> dt.date | None:
    """When work on an issue visibly began: the earlier of its first pull request and the first day
    the board moved it to In progress, else the day it closed. A parent's own moves are not evidence,
    since this script sets them."""
    evidence = [pull.opened for pull in issue.pulls] + ([issue.moved] if issue.moved and not issue.children else [])
    if evidence:
        return min(evidence)
    if issue.closed and issue.completed:
        return issue.closed_on
    if issue.status in STARTED and not issue.children:
        return issue.status_on
    return None


def real_start(issue: Issue) -> dt.date | None:
    days = [d for d in (started_on(n) for n in nodes(issue)) if d]
    return min(days) if days else None


def real_end(issue: Issue) -> dt.date | None:
    """The day a delivered issue's work ended: the last day it or a sub-issue closed."""
    if not (issue.closed and issue.completed):
        return None
    days = [n.closed_on for n in nodes(issue) if n.closed and n.closed_on]
    return max(days) if days else None


def progressing(issue: Issue) -> bool:
    """Whether an open parent is visibly under way: a sub-issue is in progress, or some of its work
    is done while some is still open. A parent whose sub-issues are all closed has not started its
    own work by that."""
    below = nodes(issue)[1:]
    if any(n.status in STARTED for n in below if not n.closed):
        return True
    return any(n.closed and n.completed for n in below) and any(not n.closed for n in below)


# ── The pace ─────────────────────────────────────────────────────────────────────────────────────────
@dataclass(frozen=True)
class Pace:
    rate: float          # points delivered a day
    delivered: float     # in the window
    days: int            # the window's length, before the floor
    since: dt.date
    daily: tuple         # what each day of the window delivered, padded to the floor with idle days
    shares: dict         # each lane's part of what was delivered


def pace(issues: dict, today: dt.date) -> Pace | None:
    """The points delivered over the last WINDOW_DAYS, per day, and how they were shared among the
    epics. None until the board has delivered something."""
    began = [d for d in (started_on(i) for i in issues.values()) if d]
    if not began:
        return None
    days = max(1, min(WINDOW_DAYS, (today - min(began)).days + 1))
    since = today - dt.timedelta(days=days - 1)
    daily = [0.0] * days
    by_lane = defaultdict(float)
    for issue in issues.values():
        points = delivered_points(issue)
        if points and issue.closed_on and since <= issue.closed_on <= today:
            daily[(issue.closed_on - since).days] += points
            by_lane[lane_of(issues, issue)] += points
    delivered = sum(daily)
    if not delivered:
        return None
    floor = max(MIN_WINDOW_DAYS, days)
    daily += [0.0] * (floor - days)
    return Pace(delivered / floor, delivered, days, since, tuple(daily),
                {lane: part / delivered for lane, part in by_lane.items()})


# ── The plan ─────────────────────────────────────────────────────────────────────────────────────────
def milestone_rank(milestones: dict):
    """Open milestones first by due date, then by title; an issue in none comes after them all."""
    order = sorted(milestones, key=lambda m: (milestones[m] is None, milestones[m] or dt.date.max, m))
    return lambda name: order.index(name) if name in order else len(order)


def priority_rank(issue: Issue | None) -> int:
    return PRIORITIES.index(issue.priority) if issue and issue.priority in PRIORITIES else len(PRIORITIES)


def queues(issues: dict, milestones: dict) -> dict:
    """Each lane's work in the order the lane goes through it: depth first, and among siblings what
    has started first, then what carries the earliest milestone, then priority, then the number. What
    has started is read from the work itself, never from a parent's status, which this script sets:
    the plan must not reorder itself on the board it was written to."""
    rank = milestone_rank(milestones)
    ahead = {}

    def key(issue: Issue):
        if issue.number not in ahead:
            ahead[issue.number] = work(issue)
        started = any(w.status in STARTED for w in ahead[issue.number])
        first = min((rank(milestone_of(issues, w)) for w in ahead[issue.number]), default=rank(None))
        return (not started, first, priority_rank(issue), issue.number)

    def walk(issue: Issue) -> list:
        below = [w for child in sorted(issue.children, key=key) for w in walk(child)]
        if issue.closed or below:
            return below
        return [issue]

    found = defaultdict(list)
    for root in sorted(roots(issues), key=key):
        found[lane_of(issues, root)] += walk(root)
    return {lane: queue for lane, queue in found.items() if queue}


def lane_order(issues: dict, milestones: dict, lanes: dict) -> list:
    """The order in which a waiting lane takes a freed place: the lane holding the earliest
    milestone's work first, then the epic's priority, then its number, the issues no epic holds last."""
    rank = milestone_rank(milestones)

    def key(lane: int):
        first = min((rank(milestone_of(issues, w)) for w in lanes[lane]), default=rank(None))
        return (first, priority_rank(issues.get(lane)), lane == NO_EPIC, lane)

    return sorted(lanes, key=key)


def simulate(lanes: dict, costs: dict, shares: dict, order: list, capacity) -> dict:
    """Works the lanes day by day. Each day `capacity(day)` points of work are shared among the lanes
    holding a place, by their measured shares; a lane works through its queue one issue at a time, and
    when it runs out of work its place goes to the next waiting lane in `order`, or, with none waiting,
    is shared by the lanes still working. Answers {issue number: (first day, last day)}, the last day
    None when the horizon came first."""
    queue = {lane: list(issues) for lane, issues in lanes.items()}
    left = {issue.number: costs[issue.number] for issues in lanes.values() for issue in issues}
    # A lane that delivered in the window holds a place even with nothing left: the first day hands
    # it on, so the share an epic finished with goes to the work that waits.
    places = {lane: share for lane, share in shares.items() if share > 0}
    waiting = [lane for lane in order if lane not in places]
    span = {}
    for day in range(HORIZON_DAYS):
        for lane in [lane for lane in places if not queue.get(lane)]:
            share = places.pop(lane)
            if waiting:
                places[waiting.pop(0)] = share
        if not places:
            break
        budget = capacity(day)
        # What a lane that runs dry today leaves of its part goes to the lanes still working today.
        while budget > 1e-9:
            working = {lane: share for lane, share in places.items() if queue.get(lane)}
            if not working:
                break
            total, spare = sum(working.values()), 0.0
            for lane, share in working.items():
                part = budget * share / total
                while part > 1e-12 and queue[lane]:
                    issue = queue[lane][0]
                    span.setdefault(issue.number, (day, None))
                    used = min(part, left[issue.number])
                    left[issue.number] -= used
                    part -= used
                    if left[issue.number] <= 1e-9:
                        span[issue.number] = (span[issue.number][0], day)
                        queue[lane].pop(0)
                spare += part
            budget = spare
    for issues in queue.values():
        for issue in issues:
            span.setdefault(issue.number, (None, None))
    return span


@dataclass(frozen=True)
class Forecast:
    pace: Pace
    cost: dict
    lanes: dict
    order: list
    span: dict          # the deterministic plan, in days from today
    today: dt.date

    def day(self, offset: int | None) -> dt.date | None:
        return None if offset is None else self.today + dt.timedelta(days=offset)


def forecast(issues: dict, milestones: dict, today: dt.date) -> Forecast | None:
    measured = pace(issues, today)
    if measured is None:
        return None
    cost = estimates(samples(issues))
    lanes = queues(issues, milestones)
    order = lane_order(issues, milestones, lanes)
    costs = {w.number: cost_of(w, cost) for queue in lanes.values() for w in queue}
    span = simulate(lanes, costs, measured.shares, order, lambda _: measured.rate)
    return Forecast(measured, cost, lanes, order, span, today)


def plan(issues: dict, fc: Forecast | None) -> dict:
    """The start and target of every dated issue, each with where it came from: what happened where
    it happened, the plan for the rest. With no forecast (no pace measured), only what happened."""
    found = {}
    for issue in issues.values():
        if issue.type not in DATED or (issue.closed and not issue.completed):
            continue
        began, ended = real_start(issue), real_end(issue)
        ahead = [fc.span[w.number] for w in work(issue) if w.number in fc.span] if fc else []
        firsts = [s for s, _ in ahead if s is not None]
        if began:
            start = (began, "real")
        elif fc is None:
            start = (None, "no pace")
        elif firsts:
            start = (fc.day(min(firsts)), "forecast")
        else:
            start = (None, "beyond the horizon")
        if ended:
            target = (ended, "real")
        elif fc is None:
            target = (None, "no pace")
        elif ahead and all(e is not None for _, e in ahead):
            target = (fc.day(max(e for _, e in ahead)), "forecast")
        else:
            target = (None, "beyond the horizon")
        found[issue.number] = (start, target)
    return found


def changes(issues: dict, dates: dict) -> list:
    """What the board should change: the points a delivered issue's size says when nobody gave it any,
    a parent under way shown In progress, a closed issue shown Done, and the dates of the plan. It
    never closes an issue: whether a parent is done is a judgement its sub-issues cannot make, so the
    report lists those for a person to close. A date the plan no longer reaches is cleared, so the
    board never shows one the report does not stand behind; with no pace, a date is left as it is."""
    found = []
    for issue in sorted(issues.values(), key=lambda i: i.number):
        # A parent's points are its sub-issues': it is never given its own.
        if issue.points is None and not issue.children and issue.item and (points := delivered_points(issue)):
            found.append(Change(issue, "Estimate", None, points, f"{size(issue):.0f} lines written"))
        if issue.type in ROLLED_UP and issue.children and not issue.closed and issue.status not in STARTED \
                and progressing(issue):
            found.append(Change(issue, "status", issue.status, "In progress", f"work began on {real_start(issue)}"))
        if issue.closed and issue.item and issue.status != "Done":
            found.append(Change(issue, "status", issue.status, "Done", "it is closed"))
        if issue.number in dates:
            for name, (value, why), held in zip(("Start date", "Target date"), dates[issue.number],
                                                (issue.start, issue.target)):
                if value and value != held:
                    found.append(Change(issue, name, held, value, why))
                elif value is None and held and why == "beyond the horizon":
                    found.append(Change(issue, name, held, None, why))
    return found


def done_below(issues: dict) -> list:
    """Open parents whose sub-issues are all closed: done, or with work of their own still ahead."""
    return [i for i in sorted(issues.values(), key=lambda i: i.number)
            if not i.closed and i.children and all(n.closed for n in nodes(i)[1:])]


def inflow(issues: dict, fc: Forecast) -> tuple:
    """The issues the board gained over the pace's window, and their work in points. A parent counts
    through its sub-issues, so a feature split into stories is not counted twice."""
    new = [i for i in issues.values() if i.opened and fc.pace.since <= i.opened <= fc.today]
    return len(new), sum(delivered_points(i) or cost_of(i, fc.cost) for i in new if not i.children)


# ── Milestones ───────────────────────────────────────────────────────────────────────────────────────
def scope(issues: dict, milestone: str | None) -> list:
    """The work a milestone waits for, its own issues' and their sub-issues'. None is all of it."""
    ahead = [w for root in roots(issues) for w in work(root)]
    return ahead if milestone is None else [w for w in ahead if milestone_of(issues, w) == milestone]


def finish(span: dict, ahead: list) -> int | None:
    """The day the last of `ahead` ends in a plan, None when one of them never does."""
    ends = [span[w.number][1] for w in ahead]
    return None if None in ends else max(ends, default=0)


def monte_carlo(issues: dict, fc: Forecast, groups: dict, runs: int, seed: int) -> dict:
    """The same plan run `runs` times with what is uncertain drawn from what the board measured:
    each day's capacity from the days of the window, and each issue's cost from its type's delivered
    costs, as a fraction of their mean when a person pointed it, whole when nobody did. Answers
    {group: sorted finishing days}, None for a run past the horizon."""
    rng = random.Random(seed)
    measured = samples(issues)
    queued = [w for q in fc.lanes.values() for w in q]
    draws = {w.type: pool(measured, w.type) for w in queued}
    results = {name: [] for name in groups}
    for _ in range(runs):
        costs = {}
        for w in queued:
            draw = rng.choice(draws[w.type])
            costs[w.number] = draw if w.points is None else w.points * draw / fc.cost[w.type]
        span = simulate(fc.lanes, costs, fc.pace.shares, fc.order, lambda _: rng.choice(fc.pace.daily))
        for name, ahead in groups.items():
            results[name].append(finish(span, ahead))
    return {name: sorted(days, key=lambda d: (d is None, d or 0)) for name, days in results.items()}


def percentile(days: list, p: float):
    """The day by which `p` of the runs had finished, None when that run never did."""
    if not days:
        return None
    return days[min(len(days) - 1, int(round(p * (len(days) - 1))))]


# ── Sprints ──────────────────────────────────────────────────────────────────────────────────────────
@dataclass(frozen=True)
class Sprint:
    title: str
    start: dt.date
    days: int = SPRINT_DAYS
    # The board's iteration id; None for a sprint the board does not have yet.
    id: str | None = None

    @property
    def end(self) -> dt.date:
        return self.start + dt.timedelta(days=self.days - 1)

    def holds(self, day: dt.date) -> bool:
        return self.start <= day <= self.end


def calendar(existing: list, until: dt.date, since: dt.date | None = None) -> list:
    """The board's sprints in order, with sprints of SPRINT_DAYS added on the same grid before them until
    the first holds `since`, the project's first day, and after them until the last holds `until`, all
    named Sprint N from the first. The project is older than its board: Sprint 1 is the project's."""
    found = sorted(existing, key=lambda s: s.start)
    if not found:
        return found
    while since and found[0].start > since:
        found.insert(0, Sprint("", found[0].start - dt.timedelta(days=SPRINT_DAYS)))
    while found[-1].end < until:
        found.append(Sprint("", found[-1].end + dt.timedelta(days=1)))
    return [replace(s, title=f"Sprint {k + 1}") for k, s in enumerate(found)]


def sprinted(issues: dict) -> list:
    """The issues a sprint holds: a delivered one with no sub-issue, and every piece of open work. A
    parent whose work is open below it spans sprints, and a sprint holds its pieces instead."""
    return sorted((i for i in issues.values()
                   if (i.closed and i.completed and not i.children) or (not i.closed and work(i) == [i])),
                  key=lambda i: i.number)


def sprint_of(issue: Issue, fc: Forecast | None, sprints: list) -> tuple:
    """The sprint an issue belongs in, and why: the one it closed in, or the one the plan finishes it
    in. With no forecast an open issue's sprint is not known, which says nothing about the one it has."""
    if issue.closed:
        day, why = issue.closed_on, "closed then"
    elif fc is None:
        return None, "no pace"
    else:
        end = fc.span.get(issue.number, (None, None))[1]
        if end is None:
            return None, "beyond the horizon"
        day, why = fc.day(end), "planned to end then"
    held = next((s for s in sprints if day and s.holds(day)), None)
    return held, why if held else "before the first sprint"


def sprint_changes(issues: dict, fc: Forecast | None, sprints: list) -> list:
    found = []
    for issue in sprinted(issues):
        if not issue.item:
            continue
        held, why = sprint_of(issue, fc, sprints)
        # By the board's id where the sprint has one: renaming the calendar moves no issue.
        same = held and ((held.id and held.id == issue.sprint_id) or held.title == issue.sprint)
        if held and not same:
            found.append(Change(issue, "Sprint", issue.sprint, held.title, why))
        elif held is None and issue.sprint and why != "no pace":
            found.append(Change(issue, "Sprint", issue.sprint, None, why))
    return found


def sprint_load(issues: dict, fc: Forecast | None, sprints: list, lines: dict = None) -> dict:
    """What each sprint delivered and is planned to hold: {title: (points its board issues delivered,
    lines a person wrote on main, points planned)}. `lines` is {day: lines written on main that day}:
    until mid-August 2026 the work went to main without pull requests or issues, so the lines are the
    one measure every sprint of the project has."""
    found = {s.title: [0.0, 0.0, 0.0] for s in sprints}
    for day, count in (lines or {}).items():
        held = next((s for s in sprints if s.holds(day)), None)
        if held:
            found[held.title][1] += count
    for issue in sprinted(issues):
        held, _ = sprint_of(issue, fc, sprints)
        if held is None:
            continue
        if issue.closed:
            found[held.title][0] += delivered_points(issue) or 0
        else:
            found[held.title][2] += cost_of(issue, fc.cost)
    return {title: tuple(load) for title, load in found.items()}
