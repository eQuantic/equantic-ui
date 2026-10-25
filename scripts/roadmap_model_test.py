"""The roadmap's arithmetic, held to what docs/ROADMAP-FORECAST.md says it does.

    python3 -m unittest discover -s scripts -p 'roadmap_*_test.py'
"""
import datetime as dt
import unittest

from roadmap_model import (NO_EPIC, Issue, PullRequest, changes, delivered_points, done_below, forecast, link,
                           monte_carlo, pace, plan, points_for, progressing, queues, scope, simulate, size, work,
                           written)

TODAY = dt.date(2026, 10, 25)


def day(n: int) -> dt.date:
    """n days before TODAY."""
    return TODAY - dt.timedelta(days=n)


def pull(number, opened, merged=None, closes=1, lines=None):
    return PullRequest(number, day(opened), day(merged) if merged is not None else None, closes, lines)


def board(*issues) -> dict:
    return link({i.number: i for i in issues})


def done(number, type_="Task", parent=None, closed=1, points=None, pulls=None, item="x"):
    return Issue(number, f"#{number}", type_, closed=True, completed=True, closed_on=day(closed), status="Done",
                 parent=parent, points=points, pulls=pulls or [], item=item)


def todo(number, type_="Task", parent=None, points=None, status="Backlog", milestone=None, item="x"):
    return Issue(number, f"#{number}", type_, status=status, parent=parent, points=points, milestone=milestone,
                 item=item)


class PointsTests(unittest.TestCase):
    def test_the_scale_doubles_at_each_step(self):
        self.assertEqual([points_for(n) for n in (0, 199, 200, 399, 400, 799, 800, 1599, 1600, 3199, 3200, 6399, 6400)],
                         [1, 1, 2, 2, 3, 3, 5, 5, 8, 8, 13, 13, 21])

    def test_lines_a_tool_writes_are_not_work(self):
        files = [("src/eQuantic.UI.Web/WebRealizer.cs", 120),
                 ("src/eQuantic.UI.Web/PublicAPI.Unshipped.txt", 900),
                 ("src/eQuantic.UI.Lucide/LucideIcons.cs", 4000),
                 ("src/eQuantic.UI.Runtime/src/shared/component-parity.fixture.json", 3000),
                 ("tests/eQuantic.UI.Compiler.Tests/Coverage/ir-migration.baseline.txt", 40),
                 ("src/eQuantic.UI.Runtime/src/shared/__transpiled__/Button.ts", 300),
                 ("docs/LEDGER.md", 200),
                 ("docs/ledger/an-entry.md", 6),
                 ("bun.lock", 50)]
        self.assertEqual(written(files), 126)

    def test_a_pull_request_shares_its_lines_among_the_issues_it_closes(self):
        shared = pull(10, opened=5, merged=4, closes=2, lines=1000)
        issue = done(1, pulls=[shared])
        self.assertEqual(size(issue), 500)
        self.assertEqual(delivered_points(issue), 3)

    def test_the_points_a_person_gave_win_over_the_size(self):
        self.assertEqual(delivered_points(done(1, points=8, pulls=[pull(10, 5, 4, lines=50)])), 8)

    def test_an_unread_pull_request_leaves_the_size_unknown(self):
        issue = done(1, pulls=[pull(10, 5, 4, lines=300), pull(11, 5, 4)])
        self.assertIsNone(size(issue))
        self.assertIsNone(delivered_points(issue))

    def test_an_open_issue_has_no_delivered_points(self):
        self.assertIsNone(delivered_points(todo(1, points=5)))


class WorkTests(unittest.TestCase):
    def test_a_parent_with_an_open_sub_issue_is_not_work_itself(self):
        issues = board(todo(1, "Feature"), todo(2, parent=1), done(3, parent=1))
        self.assertEqual([w.number for w in work(issues[1])], [2])

    def test_a_parent_whose_sub_issues_are_closed_is_work_of_its_own(self):
        # A feature also collects the defects found around it: those closing does not finish it.
        issues = board(todo(1, "Feature"), done(2, "Bug", parent=1))
        self.assertEqual([w.number for w in work(issues[1])], [1])
        self.assertFalse(progressing(issues[1]))
        self.assertEqual([i.number for i in done_below(issues)], [1])

    def test_a_closed_parent_still_hands_on_an_open_sub_issue(self):
        issues = board(done(1, "Feature"), todo(2, parent=1))
        self.assertEqual([w.number for w in work(issues[1])], [2])


class PaceTests(unittest.TestCase):
    def test_the_pace_is_the_window_points_by_lane(self):
        issues = board(todo(1, "Epic"), todo(2, "Epic"),
                       done(3, parent=1, closed=2, points=8), done(4, parent=2, closed=3, points=2),
                       done(5, parent=1, closed=40, points=13))
        issues[5].pulls = [pull(9, opened=60, merged=40)]
        p = pace(issues, TODAY)
        self.assertEqual((p.days, p.delivered), (28, 10))
        self.assertAlmostEqual(p.rate, 10 / 28)
        self.assertEqual(p.shares, {1: 0.8, 2: 0.2})

    def test_a_young_board_is_never_divided_by_fewer_than_seven_days(self):
        p = pace(board(done(1, closed=0, points=7)), TODAY)
        self.assertEqual((p.days, len(p.daily)), (1, 7))
        self.assertEqual(p.rate, 1)

    def test_no_delivery_no_pace(self):
        self.assertIsNone(pace(board(todo(1, status="In progress")), TODAY))


