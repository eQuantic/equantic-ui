## 1. Measured

- [x] 1.1 Read the token text of each spelling the way the arm before #527 wrote it, in the embedded Bun and in Node. Verify: `'\a'`, `'\e'`, `'\x7'`, `'\x007'` and `'\x0041'` read otherwise than C#, and `'\0'`, `' '` and `'A'` alike

## 2. Every spelling on both sides

- [x] 2.1 Add one case per spelling to `LiteralConformanceTests.ACharLiteral_IsItsValueAndNotItsSpelling`. Verify: the Literal cases pass on both sides, and on the arm that wrote the token text the five spellings JavaScript reads otherwise fail with the seven cases #527 added, while `'\0'`, `' '` and `'A'` pass

## 3. The requirement names them

- [x] 3.1 Add the scenario to "A char literal is its value". Verify: `./scripts/check-openspec.sh` passes

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #1165, as a file of its own under `docs/ledger/`
