# Tasks

## 1. A value hashes as it equals

- [x] 1.1 Add the runtime's hash, agreeing with `$eq.equals` case by case, and a `getHashCode` on the runtime types that carry their own `equals`. Verify: `hash.spec.ts` hashes equal values alike for every kind, and a class's identity apart
- [x] 1.2 Lower `GetHashCode()`, `base.GetHashCode()` and `HashCode.Combine` by the bound method. Verify: `HashConformanceTests.AHash_AgreesWithEquals` runs every kind on both sides, comparing two hashes, and fails against main

- [x] 1.3 Register every expression strategy the compiler declares. Verify: `StrategyRegistrationTests` fails with the hash strategy left out, and passes with it

## 2. A Guid is its canonical text (#459)

- [x] 2.1 Read a Guid made from text through the runtime's canonical reader, by the bound symbol, and move the strategy onto the IR. Verify: `HashConformanceTests.AGuid_IsItsCanonicalText` runs `==`, `ToString`, a dictionary key, a set, `TryParse`, a refusal and the hash on both sides, and fails against main

## 3. A date leaving the calendar is refused (#424)

- [x] 3.1 Refuse in `add`, `subtract`, `addMonths` and `addYears` with each member's parameter, pass `t` from the operators, and lower a compound date assignment through the twin. Verify: `DateTimeConformanceTests.DateArithmetic_LeavingTheCalendar_IsRefusedAsDotNetRefusesIt` compares every message on both sides, and fails against main

## 4. Documentation

- [x] 4.1 The wiki's SupportedFeatures page in English and Portuguese, on the pull request's wiki branch, and one `docs/LEDGER.md` line citing #519, #459 and #424
