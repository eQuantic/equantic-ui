# Tasks

## 1. An enum orders by its value

- [x] 1.1 Write a non-flags enum's members with their values in the ordering table, and read an ordering's JavaScript as given everywhere. Verify: `CollectionTailConformanceTests.AnEnum_OrdersByItsValue` runs a sorted set, a sorted dictionary, Max and Min on both sides, and fails against the previous commit (5 of 6, the flags case already passing)
- [x] 1.2 Rebuild a hydrated sorted set of an enum in that order. Verify: `HydrationSpecEmissionTests` pins the members' values in the spec, and `HydrationCrossingTests` draws the server's order

## 2. A queue and a stack find a member by its value

- [x] 2.1 Compare a queue's and a stack's members as `EqualityComparer<T>.Default` does. Verify: `AQueueOrAStack_FindsAMemberByItsValue` finds a decimal and a date on both sides, and `HydrationCrossingTests` finds a hydrated decimal, both failing against the previous commit
