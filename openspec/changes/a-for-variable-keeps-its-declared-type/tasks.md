# Tasks

## 1. The for head as a local declaration

- [x] 1.1 The head's declarators go through the local declaration's annotation, in the head and in a hoisted loop. Verify: `EmittedChain` in the emission fixtures writes `let at: EmittedChain | null = this`
- [x] 1.2 A nullable or `this`-initialized declaration says its type though the initializer has the same one. Verify: the runtime's `tsc` reads `EmittedChain` clean, and fails with `TS2322: Type 'EmittedChain | null' is not assignable to type 'this'` on main's `let at = this`; no transpiled component of the runtime moves

## 2. Proved against the real thing

- [ ] 2.1 The suites. Verify: Compiler, Web and Conformance pass, `tsc` is clean and the runtime's vitest passes
- [x] 2.2 A ledger entry under `docs/ledger/`
