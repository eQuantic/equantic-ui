#!/usr/bin/env python3
"""A session names a branch `<type>/<slug>`, as the Workflow says, and never with a tool's prefix (#1493).

Claude Code runs this before every Bash command that starts with git (`PreToolUse`, in
.claude/settings.json), with the call as JSON on standard input. When the command creates a branch
of THIS repository whose name is outside the convention (`claude/…`, `my-fix`), it exits 2, which
refuses the call and hands the session the reason on standard error. A squash merge keeps no head
ref, so the name is read at the one moment it can still be changed for free: before the first commit.

It judges the creation, never a commit: a commit happens on the wiki's master, in the middle of a
rebase and in other repositories, and a guard there has to guess which tree it is looking at.
A name that already exists, here or on origin, is not this session's to name (a pull request under
review, `main` reset from origin), and neither is a branch of another repository (`git -C`, a `cd`
before it, both followed). Everything it cannot read passes, a name a variable holds included: a
hook that failed must not stop a session's work.

    .claude/hooks/a-branch-is-named-for-its-change.py --self-test
        prove the classification against its cases, the refusal itself in a repository made for
        it, and that every path of the repository the pipeline skill and the agents cite is there
"""
from __future__ import annotations

import json
import os
import re
import shlex
import subprocess
import sys
import tempfile

TYPES = ("feat", "fix", "chore", "refactor", "docs", "test", "ci", "perf", "build")
NAME = re.compile(r"^(?:%s)/[a-z0-9][a-z0-9.-]*$" % "|".join(TYPES))
# A here-document's body is text, never a command.
HEREDOC = re.compile(r"<<-?\s*(['\"]?)(\w+)\1.*?^\s*\2\s*$", re.DOTALL | re.MULTILINE)
OPERATORS = set(";&|()") | {"&&", "||", ";;", "|&"}
WRAPPERS = {"command", "exec", "env", "sudo", "time", "nohup"}
ASSIGNMENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")
# Flags `git branch` takes while it CREATES one. Any other flag makes it a read or a deletion, so
# a flag this does not know passes: the set that is listed is the one that can refuse.
BRANCH_CREATES = {"-f", "--force", "-t", "--track", "--no-track", "-q", "--quiet", "--create-reflog"}
BRANCH_RENAMES = {"-m", "-M", "--move", "-c", "-C", "--copy"}
CREATES = {"checkout": {"-b", "-B", "--orphan"},
           "switch": {"-c", "-C", "--create", "--force-create", "--orphan"},
           "worktree": {"-b", "-B"}}
UNREADABLE = set("$`*?{}~")


def simple_commands(command: str) -> list[list[str]]:
    """A command line as its simple commands, each a list of words, without redirections."""
    lexer = shlex.shlex(HEREDOC.sub(" ", command).replace("\n", " ; "), posix=True, punctuation_chars=True)
    lexer.whitespace_split = True
    commands, words = [], []
    for word in list(lexer) + [";"]:
        if word in OPERATORS:
            commands.append(words)
            words = []
        elif set(word) <= set("<>&|"):
            words.append("\0")  # a redirection: it and its target are dropped below
        else:
            words.append(word)
    cleaned = []
    for words in commands:
        kept, skip = [], False
        for word in words:
            if word == "\0":
                if kept and kept[-1].isdigit():
                    kept.pop()
                skip = True
            elif skip:
                skip = False
            else:
                kept.append(word)
        if kept:
            cleaned.append(kept)
    return cleaned


