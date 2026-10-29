- **2026-10-29 · An explicit interface implementation holds its member's name once**: a bug under #849
  ([#1139](https://github.com/eQuantic/equantic-ui/issues/1139)). eqc wrote every explicit implementation under
  the name of the member it implements, and the overload check left them out on purpose, so two of one name
  were both written and the browser kept the last: `IFoo.Run()` and `IBar.Run()` both answered as `IBar`'s, one
  handing off to the other, written last, called itself until the stack overflowed, and so did an explicit
  property reading the type's own. One that hands back what the member holding its name answers, its parameters
  passed through, is not written now, so that member answers through each interface, and a method of the type's
  own it hands off to keeps its name instead of being refused for answering another interface. What the twin
  would still hold twice along its chain, two explicit implementations with answers of their own, one beside a
  property, an event or an override of its name, or one on the name of a base's member, is refused with EQ1007,
  naming the interfaces. A component's parser dropped a method beside an explicit implementation of its name,
  taking it for an overload, and keeps it now. Routing a call through one interface to a name of its own would
  need every type implementing that interface to answer it, so it was not done. Proved against main both ways:
  20 of the 23 compiler tests and the conformance test fail there, the conformance test by a refusal and, with
  that case set aside, by a module that never finished. Proposed and archived through OpenSpec
  (`transpiler-interfaces`).
