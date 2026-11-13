## 1. An event's accessors are called as C# calls them

- [x] 1.1 An event declared with accessors is written as its twin's `$add$` and `$remove$` methods. Verify: `EventAccessorConformanceTests`, both sides executed, answers 105 where main answered 5 for a handler subscribed through an interface's explicit event
- [x] 1.2 A subscription calls the accessors of the event it binds, the runtime choosing for an interface's, an abstract or a virtual one. Verify: the conformance cases of a removal through the interface, a field-like event behind the same interface, a class event's own accessors and a static event's
- [x] 1.3 `+=` and `-=` on a delegate compose its invocation list. Verify: the accessors' backing field holds a function, and the event raises its handlers

## 2. Proved against the real thing

- [x] 2.1 The runtime's helpers. Verify: `delegates.spec.ts`, and the runtime's `tsc` and vitest pass (205 files, 2978 tests); the served runtime stays inside its budget
- [x] 2.2 What else subscribes or composes. Verify: the Conformance tests of events, delegates, lambdas and interfaces pass (134), the Compiler tests of events, delegates, compound assignment and explicit implementations pass (76), and the transpiled libraries' pins pass (15)
- [x] 2.3 Docs. Verify: the wiki's SupportedFeatures page has an Events row in five languages, and a ledger entry cites #746
