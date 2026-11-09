- **2026-11-09 · A string search says its comparison, and text is written as .NET writes it**: bugs under #849
  ([#532](https://github.com/eQuantic/equantic-ui/issues/532),
  [#533](https://github.com/eQuantic/equantic-ui/issues/533),
  [#965](https://github.com/eQuantic/equantic-ui/issues/965),
  [#729](https://github.com/eQuantic/equantic-ui/issues/729)). A break, Edgar's decision: `StartsWith`, `EndsWith`,
  `IndexOf` and `LastIndexOf` of a string with no `StringComparison` search by the culture in .NET, which the browser
  cannot, and fail the build now (EQ1004). A culture comparison that ignores case keeps widths and kana apart, each
  form a primary difference after its other form, measured over the whole BMP: a small katakana sorts after the forms
  of its large one. A number or a date held as an interface or a type parameter is written in the culture, as one
  held as `object` is. And `px` writes what `TokenCss.Px` writes, which is a float's seven significant digits rounded,
  not its shortest text as the issue supposed: `16777216f` writes `16777220`. Proved by the conformance suite on both
  sides, 22 of the 33 cases of width, kana and text failing on main, and by a cross-pin of `px` on both sides.
  Proposed and archived through OpenSpec (`transpiler-bcl`, `typography`).
