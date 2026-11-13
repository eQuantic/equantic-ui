#!/usr/bin/env python3
"""A local, read-only board of every delivery the `pipeline` skill runs (#1524).

It reads `.claude/pipelines/*/state.json` of every worktree of the repository and Claude Code's own
files under `CLAUDE_CONFIG_DIR` (`~/.claude` by default), and serves them on 127.0.0.1 with a page
beside this file. It writes no file, and no value a request or a state carries reaches the file
system as a path: each is a key looked up in a listing the board made itself.

    python3 .claude/skills/pipeline/board/board.py            # http://127.0.0.1:8765/
    python3 .claude/skills/pipeline/board/board.py --port 9000
    python3 .claude/skills/pipeline/board/board.py --self-test

Standard library only, Python 3.9 or later. The design is
`openspec/changes/a-board-shows-every-pipeline-in-flight/design.md`.
"""

import argparse
import glob
import http.client
import http.server
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import urllib.parse
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
COUNTS = ("tokens", "tool_uses", "duration_ms")
TAIL = 256 * 1024
CSP = ("default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; "
       "base-uri 'none'; form-action 'none'; frame-ancestors 'none'")


# ---------------------------------------------------------------------------------------- the reader

def count(value):
    """A count is a non-negative integer or a string of digits, never a boolean (`True` is an `int`)."""
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value if value >= 0 else None
    if isinstance(value, str) and re.fullmatch(r"[0-9]+", value):
        return int(value)
    return None


def moment(value):
    """Seconds since the epoch of an ISO 8601 timestamp, or None. 3.9's fromisoformat takes no `Z`."""
    if not isinstance(value, str):
        return None
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp()
    except ValueError:
        return None


def is_step(node):
    return isinstance(node, dict) and ("tokens" in node or "duration_ms" in node)


def has_step(node):
    return isinstance(node, dict) and any(is_step(v) or has_step(v) for v in node.values())


def step(name, node):
    counted = {key: count(node.get(key)) for key in COUNTS}
    notes = {k: v for k, v in node.items() if k not in COUNTS and k not in ("started", "ended") and not isinstance(v, dict)}
    return dict(counted, name=name, notes=notes)


def walk(node, path, steps, notes):
    """An object carrying a count is a step, one with a step beneath it a group, any other a note."""
    for key, value in node.items():
        if not isinstance(value, dict):
            continue
        here = path + [key]
        if is_step(value):
            steps.append(step(" › ".join(here), value))
        elif has_step(value):
            walk(value, here, steps, notes)
        else:
            notes.append({"path": " › ".join(here), "values": value})


def listed(value):
    """A note the page iterates: a list as it is, any other value present as a list of one."""
    return [] if value in (None, "", [], {}) else value if isinstance(value, list) else [value]


def total(items, key):
    return sum(item[key] or 0 for item in items)


def measured(name, entry, measures, now):
    """One stage: the state's entry (or none), its measures, its steps, notes, sums and time."""
    node = measures if isinstance(measures, dict) else {}
    steps, notes = [], []
    if is_step(node):
        steps.append(step(name, node))
    walk(node, [], steps, notes)
    status = (entry or {}).get("status") or ("completed" if steps or "ended" in node else "pending")
    started, ended = moment(node.get("started")), moment(node.get("ended"))
    if started is not None and ended is not None:
        seconds = ended - started
    elif started is not None and status == "in_progress":
        seconds = now - started
    else:
        seconds = None
    return {
        "name": name,
        "status": status,
        "agent": (entry or {}).get("agent"),
        "model": (entry or {}).get("model"),
        "effort": (entry or {}).get("effort"),
        "report": os.path.basename(str((entry or {}).get("report") or "")) or None,
        "in_state": entry is not None,
        "started": node.get("started"),
        "ended": node.get("ended"),
        "seconds": seconds,
        "tokens": total(steps, "tokens"),
        "tool_uses": total(steps, "tool_uses"),
        "steps": steps,
        "notes": notes,
    }


def normalise(state, now):
    """A delivery read by the design's one rule, whatever shape its `measures` took."""
    measures = state.get("measures") if isinstance(state.get("measures"), dict) else {}
    recorded = measures.get("stages") if isinstance(measures.get("stages"), dict) else {}
    entries = [e for e in state.get("stages") or [] if isinstance(e, dict) and e.get("name")]
    stages = [measured(e["name"], e, recorded.get(e["name"]), now) for e in entries]
    named = {e["name"] for e in entries}
    stages += [measured(name, None, node, now) for name, node in recorded.items() if name not in named]

    statuses = [s["status"] for s in stages]
    open_ = any(s in ("pending", "in_progress") for s in statuses)
    has_ship = state.get("pipeline") != "review"
    merged = measures.get("merged")
    if "failed" in statuses:
        status = "failed"
    elif open_:
        status = "running"
    elif has_ship and not merged:
        status = "shipping"
    else:
        status = "done"

    ship = None
    if has_ship:
        if merged:
            ship = {"status": "completed"}
        elif measures.get("pr_opened") or measures.get("queued"):
            ship = {"status": "waiting", "note": "merge not recorded"}
        else:
            ship = {"status": "pending"}
        ship.update({k: measures.get(k) for k in ("pr_opened", "queued", "merged")})
        ship["queue_exits"] = listed(measures.get("queue_exits"))
        ship["last_ci"] = state.get("last_ci")

    started = moment(measures.get("started"))
    ends = [moment(s["ended"]) for s in stages if moment(s["ended"]) is not None]
    if started is None:
        seconds = None
    elif open_:
        seconds = now - started
    else:
        seconds = (max(ends) - started) if ends else None

    return {
        "issue": count(state.get("issue")),
        "pull_request": count(state.get("pull_request")),
        "branch": state.get("branch"),
        "pipeline": state.get("pipeline"),
        "high_effort": bool(state.get("high_effort")),
        "high_effort_reason": state.get("high_effort_reason"),
        "status": status,
        "started": measures.get("started"),
        "seconds": seconds,
        "tokens": total(stages, "tokens"),
        "stages": stages,
        "ship": ship,
        "notes": {k: listed(v) for k, v in (("pilot_findings", state.get("pilot_findings")),
                                            ("defects_after_pr", measures.get("defects_after_pr")),
                                            ("queue_exits", measures.get("queue_exits"))) if listed(v)},
    }


