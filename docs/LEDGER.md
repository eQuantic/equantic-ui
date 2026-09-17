# The ledger

What happened, when, and where the record is. One line per event, oldest first, each pointing at
the pull request, the release, the issue or the document section that holds the evidence. This file
is the chronology; [`ARCHITECTURE-AUDIT.md`](ARCHITECTURE-AUDIT.md) is the measured state; the
[board](https://github.com/orgs/eQuantic/projects/11) is the work, and its snapshot at the foot of
this file names every issue.

A plan whose slices are all delivered is retired into this ledger rather than kept: its dated status
lines come here, condensed, and whatever it still owed became an issue. A plan that code or other
documents cite stays where it is until the citation moves. The rule for an entry: what happened,
then the link. A number without a link is a claim; a link without a date is a story.

Pull requests are `#n` in [eQuantic/equantic-ui](https://github.com/eQuantic/equantic-ui/pulls);
issues live in the same numbering. Releases are the annotated tags — the tag message is the full
record of a release, the wiki's Upgrading page is the distillate.

## Chronology

### 2026-06 — the transpiler earns its bar

- **Phase 1, transpiler correctness.** The conformance harness — every fixture executed on both
  sides, C# under .NET and the emitted JavaScript under bun — reached 492 green cases and the
  supported-subset diagnostics went in; the plan declared itself essentially complete on
  2026-06-10. *(retired `IMPLEMENTATION-PLAN.md`; the harness is
  `tests/eQuantic.UI.Conformance.Tests`; the program that followed is
  [`DOTNET-COVERAGE-PROGRAM.md`](DOTNET-COVERAGE-PROGRAM.md).)*
- **Phase 2, the client router.** Route table generated from `[Page]`, a `Router` in the runtime
  intercepting internal navigation, the real-build sample, and the build-infrastructure fixes it
  forced; all exit criteria met by 2026-06-10. *(retired `PHASE-2-CLIENT-ROUTER-PLAN.md`; the router
  is `src/eQuantic.UI.Runtime/src/router`.)*
- **The compile-time class evaluator.** `[CompileTimeEvaluate]`, `CompileTimeEvaluator`,
  `CssEmitter`, `StyleClass` and four documents were built for a Tailwind adapter. The adapter was
  removed on 2026-08-08 (0704a3d0); the attribute is applied to zero types; the code is the
  adapter's shadow. *(the four documents are retired here; deleting the code is
  [#214](https://github.com/eQuantic/equantic-ui/issues/214) under
  [#167](https://github.com/eQuantic/equantic-ui/issues/167).)*
- **Photon M0.** The native GPU engine kicks off — W3/W7 slices and the engine-facing seam of W1
  (2026-06-10). *([`NATIVE-GPU-ENGINE-PLAN.md`](NATIVE-GPU-ENGINE-PLAN.md).)*

### 2026-07 — write once

- **2026-07-03 · The write-once core lands**: the abstract vocabulary, C# flex layout and the native
  realizer. **2026-07-04 · Web realizer, slice 1.** *([`SHARED-COMPONENTS-PLAN.md`](SHARED-COMPONENTS-PLAN.md),
  [`NATIVE-GPU-ENGINE-PLAN.md`](NATIVE-GPU-ENGINE-PLAN.md).)* The pre-write-once component model is
  excised in stages through August and finally on both sides of the compiler in
  [#77](https://github.com/eQuantic/equantic-ui/pull/77) (2026-09-04).
- **2026-07-31 · Live window resize** (`PhotonHost.Resize`) and **D3, the offline `metallib`**
  through `generate-shaders.sh`. *([`NATIVE-GPU-ENGINE-PLAN.md`](NATIVE-GPU-ENGINE-PLAN.md).)*
- **2026-07-31 · Style semantics, the pre-S1 photograph** — CSS-free authoring and the atomic style
  engine begin. *([`STYLE-SEMANTICS-PLAN.md`](STYLE-SEMANTICS-PLAN.md); what v1 fenced is
  [#203](https://github.com/eQuantic/equantic-ui/issues/203).)*

### 2026-08 — the tracks

- **2026-08-01 · i18n plan written** (design only, motivated by the site dogfood); culture routes
  `/pt-BR/…` deliver on 2026-08-21 over `RequestLocalization`.
  *([`I18N-PLAN.md`](I18N-PLAN.md); the v1 fences are
  [#206](https://github.com/eQuantic/equantic-ui/issues/206).)*
- **2026-08-04 · Track D, device capabilities, D1–D7 closed in one day**: capabilities as services
  discovered per shell and registered last so a test's fake wins; the photo library through the
  macOS open panel; biometrics (the first capability that did not work yet, and the malformed macOS
  bundle it exposed); network status (the first answer that does not hold still); motion sensors;
  location, where `PermissionState` earns its three values; and the camera — live video in the
  window. The lesson kept: an attribute crossing assemblies travels by NAME, never by enum ordinal.
  *(retired `DEVICE-CAPABILITIES-PLAN.md`; the interfaces are `Primitives/Contracts`; the parity row
  is `MethodChannel` in [`FLUTTER-PARITY.md`](FLUTTER-PARITY.md).)*
- **2026-08-07 · `dotnet new equantic-app`**, the version stamped at pack time.
  **2026-08-08 · The declarative surface**: factories instead of `new`, no overloads, a factory
  mirrors its constructor; the app's own components join it through a source generator. The first
  release tag, **0.2.0-preview.1**, is cut the same day, and by 2026-08-24 forty-three previews have
  been cut as the site and the studio dogfood the SDK.
- **2026-08-14 · The visual editor's decision**: declarative is the preferred form and the fence
  stays there, but *understanding* is not fenced — reading, selecting, inspecting and property-editing
  work over any code, only structural insertion is held to `children: [...]`. Phases 0–18 land
  through August (design host compiling the buffer at p50 293 ms, `VisualNode.Origin`,
  click-to-select proved over 356 of 356 simulated clicks, inspector, write-back, palette, canvas
  actions, reparenting, keyboard, installable extension, native preview, state across recompiles, a
  Photon frame from the engine). *(retired `VSCODE-VISUAL-EDITOR-PLAN.md`; the tiers phase 3 still
  owes are [#215](https://github.com/eQuantic/equantic-ui/issues/215).)*
- **2026-08-16 · Handoff fidelity audit**: the design system's component blocks compared with the
  shipped components, claim by claim, with a "closed since" ledger.
  *([`HANDOFF-FIDELITY-AUDIT.md`](HANDOFF-FIDELITY-AUDIT.md); the visible deviations still open are
  [#207](https://github.com/eQuantic/equantic-ui/issues/207).)*
- **The date and time pickers**: slice 1 the grid vocabulary (`Navigable`, `GridCell`, `Dialog`)
  with the Shortcut question answered; slice 2 culture as data, where the cross-pin changed the
  design; slice 4 the three wrappers and the typed row. *(retired `DATE-PICKER-PLAN.md`; what is
  owed — the range mode, the year grid, the mobile tier, a date-shaped `FieldRule` — is
  [#202](https://github.com/eQuantic/equantic-ui/issues/202); the C15 spec stays in
  [`design/`](design/README.md).)*
- **2026-08-23 · The wiki's second audit**: measured numbers rot like prose, diagnostics went
  unmentioned, one code meant two things; a guard with a baseline now compiles the wiki's claims
  (`WikiClaimsCompile`, `WikiVocabularyTests`, `WikiVersionMarkTests` in `tests/eQuantic.UI.Web.Tests`).
  The same day, the resx packaging decision: one package, not one per language.
- **2026-08-25 · 0.2.0-preview.44** closes the first burst of previews; Track W, Photon on the
  desktop, is planned in [#51](https://github.com/eQuantic/equantic-ui/pull/51) and its developer
  loop in [#52](https://github.com/eQuantic/equantic-ui/pull/52). *([`DESKTOP-PLAN.md`](DESKTOP-PLAN.md).)*

### 2026-09 — the audit

- **2026-09-01 · 0.2.0-preview.45** ([#57](https://github.com/eQuantic/equantic-ui/pull/57)); the
  bundle is the payload — RID allowlist, rebuilt not accreted, signed or failed
  ([#56](https://github.com/eQuantic/equantic-ui/pull/56)); consent before cookies
  ([#58](https://github.com/eQuantic/equantic-ui/pull/58)); the package set discovered from
  `IsPackable` ([#59](https://github.com/eQuantic/equantic-ui/pull/59)).
- **2026-09-02 · `[ServerOnly]` on a class** ([#60](https://github.com/eQuantic/equantic-ui/pull/60));
  entitlements in C#, and a runtime the SDK speaks for ([#66](https://github.com/eQuantic/equantic-ui/pull/66));
  `IUiDispatcher` ([#64](https://github.com/eQuantic/equantic-ui/pull/64)); W2 curves, transitions
  and a frame clock ([#69](https://github.com/eQuantic/equantic-ui/pull/69)).
- **2026-09-03 · The architecture audit, first pass — fourteen findings** measured against the tree:
  the layering, the dispatches over the vocabulary, the weight of each assembly, the duplications,
  an order of attack. *([`ARCHITECTURE-AUDIT.md`](ARCHITECTURE-AUDIT.md), pinned by
  `VocabularyCoverageTests`, `AssemblyLayeringTests`, `HandoffVocabularyTests`.)* The same day: W3,
  a component draws its own pixels ([#70](https://github.com/eQuantic/equantic-ui/pull/70));
  `Bookmark` ([#72](https://github.com/eQuantic/equantic-ui/pull/72)); an icon pack ships only the
  glyphs an app names ([#74](https://github.com/eQuantic/equantic-ui/pull/74)); a Photon app from
  `dotnet publish` ([#75](https://github.com/eQuantic/equantic-ui/pull/75));
  **0.2.0-preview.46** ([#73](https://github.com/eQuantic/equantic-ui/pull/73)).
- **2026-09-04 · The excision completes**: the pre-write-once model is gone on both sides of the
  compiler ([#77](https://github.com/eQuantic/equantic-ui/pull/77)); the vocabulary stops speaking
  the web's language ([#81](https://github.com/eQuantic/equantic-ui/pull/81)); the TypeScript
  generators leave the assembly every web app installs ([#79](https://github.com/eQuantic/equantic-ui/pull/79)).
  **0.2.0-preview.47** ([#78](https://github.com/eQuantic/equantic-ui/pull/78)) and **.48**
  ([#82](https://github.com/eQuantic/equantic-ui/pull/82)).
- **2026-09-05 · `eQuantic.UI.Core` dissolved** into the assemblies that owned its parts
  ([#83](https://github.com/eQuantic/equantic-ui/pull/83)); four things consumers found on .48
  ([#84](https://github.com/eQuantic/equantic-ui/pull/84)); the build tools become edges of the
  build graph ([#88](https://github.com/eQuantic/equantic-ui/pull/88)); **0.2.0-preview.49**
  ([#89](https://github.com/eQuantic/equantic-ui/pull/89)); charts slice 0, the plan and the data
  palette ([#90](https://github.com/eQuantic/equantic-ui/pull/90)). *([`CHARTS-PLAN.md`](CHARTS-PLAN.md);
  slices 2–5 are [#199](https://github.com/eQuantic/equantic-ui/issues/199), the two open decisions
  [#200](https://github.com/eQuantic/equantic-ui/issues/200) and
  [#201](https://github.com/eQuantic/equantic-ui/issues/201).)*
- **2026-09-06 · The Windows shell** — a Photon window on Win32
  ([#92](https://github.com/eQuantic/equantic-ui/pull/92)); charts slice 1, the bar chart drawn by
  both realizers ([#95](https://github.com/eQuantic/equantic-ui/pull/95)) and the three old defects
  it revealed — `Stack` measuring children, `Key` ignored, a canvas without a pointer
  ([#97](https://github.com/eQuantic/equantic-ui/pull/97)); **0.2.0-preview.50**
  ([#96](https://github.com/eQuantic/equantic-ui/pull/96)).
- **2026-09-07 · A regional culture falls back through its parent, not past it**
  ([#101](https://github.com/eQuantic/equantic-ui/pull/101)) — the bug class neither the code nor
  the sample could see, because the SDK's own cultures are all siblings of the neutral.
- **2026-09-08 · Photon honours `Text.Align`** ([#103](https://github.com/eQuantic/equantic-ui/pull/103))
  — the property one realizer had dropped in silence, which a cross-pin cannot see.
- **2026-09-12 · The warnings that were hiding something** ([#106](https://github.com/eQuantic/equantic-ui/pull/106));
  a contained failure is not a silent one ([#110](https://github.com/eQuantic/equantic-ui/pull/110)).
- **2026-09-13 · A theme names its faces** ([#111](https://github.com/eQuantic/equantic-ui/pull/111));
  the handoff lives in the repo and the implementation is pinned to it
  ([#112](https://github.com/eQuantic/equantic-ui/pull/112), `HandoffTokenPinTests`); a cold load
  spends its chance on the correction, not the measurement ([#115](https://github.com/eQuantic/equantic-ui/pull/115),
  found by a consumer on .51); **0.2.0-preview.51** ([#114](https://github.com/eQuantic/equantic-ui/pull/114))
  and **.52** ([#116](https://github.com/eQuantic/equantic-ui/pull/116)).
- **2026-09-14 · The audit's second pass**: six doors pinned, a seventh defect (the SSR of the
  surfaces), Visitor over Strategy, geometry above the vocabulary, `Element` as a string,
  `PhotonHost` as seven bindings ([#120](https://github.com/eQuantic/equantic-ui/pull/120)). The
  layout constraint becomes a value ([#119](https://github.com/eQuantic/equantic-ui/pull/119)); the
  server writes the grid it used to drop ([#121](https://github.com/eQuantic/equantic-ui/pull/121));
  the vocabulary's dispatch plan, one door per node
  ([#122](https://github.com/eQuantic/equantic-ui/pull/122), [`VOCABULARY-DISPATCH-PLAN.md`](VOCABULARY-DISPATCH-PLAN.md));
  an index for `docs/` ([#124](https://github.com/eQuantic/equantic-ui/pull/124)); the open door —
  releases from the tags, community files, package metadata ([#134](https://github.com/eQuantic/equantic-ui/pull/134));
  geometry is the vocabulary's own ([#135](https://github.com/eQuantic/equantic-ui/pull/135)); one
  route and one capability resolver at the web seam ([#137](https://github.com/eQuantic/equantic-ui/pull/137));
  **S1 — the vocabulary accepts a visitor and the hierarchy is closed by construction**
  ([#138](https://github.com/eQuantic/equantic-ui/pull/138)); the suite runs where the code runs,
  three runners ([#139](https://github.com/eQuantic/equantic-ui/pull/139)); the folders say what is in
  them ([#140](https://github.com/eQuantic/equantic-ui/pull/140)); what a node is to a screen reader
  belongs to the vocabulary ([#141](https://github.com/eQuantic/equantic-ui/pull/141)); what the
  first slices taught ([#142](https://github.com/eQuantic/equantic-ui/pull/142));
  **0.2.0-preview.53** ([#125](https://github.com/eQuantic/equantic-ui/pull/125)).
- **2026-09-15 · A comment one indent too far down turned the whole workflow off**
  ([#145](https://github.com/eQuantic/equantic-ui/pull/145)) — `#` inside an `if: |` block scalar is
  text, the workflow failed to parse, zero jobs ran for an hour and seven pull requests merged on
  local runs alone; the ruleset requires reviews and no status check, which is recorded as a
  decision still open. **Zero warnings**: `TreatWarningsAsErrors` on every project
  ([#126](https://github.com/eQuantic/equantic-ui/pull/126)). A painter takes a box, not four floats
  ([#143](https://github.com/eQuantic/equantic-ui/pull/143)). **0.2.0-preview.54**
  ([#151](https://github.com/eQuantic/equantic-ui/pull/151)). The SDK does not dictate which Xcode
  you have ([#149](https://github.com/eQuantic/equantic-ui/pull/149)); an entitlement answers to its
  own family ([#153](https://github.com/eQuantic/equantic-ui/pull/153)); two instruments that said
  what was no longer true ([#154](https://github.com/eQuantic/equantic-ui/pull/154)); step 3 closed
  and five rules for the nets ([#152](https://github.com/eQuantic/equantic-ui/pull/152)).
- **2026-09-15 · The first external contribution**: `ButtonStyles` moves into Components
  ([#148](https://github.com/eQuantic/equantic-ui/pull/148)), and the review measured the no-model
  fallback's two wrong shapes for `[RuntimeProvided]` types outside Primitives
  ([#150](https://github.com/eQuantic/equantic-ui/issues/150)). Then one shape for a control's
  metrics, and it is the ladder ([#155](https://github.com/eQuantic/equantic-ui/pull/155), closing
  [#184](https://github.com/eQuantic/equantic-ui/issues/184)).
- **2026-09-15 · The backlog becomes a hierarchy**: the audit's findings, the parity gaps, the
  dispatch plan's slices and each track's remainder are epics, features and stories on the public
  board — [#157](https://github.com/eQuantic/equantic-ui/issues/157) one vocabulary, one door per
  node · [#158](https://github.com/eQuantic/equantic-ui/issues/158) Primitives carries only the
  vocabulary · [#159](https://github.com/eQuantic/equantic-ui/issues/159) hosts and realizers hold
  what Flutter splits · [#160](https://github.com/eQuantic/equantic-ui/issues/160) instruments that
  fail, not warn · [#190](https://github.com/eQuantic/equantic-ui/issues/190) Flutter parity, the
  gaps that are work · [#198](https://github.com/eQuantic/equantic-ui/issues/198) the tracks in
  flight · [#208](https://github.com/eQuantic/equantic-ui/issues/208) the audit continues. The
  organisation gains the Epic and User Story issue types; nine finished or orphaned documents are
  retired into this file (below).
- **2026-09-16 · The first two dispatches leave the switch**: the semantics walk becomes
  `SemanticsVisitor` — thirteen nodes announce, twenty-seven decline through a constant named for
  its reason ([#177](https://github.com/eQuantic/equantic-ui/issues/177)) — and both email
  alternatives become visitors over ONE shared refusal set, `EmailWalk`, which closes the
  divergence where the HTML part threw on a node the plain-text part skipped in silence
  ([#178](https://github.com/eQuantic/equantic-ui/issues/178)). `VocabularyCoverageTests` goes from
  six dispatches to four; the two it lost are answered by the compiler now, and the thirty-three
  nodes email refuses are checked through both alternatives by `EmailRefusalParityTests`.
- **2026-09-17 · The browser's door closes too**: `NodeKind` is a GENERATED TypeScript union
  (`node-kinds.generated.ts`, byte-pinned beside the enum unions), `nodeKind` is that union on both
  the wire shapes and the runtime classes instead of `string`, and `lowerNodeKind` ends in
  `assertNever` — deleting one arm makes `tsc` refuse, which is the exhaustiveness the C# side gets
  from a visitor and the client could not have, since class names do not survive bundling
  ([#179](https://github.com/eQuantic/equantic-ui/issues/179)). The three rules that make a wire kind
  an identity move into the generator, so `VocabularyCoverageTests` can retire without losing them;
  the TypeScript dispatch leaves it and three C# dispatches remain. Settled the same day, on purpose
  rather than by default: a page does NOT walk a tree — `Accept` stays `[ServerOnly]` and the twins
  get no `accept`.
- **2026-09-17 · The web realizer leaves its switch**: `WebLoweringVisitor` answers for all forty
  nodes across four family files, and `WebRealizer` keeps the façade and the gradient contract
  ([#180](https://github.com/eQuantic/equantic-ui/issues/180)). Row and Column gain separate doors,
  `CodeSurface` becomes the only node that may lower to nothing and says so at its arm, and
  `_simulated` stops being an `AsyncLocal` — the ceremony a static class needed, which a per-pass
  visitor does not. Four of the six dispatches have now crossed; `LayoutEngine.MeasureCore` and
  `PhotonRealizer.EmitNode` remain, and `VocabularyCoverageTests` is down to two entries.

## Retired documents

| document | what it was | where its substance lives now |
|---|---|---|
| `COMPILE-TIME-EVALUATION-INDEX.md`, `COMPILE-TIME-EVALUATION-SUMMARY.md`, `COMPILER-COMPILE-TIME-EVALUATION.md`, `COMPILER-IMPLEMENTATION-GUIDE.md` | 1,561 lines around a compile-time class evaluator for a Tailwind adapter removed on 2026-08-08; `[CompileTimeEvaluate]` is applied to zero types | [#214](https://github.com/eQuantic/equantic-ui/issues/214) deletes the evaluator; the audit's §9 records the measurement |
| `IMPLEMENTATION-PLAN.md` | Phase 1 — transpiler correctness and the conformance harness; complete 2026-06-10 | `tests/eQuantic.UI.Conformance.Tests`; [`DOTNET-COVERAGE-PROGRAM.md`](DOTNET-COVERAGE-PROGRAM.md); the compiler section of `CLAUDE.md` |
| `PHASE-2-CLIENT-ROUTER-PLAN.md` | Phase 2 — the client router; all exit criteria met 2026-06-10 | `src/eQuantic.UI.Runtime/src/router`, and `boot.ts` importing the page's module on navigation |
| `DEVICE-CAPABILITIES-PLAN.md` | Track D — typed device capabilities instead of method channels; D1–D7 closed 2026-08-04 | the capability interfaces in `Primitives/Contracts`; the wiki's Capabilities page; the `MethodChannel` row of [`FLUTTER-PARITY.md`](FLUTTER-PARITY.md) |
| `DATE-PICKER-PLAN.md` | the date and time pickers; slices 1, 2 and 4 delivered | what is owed is [#202](https://github.com/eQuantic/equantic-ui/issues/202); the C15 spec stays in [`design/`](design/README.md) |
| `VSCODE-VISUAL-EDITOR-PLAN.md` | Track E — the visual editor in VS Code; phases 0–18 done, the decision of 2026-08-14 | the tiers are [#215](https://github.com/eQuantic/equantic-ui/issues/215); the design host is `src/eQuantic.UI.Design*`, the extension `extensions/vscode` |

Still standing, on purpose: [`NATIVE-GPU-ENGINE-PLAN.md`](NATIVE-GPU-ENGINE-PLAN.md) and
[`HANDOFF-FIDELITY-AUDIT.md`](HANDOFF-FIDELITY-AUDIT.md) hold design and open items that have not
yet become wiki pages or stories ([#204](https://github.com/eQuantic/equantic-ui/issues/204),
[#207](https://github.com/eQuantic/equantic-ui/issues/207)); the four track plans in flight are
cited by code comments and by each other.

## The board, as of 2026-09-15

Every issue on [project #11](https://github.com/orgs/eQuantic/projects/11), by parent. The board
is the live view; this table is the snapshot the retirement above was made against. Closed items
are struck through.

- [#157](https://github.com/eQuantic/equantic-ui/issues/157) One vocabulary, one door per node *(Epic)*
  - [#161](https://github.com/eQuantic/equantic-ui/issues/161) A visitor per dispatch over the vocabulary *(Feature)*
    - [#177](https://github.com/eQuantic/equantic-ui/issues/177) S2 · Semantics.Walk becomes SemanticsVisitor *(User Story)*
    - [#178](https://github.com/eQuantic/equantic-ui/issues/178) S3 · EmailRealizer and WalkText become two visitors with one refusal set *(User Story)*
    - [#179](https://github.com/eQuantic/equantic-ui/issues/179) S7 · NodeKind generated for TypeScript, assertNever in the browser's switch *(User Story)*
    - [#180](https://github.com/eQuantic/equantic-ui/issues/180) S4 · WebRealizer.LowerNodeKind becomes WebLoweringVisitor *(User Story)*
    - [#181](https://github.com/eQuantic/equantic-ui/issues/181) S5 · LayoutEngine.MeasureCore becomes MeasureVisitor with MeasureState *(User Story)*
    - [#182](https://github.com/eQuantic/equantic-ui/issues/182) S6 · PhotonRealizer.EmitNode becomes EmitVisitor *(User Story)*
    - [#183](https://github.com/eQuantic/equantic-ui/issues/183) S8 · VocabularyCoverageTests is deleted *(User Story)*
  - [#162](https://github.com/eQuantic/equantic-ui/issues/162) Node shapes: SingleChildNode, the intrinsic questions, VisualNode.cs split *(Feature)*
  - [#132](https://github.com/eQuantic/equantic-ui/issues/132) One transpiled set in the runtime, not two *(Feature)*
    - ~~[#184](https://github.com/eQuantic/equantic-ui/issues/184) Fold ButtonStyles into Button over Sizing; no generator entry, no runtime export~~ *(User Story)*
  - [#163](https://github.com/eQuantic/equantic-ui/issues/163) The vocabulary's TypeScript twins are emitted by eqc *(Feature)*
  - [#164](https://github.com/eQuantic/equantic-ui/issues/164) The transpiler's fences hold on every path *(Feature)*
    - [#146](https://github.com/eQuantic/equantic-ui/issues/146) 🐛 fix: a float that leaves a method is not rounded, so the twin keeps a double *(Bug)*
    - [#150](https://github.com/eQuantic/equantic-ui/issues/150) The no-model fallback has no route for [RuntimeProvided] types outside Primitives *(Task)*
- [#158](https://github.com/eQuantic/equantic-ui/issues/158) Primitives carries only the vocabulary *(Epic)*
  - [#165](https://github.com/eQuantic/equantic-ui/issues/165) The Primitives diet *(Feature)*
    - [#185](https://github.com/eQuantic/equantic-ui/issues/185) Decide where the Code and Sheet editor models live *(User Story)*
    - [#186](https://github.com/eQuantic/equantic-ui/issues/186) Decide per-component theme slots on IAppTheme *(User Story)*
    - [#128](https://github.com/eQuantic/equantic-ui/issues/128) PaletteAudit ships in every bundle and every AOT image; it belongs in tests or an analyzer *(Task)*
    - [#129](https://github.com/eQuantic/equantic-ui/issues/129) The Photon* declaration attributes live in the neutral assembly; move them to Native.Hosting *(Task)*
    - ~~[#130](https://github.com/eQuantic/equantic-ui/issues/130) Two web words in the vocabulary's public API: Navigator.Go(href) and CookieConsent.PolicyHref~~ *(Task)*
    - ~~[#131](https://github.com/eQuantic/equantic-ui/issues/131) Primitives/Nodes holds nine files that are not nodes~~ *(Task)*
    - [#133](https://github.com/eQuantic/equantic-ui/issues/133) ImageOptimizationState is written by UseImageOptimization and read by nothing *(Bug)*
  - [#166](https://github.com/eQuantic/equantic-ui/issues/166) Geometry and semantics live in the vocabulary *(Feature)*
    - [#187](https://github.com/eQuantic/equantic-ui/issues/187) A container semantic role unmutes Navigable and Overlay on Photon *(User Story)*
  - [#167](https://github.com/eQuantic/equantic-ui/issues/167) The adapter's shadow leaves the tree *(Feature)*
    - [#214](https://github.com/eQuantic/equantic-ui/issues/214) Delete the compile-time evaluator and the documents that describe it, and the two plans that cite a roadmap that is not in docs/ *(User Story)*
- [#159](https://github.com/eQuantic/equantic-ui/issues/159) Hosts and realizers hold what Flutter splits *(Epic)*
  - [#168](https://github.com/eQuantic/equantic-ui/issues/168) PhotonHost split into its bindings *(Feature)*
  - [#169](https://github.com/eQuantic/equantic-ui/issues/169) The Element decision *(Feature)*
  - [#170](https://github.com/eQuantic/equantic-ui/issues/170) The truncation mark on DirectWrite *(Feature)*
  - [#171](https://github.com/eQuantic/equantic-ui/issues/171) The server writes CodeSurface *(Feature)*
  - [#172](https://github.com/eQuantic/equantic-ui/issues/172) A write-once page reaches data on both targets *(Feature)*
- [#160](https://github.com/eQuantic/equantic-ui/issues/160) Instruments that fail, not warn *(Epic)*
  - [#173](https://github.com/eQuantic/equantic-ui/issues/173) A public-surface baseline per shipped assembly *(Feature)*
  - [#174](https://github.com/eQuantic/equantic-ui/issues/174) Culture fixtures assert the mapping, never the host *(Feature)*
    - [#147](https://github.com/eQuantic/equantic-ui/issues/147) 🐛 fix: two pins compare the host's ICU, not this repository's code *(Task)*
    - ~~[#188](https://github.com/eQuantic/equantic-ui/issues/188) Delete the unused CultureDataFactAttribute~~ *(Task)*
  - [#175](https://github.com/eQuantic/equantic-ui/issues/175) Sizing generated from the handoff's tokens.json *(Feature)*
  - [#176](https://github.com/eQuantic/equantic-ui/issues/176) Release notes are written from the public-surface diff *(Feature)*
- [#190](https://github.com/eQuantic/equantic-ui/issues/190) Flutter parity: the gaps that are work *(Epic)*
  - [#191](https://github.com/eQuantic/equantic-ui/issues/191) Layout protocol: LayoutBuilder, authorable constraints, custom layout delegates, a sliver contract *(Feature)*
  - [#192](https://github.com/eQuantic/equantic-ui/issues/192) State primitives: what stands where Flutter has ValueNotifier, ChangeNotifier and ListenableBuilder *(Feature)*
  - [#193](https://github.com/eQuantic/equantic-ui/issues/193) Motion: Hero, simulations beyond the spring, and what AnimatedBuilder means in a declarative model *(Feature)*
  - [#194](https://github.com/eQuantic/equantic-ui/issues/194) Pointer, gestures and focus: a raw-pointer wrapper, HitTestBehavior, custom recognizers, a focus object *(Feature)*
  - [#195](https://github.com/eQuantic/equantic-ui/issues/195) One text-editing protocol across hosts *(Feature)*
  - [#196](https://github.com/eQuantic/equantic-ui/issues/196) Platform and lifecycle: app-state observers, a single media query, multiple windows, embedded views, work hand-off *(Feature)*
  - [#197](https://github.com/eQuantic/equantic-ui/issues/197) Semantics and text direction: MergeSemantics, ExcludeSemantics, RTL *(Feature)*
- [#198](https://github.com/eQuantic/equantic-ui/issues/198) The tracks in flight *(Epic)*
  - [#199](https://github.com/eQuantic/equantic-ui/issues/199) Charts: slices 2–5 of the chart library *(Feature)*
    - [#200](https://github.com/eQuantic/equantic-ui/issues/200) Decide whether eQuantic.UI.Charts is implicit in the SDK or an opt-in package *(User Story)*
    - [#201](https://github.com/eQuantic/equantic-ui/issues/201) Decide when the chart wrappers go: at slice 5, or as slices 1–3 land *(User Story)*
  - [#202](https://github.com/eQuantic/equantic-ui/issues/202) Date pickers: the calendar's range mode, year grid, mobile tier, and a date-shaped FieldRule *(Feature)*
  - [#203](https://github.com/eQuantic/equantic-ui/issues/203) Style semantics: the fences to revisit when a screen demands them *(Feature)*
  - [#204](https://github.com/eQuantic/equantic-ui/issues/204) Photon engine: the open questions the plan still lists *(Feature)*
    - [#205](https://github.com/eQuantic/equantic-ui/issues/205) Strike the codename question in NATIVE-GPU-ENGINE-PLAN.md: Photon shipped under that name *(Task)*
  - [#206](https://github.com/eQuantic/equantic-ui/issues/206) i18n: the fences of v1 to revisit — RTL and script coverage, three-form plurals, per-page catalogs *(Feature)*
  - [#207](https://github.com/eQuantic/equantic-ui/issues/207) Handoff fidelity: verify and fix the visible deviations *(Feature)*
  - [#215](https://github.com/eQuantic/equantic-ui/issues/215) Visual editor: the click-to-select tiers (Literal, Derived, Foreign) *(Feature)*
- [#208](https://github.com/eQuantic/equantic-ui/issues/208) The audit continues *(Epic)*
  - [#209](https://github.com/eQuantic/equantic-ui/issues/209) Measure the compiler's internals beyond file sizes *(Task)*
  - [#210](https://github.com/eQuantic/equantic-ui/issues/210) Measure the shells' own platform code *(Task)*
  - [#211](https://github.com/eQuantic/equantic-ui/issues/211) Measure the design host's DesignSession *(Task)*
  - [#212](https://github.com/eQuantic/equantic-ui/issues/212) Measure the Server's endpoint surface *(Task)*
  - [#213](https://github.com/eQuantic/equantic-ui/issues/213) Measure the TypeScript runtime's core/ and dom/ beyond the twins *(Task)*

## How to add an entry

One line under the date, oldest first: what happened, then the link that holds the evidence — a
pull request, a release tag, an issue, a document section. When a plan's last slice lands, move its
dated lines here, condensed, and retire the plan in the same pull request — unless code or another
document cites it, in which case the citation moves first. The board snapshot is regenerated when
the hierarchy changes shape, not on every issue.