class SimulationTests(unittest.TestCase):
    def test_a_lane_works_its_queue_in_order_at_its_share(self):
        a, b = todo(1), todo(2)
        span = simulate({7: [a, b]}, {1: 2.0, 2: 2.0}, {7: 1.0}, [7], lambda _: 1.0)
        self.assertEqual(span, {1: (0, 1), 2: (2, 3)})

    def test_a_finished_lane_hands_its_place_to_the_next_that_waits(self):
        fast, slow, idle = todo(1), todo(2), todo(3)
        span = simulate({7: [fast], 8: [slow], 9: [idle]}, {1: 1.0, 2: 10.0, 3: 1.0}, {7: 0.5, 8: 0.5}, [7, 8, 9],
                        lambda _: 2.0)
        # Lane 9 had no share: it starts the day after lane 7 runs dry, with lane 7's half of the pace.
        self.assertEqual(span[1], (0, 0))
        self.assertEqual(span[3], (1, 1))

    def test_a_lane_with_a_share_and_no_work_gives_its_place_away_at_once(self):
        span = simulate({9: [todo(1)]}, {1: 1.0}, {7: 1.0}, [9], lambda _: 1.0)
        self.assertEqual(span[1], (0, 0))

    def test_what_the_horizon_does_not_reach_has_no_end(self):
        span = simulate({7: [todo(1)]}, {1: 10.0}, {7: 1.0}, [7], lambda _: 0.0)
        self.assertEqual(span[1], (None, None))


class PlanTests(unittest.TestCase):
    def setUp(self):
        self.issues = board(
            todo(1, "Epic"),
            todo(2, "Feature", parent=1), done(3, parent=2, closed=5, points=3,
                                              pulls=[pull(30, opened=9, merged=5, lines=500)]),
            todo(4, parent=2, points=2),
            todo(5, "Feature", parent=1, milestone="1.0.0"), todo(6, parent=5, points=1),
            todo(7, "Feature", parent=1, status="In progress", points=1),
        )

    def test_started_work_comes_first_then_the_milestone_then_the_number(self):
        lanes = queues(self.issues, {"1.0.0": None})
        self.assertEqual([w.number for w in lanes[1]], [7, 6, 4])

    def test_the_plan_dates_what_happened_as_it_happened(self):
        fc = forecast(self.issues, {"1.0.0": None}, TODAY)
        dates = plan(self.issues, fc)
        (start, why), (target, target_why) = dates[2]
        self.assertEqual((start, why), (day(9), "real"))
        self.assertEqual(target_why, "forecast")
        self.assertGreaterEqual(target, TODAY)

    def test_the_board_is_told_dates_and_statuses_and_never_to_close(self):
        fc = forecast(self.issues, {"1.0.0": None}, TODAY)
        found = changes(self.issues, plan(self.issues, fc))
        kinds = {(c.issue.number, c.kind) for c in found}
        self.assertIn((2, "status"), kinds)
        self.assertIn((1, "Start date"), kinds)
        self.assertNotIn("close", {c.kind for c in found})
        self.assertTrue(all(c.new != "Done" or c.issue.closed for c in found))

    def test_a_milestone_waits_for_its_issues_and_their_sub_issues(self):
        self.assertEqual([w.number for w in scope(self.issues, "1.0.0")], [6])

    def test_the_range_is_the_same_for_the_same_seed(self):
        fc = forecast(self.issues, {"1.0.0": None}, TODAY)
        groups = {"1.0.0": scope(self.issues, "1.0.0")}
        self.assertEqual(monte_carlo(self.issues, fc, groups, 50, 7), monte_carlo(self.issues, fc, groups, 50, 7))


def written_to(issues: dict, found: list):
    """What --apply does to the board, done to the values instead."""
    for c in found:
        if c.kind == "status":
            c.issue.status = c.new
        elif c.kind == "Estimate":
            c.issue.points = c.new
        elif c.kind == "Start date":
            c.issue.start = c.new
        else:
            c.issue.target = c.new


class IdempotenceTests(unittest.TestCase):
    def test_a_board_that_took_the_plan_is_given_the_same_plan(self):
        # The parents the plan shows In progress must not reorder the plan that put them there.
        # Feature 6 is under way (one sub-issue done, one open) and shows Backlog; feature 3 has not
        # started and comes first by number until the plan shows 6 In progress.
        issues = board(
            todo(1, "Epic"),
            todo(3, "Feature", parent=1, points=8),
            todo(6, "Feature", parent=1), done(7, parent=6, closed=6, points=5), todo(8, parent=6, points=3),
        )
        written_to(issues, changes(issues, plan(issues, forecast(issues, {}, TODAY))))
        self.assertEqual(changes(issues, plan(issues, forecast(issues, {}, TODAY))), [])


class ChangeTests(unittest.TestCase):
    def test_a_delivered_issue_nobody_pointed_gets_its_size_in_points(self):
        issues = board(done(1, pulls=[pull(10, 5, 4, lines=900)]))
        found = changes(issues, {})
        self.assertEqual([(c.kind, c.new) for c in found], [("Estimate", 5)])

    def test_a_closed_issue_shown_elsewhere_is_shown_done(self):
        issue = done(1, points=2)
        issue.status = "In progress"
        self.assertEqual([(c.kind, c.new) for c in changes(board(issue), {})], [("status", "Done")])

    def test_an_issue_no_epic_holds_works_in_a_lane_of_its_own(self):
        issues = board(todo(1, points=3), done(2, closed=1, points=3))
        self.assertEqual(list(queues(issues, {})), [NO_EPIC])


if __name__ == "__main__":
    unittest.main()
