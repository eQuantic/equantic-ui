#!/usr/bin/env python3
"""Create an issue on the board whole, or not at all.

    python3 scripts/new-issue.py --type Task --parent 160 --points 5 --title 'What it delivers' --body-file body.md
    python3 scripts/new-issue.py --type Epic --priority High --title 'What it is for' --body-file body.md

The plan (scripts/roadmap.py) counts an issue by its type, the epic above it and its points, and gives an
epic's lane its part of the pace by the epic's priority. An issue created without them is invisible to
the plan, or costs a guess, until someone notices, so this refuses to create one without them: a type;
for an epic, its priority; for every other issue, a parent that is open and under an epic, and points,
one of 1, 2, 3, 5, 8 or 13 (bigger is a parent, created as one and split into slices). It creates the
issue with its type, parent, milestone, assignee and priority in one request, puts it on Project 11 with
its points and status, then reads it back and fails naming whatever did not land.

The body is read from a file, or from standard input with `-`, and every value reaches GitHub as a
GraphQL variable, never through a shell. The issue is assigned to the account that runs this, unless
`--assignee` names another. A session's hook (.claude/hooks/issues-are-born-whole.py) refuses the other
ways of creating an issue. Needs gh, logged in with the project scope.
"""
from __future__ import annotations

import argparse
import sys
import time

import roadmap_board as board
from roadmap_model import PRIORITIES, TYPES, lacks

STATUSES = ("Backlog", "Ready", "In progress")
# How far up the tree an epic is looked for: epic, feature, story, task.
DEPTH = 4


def refuse(reasons: list):
    print("An issue is created whole, and this one would lack:", file=sys.stderr)
    for reason in reasons:
        print(f"  - {reason}", file=sys.stderr)
    sys.exit(2)


def chain(number: int) -> dict:
    """The parent and, above it, the ancestors an epic is looked for among."""
    parent = "parent { number issueType { name } " * (DEPTH - 1) + "}" * (DEPTH - 1)
    try:
        found = board.gql(f"""query($o: String!, $r: String!, $n: Int!) {{ repository(owner: $o, name: $r) {{
            issue(number: $n) {{ id number state issueType {{ name }} {parent} }} }} }}""",
                          o=board.OWNER, r=board.REPO, n=number)
    except SystemExit:
        # GitHub answers a number it does not know with an error, which the caller words as a refusal.
        return None
    return found["repository"]["issue"]


