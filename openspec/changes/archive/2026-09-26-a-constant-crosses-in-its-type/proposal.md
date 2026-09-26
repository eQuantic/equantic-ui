# Proposal

Closes #444, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A constant of `decimal` reaches the browser as a name nothing defines. `return Math.Round(decimal.MaxValue, 2).ToString();` emits `return String(decimal.maxValue.round(2));`, and bun fails with `ReferenceError: decimal is not defined`. `InlinedConstantStrategy.TryResolveConstant` writes a `const` field as its value, and its `decimal` case answers "not mine" on the assumption that a dedicated strategy writes decimals: none writes a decimal const FIELD, only a decimal literal. Measured on main with the conformance harness, the reach is wider than `decimal`'s five constants:

- The same function writes a `long` constant in a number's range as a plain number, so `t / TimeSpan.TicksPerSecond`, `TimeSpan.TicksPerDay * 2` and a record's `const long` added to a long throw "Invalid mix of BigInt and other type".
- A decimal literal is written from its TEXT, so `1_000.5m` reaches the runtime as `$eq.num.dec("1_000.5")` and fails.
- A parameter's default filled in for a skipped argument (`F(b: 2)`) goes through a second writer, which writes a decimal or a long default as a number, a float as its own shortest text, a char with no quotes (a bare identifier) and a string with a quote in it as a broken literal.

The BCL audit graded no static member of `decimal`, which is why nothing noticed. A const declared in the component's own source keeps working, through the static its module declares.

The author's review of this change, run before Copilot's, found three more on the same paths, each measured on both sides:

- A decimal constant in a pattern or a case label compared by `===`, which never matches the runtime's Decimal, an object: `d is 1m` and `case 1m:` answered false on main, and `d is decimal.One` would have gone from a ReferenceError to the same silent false.
- A constant whose TYPE is an enum was written as its underlying number (`Cal.First.ToString()` answered null), and a skipped default of a `[Flags]` enum as a member's name, where a flags enum is a number.
- The writer's escaping left a lone surrogate raw, which UTF-8 cannot encode, so the module could not be written.

## What Changes

- **One writer** for a constant's value in its C# type, `ConstantLiteral`: a `decimal` as the runtime's exact Decimal from its invariant text, scale kept; a `long` or a `ulong` as a BigInt whatever its size; a `float` as the double it is; a `char` and a `string` as escaped text.
- **An inlined const goes through it**, reached through its type (`decimal.MaxValue`, `Decimal.MaxValue`, `TimeSpan.TicksPerSecond`) or, for a const with no source to emit a twin from, through its bare name under a `using static`. A const of the app's own source reached by its bare name keeps its reference.
- **A decimal literal is its value**, as the parser read it, and a negated decimal constant folds through the same writer.
- **A skipped parameter's default** is the constant in the parameter's type, for a creation and for an invocation alike.
- The primitive constant table keeps only what is not a constant (`bool.TrueString`, `bool.FalseString`): every other entry could never answer, since the inlining strategy runs first.
- A narrow integer (`byte`, `sbyte`, `short`, `ushort`, `uint`) and a `ulong` annotate in their JavaScript type, `number` and `bigint`, where each reached TypeScript verbatim in a constant's own static.
- **A constant of an enum type** is the enum's representation, inlined or as a skipped default: a `[Flags]` enum's number, any other enum's camelCase member name.
- **A decimal constant matches by value** in a constant pattern, a switch expression's arm and a switch statement's case label, which a decimal label turns into the if/else chain.
- **A constant's text escapes** a lone surrogate, a control character other than the tab and a line separator, and a string literal takes the same writer.
- The BCL audit grades `decimal`'s static surface. Its nine translated members are proved on both sides: the five constants, `Parse` (already proved by `DecimalConversionConformanceTests`) and the three `Round` overloads. The 38 it fences stay fenced, left to #449.

For a developer using the SDK: `decimal.MaxValue`, `MinValue`, `Zero`, `One` and `MinusOne` work, a `long` constant such as `TimeSpan.TicksPerSecond` mixes with longs, a decimal literal with digit separators works, a skipped parameter's default keeps its type, a decimal constant matches in a pattern or a `case`, and a constant of an enum type is that enum's. Nothing is written differently.

The part reached is eqc, with its compiler and conformance tests and the audit's baseline. No runtime code changes, the six shared twins are unchanged, and the public surface of eqc does not move (`ConstantLiteral` and the changed signatures are internal).

## Capabilities

### New Capabilities

- `transpiler-constants`: how a compile-time constant reaches JavaScript, in its C# type.

### Modified Capabilities

None.

## Impact

`src/eQuantic.UI.Compiler` (the inlining strategy, the literal and unary strategies, the parameter default writer shared by creations and invocations, the pattern converter and the switch statement strategy, the primitive static strategy's table and the TypeScript type mapping), tests in the compiler and conformance suites, the BCL audit's baseline, `docs/LEDGER.md`, and the wiki's SupportedFeatures page in English and Portuguese.
