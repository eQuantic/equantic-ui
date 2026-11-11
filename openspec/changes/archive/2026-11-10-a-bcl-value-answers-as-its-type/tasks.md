## 1. Equals

- [x] 1.1 Add `PrimitiveEqualsStrategy`, reading the other value's type through `StructuralEqualsStrategy.OfAnotherType`, and take `Equals` off `BclSurfaceTailStrategy`'s table, a Guid's aside.

## 2. Text of a pair

- [x] 2.1 Write `pairText` and `pairTextGroup` in `utils/format.ts`, and read a pair held as `object` in `general`.
- [x] 2.2 Route a `KeyValuePair` through `StringConversion`'s own text and its method group.

## 3. Tasks

- [x] 3.1 Write `fromException` and `fromCanceled` in `utils/cancellation.ts`, and route them from `TaskMethodStrategy`.

## 4. Tests

- [x] 4.1 `PrimitiveEqualsConformanceTests`: each primitive against its own type, another type, null and a value held as `object`, the evaluation order, and the typed overload.
- [x] 4.2 `PairTextConformanceTests`: each route into text, each half's type, a culture, a method group, and the reference tuple's refusal.
- [x] 4.3 `AsyncConformanceTests`: a faulted and a canceled task, awaited and left alone, and both refusals, in place of the case that pinned the build's refusal.
