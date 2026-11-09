- **2026-11-09 · The served document writes each value once, and is never stored**: two Bugs under #1174
  ([#1284](https://github.com/eQuantic/equantic-ui/issues/1284),
  [#1288](https://github.com/eQuantic/equantic-ui/issues/1288)), found by the security review of #1265.
  The shell's template engine replaced each `{{Variable}}` over the whole document, one after another, so a
  placeholder inside a value already written was replaced by a later variable: a visitor's
  `{{InitialState}} onmouseover=alert(1) x`, drawn in a field and kept in the page's state, became the
  state's JSON in the field's `value`, whose first quote closed the attribute and left a real
  `onmouseover`. Every placeholder of the template is now filled in one pass over the template alone. And
  the document went with no `Cache-Control` while it carries the visitor's cookie choices and the page's
  state, so a cache that kept HTML would hand one visitor's data to the next: every answer of a page's
  route is now `no-store`, said once. Held by two Server test classes, each failing before its fix.
  Proposed and archived through OpenSpec (`served-document`).
