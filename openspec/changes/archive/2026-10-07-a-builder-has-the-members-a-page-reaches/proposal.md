# Proposal

Closes #679, a sub-issue of #565 (the transpiler's fences hold on every path).

## Why

The BCL audit graded every `StringBuilder` member `native`, which only said a strategy answered. The runtime's builder had `append`, `appendLine`, `insert`, `remove`, `replace`, `clear`, `toString` and a `length` getter, and eqc wrote every other member under its own name: `AppendFormat`, `AppendJoin` and `EnsureCapacity` were TypeErrors in the browser, `Capacity` and `MaxCapacity` read undefined, `sb[0]` read a property no builder had and `sb[0] = 'x'` wrote one nobody read, and `sb.Length = 1` was a TypeError. `Append(CultureInfo.InvariantCulture, $"...")` handed the provider to the parameter the runtime reads as the value. Nothing failed at build time.

## What Changes

- **`AppendFormat` and `AppendJoin` append what `string.Format` and `string.Join` write**, by the same lowering, bound by the same parameters and under the same culture policy.
- **`Capacity` is .NET's.** The runtime keeps the sizes of the chunks .NET would allocate: an append fills the last chunk and opens one as large as the text so far, up to 8,000, or as what is left; an insert goes in place in a small chunk with room and into a chunk of its own anywhere else; a removal takes from the chunks it crosses; a longer replacement takes a chunk of its own; and a shorter `Length` keeps `min(Capacity, max(Length * 6 / 5, the last chunk))`. It matches .NET on 41 measured sequences. `MaxCapacity`, `EnsureCapacity`, the `Capacity` setter and the constructors that take a capacity or a maximum answer and refuse as .NET's do.
- **The `Chars` indexer is the twin's `item` and `setItem`**, through the place every writer takes, refused past the text as .NET refuses it.
- **`Length` cuts the text or fills it with `\0`**, `Equals(StringBuilder)` compares the text whatever the capacity (`Equals(object)` stays identity), and `CopyTo` copies into a `char[]`.
- **A member that .NET makes of several appends makes them as .NET does.** An interpolated `Append($"…")` or `AppendLine($"…")` appends each part in turn, as .NET's interpolation handler does, so a hole that reads the builder sees the parts before it (`$"{b.Length}{b.Length}"` held "00" where .NET holds "01"), each part grows the chunks, and a refusal keeps the parts before it; a hole prints as a plain interpolation's does, where an enum reached the builder as its camelCase key. `AppendLine(value)` appends the line's end after the value, `Append(StringBuilder)` checks the maximum before it copies and refuses in words of its own, and a `Replace` past the maximum keeps the chunks it replaced first.
- **A named argument is bound to its parameter**, evaluated in the order written: the twin takes C#'s parameters by position, and `CopyTo(count: 2, destination: a, …)` read the count as the source index.
- **A text longer than the browser's string can hold is .NET's `OutOfMemoryException`**, the builder unchanged: an engine's longest string is shorter than what .NET holds (2^29 characters in V8), and JavaScript's `RangeError` escaped every `catch (OutOfMemoryException)`.
- **What cannot cross is refused at the build**: a provider other than the current culture on `Append` and `AppendLine` (EQ2108, since an interpolation formats in the app's culture alone), `GetChunks`, and an overload over a span or a memory. A `params` span is the values written one by one and crosses.

For a developer using the SDK: every `StringBuilder` member a page reaches answers as .NET's does, or fails the build where it cannot. Nothing an app writes changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-bcl`: a builder has the members a page reaches.

## Impact

- eqc: `StringBuilderStrategy` (AppendFormat, AppendJoin, the interpolated appends part by part, `appendBuilder`, `equalsBuilder`, the named arguments, the refusals), `InterpolatedStringStrategy` (a hole's text, padded or for a writer that pads it), `ValueFlow` (a handler's plain hole flows into text), `Indexer` (the builder's indexer is carried by its twin), `StringStaticStrategy` (`FormatCall` and `JoinCall` shared).
- The runtime: `utils/string-builder.ts` (the chunk sizes, the members), `utils/exceptions.ts` (`IndexOutOfRangeException`).
- Tests: `StringBuilderConformanceTests`, both sides; `StringBuilderStrategyTests`; `string-builder.spec.ts`.
