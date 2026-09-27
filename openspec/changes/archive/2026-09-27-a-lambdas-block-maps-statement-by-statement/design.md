# Design

## How Flutter answers it

It has no direct bearing. Dart's compilers print JavaScript from a tree, so a closure's body is never
text before the printer reaches it, and the question this change answers (a body that became text
before any writer saw it) does not arise there. docs/FLUTTER-PARITY.md has no row for source maps.

## Decisions

- **The arrow records the layout and depth of its block.** A string seam (`ConvertExpression`, a text
  strategy splicing an argument) writes the arrow long before the statement that holds it is placed,
  and that seam knows nothing of where the lambda stands. The converter does, when the lambda is
  converted, so the node carries it, and every writer lays the block out the same way: the text is
  what it was, and the map only gains lines.
- **One writer composes marks, not two.** The expression writer returns text and marks from every
  node, and the statement writer's builder is shared rather than copied, so a mark moves by the same
  rule wherever it is placed: a template's hole, a bound part in its argument list, a receiver, an
  operand.
- **A strategy that still writes text keeps its lambda's lines unmarked.** Migrating every text
  strategy that takes a lambda is the IR migration's work, one strategy at a time. `ListMethodStrategy`
  crosses here because `List.ForEach` is the case the issue measures, and `CollectionExpressionStrategy`
  because a screen composes its children in one.
- **The rest of a statement resumes after a block** (found in review). A mark is where a statement
  begins, and a debugger reads a position as the last mark before it, so a block's last statement
  claimed whatever followed the block on its closing line. The expression writer records where each
  multi-line block closes, and the statement that holds the expression turns each place into a mark
  of its own, the way a `do`'s condition line is marked.
- **A block arrow is its own node, with no defaults** (found in review). `JsArrow` had grown a block,
  a layout and a depth with defaults that let a block arrow be built silently on one line; the two
  shapes are two records now, and a local function's `const` is a block arrow like any lambda's, so
  there is one writer of an arrow's head.
- **The class is measured, not sampled.** The map tests read lines picked by hand, and the review
  found two carriers they never reached. `LambdaStatementMapTests` counts every statement inside a
  lambda's block in the shared components against a baseline that may only shrink, which is how the
  next text seam that drops a lambda's lines fails instead of shipping.
