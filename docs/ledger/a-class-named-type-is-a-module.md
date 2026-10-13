- **2026-10-13 · A class named as .NET names one of its types is a module the page imports**: a
  library's `static class Type` reached the page as `Type.family()` with no import and no module of
  `Type`, the build said nothing, and the browser met a name nothing defined. The emitter keeps a list
  of names it takes for .NET's (`Type`, `Uri`, `Console`, `Convert`, `Math`, `Regex` among them) and
  dropped the import of any name on it before it asked whether the build writes a module of that
  name, and the rule that takes a name ending in `Exception` for .NET's did the same to a library's
  own exception class. Both now ask the module's own symbols, since the name alone made a file that
  calls `System.Math` import an app's own `Math`, which review of the first form found. Six names of
  the list, the app's own class named `Type`, a library's exception class and a solution's `Math`
  beside a file that calls `System.Math` each fail on main's emitter, and a page that calls
  `System.Math` with no class of that name still imports nothing. Found while testing #887, whose
  test library's class was first called `Type`. Left for its own change: a file that names both a
  solution's `Math` and `System.Math` has one binding for the two
  ([#890](https://github.com/eQuantic/equantic-ui/issues/890))
  ([#888](https://github.com/eQuantic/equantic-ui/issues/888)). Proposed and archived through
  OpenSpec (`openspec/specs/module-imports`).
