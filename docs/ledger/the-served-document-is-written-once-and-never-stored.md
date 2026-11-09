- **2026-11-09 · The served document writes each value once, and is never stored**: a security fix tracked
  in [#1319](https://github.com/eQuantic/equantic-ui/issues/1319) and a Bug under #1174
  ([#1288](https://github.com/eQuantic/equantic-ui/issues/1288)), both from the security review of #1265.
  The shell's template engine filled its placeholders one variable after another over the whole document,
  so text one variable wrote could be read again as template by the next. Every placeholder of the template
  is now filled in one pass over the template alone, and what a variable writes is never read again. And the
  document went with no `Cache-Control` while it carries the visitor's cookie choices and the page's state:
  every answer of a page's route is now `no-store`, said once. Held by two Server test classes, each failing
  before its fix. Proposed and archived through OpenSpec (`served-document`).
