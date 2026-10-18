# Tasks

## 1. A span's operator by the operator C# bound

- [x] 1.1 `BoundOperands` reads the bound `IBinaryOperation` or `ICompoundAssignmentOperation`: a date's, an instant's, a time's or a span's own operator gives its parameter types and its lifting
- [x] 1.2 The binary and the compound dispatch ask it, ValueFlow converting the operand as it does
- [x] 1.3 Pin a span scaled and divided by a `Factor` with an implicit conversion, both orders, in place and lifted, in `TimeSpanMemberConformanceTests`

## 2. A method group inside a rebuilt expression

- [x] 2.1 `MethodGroupStrategy.Call` binds the call at `SemanticHelper.Original(group)`, its tree checked and its position taken there
- [x] 2.2 Pin a group in a `?.` chain over a null and a non-null receiver, and in a query, in `MethodGroupInARebuiltExpressionConformanceTests`, and measure them on main d702ca101

## 3. A package's statics

- [x] 3.1 The collection table passes over a package type's static `Count`, which the read fence refuses
- [x] 3.2 The read fence passes over only what a .NET interface declares
- [x] 3.3 Pin a package's static `Count` and its interface's static in `APackagesTypeTests`

## 4. Proof and documentation

- [x] 4.1 The targeted A/B on each path: the new tests fail over c0bb08652's source and pass at the fix
- [x] 4.2 The two requirements' text, and one ledger entry under `docs/ledger/` citing the issue
