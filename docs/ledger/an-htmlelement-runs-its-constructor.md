- **2026-11-23 · An HtmlElement subclass runs its constructor as C# does**: a class over `HtmlElement` that took
  constructor parameters reached the browser without what its constructor does. `new Chip("a", 5).Says()` read
  `a|5` in .NET and `|` in the browser. The primitive path's parser kept a constructor reader of its own that stored
  no constructor's syntax, so no body ran, no parameter kept its default, every parameter landed on a member named
  after it, and a primary constructor was not read at all. It now reads constructors through the component path's
  `ParseConstructors`, and writes them through the writers the component branch uses. An expression-bodied
  `Render()` and a field's initializer, measured beside it, stay with their own issues, #1540 and #1541
  ([#1837](https://github.com/eQuantic/equantic-ui/issues/1837)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
