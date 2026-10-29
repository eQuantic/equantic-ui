# Tasks

## 1. A pattern's member read is a member access

- [x] 1.1 `PatternConverter` writes each member of a subpattern's path as a member access on a placeholder,
      handed to the converter, and its own table goes
- [x] 1.2 The type of the value a recursive pattern reads from is the bound pattern's, for its members and its
      positional parts, and `accessType` goes from `BuildCondition`, `CollectBindings` and their callers
- [x] 1.3 Check: `PatternConformanceTests.AMemberInAPattern_ReadsAsItsMemberAccessReadsIt` executed on both
      sides, the nested, positional, list, tuple and dictionary cases failing before the change, and
      `AMemberNoBrowserValueCarries_IsRefusedInAPatternAsInAMemberAccess` refused with EQ2004, accepted before

## 2. Documentation

- [x] 2.1 One ledger entry citing #1147 and #920, as a file of its own under `docs/ledger/`
