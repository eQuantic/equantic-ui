- **2026-11-26 · A static generic class keeps its statics per closed type**: the twin was one class for every type
  argument, so `Cache<int>` and `Cache<string>` shared their statics and the second's static constructor never ran, and
  TypeScript refused its static members for reading the class's type parameter. The runtime now keeps one object per
  closed type (`$eq.statics(Cache, 'int')`), the class's static members read their statics through it, other code reaches
  the closed type it names, and the twin declares no type parameters. A type argument known only at run time is refused
  (EQ2018) ([#1855](https://github.com/eQuantic/equantic-ui/issues/1855)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
