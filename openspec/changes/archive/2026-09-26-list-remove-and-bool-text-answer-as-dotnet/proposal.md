# Proposal

Closes #400 and #402, Bugs under #164 (The transpiler's fences hold on every path).

## Why

Two BCL members answered differently in the browser than in .NET, and the build said nothing.

- `list.Remove(item)` lowered to `((_idx = list.indexOf(item)) >= 0 && list.splice(_idx, 1))`, which
  assigns a name nothing declares, so every call threw `ReferenceError: _idx is not defined` in a
  module. The documentation site's "you are here" stayed on the first heading of every page. It would
  have answered the spliced array where C# answers a bool, and no conformance case called it.
- `bool.Parse(s)` lowered to `String(s).trim().toLowerCase() === 'true'`: it never threw, a null was
  the text "null", a trailing NUL (which .NET trims) made "true\0" false, and JavaScript's `trim()`
  is not `char.IsWhiteSpace`. `bool.TryParse` had no translation at all, and `Convert.ToBoolean(string)`
  shared the comparison.

## What Changes

- **`List<T>.Remove`** goes through the runtime (`$eq.collections.remove`): it takes out the first
  item equal to the value, by `EqualityComparer<T>.Default`'s rule (a type's own `Equals`, a double's
  where NaN equals NaN, identity otherwise), and answers whether there was one. The list and the item
  are each evaluated once, in the order C# evaluates them.
- **`bool.Parse`, `bool.TryParse` and `Convert.ToBoolean(string)`** read the text as .NET's
  `Boolean.TryParse` does (`$eq.bool.*`): "True" or "False" in any case, trimmed of white space and
  NUL characters at both ends, white space being `char.IsWhiteSpace`'s set. Parse throws .NET's
  FormatException and ArgumentNullException with their words, TryParse answers false and writes
  false to its `out`, and `Convert.ToBoolean(null)` is false. The case fold is ASCII only, because
  .NET folds nothing else onto "True" or "False" (a long s is not an s there).
- `bool.Parse` and `bool.TryParse` join the numeric `Parse`/`TryParse` lowering, which already
  handles an `out` with an effect, a discard and a named argument. `BooleanMethodStrategy` goes.
- The runtime's number reader uses the one white space list (`utils/white-space`), where it kept a copy.

For a developer using the SDK: `list.Remove` works in the browser and answers a bool, and a bool read
from text answers and fails as it does on the server. Nothing is written differently.

The parts reached are eqc and the runtime. The public surface moves: `BooleanMethodStrategy` is
removed, and the `Eq` constants `ListRemove`, `BoolParse`, `BoolTryParse` and `BoolConvert` are added.
The developer surface does not move.

## Capabilities

### New Capabilities

- `transpiler-bcl`: what a BCL member answers in the browser, which is what it answers in .NET.

### Modified Capabilities

None.

## Impact

`src/eQuantic.UI.Compiler` (the list, number and convert strategies, and the strategy registry), the
runtime's collections, a new `boolean-text.ts` and the number reader, the BCL audit baseline, and
tests in the conformance and compiler suites.
