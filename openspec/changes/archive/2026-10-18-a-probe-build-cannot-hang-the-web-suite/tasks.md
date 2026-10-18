# Tasks

## 1. Measured

- [x] 1.1 Build a scratch project as the probe does, on two pipes and under a handshake salt no idle node carries, in the shell's environment and in the test host's. Verify: when each pipe ends against the exit, and which process the build left holds a write end, read with lsof

## 2. One deadline, and a build that leaves nothing behind

- [x] 2.1 A case runs `/bin/sh -c 'sleep 30 & echo done'` with a deadline of two seconds and gives the run twenty. Verify: on the helper as it was, the run is still waiting after twenty seconds, and on its own it returns after thirty
- [x] 2.2 Hold the exit and both streams to one deadline in `ChildProcess.Run`, start `dotnet` in `ChildProcess.Dotnet` without the variable and with node reuse and the build servers off, and send `SdkProbe.Run` through it. Verify: the case of 2.1 fails in two seconds, naming the command
- [x] 2.3 A case builds a project with the variable given, every project built out of process, and a handshake salt of its own. Verify: on the helper as it was it holds the test host until the blame collector's timeout, its node alive and holding the pipes; with the fix it passes, and no process carrying its salt is left

## 3. Every dotnet a test starts

- [x] 3.1 Route the four other copies through the owner, link it into the native engine's tests, and drop the `-nr:false` switches of the Web cases. Verify: the affected cases of both suites pass
- [x] 3.2 Add the guard. Verify: on the tree before the routing it names the four copies, and it passes after

## 4. Proved against the real thing

- [x] 4.1 Run the Web suite whole five times in a row through the suite lock. Verify: each run's total, duration and exit code, and no run held

## 5. Documentation

- [x] 5.1 One entry for the ledger citing #971, as a file of its own under `docs/ledger/`
