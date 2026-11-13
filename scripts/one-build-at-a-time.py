#!/usr/bin/env python3
"""Run one command while holding this host's build lock, so parallel deliveries never build at once.

Several pipeline deliveries can run on one machine, each in a worktree of its own (#1526). Reading
and writing in parallel is cheap; a build or a test suite is what loads the machine, so every build
and suite of every delivery goes through this one lock, whichever worktree it runs in:

    python3 scripts/one-build-at-a-time.py -- dotnet test tests/eQuantic.UI.Web.Tests

It waits for the lock, says so once on stderr, runs the command and exits with its exit code. The
lock is a file under the user's cache (`EQ_BUILD_LOCK` names another), held with `flock`, so a
process that dies releases it. Where `fcntl` does not exist (Windows), it runs the command unlocked
and says so: it serialises, it never stops a build.

    python3 scripts/one-build-at-a-time.py --self-test
"""
import os
import subprocess
import sys
import tempfile
import time

try:
    import fcntl
except ImportError:  # Windows
    fcntl = None


def lock_path():
    named = os.environ.get("EQ_BUILD_LOCK")
    if named:
        return named
    cache = os.environ.get("XDG_CACHE_HOME") or os.path.join(os.path.expanduser("~"), ".cache")
    return os.path.join(cache, "equantic-ui", "one-build-at-a-time.lock")


def run(command):
    if fcntl is None:
        print("one-build-at-a-time: no flock on this system, running unlocked", file=sys.stderr)
        return subprocess.call(command)
    path = lock_path()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "a+") as handle:
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print(f"one-build-at-a-time: another build holds {path}, waiting", file=sys.stderr)
            fcntl.flock(handle, fcntl.LOCK_EX)
        return subprocess.call(command)


def self_test():
    if fcntl is None:
        print("one-build-at-a-time self-test: no flock here, nothing to test")
        return 0
    failures = []
    with tempfile.TemporaryDirectory() as scratch:
        env = dict(os.environ, EQ_BUILD_LOCK=os.path.join(scratch, "lock"))
        log = os.path.join(scratch, "log")
        # Each holder writes its start and end; held one at a time, no start falls inside another's span.
        step = f"echo start >> {log}; sleep 0.5; echo end >> {log}"
        me = os.path.abspath(__file__)
        started = time.monotonic()
        holders = [subprocess.Popen([sys.executable, me, "--", "sh", "-c", step], env=env,
                                    stderr=subprocess.DEVNULL) for _ in range(3)]
        codes = [h.wait() for h in holders]
        took = time.monotonic() - started
        lines = open(log).read().split()
        if codes != [0, 0, 0]:
            failures.append(f"exit codes {codes}")
        if lines != ["start", "end"] * 3:
            failures.append(f"the holders overlapped: {lines}")
        if took < 1.4:
            failures.append(f"three half-second holders took {took:.2f}s, so they ran together")
        code = subprocess.call([sys.executable, me, "--", "sh", "-c", "exit 7"], env=env)
        if code != 7:
            failures.append(f"the command's exit code 7 came back as {code}")
    for failure in failures:
        print(f"FAIL: {failure}")
    print(f"one-build-at-a-time self-test: {len(failures)} failure(s)")
    return 1 if failures else 0


def main(argv):
    if argv[:1] == ["--self-test"]:
        return self_test()
    if argv[:1] != ["--"] or len(argv) < 2:
        print(__doc__.strip().splitlines()[0], file=sys.stderr)
        print("usage: one-build-at-a-time.py -- <command> [args...] | --self-test", file=sys.stderr)
        return 2
    return run(argv[1:])


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
