# Proposal

Closes #1491, a Bug under #1174, reopened after #1496.

## Why

A server in development whose eqc died could still hang the next save for good after #1496. The
compiler asked the running eqc whether it had exited while it held its gate. When nothing has seen the
exit yet, that question raises the process's exit event, and raising it takes the process's own lock. The
process watcher's exit event runs inside that same lock and reads the compile in flight through the
gate. When a save was the first to see eqc dead as the watcher raised the exit, each held the lock the
other waited for. #1496 moved the release of the dead eqc out of the gate. Its test let the process see
the exit before the save looked, so it could not catch the question. CI kept meeting the hang as a
Server suite that stopped for its 15 minutes in `AnEqcThatDied_IsStartedAgainByTheNextSave`, with #1496
in, and the hang ejected a pull request from a merge group.

## What Changes

- A save reads the running eqc under the gate, and asks whether it exited and releases it with the gate
  free. No member of the eqc's process is called while the gate is held.

## Impact

- `src/eQuantic.UI.Server/HotReload/RunningCompiler.cs`. No API changes, and an app writes nothing new.
