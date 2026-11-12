## 1. A fixed size

- [x] 1.1 Mark a `T[]` at its conversion to `ICollection<T>` or `IList<T>`.
- [x] 1.2 Refuse a change of its size in `add`, `clear`, `remove`, `insert` and `removeAt`, and read `IsReadOnly`.

## 2. The params convention

- [x] 2.1 Write a method's, a component's and a local function's `params` parameter as the array it is, on a twin this build writes.
- [x] 2.2 Pack what a call lists, and hand a whole array as it is.

## 3. Tests

- [x] 3.1 `FixedSizeArrayConformanceTests` and `ParamsArrayConformanceTests`, through a base, an interface, an extension, a delegate and a method group.
