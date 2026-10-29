- **2026-10-29 · A list adds and inserts a range of any size, as .NET's does**: a bug under #849
  ([#1060](https://github.com/eQuantic/equantic-ui/issues/1060)). eqc lowered `AddRange` to `push(...items)` and
  `InsertRange` to `splice(index, 0, ...items)`, which hand every element to one call as its arguments, and an
  engine caps those by its stack: measured with a spread into `push`, about 638,000 in JavaScriptCore (Safari's
  engine, and the embedded Bun) and 124,000 in V8, so a million elements threw a RangeError in every browser. The
  same spread threw a TypeError for a null collection, added a string's code points, inserted at an index .NET
  refuses, and put a named collection in the index's place. The runtime adds and inserts one element at a time
  now (`$eq.collections.addRange`, `insertRange`), copying the collection as `new List<T>(collection)` copies it,
  which also asks a sorted set's view its count as .NET does, and eqc calls them for the bound overload with each
  argument in its parameter. With no model, `Max` and `Min` fold the values instead of spreading them. Measured at
  a million, `Concat`, `ToList`, `new List<T>(items)`, `..items`, `RemoveAll` and a bound `Max` were already
  bounded. Proposed and archived through OpenSpec (`transpiler-bcl`, `runtime-collections`).
