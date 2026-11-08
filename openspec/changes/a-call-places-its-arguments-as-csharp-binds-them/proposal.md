## Why

Closes #1071 (under #849). Three paths still place a call's arguments from its syntax: a vocabulary type's constructor whose twin is the runtime's own (`ObjectCreationStrategy.OrderedArguments`), the constructor of a twin eqc writes where no bound tree answers (`ConstructorArguments`), and an invocation (`InvocationStrategy`). Two defects outlived #1070 there:

- **Evaluation order.** Named arguments were reordered into parameter order and passed so: `Calls.Pair(b: Seq.Int(2), a: Seq.Int(1))` ran `Seq.Int(1)` first in the browser and `Seq.Int(2)` first in .NET, and `new TypeStyle(LineHeight: …, Size: …)` the same.
- **An out argument.** An invocation converts no `out` argument, then paired `ArgumentList.Arguments[i]` with the converted list's `i`, so the argument after an `out` one took the next argument's value: `Try(out var found, b: 7)` passed `7` as `a`.

## What Changes

- `BoundArguments.InPlaces` places each written argument by its index in the list written, never by its position in a list of what was converted, so an `out` argument shifts nothing; an `out` parameter takes no place, as the twin's signature has none.
- `BoundArguments.PassedAsWritten` passes the placed arguments in parameter order and evaluates them in the order written: where those differ and an argument does anything, each such argument is evaluated into a temporary, in the order written, ahead of the first argument passed, which keeps one argument per slot for the callers that count them (`string.repeat`, a trailing config slot). Where nothing is open to declare a temporary (a field's initializer, a constructor's base call), the parameter order stands, as before.
- The three paths go through both.

## Capabilities

### Modified Capabilities

- `transpiler-expressions`: a call evaluates its arguments in the order they are written, whatever path places them.

## Impact

- Reaches `eQuantic.UI.Compiler` (`BoundArguments`, `ObjectCreationStrategy`, `InvocationStrategy`). No public surface moves.
