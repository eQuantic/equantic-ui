Every failing test named below is already in the tree, written by the diagnosis: make it pass, do not
rewrite it. Every run goes through the host lock the delivery's `state.json` names. No group touches the
runtime: its `addToEvent` already calls `$add$<e>` where a twin holds it, so no TypeScript twin moves. No
`PublicAPI.Unshipped.txt` and no line of `developer-surface.baseline.txt` moves.

## 1. The server registers no accessor (#1512 H1)

- [ ] 1.1 `ServerActionRegistry.ScanAssembly` keeps a method only when `!method.IsSpecialName`
- [ ] 1.2 `AppSurface.HasActions` keeps a method only when `!method.IsSpecialName`
- [ ] 1.3 Prove it: `tests/eQuantic.UI.Server.Tests/ServerActionAccessorTests.cs`, 6 of 6 failing today,
      passes; `AnAccessorMarkedAsAnAction_MakesNoAppHaveActions` fails again with 1.2 alone reverted (the most
      plausible wrong fix: the registry filtered and the surface not); the rest of `eQuantic.UI.Server.Tests`
      passes

## 2. One dispatch for the defaults a type takes (#1511, no behaviour)

- [ ] 2.1 An abstract `DefaultMemberWriter` in `src/eQuantic.UI.Compiler/CodeGen/` owns the loop over
      `DefaultInterfaceMembers.Of`, the `Homeless` refusal, the `InFileOf`, the choice of arm, the delegated
      arm and the `Unreadable` refusal (EQ1008); its abstract members are a method, a property, an
      indexer, an event and a delegated default
- [ ] 2.2 `TypeScriptEmitter.EmitInheritedDefaults` becomes its class-and-component writer, with the
      writers it has today (`EmitClassMethod`, `EmitGetter`/`EmitSetter`, `EmitIndexer`,
      `EmitDelegatedDefault`), and `RecordTypeEmitter`'s switch its record-and-struct writer (`EmitMethod`,
      `ComputedProperty`, `_lowering.Indexer` after `SetCurrentClass`, its delegated text); the property
      arms lose their `when` guards (design decision 2)
- [ ] 2.3 The event member of each writer refuses for now with the `Unreadable` message, so 2 lands with
      no output change; `Delegation` takes a method or a property only, and an event goes to `Unreadable`
- [ ] 2.4 Prove it: the component pins (`SharedComponentTranspilationTests`) pass with no
      `EQ_UPDATE_TRANSPILED`, and `DefaultInterfaceMemberEmissionTests`, the records' and the interfaces'
      conformance suites pass unchanged

## 3. A default event reaches every twin (#1511)

- [ ] 3.1 `DefaultInterfaceMembers.Of` admits an `IEventSymbol` on both walks (the interface members and
      the private helpers), never an accessor method
- [ ] 3.2 Each writer's event member calls `EventAccessors(e, …, asStatic: false)`
      (`Lowering.` in the class emitter, `_lowering.` after `SetCurrentClass` in the record emitter)
- [ ] 3.3 `OverloadedMethods`: a default event, among the class's and its bases' defaults, claims
      `EventAccessors.Name("add", twin)` and `("remove", twin)`, never `MemberTwinName()`
- [ ] 3.4 Add the EQ1007 row the spec names: `class Both : IA, IB`, each with a default `event Action E`,
      fails naming both defaults (`DefaultInterfaceMemberEmissionTests`, beside the `Ringing` fixture)
- [ ] 3.5 Prove it: `EventAccessorConformanceTests.AnInterfacesDefaultEvent_ReachesEveryTwin` with
      `EQ_REQUIRE_JS=1`, both variants, executed on both sides (`52|1` on five rows, `2200|0` on `Siren`);
      `APlainClassTakesTheDefaultEventsAccessors` and `AClassMethodOnADefaultEventsNameIsNoClash` pass, and
      the second fails with 3.3 reverted (the wrong claim of `rang`); the pins still pass unchanged

## 4. Every component path writes its twin members (#1513)

- [ ] 4.1 In `TypeScriptEmitter.Emit(ComponentDefinition)`, move `EmitComponentProperties`, the
      `TypeInitializer.Members` block and the indexer loop out of the concrete branch to just before
      `if (component.IsPrimitive)`, in that order; leave the branch's condition as it is
- [ ] 4.2 Prove it: `IndexerConformanceTests.EveryComponentPathsIndexer_ReachesItsTwin` with
      `EQ_REQUIRE_JS=1`, both variants (`815`, `27`, `2`, `13`), and
      `ComponentTwinMemberTests.AComponentsIndexer_IsWrittenOnEveryPath` pass; the component pins pass with no
      `EQ_UPDATE_TRANSPILED` (a moved pin is a defect of 4.1, not a pin to refresh); the dashboard sample
      builds (`dotnet build samples/DefaultUIDashboard`)

