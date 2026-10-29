- **2026-10-29 · A pattern reads its input once, a member's getter included**: a bug under #849
  ([#1149](https://github.com/eQuantic/equantic-ui/issues/1149)). An `is` pattern settled single evaluation
  of its input with a rule of its own: a call was bound in an arrow, and a path of names (`c.Value`,
  `this.value`) was read again at every mention, where a property's getter runs each time. Measured with a
  getter that counts its reads, `c.Value is 2 or 1` answered 21 where .NET answers 11, through every shape
  of pattern and the binary type test, and `f() is var _` never called `f`. The test is now a
  `JsExpr.Template` over the input, so the writer binds it once wherever the test reads it twice, as for
  every other translation, and the writer reads a template's holes in its code only, a string spelling
  `{0}` left as it is. A member read inside the pattern is still read once per mention, which #1150
  follows. Proposed and archived through OpenSpec (`transpiler-expressions`).
