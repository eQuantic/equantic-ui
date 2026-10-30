## Why

Closes #687, #959, #962, #979 and #980, sub-issues of Feature #849.

- An `Equals` the app wrote was skipped at identity checks in `sameItem`, `$eq.equals` and their callers, so a value compared with itself answered true with no call.
- A comparison reached a `[ServerOnly]` `Equals` through a collection's comparer, a tuple's member or an instance call through a contract with no EQ2010, and answered what the server's method never would.
- Two delegates over one target and method were two functions, so `==`, `Equals` and `-=` found nothing. A plain delegate's `+=`, `-=`, `+` and `-` were JavaScript's arithmetic, and `Delegate.Equals` failed the build.
- Generic code through `IEquatable<U>` fell to `Equals(object)` over a class that answers it for several other types.
- A generic record built in generic code, a struct's zero and a value rebuilt from the wire carried no closed type, so they equalled a value of another type argument.

## What Changes

- `sameItem` and `equals` ask a value's own `Equals` every time, a value compared with itself included, unless that `Equals` is a record's synthesized one, which checks the reference first (`checksItsReference`).
- `ElementEquality.Refuses` is the one fence for a comparison, walking a tuple's, an anonymous object's and a pair's parts, and every comparer a call writes meets it (`ElementEquality.Of(type, at, context)`, `LinqKeys.Matches`, a dictionary's and a set's construction). An instance call through a contract meets it too.
- `$eq.delegates.bound` keeps one function for a target and a method, and `$eq.delegates.over` one delegate built from a source. A delegate's `+=`, `-=`, `+`, `-` and `Equals` go through `$eq.delegates`.
- A contract over an erased type argument is chosen by the other value's kind, or its class's name, where a class answers several.
- The closed type travels: a generic method's answer is marked at its call, a struct's zero where it is written, and a value from the wire by its hydration spec (`closed`). The marks move to `utils/closing.ts`.

## Limits

- `object.ReferenceEquals` of two delegates over one target and method answers true, where .NET answers false, since they are one function here.
- A multicast delegate compares by identity.
- A value built in generic code and stored there, a `List<Box<T>>` filled inside a generic method, still carries no mark and is not taken for another type: only a value a call hands back is closed.

## Impact

- Reaches the compiler (`ElementEquality`, `StructuralEqualsStrategy`, the comparers' strategies, `InvocationStrategy`, `ObjectCreationStrategy`, `EventSubscriptionStrategy`, `BinaryExpressionStrategy`, `MemberAccessStrategy`, `IdentifierStrategy`, `DefaultValue`, `HydrationSpec`) and the runtime (`utils/equals.ts`, `utils/key-equality.ts`, `utils/delegates.ts`, `utils/closing.ts`, `utils/hydrate.ts`).
- A build that compares a type whose `Equals` is `[ServerOnly]` through a collection now fails with EQ2010, where it built and answered wrongly.
- The transpiled components move: a method group is `$eq.delegates.bound(this, this.m)`.
- `PublicAPI.Unshipped.txt` adds two `Eq` constants, on the compiler's tool surface. `developer-surface.baseline.txt` does not move.
