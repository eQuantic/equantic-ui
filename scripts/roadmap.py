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
- on epics and features still open, the Start and Target dates the forecast gives them.

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
from roadmap_model import (NO_EPIC, POINTS, changes, cost_of, done_below, finish, forecast, inflow, monte_carlo,
                           percentile, plan, scope)

TYPES = ("Epic", "Feature", "User Story", "Task", "Bug")


def when(fc, days) -> str:
    return "beyond the horizon" if days is None else str(fc.day(days))


def numbers(issues: list) -> str:
    return ", ".join(f"#{i.number}" for i in sorted(issues, key=lambda i: i.number))


def report(issues: dict, milestones: dict, fc, runs: int, seed: int) -> list:
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

    found = changes(issues, plan(issues, fc))
    print()
    for c in found:
        print(f"#{c.issue.number:<5} {c.issue.type:<10} {c.kind:<11} {str(c.old or '-'):<11} → {str(c.new):<11} "
              f"{c.why[:28]:<28} {c.issue.title[:58]}")
    if not found:
        print("The board already shows this.")

    groups = {m: scope(issues, m) for m in milestones}
    groups[None] = ahead
    ranges = monte_carlo(issues, fc, groups, runs, seed)
    print(f"\nMilestones: the plan's date, a range from {runs} runs (half of them done by the first date, 85% by the "
          "second), and the date it would land with all the pace.")
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
    return found


def apply(meta: dict, found: list):
    dates = defaultdict(dict)
    for c in found:
        if c.kind == "status":
            board.set_status(meta, c.issue, c.new)
        elif c.kind == "Estimate":
            board.set_points(meta, c.issue, c.new)
        else:
            dates[c.issue.number]["start" if c.kind == "Start date" else "target"] = (c.issue, c.new)
    for wanted in dates.values():
        issue = next(iter(wanted.values()))[0]
        board.set_dates(meta, issue, wanted.get("start", (None, None))[1], wanted.get("target", (None, None))[1])


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--apply", action="store_true", help="write the changes to the board")
    parser.add_argument("--runs", type=int, default=1000, help="how many runs the milestones' range draws from")
    parser.add_argument("--seed", type=int, default=1, help="the draws' seed, so two reports of one day agree")
    args = parser.parse_args()

    meta, milestones, issues = board.load()
    fc = forecast(issues, milestones, dt.date.today())
    if fc is None:
        print("The board has delivered nothing yet, so there is no pace to forecast from.")
        return
    found = report(issues, milestones, fc, args.runs, args.seed)
    if args.apply and found:
        apply(meta, found)
        print(f"\nWrote {len(found)} changes to the board.")
    elif found:
        print(f"\n{len(found)} changes. Run with --apply to write them.")


if __name__ == "__main__":
    main()
