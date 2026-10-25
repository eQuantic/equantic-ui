"""The board scripts/roadmap.py reads and writes: Project 11 of the eQuantic organization, through gh.

Status and Estimate (the points) are fields of the project. Priority, Start date and Target date are not: they are the
organization's issue fields, written on the issue itself (setIssueFieldValue), and the project shows
them. The issue types (Epic, Feature, User Story, Task, Bug) are the organization's too, and the
hierarchy is sub-issues.

Every request goes to `gh api graphql` with the query and its variables as JSON on standard input,
so no title, body or other text read from GitHub is ever part of a command line.
"""
from __future__ import annotations

import datetime as dt
import json
import subprocess
import sys

from dataclasses import replace

from roadmap_model import STARTED, Issue, PullRequest, link, unread, written

OWNER, REPO, PROJECT = "eQuantic", "equantic-ui", 11


def gql(query: str, **variables) -> dict:
    """One GraphQL request. GitHub sometimes hangs or answers "Something went wrong", so a request is
    tried three times; every write here sets a value, so a repeat changes nothing twice."""
    payload, problem = json.dumps({"query": query, "variables": variables}), "no answer"
    for _ in range(3):
        try:
            result = subprocess.run(["gh", "api", "graphql", "--input", "-"], capture_output=True, text=True,
                                    input=payload, timeout=90)
        except subprocess.TimeoutExpired:
            problem = "no answer within 90 seconds"
            continue
        data = json.loads(result.stdout or "{}")
        if not result.returncode and not data.get("errors"):
            return data["data"]
        problem = data.get("errors") or result.stderr.strip()
        if "Something went wrong" not in str(problem) and "HTTP 5" not in str(problem):
            break
    sys.exit(f"GitHub refused the request: {problem}")


def day(timestamp: str | None) -> dt.date | None:
    return dt.date.fromisoformat(timestamp[:10]) if timestamp else None


META = """query($owner: String!, $repo: String!, $number: Int!) {
  organization(login: $owner) {
    projectV2(number: $number) { id field(name: "Status") { ... on ProjectV2SingleSelectField { id options { id name } } }
      estimate: field(name: "Estimate") { ... on ProjectV2Field { id } } }
    issueFields(first: 20) { nodes { ... on IssueFieldDate { id name } ... on IssueFieldSingleSelect { id name } } } }
  repository(owner: $owner, name: $repo) { milestones(first: 50, states: OPEN) { nodes { title dueOn } } } }"""

ITEMS = """query($owner: String!, $number: Int!, $cursor: String) { organization(login: $owner) { projectV2(number: $number) {
  items(first: 40, after: $cursor) { pageInfo { hasNextPage endCursor } nodes { id
    fieldValueByName(name: "Status") { ... on ProjectV2ItemFieldSingleSelectValue { name updatedAt } }
    estimate: fieldValueByName(name: "Estimate") { ... on ProjectV2ItemFieldNumberValue { number } }
    content { ... on Issue { id number title state stateReason createdAt closedAt repository { nameWithOwner }
      issueType { name } milestone { title } parent { number repository { nameWithOwner } }
      issueFieldValues(first: 10) { nodes {
        ... on IssueFieldDateValue { value field { ... on IssueFieldDate { name } } }
        ... on IssueFieldSingleSelectValue { name field { ... on IssueFieldSingleSelect { name } } } } }
      timelineItems(first: 30, itemTypes: [PROJECT_V2_ITEM_STATUS_CHANGED_EVENT]) { nodes {
        ... on ProjectV2ItemStatusChangedEvent { createdAt status project { number } } } }
      closedByPullRequestsReferences(first: 25, includeClosedPrs: true) { nodes { number createdAt mergedAt
        closingIssuesReferences { totalCount } commits(first: 1) { nodes { commit { authoredDate } } } } } } } } } } } }"""


