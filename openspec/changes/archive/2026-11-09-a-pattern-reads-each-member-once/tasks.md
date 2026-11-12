# Tasks

## 1. A member a pattern reads

- [x] 1.1 `MatchParts` holds what a symbol reads off a value, a `Deconstruct`'s parts or a member's value, one
      temporary per value and symbol for the operation
- [x] 1.2 `PatternConverter.Read` holds a member whose read runs the app's code, in a box the first test that
      reaches it assigns, and writes every other member where it reads it
- [x] 1.3 `PropertyStore.IsKeptUnderItsName` names the rule `SlotOf` followed for an auto-property the twin
      keeps as a field, which the pattern asks too
- [x] 1.4 Check, both sides executed, in TypeScript and in JavaScript: a getter that counts its reads answers as
      .NET does through an `or` and an `and` of property patterns, a binding, the alternatives of a nested
      pattern, an extended path, a null it answers, the arms of a switch expression and the sections of a switch
      statement (a guard that fails and an arm that stops before it included), a member of `this`, an element of
      a list pattern, a part of a positional pattern, an interface's property, an abstract one, a virtual
      auto-property an override counts, and each evaluation in a loop (`APatternReadsEachMemberOnce`); with the
      converter as it was, every case fails
- [x] 1.5 A field and an auto-property are written where the pattern reads them
      (`AMemberNobodyCanSeeRead_IsWrittenInPlace`), and the transpiled pins regenerated, their diff read

## 2. Documentation

- [x] 2.1 The wiki's SupportedFeatures page, in its five languages, says a pattern reads each member it tests once
- [x] 2.2 One ledger entry citing #1150, as a file of its own under `docs/ledger/`
