# Proposal

Closes #961, a Bug under #849: what Copilot's second review of #958 found after #958 merged.

## Why

#958 made every name the browser lacks either answer as .NET does or fail the build. Four paths still
did neither, measured at #958's head (c0bb08652, the tree its squash d331ec552 merged):

- A span scaled or divided by a value whose own type converts it to a double, `TimeSpan.FromTicks(3) *
  factor` with an in-source `Factor` and its implicit conversion, in a binary or in place: the dispatch
  asked the operand's own type, so the product was JavaScript's `*`, NaN, though ValueFlow had already
  converted the operand.
- A method group inside an expression a strategy rebuilds, a `?.` chain or a query: its call was bound
  at the rebuilt node, which no model knows, and the group was refused (EQ2004).
  `values?.Select(Math.Abs)` is a REGRESSION of #958: main at d702ca101 built and ran it, and #958
  refused it. `from s in strings select new Func<string>(s.ToString)` failed on main too, as a class
  named `Func` nothing defines.
- A package type's static `Count` was read through the collection table before the read fence, so
  `Status.Count` wrote `Status.count`, a ReferenceError, with no diagnostic.
- A package interface's static was let through with the members a .NET interface declares:
  `IStatus.Label` wrote `IStatus.label`.

A fifth finding of that review, an interface- or type-parameter-typed hole in
`FormattableString.Invariant`, does not reproduce: it writes `1.5` on a pt-BR page at c0bb08652, as .NET
does, and its case stays as a pin.

## What Changes

- The date, time and span dispatch is chosen by the operator C# bound: where it is a date's, an
  instant's, a time's or a span's own, its parameter types choose the twin's method and its lifting the
  lift, for a binary and a compound assignment alike.
- A method group's call is bound at the node the group was written as, which the semantic helper
  answers for every rebuilt node.
- The collection table no longer claims a package type's static `Count`, and the read fence refuses a
  package's static whatever kind of type declares it, passing over only what a .NET interface declares.

- Found by the review before the pull request opened: a `DateTimeOffset` beside a `DateTime`, which matched no
  operator on main either and ran JavaScript's own, is converted as C#'s implicit operator converts it, and a
  package's static is refused before every name-keyed shortcut, so `Vendor.Guid.Empty` and `Vendor.DateTime.Now`
  are refused rather than read as `''` and `new Date()`.

## Impact

- eqc: `BinaryExpressionStrategy.BoundOperands` (internal), asked by `BinaryExpressionStrategy` and
  `AssignmentExpressionStrategy`; `MethodGroupStrategy.Call`; `MemberAccessStrategy`'s `Count` branch;
  `PlatformMemberRead.NothingCarries`. The compiler's tool surface moves once, for Copilot's second review
  of #964: `UserDefinedOperators.Conversion` takes its operand as IR and whether the bound conversion is
  lifted, the shape #948 gives every operator and conversion.
- Specs: `runtime-dates`' "A span scales and divides as .NET's operators do" and `transpiler-bcl`'s "A
  package's type has no name in the browser" say the operator decides and that a package's static of
  any kind and name is refused.
- Tests: one conformance case per path beside #958's, in `TimeSpanMemberConformanceTests`,
  `MethodGroupInARebuiltExpressionConformanceTests`, `APackagesTypeTests` and
  `InterpolationInACultureConformanceTests`.
