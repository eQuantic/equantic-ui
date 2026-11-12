- **2026-11-10 · A collection's negative count is refused with its value, and a value no enum member names
  crosses as the browser holds it**: bugs under #1174. `Queue<T>`'s and `Stack<T>`'s capacity and the
  `CopyTo` of a `LinkedList<T>` and a `HashSet<T>` throw through the guards' one writer, so `ActualValue` reaches
  C# where it was null, measured first on .NET 10 to keep `List<T>`'s and `HashSet<T>`'s own capacity checks,
  which carry none, as they are ([#1273](https://github.com/eQuantic/equantic-ui/issues/1273)). A non-flags
  enum's value that no member names crossed from the server as its digits in a string, where the browser holds a
  number or a BigInt, and the hydration spec now revives it, as a field, an element and a key
  ([#1238](https://github.com/eQuantic/equantic-ui/issues/1238)). Proposed and archived through OpenSpec
  (`runtime-collections`, `hydration-contract`).
