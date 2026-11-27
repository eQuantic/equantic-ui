# Tasks

## 1. The import

- [x] 1.1 Make `$eq` a candidate of a class module, kept where the emitted text names it. Verify `EmittedRoutes`, a static class whose one runtime call is `Uri.EscapeDataString`, imports `$eq`, and that the runtime's `tsc` refuses its module without the fix.

## 2. The run

- [x] 2.1 Add a conformance case that builds `OrderRoutes` as an app builds it and calls `Pay`, with type annotations on and off. Verify it fails without the fix and answers as .NET does with it.

## 3. The record

- [x] 3.1 Write the ledger entry under `docs/ledger/`, citing #1882.
