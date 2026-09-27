# Proposal

Closes #384, a Bug under #164 (The transpiler's fences hold on every path).

## Why

Since #382, every statement of a member's body maps to its own C# line. The statements inside a
lambda's block did not: an arrow reached the writer with its block as TEXT, laid out where the lambda
was converted, and the marks its statements carried were dropped when that text was made. No line
inside a lambda's block had a segment in the map, so a breakpoint there bound nowhere, and a frame
thrown there read, through the composed map, as the line that holds the lambda. Measured on main at
a4112476: `StatementSourceMapTests` found no segment on any of 13 lines inside lambda blocks, and a
frame thrown inside a `List.ForEach` block led to `values.ForEach(value =>`, not to the statement
that called.

## What Changes

- **An arrow's block is the statement IR.** `JsArrow.Block` is a `JsStatement`, with the layout and
  the depth it is laid out at: where the lambda stands in the C#, which a writer placing the arrow
  through a string seam does not know. The text is byte for byte what it was.
- **The expression writer carries marks.** Every node it writes composes the marks inside it, which
  only an arrow's block has, through the builder the statement writer already used (`JsWritten` and
  `JsWrittenBuilder`, now shared), and the statement writer places each expression it holds with them.
- **The lambda and `delegate` strategies hand the writer their block**, and the block the emitter
  adds to a concise body that declares a variable maps to that body.
- **List's own methods cross to the IR.** `ForEach`, `Find`, `Exists` and the other calls of the
  array's own method are built as IR, so the lambda passed to one reaches the writer as an arrow.
  `ListMethodStrategy` leaves the text baseline.

For a developer using the SDK: a breakpoint inside a lambda's block binds on its own line, and a
frame thrown there names its statement. Nothing is written differently: every transpiled pin holds.

## Impact

- The compiler: `CodeGen/Ir` (`JsExpr`, `JsExprWriter`, `JsStatementWriter`, and the shared
  `JsWritten` and `JsWrittenBuilder`, new), `LambdaExpressionStrategy`,
  `AnonymousMethodExpressionStrategy` and `ListMethodStrategy`.
- Public surface: `JsArrow` and `JsExpr.ArrowBlock` take the block as a `JsStatement` with its layout
  and depth, and `ListMethodStrategy` is an `IExpressionIrStrategy`. `PublicAPI.Unshipped.txt` carries
  the five signatures this retires.
- What still maps as its statement: a lambda inside a translation that writes text, until that
  translation is on the IR, and a body with an `out` or `ref` parameter, whose wrapper is text
  (#487).
