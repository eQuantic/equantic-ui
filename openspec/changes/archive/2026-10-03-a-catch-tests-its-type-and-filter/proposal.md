# Proposal

Closes #474 and #539, sub-issues of #164.

## Why

Measured through the conformance runner, both sides executed (#474): each catch clause was its own
JavaScript `catch`, so two clauses were a SyntaxError that cost the module, and one clause took every
exception, its type never tested and its `when` filter dropped. An exception carried no type at all,
so a type pattern over one was a null check, and `throw;` was a SyntaxError.

Five lowerings wrote a JavaScript function around a C# expression (#539): `checked(…)`, a `throw`
expression, `Trim` with characters, `Enumerable.Range` and `Repeat`. An `await` in it did not parse,
and `Range(Start(), 3)` called `Start` three times and answered `1,3,5`.

## What Changes

- A .NET exception in the browser is an `Error` that carries its .NET types, the most derived first
  (`$eq.exceptions`): built by `new T(…)` from T's symbol, an app's own exception class and the
  target-typed `new(…)` included, and thrown with .NET's type by the runtime wherever it throws on
  .NET's behalf (an overflow, a missing key, a bad format). A JavaScript `TypeError` reads as the
  `NullReferenceException` it is in a type-checked program, and any other untyped error as an
  `Exception` and nothing more specific.
- The catch clauses of a `try` are ONE JavaScript catch that tries them in order, each by its type,
  the test a type pattern writes, and by its filter, which runs as .NET runs it: a filter that throws
  has answered false. A filter declares its own variables, an exception no clause takes is thrown on,
  and `throw;` rethrows the exception caught. A clause that takes everything, alone, is the catch it
  always was.
- `checked(…)` and `unchecked(…)` are their operand, which already settles each operation under the
  bound tree's context. A `throw` expression hands its exception to `$eq.exceptions.raise` as an
  argument. `Trim`'s characters and `Range`'s and `Repeat`'s arguments go to the runtime as arguments,
  evaluated once, in the order C# evaluates them; `Trim` of no characters trims white space, and
  `Range` and `Repeat` refuse a negative count, as .NET does.
- A guard counts the functions the lowerings still write by hand, per file, against a baseline that
  may only shrink.

What a developer sees: a `catch` takes what it names and nothing else, a filter decides, two clauses
compile, `throw;` rethrows, and an `await` inside `checked`, a throw expression, `Trim`, `Range` or
`Repeat` works. One difference stays, the platform's: .NET runs a filter before the `finally` blocks
between the throw and the clause, and JavaScript has unwound them by the time its catch runs.

## Capabilities

### New Capabilities

- `transpiler-exceptions`: what an exception is in the browser, how a catch clause takes it, and how
  a filter, a rethrow and a type pattern over an exception run.

### Modified Capabilities

- `transpiler-expressions`: an expression the transpiler lowers runs where C# runs it, with no
  function of the transpiler's own around it.

## Impact

- **eqc**: `TryStatementStrategy`, `ThrowStatementStrategy`, `ThrowExpressionStrategy`,
  `CheckedExpressionStrategy` and `EnumerableFactoryStrategy` (the last three cross over to the IR),
  `ExceptionCreationStrategy` and `ExceptionTypes` (new), `PatternConverter.TypeCheck`,
  `ObjectCreationStrategy`, `StringMethodStrategy`, `ConvertStrategy`, `PrimitiveStaticStrategy`, and
  the try IR (`JsTry` holds one catch, `JsThrow` always a value).
- **Runtime**: `utils/exceptions.ts` and `utils/sequence-factories.ts` (new), `trim` with characters,
  and every .NET throw of the twins under `utils/` typed, which a spec now requires.
- **The transpiled library**: the pins of the components that throw or trim (`AppBar`, `Dialog`,
  `CodeLanguages`…), regenerated.
- **Public surface**: the compiler's `PublicAPI.Unshipped.txt` declares the changes. **Break**:
  `JsStatement.Try` takes one optional catch where it took a list, and `JsStatement.Throw` a value
  where it took a nullable one; a caller passes its one catch, or null. The developer surface does not
  move.
