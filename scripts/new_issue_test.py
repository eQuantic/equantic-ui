"""scripts/new-issue.py, run whole against a board that answers in memory: an issue is finished on the
board, or the script says what did not land.

    python3 -m unittest discover -s scripts -p 'new_issue_test.py'
"""
import contextlib
import importlib.util
import io
import os
import sys
import tempfile
import unittest
from unittest import mock

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
SPEC = importlib.util.spec_from_file_location("new_issue", os.path.join(HERE, "new-issue.py"))
new_issue = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(new_issue)

# What GitHub answered on 2026-11-18, when the board's own workflow added #1645 as the script did.
ALREADY = ("GitHub refused the request: [{'type': 'UNPROCESSABLE', 'path': ['addProjectV2ItemById'], "
           "'message': 'Content already exists in this project'}]")
FORBIDDEN = ("GitHub refused the request: [{'type': 'FORBIDDEN', 'path': ['addProjectV2ItemById'], "
             "'message': 'Resource not accessible by integration'}]")


class Board:
    """Project 11 as the script talks to it, one GraphQL request at a time.

    `refusals` are what the first adds of the issue answer, one each: GitHub's refusal text. An add past
    them answers the issue's item, which is the one the board's own workflow made when a refusal said the
    issue was already there, since adding an issue the board holds answers the item it has."""

    def __init__(self, *refusals):
        self.refusals, self.adds = list(refusals), 0
        self.item, self.status, self.points = None, None, None

    def gql(self, query: str, **v) -> dict:
        if query is new_issue.board.META:
            return {"organization": {"projectV2": {"id": "P", "estimate": {"id": "E"}, "field": {"id": "S", "options": [
                {"id": f"o-{name}", "name": name} for name in new_issue.STATUSES]}}}}
        if "createIssue" in query:
            return {"createIssue": {"issue": {"id": "I-new", "number": 2000,
                                              "url": "https://github.com/eQuantic/equantic-ui/issues/2000"}}}
        if "addProjectV2ItemById" in query:
            self.adds += 1
            if self.refusals:
                refusal = self.refusals.pop(0)
                if refusal == ALREADY:
                    self.item, self.status = "made-by-the-workflow", "Backlog"
                raise SystemExit(refusal)
            self.item = self.item or "added-by-the-script"
            return {"addProjectV2ItemById": {"item": {"id": self.item}}}
        if "updateProjectV2ItemFieldValue" in query:
            assert v["i"] == self.item, f"a field set on {v['i']}, not on the issue's item {self.item}"
            if v["f"] == "S":
                self.status = next(name for name in new_issue.STATUSES if f"o-{name}" == v["o"])
            else:
                self.points = v["n"]
            return {"updateProjectV2ItemFieldValue": {"projectV2Item": {"id": self.item}}}
        if "projectItems" in query:
            items = [] if self.item is None else [{
                "id": self.item, "project": {"number": new_issue.board.PROJECT},
                "status": {"name": self.status} if self.status else None,
                "estimate": {"number": self.points} if self.points is not None else None}]
            return {"repository": {"issue": {"issueType": {"name": "Task"}, "parent": {"number": 160},
                                             "milestone": None, "assignees": {"nodes": [{"login": "someone"}]},
                                             "issueFieldValues": {"nodes": []}, "projectItems": {"nodes": items}}}}
        if "viewer @include" in query:
            return {"viewer": {"id": "U", "login": "someone"},
                    "repository": {"id": "R", "milestones": {"nodes": []},
                                   "issueTypes": {"nodes": [{"id": "T-Task", "name": "Task"}]}},
                    "organization": {"issueFields": {"nodes": []}}}
        if "state issueType" in query:
            return {"repository": {"issue": {"id": "I-160", "number": 160, "state": "OPEN", "issueType": {"name": "Feature"},
                                             "parent": {"number": 1, "issueType": {"name": "Epic"}, "parent": None}}}}
        raise AssertionError(f"a request the board does not answer: {query[:80]}")


def run(board: Board) -> tuple:
    """Runs the script for a task of 5 points; answers its exit code, standard output and error."""
    with tempfile.NamedTemporaryFile("w", suffix=".md", delete=False) as body:
        body.write("What it delivers.")
    argv = ["new-issue.py", "--type", "Task", "--parent", "160", "--points", "5", "--title", "A task",
            "--body-file", body.name]
    out, err, code = io.StringIO(), io.StringIO(), 0
    try:
        with mock.patch.object(new_issue.board, "gql", board.gql), mock.patch.object(new_issue.time, "sleep"), \
                mock.patch.object(sys, "argv", argv), contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            try:
                new_issue.main()
            except SystemExit as ended:
                code = ended.code
    finally:
        os.unlink(body.name)
    return code, out.getvalue(), err.getvalue()


class AnIssueIsCreatedWhole(unittest.TestCase):

    def test_the_issue_the_script_adds_gets_its_status_and_points(self):
        board = Board()
        code, out, err = run(board)
        self.assertEqual((code, err), (0, ""))
        self.assertIn("#2000 https://github.com/eQuantic/equantic-ui/issues/2000", out)
        self.assertEqual((board.adds, board.item, board.status, board.points),
                         (1, "added-by-the-script", "Backlog", 5.0))

    def test_the_issue_the_board_added_at_the_same_moment_is_finished_on_its_item(self):
        # The board's workflow added the issue as the script did, and GitHub refused the script's add
        # (#1752). The add made again answers the workflow's item, which gets the points.
        board = Board(ALREADY)
        code, out, err = run(board)
        self.assertEqual((code, err), (0, ""))
        self.assertIn("#2000 ", out)
        self.assertEqual((board.adds, board.item, board.status, board.points),
                         (2, "made-by-the-workflow", "Backlog", 5.0))

    def test_an_add_refused_every_time_is_reported_as_not_whole(self):
        board = Board(*[ALREADY] * new_issue.ADD_TRIES)
        code, out, err = run(board)
        self.assertEqual((code, out), (1, ""))
        self.assertIn("#2000 https://github.com/eQuantic/equantic-ui/issues/2000 was created, but not whole: "
                      + ALREADY, err)
        self.assertEqual(board.adds, new_issue.ADD_TRIES)

    def test_any_other_refusal_of_the_add_is_reported_at_once(self):
        board = Board(FORBIDDEN)
        code, out, err = run(board)
        self.assertEqual((code, out), (1, ""))
        self.assertIn("was created, but not whole: " + FORBIDDEN, err)
        self.assertEqual(board.adds, 1, "a refusal that is not the board's own add is never tried again")


if __name__ == "__main__":
    unittest.main()
