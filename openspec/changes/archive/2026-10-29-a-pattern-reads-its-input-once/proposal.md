## Why

Closes #1149, a bug under #849 (the transpiler's fences hold on every path, part three), under the epic #157.
.NET reads the value an `is` pattern tests once, whatever the pattern asks of it. eqc settled that with a rule
of its own inside `IsPatternStrategy`: a call was bound in an arrow, and a path of names (`c.Value`,
`this.value`) was written again at every mention, taken for a read nobody could observe. A property's getter
runs each time it is read, so a getter that counts its reads, measured on the module-graph harness, answered
21 where .NET answers 11 for `c.Value is 2 or 1`, `c.Value is > 0 and < 10`, `c.Value is not (2 or 3)`,
`c.Corner is { X: 1, Y: 2 }`, `c.Boxed is int or long`, `c.Value is > 0 and var v` and `Value is 2 or 1` inside
the class. The binary form read it twice for one type test (`c.Text is char` asks a string its type and then
its length). And an input the pattern never names was never evaluated: `f() is var _` answered `true`
without calling `f`.

The brief this came from measured `f() is 2 or 1` written as `f() === 2 || f() === 1`. That no longer
reproduces: #543 bound a call in an arrow. The member path is what was left of it.

## What Changes

For a developer using the SDK, nothing to write: a pattern reads its input once in the browser, as .NET does,
whether the input is a call, a property or a member of `this`, and evaluates an input the pattern names
nothing of.

- **The writer decides single evaluation for a pattern's input.** The test is written over a name for the
  input and becomes a `JsExpr.Template` whose hole is each place its code names it. The writer binds the
  input once where the test reads it more than once, and writes a plain name or a literal in its place, the
  rule every template follows. The strategy's own copy of that rule (`Once`, `IsPath`) goes.
- **A template's holes are code.** A string a template's text holds may spell a hole (the test of
  `s is "{0}"` compares with that text), and the writer leaves it as it is, where it filled it with the part.
- **An input the pattern never names is evaluated.** `f() is var _` calls `f`; a name or a literal is left
  out, since reading one cannot be observed.

The parts it reaches: eqc only (`IsPatternStrategy`, `JsExprWriter`). The public surface does not move, nor
does the developer surface. Nothing an app wrote breaks. What a page does changes only where a pattern's
input is a member whose getter has an effect, or an input a pattern never names.

A member a pattern reads INSIDE it (`c is { Value: 2 or 1 }`, the arms of a switch over `{ Value: … }`) is
still read once per mention: that is #1150, which needs the member held once per operation, as `MatchParts`
holds a `Deconstruct`'s parts.
