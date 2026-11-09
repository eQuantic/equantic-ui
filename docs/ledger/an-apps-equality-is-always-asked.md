- **2026-11-03 · An app's Equals is always asked, and two delegates compare by target and method**: bugs under #849
  ([#979](https://github.com/eQuantic/equantic-ui/issues/979),
  [#980](https://github.com/eQuantic/equantic-ui/issues/980),
  [#962](https://github.com/eQuantic/equantic-ui/issues/962),
  [#959](https://github.com/eQuantic/equantic-ui/issues/959)) and under #565
  ([#687](https://github.com/eQuantic/equantic-ui/issues/687)). A value compared with itself answered at a reference
  check almost everywhere, so an `Equals` the app wrote never ran there; it runs every time .NET runs it, and only a
  record's synthesized `Equals` keeps the check. Every route a comparison takes, a collection, a tuple, an anonymous
  object and a contract, meets the server-only fence (EQ2010). A call through `IEquatable<U>` in generic code reaches
  the method it binds. A generic record keeps its closed type where generic code builds it, at its zero and from the
  wire. Two delegates over one target and method are one function, so `==` and `-=` find a handler made again.
  Proved by the conformance suite on both sides. Proposed and archived through OpenSpec (`transpiler-expressions`,
  `transpiler-records`, `client-boundary`).
