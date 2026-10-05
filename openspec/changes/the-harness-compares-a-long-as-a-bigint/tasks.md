# Tasks

## 1. The harness writes a value as the runtime holds it

- [x] 1.1 Measure what each side printed on main, kind by kind: a long, a ulong, a decimal, an enum, a double, a float, a tuple, a pair, a date, a char, and a long in a case that imports nothing
- [x] 1.2 `RuntimeJson`: the converters the .NET side writes with, and `NumberText`, JavaScript's `Number::toString` over .NET's shortest digits
- [x] 1.3 `ConformanceRunner.Print`: one printing statement for every assert, its replacer writing a BigInt as the runtime's `toJSON` does
- [x] 1.4 Prove it: `ReturnedValueConformanceTests` (37 cases, 33 of them failing against main's harness, the other 4 neighbours kept as pins), `HarnessSelfChecksTests.AWrongRepresentation_Fails` (a wrong representation fails, the right one matches) and `NumberTextTests` (the writer against bun's `String` on about 5,000 doubles by their bits)

## 2. A DateTimeOffset's unix times

- [x] 2.1 Re-run the whole conformance suite under the new harness: one case newly fails, `ToUnixTimeSeconds()`, a long the runtime answered as a JS number
- [x] 2.2 Answer a BigInt from `toUnixTimeSeconds` and `toUnixTimeMilliseconds`, cut from 0001-01-01 before the epoch is taken off
- [x] 2.3 Prove it on both sides in `DateTimeOffsetConformanceTests`: an instant one tick and 999 milliseconds before the epoch, and a unix time multiplied by a long

## 3. Documentation and the suites

- [x] 3.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 3.2 `docs/LEDGER.md`: one line citing #596
- [x] 3.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
