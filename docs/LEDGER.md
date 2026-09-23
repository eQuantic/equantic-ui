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
  `CodeSurface` becomes the only node with no lowering at all — the only one that answers null for
  every instance, said at its arm rather than in an exemption list — and
  `_simulated` stops being an `AsyncLocal` — the ceremony a static class needed, which a per-pass
  visitor does not. Four of the six dispatches have now crossed; `LayoutEngine.MeasureCore` and
  `PhotonRealizer.EmitNode` remain, and `VocabularyCoverageTests` is down to two entries.

- **2026-09-17 · One type per file, with the exceptions named**: a Roslyn census of every `.cs` under
  `src/` and a baseline that may only shrink, each entry carrying the count it was measured at and
  why the file is the unit ([#220](https://github.com/eQuantic/equantic-ui/issues/220)). 135 files,
  not the 132 the issue estimated. Found because splitting `WebRealizer.cs` had dropped
  `RealizedElement` for one build.
- **2026-09-17 · A cyclic component is contained**: `ComponentBoundary.ExpandContained` bounds the
  chain a realizer expands, so a component that builds itself renders the containment surface
  instead of taking the request or the frame down by an uncatchable stack overflow
  ([#221](https://github.com/eQuantic/equantic-ui/issues/221)). Four expansion sites across both
  realizers go through it; the email realizer deliberately does not, which is
  [#223](https://github.com/eQuantic/equantic-ui/issues/223).
- **2026-09-17 · The single-child shape is a type**: `SingleChildNode` for twenty of the twenty-one
  wrappers, the two engine lists collapsed onto it with their exceptions named, and `VisualNode.cs`
  split into 59 files, one per type ([#162](https://github.com/eQuantic/equantic-ui/issues/162)).
  Measuring the lists before removing them is the finding: they disagreed in eight places, and the
  disagreement is load-bearing — [#225](https://github.com/eQuantic/equantic-ui/issues/225) is where
  transparency is decided for all four readers. S5's prerequisite is done.

- **2026-09-17 · The layout dispatch leaves its switch**: `LayoutEngine.MeasureCore` becomes
  `MeasureVisitor` over `MeasureState`, and `LayoutEngine.cs` goes 1,932 → 442 lines
  ([#181](https://github.com/eQuantic/equantic-ui/issues/181)). The default arm was hiding two nodes:
  `Navigable` and `WebFrame` measured as a zero box, their reasons alive in an exemption array with
  nothing tying them to the code — a `WebFrame` cannot cross to a surface with no browser behind it,
  while a `Navigable` is a real node nobody has laid out, and only doors can hold that difference.
  FIVE OF THE SIX DISPATCHES HAVE NOW CROSSED; `PhotonRealizer.EmitNode` is the last, and
  `VocabularyCoverageTests` is down to one entry. Review found the cost that mattered: a frame is not
  one `Layout` call — the realizer lays out the page and then each `Overlay` — so a per-call visitor
  was one per layer, invisible to a budget with 0.1 KB of headroom and no overlay in any scene.

- **2026-09-18 · The last dispatch crosses, and the pin that policed them all retires**:
  `PhotonRealizer.EmitNode` becomes `EmitVisitor`, `PhotonRealizer.cs` goes 1,806 → 343 lines, and
  `VocabularyCoverageTests` is deleted ([#182](https://github.com/eQuantic/equantic-ui/issues/182),
  [#183](https://github.com/eQuantic/equantic-ui/issues/183)). ALL SIX HAVE CROSSED. The plan called
  this "the nine `is` branches become visits" and the shape was not that: `EmitNode` was TWO
  dispatches over the same node, and `Box` sat in both four hundred lines apart, so a single
  27-door visitor would have dropped the chrome of every clipping box. What made one door right is
  that nothing which `break`s ever touches its children — the three arms that descend are the three
  that `return`. A TWELFTH DOOR appeared that the pin could not have asked about: it named eleven
  exempt nodes, and `UiComponent` is abstract, which is outside the vocabulary the pin enumerates.
  What I first wrote on that door was WRONG — "cannot arrive" — and review challenging it, then a
  probe, settled it: making the door throw fails 428 of 1,237 Photon tests, because `MeasureWrapper`
  builds the LayoutNode with the component as its `Source` and adopts the built subtree beneath it.
  Absent from a file is not absent from the walk. And the harness refused the first arrangement **by two
  bytes** — a visitor with six fields cost 64 bytes a frame against a ceiling with 2 to spare — so
  the fields moved into the state and the visitor became a singleton: 75,714 bytes/frame,
  byte-identical to `main`. A budget with no headroom left is a real constraint, not a nuisance, and
  it bought a better design than the one it refused.

- **2026-09-18 · FlexNode stops promising a twin, and a hole in #226's fence is what it found**:
  `FlexNode` is `[ServerOnly]` and leaves the runtime pin's `NO_TWIN_OWED` list
  ([#228](https://github.com/eQuantic/equantic-ui/issues/228)). #226 had left it there deliberately
  — fencing a SHIPPED type refuses code that compiled yesterday — so the radius was measured first:
  nothing in `samples`, `Components`, `Charts` or `Templates` names it, and the one public surface
  that does (`With<T>(…) where T : FlexNode`) still compiles, because a constraint is not a call
  site. THE MEASUREMENT WAS INCOMPLETE ANYWAY, and the gap is the finding. Applying the attribute
  turned 29 `Add` calls in the shared component library red — sixteen tests across three suites —
  because #226 made the fence receiver-aware at the member-ACCESS site and nowhere else. A property
  inherited into a client-visible node was fixed; a METHOD was not, and nothing caught it because
  `SingleChildNode`'s members are all properties. The invocation path asks what the call went
  through now. Its guard is a whole compilation rather than a statement probe, and that is measured
  too: the statement probe reports nothing for `row.Add(child)` with the fix reverted, so a test
  written there would have passed either way — the first one I wrote did.

- **2026-09-18 · One statement of layout transparency, and the contract that made four lists
  necessary**: three readers in the layout engine kept hand-written lists of which wrappers to look
  through, and they differed in eight of the twenty
  ([#225](https://github.com/eQuantic/equantic-ui/issues/225)). The issue framed the eight as
  omissions nobody had decided and asked whether fixing them moved pixels. MEASURED, the answer was
  sharper in both directions. ELEVEN of the twenty overflowed a fixed row outright — `Pressable`,
  `Link`, `Hoverable`, most of the tappable text in a real screen — by 128dp where the text was one
  unbreakable word and the bare one ellipsized. And the nine that did NOT overflow agreed only in
  WIDTH: NINETEEN of the twenty wrapped to as many lines as they liked where the bare text was cut
  to one, every wrapper but `Overlay`. The single guard on this could not see it, because it
  asserted the one number on which the two agreed. Both come from
  the FOURTH reader: the truncation contract found its subjects with `children[i] is Text`, so a
  wrapped text was not a text, and it re-measured by HAND — which could only ever cut a bare `Text`
  and dropped a rich paragraph's runs by rebuilding the node from `PlainContent`. The cut is a
  re-measure of the ITEM now, through the same pass everything else takes, carrying the line cap on
  the constraints; `LayoutTransparency` states once which wrappers carry geometry of their own, and
  the default is transparent so a twenty-first wrapper is covered on the day it is declared. THREE
  READERS, NOT FOUR, and that is measured rather than tidy: `Shrinkable` looked like the fourth, but
  across 168 rows an arm there changed exactly one family of cases and changed it for the worse — a
  `Flexible`'s exclusion is a contract with the DIRECT parent, so inheriting it through a wrapper
  inherits a promise nobody made, and the wrapped Flexible then kept 100/150/220 where the bare one
  yields 0/22/92. TWO OF THE NEW TESTS WERE TAUTOLOGIES on the first draft and mutation-checking
  caught both: a fixed Box measured at a narrower bound comes back at its fixed width either way, so
  the probe had to become the shape the shared-buttons golden builds — which is the golden that
  caught it. REVIEW FOUND ONE MORE, and it was real: the ellipsis took its face from the style in
  hand at the wrap decision — the word that FAILED to fit, which is the first word of the next run
  as often as not — and carried neither the ink nor the link of the run it was ending, so a
  truncated link's mark was not pressable where a target hit-tests per fragment. The remedy needed
  one step more than the review said: the last fragment on a cut line is frequently the SPACE that
  follows the last word, and that space already belongs to the next run, so the mark takes the last
  VISIBLE fragment's. A SECOND ROUND found the edge the first left: with one word on the cut line
  and nothing to drop, the mark's width was added to a line already at the limit, so the node
  reported more room than its parent gave it — 75.48 into a box of 40 — where the plain path has
  always ended a cut line with `Min(lineWidth + ellipsis, maxWidth)`. The two paths are pinned
  against EACH OTHER now rather than against a number. The review's stated mechanism was wrong (the
  measured width grows WITH the mark, so the fragment is inside it) and its consequence was right
  anyway, which is the case for reading a finding past its first sentence. It also caught a stale
  count my own mutation harness had reverted: a restore from a backup taken before the fix put
  "nine of the twenty" back, and I did not re-read the file after the last restore. A THIRD ROUND
  said the runs path read `maxW <= 0` as unbounded, and measuring it found the finding was narrower
  than the fault: the runs path clamped NO line to its limit, where the plain measurer has always
  committed each one at `Min(candidate, maxWidth)` — 170dp reported into a box of 0, and 54.4 into a
  box of 10, which has nothing to do with zero. Every line is clamped now, and only infinity counts
  as unbounded. THAT TEST THEN FOUND A DEFECT IT DOES NOT FIX: every inter-word space in a rich
  paragraph measures ZERO, because `MeasureRuns` asks the measurer for each piece and the measurer
  splits on `' '` with `RemoveEmptyEntries`, so a lone space is an empty word list. Identical for one
  word, exactly 5.1dp short per gap after that — a paragraph with emphasis claims less room than the
  same sentence without. It is a different mechanism, in the measurer rather than the clamp, and
  moving it moves every rich paragraph's geometry; pinned rather than widened into this change. A
  FOURTH ROUND then found the half of the clamp that clamping alone could not reach: with the room
  too narrow for even one word, the reported width was right and the MARK was placed past it —
  present in the fragments and invisible behind the realizer's clip, on a line the measurement
  already called ellipsized. Dropping stops at one word, so the word is what gives now: its tail is
  cut to leave exactly the mark's width, asked of the measurer one prefix at a time because an
  advance is the measurer's business. `alphabet` in a box of 40 lays out as `alp…` at 31.28. That
  cost the exact-parity assertion of the round before, and dropping it was a decision rather than a
  concession: the rich path reports the content it laid out where the plain measurer reports the
  clamp, and reporting LESS than the room offered is what every hugging node does. A FIFTH ROUND
  took the prefix search from a walk to a binary one — a long unbreakable word cost one measurement
  per character, and against a real shaper those are not arithmetic — and caught a doc of mine
  contradicting my own code two paragraphs above it, which is the defect this whole PR keeps
  finding. The search's correctness does not rest on monotonicity, only its optimality: every
  candidate it returns is one it measured and saw fit. FIVE ROUNDS, SEVEN FINDINGS, all real, and
  the last two were about the fix rather than the subject — which is what happens when a change
  reaches into a stack next to its own. The 91 goldens did not move.

- **2026-09-18 · A cycle fails the send rather than the process**: the email realizer takes the
  component-chain bound too, through a scope that shares one counter with the containing realizers
  and THROWS where they contain ([#223](https://github.com/eQuantic/equantic-ui/issues/223)). #221
  left it out deliberately, because email expands through `Build` rather than `BuildContained` — a
  broken component must fail the send, never reach an inbox dressed as a describe-box. THE BOUND WAS
  READ AS PART OF THAT DIVERGENCE AND IS NOT: reproduced first, a component that builds itself died
  on `Test host process crashed : Stack overflow`, uncatchable, so the sending process went with it
  and reported nothing — the one outcome worse than a failed send. A sweep for other hand-rolled
  expansions found `eqicon`'s, which turned out to be bounded already by the layout pass it hands
  its tree to, and the email RENDERER's own root, which was not. That root was the finding: left
  outside on the reasoning that the visitors bound everything after it (true, and not the point — a
  `Build` outside the seam is a second place the rule lives), it surfaced as a test that could not
  tell whether the counter had been restored, because the component it rendered afterwards was
  expanded there without ever asking. The A/B is not an assertion and cannot be: with the bound
  removed the tests do not fail, they abort the host, which is the shape #221 measured. REVIEW WAS
  RIGHT ABOUT THE SCOPE and about what the tests missed: a public value type whose `Dispose`
  DECREMENTED is not idempotent, and a copy of it disposed beside the original would put the counter
  below the walk's real depth — far enough below, the bound stops being reached at all, which is the
  overflow arriving through the thing that replaced it. It restores the depth it found instead, which
  is idempotent by construction; `CapabilityScope` reaches the same property with a flag on a
  reference type, because what IT puts back is a resolver. And the root seam had no test: with its
  scope removed every cycle case stayed green, because a cycle exceeds any bound whether or not one
  level was counted. Asked with a FINITE chain now, at both edges.

- **2026-09-22 · The first week of the board, reviewed**: 35 pull requests merged since the ledger
  landed, two releases ([.55](https://github.com/eQuantic/equantic-ui/releases/tag/v0.2.0-preview.55),
  [.56](https://github.com/eQuantic/equantic-ui/releases/tag/v0.2.0-preview.56)), the dispatch epic's
  two features closed with the compiler holding what a regex held ([#161](https://github.com/eQuantic/equantic-ui/issues/161),
  [#162](https://github.com/eQuantic/equantic-ui/issues/162)), the public surface declared per assembly
  ([#280](https://github.com/eQuantic/equantic-ui/pull/280), which is [#173](https://github.com/eQuantic/equantic-ui/issues/173)
  by the mechanism .NET has), and three rules written into `CLAUDE.md` by the sessions that broke them
  (one type per file, no member survives to keep an old shape alive, the PR is not optional). Reviewed
  against the briefs: no slice departed from the plan without measuring first, and three departures
  corrected the plan. What the review found owed: four diagnostics on the docs page and none on the
  wiki at review time — two of them, and the components-over-your-own-base section, landed the same
  day in wiki commit 17b7c3d; `EQ2011` and `EQ2012` remain
  ([#283](https://github.com/eQuantic/equantic-ui/issues/283)) — two findings the slices called
  "worth an issue" that had none ([#284](https://github.com/eQuantic/equantic-ui/issues/284),
  [#285](https://github.com/eQuantic/equantic-ui/issues/285)), seven issues on the board with no type
  and no parent (typed, parented; the SSR-to-client seam is [#282](https://github.com/eQuantic/equantic-ui/issues/282)),
  and the roadmap's phases and tracks with no issue at all — now the epic
  [#288](https://github.com/eQuantic/equantic-ui/issues/288) with fourteen features, plus the desktop
  and email remainders under [#198](https://github.com/eQuantic/equantic-ui/issues/198).

- **2026-09-22 · An icon's elements keep their own origin**: the pack generator joined every element
  of an Iconify icon into one path, and an element opening with a relative moveto started where the
  previous one ended — Lucide's `circle-check` drew its circle and not its tick, and 941 glyphs across
  six packs shipped displaced, found by the eQuantic.Auth pages ([#320](https://github.com/eQuantic/equantic-ui/issues/320)).
  Each element is joined with its first moveto absolute; the source is pinned to one
  iconify/icon-sets commit so a regeneration takes nothing it did not ask for; the twelve packs are
  regenerated, eight of them into the trimmable property form the other four already had (#74); and
  `IconPackGeometryTests` holds every glyph of every marker-found pack to the seam rule and to its box.
- **2026-09-22 · The runtime the Server serves is a build output, and only that**: the committed
  `src/eQuantic.UI.Server/wwwroot/runtime.js` is gone, ignored, and written by `BundleRuntime` from
  `boot.ts` before every Server build, which it always was; the copy in git was the one that lagged the
  runtime's source twice ([#273](https://github.com/eQuantic/equantic-ui/issues/273)). The VS Code
  extension bundles it through the same target instead of copying a leftover.
- **2026-09-22 · One identity and one mount at the SSR seam**: a component's hydration key is its CLR
  full name on both sides — `ComponentIdentity.Of` on the server, `static $typeId` written by eqc on
  the twin — so two `Row`s from different namespaces are two types to the check that makes a drift safe,
  and the key no longer depends on the bundler leaving class names alone
  ([#278](https://github.com/eQuantic/equantic-ui/issues/278)). An escape-hatch page, served by SSR and
  never mounted, has its client half: `EscapeHatchPage` hosts it through the stateless page's own
  machinery, as one walk whose bridges continue the page's count
  ([#279](https://github.com/eQuantic/equantic-ui/issues/279), [#282](https://github.com/eQuantic/equantic-ui/issues/282)).
- **2026-09-22 · What ships serves the app**: the compile-time evaluator, its strategy and attribute,
  `CssEmitter` and `StyleClass` leave the tree ([#214](https://github.com/eQuantic/equantic-ui/issues/214)) —
  measured first, `StyleUsages` was read and never written, so the emitter never produced a rule and a
  `StyleClass` on an escape-hatch element named a class no stylesheet defined; the framework has one
  styling engine, and now nothing says otherwise. `PaletteAudit` moves from Primitives into the tests
  that are its only reader, out of every bundle and AOT image ([#128](https://github.com/eQuantic/equantic-ui/issues/128)).
- **2026-09-23 · A vocabulary type's conversion crosses the seam**: `Icon(Icons.Search)` lowered the
  enum to its bare name, so the generated factory's `glyph: IconGlyph` described its argument wrongly
  and only a constructor that guessed at strings kept it running
  ([#281](https://github.com/eQuantic/equantic-ui/issues/281)). A vocabulary conversion now crosses as
  a call to its twin's static (`IconGlyph.fromIcons`); `[ConversionPassesThrough]` keeps `SizeValue`
  from a number as the number it is, and `VocabularyConversionTests` derives the whole set and fails
  on either half left implicit.
- **2026-09-23 · A generated file its generator stopped emitting goes with it**: eqc reads generated
  sources as files, and Roslyn deletes none, so a deleted component's factory surface stayed in
  obj/.../generated and was transpiled on the next build
  ([#253](https://github.com/eQuantic/equantic-ui/issues/253)). Measured, a compile that runs
  rewrites every file it generates; the SDK now removes, after a compile that ran, each generated
  file older than its start that the compile did not take as input, and nothing after one that was
  skipped. The vector catalog in the same folder had never skipped at all: its target's Inputs was
  a wildcard in a plain string, which MSBuild does not expand, so eqicon started on every build. It
  now skips on an unchanged set of SVGs, notices a deleted one through a record of the set, and a
  skipped catalog target still adds the catalog to @(Compile), which keeps it out of the prune. The
  guard that eqc takes its file list from the one function was already #264's.
- **2026-09-23 · The surface an app writes outside C# is pinned**: the API analyzer holds every C#
  signature, and nothing held the SDKs' MSBuild properties, the `Photon` configuration section or the
  template parameters, where a rename is a setting silently ignored
  ([#322](https://github.com/eQuantic/equantic-ui/issues/322)). `DeveloperSurfaceContractTests` reads
  all three from the source against a committed baseline, and the release reads its diff for the
  notes beside the `*REMOVED*` lines.
- **2026-09-23 · A published map carries no C#**: every module's map held its `.cs` whole and by its
  absolute path, and the output folder is a web root, so a Release publish of the dashboard sample
  shipped 13 maps, two of them with `[ServerAction]` bodies inside
  ([#352](https://github.com/eQuantic/equantic-ui/issues/352), reported by the session packaging
  eQuantic.Auth.WebPages). `EQuanticSourceMaps` is `full` in Debug and `none` everywhere else, a
  map's sources are named inside the project, eqc's maps stay out of the static web assets (a Debug
  build's maps, deleted by a Release build after being registered, failed the publish), and a
  publish takes no map whatever the setting says. CI publishes the sample and looks for a sentence
  only its server has.
- **2026-09-23 · The code editor's engine gets an assembly of its own**: Track I became the editor
  `../equantic-code` is built on, planned in [`CODE-EDITOR-PLAN.md`](CODE-EDITOR-PLAN.md) after
  driving the current one in Chromium and through `PhotonHost` found eighteen defects. Its first
  slice ([#359](https://github.com/eQuantic/equantic-ui/pull/359)) moves the engine out of
  `Primitives` into `eQuantic.UI.Code` (the audit's section 4, decided with Edgar) and puts one
  protocol, `ICodeSurfaceModel`, between it and both hosts: the grid and the pointer semantics are
  the engine's, which gave the web drag selection, shift-click, and the double and triple click it
  never had. Two transpiler gaps it exposed were fixed where they live (a plain class's unassigned
  field, and `bool | bool` answering a number), and the review found more: a struct began `null`
  where C# holds its zero (`new CodeGrid()` threw at its first read; `[ZeroConstructs]` names the
  hand-written twins that build one), a record's module never imported the app types its body
  named, a type pattern over the transpiled namespaces answered `!= null`, a bool compound on a
  dictionary entry or a member stored a number or evaluated its target twice, and a comparer handed
  to a collection's constructor vanished (`CodeLanguages.For("CSharp")` was plain text on the web;
  EQ2007 refuses one now). Driving it by hand then found the caret missing beside every bracket
  (defect 18, on Photon on every line) and the pointer an arrow over the code, both fixed here.
- **2026-09-23 · The runtime ships once, inside the package that serves it**: three things wrote a
  file called the runtime, a library build by vite in CI and two bundles of `boot.ts` by bun, and the
  copy every app received was the vite one, which exports no `boot`
  ([#335](https://github.com/eQuantic/equantic-ui/issues/335)). Measured on a template app from the
  published .57: `wwwroot/_equantic/runtime.js` was 761,837 bytes while `/_equantic/runtime.js`
  answered 538,810, the Server's embedded bundle, because the endpoint wins over the static files. The
  Server's `BundleRuntime` is now the one writer, the Server package ships its bytes as a file, the
  SDK copies that file, and three CI checks compare the bytes instead of looking for a file. The
  `equantic.css` every app received, a base sheet no page had ever linked, is gone, and the bun
  packages are private to the build, so a library packed on the SDK no longer depends on them.
- **2026-09-23 · The runtime and the frame have budgets that fail**: the web side measured no size
  at all ([#290](https://github.com/eQuantic/equantic-ui/issues/290)). The runtime the Server
  serves, which every page loads first and which carries the shared component library, is recorded
  at 137,801 bytes gzipped, and the suite fails when it grows more than 1% or shrinks more than 5%
  without the record moving with it. The dashboard sample's page modules are reported on every pull
  request. On Photon, the eight-layer scene's 78.1 KB/frame sat over the dense scene's ceiling with
  nothing deciding it: the ruler is now what one open layer adds, 506 bytes measured between 24 and
  32 layers once each layer's root path stopped being rebuilt every frame, under a ceiling one
  object tighter, and the harness runs alone. The definition's code-splitting per route is met by the module graph, and bun already
  splits what pages share into chunks.
- **2026-09-23 · Source maps compose without an npm package**: eqc composed each module's map
  (JavaScript to TypeScript to C#) with a script over `@ampproject/remapping`, installed into the
  SDK's own folder in the package cache on a consumer's first Debug build, and fetched by bun's
  auto-install where that failed ([#356](https://github.com/eQuantic/equantic-ui/issues/356)). The
  composition is C# now, with the script's rules: the dashboard sample's thirteen maps compose to
  the same segments, sources and contents either way. It keeps bun's `debugId`, which the script
  dropped although an `external` map exists to be matched by it. CI fails if a build leaves a
  package manager's files beside eqc, and the template gate compares the SDK's package folder
  before and after a Debug build.
- **2026-09-23 · The code editor takes input the platform's way**: slice 1a of
  [`CODE-EDITOR-PLAN.md`](CODE-EDITOR-PLAN.md) ([#368](https://github.com/eQuantic/equantic-ui/pull/368)).
  The web surface read characters off `keydown`, so a dead key, an input method, AltGr on a
  European layout, a phone's keyboard and dictation never reached the document, and ⌘V was
  cancelled before the browser could deliver a paste. Text now arrives through a textarea held at
  the caret, as the platform's own input, composition and clipboard events, and an input method's
  text lives in the document underlined until it commits as one edit. The keymap learned the
  keyboard's two traditions (Ctrl+← went to the line's start on Windows and Linux), Escape releases
  Tab, Photon brings a moved caret into view and shows the composition it used to track unseen, and
  the selection is drawn by the component under the text. The server's arm for the surface was built
  and withdrawn: the server measures text as 0 and hydration keeps its markup, so the adopted editor
  kept a 12px gutter (the plan's SSR slice, which the standalone `CodeBlock` needs too). Found on the
  way: a nullable field with no initializer began 0, false or unassigned in its twin, and the
  editor's accessible name was English in every language. The served runtime grew from 137,801 to
  140,267 bytes gzipped, the price of the input path.
- **2026-09-23 · What the server could not measure, the client draws**: the code editor's SSR
  slice ([#370](https://github.com/eQuantic/equantic-ui/pull/370)), which closes the last
  node the server wrote nothing for. The server has no font, so a component whose geometry is text
  geometry was built on zeros there, and hydration keeps the server's markup: every code block the
  server sent kept a 12px gutter for as long as the page lived (the fenced code on `/markdown`), and
  a server that wrote the editor's surface had the same zeros adopted. `WebRealizer` now hands
  components a measurer with no fonts, which answers 0 and counts the questions, the component whose
  own `Build` asked is marked `data-eq-unmeasured`, and hydration draws a marked subtree instead of
  adopting it. The server writes `CodeSurface` (the code, its carets and its input), `/code`
  hydrates whole where one missing child used to send it to a full re-render, and the block's gutter
  is 26px where it was 12.
- **2026-09-23 · The code editor counts what is drawn**: slice 1b of
  [`CODE-EDITOR-PLAN.md`](CODE-EDITOR-PLAN.md) ([#371](https://github.com/eQuantic/equantic-ui/pull/371)).
  The engine counted one column per UTF-16 unit and placed every caret one cell per column, while
  the browser drew a tab to the next eight-column stop and a wide character across two cells: the
  caret stood beside the wrong glyph, and a Backspace on an emoji left half of its surrogate pair.
  One map from a column to its cell (`CodeLineCells`) now serves the caret, the selection, the
  click, the arrows, Backspace and the drawing, which draws a tab as spaces to its stop and a wide
  character in a box two cells wide. Measured in Chromium, the glyph after an ideograph starts at
  the pixel the caret before it stands on. Text elements come from the platform on both sides
  (`StringInfo`, transpiled to `Intl.Segmenter`). The model's remaining defects went with it: Tab,
  Shift+Tab and ⌘/ keep the selection they edit, a closing brace steps back to its block, typing
  over a selection is one undo and a paste is its own, and C# raw strings are one string across
  lines. Found on the way in eqc: the `(string, index)` overloads of the char classifiers tested
  the whole string, and a code point read from a string reached tsc as `number | undefined`. Found
  in review: a char method spliced its argument as a receiver, so over a conditional it read one
  branch, and the bracket match walked to its pair with the caret's step, segmenting every line on
  the way (110 ms a frame on 3000 lines, under one now that it scans). A second review of the model
  found more: ⌘/ over three lines re-coloured only the first, so a line it emptied kept a comment
  longer than itself and Photon's boundary replaced the editor with its failure panel; a `$` after an
  operator was drawn twice, and `@$"""` coloured the rest of the file as a string; ✌🏻 took one cell
  and drew two; a click kept the cell a run of ↓ had aimed at; Tab counted columns, not cells. The
  width table was written by hand, so it is now compared, character by character and on both sides,
  with the SDK's own Bun (`Bun.stringWidth`), which found the web twin reading every astral
  character as one cell: eqc translated `char.IsSurrogatePair(char, char)` as the (string, index)
  overload. A third review found a mark that begins an element taking cells (the voiced sound mark,
  two) and a word step stopping between a letter and its accent: a nonspacing or enclosing mark
  takes none now, read from `CharUnicodeInfo.GetUnicodeCategory`, which eqc translates for the
  first time, and words step over whole elements. The served runtime grew to 144,405 bytes gzipped.
- **2026-09-23 · A publish sees the files this build wrote**: editing a component two pages share
  and publishing failed on the first run ([#361](https://github.com/eQuantic/equantic-ui/issues/361)).
  bun names a shared chunk by its content's hash, and the static web assets pipeline registered
  wwwroot's files while the project evaluated, before eqc rewrote the folder, so the renamed
  chunk's old name reached the publish's compression with no file behind it. The same order had the
  build's compression packing the previous build's modules. eqc's folder leaves Content and is
  defined as web assets from what is on disk once its writers have run, through the pipeline's own
  hook for generated assets, and CI edits a shared component and publishes.
- **2026-09-23 · A float is a single where it is produced**: eqc rounded a `float` only at a store,
  arguing from what ECMA-335 permits, and RyuJIT rounds every operation — so a float-returning method
  handed its caller a double and a bar's hit bound differed by one ULP between server and browser
  ([#146](https://github.com/eQuantic/equantic-ui/issues/146)). Every float operation, increment,
  wide-int conversion, constant and hydrated value now rounds where it is born; the numeric table
  answers in single precision for the `float` home and serves `Math`/`MathF` from the same entries,
  a call no model bound included; and `Math.Round` detects a midpoint exactly and honours every
  `MidpointRounding`. A value the browser produces (a scroll offset, a drag's travel, a pointer's
  position) enters C# through the runtime, which now rounds it at each of the five seams C# types
  `float`; `FloatSeamsTests` derives them by reflection and requires a spec for each.

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

## The board, as of 2026-09-22

Every issue on [project #11](https://github.com/orgs/eQuantic/projects/11), by parent. The board
is the live view; this snapshot is regenerated when the hierarchy changes shape. Closed items are
struck through. Eight epics now: the audit's four, the parity gaps, the tracks in flight, the audit
that continues, and — since 2026-09-22 — the roadmap ahead.

- [#157](https://github.com/eQuantic/equantic-ui/issues/157) One vocabulary, one door per node *(Epic)*
  - ~~[#161](https://github.com/eQuantic/equantic-ui/issues/161) A visitor per dispatch over the vocabulary~~ *(Feature)*
    - ~~[#177](https://github.com/eQuantic/equantic-ui/issues/177) S2 · Semantics.Walk becomes SemanticsVisitor~~ *(User Story)*
    - ~~[#178](https://github.com/eQuantic/equantic-ui/issues/178) S3 · EmailRealizer and WalkText become two visitors with one refusal set~~ *(User Story)*
    - ~~[#179](https://github.com/eQuantic/equantic-ui/issues/179) S7 · NodeKind generated for TypeScript, assertNever in the browser's switch~~ *(User Story)*
    - ~~[#180](https://github.com/eQuantic/equantic-ui/issues/180) S4 · WebRealizer.LowerNodeKind becomes WebLoweringVisitor~~ *(User Story)*
    - ~~[#181](https://github.com/eQuantic/equantic-ui/issues/181) S5 · LayoutEngine.MeasureCore becomes MeasureVisitor with MeasureState~~ *(User Story)*
    - ~~[#182](https://github.com/eQuantic/equantic-ui/issues/182) S6 · PhotonRealizer.EmitNode becomes EmitVisitor~~ *(User Story)*
    - ~~[#183](https://github.com/eQuantic/equantic-ui/issues/183) S8 · VocabularyCoverageTests is deleted~~ *(User Story)*
  - ~~[#162](https://github.com/eQuantic/equantic-ui/issues/162) Node shapes: SingleChildNode, the intrinsic questions, VisualNode.cs split~~ *(Feature)*
  - [#132](https://github.com/eQuantic/equantic-ui/issues/132) One transpiled set in the runtime, not two *(Feature)*
    - ~~[#184](https://github.com/eQuantic/equantic-ui/issues/184) Fold ButtonStyles into Button over Sizing; no generator entry, no runtime export~~ *(User Story)*
  - [#163](https://github.com/eQuantic/equantic-ui/issues/163) The vocabulary's TypeScript twins are emitted by eqc *(Feature)*
  - [#164](https://github.com/eQuantic/equantic-ui/issues/164) The transpiler's fences hold on every path *(Feature)*
    - [#146](https://github.com/eQuantic/equantic-ui/issues/146) 🐛 fix: a float that leaves a method is not rounded, so the twin keeps a double *(Bug)*
    - [#150](https://github.com/eQuantic/equantic-ui/issues/150) The no-model fallback has no route for [RuntimeProvided] types outside Primitives *(Task)*
    - [#253](https://github.com/eQuantic/equantic-ui/issues/253) ✅ test: eqc's own module list is unguarded, and a generator's stale output survives its configuration *(Bug)*
    - [#281](https://github.com/eQuantic/equantic-ui/issues/281) ♻️ refactor: a vocabulary type's implicit conversion should cross the seam *(Task)*
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
    - ~~[#187](https://github.com/eQuantic/equantic-ui/issues/187) A container semantic role unmutes Navigable and Overlay on Photon~~ *(User Story)*
  - [#167](https://github.com/eQuantic/equantic-ui/issues/167) The adapter's shadow leaves the tree *(Feature)*
    - [#214](https://github.com/eQuantic/equantic-ui/issues/214) Delete the compile-time evaluator and the documents that describe it, and the two plans that cite a roadmap that is not in docs/ *(User Story)*
- [#159](https://github.com/eQuantic/equantic-ui/issues/159) Hosts and realizers hold what Flutter splits *(Epic)*
  - [#168](https://github.com/eQuantic/equantic-ui/issues/168) PhotonHost split into its bindings *(Feature)*
  - [#169](https://github.com/eQuantic/equantic-ui/issues/169) The Element decision *(Feature)*
  - [#170](https://github.com/eQuantic/equantic-ui/issues/170) The truncation mark on DirectWrite *(Feature)*
  - [#171](https://github.com/eQuantic/equantic-ui/issues/171) The server writes CodeSurface *(Feature)*
  - [#172](https://github.com/eQuantic/equantic-ui/issues/172) A write-once page reaches data on both targets *(Feature)*
  - [#282](https://github.com/eQuantic/equantic-ui/issues/282) The SSR-to-client seam: one identity, one payload, one mount *(Feature)*
    - [#278](https://github.com/eQuantic/equantic-ui/issues/278) 🐛 fix: the hydration key names a component by its SIMPLE name, so two `Row`s share a net *(Bug)*
    - [#279](https://github.com/eQuantic/equantic-ui/issues/279) 🐛 fix: an escape-hatch page is served but never mounted — it has no client half *(Bug)*
- [#160](https://github.com/eQuantic/equantic-ui/issues/160) Instruments that fail, not warn *(Epic)*
  - ~~[#173](https://github.com/eQuantic/equantic-ui/issues/173) A public-surface baseline per shipped assembly~~ *(Feature)*
  - [#174](https://github.com/eQuantic/equantic-ui/issues/174) Culture fixtures assert the mapping, never the host *(Feature)*
    - [#147](https://github.com/eQuantic/equantic-ui/issues/147) 🐛 fix: two pins compare the host's ICU, not this repository's code *(Task)*
    - ~~[#188](https://github.com/eQuantic/equantic-ui/issues/188) Delete the unused CultureDataFactAttribute~~ *(Task)*
  - [#175](https://github.com/eQuantic/equantic-ui/issues/175) Sizing generated from the handoff's tokens.json *(Feature)*
  - ~~[#176](https://github.com/eQuantic/equantic-ui/issues/176) Release notes are written from the public-surface diff~~ *(Feature)*
  - [#217](https://github.com/eQuantic/equantic-ui/issues/217) The wiki's Diagnostics page is held to docs/DIAGNOSTICS.md by a test *(Task)*
  - ~~[#220](https://github.com/eQuantic/equantic-ui/issues/220) One type per file, with the exceptions named rather than assumed~~ *(-)*
  - [#273](https://github.com/eQuantic/equantic-ui/issues/273) ✅ test: nothing compares the Server's committed bundle against a fresh one, so it drifts silently *(Bug)*
  - [#277](https://github.com/eQuantic/equantic-ui/issues/277) ✅ test: boot.ts has no navigation harness, so the SPA path's state and metadata are unpinned *(Task)*
  - [#283](https://github.com/eQuantic/equantic-ui/issues/283) Wiki backfill: EQ2010, EQ2011, EQ2012 and EQ2111 rows, and the component-over-your-own-base section (EN + pt-BR) *(Task)*
  - [#285](https://github.com/eQuantic/equantic-ui/issues/285) The stand-in text measurer gives every inter-word space zero width in a rich paragraph, so its goldens are narrower than any shell draws *(Bug)*
  - [#286](https://github.com/eQuantic/equantic-ui/issues/286) Decide the headless-browser instrument: geometry and hit-testing measured in a real browser, not inferred from markup *(User Story)*
  - [#287](https://github.com/eQuantic/equantic-ui/issues/287) Require the CI status checks in the ruleset on main *(Task)*
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
    - [#284](https://github.com/eQuantic/equantic-ui/issues/284) Decide the Photon frame allocation budget: an eight-layer overlay frame allocates 78.1 KB against the 74 KB ceiling *(User Story)*
  - [#206](https://github.com/eQuantic/equantic-ui/issues/206) i18n: the fences of v1 to revisit — RTL and script coverage, three-form plurals, per-page catalogs *(Feature)*
  - [#207](https://github.com/eQuantic/equantic-ui/issues/207) Handoff fidelity: verify and fix the visible deviations *(Feature)*
  - [#215](https://github.com/eQuantic/equantic-ui/issues/215) Visual editor: the click-to-select tiers (Literal, Derived, Foreign) *(Feature)*
  - [#307](https://github.com/eQuantic/equantic-ui/issues/307) Desktop (Track W): the workstreams the plan still owes *(Feature)*
    - [#308](https://github.com/eQuantic/equantic-ui/issues/308) W4 · The macOS desktop surface: menus, tray, notifications, and every seam behind a capability *(User Story)*
    - [#309](https://github.com/eQuantic/equantic-ui/issues/309) W5 · The one proof packaging still owes: a notarized, stapled bundle from dotnet publish in CI, installed and relaunched by the updater *(User Story)*
    - [#310](https://github.com/eQuantic/equantic-ui/issues/310) W6 · Windows: UI Automation over the one semantics tree *(User Story)*
    - [#311](https://github.com/eQuantic/equantic-ui/issues/311) W6 · Linux shell — after M5, by decision *(User Story)*
    - [#312](https://github.com/eQuantic/equantic-ui/issues/312) W7 · The developer loop on simulators and emulators: one command per target, profiles the IDEs run, redeploy-on-save *(User Story)*
  - [#313](https://github.com/eQuantic/equantic-ui/issues/313) Email (Track M): the real-client matrix and HTML pins *(Feature)*
- [#208](https://github.com/eQuantic/equantic-ui/issues/208) The audit continues *(Epic)*
  - [#209](https://github.com/eQuantic/equantic-ui/issues/209) Measure the compiler's internals beyond file sizes *(Task)*
  - [#210](https://github.com/eQuantic/equantic-ui/issues/210) Measure the shells' own platform code *(Task)*
  - [#211](https://github.com/eQuantic/equantic-ui/issues/211) Measure the design host's DesignSession *(Task)*
  - [#212](https://github.com/eQuantic/equantic-ui/issues/212) Measure the Server's endpoint surface *(Task)*
  - [#213](https://github.com/eQuantic/equantic-ui/issues/213) Measure the TypeScript runtime's core/ and dom/ beyond the twins *(Task)*
- [#288](https://github.com/eQuantic/equantic-ui/issues/288) The roadmap ahead: phases and tracks not yet on the board *(Epic)*
  - [#289](https://github.com/eQuantic/equantic-ui/issues/289) Phase 7 · Global state: signals and context beyond component-local SetState *(Feature)*
  - [#290](https://github.com/eQuantic/equantic-ui/issues/290) Performance budgets enforced in CI: the web bundle and the Photon frame *(Feature)*
  - [#291](https://github.com/eQuantic/equantic-ui/issues/291) Real-time server push: [ServerEvent] over SignalR *(Feature)*
  - [#292](https://github.com/eQuantic/equantic-ui/issues/292) A typed programmatic Navigator: routes as types, not strings *(Feature)*
  - [#293](https://github.com/eQuantic/equantic-ui/issues/293) Debugging C# in the browser: statement-level source maps and a stack-trace smoke test *(Feature)*
  - [#294](https://github.com/eQuantic/equantic-ui/issues/294) A component test harness for app authors, and the variant matrix for every shipped component *(Feature)*
  - [#295](https://github.com/eQuantic/equantic-ui/issues/295) Track I · CodeEditor intelligence: completions, signature help and live diagnostics *(Feature)*
    - [#296](https://github.com/eQuantic/equantic-ui/issues/296) I1 · The completion model in Primitives: items, selection, the span a commit replaces *(User Story)*
    - [#297](https://github.com/eQuantic/equantic-ui/issues/297) I2 · Keys and the list in Components, placed at the caret from the editor's own metrics *(User Story)*
    - [#298](https://github.com/eQuantic/equantic-ui/issues/298) I3 · Signature help on `(`: the parameter panel over I1's plumbing and I2's placement *(User Story)*
    - [#299](https://github.com/eQuantic/equantic-ui/issues/299) Live diagnostics: squiggles as you type, without pressing Run *(User Story)*
  - [#300](https://github.com/eQuantic/equantic-ui/issues/300) Track F · F2 Wear OS: a round safe area, a size class below Compact, rotary input, swipe-to-dismiss *(Feature)*
  - [#301](https://github.com/eQuantic/equantic-ui/issues/301) Track F · F3 Android TV: the leanback manifest, an overscan-safe area, the 10-foot type scale, a focus ring readable from three metres *(Feature)*
  - [#302](https://github.com/eQuantic/equantic-ui/issues/302) Image decoding on the Android shell *(Feature)*
  - [#303](https://github.com/eQuantic/equantic-ui/issues/303) ListView with variable item extents: incremental measurement beyond the fixed-extent v1 *(Feature)*
  - [#304](https://github.com/eQuantic/equantic-ui/issues/304) Spreadsheet beyond v1: two-dimensional scroll, a formula engine, per-cell formatting, merges, frozen panes *(Feature)*
  - [#305](https://github.com/eQuantic/equantic-ui/issues/305) Inline marked-text rendering in code surfaces during IME composition *(Feature)*
  - [#306](https://github.com/eQuantic/equantic-ui/issues/306) A formal security hardening review, and CSP guidance the SDK writes for the developer *(Feature)*

## How to add an entry

One line under the date, oldest first: what happened, then the link that holds the evidence — a
pull request, a release tag, an issue, a document section. When a plan's last slice lands, move its
dated lines here, condensed, and retire the plan in the same pull request — unless code or another
document cites it, in which case the citation moves first. The board snapshot is regenerated when
the hierarchy changes shape, not on every issue.
