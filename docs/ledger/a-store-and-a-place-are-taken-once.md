- **2026-11-10 · A component's guarded store starts as C# starts it, and a ref argument's element is taken once**:
  bugs under #1174. A component's property that keeps its store with `field` read undefined until something wrote
  it, and now starts at its initializer or its type's zero
  ([#1183](https://github.com/eQuantic/equantic-ui/issues/1183)). A `ref` argument counted from the end, and an
  array's element counted from the start whose receiver or position runs code, is read and written back through one
  receiver and position taken before the call, held in a temporary so an `await` among the arguments stays the
  function's own: `Twice(ref Get()[^1])` ran `Get` twice, and `Twice(ref a[i++])` stepped twice
  ([#1190](https://github.com/eQuantic/equantic-ui/issues/1190)). Proposed and archived through OpenSpec
  (`transpiler-classes`, `transpiler-expressions`).
