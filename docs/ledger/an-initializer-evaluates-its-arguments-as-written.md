- **2026-11-11 · An initializer evaluates a call's arguments as written**: a bug under #849
  ([#1201](https://github.com/eQuantic/equantic-ui/issues/1201)), left by #1071. A field's or a property's
  initializer had nowhere to declare the temporaries that evaluate named arguments in the order written, so
  `public string Value = Calls.Pair(b: Seq.Int(2), a: Seq.Int(1));` ran `Seq.Int(1)` first in the browser. An
  initializer is now converted in a scope of its own, and the arrow that declared what it declares holds what it
  binds too; C# never lets an initializer await, so the arrow costs nothing the other lowerings avoided it for. An
  object initializer and a null-conditional in an initializer bind there as well, and seven twins of the runtime's
  transpiled libraries changed with them, running alike. Proposed and archived through OpenSpec
  (`transpiler-expressions`).
