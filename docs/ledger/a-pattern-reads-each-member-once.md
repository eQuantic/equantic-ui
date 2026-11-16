- **2026-10-29 · A pattern reads each member it tests once**: a bug under #849
  ([#1150](https://github.com/eQuantic/equantic-ui/issues/1150)). A property pattern's member was read again at
  every place the operation names it, each alternative, conjunct, binding, arm and section, so a getter that
  counts its reads answered 21 where .NET answers 11 for `c is { Value: 2 or 1 }` and for a switch's arms over
  `{ Value: … }`, and 41 for `c is { Corner: { X: 2 } or { Y: 2 } }`. `MatchParts`, which held what a
  `Deconstruct` the app wrote hands back once per operation, now holds a member whose read runs the app's code
  too, in a box the first test that reaches it fills, so a null is held as well; a field, an auto-property the
  twin keeps under its own name and a property of the platform's are written in place, as they were. Proposed
  and archived through OpenSpec (`transpiler-expressions`).
