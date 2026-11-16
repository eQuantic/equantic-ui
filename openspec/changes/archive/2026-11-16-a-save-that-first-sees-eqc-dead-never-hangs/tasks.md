# Tasks

## 1. The proof

- [x] 1.1 A test kills eqc through a handle of its own and holds the compiler's process lock from before the kill, as the watcher's exit event holds it, while the next save runs. On the code before the cure the compiler's gate is not free in 5 seconds
- [x] 1.2 A test kills eqc and saves again for 60 rounds, each under a deadline, and fails naming the round that hung. On the code before the cure one run in five hung at its 60th round on macOS
- [x] 1.3 The dead-eqc tests run 50 times under a two-minute hang limit on macOS and on Linux, with none hung

## 2. The cure

- [x] 2.1 The save asks the eqc whether it exited, and releases it, outside the gate
