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
import os
import re
import sys
import tempfile

# Where a command starts: the line's start, or after an operator, a subshell or a substitution, or
# inside a shell's `-c` string (with any flags before it) or an `eval`, with any wrappers and
# environment assignments before it. A phrase quoted inside an argument (a commit message that names
# the command) is not a command.
START = (r"(?:^|[;&|(`{]|\$\(|\bthen\b|\bdo\b|\bxargs\b"
         r"|\b(?:ba|z|da)?sh\s+(?:-[A-Za-z]+\s+)*-[A-Za-z]*c\s*['\"]|\beval\s+['\"]?)\s*"
         r"(?:(?:sudo|command|exec|env|nohup|time)\s+)*(?:[A-Za-z_][A-Za-z0-9_]*=\S*\s+)*")
GH_ISSUE_CREATE = re.compile(START + r"gh\s+issue\s+create\b", re.MULTILINE)
GRAPHQL_CREATE = re.compile(r"\bcreateIssue\s*\(")
# The REST collection itself, never an issue under it (/issues/12/comments).
REST_ISSUES = re.compile(r"""(?:^|[\s'"/])repos/[^/\s'"]+/[^/\s'"]+/issues(?=$|[\s'"?])""")
GH_API = re.compile(START + r"gh\s+api\b", re.MULTILINE)
# What can send a GraphQL mutation written on the command line: gh, curl, or a program run on it.
SENDS = re.compile(START + r"(?:gh|curl|python3?|node|bun|deno|ruby|perl)\b", re.MULTILINE)
CURL = re.compile(START + r"curl\b", re.MULTILINE)
# What makes `gh api` write: a method named, or a field or a body, which turn a GET into a POST.
GH_WRITES = re.compile(r"(?:-X\s*|--method[=\s]+)POST\b|\s(?:-f|-F|--field|--raw-field|--input)(?:\s|=)")
CURL_WRITES = re.compile(r"(?:-X\s*|--request[=\s]+)POST\b|\s(?:-d|--data(?:-raw|-binary)?|--json)(?:\s|=)")
# A body a command reads from a file: `gh api -F query=@q.graphql`, `--input body.json`, `curl -d @body`.
FILE_ARGUMENT = re.compile(r"""(?:=@|\s--input[=\s]+|\s(?:-d|--data(?:-raw|-binary)?)[=\s]+@)(?P<path>[^\s'";|&)]+)""")
# How much of a file is read: a GraphQL query or a JSON body, never a large one.
FILE_LIMIT = 1 << 20
MCP_CREATE = re.compile(r"^mcp__.*create_issue")
# A tool that writes issues by method (GitHub's own server names it issue_write): only `create` creates.
MCP_WRITE = re.compile(r"^mcp__.*issue_write")

REASON = ("Issues are created with scripts/new-issue.py, which creates one whole or not at all: a type, an open "
          "parent under an epic and points (1, 2, 3, 5, 8 or 13), or, for an epic, its priority, then puts it on "
          "Project 11 and reads it back. `python3 scripts/new-issue.py --help` says how. (The Workflow section "
          "of CLAUDE.md, #1430.)")


def files_hold_a_creation(command: str, cwd: str | None) -> bool:
    """Whether a file the command hands GitHub as its body or query holds a `createIssue`: the
    mutation a query file carries is not on the command line."""
    for match in FILE_ARGUMENT.finditer(command):
        path = match.group("path")
        if path == "-":
            continue
        path = os.path.join(cwd or os.getcwd(), os.path.expanduser(path))
        try:
            with open(path, encoding="utf-8", errors="replace") as file:
                if GRAPHQL_CREATE.search(file.read(FILE_LIMIT)):
                    return True
        except OSError:
            continue
    return False


def creates_an_issue(command: str, cwd: str | None = None) -> bool:
    if GH_ISSUE_CREATE.search(command):
        return True
    # A mutation on the command line creates an issue only when something there can send it: a
    # grep for it, a git log -S or a commit that quotes it reads it.
    if GRAPHQL_CREATE.search(command) and SENDS.search(command):
        return True
    if (GH_API.search(command) or CURL.search(command)) and files_hold_a_creation(command, cwd):
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
    return tool == "Bash" and creates_an_issue((call.get("tool_input") or {}).get("command") or "", call.get("cwd"))


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
        "bash -e -c 'gh issue create --title x --body y'",
        "eval \"gh issue create --title x\"",
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
        "grep -n 'createIssue(' scripts/new-issue.py",
        "git log -S 'createIssue(' --oneline",
        "git commit -m 'A session never runs gh issue create, it runs the script'",
        "curl -s https://api.github.com/repos/eQuantic/equantic-ui/issues",
    ]
    failures = [f"not refused: {c}" for c in refused if not creates_an_issue(c)]
    failures += [f"refused: {c}" for c in passed if creates_an_issue(c)]
    # A query or a body read from a file: the mutation is in the file, not on the command line.
    with tempfile.TemporaryDirectory() as scratch:
        creation, reading = os.path.join(scratch, "create.graphql"), os.path.join(scratch, "read.graphql")
        with open(creation, "w", encoding="utf-8") as file:
            file.write("mutation($i: CreateIssueInput!) {\n  createIssue(input: $i) { issue { number } }\n}\n")
        with open(reading, "w", encoding="utf-8") as file:
            file.write("{ repository(owner: \"eQuantic\", name: \"equantic-ui\") { issues(first: 1) { totalCount } } }\n")
        from_files = [("gh api graphql -F query=@create.graphql -f i=x", True),
                      ("gh api graphql --input create.graphql", True),
                      ("curl -X POST https://api.github.com/graphql -d @create.graphql", True),
                      ("gh api graphql -F query=@read.graphql", False),
                      ("cat create.graphql", False)]
        failures += [f"from a file, refused is {not want}: {c}" for c, want in from_files
                     if creates_an_issue(c, scratch) != want]
        refused += [c for c, want in from_files if want]
        passed += [c for c, want in from_files if not want]
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