## 5. A server mark on an accessor is refused (#1512 H2, H3c)

- [ ] 5.1 A declaration check run beside EQ1007's in `ComponentCompiler` (`DeclarationOf(component)`), a
      class of its own imitating `OverloadedMethods.Check`: each `AccessorDeclarationSyntax` of the type,
      through its symbol (`PlainClassModule.IsServerAction` / `IsServerOnly`), refuses `[ServerAction]` on
      any accessor and `[ServerOnly]` on a property's or an indexer's, with EQ2016 at the accessor, naming
      the type, the member, the accessor and the `[ServerAction]`/`[ServerOnly]` method to write instead
- [ ] 5.2 The accessor rule reads `[ServerOnly]` alone: `ComponentParser.StaysOnTheServer`'s accessor arm and
      `IsServerOnlyAccessor` lose `inAComponent`; `MethodLowering.EventAccessors` and
      `HostOnlySymbolExtensions.ReportIfAccessorStaysOnTheServer` lose their `IsUiComponent()`; their XML
      documentation and EQ2010's message drop "or a component's [ServerAction]"
- [ ] 5.3 Move the two #1445 rows: `Meter.Served`'s `[ServerAction] remove` leaves `KeptAccessors` (its row
      becomes an EQ2016 case of `AServerMarkOnAnAccessorTests`), and the third `InlineData` of
      `ASubscriptionToAServerOnlyAccessor_IsRefused` leaves that theory for the same file
- [ ] 5.4 Pin `AServerMarkOnAnAccessorTests` to `EQ2016` (it asserts `error EQ` and the member today), and
      regenerate `tests/eQuantic.UI.Compiler.Tests/Coverage/diagnostics.baseline.txt` with
      `EQ_UPDATE_DIAGNOSTICS_BASELINE=1`, reading the diff: EQ2016 gains one file and nothing else moves
- [ ] 5.5 Prove it: `AServerMarkOnAnAccessorTests` 5 of 5 (plus the moved rows), the event-accessor
      conformance suite with `EQ_REQUIRE_JS=1` (the `[ServerOnly]` rows still run as in .NET), and
      `DiagnosticsDocumentedTests` after 7.1

## 6. A subscription through a contract meets the fence (#1512 H3a)

- [ ] 6.1 `ServerOnlyContracts.ServerOnlyMethods` yields the server-only accessors of events too, by the
      same `StaysOnTheServer` rule; the interface loop admits `MethodKind.EventAdd` and `EventRemove`
- [ ] 6.2 `ReportIfAccessorStaysOnTheServer` asks `ReportIfFulfilledOnTheServer` when the direct question is
      silent, with the subscription's receiver type (the member access on the assignment's left), and the
      message names the accessor that keeps the body
- [ ] 6.3 Prove it: `ASubscriptionThroughAContractAServerOnlyAccessorFulfils_IsRefused` (2 of 2 failing
      today) passes, `ASubscriptionNoServerOnlyAccessorAnswers_Builds` still passes (the guard against refusing
      too much), and the first fails again with 6.1's `MethodKind` admission reverted; the client-boundary
      suites for methods through a contract pass unchanged

## 7. Documentation and the whole proof

- [ ] 7.1 `docs/DIAGNOSTICS.md`: the EQ2016 row (both cases, what to write instead) and the section's range
      `EQ2001–EQ2016`; EQ2010's row drops "or `[ServerAction]`, on a component's" and gains the
      subscription through a contract; EQ1007's row says a default event takes its accessors' two names
- [ ] 7.2 The wiki, on a branch named `fix/every-accessor-and-indexer-reaches-its-twin`, in all five
      languages: Diagnostics (the same three rows), SupportedFeatures (the Interfaces row: a default event;
      the server-only fence: a subscription through a contract), and Upgrading's "To 0.2.0-preview.69" in
      English and Portuguese (the EQ2016 break and its migration line); `WikiDiagnosticsTests` passes
      against that branch through `EQ_WIKI_DIR`
- [ ] 7.3 One ledger entry under `docs/ledger/` citing #1511, #1512 and #1513
- [ ] 7.4 File under #1174 the class's own accessor event that claims `<e>` in EQ1007 (design, Open)
- [ ] 7.5 The whole suites, through the lock: `eQuantic.UI.Compiler.Tests`, `eQuantic.UI.Conformance.Tests`
      with `EQ_REQUIRE_JS=1`, `eQuantic.UI.Server.Tests`, `eQuantic.UI.Web.Tests`; the samples build;
      `./scripts/check-openspec.sh` passes, and the change is archived with `openspec archive
      every-accessor-and-indexer-reaches-its-twin --yes` before the merge
