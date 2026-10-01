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

- **An arrow's block is the statement IR.** A block arrow is its own node, `JsArrowBlock`, which holds
  the block as a `JsStatement` with the layout and the depth it is laid out at: where the lambda
  stands in the C#, which a writer placing the arrow through a string seam does not know. `JsArrow`
  is the expression-bodied one. The text is what it was.
- **The expression writer carries marks.** Every node it writes composes the marks inside it, which
  only an arrow's block has, through the builder the statement writer already used (`JsWritten` and
  `JsWrittenBuilder`, now shared), and the statement writer places each expression it holds with them.
  A block is laid out once per arrow however many times a text seam writes it, and a statement that
  reads the same in either layout is written in one place.
- **What follows a block maps to its statement again** (found in review). Once a block's statements
  carried marks, what followed the block on its closing line (a chained call, the next argument, an
  operand) read as the block's last statement: the writer records where a block closes, and the
  statement that holds it marks that place as itself. And a body C# wrote without braces keeps its
  origin when a declaration it hoists braces it, where the braces handed both lines to the `if`.
- **The strategies hand the writer their block.** A lambda's and a `delegate`'s block, and a concise
  body that declares a variable, which is one lowering for a lambda and a local function
  (`ConvertExpressionBodyIr`), mapped to its expression. A local function is a `const` bound to a
  block arrow, which retires `JsConstArrow`, and a switch section converts at the depth its
  statements are written at.
- **The carriers of a lambda cross to the IR.** List's own methods (`ForEach`, `Find`, `Exists`…)
  and collection expressions (`children: [ … ]`), with `JsArray` and `JsSpread`. Both strategies leave
  the text baseline.
- **The class is counted.** `LambdaStatementMapTests` counts, per shared source, the statements
  inside a lambda's block that map to no line of their own, against a baseline that may only shrink.

For a developer using the SDK: a breakpoint inside a lambda's block binds on its own line, and a
frame thrown there names its statement, wherever the lambda reaches the writer as IR. Nothing is
written differently in any pinned module. Where a migrated strategy's text changes, it is the IR's
punctuation: an author's redundant parentheses around a List call, an argument or an element are
re-derived, and a `x!` receiver is fenced, each executing as before.

## Impact

- The compiler: `CodeGen/Ir` (`JsExpr`, `JsExprWriter`, `JsStatementWriter`, `JsStatement`, and the
  new `JsArrowBlock`, `JsArray`, `JsSpread`, `JsWritten`, `JsWrittenBuilder` and `JsPosition`),
  `CSharpToJsConverter`, `LambdaExpressionStrategy`, `AnonymousMethodExpressionStrategy`,
  `LocalFunctionStatementStrategy`, `SwitchStatementStrategy`, `ListMethodStrategy` and
  `CollectionExpressionStrategy`.
- Public surface: `JsArrow` loses its block, and a block arrow is `JsArrowBlock`, which
  `JsExpr.ArrowBlock` returns with its layout and depth; `JsConstArrow` and `JsStatement.ConstArrow`
  go; `JsArray`, `JsSpread`, their factories and `ConvertExpressionBodyIr` are new; `ListMethodStrategy`
  and `CollectionExpressionStrategy` are `IExpressionIrStrategy` implementations. `PublicAPI.Unshipped.txt`
  carries every retired signature as a `*REMOVED*` line. The developer surface does not move.
- The migration line: an app writes none of these, since they are eqc's own IR, which no app
  compiles against.
- What still maps as its statement, counted by the baseline: a lambda inside an expression-bodied
  member or an object creation or initializer (#492), and inside any other translation that writes
  text, until it is on the IR, and a body with an `out` or `ref` parameter, whose wrapper is text
  (#487).