def created_branches(command: str, cwd: str = ".") -> list[tuple[str, str]]:
    """Each branch a command creates, with the directory git runs in when it does."""
    found = []
    for words in simple_commands(command):
        while words and (words[0] in WRAPPERS or ASSIGNMENT.match(words[0])):
            words = words[1:]
        if len(words) == 2 and words[0] in ("cd", "pushd"):
            cwd = os.path.join(cwd, words[1])
            continue
        if not words or words[0] != "git":
            continue
        words, at = words[1:], cwd
        while words and words[0].startswith("-"):
            if words[0] == "-C" and len(words) > 1:
                at, words = os.path.join(at, words[1]), words[2:]
            elif words[0] == "-c":
                words = words[2:]
            elif words[0].startswith(("--git-dir", "--work-tree")):
                words = []  # a tree named another way: not read
            else:
                words = words[1:]
        if not words:
            continue
        verb, rest = words[0], words[1:]
        if verb == "stash" and rest[:1] == ["branch"] and len(rest) > 1:
            found.append((rest[1], at))
        elif verb in CREATES and (verb != "worktree" or rest[:1] == ["add"]):
            flags = CREATES[verb]
            named = False
            for i, word in enumerate(rest):
                if word in flags and i + 1 < len(rest):
                    found.append((rest[i + 1], at))
                    named = True
                elif word.startswith("--") and "=" in word and word.split("=")[0] in flags:
                    found.append((word.split("=", 1)[1], at))
                    named = True
                elif word[:2] in flags and len(word) > 2 and not word.startswith("--"):
                    found.append((word[2:], at))
                    named = True
            plain = [word for word in rest[1:] if not word.startswith("-")]
            if verb == "worktree" and not named and len(plain) == 1 and not {"-d", "--detach"} & set(rest):
                found.append((os.path.basename(plain[0].rstrip("/")), at))  # the path's last word names it
        elif verb == "branch":
            flags = {word for word in rest if word.startswith("-")}
            plain = [word for word in rest if not word.startswith("-")]
            if plain and flags <= BRANCH_CREATES | BRANCH_RENAMES:
                found.append((plain[-1] if flags & BRANCH_RENAMES else plain[0], at))
    return [(name, at) for name, at in found if not UNREADABLE & set(name)]


def git(at: str, *arguments: str) -> str | None:
    try:
        out = subprocess.run(["git", "-C", at, *arguments], capture_output=True, text=True, timeout=5)
    except (OSError, subprocess.SubprocessError):
        return None
    return out.stdout.strip() if out.returncode == 0 else None


def is_this_repository(at: str) -> bool:
    """Whether git run in `at` acts on the repository the session is in."""
    root = os.environ.get("CLAUDE_PROJECT_DIR")
    if not root:
        return False
    here = git(at, "rev-parse", "--path-format=absolute", "--git-common-dir")
    there = git(root, "rev-parse", "--path-format=absolute", "--git-common-dir")
    return bool(here) and bool(there) and os.path.realpath(here) == os.path.realpath(there)


def exists(name: str, at: str) -> bool:
    return any(git(at, "rev-parse", "--verify", "--quiet", ref) is not None
               for ref in (f"refs/heads/{name}", f"refs/remotes/origin/{name}"))


def misnamed(command: str) -> list[str]:
    """What the classification alone refuses, before the tree is asked anything."""
    return [name for name, _ in created_branches(command) if not NAME.match(name)]


def refused(command: str, cwd: str) -> list[str]:
    return [name for name, at in created_branches(command, cwd)
            if not NAME.match(name) and is_this_repository(at) and not exists(name, at)]


