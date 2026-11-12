#!/usr/bin/env python3
"""A session names a branch `<type>/<slug>`, as the Workflow says, and never with a tool's prefix (#1493).

Claude Code runs this before every Bash command that starts with git (`PreToolUse`, in
.claude/settings.json), with the call as JSON on standard input. When the command creates a branch
of THIS repository whose name is outside the convention (`claude/…`, `my-fix`), it exits 2, which
refuses the call and hands the session the reason on standard error. A squash merge keeps no head
ref, so the name is read at the one moment it can still be changed for free: before the first commit.

It judges the creation, never a commit: a commit happens on the wiki's master, in the middle of a
rebase and in other repositories, and a guard there has to guess which tree it is looking at.
Everything it cannot read passes, and so does a command aimed at another tree (`git -C`, a `cd`
before it): a hook that failed must not stop a session's work.

    .claude/hooks/a-branch-is-named-for-its-change.py --self-test
        prove the classification against its cases, and that every path of the repository the
        pipeline skill and the agents cite is there
"""
from __future__ import annotations

import json
import os
import re
import shlex
import subprocess
import sys

TYPES = ("feat", "fix", "chore", "refactor", "docs", "test", "ci", "perf", "build")
NAME = re.compile(r"^(?:%s)/[a-z0-9][a-z0-9.-]*$" % "|".join(TYPES))
# Where a command starts, as issues-are-born-whole.py reads it, and the git command up to the
# operator that ends it.
START = r"(?:^|[;&|(`{]|\$\(|\bthen\b|\bdo\b)\s*(?:[A-Za-z_][A-Za-z0-9_]*=\S*\s+)*"
GIT = re.compile(START + r"git\s+(?P<rest>[^;&|)\n]*)", re.MULTILINE)
MOVES_ELSEWHERE = re.compile(START + r"(?:cd|pushd)\b", re.MULTILINE)
# `git branch` flags after which no branch is created.
BRANCH_READS = {"-d", "-D", "--delete", "-l", "--list", "-a", "--all", "-r", "--remotes", "-v", "-vv",
                "--show-current", "--merged", "--no-merged", "--contains", "-u", "--set-upstream-to",
                "--unset-upstream", "--edit-description"}


def created_branches(command: str) -> list[str]:
    """The names of the branches a command creates in the tree it runs in."""
    if MOVES_ELSEWHERE.search(command):
        return []
    names = []
    for found in GIT.finditer(command):
        try:
            words = shlex.split(found.group("rest"))
        except ValueError:
            continue
        while words and words[0].startswith("-"):
            if words[0] in ("-C", "--git-dir", "--work-tree"):
                words = []  # another tree: not this one's to judge
            elif words[0] == "-c":
                words = words[2:]
            else:
                words = words[1:]
        if not words:
            continue
        verb, rest = words[0], words[1:]
        after = {flag: rest[i + 1] for i, flag in enumerate(rest[:-1])}
        if verb in ("checkout", "switch", "worktree"):
            for flag in ("-b", "-B", "-c", "-C", "--create", "--force-create", "--orphan"):
                if flag in after and not (verb == "worktree" and flag in ("-c", "-C")):
                    names.append(after[flag])
        elif verb == "branch" and not BRANCH_READS.intersection(rest):
            plain = [word for word in rest if not word.startswith("-")]
            renames = {"-m", "-M", "--move", "-c", "-C", "--copy"}.intersection(rest)
            if plain:
                names.append(plain[-1] if renames else plain[0])
    return names


def is_this_repository(cwd: str | None) -> bool:
    """Whether a git command run in `cwd` acts on the repository the session is in."""
    root = os.environ.get("CLAUDE_PROJECT_DIR")
    if not root or not cwd:
        return False

    def common(path: str) -> str | None:
        try:
            out = subprocess.run(["git", "-C", path, "rev-parse", "--path-format=absolute", "--git-common-dir"],
                                 capture_output=True, text=True, timeout=5)
        except (OSError, subprocess.SubprocessError):
            return None
        return os.path.realpath(out.stdout.strip()) if out.returncode == 0 else None

    here = common(cwd)
    return here is not None and here == common(root)


def refused(command: str) -> list[str]:
    return [name for name in created_branches(command) if not NAME.match(name)]


def cited_paths_missing(root: str) -> list[str]:
    """Paths of the repository the skill and the agents cite that are not there.

    A citation is a backticked path whose first segment is an entry of the repository's root, so
    `feat/slug` and `owner/repo` are not ones, and neither is a pattern (`<name>`, `*`).
    """
    sources = []
    for folder in (".claude/skills/pipeline", ".claude/agents"):
        at = os.path.join(root, folder)
        if os.path.isdir(at):
            sources += [os.path.join(at, name) for name in sorted(os.listdir(at)) if name.endswith(".md")]
    top = set(os.listdir(root))
    missing = []
    for source in sources:
        text = open(source, encoding="utf-8").read()
        for cited in re.findall(r"`([A-Za-z0-9_.\-]+(?:/[A-Za-z0-9_.\-]+)+)/?`", text):
            if cited.split("/")[0] in top and not os.path.exists(os.path.join(root, cited)):
                missing.append(f"{os.path.relpath(source, root)}: {cited}")
    return missing


def self_test() -> int:
    cases = [
        # (command, the names it must refuse)
        ("git switch -c claude/fix-the-thing", ["claude/fix-the-thing"]),
        ("git checkout -b my-fix origin/main", ["my-fix"]),
        ("git fetch && git switch -c Feat/x origin/main", ["Feat/x"]),
        ("git branch wip", ["wip"]),
        ("git branch -m fix/a-good-name worse", ["worse"]),
        ("git worktree add ../tree -b scratch", ["scratch"]),
        ("git -c core.editor=true checkout -B feature/x", ["feature/x"]),
        ("git switch -c fix/a-dependent-builds-in-the-frame-it-opens origin/main", []),
        ("git checkout -b chore/0.2.0-preview.52", []),
        ("git branch -d claude/old", []),
        ("git branch --show-current", []),
        ("git branch -a", []),
        ("git switch main", []),
        ("git checkout -- src/File.cs", []),
        ("git commit -m 'git switch -c claude/x is refused'", []),
        ("git -C ../equantic-ui.wiki switch -c anything", []),
        ("cd ../other && git checkout -b anything", []),
        ("git worktree add ../wiki-tree -b docs/a-page-in-five-languages", []),
        ("git commit --amend --no-edit", []),
        ("git checkout -b 'unterminated", []),
    ]
    failures = [f"{command!r}: refused {refused(command)}, expected {expected}"
                for command, expected in cases if refused(command) != expected]
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    failures += [f"cites a path that is not there: {missing}" for missing in cited_paths_missing(root)]
    for failure in failures:
        print(f"FAIL {failure}", file=sys.stderr)
    print(f"a-branch-is-named-for-its-change self-test: {len(cases)} cases, {len(failures)} failures")
    return 1 if failures else 0


def main() -> int:
    if sys.argv[1:] == ["--self-test"]:
        return self_test()
    try:
        call = json.load(sys.stdin)
        command = call.get("tool_input", {}).get("command", "")
        names = refused(command) if isinstance(command, str) else []
        if not names or not is_this_repository(call.get("cwd")):
            return 0
    except Exception:  # a call this cannot read passes
        return 0
    print(f"A branch is named <type>/<slug>, the change in words (Workflow, Branches): {', '.join(names)} "
          f"is not. The type is one of {', '.join(TYPES)}, and the slug is lowercase words joined by "
          "hyphens, never a tool's or an agent's own prefix.", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
