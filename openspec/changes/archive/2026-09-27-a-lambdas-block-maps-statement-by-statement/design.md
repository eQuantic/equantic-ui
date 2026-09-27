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
  crosses here because `List.ForEach` is the case the issue measures.
