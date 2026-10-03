# Tasks

## 1. One table for how a type orders

- [x] 1.1 Move the ordering table into `ValueOrdering` and `utils/ordering.ts`, and read it from `Max`/`Min`. Verify: the LINQ conformance suites pass unchanged
- [x] 1.2 Build every sorted set, sorted dictionary and sorted list in its element type's order, an ordinal comparer as the code-unit order. Verify: `CollectionTailConformanceTests.ASortedCollection_OrdersAsItsElementTypeDoes` runs strings, decimals, doubles, dates, chars and an app's comparable type on both sides, and fails against the previous commit
- [x] 1.3 Rebuild a hydrated sorted set and sorted dictionary in that order. Verify: `HydrationSpecEmissionTests` pins the order each spec carries, and `HydrationCrossingTests` draws the server's order in the browser

## 2. Only what the browser would answer differently is left out

- [x] 2.1 Read the comparer of the collections whose comparer can be read, through their wrappers, and refuse an unreadable set or dictionary in a member rebuilt as one. Verify: `ServerValueCrossingTests` crosses the default, ordinal and wrapped-plain ones and a plain object with a comparer, and leaves out the case-insensitive, immutable, wrapped, ordinal-sorted and app-owned ones, and fails against the previous commit
- [x] 2.2 Leave out only the read of a projected value. Verify: the roster's name crosses beside the set left out
