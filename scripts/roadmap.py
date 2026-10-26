#!/usr/bin/env python3
"""Keep the board's Roadmap on real dates, with a forecast measured on the board.

    python3 scripts/roadmap.py           # the report: what would change, and the forecast behind it
    python3 scripts/roadmap.py --apply   # writes it to the board

Run it whenever the work moves, as the Workflow section of CLAUDE.md says. On Project 11 it sets:

- the points (Estimate) of a delivered issue nobody pointed, from the size of what it delivered;
- In progress on an epic, a feature or a user story whose sub-issues show it under way;
- Done on a closed issue the board still shows elsewhere;
- on epics and features, the Start date their work really started and, once they are delivered, the
  Target date it really ended;
- on epics and features still open, the Start and Target dates the forecast gives them;
- the sprints: two weeks each, named Sprint N and added as the plan reaches them, a delivered issue in
  the one it closed in and an open one in the one the plan finishes it in.

It never closes an issue: the report lists the open parents whose sub-issues are all closed, for a
person to close or to leave open. It also puts each open milestone's forecast beside its due date, as
the plan's date, a range, and the date it would land with all the pace. How the forecast is made is
docs/ROADMAP-FORECAST.md, and scripts/roadmap_model.py says it in code. Needs gh, logged in with the
project scope.
"""
from __future__ import annotations

import argparse
import datetime as dt
import math
from collections import defaultdict

import roadmap_board as board
from roadmap_model import (NO_EPIC, POINTS, calendar, changes, cost_of, done_below, finish, forecast, inflow,
                           monte_carlo, percentile, plan, scope, sprint_changes, sprint_load)

TYPES = ("Epic", "Feature", "User Story", "Task", "Bug")


def when(fc, days) -> str:
    return "beyond the horizon" if days is None else str(fc.day(days))


def numbers(issues: list) -> str:
    return ", ".join(f"#{i.number}" for i in sorted(issues, key=lambda i: i.number))


def renamed(meta: dict, sprints: list) -> list:
    """What the board's sprints must become: a sprint renamed, or one it does not have yet."""
    held = {s.id: s.title for s in board.sprints(meta)}
    return [f"{held[s.id]} → {s.title}" if s.id else f"{s.title} from {s.start}"
            for s in sprints if s.id is None or held[s.id] != s.title]


def show(found: list, calendar_changes: list):
    print()
    if calendar_changes:
        print(f"Sprints: {', '.join(calendar_changes)}")
    for c in found:
        new = "cleared" if c.new is None else str(c.new)
        print(f"#{c.issue.number:<5} {c.issue.type:<10} {c.kind:<11} {str(c.old or '-'):<11} → {new:<11} "
              f"{c.why[:28]:<28} {c.issue.title[:58]}")
    if not found and not calendar_changes:
        print("The board already shows this.")


