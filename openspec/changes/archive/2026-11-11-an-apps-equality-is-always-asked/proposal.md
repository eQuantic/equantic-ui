## Why

Closes #979, #980, #962 and #959, sub-issues of Feature #849, and #687, a sub-issue of #565.

- #979: a value compared with itself answered at a reference check almost everywhere, so an `Equals` the app wrote never ran there: an instance call through a base or `object`, a member compared inside a twin, a collection's search, a tuple's and an anonymous object's member. .NET asks it every time but in a record's synthesized `Equals`, and an `Equals` that refuses itself changes the answer.
- #980: a collection, a tuple, an anonymous object and a call through a contract reached a `[ServerOnly]` `Equals` with no EQ2010, and answered by identity or by the twin's member-wise compare, which the server never would.
- #962: generic code through `IEquatable<U>` answered by `Equals(object)` on a class that answers the interface only for other types, since `U` is erased in the browser.
- #687: a generic record built in generic code, a generic struct's zero and a value rebuilt from the wire carried no closed type, so they equalled a value of another type argument.
- #959: two delegates over the same target and method were two functions, compared by identity, so `g == h` answered false and `-=` of a handler made from a method group again removed nothing.

## What Changes

- The runtime's equality (`utils/equals.ts`, `utils/key-equality.ts`) asks an app's `Equals` every time .NET does, a value compared with itself included; only a record's synthesized `Equals` keeps its reference check, as .NET's does.
- Every comparison the compiler lowers asks the server-only fence of the equality it reaches, a collection's comparer, a tuple's and an anonymous object's member and a call through a contract included, and refuses one that reaches a `[ServerOnly]` `Equals` with EQ2010 where it is written.
- A call through `IEquatable<U>` in generic code reaches the method the contract binds: the runtime picks it by the other value where a class answers several (`$eq.collections.equatableCall`, in `utils/key-equality.ts`).
- A generic record or struct keeps its closed type where generic code builds it, at its zero and when it is rebuilt from the wire (`utils/closures.ts`, `utils/hydrate.ts`).
- A delegate made from a method group is the one function its target and method have, so `==`, `!=`, `Equals`, a collection's search and `-=` find it, and `+` and `+=` of a plain delegate are `Delegate.Combine` (`utils/delegates.ts`).

## Impact

- Reaches the compiler (the equality and delegate strategies, the server-only fence) and the runtime (`utils/equals.ts`, `utils/key-equality.ts`, `utils/delegates.ts`, `utils/closures.ts`, `utils/hydrate.ts`). The transpiled components that ship inside the runtime name the delegate helper and close their generic zeros.
- No migration line: no contract member moves, and `developer-surface.baseline.txt` does not move.
