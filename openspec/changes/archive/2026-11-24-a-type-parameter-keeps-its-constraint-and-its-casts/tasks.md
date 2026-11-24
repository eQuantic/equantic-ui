# Tasks

## 1. The constraint

- [x] 1.1 Give `TwinTypeParameters` a constraint namer, and have the class, component and record writers pass theirs: a class that is not generic and has a twin, imported. Verify `EmittedMeasured` and `EmittedTagged` are refused by the runtime's `tsc` on main (TS2339) and taken with the fix.
- [x] 1.2 Ask whether the class has a twin as a downcast asks it, through one shared predicate. Verify a constraint to the server's `FlexNode` is refused by the client fence before it is written (EQ2010), so no header names a class with no twin.

## 2. The cast

- [x] 2.1 Assert a cast to a type parameter in TypeScript, `(value as T)`. Verify `EmittedHandler` is refused on main (TS2345) and taken with the fix, and that plain JavaScript is unchanged.

## 3. The record

- [x] 3.1 Write the ledger entry under `docs/ledger/`, citing #1854 and #1860.