def cited_paths_missing(root: str) -> list[str]:
    """Paths of the repository the skill and the agents cite that are not there.

    A citation is a word of a backticked span, a command's arguments included, whose first segment
    is an entry of the repository's root: `feat/slug` and `owner/repo` are not ones, and neither is
    a pattern (`<name>`, `*`).
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
        for span in re.findall(r"`([^`\n]+)`", text):
            for word in span.split():
                cited = word.removeprefix("./").rstrip("/.,:;")
                if (re.fullmatch(r"[A-Za-z0-9_.\-]+(?:/[A-Za-z0-9_.\-]+)+", cited) and cited.split("/")[0] in top
                        and not os.path.exists(os.path.join(root, cited))):
                    missing.append(f"{os.path.relpath(source, root)}: {cited}")
    return missing


def refusals_in_a_repository() -> list[str]:
    """The hook run as Claude Code runs it, in a repository made for it: the exit code is the proof."""
    failures = []
    with tempfile.TemporaryDirectory() as temp:
        ours, other = os.path.join(temp, "ours"), os.path.join(temp, "other")
        for path in (ours, other):
            subprocess.run(["git", "init", "-q", "-b", "main", path], check=True)
            subprocess.run(["git", "-C", path, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q",
                            "--allow-empty", "-m", "first"], check=True)
        runs = [  # (command, the directory it runs in, the exit code)
            ("git switch -c claude/x", ours, 2),
            (f"cd {ours} && git switch -c claude/x", other, 2),
            (f"git -C {ours} checkout -b wip", other, 2),
            ("git switch -c fix/a-good-name", ours, 0),
            ("git checkout -B main", ours, 0),  # it exists: reset, not named
            ("git switch -c claude/x", other, 0),
            (f"git -C {other} switch -c claude/x", ours, 0),
            ("git switch -c claude/x", os.path.join(temp, "no-repository"), 0),
        ]
        for command, cwd, expected in runs:
            call = json.dumps({"tool_name": "Bash", "tool_input": {"command": command}, "cwd": cwd})
            ran = subprocess.run([sys.executable, os.path.abspath(__file__)], input=call, text=True,
                                 capture_output=True, env={**os.environ, "CLAUDE_PROJECT_DIR": ours})
            if ran.returncode != expected or (expected == 2 and "<type>/<slug>" not in ran.stderr):
                failures.append(f"{command!r} in {os.path.basename(cwd)}: exit {ran.returncode}, expected {expected}")
        ran = subprocess.run([sys.executable, os.path.abspath(__file__)], input="not json", text=True,
                             capture_output=True, env={**os.environ, "CLAUDE_PROJECT_DIR": ours})
        if ran.returncode != 0:
            failures.append(f"a call that cannot be read: exit {ran.returncode}, expected 0")
    return failures


def self_test() -> int:
    cases = [
        # (command, the names the classification refuses)
        ("git switch -c claude/fix-the-thing", ["claude/fix-the-thing"]),
        ("git checkout -b my-fix origin/main", ["my-fix"]),
        ("git fetch && git switch -c Feat/x origin/main", ["Feat/x"]),
        ("git branch wip", ["wip"]),
        ("git branch -m fix/a-good-name worse", ["worse"]),
        ("git branch -m fix/a-good-name worse 2>&1", ["worse"]),
        ("git worktree add ../tree -b scratch", ["scratch"]),
        ("git worktree add ../scratch", ["scratch"]),
        ("git -c core.editor=true checkout -B feature/x", ["feature/x"]),
        ("git switch --create=claude/x", ["claude/x"]),
        ("git checkout -bclaude/x", ["claude/x"]),
        ("git stash branch wip", ["wip"]),
        ("command git switch -c claude/x", ["claude/x"]),
        ("git switch -c claude/x && cd src", ["claude/x"]),
        ("cd src && git switch -c claude/x", ["claude/x"]),
        ("git -C . switch -c claude/x", ["claude/x"]),
        ("git switch -c fix/a-dependent-builds-in-the-frame-it-opens origin/main", []),
        ("git checkout -b chore/0.2.0-preview.52", []),
        ("git worktree add ../wiki-tree -b docs/a-page-in-five-languages", []),
        ("git worktree add ../tree fix/an-existing-branch", []),
        ("git worktree add --detach ../tree", []),
        ("git branch -d claude/old", []),
        ("git branch --show-current", []),
        ("git branch -a", []),
        ("git branch 2>/dev/null", []),
        ("git branch 2>&1 | head", []),
        ("git branch --no-contains main", []),
        ("git branch --points-at HEAD", []),
        ("git branch --sort committerdate", []),
        ("git branch --set-upstream-to=origin/main claude/x", []),
        ("git switch main", []),
        ("git checkout -- src/File.cs", []),
        ("git switch -c \"$BRANCH\" origin/main", []),
        ("git commit -m 'git switch -c claude/x is refused'", []),
        ("git commit -m \"one; git branch wip; two\"", []),
        ("git commit -m \"$(cat <<'EOF'\nsubject\n\ngit switch -c claude/x is refused now\nEOF\n)\"", []),
        ("git commit -F - <<EOF\ngit branch wip\nEOF", []),
        ("git commit --amend --no-edit", []),
        ("git checkout -b 'unterminated", []),
    ]
    failures = []
    for command, expected in cases:
        try:
            got = misnamed(command)
        except ValueError:  # what the shell itself would not read
            got = []
        if got != expected:
            failures.append(f"{command!r}: refused {got}, expected {expected}")
    failures += refusals_in_a_repository()
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    failures += [f"cites a path that is not there: {missing}" for missing in cited_paths_missing(root)]
    for failure in failures:
        print(f"FAIL {failure}", file=sys.stderr)
    print(f"a-branch-is-named-for-its-change self-test: {len(cases)} cases and the hook run in a "
          f"repository, {len(failures)} failures")
    return 1 if failures else 0


def main() -> int:
    if sys.argv[1:] == ["--self-test"]:
        return self_test()
    try:
        call = json.load(sys.stdin)
        command, cwd = call.get("tool_input", {}).get("command", ""), call.get("cwd")
        names = refused(command, cwd) if isinstance(command, str) and cwd else []
    except Exception:  # a call this cannot read passes
        return 0
    if not names:
        return 0
    print(f"A branch is named <type>/<slug>, the change in words (Workflow, Branches): {', '.join(names)} "
          f"is not. The type is one of {', '.join(TYPES)}, and the slug is lowercase words joined by "
          "hyphens, never a tool's or an agent's own prefix.", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