def under_an_epic(issue: dict) -> bool:
    node = issue
    while node:
        if (node.get("issueType") or {}).get("name") == "Epic":
            return True
        node = node.get("parent")
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--type", required=True, choices=TYPES)
    parser.add_argument("--title", required=True)
    parser.add_argument("--body-file", required=True, help="the body in Markdown, or - for standard input")
    parser.add_argument("--parent", type=int, help="the issue it is a sub-issue of (every type but Epic)")
    parser.add_argument("--points", type=int, help="1, 2, 3, 5, 8 or 13 (every type but Epic)")
    parser.add_argument("--priority", choices=PRIORITIES, help="an epic's part of the pace (required for Epic)")
    parser.add_argument("--milestone", help="an open milestone's title")
    parser.add_argument("--status", choices=STATUSES, default="Backlog")
    parser.add_argument("--assignee", help="a login; the account that runs this when omitted")
    args = parser.parse_args()

    missing = lacks(args.type, args.parent, args.points, args.priority)
    if args.type == "Epic" and (args.parent or args.points):
        missing.append("nothing above it and no points: an epic is a lane, and its points are its sub-issues'")
    if missing:
        refuse(missing)
    body = sys.stdin.read() if args.body_file == "-" else open(args.body_file, encoding="utf-8").read()
    if not args.title.strip() or not body.strip():
        refuse(["a title and a body"])

    meta = board.gql(board.META, owner=board.OWNER, repo=board.REPO, number=board.PROJECT)
    project = meta["organization"]["projectV2"]
    context = board.gql("""query($o: String!, $r: String!, $login: String!, $me: Boolean!) {
        viewer @include(if: $me) { id login }
        user(login: $login) @skip(if: $me) { id login }
        repository(owner: $o, name: $r) { id milestones(first: 50, states: OPEN) { nodes { id title } } }
        organization(login: $o) { issueTypes(first: 20) { nodes { id name } }
          issueFields(first: 20) { nodes { ... on IssueFieldSingleSelect { id name options { id name } } } } } }""",
                        o=board.OWNER, r=board.REPO, login=args.assignee or "", me=not args.assignee)
    assignee = context.get("viewer") or context.get("user")
    if not assignee:
        refuse([f"an assignee GitHub knows ({args.assignee})"])

    reasons = []
    parent = None
    if args.parent:
        parent = chain(args.parent)
        if not parent:
            reasons.append(f"a parent that exists (#{args.parent} does not)")
        elif parent["state"] != "OPEN":
            reasons.append(f"an open parent (#{args.parent} is closed)")
        elif not under_an_epic(parent):
            reasons.append(f"an epic above it (#{args.parent} is under none, so the plan would hold it in no lane)")
    milestone = None
    if args.milestone:
        milestone = next((m["id"] for m in context["repository"]["milestones"]["nodes"]
                          if m["title"] == args.milestone), None)
        if not milestone:
            reasons.append(f"an open milestone named {args.milestone!r}")
    if reasons:
        refuse(reasons)

    type_id = next(t["id"] for t in context["organization"]["issueTypes"]["nodes"] if t["name"] == args.type)
    fields = []
    if args.priority:
        priority = next(f for f in context["organization"]["issueFields"]["nodes"] if f and f.get("name") == "Priority")
        option = next(o["id"] for o in priority["options"] if o["name"] == args.priority)
        fields.append({"fieldId": priority["id"], "singleSelectOptionId": option})

    created = board.gql("""mutation($i: CreateIssueInput!) { createIssue(input: $i) { issue { id number url } } }""",
                        i={"repositoryId": context["repository"]["id"], "title": args.title, "body": body,
                           "issueTypeId": type_id, "assigneeIds": [assignee["id"]],
                           **({"parentIssueId": parent["id"]} if parent else {}),
                           **({"milestoneId": milestone} if milestone else {}),
                           **({"issueFields": fields} if fields else {})})["createIssue"]["issue"]
    number = created["number"]

    # The board's own fields are set on its item. Adding it answers the item at once, where an item
    # createIssue would add shows on the issue only some seconds later.
    item = board.gql("""mutation($p: ID!, $c: ID!) { addProjectV2ItemById(input: { projectId: $p, contentId: $c }) {
        item { id } } }""", p=project["id"], c=created["id"])["addProjectV2ItemById"]["item"]["id"]
    option = next(o["id"] for o in project["field"]["options"] if o["name"] == args.status)
    board.gql("""mutation($p: ID!, $i: ID!, $f: ID!, $o: String!) { updateProjectV2ItemFieldValue(input: {
        projectId: $p, itemId: $i, fieldId: $f, value: { singleSelectOptionId: $o } }) { projectV2Item { id } } }""",
              p=project["id"], i=item, f=project["field"]["id"], o=option)
    if args.points:
        board.gql("""mutation($p: ID!, $i: ID!, $f: ID!, $n: Float!) { updateProjectV2ItemFieldValue(input: {
            projectId: $p, itemId: $i, fieldId: $f, value: { number: $n } }) { projectV2Item { id } } }""",
                  p=project["id"], i=item, f=project["estimate"]["id"], n=float(args.points))

    # Read back: an issue that exists without what this promised is the case to catch. GitHub shows a
    # write on a read some seconds later, so a difference is read again before it is reported.
    for _ in range(5):
        wrong = whole(read(number), args, assignee["login"])
        if not wrong:
            break
        time.sleep(3)
    if wrong:
        print(f"#{number} was created, but not whole: " + "; ".join(wrong), file=sys.stderr)
        sys.exit(1)
    print(f"#{number} {created['url']}")


def whole(landed: dict, args, login: str) -> list:
    """What the issue as GitHub shows it differs in from what was asked for."""
    values = {v["field"]["name"]: v["name"] for v in landed["issueFieldValues"]["nodes"] if v and v.get("field")}
    on_board = next((n for n in landed["projectItems"]["nodes"] if n["project"]["number"] == board.PROJECT),
                    {"status": None, "estimate": None})
    wanted = {"type": (args.type, (landed["issueType"] or {}).get("name")),
              "parent": (args.parent, (landed["parent"] or {}).get("number")),
              "milestone": (args.milestone, (landed["milestone"] or {}).get("title")),
              "assignee": (login, next((a["login"] for a in landed["assignees"]["nodes"]), None)),
              "priority": (args.priority, values.get("Priority")),
              "status": (args.status, (on_board["status"] or {}).get("name")),
              "points": (float(args.points) if args.points else None, (on_board["estimate"] or {}).get("number"))}
    return [f"{name}: wanted {want}, it has {has}" for name, (want, has) in wanted.items() if want != has]


def read(number: int) -> dict:
    return board.gql("""query($o: String!, $r: String!, $n: Int!) { repository(owner: $o, name: $r) { issue(number: $n) {
        issueType { name } parent { number } milestone { title } assignees(first: 5) { nodes { login } }
        issueFieldValues(first: 10) { nodes { ... on IssueFieldSingleSelectValue { name field { ... on IssueFieldSingleSelect { name } } } } }
        projectItems(first: 10) { nodes { id project { number }
          status: fieldValueByName(name: "Status") { ... on ProjectV2ItemFieldSingleSelectValue { name } }
          estimate: fieldValueByName(name: "Estimate") { ... on ProjectV2ItemFieldNumberValue { number } } } } } } }""",
                     o=board.OWNER, r=board.REPO, n=number)["repository"]["issue"]


if __name__ == "__main__":
    main()
