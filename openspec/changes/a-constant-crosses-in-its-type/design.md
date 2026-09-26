# Design

## How Dart does it

No row of `docs/FLUTTER-PARITY.md` applies: a Dart `const` is a value of its own type everywhere it is read, which is what this change makes a C# constant on this side.

## Context

A constant's value reached JavaScript through two writers. `InlinedConstantStrategy.TryResolveConstant` wrote a const FIELD reached through its type, and answered "not mine" for a decimal and for a long past 2^53, on the assumption that dedicated strategies kept those in their compat types. `ObjectCreationStrategy.ParameterDefaultLiteral` wrote a parameter's default for a skipped argument, with `Convert.ToString` for everything it did not name. A decimal literal was a third, textual path. The primitive constant table held entries for constants the inlining strategy always reached first.

## Decisions

### One writer, in the constant's C# type

`ConstantLiteral.Write(value, type)` answers a constant as the JavaScript value of its C# type, and every path that writes a constant's value goes through it. It is pure, so a strategy can ask it in `CanConvert`; the overload that takes the context registers the runtime import a decimal needs.

- A `decimal` is `$eq.num.dec("<invariant text>")`: the text keeps the scale (`1.50`) and every one of `decimal.MaxValue`'s 29 digits.
- A `long` or a `ulong` is a BigInt literal whatever its size. The old rule kept a long as a number when a double held it exactly, which is right for its digits and wrong for its type: the first long it met threw.
- A native-sized integer stays a number, whichever box its constant arrived in.

### Reached through its type, or by a bare name with no source

`InlinedConstantStrategy` claimed member accesses only. A bare name reaches a const of another type through `using static`, where the identifier strategy writes `Owner.member`: right for a const of the app's own source, whose module declares the static, and a ReferenceError for one from the BCL or a referenced assembly. The strategy claims a bare name only in the second case.

### A decimal literal is its value

The literal strategy wrote a decimal from the token's text, which handed the runtime a digit separator. It writes the value the parser read, through the same writer.

### The constant table keeps what is not a constant

With every constant written first by the inlining strategy (priority 25), the primitive table (priority 12) keeps `bool.TrueString` and `bool.FalseString`, which are `static readonly`, and is renamed after what it holds.

### The audit grades decimal

`typeof(decimal)` joins the audit's static surfaces. The nine members it grades as translated are claims, each proved on both sides; the 38 it fences are a build error, not a wrong answer, and #449 takes them up.

## Risks / Trade-offs

- [A twin that read a long constant as a number now reads a BigInt] → That number met the first long as a TypeError; none of the six shared twins reads one, and their pins are unchanged.

## Not here

- The 38 fenced members of `decimal`'s static surface (#449).
- The static surfaces of `float` and of the narrow integers, which the audit does not grade yet.
