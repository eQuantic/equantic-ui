- **2026-10-17 · Four names #958 left answer as .NET does or fail the build**: found by Copilot's
  second review of #958. A span scaled or divided by a value its own type converts to a double, a
  `Factor` with an implicit conversion, was JavaScript's `*`, NaN, because the dispatch asked the
  operand's type; the operator C# bound decides now, in a binary and in place. A method group inside a
  `?.` chain or a query was refused with EQ2004, its call bound at the rebuilt node no model knows: a
  regression for `values?.Select(Math.Abs)`, which main at d702ca101 built and ran and #958 refused,
  while the query form named a `Func` nothing defines on main as well; the call is bound where the
  group was written now. A package type's static `Count` and a package interface's static were written
  as names, `Status.count` and `IStatus.label`; both are refused with EQ2004 now. Each path's case
  failed over #958's tree and passes at the fix, and a fifth finding, an interface-typed hole in
  `FormattableString.Invariant`, did not reproduce and stays as a pin
  ([#961](https://github.com/eQuantic/equantic-ui/issues/961)). The review before the pull request
  opened found two more: a `DateTimeOffset` beside a `DateTime` ran JavaScript's own operators, on main
  too, and converts the date as C#'s implicit operator does now, and a package type named like a .NET
  one (`Vendor.Guid.Empty`, `Vendor.DateTime.Now`) was read through a name shortcut before the fence,
  refused now. Copilot's first review of #964 found two more: a package's static reached through
  an alias named like a type a strategy claims by its spelling (`Int32.MaxValue` under `using Int32 =
  Vendor.Status` was 2147483647), refused now by the dispatcher before any strategy is asked, and a
  null `DateTime?` made a `DateTimeOffset?`, which threw a TypeError and stays null now. Proposed and
  archived through OpenSpec
  (`openspec/specs/runtime-dates`, `transpiler-bcl`).
