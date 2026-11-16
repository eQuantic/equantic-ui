# Proposal

Closes #1491, a Bug under #1174.

## Why

A server in development whose eqc died could hang the next save, and every save after it. The compiler
released the dead eqc while it held its gate, and releasing a process takes the process's own lock.
The process's exit event runs inside that lock and reads the compile in flight through the gate. When
the exit was raised as the next save found the dead eqc, each side held the lock the other waited for.
CI met it as a Server suite that stopped for its 15 minutes in
`RunningCompilerTests.AnEqcThatDied_IsStartedAgainByTheNextSave`.

## What Changes

- A save that finds eqc dead takes it out under the gate and releases it outside, as stopping the app
  already releases the running one.

## Impact

- `src/eQuantic.UI.Server/HotReload/RunningCompiler.cs`. No API changes, and an app writes nothing new.
