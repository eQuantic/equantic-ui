- **2026-11-23 · A type test against a type parameter is refused with EQ2016**: `x is T`, `x as T`, a type pattern
  `T t` and a `catch (T e)` were answered as `x != null`, true for any value, so a factory of one recognizer said it
  made every one. The browser carries no type arguments at run time: a closed construction is marked with them as
  text, and "is a `T`" takes a subclass, which no text answers. The test is now a build error that names the
  parameter and says what to write instead, documented here and on the wiki in every language
  ([#1846](https://github.com/eQuantic/equantic-ui/issues/1846)); whether generic code carries its type arguments is
  #755's question. Proposed and archived through OpenSpec (`openspec/specs/transpiler-expressions`).
