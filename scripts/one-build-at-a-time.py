#!/usr/bin/env python3
"""Run one command while holding this host's build lock, so parallel deliveries never build at once.

Several pipeline deliveries can run on one machine, each in a worktree of its own (#1526). Reading
and writing in parallel is cheap; a build or a test suite is what loads the machine, so every build
and suite of every delivery goes through this one lock, whichever worktree it runs in:

    python3 scripts/one-build-at-a-time.py -- dotnet test tests/eQuantic.UI.Web.Tests

It waits for the lock, saying so on stderr every minute it waits, runs the command and exits with
its exit code (128 + N when a signal N ended it). A signal the wrapper receives is passed on to the
command, so the lock is never free while the command it guards still runs. The lock is a file under
the user's cache (`EQ_BUILD_LOCK` names another, made absolute), held with `flock`, so a process
that dies releases it. A command with shell syntax is one argument to a shell:

    python3 scripts/one-build-at-a-time.py -- sh -c 'EQ_UPDATE_TRANSPILED=1 dotnet test tests/X'

The wait counts against the caller's own timeout: run a wrapped suite in the background, or with
the longest timeout the caller allows. Where `fcntl` does not exist (Windows), it runs the command unlocked
and says so: it serialises, it never stops a build.

    python3 scripts/one-build-at-a-time.py --self-test
"""
import os
import signal
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
        return os.path.abspath(named)
    cache = os.environ.get("XDG_CACHE_HOME") or os.path.join(os.path.expanduser("~"), ".cache")
    return os.path.join(cache, "equantic-ui", "one-build-at-a-time.lock")


def guarded(command):
    """The command's exit code, with every signal the wrapper gets passed on to it first."""
    child = subprocess.Popen(command)
    forwarded = (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)
    for number in forwarded:
        signal.signal(number, lambda n, _: child.send_signal(n))
    code = child.wait()
    return 128 - code if code < 0 else code


def run(command):
    if fcntl is None:
        print("one-build-at-a-time: no flock on this system, running unlocked", file=sys.stderr)
        return subprocess.call(command)
    path = lock_path()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "a+") as handle:
        waited = 0
        while True:
            try:
                fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                if waited % 60 == 0:
                    print(f"one-build-at-a-time: another build holds {path}, waiting ({waited}s)",
                          file=sys.stderr, flush=True)
                time.sleep(1)
                waited += 1
        return guarded(command)


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
        code = subprocess.call([sys.executable, me, "--", "sh", "-c", "kill -TERM $$"], env=env)
        if code != 128 + signal.SIGTERM:
            failures.append(f"a command ended by SIGTERM came back as {code}, not {128 + signal.SIGTERM}")
        # A signal to the wrapper reaches the command: the command's trap writes, then it exits.
        trapped = os.path.join(scratch, "trapped")
        wrapper = subprocess.Popen([sys.executable, me, "--", "sh", "-c",
                                    f"trap 'echo got > {trapped}; exit 0' TERM; sleep 5 & wait"],
                                   env=env)
        time.sleep(0.5)
        wrapper.send_signal(signal.SIGTERM)
        wrapper.wait(timeout=5)
        if not os.path.exists(trapped):
            failures.append("a SIGTERM to the wrapper did not reach the command it runs")
        relative = subprocess.call([sys.executable, me, "--", "true"], cwd=scratch,
                                   env=dict(os.environ, EQ_BUILD_LOCK="relative.lock"))
        if relative != 0 or not os.path.exists(os.path.join(scratch, "relative.lock")):
            failures.append(f"a relative EQ_BUILD_LOCK did not lock beside the cwd (exit {relative})")
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
