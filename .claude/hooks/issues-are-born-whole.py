#!/usr/bin/env python3
"""A session creates an issue with scripts/new-issue.py, which refuses one that is not whole (#1430).

Claude Code runs this before every Bash command and every MCP tool a session calls (`PreToolUse`, in
.claude/settings.json), with the call as JSON on standard input. When the call would create an issue any
other way (`gh issue create`, the GraphQL `createIssue`, a `POST` to the REST `/issues`, or an MCP tool
that creates one), it exits 2, which refuses the call and hands the session the reason on standard
error. Everything else passes untouched, and a call this cannot read passes too: a hook that failed
must not stop a session's work, and the roadmap's report still lists an incomplete issue.

    .claude/hooks/issues-are-born-whole.py --self-test   prove the classification against its cases
"""
from __future__ import annotations

import json
import re
import sys

# Where a command starts: the line's start, or after an operator, a subshell or a substitution, with
# any environment assignments before it. A phrase quoted inside an argument (a commit message that
# names the command) is not a command.
START = (r"(?:^|[;&|(`{]|\$\(|\bthen\b|\bdo\b|\bxargs\b|\b(?:ba|z)?sh\s+-l?c\s*['\"])\s*"
         r"(?:(?:sudo|command|exec|env|nohup|time)\s+)*(?:[A-Za-z_][A-Za-z0-9_]*=\S*\s+)*")
GH_ISSUE_CREATE = re.compile(START + r"gh\s+issue\s+create\b", re.MULTILINE)
GRAPHQL_CREATE = re.compile(r"\bcreateIssue\s*\(")
# The REST collection itself, never an issue under it (/issues/12/comments).
REST_ISSUES = re.compile(r"""(?:^|[\s'"/])repos/[^/\s'"]+/[^/\s'"]+/issues(?=$|[\s'"?])""")
GH_API = re.compile(START + r"gh\s+api\b", re.MULTILINE)
CURL = re.compile(START + r"curl\b", re.MULTILINE)
# What makes `gh api` write: a method named, or a field or a body, which turn a GET into a POST.
GH_WRITES = re.compile(r"(?:-X\s*|--method[=\s]+)POST\b|\s(?:-f|-F|--field|--raw-field|--input)(?:\s|=)")
CURL_WRITES = re.compile(r"(?:-X\s*|--request[=\s]+)POST\b|\s(?:-d|--data(?:-raw|-binary)?|--json)(?:\s|=)")
MCP_CREATE = re.compile(r"^mcp__.*create_issue")
# A tool that writes issues by method (GitHub's own server names it issue_write): only `create` creates.
MCP_WRITE = re.compile(r"^mcp__.*issue_write")

REASON = ("Issues are created with scripts/new-issue.py, which creates one whole or not at all: a type, an open "
          "parent under an epic and points (1, 2, 3, 5, 8 or 13), or, for an epic, its priority, then puts it on "
          "Project 11 and reads it back. `python3 scripts/new-issue.py --help` says how. (The Workflow section "
          "of CLAUDE.md, #1430.)")


def creates_an_issue(command: str) -> bool:
    if GH_ISSUE_CREATE.search(command) or GRAPHQL_CREATE.search(command):
        return True
    if REST_ISSUES.search(command):
        if GH_API.search(command) and GH_WRITES.search(command):
            return True
        if CURL.search(command) and CURL_WRITES.search(command):
            return True
    return False


def refuses(call: dict) -> bool:
    tool = call.get("tool_name") or ""
    if MCP_CREATE.search(tool):
        return True
    if MCP_WRITE.search(tool):
        return str((call.get("tool_input") or {}).get("method") or "").lower() == "create"
    return tool == "Bash" and creates_an_issue((call.get("tool_input") or {}).get("command") or "")


def self_test() -> int:
    refused = [
        "gh issue create --title x --body y",
        "cd /repo && gh issue create -R eQuantic/equantic-ui --title x --body-file b.md",
        "GH_REPO=eQuantic/equantic-ui gh issue create --title x",
        "gh api graphql -f query='mutation { createIssue(input: {repositoryId: \"R\", title: \"x\"}) { issue { number } } }'",
        "python3 - <<'EOF'\nq = 'mutation($i: CreateIssueInput!) { createIssue (input: $i) { issue { id } } }'\nEOF",
        "gh api repos/eQuantic/equantic-ui/issues -f title=x -f body=y",
        "gh api -X POST /repos/eQuantic/equantic-ui/issues --input body.json",
        "gh api --method POST repos/eQuantic/equantic-ui/issues --input -",
        "curl -X POST -H 'Authorization: token t' https://api.github.com/repos/eQuantic/equantic-ui/issues -d '{}'",
        "bash -c 'gh issue create --title x --body y'",
        "sh -c \"gh issue create --title x\"",
        "sudo gh issue create --title x",
        "command gh issue create --title x",
        "env GH_REPO=eQuantic/equantic-ui gh issue create --title x",
    ]
    passed = [
        "gh issue list --state open",
        "gh issue view 1430 --json title",
        "gh issue edit 1430 --add-label bug",
        "gh issue comment 1430 --body-file note.md",
        "gh issue close 1430",
        "gh api 'repos/eQuantic/equantic-ui/issues?state=open&per_page=5'",
        "gh api repos/eQuantic/equantic-ui/issues/12/comments -f body=thanks",
        "gh api graphql -f query='{ repository(owner: \"eQuantic\", name: \"equantic-ui\") { issues(first: 5) { nodes { number } } } }'",
        "python3 scripts/new-issue.py --type Task --parent 160 --points 5 --title 'x' --body-file b.md",
        "grep -rn createIssue scripts/",
        "git commit -m 'A session never runs gh issue create, it runs the script'",
        "curl -s https://api.github.com/repos/eQuantic/equantic-ui/issues",
    ]
    failures = [f"not refused: {c}" for c in refused if not creates_an_issue(c)]
    failures += [f"refused: {c}" for c in passed if creates_an_issue(c)]
    calls = [({"tool_name": "mcp__github__create_issue", "tool_input": {}}, True),
             ({"tool_name": "mcp__github__issue_write", "tool_input": {"method": "create"}}, True),
             ({"tool_name": "mcp__github__issue_write", "tool_input": {"method": "update"}}, False),
             ({"tool_name": "mcp__github__list_issues", "tool_input": {}}, False),
             ({"tool_name": "Read", "tool_input": {"file_path": "x"}}, False),
             ({"tool_name": "Bash", "tool_input": {"command": "gh issue create --title x"}}, True)]
    failures += [f"call {call['tool_name']}: refused is {refuses(call)}" for call, want in calls if refuses(call) != want]
    total = len(refused) + len(passed) + len(calls)
    for failure in failures:
        print(f"FAIL: {failure}", file=sys.stderr)
    print(f"issues-are-born-whole self-test: {total - len(failures)} of {total} cases pass")
    return 1 if failures else 0


def main() -> int:
    if sys.argv[1:] == ["--self-test"]:
        return self_test()
    try:
        call = json.load(sys.stdin)
    except (ValueError, OSError):
        return 0
    if isinstance(call, dict) and refuses(call):
        print(REASON, file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
