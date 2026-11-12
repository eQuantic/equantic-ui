# Tasks

## 1. The for head as a local declaration

- [x] 1.1 The head's declarators go through the local declaration's annotation, in the head and in a hoisted loop. Verify: `EmittedChain` in the emission fixtures writes `let at: EmittedChain | null = this`
- [x] 1.2 A nullable or `this`-initialized declaration says its type though the initializer has the same one. Verify: the runtime's `tsc` reads `EmittedChain` clean, and fails with `TS2322: Type 'EmittedChain | null' is not assignable to type 'this'` on main's `let at = this`; no transpiled component of the runtime moves

## 2. Proved against the real thing

- [x] 2.1 The suites. Verify: the pull request's CI, 23 checks on Ubuntu, Windows and macOS (Compiler, Web, Server, Conformance, the runtime's `tsc` and vitest, the samples) green on 3c585bd3f; the Release's subsection 2 found `tsc` clean and the pins 15 of 15 against E3b-1
- [x] 2.2 A ledger entry under `docs/ledger/`