def report(issues: dict, milestones: dict, fc, sprints: list, calendar_changes: list, runs: int, seed: int) -> list:
    p = fc.pace
    ahead = scope(issues, None)
    left = sum(cost_of(w, fc.cost) for w in ahead)
    gained, gained_cost = inflow(issues, fc)
    print(f"Pace: {p.delivered:.0f} points delivered in the {p.days} days since {p.since}, "
          f"over {len(p.daily)}: {p.rate:.1f} a day.")
    print("An open issue nobody pointed counts what its type averaged when delivered: "
          + ", ".join(f"{t} {fc.cost[t]:.1f}" for t in TYPES) + ".")
    unpointed = [w for w in ahead if w.points is None]
    print(f"Left: {left:.0f} points in {len(ahead)} open issues, {len(unpointed)} of them not pointed yet. In the same "
          f"days the board gained {gained} issues, {gained_cost:.0f} points, which no forecast below counts.\n")

    print("Lanes, in the order a waiting one takes a freed place:")
    for lane in fc.order:
        name = "no epic" if lane == NO_EPIC else f"#{lane} {issues[lane].title[:58]}"
        share = p.shares.get(lane, 0.0)
        cost = sum(cost_of(w, fc.cost) for w in fc.lanes[lane])
        print(f"  {name:<64} {share:5.0%} of the pace  {cost:6.1f} left  ends {when(fc, finish(fc.span, fc.lanes[lane]))}")

    untyped = [i for i in issues.values() if not i.closed and not i.typed]
    if untyped:
        print(f"\nOpen issues with no type, forecast as tasks: {numbers(untyped)}")
    if fc.lanes.get(NO_EPIC):
        print(f"Open issues no epic holds, forecast in a lane of their own: {numbers(fc.lanes[NO_EPIC])}")
    split = [w for w in ahead if w.points is not None and w.points > POINTS[-1]]
    if split:
        print(f"Open issues pointed past {POINTS[-1]}, to split into slices: {numbers(split)}")
    closable = done_below(issues)
    if closable:
        print(f"Open, with every sub-issue closed: {numbers(closable)}. Each is forecast as one more of its type; "
              "close the ones that are done.")

    found = changes(issues, plan(issues, fc)) + sprint_changes(issues, fc, sprints)
    show(found, calendar_changes)

    groups = {m: scope(issues, m) for m in milestones}
    groups[None] = ahead
    ranges = monte_carlo(issues, fc, groups, runs, seed)
    print(f"\nMilestones: the plan's date, a range from {runs} runs (half of them done by the first date, 85% by the "
          "second), and the date it would land with all the pace, the order between its issues aside.")
    for name, work in groups.items():
        label = name or "all open work"
        if not work:
            print(f"  {label:<14} nothing open")
            continue
        cost = sum(cost_of(w, fc.cost) for w in work)
        focus = fc.today + dt.timedelta(days=max(0, math.ceil(cost / p.rate) - 1))
        due = milestones.get(name)
        planned = finish(fc.span, work)
        late = " (past its due date)" if due and planned is not None and fc.day(planned) > due else ""
        print(f"  {label:<14} {len(work):3} open, {cost:5.1f} left   due {due or '-'}   plan {when(fc, planned)}{late}   "
              f"range {when(fc, percentile(ranges[name], 0.5))} to {when(fc, percentile(ranges[name], 0.85))}   "
              f"focused {focus}")
        if name:
            for w in sorted(work, key=lambda w: fc.span[w.number][1] if fc.span[w.number][1] is not None else 10**9):
                points = f"{w.points:g}" if w.points is not None else "-"
                print(f"      #{w.number:<5} {w.type:<10} {points:>3} pts  {when(fc, fc.span[w.number][1]):<11} {w.title[:64]}")

    print("\nSprints: the points each delivered through the board's issues, the lines written on main (the one\n"
          "measure the months before the board have), and the points the plan puts in each one still to end.")
    load = sprint_load(issues, fc, sprints, board.lines_on_main())
    for sprint in sprints:
        points, lines, planned = load[sprint.title]
        print(f"  {sprint.title:<10} {sprint.start} to {sprint.end}   delivered {points:5.0f} pts   "
              f"written {lines:7,.0f} lines   planned {planned:5.0f} pts")
    return found


def apply(meta: dict, found: list, sprints: list, calendar_changes: list):
    # The calendar first: a sprint the board does not have yet has no id to put an issue in.
    ids = board.set_sprints(meta, sprints) if calendar_changes else {s.title: s.id for s in sprints}
    dates = defaultdict(dict)
    for c in found:
        if c.kind == "Sprint":
            board.set_sprint(meta, c.issue, ids[c.new] if c.new else None)
        elif c.kind == "status":
            board.set_status(meta, c.issue, c.new)
        elif c.kind == "Estimate":
            board.set_points(meta, c.issue, c.new)
        else:
            dates[c.issue.number][c.kind] = c.new
    for c in {c.issue.number: c for c in found if c.issue.number in dates}.values():
        board.set_dates(meta, c.issue, dates[c.issue.number])


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--apply", action="store_true", help="write the changes to the board")
    parser.add_argument("--runs", type=int, default=1000, help="how many runs the milestones' range draws from")
    parser.add_argument("--seed", type=int, default=1, help="the draws' seed, so two reports of one day agree")
    args = parser.parse_args()

    meta, milestones, issues = board.load()
    today = dt.date.today()
    fc = forecast(issues, milestones, today)
    # The sprints reach as far as the plan does.
    ends = [fc.day(end) for _, end in fc.span.values() if end is not None] if fc else []
    sprints = calendar(board.sprints(meta), max([today] + ends), board.day(meta["repository"]["createdAt"]))
    calendar_changes = renamed(meta, sprints)
    if fc is None:
        # What happened still reaches the board: the points, the statuses, the real dates and the sprints
        # of what closed need no pace.
        print("Nothing was delivered in the window, so there is no pace to forecast from: only what happened "
              "is written.")
        found = changes(issues, plan(issues, None)) + sprint_changes(issues, None, sprints)
        show(found, calendar_changes)
    else:
        found = report(issues, milestones, fc, sprints, calendar_changes, args.runs, args.seed)
    if args.apply and (found or calendar_changes):
        apply(meta, found, sprints, calendar_changes)
        print(f"\nWrote {len(found) + len(calendar_changes)} changes to the board.")
    elif found or calendar_changes:
        print(f"\n{len(found) + len(calendar_changes)} changes. Run with --apply to write them.")


if __name__ == "__main__":
    main()