_CACHE = {}
_LOCK = threading.Lock()


def stamped(read, path):
    """`read(path)`, read again only when the file's (mtime, size) moved: the page polls every 3 s,
    and a transcript or a meta that did not change is not parsed again."""
    try:
        info = os.stat(path)
    except OSError:
        return read(path)
    stamp, key = (info.st_mtime_ns, info.st_size), (read.__name__, path)
    with _LOCK:
        hit = _CACHE.get(key)
    if hit is not None and hit[0] == stamp:
        return hit[1]
    value = read(path)
    with _LOCK:
        _CACHE[key] = (stamp, value)
    return value


def recent(compute, root, seconds=5.0):
    """`compute(root)`, kept a few seconds: the worktrees and the remote rarely move between polls."""
    key = (compute.__name__, root)
    with _LOCK:
        hit = _CACHE.get(key)
    if hit is not None and time.monotonic() - hit[0] < seconds:
        return hit[1]
    value = compute(root)
    with _LOCK:
        _CACHE[key] = (time.monotonic(), value)
    return value


def worktrees(root):
    try:
        out = subprocess.run(["git", "-C", root, "worktree", "list", "--porcelain"],
                             capture_output=True, text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return [root]
    return [line[len("worktree "):] for line in out.splitlines() if line.startswith("worktree ")] or [root]


def deliveries(root):
    """{key: (worktree, folder)} of every delivery folder of every worktree, from listings only."""
    found = {}
    for tree in recent(worktrees, root):
        base = os.path.join(tree, ".claude", "pipelines")
        # `.claude` or `.claude/pipelines` checked out as a link would take every read out of the tree
        if os.path.realpath(base) != os.path.join(os.path.realpath(tree), ".claude", "pipelines"):
            continue
        try:
            names = sorted(os.listdir(base))
        except OSError:
            continue
        for name in names:
            folder = os.path.join(base, name)
            if os.path.isdir(folder) and not os.path.islink(folder):
                key = name
                while key in found:
                    key += "~"
                found[key] = (tree, folder)
    return found


def readable(folder, name, names=None):
    """A file the board serves: listed by the folder, `.md` or state.json, regular, really inside it."""
    try:
        names = os.listdir(folder) if names is None else names
    except OSError:
        return None
    if name not in names or not (name.endswith(".md") or name == "state.json"):
        return None
    path = os.path.join(folder, name)
    inside = os.path.realpath(folder) + os.sep
    if not os.path.isfile(path) or not os.path.realpath(path).startswith(inside):
        return None
    return path


# ---------------------------------------------------------------------------- Claude Code's own files

def config_dir():
    return os.environ.get("CLAUDE_CONFIG_DIR") or os.path.join(os.path.expanduser("~"), ".claude")


def proc_start(pid):
    """Field 22 of /proc/<pid>/stat, the process's start time, or None where /proc has no answer."""
    try:
        with open(f"/proc/{pid}/stat", encoding="utf-8") as f:
            return f.read().rsplit(")", 1)[1].split()[19]
    except (OSError, IndexError):
        return None


def ps_start(pid):
    """When the process began, in seconds since the epoch, from `ps` (where there is no /proc)."""
    try:
        out = subprocess.run(["ps", "-o", "lstart=", "-p", str(pid)], capture_output=True, text=True, timeout=5,
                             env=dict(os.environ, LC_ALL="C")).stdout.strip()
        return time.mktime(time.strptime(out, "%a %b %d %H:%M:%S %Y"))
    except (OSError, ValueError, subprocess.SubprocessError):
        return None


def same_process(began, started_at):
    """A pid is reused, so it is the session's only when its process began within a minute of the
    session's `startedAt` (milliseconds); measured on Linux, the two are 2 s apart."""
    if began is None or count(started_at) is None:
        return None
    return abs(began - count(started_at) / 1000) <= 60


def alive(pid, record):
    """Whether the session a registry file describes is running: its pid, and the same process."""
    if os.path.isdir("/proc"):
        now, started = proc_start(pid), record.get("procStart")
        return now is not None and (started is None or str(started) == now)
    if os.name != "posix":
        return None  # os.kill would end the process on Windows: liveness unknown
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        pass
    return same_process(ps_start(pid), record.get("startedAt"))


def registry(config):
    """{session id: live?} from `sessions/<digits>.json` only: the `.key` beside each is never read."""
    folder = os.path.join(config, "sessions")
    if not os.path.isdir(folder):
        return None
    sessions = {}
    for name in os.listdir(folder):
        if not re.fullmatch(r"[0-9]+\.json", name):
            continue
        try:
            with open(os.path.join(folder, name), encoding="utf-8") as f:
                data = json.load(f)
        except (OSError, ValueError):
            continue
        if isinstance(data, dict) and isinstance(data.get("sessionId"), str):
            sessions[data["sessionId"]] = alive(int(name[:-5]), data)
    return sessions


def agents(config):
    """{agent id: its meta and files}, indexed by the names of the files Claude Code wrote."""
    index = {}
    pattern = os.path.join(glob.escape(config), "projects", "*", "*", "subagents", "agent-*.meta.json")
    for meta in glob.glob(pattern):
        match = re.fullmatch(r"agent-([A-Za-z0-9_-]+)\.meta\.json", os.path.basename(meta))
        data = stamped(read_meta, meta) if match else None
        if isinstance(data, dict):
            index[match.group(1)] = {
                "meta": data,
                "session": os.path.basename(os.path.dirname(os.path.dirname(meta))),
                "transcript": meta[: -len(".meta.json")] + ".jsonl",
            }
    return index


def read_meta(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def tail(path):
    """`read_tail`, parsed again only when the transcript moved."""
    return stamped(read_tail, path)


def read_tail(path, window=TAIL):
    """The last usage, tool use, timestamp, branch and model of a transcript, from its end.

    A line can be longer than the window (151,748 bytes measured), so the window widens fourfold
    while a field is missing, up to the whole file.
    """
    wanted = ("usage", "tool", "timestamp", "branch", "model")
    try:
        size = os.path.getsize(path)
    except OSError:
        return {}
    while True:
        found = {}
        offset = max(0, size - window)
        with open(path, "rb") as f:
            f.seek(offset)
            lines = f.read().split(b"\n")
        if offset:
            lines = lines[1:]
        for raw in reversed(lines):
            try:
                record = json.loads(raw)
            except ValueError:
                continue
            if not isinstance(record, dict):
                continue
            if "timestamp" not in found and isinstance(record.get("timestamp"), str):
                found["timestamp"] = record["timestamp"]
            if "branch" not in found and isinstance(record.get("gitBranch"), str):
                found["branch"] = record["gitBranch"]
            message = record.get("message")
            if record.get("type") == "assistant" and isinstance(message, dict):
                if "usage" not in found and isinstance(message.get("usage"), dict):
                    found["usage"] = message["usage"]
                if "model" not in found and isinstance(message.get("model"), str):
                    found["model"] = message["model"]
                content = message.get("content")
                if "tool" not in found and isinstance(content, list):
                    for part in reversed(content):
                        if isinstance(part, dict) and part.get("type") == "tool_use":
                            found["tool"] = (part.get("name"), part.get("input") or {})
                            break
            if len(found) == len(wanted):
                break
        if len(found) == len(wanted) or offset == 0:
            break
        window *= 4
    try:
        with open(path, "rb") as f:
            first = json.loads(f.readline())
        found["first"] = first.get("timestamp") if isinstance(first, dict) else None
    except (OSError, ValueError):
        found["first"] = None
    return found


def doing(tool, tree):
    """The tool and its telling argument, cut at 120 characters."""
    if not tool:
        return None
    name, args = tool
    args = args if isinstance(args, dict) else {}
    detail = None
    if name == "Bash":
        # a command can carry a secret (`EQS_TOKEN=… eqs …`): its description, else its program only
        detail = args.get("description") if isinstance(args.get("description"), str) else None
        if not detail and isinstance(args.get("command"), str):
            words = [w for w in args["command"].split() if not re.match(r"[A-Za-z_][A-Za-z0-9_]*=", w)]
            detail = os.path.basename(words[0])[:40] if words else None
    for key in ("file_path", "notebook_path", "path"):
        if detail is None and isinstance(args.get(key), str):
            detail = args[key]
            if tree and os.path.isabs(detail) and detail.startswith(tree.rstrip(os.sep) + os.sep):
                detail = os.path.relpath(detail, tree)
    for key in ("pattern", "description", "query"):
        if detail is None and isinstance(args.get(key), str):
            detail = args[key]
    text = f"{name}: {detail}" if detail else str(name)
    text = " ".join(text.split())
    return text if len(text) <= 120 else text[:119] + "…"


def strings(node):
    if isinstance(node, str):
        yield node
    elif isinstance(node, dict):
        for value in node.values():
            yield from strings(value)
    elif isinstance(node, list):
        for value in node:
            yield from strings(value)


def working(state, delivery, tree, index, sessions, now):
    """The agents working now on one delivery: unfinished, in a live session of the delivery, and
    named by the state or on its branch. A session alone does not attribute: one ran three."""
    ids = {s for s in strings(state) if s in index}
    owned = {index[i]["session"] for i in ids}
    if isinstance(state.get("session"), str):
        owned.add(state["session"])
    in_progress = next((s["name"] for s in delivery["stages"] if s["status"] == "in_progress"), None)
    shown = []
    for agent_id, agent in sorted(index.items()):
        meta = agent["meta"]
        if meta.get("finished") or agent["session"] not in owned:
            continue
        live = None if sessions is None else sessions.get(agent["session"], False)
        if live is False:
            continue
        seen = tail(agent["transcript"])
        if agent_id not in ids and seen.get("branch") != state.get("branch"):
            continue
        stage = next((e.get("name") for e in state.get("stages") or []
                      if isinstance(e, dict) and agent_id in set(strings(e))), None) or in_progress
        usage = seen.get("usage") or {}
        context = sum(count(usage.get(k)) or 0 for k in
                      ("input_tokens", "cache_read_input_tokens", "cache_creation_input_tokens", "output_tokens"))
        first, last = moment(seen.get("first")), moment(seen.get("timestamp"))
        shown.append({
            "id": agent_id,
            "live": live,
            "type": meta.get("agentType"),
            "description": meta.get("description"),
            "model": seen.get("model") or meta.get("model"),
            "effort": meta.get("effort"),
            "context": context if usage else None,
            "doing": doing(seen.get("tool"), tree),
            "stage": stage,
            "started": seen.get("first"),
            "running_for": None if first is None else now - first,
            "idle_for": None if last is None else now - last,
        })
    return shown


def origin(root):
    """`https://github.com/<owner>/<repo>` from the `origin` remote, or None."""
    try:
        url = subprocess.run(["git", "-C", root, "remote", "get-url", "origin"],
                             capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return None
    match = re.fullmatch(r"(?:git@github\.com:|https://github\.com/|ssh://git@github\.com/)"
                         r"([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?/?", url)
    return f"https://github.com/{match.group(1)}/{match.group(2)}" if match else None


def board(root, config, now=None):
    """Everything `/api/pipelines` answers."""
    now = time.time() if now is None else now
    index, sessions = agents(config), registry(config)
    shown = []
    for key, (tree, folder) in deliveries(root).items():
        try:
            names = os.listdir(folder)
        except OSError:
            names = []
        reports = sorted(n for n in names if readable(folder, n, names))
        item = {"key": key, "worktree": tree, "reports": reports}
        path = readable(folder, "state.json", names)
        if path is None:
            item.update(status="broken", error="no state.json")
            shown.append(item)
            continue
        try:
            with open(path, encoding="utf-8") as f:
                state = json.load(f)
            if not isinstance(state, dict):
                raise ValueError("state.json is not an object")
        except (OSError, ValueError) as error:
            item.update(status="broken", error=str(error))
            shown.append(item)
            continue
        try:
            item.update(normalise(state, now))
            item["agents"] = working(state, item, tree, index, sessions, now)
        except Exception as error:  # an unknown shape shows as this delivery's error, never stops the rest
            item = {"key": key, "worktree": tree, "reports": reports, "status": "broken",
                    "error": f"unreadable shape: {type(error).__name__}: {error}"}
        shown.append(item)
    order = {"running": 0, "failed": 1, "shipping": 2, "done": 3, "broken": 4}
    shown.sort(key=lambda d: (order.get(d["status"], 5), -(moment(d.get("started")) or 0), d["key"]))
    return {"now": now, "repo": recent(origin, root), "registry": sessions is not None, "deliveries": shown}


# ---------------------------------------------------------------------------------------- the server

def kebab(word):
    return re.sub(r"(?<=[a-z0-9])([A-Z])", r"-\1", word).lower()


def tokens_css(assets):
    """`--<path below color>: light-dark(<light>, <dark>)` for every colour of the design tokens."""
    with open(os.path.join(assets, "docs", "design", "tokens.json"), encoding="utf-8") as f:
        colors = json.load(f)["color"]
    lines = [":root {", "  color-scheme: light dark;"]

    def visit(node, path):
        if isinstance(node, dict) and "light" in node and "dark" in node:
            lines.append(f"  --{'-'.join(kebab(p) for p in path)}: light-dark({node['light']}, {node['dark']});")
        elif isinstance(node, dict):
            for key, value in node.items():
                visit(value, path + [key])

    visit(colors, [])
    lines.append("}")
    return "\n".join(lines) + "\n"


STATIC = {
    "/": ("here", "index.html", "text/html; charset=utf-8"),
    "/board.css": ("here", "board.css", "text/css; charset=utf-8"),
    "/board.js": ("here", "board.js", "text/javascript; charset=utf-8"),
    "/logo-dark.svg": ("assets", "assets/logo-equantic-ui-white.svg", "image/svg+xml"),
    "/logo-light.svg": ("assets", "assets/logo-equantic-ui.svg", "image/svg+xml"),
    "/favicon.png": ("assets", "assets/Icon.png", "image/png"),
}


class Board(http.server.ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, port, root, config, assets):
        super().__init__(("127.0.0.1", port), Handler)
        self.root, self.config, self.assets = root, config, assets


class Handler(http.server.BaseHTTPRequestHandler):
    server_version = "pipelines-board"
    sys_version = ""

    def log_message(self, *args):
        pass

    def answer(self, code, body, kind="text/plain; charset=utf-8"):
        body = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Content-Security-Policy", CSP)
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(body)

    def send_error(self, code, message=None, explain=None):
        """Every answer, the server's own errors included, carries the CSP."""
        self.close_connection = True
        self.answer(code, f"{code}\n")

    def addressed(self):
        port = self.server.server_address[1]
        return self.headers.get("Host") in (f"127.0.0.1:{port}", f"localhost:{port}")

    def refuse(self):
        self.answer(403 if not self.addressed() else 405, "refused\n")

    do_POST = do_PUT = do_DELETE = do_PATCH = do_HEAD = do_OPTIONS = refuse

    def do_GET(self):
        if not self.addressed():
            return self.answer(403, "refused\n")
        raw = urllib.parse.urlsplit(self.path).path
        if raw.startswith("/api/") and self.headers.get("Sec-Fetch-Site") in ("cross-site", "same-site"):
            return self.answer(403, "refused\n")  # another site's page may not make the board work
        server = self.server
        try:
            if raw in STATIC:
                base, name, kind = STATIC[raw]
                with open(os.path.join(HERE if base == "here" else server.assets, name), "rb") as f:
                    return self.answer(200, f.read(), kind)
            if raw == "/tokens.css":
                return self.answer(200, tokens_css(server.assets), "text/css; charset=utf-8")
            if raw == "/api/pipelines":
                return self.answer(200, json.dumps(board(server.root, server.config)), "application/json")
            parts = raw.split("/")
            if len(parts) == 5 and parts[:3] == ["", "api", "report"]:
                key, name = urllib.parse.unquote(parts[3]), urllib.parse.unquote(parts[4])
                found = deliveries(server.root).get(key)
                path = found and readable(found[1], name)
                if path:
                    with open(path, "rb") as f:
                        return self.answer(200, f.read())
        except OSError:
            return self.answer(500, "unreadable\n")
        return self.answer(404, "not found\n")


# ------------------------------------------------------------------------------------- the self-test

def self_test():
    failures = []

    def check(case, ok):
        if not ok:
            failures.append(case)

    scratch = tempfile.mkdtemp(prefix="pipelines-board-")
    try:
        _fixture_and_cases(scratch, check)
    except Exception as error:  # a case that raises fails the self-test, named, instead of a bare trace
        failures.append(f"raised {type(error).__name__}: {error}")
    finally:
        shutil.rmtree(scratch, ignore_errors=True)
    for case in failures:
        print(f"FAIL {case}")
    print(f"{'failed' if failures else 'passed'}: {len(failures)} case(s) failed")
    return 1 if failures else 0


def _write(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content if isinstance(content, str) else json.dumps(content))


def _jsonl(path, records):
    _write(path, "".join(json.dumps(r) + "\n" for r in records))


def _fixture_and_cases(scratch, check):
    git = ["git", "-c", "user.name=t", "-c", "user.email=t@t", "-c", "init.defaultBranch=main"]
    repo, two, config = (os.path.join(scratch, n) for n in ("repo", "two", "config"))
    os.makedirs(repo)
    subprocess.run(git + ["init", "-q", repo], check=True)
    subprocess.run(git + ["-C", repo, "commit", "-q", "--allow-empty", "-m", "init"], check=True)
    subprocess.run(git + ["-C", repo, "worktree", "add", "-q", "-b", "feat/two", two], check=True)

    A, B, C, D, E, F = ("a" + c * 16 for c in "abcdef")
    now = time.time()
    iso = lambda seconds_ago: datetime.fromtimestamp(now - seconds_ago, timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    leak_marker, key_marker = "LEAK-MARKER-7f3a", "KEY-MARKER-91c2"

    flat = os.path.join(repo, ".claude", "pipelines", "1-flat")
    _write(os.path.join(flat, "state.json"), {
        "issue": 1, "branch": "feat/one", "pipeline": "feature", "pull_request": 2,
        "stages": [{"name": "Write", "status": "completed", "agent_id": A, "report": "x/write.md"},
                   {"name": "Gate", "status": "completed"}],
        "last_ci": {"run": 5, "sha": "abc"},
        "measures": {"started": "2026-01-01T00:00:00Z", "stages": {
            "Write": {"started": "2026-01-01T00:00:00Z", "ended": "2026-01-01T00:01:00Z",
                      "tokens": "84430", "tool_uses": "30", "duration_ms": "60000"},
            "Gate": {"started": "2026-01-01T00:01:00Z", "ended": "2026-01-01T00:10:00Z",
                     "round1": {"preship": {"tokens": 57254, "verdict": "open"}, "sorted": {"defects": 3}},
                     "round2": {"code_review": {"tokens": "1000", "duration_ms": "5"}, "sorted": {"defects": 0}},
                     "prover": {"tokens": 94392, "verdict": "proved"}}},
            "pr_opened": "2026-01-01T00:11:00Z", "queued": "2026-01-01T00:12:00Z",
            "queue_exits": [{"job": "samples", "cause": "runner lost"}]}})
    _write(os.path.join(flat, "design.md"), "# design\n")
    _write(os.path.join(scratch, "outside", "secret.md"), leak_marker + "\n")
    try:
        os.symlink(os.path.join(scratch, "outside", "secret.md"), os.path.join(flat, "leak.md"))
        linked = True
    except OSError:
        linked = False
    check("the fixture holds a symbolic link out of a delivery folder", linked)

    _write(os.path.join(repo, ".claude", "pipelines", "2-nested", "state.json"), {
        "issue": 2, "branch": "feat/nested", "pipeline": "bug",
        "stages": [{"name": "Implement", "status": "completed"}],
        "measures": {"started": "2026-01-01T00:00:00Z", "merged": "2026-01-01T02:00:00Z", "stages": {
            "Implement": {"started": "2026-01-01T00:00:00Z", "ended": "2026-01-01T01:00:00Z",
                          "tokens": 50, "tool_uses": 2, "duration_ms": 10},
            "Owner review fix": {"tokens": 7, "duration_ms": 1}}}})

    _write(os.path.join(two, ".claude", "pipelines", "3-running", "state.json"), {
        "issue": 3, "branch": "feat/two", "pipeline": "feature", "session": "live",
        "stages": [{"name": "Design", "status": "completed", "agent_id": B},
                   {"name": "Implement", "status": "in_progress"},
                   {"name": "Gate", "status": "pending", "agent_id": E}],
        "measures": {"started": iso(600), "stages": {
            "Design": {"started": iso(600), "ended": iso(300),
                       "first_run": {"tokens": 100, "outcome": "stopped"}, "resumed_run": {"tokens": 200}},
            "Implement": {"started": iso(200), "probe": {"tokens": True, "duration_ms": 5}}}}})

    _write(os.path.join(repo, ".claude", "pipelines", "4-failed", "state.json"),
           {"issue": 4, "branch": "feat/four", "stages": [{"name": "Implement", "status": "failed"}]})
    _write(os.path.join(repo, ".claude", "pipelines", "5-broken", "state.json"), "{not json")
    _write(os.path.join(repo, ".claude", "pipelines", "6-odd", "state.json"), {"issue": 6, "stages": 3})
    _write(os.path.join(repo, ".claude", "pipelines", "7-notes", "state.json"), {
        "issue": "7", "pull_request": "8", "branch": "feat/seven", "pilot_findings": "one finding",
        "stages": [{"name": "Write", "status": "completed"}],
        "measures": {"defects_after_pr": 2, "queue_exits": {"job": "samples"}}})

    # `.claude/pipelines` and `.claude` checked out as links out of the tree: nothing under them is read.
    secret_marker = "LINKED-TREE-MARKER-4d1e"
    for branch, linked_part in (("feat/three", "pipelines"), ("feat/four", "claude")):
        tree = os.path.join(scratch, branch.split("/")[1])
        subprocess.run(git + ["-C", repo, "worktree", "add", "-q", "-b", branch, tree], check=True)
        victim = os.path.join(scratch, "victim-" + linked_part)
        target = os.path.join(victim, "pipelines") if linked_part == "claude" else victim
        _write(os.path.join(target, "v-" + linked_part, "state.json"), {"issue": 9, "branch": secret_marker})
        _write(os.path.join(target, "v-" + linked_part, "private.md"), secret_marker)
        if linked_part == "claude":
            os.symlink(victim, os.path.join(tree, ".claude"))
        else:
            os.makedirs(os.path.join(tree, ".claude"))
            os.symlink(victim, os.path.join(tree, ".claude", "pipelines"))

    # Claude Code's files: a live session (this process) and a dead one (a child already reaped).
    child = subprocess.Popen([sys.executable, "-c", ""])
    child.wait()
    _write(os.path.join(config, "sessions", f"{os.getpid()}.json"),
           {"pid": os.getpid(), "sessionId": "live", "procStart": proc_start(os.getpid()),
            "startedAt": int((ps_start(os.getpid()) or now) * 1000)})
    _write(os.path.join(config, "sessions", f"{os.getpid()}.0123abcd.key"), key_marker)
    _write(os.path.join(config, "sessions", f"{child.pid}.json"),
           {"pid": child.pid, "sessionId": "dead", "procStart": "1"})
    live, dead = (os.path.join(config, "projects", "p", s, "subagents") for s in ("live", "dead"))

    def agent(folder, agent_id, meta, records):
        _write(os.path.join(folder, f"agent-{agent_id}.meta.json"), meta)
        _jsonl(os.path.join(folder, f"agent-{agent_id}.jsonl"), records)

    def act(branch, ago, tool, args, usage=None):
        return {"type": "assistant", "timestamp": iso(ago), "gitBranch": branch,
                "message": {"model": "claude-test", "usage": usage or {"input_tokens": 1},
                            "content": [{"type": "text", "text": "…"},
                                        {"type": "tool_use", "name": tool, "input": args}]}}

    agent(live, A, {"agentType": "docs-writer", "finished": True}, [act("feat/one", 900, "Read", {})])
    agent(live, B, {"agentType": "engineer", "description": "Build it", "effort": "high"}, [
        {"type": "user", "timestamp": iso(120), "gitBranch": "feat/two", "message": {"content": "go"}},
        {"type": "user", "timestamp": iso(60), "gitBranch": "feat/two", "message": {"content": "x" * (TAIL + 4096)}},
        act("feat/two", 30, "Bash", {"command": "python3 board.py --self-test",
                                     "description": "Run the reader's self-test"},
            {"input_tokens": 10, "cache_read_input_tokens": 1000, "cache_creation_input_tokens": 200,
             "output_tokens": 5})])
    agent(live, C, {"agentType": "explorer"}, [act("feat/two", 10, "Read", {"file_path": os.path.join(two, "src", "a.py")})])
    agent(live, D, {"agentType": "explorer"}, [act("feat/else", 10, "Grep", {"pattern": "x"})])
    agent(dead, E, {"agentType": "prover"}, [act("feat/two", 10, "Bash", {"command": "true"})])
    command_marker = "COMMAND-MARKER-0b7c"
    agent(live, F, {"agentType": "engineer"},
          [act("feat/two", 5, "Bash", {"command": f"EQS_TOKEN={command_marker} /usr/bin/eqs runs ls"})])

    # The tail widens: a last line longer than the window hides the tool use before it.
    wide = os.path.join(scratch, "wide.jsonl")
    _jsonl(wide, [act("b", 5, "Glob", {"pattern": "*.py"}), {"type": "progress", "data": "y" * (TAIL + 10)}])
    check("the tail widens past a line longer than 256 KiB", (tail(wide).get("tool") or [None])[0] == "Glob")

    # ------------------------------------------------------------------------------ the reader
    check("count: digits string", count("84430") == 84430)
    check("count: a boolean is not a count", count(True) is None and count(False) is None)
    check("count: other strings", count("12k") is None and count("") is None)
    check("count: a negative is not a count", count(-5) is None and count("-5") is None)
    first = tail(wide)
    check("a transcript that did not move is not parsed again", tail(wide) is first)
    with open(wide, "a", encoding="utf-8") as f:
        f.write(json.dumps(act("b", 1, "Grep", {"pattern": "y"})) + "\n")
    check("a transcript that moved is parsed again", (tail(wide).get("tool") or [None])[0] == "Grep")
    check("a Bash call with a description shows it",
          doing(("Bash", {"command": "rm -rf x", "description": "Clean"}), None) == "Bash: Clean")
    check("a Bash call without one shows its program only",
          doing(("Bash", {"command": "TOKEN=s3cret A=1 /usr/bin/gh api -H x"}), None) == "Bash: gh")
    if os.path.isdir("/proc"):
        check("a live pid of another start is not the session", alive(os.getpid(), {"procStart": "1"}) is False)
    check("without /proc, a pid is the session's only near its start",
          same_process(now, int(now * 1000)) is True and same_process(now - 3600, int(now * 1000)) is False
          and same_process(None, int(now * 1000)) is None)
    if shutil.which("ps"):
        began = ps_start(os.getpid())
        check("ps gives this process's start", began is not None and abs(began - now) < 600)

    data = board(repo, config, now)
    by = {d["key"]: d for d in data["deliveries"]}
    check("every worktree's deliveries are listed",
          sorted(by) == ["1-flat", "2-nested", "3-running", "4-failed", "5-broken", "6-odd", "7-notes"])
    check("a .claude or .claude/pipelines that is a link out is not read",
          not any(k.startswith("v-") for k in by) and secret_marker not in json.dumps(data))
    check("a state of an unknown shape is that delivery's error, and the rest are listed",
          by["6-odd"]["status"] == "broken" and "TypeError" in by["6-odd"].get("error", ""))
    notes = by["7-notes"]["notes"]
    check("notes of any shape reach the page as lists",
          notes == {"pilot_findings": ["one finding"], "defects_after_pr": [2], "queue_exits": [{"job": "samples"}]}
          and by["7-notes"]["ship"]["queue_exits"] == [{"job": "samples"}])
    check("an issue and a pull request written as strings are integers",
          by["7-notes"]["issue"] == 7 and by["7-notes"]["pull_request"] == 8)
    check("the running delivery is listed first", data["deliveries"][0]["key"] == "3-running")
    check("the broken state is listed with its error",
          by["5-broken"]["status"] == "broken" and by["5-broken"].get("error"))

    flat_d = by["1-flat"]
    stage = {s["name"]: s for s in flat_d["stages"]}
    check("a stage carrying tokens as a string is one step named after it",
          [(s["name"], s["tokens"]) for s in stage["Write"]["steps"]] == [("Write", 84430)])
    check("tool uses and durations written as strings are counted",
          stage["Write"]["steps"][0]["tool_uses"] == 30 and stage["Write"]["steps"][0]["duration_ms"] == 60000)
    check("rounds are steps by their key path",
          [s["name"] for s in stage["Gate"]["steps"]] == ["round1 › preship", "round2 › code_review", "prover"])
    check("an object with no step beneath it is a note",
          [n["path"] for n in stage["Gate"]["notes"]] == ["round1 › sorted", "round2 › sorted"])
    check("a step's other keys are its notes", stage["Gate"]["steps"][0]["notes"] == {"verdict": "open"})
    check("a stage's tokens are its steps' sum", stage["Gate"]["tokens"] == 57254 + 1000 + 94392)
    check("a stage's time is ended - started", stage["Gate"]["seconds"] == 540)
    check("a delivery's tokens are its stages' sum", flat_d["tokens"] == 84430 + 57254 + 1000 + 94392)
    check("a delivery's time runs to the last ended", flat_d["seconds"] == 600)
    check("a delivery with an open pull request and no merge is shipping", flat_d["status"] == "shipping")
    check("Ship waits with pr_opened, and says the merge is not recorded",
          flat_d["ship"]["status"] == "waiting" and flat_d["ship"]["note"] == "merge not recorded"
          and flat_d["ship"]["queue_exits"] and flat_d["ship"]["last_ci"] == {"run": 5, "sha": "abc"})
    check("the queue's exits are a note", flat_d["notes"].get("queue_exits"))
    check("a report is named by its file only", stage["Write"]["report"] == "write.md")
    check("the folder's reports exclude a link out",
          flat_d["reports"] == ["design.md", "state.json"])

    nested = by["2-nested"]
    check("a stage only in measures is drawn after the state's, and counted",
          [s["name"] for s in nested["stages"]] == ["Implement", "Owner review fix"]
          and not nested["stages"][1]["in_state"] and nested["tokens"] == 57)
    check("a merged delivery is done and its Ship completed",
          nested["status"] == "done" and nested["ship"]["status"] == "completed")

    running = by["3-running"]
    stage = {s["name"]: s for s in running["stages"]}
    check("resumed runs are two steps", [s["name"] for s in stage["Design"]["steps"]] == ["first_run", "resumed_run"]
          and stage["Design"]["tokens"] == 300)
    check("a boolean token adds nothing to its stage", stage["Implement"]["tokens"] == 0)
    check("an in-progress stage's time runs to now", abs(stage["Implement"]["seconds"] - 200) < 2)
    check("a pending stage has no time", stage["Gate"]["seconds"] is None)
    check("a running delivery's time runs to now", running["status"] == "running" and abs(running["seconds"] - 600) < 2)
    check("a failed stage fails the delivery", by["4-failed"]["status"] == "failed")

    # -------------------------------------------------------------------------------- agents
    found = {a["id"]: a for a in running["agents"]}
    check("B, C and F work on the running delivery, and only they", sorted(found) == [B, C, F])
    check("a Bash call's command never reaches the API",
          F in found and found[F]["doing"] == "Bash: eqs" and command_marker not in json.dumps(data))
    check("an agent of a dead session works on nothing (finished alone does not decide)",
          all(E not in {a["id"] for a in d.get("agents", [])} for d in data["deliveries"]))
    check("an agent on another branch that no state names works on nothing",
          all(D not in {a["id"] for a in d.get("agents", [])} for d in data["deliveries"]))
    check("one session, two branches: the other delivery of the session has none", flat_d["agents"] == [])
    if B in found:
        b = found[B]
        check("B's stage is the stage that names it", b["stage"] == "Design")
        check("B's step is its last tool and description", b["doing"] == "Bash: Run the reader's self-test")
        check("B's context is the last usage's sum", b["context"] == 1215)
        check("B started at its first record", b["started"] == iso(120) and abs(b["running_for"] - 120) < 2)
        check("B is live", b["live"] is True)
    if C in found:
        check("C, unnamed, is on the stage in progress", found[C]["stage"] == "Implement")
        check("a file tool's path is relative to the worktree", found[C]["doing"] == "Read: src/a.py")

    # -------------------------------------------------------------------------------- server
    server = Board(0, repo, config, REPO)
    port = server.server_address[1]
    threading.Thread(target=server.serve_forever, daemon=True).start()
    seen = []

    def get(path, host=None, method="GET", headers=()):
        conn = http.client.HTTPConnection("127.0.0.1", port, timeout=10)
        conn.putrequest(method, path, skip_host=True, skip_accept_encoding=True)
        conn.putheader("Host", host or f"127.0.0.1:{port}")
        for name, value in headers:
            conn.putheader(name, value)
        conn.endheaders()
        response = conn.getresponse()
        body = response.read().decode("utf-8", "replace")
        conn.close()
        seen.append((path, response.status, response.getheader("Content-Security-Policy"),
                     response.getheader("X-Content-Type-Options"), body))
        return response.status, body

    try:
        for path in ("/", "/board.css", "/board.js", "/tokens.css", "/logo-dark.svg", "/logo-light.svg",
                     "/api/pipelines", "/api/report/1-flat/design.md", "/api/report/1-flat/state.json"):
            check(f"GET {path} is 200", get(path)[0] == 200)
            check(f"localhost:<port> is answered for {path}", get(path, f"localhost:{port}")[0] == 200)
            check(f"Host: evil.example is 403 on {path}", get(path, "evil.example")[0] == 403)
        for path in ("/api/report/1-flat/leak.md", "/api/report/1-flat/..%2Fstate.json",
                     "/api/report/1-flat/..%2F..%2F..%2F..%2Fetc%2Fpasswd", "/api/report/1-flat/../2-nested/state.json",
                     "/api/report/nope/design.md", "/api/report/1-flat/nope.md", "/api/report/1-flat/",
                     "/api/report/..%2Foutside/secret.md", "/nope", "/index.html"):
            check(f"GET {path} is 404", get(path)[0] == 404)
        check("Host: evil.example is 403 on an unknown path", get("/nope", "evil.example")[0] == 403)
        check("a POST is refused", get("/api/pipelines", method="POST")[0] == 405)
        check("a HEAD and an OPTIONS are refused",
              get("/api/pipelines", method="HEAD")[0] == 405 and get("/", method="OPTIONS")[0] == 405)
        check("GET /favicon.png is 200", get("/favicon.png")[0] == 200)
        for site in ("cross-site", "same-site"):
            for path in ("/api/pipelines", "/api/report/1-flat/design.md"):
                check(f"Sec-Fetch-Site: {site} is 403 on {path}",
                      get(path, headers=[("Sec-Fetch-Site", site)])[0] == 403)
        for site in ("same-origin", "none"):
            check(f"Sec-Fetch-Site: {site} is answered",
                  get("/api/pipelines", headers=[("Sec-Fetch-Site", site)])[0] == 200)
        check("a page of another site still opens the board itself",
              get("/", headers=[("Sec-Fetch-Site", "cross-site")])[0] == 200)
        check("a delivery under a linked tree is not served", get("/api/report/v-pipelines/private.md")[0] == 404)
        check("a POST from another host is 403", get("/api/pipelines", "evil.example", "POST")[0] == 403)
        status, body = get("/api/pipelines")
        check("/api/pipelines lists the deliveries", status == 200 and
              sorted(d["key"] for d in json.loads(body)["deliveries"]) == sorted(by))
        with open(os.path.join(REPO, "docs", "design", "tokens.json"), encoding="utf-8") as f:
            background = json.load(f)["color"]["surface"]["background"]
        check("/tokens.css gives the background as tokens.json does",
              f"--surface-background: light-dark({background['light']}, {background['dark']});" in get("/tokens.css")[1])
        check("the CSP and nosniff are on every answer", all(c == CSP and n == "nosniff" for _, _, c, n, _ in seen))
        check("no answer carries the link's file or the registry's key",
              not any(m in b for *_, b in seen for m in (leak_marker, key_marker, secret_marker, command_marker)))
    finally:
        server.shutdown()
        server.server_close()


def main():
    parser = argparse.ArgumentParser(description="A read-only board of the pipeline skill's deliveries.")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    server = Board(args.port, REPO, config_dir(), REPO)
    print(f"http://127.0.0.1:{server.server_address[1]}/  (read-only; Ctrl+C stops it)", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
