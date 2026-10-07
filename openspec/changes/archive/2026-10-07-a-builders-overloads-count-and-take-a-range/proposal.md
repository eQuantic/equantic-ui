# Proposal

Closes #650, a sub-issue of #565 (the transpiler's fences hold on every path).

## Why

The runtime's `StringBuilder` had one shape per method, and eqc passes every overload's arguments to it as they are written. So the overloads that count or take a range ignored what they were given: `sb.Append('x', 3)` appended `x` where .NET appends `xxx`, `Append(text, 2, 3)` and `Append(chars, 2, 3)` appended the whole value, `Insert(0, "ab", 2)` inserted `ab` once, `Replace` over a range replaced everywhere and `ToString(1, 2)` returned the whole text. A `char[]` was written as JavaScript's text of an array, `a,b`, and a null as `null`. Nothing failed at build time, the page just printed something else.

## What Changes

- **The runtime's builder takes each of .NET's overload shapes by its count of arguments**, which C# fixes per overload: `append(value)`, `append(char, repeatCount)`, `append(value, startIndex, count)` for a string or a builder, `insert(index, value)` and `insert(index, string, count)`, `replace` with a range, `remove` and `toString(startIndex, length)`. A null appends and inserts nothing, in every overload.
- **The `char[]` overloads of `Append` and `Insert` are methods of their own**, `appendChars` and `insertChars`, which eqc names from the overload the call binds: the runtime cannot tell a null array from a null string, and .NET refuses the two in different words.
- **Every refusal is .NET's, its checks in .NET's order**: a negative count or start with its actual value, a range past the end naming the parameter .NET names, a null value with anything to take, an empty or null old value in `Replace` before its range, `Remove`'s length before its start.

For a developer using the SDK: a builder's counted and ranged overloads write what .NET writes, and refuse what it refuses. Nothing an app writes changes, and the public surface does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a builder's counted and ranged overloads answer as .NET's do.

## Impact

- eqc: `StringBuilderStrategy` names the `char[]` overloads.
- The runtime: `utils/string-builder.ts`; `utils/string-statics.ts` shares `requireNonNegative` and the text of a range past the end.
- Tests: `StringBuilderConformanceTests`, both sides; `StringBuilderStrategyTests`; `string-builder.spec.ts`.
- Found on the way and filed apart: the builder members the runtime does not have (`AppendFormat`, `AppendJoin`, `Capacity`, the indexer, `Length`'s setter and the `IFormatProvider` overloads).
