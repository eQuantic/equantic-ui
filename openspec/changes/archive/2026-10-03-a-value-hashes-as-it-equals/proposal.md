# Proposal

Closes #519, #459 and #424, sub-issues of #164 (the transpiler's fences hold on every path).

## Why

`x.GetHashCode()` was emitted as `x.getHashCode()` for every receiver, and nothing defines
`getHashCode`: not a string, not a number, not a record's twin. The call compiled, rendered on the
server, and threw in the browser. `HashCode.Combine`, which an app's own `GetHashCode` usually
returns, named a class nothing defines.

A Guid is its text in the browser, and `Guid.Parse` handed that text back as it was written, so two
spellings of one Guid were two values to `==`, a dictionary and a set, and `new Guid(text)` named a
class nothing defines (#459). And a date that left the calendar was built without a word wherever it
was not moved by a fraction: `Add`, `Subtract`, `AddMonths`, `AddYears` and the operators, whose
compound form (`d -= span`) did not even call the twin (#424).

## What Changes

- `GetHashCode()` answers in the browser by .NET's contract: values `Equals` finds equal hash equal,
  a string, a number, a long, a bool, a tuple, a record, a struct, a decimal, a date and a value
  under `object` included. A type that overrides `GetHashCode` answers its own, and an instance of a
  class that does not is hashed by its identity, as `object.GetHashCode` is.
- `base.GetHashCode()` calls the base's override where the app wrote one, and the identity's where
  the base's is `object`'s. `HashCode.Combine(…)` combines its values' hashes in order.
- A Guid made from text, by `Parse`, `TryParse` or `new Guid(text)`, is its canonical text, the
  lowercase `D` format, read from any format .NET reads, and a text .NET refuses is refused.
- A date's `Add`, `Subtract`, `AddMonths`, `AddYears` and the `+` and `-` operators (and their compound
  forms, which now call the twin) refuse a result off the calendar in .NET's words, each naming its own
  parameter (`value`, `months`, `t`), and `AddMonths` and `AddYears` refuse a count past their range.
- .NET's own numbers are not stable across processes (a string's hash is randomized in every one), so
  the browser keeps the contract, never the server's number, and a page must not depend on it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-bcl`: `GetHashCode` and `HashCode.Combine` answer by .NET's contract.

## Impact

- **eqc**: `GetHashCodeStrategy`, and `Eq.Hash`, `Eq.HashCombine`, `Eq.HashIdentity`. The Guid
  strategy moves onto the IR and reads its type by symbol, the spelling only where no model can be
  asked, and a compound date assignment goes through the binary lowering.
  `StrategyRegistrationTests` fails for a strategy the converter does not register.
- **Runtime**: `utils/hash.ts` (`$eq.hash.of`, `.combine`, `.identity`), and a `getHashCode` on the
  decimal, the dates, the time span and the dictionaries, agreeing with their `equals`;
  `utils/guid.ts` (`$eq.guid.parse`, `.tryParse`); the dates' `add`, `subtract`, `addMonths` and
  `addYears` with .NET's refusals.
- **Public surface**: the compiler's new strategy and constants. The developer surface does not move.
- **Break**: a Guid read from text is lowercase in the browser, as .NET writes it. A page that showed
  the text a Guid was parsed from now shows .NET's.
