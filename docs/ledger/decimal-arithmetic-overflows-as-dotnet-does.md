- **2026-10-14 · A decimal computes in the browser what it computes on the server**: the runtime's
  `Decimal` added, subtracted and multiplied on an unbounded mantissa and divided to 28 places
  whatever the dividend, so `decimal.MaxValue + decimal.MaxValue`, and a `Sum` or an `Average` of
  them, printed a number no decimal holds, a product kept every place past 28, and `4.00m / 2m`
  printed `2`. Each operation now follows .NET 10's `DecCalc`: past 96 bits or 28 places a result
  rounds half to even to the places that fit, or throws .NET's `OverflowException`; a quotient keeps
  the places that fit, and its scale, a remainder's and a zero product's are .NET's. Half a million
  random operations on every width and scale `DecCalc` takes a path for answered 44% differently
  before; 1.5 million answer alike now, and 24 of the 32 new conformance cases fail without the
  change ([#764](https://github.com/eQuantic/equantic-ui/issues/764)). Proposed and archived through
  OpenSpec (`openspec/specs/runtime-decimal`).
