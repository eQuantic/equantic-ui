## 1. One writer of an event's accessors

- [x] 1.1 Move the body of `TypeScriptEmitter.EmitEventAccessors` to `MethodLowering.EventAccessors(EventDeclarationSyntax, Func<TypeSyntax?, string> declaredType, bool asStatic)`, yielding `JsClassMember`s with their `Origin` on the accessor, as `MethodLowering.Indexer` does; drop the `StaysOnTheServer` call (inert for an event, design.md) and delete `EmitEventAccessors`
- [x] 1.2 `EmitStaticMembers` writes each member the new writer yields where its loop over `EventDeclarationSyntax` is now. Verify: the #746 theory `AnEventWithAccessors_LowersToThem` in `EventAccessorConformanceTests` passes both modes, and the transpiled pins (`EQ_REQUIRE_JS=1`, no `EQ_UPDATE_TRANSPILED`) do not move for any class

## 2. Every twin calls it

- [x] 2.1 `RecordTypeEmitter.EmitModule` appends `Written(member)` for each event of the record or struct, after the indexers
- [x] 2.2 `TypeScriptEmitter.Emit(ComponentDefinition)` writes each event of `component.ClassSyntax` inside the class callback, `asStatic: false`
- [x] 2.3 Prove it as `docs/PROVING.md` says. Verify: `EQ_REQUIRE_JS=1 dotnet test tests/eQuantic.UI.Conformance.Tests --filter "FullyQualifiedName~EventAccessorConformanceTests"` passes 4 of 4, with .NET's 21, 52 and 52 on both sides; it fails with 2.1 reverted (the record and struct rows throw) and with 2.2 reverted (the component row throws), the most plausible wrong fixes

## 3. Proved against the real thing

- [x] 3.1 What else subscribes, composes or writes a twin. Verify: the Conformance tests of events, delegates, records, structs and components pass, the Compiler tests of events and explicit implementations pass, and the transpiled libraries' pins pass with only the expected `$add$`/`$remove$` lines added for a type that declares an event with accessors
- [x] 3.2 Docs. Verify: the wiki's SupportedFeatures page, in all five languages, no longer says an event's `add` and `remove` accessors "are not written yet" (the partial-members row) and its Events row names records, structs and components; a ledger entry under `docs/ledger/` cites #1445