def load():
    """The board: its issues of this repository linked into their tree, its open milestones and their
    due dates, and the ids a write needs."""
    meta = gql(META, owner=OWNER, repo=REPO, number=PROJECT)
    milestones = {m["title"]: day(m["dueOn"]) for m in meta["repository"]["milestones"]["nodes"]}
    issues, cursor = {}, None
    while True:
        page = gql(ITEMS, owner=OWNER, number=PROJECT, cursor=cursor)["organization"]["projectV2"]["items"]
        for node in page["nodes"]:
            content = node["content"] or {}
            if content.get("repository", {}).get("nameWithOwner") != f"{OWNER}/{REPO}":
                continue
            values = {v["field"]["name"]: v.get("value") or v.get("name")
                      for v in content["issueFieldValues"]["nodes"] if v and v.get("field")}
            status = node["fieldValueByName"] or {}
            moves = [day(e["createdAt"]) for e in content["timelineItems"]["nodes"]
                     if e and (e.get("project") or {}).get("number") == PROJECT and e.get("status") in STARTED]
            parent = content["parent"] or {}
            pulls = []
            for pr in content["closedByPullRequestsReferences"]["nodes"]:
                first = [c["commit"]["authoredDate"] for c in pr["commits"]["nodes"]]
                pulls.append(PullRequest(pr["number"], min(day(d) for d in [pr["createdAt"]] + first),
                                         day(pr["mergedAt"]), pr["closingIssuesReferences"]["totalCount"]))
            issues[content["number"]] = Issue(
                number=content["number"], title=content["title"],
                type=(content["issueType"] or {}).get("name") or "Task", typed=bool(content["issueType"]),
                opened=day(content["createdAt"]),
                closed=content["state"] == "CLOSED", completed=content["stateReason"] == "COMPLETED",
                closed_on=day(content["closedAt"]), status=status.get("name"), status_on=day(status.get("updatedAt")),
                moved=min(moves) if moves else None,
                milestone=(content["milestone"] or {}).get("title"), priority=values.get("Priority"),
                points=(node["estimate"] or {}).get("number"),
                start=day(values.get("Start date")), target=day(values.get("Target date")),
                parent=parent.get("number") if parent.get("repository", {}).get("nameWithOwner") == f"{OWNER}/{REPO}" else None,
                pulls=pulls, id=content["id"], item=node["id"])
        if not page["pageInfo"]["hasNextPage"]:
            break
        cursor = page["pageInfo"]["endCursor"]
    link(issues)
    read_sizes(issues)
    return meta, milestones, issues


FILES = """pullRequest(number: %d) { files(first: 100%s) { pageInfo { hasNextPage endCursor } nodes { path additions deletions } } }"""


def read_sizes(issues: dict):
    """Reads the files of the merged pull requests a delivered, unpointed issue needs to be pointed by
    its size, and records on each the lines a person wrote. Fifteen at a time, and a pull request with
    more than a hundred files page by page."""
    numbers, found = unread(issues), {}
    for k in range(0, len(numbers), 15):
        batch = numbers[k:k + 15]
        query = "query { repository(owner: \"%s\", name: \"%s\") { %s } }" % (
            OWNER, REPO, " ".join(f"p{n}: " + FILES % (n, "") for n in batch))
        repo = gql(query)["repository"]
        for n in batch:
            files = repo[f"p{n}"]["files"]
            nodes, page = list(files["nodes"]), files["pageInfo"]
            while page["hasNextPage"]:
                more = gql("query { repository(owner: \"%s\", name: \"%s\") { %s } }" % (
                    OWNER, REPO, FILES % (n, f', after: \"{page["endCursor"]}\"')))["repository"]["pullRequest"]["files"]
                nodes += more["nodes"]
                page = more["pageInfo"]
            found[n] = written([(f["path"], f["additions"] + f["deletions"]) for f in nodes])
    for issue in issues.values():
        issue.pulls = [replace(p, written=found[p.number]) if p.number in found else p for p in issue.pulls]


def set_status(meta: dict, issue: Issue, status: str):
    project = meta["organization"]["projectV2"]
    option = {o["name"]: o["id"] for o in project["field"]["options"]}[status]
    gql("""mutation($p: ID!, $i: ID!, $f: ID!, $o: String!) { updateProjectV2ItemFieldValue(input: {
        projectId: $p, itemId: $i, fieldId: $f, value: { singleSelectOptionId: $o } }) { projectV2Item { id } } }""",
        p=project["id"], i=issue.item, f=project["field"]["id"], o=option)


def set_points(meta: dict, issue: Issue, points: float):
    project = meta["organization"]["projectV2"]
    gql("""mutation($p: ID!, $i: ID!, $f: ID!, $n: Float!) { updateProjectV2ItemFieldValue(input: {
        projectId: $p, itemId: $i, fieldId: $f, value: { number: $n } }) { projectV2Item { id } } }""",
        p=project["id"], i=issue.item, f=project["estimate"]["id"], n=float(points))


def set_dates(meta: dict, issue: Issue, dates: dict):
    """Writes `dates`, {"Start date" or "Target date": a date, or None to clear the field}."""
    fields = {f["name"]: f["id"] for f in meta["organization"]["issueFields"]["nodes"] if f}
    values = [{"fieldId": fields[name], "dateValue": value.isoformat()} if value else
              {"fieldId": fields[name], "delete": True} for name, value in dates.items()]
    if values:
        gql("""mutation($i: ID!, $f: [IssueFieldCreateOrUpdateInput!]!) { setIssueFieldValue(input: {
            issueId: $i, issueFields: $f }) { clientMutationId } }""", i=issue.id, f=values)
