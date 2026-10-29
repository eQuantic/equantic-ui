## Why

Closes #1139, a sub-issue of Feature #849.

eqc writes an explicit interface implementation under the name of the member it implements, the name every call through the interface reaches, and a JavaScript class holds one member per name. The EQ1007 overload check leaves explicit implementations out on purpose, since their name is the interface's, so nothing judged them, and each case below passed the build and went wrong in the browser:

- `string IFoo.Run() => "foo";` beside `string IBar.Run() => "bar";` wrote two `run()`, and a call through `IFoo` answered `bar`.
- `string IBar.Run() => ((IFoo)this).Run();`, written last, called itself until the stack overflowed, and so did `int IVal.Value => Value;` beside `public int Value`.
- One interface over two type arguments (`IGet<int>.Get()`, `IGet<string>.Get()`), two static abstract members of one name, an explicit property beside the type's own property, an explicit implementation beside an override, and one on the name of a member a base declares each wrote one name twice along the twin's chain.
- A public method that answers `IBar` beside `string IFoo.Run() => Run();` was refused (EQ1007), although both interfaces answer alike.
- A component's method beside an explicit implementation of its name was dropped by the component's parser, which took the second for an overload, so the twin called a method nobody wrote.

## What Changes

- An explicit implementation that hands back what the member holding its name answers is not written: its body calls a method, or reads a property, of the same object (cast to an interface or not), passing its own parameters through as they came, and a property's setter writes what its getter reads. That member answers every call through each interface. A method of the type's own it hands off to keeps its name, and is not refused for answering another interface. (`ExplicitImplementations`, which the type, record and component emitters and `TwinMethodName.ExplicitBeside` ask; it takes over the hand-off reading `IterableTwin` did for the enumeration interfaces.)
- Every other explicit implementation is written under its member's name, and what else the twin would hold on that name along its chain is refused with EQ1007, naming both members, the interfaces, and the hand-off that would make one answer as the other where the two are of one shape (`OverloadedMethods.CheckExplicitImplementations`).
- A component's parser keeps an explicit implementation beside a method of its name.
- A call through an interface binds the interface's member, never the type that implements it, so routing a call to a name derived from one implementation would need every implementation of that interface to answer that name. That is not this change.

## Impact

- Reaches `eQuantic.UI.Compiler` (`ExplicitImplementations`, `TwinMethodName`, `IterableTwin`, `OverloadedMethods`, `TypeScriptEmitter`, `RecordTypeEmitter`, `ComponentParser`).
- No new diagnostic: EQ1007 covers more cases, and `docs/DIAGNOSTICS.md` and the wiki's Diagnostics and SupportedFeatures pages say which.
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
