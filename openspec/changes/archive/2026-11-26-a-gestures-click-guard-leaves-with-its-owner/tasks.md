# Tasks

## 1. The guard

- [x] 1.1 One owned click guard: arm on a held host, go with the click, with the owner, or after its time
- [x] 1.2 The context controller owns a hold's guard, and its reset lets go of it
- [x] 1.3 Each gesture controller's delegate owns its drags' guards, handed to `followPointer`, and its reset lets go of them
- [x] 1.4 The table viewport's pan guard (#1888) moves onto the same helper
- [x] 1.5 The gesture arena's controller owns a lost press's guard: clicks inside the press only, its time from the arena's end, and its reset lets go of every guard

## 2. Proof

- [x] 2.1 The guard's own spec: one click stopped, the next heard, its timer gone; expiry; an owner lets go of its own and of no other's
- [x] 2.2 A hold's guard goes with its click, and with the controller's reset; both fail with main's code
- [x] 2.3 A drag's guard goes with its click, and with the controller's reset; both fail with main's code
- [x] 2.5 A lost press's guard goes with its click, and with the arena's reset after the press ended; both fail with main's code
- [x] 2.6 The guard stops only what it accepts, and waits with no time until its owner starts it
- [x] 2.4 The runtime suite runs repeatedly without the error (five runs in a row)

## 3. Documentation

- [x] 3.1 One entry under `docs/ledger/`
