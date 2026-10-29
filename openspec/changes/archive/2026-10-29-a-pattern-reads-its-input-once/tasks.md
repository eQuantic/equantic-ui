# Tasks

## 1. The input of a pattern

- [x] 1.1 The writer reads a template's holes in its code only, a string spelling `{0}` left as it is
      (`JsTemplateTests.AStringThatSpellsAHole_IsLeftAsItIs`)
- [x] 1.2 `IsPatternStrategy` converts the input once, writes the test over a placeholder and hands the writer
      a template whose hole is each place the test's code names it; `Once` and `IsPath` go
- [x] 1.3 An input the test never names is evaluated before it, unless it is a name or a literal
- [x] 1.4 Check, both sides executed: a getter that counts its reads answers as .NET does through `or`, `and`,
      `not`, property, positional and type patterns, a binding, a member of `this` and the binary type test, in
      TypeScript and in JavaScript (`APatternReadsAMemberInputOnce`), and a call through every shape, the switch
      forms and `is var _` (`APatternReadsItsInputOnce`); with the strategy as it was, the getter cases and
      `is var _` fail
- [x] 1.5 The transpiled pins regenerated, and their diff read: only inputs read twice change

## 2. Documentation

- [x] 2.1 The wiki's SupportedFeatures page, in its five languages, says a pattern reads its input once
- [x] 2.2 One ledger entry citing #1149, as a file of its own under `docs/ledger/`
