- **2026-10-30 · An app's equality is asked every time, two delegates compare by target and method, and a closed type travels**:
  bugs under #849 ([#687](https://github.com/eQuantic/equantic-ui/issues/687), [#959](https://github.com/eQuantic/equantic-ui/issues/959),
  [#962](https://github.com/eQuantic/equantic-ui/issues/962), [#979](https://github.com/eQuantic/equantic-ui/issues/979),
  [#980](https://github.com/eQuantic/equantic-ui/issues/980)). The identity checks in `sameItem` and `$eq.equals` answered for an `Equals`
  of the app's that never ran. Every comparer a call writes meets one fence for a `[ServerOnly]` `Equals`. A target and a method have one
  function, so `==` and `-=` find a handler made again, and a plain delegate's `+=` is `Delegate.Combine`. Generic code through
  `IEquatable<U>` chooses its contract by the other value, and a generic value keeps its closed type at a call, at its zero and across the wire.
