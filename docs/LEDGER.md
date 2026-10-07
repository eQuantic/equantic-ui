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
- **2026-09-26 · The handoff's own figures and statuses are held by tests**: the design system came
  back from Claude Design with a review against Apple's and Google's guidance
  ([#337](https://github.com/eQuantic/equantic-ui/issues/337)). The pages called four shipped
  things requests (DatePicker, NavigationRail, `VariantColors.Hover`, the `SemanticNode` state
  fields) and counted 44 components where the SDK has 56. `status.json` now derives each block's
  status from the public API, both ways; the SDK's selection, avatar and wheel values are published
  and a token type `Tokens.cs` declares is scanned or named; a figure printed on a page is compared
  with its token. APCA joins WCAG 2 as a second contrast gate, and 14 dark values were re-solved to
  clear it, which moved 19 dark goldens and the web's cross-pins with them. The review's proposals
  are issues #338 to #351. Edgar confirmed its three decisions on 2026-09-26: the pointer exception
  keeps a 24dp floor, whose change is [#430](https://github.com/eQuantic/equantic-ui/issues/430), and
  continuous corners reach the web through `corner-shape`
  ([#346](https://github.com/eQuantic/equantic-ui/issues/346)).
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
  first time, and words step over whole elements. The served runtime grew from 143,104 bytes
  gzipped, main's after #330, to 146,004.
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
- **2026-09-23 · An integer division refuses what .NET refuses**: `a / b` and `a % b` on integers
  answered `Infinity`, `NaN` or 2147483648 where .NET throws, so a count of zero rendered `Infinity`
  in the browser where the server threw; a long's BigInt threw a `RangeError` of its own for zero and
  answered 2^63 for `long.MinValue / -1` ([#333](https://github.com/eQuantic/equantic-ui/issues/333)).
  A divisor that can be zero, or -1 beside `MinValue`, goes through the runtime's check, the compound
  and lifted forms included; a constant divisor other than 0 and -1 keeps the bare operator.
- **2026-09-23 · A number reads and writes as .NET's**: a double and a float wrote their text through
  JavaScript's `String()`, which keeps fixed notation up to 1e21, spells `1e+21` and drops the sign
  of -0, where .NET writes `1E+17` and `-0` ([#336](https://github.com/eQuantic/equantic-ui/issues/336));
  and a decimal read from text or from `Convert` was a JavaScript number, `parseFloat`'s, with none
  of the methods decimal arithmetic calls next ([#358](https://github.com/eQuantic/equantic-ui/issues/358)).
  The runtime now writes .NET's notation from the shortest digits, reads a number's text by .NET's
  own grammar under the `NumberStyles` a call names, rounds it into a decimal as .NET's parser does,
  and converts a double or a float by the steps of .NET's `DecCalc`; the runtime spec is generated
  from what .NET printed, and the conformance suites run every form on both sides. Reading a number
  says which culture it reads in, as formatting already did: `CultureInfo.InvariantCulture`,
  recognised by the property a provider binds to and not by its name, crosses exactly; no provider
  is EQ2110, and any other is EQ2108, since the browser has no parser for another culture's text.
- **2026-09-24 · A thrown error's frame names its C# statement**: C# mapped member by member in the
  browser, so a frame or a breakpoint anywhere in a body landed on its method's first line
  ([#293](https://github.com/eQuantic/equantic-ui/issues/293)). A statement now carries the C# it
  came from, the statement writer marks the line it lands on, and the source map carries a segment
  per statement. Method and constructor bodies reach the writer as IR instead of text, which is what
  dropped the origins, and eqc's bundling moved into the compiler library as `ModuleBundler`, so a
  smoke test bundles, runs and throws through the same pipeline and reads each frame back to its
  C# line. A line a strategy lowers belongs to the statement that produced it: a pattern switch's
  arm maps to its case, a `using`'s dispose to the `using`, and a `do`'s condition to itself.
- **2026-09-24 · A nullable number keeps null and its type's rule**: a compound assignment, an
  increment and a unary `-`, `~` or `+` on an `int?`, a `float?`, a `byte?` or a `decimal?` reached
  JavaScript's own operator, which reads null as 0, so `x += 1` and `x++` on a null `int?` answered 1
  and `-x` answered -0, and a value met none of its type's rules: a `float?` added doubles, a `byte?`
  never wrapped, a `decimal?` called a method on null
  ([#372](https://github.com/eQuantic/equantic-ui/issues/372)). Each now takes the underlying type's
  rule inside the runtime's lift, the same rule a non-nullable target takes, and a nullable division
  is one case of it. Found on the way: a literal the C# compiler types `long` because no `int` or
  `uint` holds it (`637000000000000000`) was emitted as a plain number, which lost its low digits and
  threw at the first arithmetic with another long. And from the review, for every target: a uint's
  `&`, `|`, `^` and `>>` answered JavaScript's signed 32 bits (`uint.MaxValue & uint.MaxValue` was -1),
  a long's shift threw a TypeError for an int count, kept the bits C# discards and did not mask its
  count, and a checked or explicitly unchecked negation neither threw .NET's message nor wrapped. A
  uint's or a ulong's complement answered a negative number. A step on a dictionary entry whose key
  is missing now throws as .NET does, through the guard compound assignments read with, which closes
  three of the conversion gaps. A decimal remainder is exact and stays a decimal: the runtime's
  Decimal had none, so `%` computed in doubles (`0.3m % 0.1m` was `0.09999999999999998`).
- **2026-09-24 · A value is written as .NET writes it, through the formatter too**: a bool's
  `ToString()` was JavaScript's `false` ([#381](https://github.com/eQuantic/equantic-ui/issues/381));
  `string.Format` took a format provider for its template, which threw `CultureInfo is not defined`
  in the browser ([#377](https://github.com/eQuantic/equantic-ui/issues/377)); and a float that
  reached the formatter printed the double underneath, since a number cannot say it is a single
  ([#378](https://github.com/eQuantic/equantic-ui/issues/378)). `ToString()` on a bool takes the
  concatenation's conversion; `string.Format` binds its arguments by the method and follows the
  formatting culture policy; the compiler tells the formatter a float's kind where it knows it, and
  boxes a float passed to `string.Format` with it. `G`, `R` and a placeholder with no specifier write
  .NET's notation, and a placeholder aligns. From the review: a null provider formats with the current
  culture, as .NET reads it, and a params array passed whole spreads by the form C# bound, so a
  `string[]` or a collection expression is formatted element by element, where it was one value.
  Each value is passed in its parameter's slot and evaluated where it was written, a named argument
  included, and with no model to bind the call a named culture is still known for the provider. An
  invariant conversion writes the invariant culture's date patterns and the generic ¤, where it read
  the reader's.

- **2026-09-24 · The code editor is a component**: slice 1c of
  [`CODE-EDITOR-PLAN.md`](CODE-EDITOR-PLAN.md) ([#375](https://github.com/eQuantic/equantic-ui/pull/375)).
  An IDE holds the editor in a pane, and without a height cap there was no viewport, so every
  keystroke built every line: `Height` (Fill or a fixed height) bounds it now, and it builds what is
  in view. On the web a capped box lays its child out as a column, so `MaxHeight` scrolls (the
  scroller grew to 2827px inside 520), and a scroll view anchors nothing, since the browser's scroll
  anchoring moved the offset whenever the line window swapped rows and slid a revealed match back
  out of view. The find bar is a layer over code that keeps its place in the tree (opening it made
  the surface a new one to every host: the scroll went back to the top and Photon's keyboard pointed
  at nothing), its field takes the keyboard when it appears, Enter walks the matches and keeps the
  field, Escape closes it and gives the keyboard back through a request the model carries and both
  hosts honour (`RequestFocus`, `FocusVersion`), and the app hears a move as a move and an edit as an
  edit. A build draws and measures the lines in view only (the marks, the selection's bands, the
  matches of a search, found once per search, and the widest line, once per document), where a
  select-all with a search on built 8001 boxes a frame over 4000 lines and a scroll step over 50,000
  took 81 ms (3 now). An editor that stops being bounded lets its window go, and on the web a
  viewport is measured once the render is written, and again when it resizes with no render at all:
  a Fill editor in a pane that grew kept the rows it had built for the old height. On the way:
  Photon honoured a field's `Autofocus` once per path for the life of a window and never a code
  surface's, Enter left a field on Photon and stayed on the web (it stays on both), a key an app's
  shortcut took still reached the editor on the web, a code block's corner lay over its whole first
  line, and seven tests asserted nothing when their value was null.

- **2026-09-24 · The code engine diffs two texts**: the engine half of slice 2b of
  [`CODE-EDITOR-PLAN.md`](CODE-EDITOR-PLAN.md) ([#386](https://github.com/eQuantic/equantic-ui/pull/386)).
  `CodeDiffer` answers the lines that changed between two texts and, inside each change, the words:
  Myers' shortest edit script over what is left once the common head and tail are trimmed, so the
  cost follows the change and not the file, and the same algorithm over a change's tokens. Past
  2,000 rounds two ranges are a rewrite, marked whole, counted in rounds rather than by a clock so
  .NET and the web stop at the same point. Its counts are `git diff --minimal`'s own on five files of
  this repository's history, and its twin answers change for change on 400 random pairs. On the way:
  eqc named a method by its name alone, so two overloads reached a twin as one method, JavaScript
  kept the last and a component's parser the first; the build now stops at the second declaration
  and names the first (EQ1007). And white space is .NET's on the web: `char.IsWhiteSpace`, the
  `Trim` family and `IsNullOrWhiteSpace` read one list, where JavaScript's left U+0085 and took
  U+FEFF (the twin's word diff split on it), and a bare `Split()` splits on it instead of into
  characters. The view's design, a row that is not a line, is the plan's ninth section.
- **2026-09-24 · The BCL audit probes every arity**: a static surface was probed by name, so only
  the shortest overload of each member was graded, and a group whose first overload takes an
  `IFormatProvider` or an `IComparer` hid its siblings
  ([#390](https://github.com/eQuantic/equantic-ui/pull/390)). Probed by name and arity, from the first
  overload a probe can write, the baseline gained 59 lines, and each `native` or `eq` one is a claim
  that `BclOverloadConformanceTests` runs on both sides: 161 of its 288 cases failed before the fixes.
  `Convert` reads and writes an integer in a base as .NET does (a port of `ParseNumbers`), the
  `TimeSpan` factories count every component and read a double to the tick, `string.Compare`,
  `CompareOrdinal`, `Equals` with a comparison, `Concat` and a ranged `Join` answer as .NET's,
  `char.IsWhiteSpace` reads the White_Space property, LINQ's `Max` and `Min` order by the type they
  answer, `ToDictionary` refuses a key twice, and `DateOnly`/`TimeOnly.ParseExact` are EQ2004 where
  they threw in the browser. The review found more of the kind, each measured on .NET before it was
  fixed: a dictionary keyed by what a plain object cannot hold (a `DateTime`, a class, an enum with
  aliases) is EQ1004 and a record key is held by value, `GroupBy` and `ToLookup` compare a record or
  a date key by value, a named argument fills its own parameter, a double past 2^53 ticks multiplies
  as .NET's does, an ordinal comparison that ignores case reads a surrogate pair as its code point,
  `Max`/`Min` over a type with no `compareTo` here is EQ1004 where .NET's default comparer throws,
  a LINQ operator called as `Enumerable.Count(source)` is EQ1004 where every strategy but `Max` and
  `Min` read the type as its source, and a record holding NaN equals itself, as a double's `Equals`
  holds it, while a tuple's `==` stays its elements'. Found on the way, each a task: the audit
  grades `eq` without asking whether the member exists (9 lines are a TypeError in the browser),
  overloads of one arity are still one probe (267 more lines by signature), a `Dictionary<int, T>`
  loses insertion order, a decimal constant does not cross, a lone surrogate in a string literal
  is written raw, and the date types' `Add*` round a double to the millisecond.
- **0.2.0-preview.58 released** from `4a330275`: the numeric parity family (#330, #373, #374, #379,
  #383, #389, #390, #391, #403, #405), the code engine in an assembly of its own (#359) with its
  input, model and component slices (#368, #371, #375, #370), and a published app that no longer
  ships its C# in its source maps (#357). *([v0.2.0-preview.58](https://github.com/eQuantic/equantic-ui/releases/tag/v0.2.0-preview.58))*
- **The working agreement lives in the repository** (#410): the Workflow section, one text in
  `CLAUDE.md` and `AGENTS.md` held so by `WorkflowSectionTests`; OpenSpec 1.13.2, pinned by a
  lockfile and validated strictly in CI; and a `SessionStart` hook that gives a cloud container the
  owner's identity, no foreign signature and the pinned .NET SDK, asserted by CI's `session-start` job.

- **2026-09-26 · A record member starts as its declaration says**: a record's field initializer went
  nowhere, and the defaults that crossed were copied as literals into every construction site, so a
  decimal, a long, a float or a `new()` came out as a plain number or as null
  ([#385](https://github.com/eQuantic/equantic-ui/issues/385)). The twin's constructor now writes
  every member's default from its declaration, converted like any expression, and a construction
  that skips a member leaves it to the constructor; a default and a base clause read the primary
  constructor's parameters as its own. From the review: a zero built member by member names a struct
  no syntax of the class does, and every emitter now imports it, and a base clause that computed
  from a parameter read `this` before `super()`. Measured and left to their own issues: an
  initializer's side effect when an object initializer sets its member
  ([#413](https://github.com/eQuantic/equantic-ui/issues/413)), and statics read before C# would have
  zeroed them ([#417](https://github.com/eQuantic/equantic-ui/issues/417)).
- **2026-09-26 · A date's fractional Add* lands on the tick**: the runtime's `DateTime`,
  `DateTimeOffset` and `TimeOnly` added a fractional count through milliseconds and rounded, as
  .NET 6 did, where .NET 7 and later land on the tick, so `AddSeconds(0.00001)` moved nothing and
  `AddMilliseconds(0.5)` a whole millisecond
  ([#422](https://github.com/eQuantic/equantic-ui/issues/422)). The twins port .NET 10's
  `DateTime.AddUnits` now, the whole units and the fraction apart and the fraction truncated
  toward zero, `TimeOnly` takes its one product as .NET 9 and later convert a double, and
  `DateTime.AddMicroseconds`, `DateTimeOffset.AddMilliseconds` and `AddMicroseconds`, which the
  audit graded native and no twin had, exist. An out-of-range count or result throws in .NET's
  words where it built an invalid date. 38 of the 44 conformance cases failed before the port.
  Measured and left to its own issue: `Add(TimeSpan)`, the operators, `AddMonths` and `AddYears`
  at the calendar's edge ([#424](https://github.com/eQuantic/equantic-ui/issues/424)).
- **2026-09-26 · The wiki moves with its pull request**: CI cloned the wiki's master for every run, so
  a pull request that added a diagnostic failed its own docs guard until its rows were published,
  and every other pull request failed the same guard once they were: #386's EQ1007 rows went to
  master early on 2026-09-24 and had to be taken back out, and #418 met the same wall with EQ1008
  ([#406](https://github.com/eQuantic/equantic-ui/issues/406)). `scripts/checkout-wiki.sh` now reads
  the wiki at the branch named like the pull request's own when there is one, and master otherwise,
  failing rather than guessing when the branches cannot be listed; a `wiki-checkout` job runs its
  self-test against a fixture wiki, and the Workflow section says the wiki branch merges into master
  with its pull request. Locally every guard now finds the wiki through one locator, which reads
  `EQ_WIKI_DIR` for a worktree of a pull request's wiki branch (checking a branch out in the shared
  clone had made #354's guards fail on #418's rows), fails when the variable names no wiki, and names
  the directory, branch and commit a failing guard read.
- **2026-09-26 · A class keeps its interface's defaults**: eqc wrote no default interface member
  into the twins of the classes that take one, so `PlainTextLanguage`, which relies on
  `ICodeLanguage.Rules`, had no `rules`, and on 0.2.0-preview.58 every plain-text `CodeBlock` threw
  on `indentWidth` in the browser, which kept the documentation site on .57
  ([#414](https://github.com/eQuantic/equantic-ui/issues/414)). Each default a class takes is now
  written into its twin from the interface's source, in the plain-class, record and component
  emitters, with the implementation C# picks and the types it names imported. An app compiles
  against the SDK's assemblies, where its interfaces have no source, so the runtime carries the
  vocabulary's defaults and an app's theme, language or completion provider delegates to them
  ([#415](https://github.com/eQuantic/equantic-ui/issues/415)): measured on the site, its theme went
  from three warnings on every build to none. An interface from any other compiled assembly has
  neither, and the build refuses the class (EQ1008). From the review: two defaults on one name are
  EQ1007, and so is the same pair along the class chain (a derived field on the name of its base's
  default shadowed it for every call through the interface); a default reaching an interface's static
  is EQ1008, and so is a default indexer, which no twin has a form for
  ([#427](https://github.com/eQuantic/equantic-ui/issues/427)); only the runtime's own assemblies'
  interfaces delegate, whatever namespace another assembly declares; an explicit implementation and
  an event take their member's name in the EQ1007 check; a derived class that lists an interface overriding its base's
  default takes the more specific one; a record with only a base list gets its twin, one named
  without arguments extends its base record, and a record's optional parameters and setters are kept.
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-interfaces`).

- **2026-09-26 · List.Remove and a bool from text answer as .NET does**: `list.Remove(item)` assigned
  an index nothing declared, so every call threw `ReferenceError: _idx is not defined` in the
  browser and the documentation site's "you are here" never moved
  ([#400](https://github.com/eQuantic/equantic-ui/issues/400)); `bool.Parse` was a comparison that
  never threw, read a null as the text "null" and kept a trailing NUL, and `bool.TryParse` had no
  translation ([#402](https://github.com/eQuantic/equantic-ui/issues/402)). Both go through the
  runtime now: Remove answers a bool and compares as `EqualityComparer<T>.Default`, and a bool reads
  as `Boolean.TryParse`, with .NET's trimming, its ASCII-only case fold and its exceptions. The
  number reader uses the one white space list, and `Convert.ToBoolean(object)` is left to its family
  ([#401](https://github.com/eQuantic/equantic-ui/issues/401)). From the review: every other
  `Convert.ToBoolean` overload answers by the type C# binds (a false bool was true, and so was `0L`,
  a BigInt here), a tuple in Remove compares element by element as `Contains` compares it, a
  HashSet, a LinkedList, a SortedSet or a dictionary's pair reached through `ICollection<T>` removes
  as it does directly, a pair compared half by half and a nullable tuple and an anonymous type by
  value, a ToBoolean provider is evaluated in the order it is written (another `CultureInfo`
  than the invariant or the current one is EQ2108, having no twin to evaluate), and the BCL
  audit's `(Object)` probes call the object overload instead of the string one beside it.
- **2026-09-26 · A dictionary enumerates as .NET's does**: a `Dictionary<K, V>` with a primitive key
  was a plain object, which listed integer-like keys ascending and handed every key back as a
  string, so `d[3] = 30; d[1] = 10;` enumerated `1,3` where .NET enumerates `3,1`
  ([#435](https://github.com/eQuantic/equantic-ui/issues/435)). Every dictionary is the runtime's
  dictionary class now, which holds its entries by slot as .NET's does (a removed entry's slot is the
  next one reused, the last freed first) and keeps each key in its type, found through a `Map` by
  identity or by `$eq.equals` where the key type's default comparer finds it by value. One strategy
  owns every dictionary, sorted ones included, `ToDictionary` builds the same class and no longer
  refuses a date or a class key, every dictionary field and result revives from the wire with its
  keys in their type, and the DOM escape hatch reads a dictionary or a plain object alike. Three
  paths that threw work: a key the plain path could not hold (a char, a `Guid`, a date), a
  deconstructing `foreach` over a record-keyed or sorted dictionary, and a copy, which was an alias.
  A key whose type does not decide (`object`, an interface, a type parameter, a class) compares by
  the value's own equality, which Copilot's third round found missing: two equal records under
  `object` were two keys. The author's review found that a key added while the pairs are walked was
  visited in turn, so a loop that added as it went never ended: it ends now as .NET's enumerator
  ends it, with an InvalidOperationException. 34 of the 52 conformance cases failed before, and
  the served runtime grew 819 gzip bytes. Measured
  and left to their own issues: integer keys across the JSON wire
  ([#437](https://github.com/eQuantic/equantic-ui/issues/437)), a `HashSet<T>`'s slot reuse
  ([#438](https://github.com/eQuantic/equantic-ui/issues/438)), LINQ over a dictionary's pairs
  ([#439](https://github.com/eQuantic/equantic-ui/issues/439)), `Add` of a key already there
  ([#440](https://github.com/eQuantic/equantic-ui/issues/440)), `string.Join` over bools, enums and
  floats ([#441](https://github.com/eQuantic/equantic-ui/issues/441)), and an enum key, which EqJson
  refuses on the wire ([#442](https://github.com/eQuantic/equantic-ui/issues/442)), and a Guid,
  which keeps the text it was written in ([#459](https://github.com/eQuantic/equantic-ui/issues/459)),
  a nested initializer that replaces a member's collection
  ([#462](https://github.com/eQuantic/equantic-ui/issues/462)), and live `Keys` and `Values` with
  .NET's capacity ([#463](https://github.com/eQuantic/equantic-ui/issues/463)).
- **2026-09-26 · A record's and a struct's members lower as a class's do**: the record and struct
  emitter lowered a method with its own copy of the class emitter's lowering, which handled none of
  an async method, an iterator, or an out or ref parameter
  ([#432](https://github.com/eQuantic/equantic-ui/issues/432)). The first two wrote a module that
  does not parse, and an out or ref value never came back. One lowering now serves every type with
  methods (`MethodLowering`): a record's and a struct's method, operator, conversion and computed
  property go through the class emitter's own, so what the class path learns reaches them too. From
  the review: whether a method is async is asked of its return type's symbol (a method returning
  `TaskItem` was made async by the name), a getter that yields fills its buffer in every emitter, and
  a C# 14 extension member takes the same lowering. The copy's pattern variables and reserved
  parameter names, which this change fixed too, were fixed on main first by #484 and #399 (the
  declarations that still skip `ToJsIdentifier` are
  [#467](https://github.com/eQuantic/equantic-ui/issues/467)). A new conformance class fails 9 of its
  20 cases on main: every async, iterator, out and ref case, and the getter that yields.
- **2026-09-26 · A number prints through its specifier as .NET prints it**: the resx subset admitted
  the `E` specifier and the formatter had no branch for it, so `{0:E2}` passed the build and printed
  `12345` ([#393](https://github.com/eQuantic/equantic-ui/issues/393)). Measured, every specifier
  missed in the same way: .NET writes a double from its exact binary value, a long and a decimal from
  every digit, and rounds a half by the type, and the formatter started from the shortest text with
  one rule. It now formats from the exact decimal expansion (`utils/exact-decimal.ts`), writes `E` as
  .NET does, and rounds an exact half to even for a double and a float and away from zero for a
  decimal and an integer, which the compiler now names where a specifier is written (`FormatKind`,
  `$eq.text.asInteger`). A new conformance class fails all fifteen of its cases on main. Found on the
  way: `decimal`'s constants do not cross ([#444](https://github.com/eQuantic/equantic-ui/issues/444)).
  Found in review ([#445](https://github.com/eQuantic/equantic-ui/pull/445)): `X` wrote a negative int
  as `-1` and now writes it at its type's width, with `B` beside it; a custom picture read its digit
  places alone and is now drawn as .NET draws it (sections, text, percent, exponents); a precision past
  100 digits was cut, and a fraction under `D` printed where .NET throws; es-ES left `1234` ungrouped,
  and sv-SE's minus sign was a hyphen. Left for later: a number's text outside a specifier ignores the
  culture ([#454](https://github.com/eQuantic/equantic-ui/issues/454)), the guesses where a type did not
  travel ([#455](https://github.com/eQuantic/equantic-ui/issues/455)), and EQ2100's subset can widen
  ([#456](https://github.com/eQuantic/equantic-ui/issues/456)).

- **2026-09-26 · The Copilot loop stops on its own**: waiting for Copilot's light review to run dry
  cost about one round per finding (69 findings in 47 rounds on six pull requests, 14 rounds for
  #354). The author now reviews the whole diff before opening a pull request, a finding earns
  another round only when it is a defect, and the loop ends at the first round without one, after
  three rounds in any case. The ruleset no longer reviews on push, so a round is asked for once per
  push of fixes ([#446](https://github.com/eQuantic/equantic-ui/issues/446)).

- **2026-09-26 · A pull request merges against the current main**: the ruleset requires thirteen of
  the CI's jobs, the whole dependency chain since GitHub counts a job skipped for a failed dependency
  as passed, and a head up to date with main. Before it required only a review, so a workflow that
  never ran read mergeable, and two pull requests that each passed alone broke main together (#453)
  ([#287](https://github.com/eQuantic/equantic-ui/issues/287)).
- **2026-09-26 · main's runtime suite is green again**: the interface-defaults fixture pinned
  PhotonTheme's dark code colours from before #354 re-solved the dark palette, and #418 merged after
  #354 without a CI run on a main that had it, so `test-runtime` and the fixture's C# pin failed on
  main. The fixture is regenerated from the palette both sides now carry
  ([#453](https://github.com/eQuantic/equantic-ui/issues/453)).
- **2026-09-26 · A date prints through its specifier as .NET prints it**: a `DateTime`'s own
  `ToString` reached the twin's `toString(pattern)`, which knew custom tokens only, so `d.ToString("D")`
  printed `D`, and a provider crossed as a name no browser defines
  ([#388](https://github.com/eQuantic/equantic-ui/issues/388)). It now goes through the formatter, as a
  number's does, its provider read through `NamedCulture`. Measured on the way, the formatter wrote the
  round-trip and sortable forms from `toISOString()`, UTC, so a page off UTC shifted the hour; it had no
  `R`, `u` or `U`; a custom picture replaced six tokens by text (`d/M/yyyy` printed `d/M/2026`); and with
  no culture in force a date took `Intl`'s en-US presets and the host's names. Each is fixed, the short
  and long date and time strings go the same way, and the cross-pinned fixture now
  carries every date specifier the resx subset admits. From Copilot's first round: a `ToString()` with no
  specifier writes the current culture's `G`, and no time zone moves a value's parts (a spring-forward
  gap turned 02:30 into 03:30). From its second round: a null or empty format is `G` too, and `U`
  reads an hour a fall-back repeats as standard time (01:30 in New York printed 05:30 UTC, where
  .NET prints 06:30). A new conformance class fails all 12 of its cases on main.

- **2026-09-26 · A name the transpiler changes lands on no name its scope holds**: a local function
  was camel-cased onto a local that differed only by case, a module that did not load; a reserved
  word took a trailing underscore that C# can also write; and a component's constructor bound its
  parameters camel-cased while its body read them as written. `LocalFunctionName` owns a local
  function's name, a reserved word takes a `$`, and every declaration and its readers use one
  spelling. 29 of the 32 conformance cases failed on main before it
  ([#465](https://github.com/eQuantic/equantic-ui/issues/465)). #396, #397, #398 and #400 are the
  same question elsewhere, each with an issue of its own.
- **2026-09-26 · An expression variable is declared by its statement, with C#'s scope**: what a
  pattern, an `out var` or a deconstruction declares had four rules. A method declared every `out
  var` at its top, so a closure made in a loop read the last iteration's value (.NET 12, JavaScript
  22) and a recursive local function overwrote its caller's; a getter, a setter, an initializer, a
  record's members and a component's Build declared none; a deconstruction's elements were declared
  by nothing; and plain JavaScript carried `let n: any;`
  ([#466](https://github.com/eQuantic/equantic-ui/issues/466)). One owner,
  `ExpressionVariableScanner`, now answers every statement with the scope Roslyn gives: in front of
  an `if` or an expression statement, or at the top of the switch whose section holds it, inside a
  `foreach` or a `using`, in a `while`'s, a `do`'s or a `for`'s own head for a fresh variable every
  iteration, and in its own arrow for an initializer, each spelled as #399 spells its readers. The
  harness had declared every `out var` itself, which is why none of it showed: 43 of the new class's
  first 47 cases fail on main without that, and neither mode's modules load in the new emission
  test. The author's review found four more before the pull request opened: a section's variable
  assigned by another section, two queries binding one name, a `for` initialized by a
  deconstruction, and a `do`'s condition, each now a case. Found on the way: a static property on a
  plain class was an instance getter (its `field` slot is still an instance one,
  [#483](https://github.com/eQuantic/equantic-ui/issues/483)), plain JavaScript annotated a local
  declared as another type, and a switch's subject is now `$s`, since a local `_s` could land on it
  (a slice of [#397](https://github.com/eQuantic/equantic-ui/issues/397)). Filed: two catch clauses
  do not parse and one ignores its type and filter
  ([#474](https://github.com/eQuantic/equantic-ui/issues/474)), a lock drops its expression
  ([#475](https://github.com/eQuantic/equantic-ui/issues/475)), a for's own variable is one per
  iteration ([#476](https://github.com/eQuantic/equantic-ui/issues/476)), `fs[0]()` reads off
  `this` ([#477](https://github.com/eQuantic/equantic-ui/issues/477)), `new object()` names no class
  ([#478](https://github.com/eQuantic/equantic-ui/issues/478)), a base written with its namespace is
  extended as written ([#479](https://github.com/eQuantic/equantic-ui/issues/479)), and a bare type
  pattern never matches ([#482](https://github.com/eQuantic/equantic-ui/issues/482)). Proposed and
  archived through OpenSpec (`openspec/specs/transpiler-expression-variables`).
- **2026-09-27 · The code diff view**: the view half of slice 2b of
  [`CODE-EDITOR-PLAN.md`](CODE-EDITOR-PLAN.md) ([#420](https://github.com/eQuantic/equantic-ui/issues/420), [#412](https://github.com/eQuantic/equantic-ui/pull/412)).
  `CodeDiff` draws two texts, or one file of a patch, side by side (the sides level at every change)
  or inline (the removed lines between the lines that replaced them), washes a changed line and
  marks its changed words, folds an unchanged run into a row that opens on a press, steps through
  the changes with F7 and the toolbar, and edits the modified side, compared again after every edit.
  Under it the engine maps a view's lines to rows (`CodeRows`), `CodeDiffLayout` lays out each side
  and `CodePatch` reads a unified diff, and `CodeBlock` draws the rows. On the way, eqc's twins of
  this code were the first to cross an array of a union, a tuple return, a local starting null and a
  function-typed parameter, and each crossed wrong (a negative declared default did too, and
  [#409](https://github.com/eQuantic/equantic-ui/pull/409), above, settled it first); an extended
  property pattern read `changes.Count`, which is undefined; a button drawn in a code surface never
  heard its click on the web; a `Shortcut` answered for the whole page, so F7 in one diff stepped
  another, and `FocusScoped` now makes a chord the subtree's own (FLUTTER-PARITY said SAME, and it
  was not); and a Mac's function keys reached the host as the characters they type. The author's
  review found six more, each fixed: a local annotated in the plain JavaScript the design host
  inlines, a zero-context patch's gaps, F7 stuck past a removed end, the focus a fold's press
  dropped on the web, a patch line's bare carriage return, and a tuple's enum named as in C#; the
  find bar's Escape, still page-wide, is [#457](https://github.com/eQuantic/equantic-ui/issues/457).
- **0.2.0-preview.59 released** from `e83e07fa`: the code diff view (#412) over the engine's line and
  word diffs (#386), the number specifiers printed as .NET prints them (#445) beside List.Remove, a
  bool from text (#421), a date's fractional Add* (#426), a record member's starting value (#409),
  a class's default interface members (#418) and an expression variable's scope (#484), names the
  transpiler changes kept off the names a scope holds (#399), and the working agreement in the
  repository (#411, #431, #448). *([v0.2.0-preview.59](https://github.com/eQuantic/equantic-ui/releases/tag/v0.2.0-preview.59))*
- **2026-09-27 · A lambda's block maps statement by statement**: since #382 every statement of a
  member's body mapped to its own line, but a lambda's block reached the writer as text, laid out
  where the lambda was converted, and its statements' marks were dropped with it
  ([#384](https://github.com/eQuantic/equantic-ui/issues/384)). No line inside a lambda's block had a
  segment, so a breakpoint there bound nowhere and a frame read as the line that holds the lambda.
  A block arrow is its own node now (`JsArrowBlock`), its block the statement IR laid out where the
  lambda stands, so the text is what it was, and the expression writer composes every node's marks
  through the builder it shares with the statement writer. The author's review found the change's own
  regression, fixed before it opened: what followed a block on its closing line read as the block's
  last statement, where main read it as the statement, and now the statement takes the line back. The
  review also found a braced body losing its origin to the `if` around it, retired `JsConstArrow` for a
  `const` bound to a block arrow, and moved List's own methods and collection expressions to the IR,
  both off the text baseline. Of the 13 lambda lines `StatementSourceMapTests` reads, none had a
  segment on main. `LambdaStatementMapTests` now counts the shared components' lambda statements with
  no segment against a baseline that may only shrink: 59, where main has 79 for the same sources, the
  rest inside expression-bodied members and object creations
  ([#492](https://github.com/eQuantic/equantic-ui/issues/492)). Filed: a body with an `out` or `ref`
  parameter is wrapped as text ([#487](https://github.com/eQuantic/equantic-ui/issues/487)), List's
  `Sort`, `RemoveAll`, `BinarySearch`, `CopyTo`, `Find` and `FindIndex` answer as a JavaScript array
  does ([#488](https://github.com/eQuantic/equantic-ui/issues/488)), a default interface member maps
  its lines into the implementing class's file
  ([#490](https://github.com/eQuantic/equantic-ui/issues/490)), and a string holding U+2028 or U+2029
  shifts the map's later lines ([#491](https://github.com/eQuantic/equantic-ui/issues/491)). Proposed
  and archived through OpenSpec (`openspec/specs/transpiler-source-maps`).
- **2026-10-01 · A radio, a tab, a menu item, an option and a destination are themselves on Photon**:
  the native semantics walk knew three of the nine pressable roles and sent the rest to
  `_ => Button`, so VoiceOver and TalkBack announced all five as buttons where the web said `radio`,
  `tab`, `menuitem`, `option` and `aria-current`
  ([#338](https://github.com/eQuantic/equantic-ui/issues/338)). `SemanticRole` gains the five,
  appended at 12 to 16, and the walk's switch has no default arm, so an appended pressable role fails
  the build. `NativeRole` speaks each in the platform's own words, AppKit's and Android's from the
  W3C Core-AAM for the same ARIA role, with an `AppKitSubrole` column for the tab's `AXTabButton`;
  an option is `AXMenuItem` rather than Core-AAM's `AXStaticText`, and a destination keeps a
  button's words, since a `ListItem` is one only while it is the current row. A radio carries its
  check, as `aria-checked` does, which UIKit hears as Selected rather than as a toggle's value, the
  review's find, in a `UIKitCheckAsSelected` column. Building the macOS elements in a test found the bridge reaching its
  classes through `objc_getClass`, which answers nil where no window has loaded AppKit, and hung;
  it goes through `AppKit.Class` now, and `AppKitAccessibilityTests` reads each role and subrole
  back from AppKit. The handoff's `native-roles` request is partial: a combobox trigger and a dialog
  ([#501](https://github.com/eQuantic/equantic-ui/issues/501)). Measured on the way: a `Tabs` and a
  `RadioGroup` reach every bridge as an unnamed slider
  ([#500](https://github.com/eQuantic/equantic-ui/issues/500)), and no item says where it sits in
  its set ([#502](https://github.com/eQuantic/equantic-ui/issues/502)). Proposed and archived
  through OpenSpec (`openspec/specs/native-accessibility`).
- **2026-10-03 · The build derives the hydration contract**: the server named a component's values
  after the fields the C# compiler synthesizes, so an auto-property and a captured primary-constructor
  parameter drew on the server and vanished on hydration
  ([#509](https://github.com/eQuantic/equantic-ui/issues/509)), and a service registered as a class
  crossed whole into the HTML of a page that prefetched, while the browser, which builds a page without
  it, drew the other branch ([#510](https://github.com/eQuantic/equantic-ui/issues/510)). The source
  generator now writes a hydration manifest, the server writes each payload from it and eqc each
  twin's adoption: every value under the twin's name, and a value from the container as its
  projection, what the browser-side code reads of it, worked out from the bound tree of what the twin
  runs. A use the projection cannot follow fails the build with EQ2114, and the container has the last
  word on a value written whole. `HydrationCrossingTests` renders pages on the server and draws their
  twins in the embedded Bun on the payloads the server wrote, and the dashboard sample's Server data
  and Identity screens were measured in a browser against main. Filed on the way: two pages of one
  simple name share one entry of the page index
  ([#514](https://github.com/eQuantic/equantic-ui/issues/514)). Proposed and archived through OpenSpec
  (`openspec/specs/hydration-contract`).
- **2026-10-03 · The transpiler reads a name by its symbol**: a base written with its namespace, an
  alias or `global::` was copied into the module as written, a name nothing there defines, so the
  module failed when it loaded ([#479](https://github.com/eQuantic/equantic-ui/issues/479)); and a .NET
  member reached bare through `using static` fell to the rule for an app's own statics, so `NaN` read
  `Double.naN`, `Join` called `String.join` and `Round` sent a half up where .NET sends it to even
  ([#485](https://github.com/eQuantic/equantic-ui/issues/485)); and a type's own `Count` was read as an
  array's `length` unless the app declared the type, so a library's domain model counted `undefined`
  ([#517](https://github.com/eQuantic/equantic-ui/issues/517)). A base is named after its twin from its
  symbol, a bare member goes through the translation its qualified spelling reaches, or fails the
  build with EQ2004, and a `Count` is spelled by the receiver's symbol. `UsingStaticConformanceTests`
  executes each case on both sides. Proposed and
  archived through OpenSpec (`openspec/specs/transpiler-names`, `openspec/specs/transpiler-bcl`).
- **2026-10-03 · Patterns, locks, loops and deconstruction run as C# runs them**: a type pattern with
  nothing bound tested false and a long was never one
  ([#482](https://github.com/eQuantic/equantic-ui/issues/482)), `x is Limits.Max` was a null check
  ([#451](https://github.com/eQuantic/equantic-ui/issues/451)), a lock's expression ran nowhere
  ([#475](https://github.com/eQuantic/equantic-ui/issues/475)), `new object()` named a class JavaScript
  does not have ([#478](https://github.com/eQuantic/equantic-ui/issues/478)), a for loop gave each
  iteration its own variable ([#476](https://github.com/eQuantic/equantic-ui/issues/476)), a delegate
  called from a list was read off `this` ([#477](https://github.com/eQuantic/equantic-ui/issues/477)),
  a record deconstructed into existing variables was array destructuring
  ([#486](https://github.com/eQuantic/equantic-ui/issues/486)), and a static `field` store lived on the
  instance or, in a record, nowhere ([#483](https://github.com/eQuantic/equantic-ui/issues/483)). Each
  is a conformance case on both sides that failed against main. The local review found the first cut
  short, and widened it: every deconstruction, declared, assigned or in a `foreach`, nested or through
  a struct's own `Deconstruct`, goes through one lowering read from the bound tree; a long constant
  is a BigInt, and a constant in a pattern is tested by its value (a decimal, a NaN, a null); a char
  is one code unit; a static store starts as its initializer; and a local function's `out` reaches
  its caller ([#541](https://github.com/eQuantic/equantic-ui/issues/541)). Copilot's third round found
  four more, each fixed and run on both sides: a deconstruction's targets are evaluated before its
  value, a for loop's head of expressions is one variable for the loop, `new object() { }` is an
  object, and a property of an enum type starts as its zero member. A deconstruction into an
  indexer or a wider type is left to [#542](https://github.com/eQuantic/equantic-ui/issues/542).
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-statements`,
  `transpiler-expressions`, `transpiler-records`).
- **2026-10-03 · A route's title reaches the document**: `[Page(Title = …)]` applied only when the
  page's metadata had no title, which the app's default always filled, so no page ever got it, and a
  client navigation answered with the app's title over the one the router had set
  ([#416](https://github.com/eQuantic/equantic-ui/issues/416)); the client's configuration quoted its
  strings by hand, so a title holding a line break stopped the client of every page
  ([#526](https://github.com/eQuantic/equantic-ui/issues/526)). Each route hands its declaration to
  both doors, one builder writes a document's metadata (the app's, the route's, then the page's own),
  a navigation replaces the head's metadata as a marked set, a page answering with a status of its
  own (a 404 through `IHandleStatus`) still hands a navigation its marked payload, and the
  configuration is serialized as JSON. `PageTitleTests` runs both doors, and the dashboard
  sample's titles were measured in a browser against main. Proposed and archived through OpenSpec
  (`openspec/specs/document-metadata`).
- **2026-10-03 · A run that links follows the language**: a `TextRun` with a `Destination` lowered to
  an anchor holding the destination as written, on both producers, so with language prefixes on a run
  to `/terms` on a Portuguese page led to the English one, and a Markdown page's internal links did
  the same; it carried no `data-prefetch` either
  ([#505](https://github.com/eQuantic/equantic-ui/issues/505)). It lowers as a `Link` does now, and
  one rule decides what warms on hover for both, which no longer marks a protocol-relative URL. The
  component parity fixture lowers both under `pt-BR` on each side and compares them. Proposed and
  archived through OpenSpec (`openspec/specs/links`).
- **2026-10-03 · A value reaches the browser as its code reads it**: `Color` is the one vocabulary
  value type the browser holds as plain data, and eqc emitted its instance members as methods of the
  value, so `Color.FromRgb(…).WithOpacity(0.8f)` rendered on the server and threw once the page
  hydrated, its text read `[object Object]`, and `new Color(…)` was recognized by its name, building an
  app's own `Color` as the vocabulary's ([#494](https://github.com/eQuantic/equantic-ui/issues/494)).
  `[TwinIsData]` says so by symbol now, every member lowers to the runtime's companion and the text to
  the record text .NET writes. A `HashSet`, and the runtime's sorted set, queue, stack and linked
  list, crossed hydration as the array the server writes
  ([#516](https://github.com/eQuantic/equantic-ui/issues/516)): each is rebuilt as its class, a stack
  with its top coming off first, a server value's projection takes a set of scalars whole, and a set
  or a dictionary the browser's copy would answer differently, by a comparer of its own or an equality
  nothing can read, stays out of the page, the rest of a projected value still crossing. Every sorted
  collection the browser built or rebuilt ordered by `<`, so strings ignored the culture and decimals
  compared their text: one table (`ValueOrdering`, `utils/ordering.ts`) now names how a type orders,
  an enum by its value included, read by `Max`/`Min`, by every sorted collection eqc builds and by
  every one a hydration rebuilds, and a queue or a stack finds a member by value. On
  the way, a property pattern counted a set or a runtime collection as an array, and a `SortedSet`
  dropped its collection initializer. `VocabularyValueConformanceTests` executes every member of every type
  `[TwinIsData]` marks on both sides and checks each vocabulary value type's export against the
  attribute, which found `Curve` in two shapes
  ([#518](https://github.com/eQuantic/equantic-ui/issues/518)); the dashboard sample's Colors screen
  was measured in a browser against main. Filed on the way: `GetHashCode` has no lowering
  ([#519](https://github.com/eQuantic/equantic-ui/issues/519)), and a `HashSet` finds a date, a
  decimal or a record by reference ([#531](https://github.com/eQuantic/equantic-ui/issues/531)). The
  `SortedSet` initializer half of [#434](https://github.com/eQuantic/equantic-ui/issues/434) is fixed
  here, and its LINQ half stays open. Proposed
  and archived through OpenSpec (`openspec/specs/transpiler-vocabulary-values`,
  `openspec/specs/hydration-contract`, `openspec/specs/transpiler-bcl`).
- **2026-10-03 · A string or a char keeps its value in its module**: eqc wrote C# string and char
  values in spellings that lost them ([#520](https://github.com/eQuantic/equantic-ui/issues/520)).
  A surrogate that is not half of a pair has no UTF-8 encoding, so a page holding `"x\uD83D"` stopped
  the dashboard sample's build with `EQ0001: Compilation crash`, naming no file; a char literal
  crossed in its C# spelling, so `'\a'` and `'\e'` were `'a'` and `'e'` and `'\x1'` a syntax error;
  and `$"{date:dd 'de' MMMM}"` closed its own quotes and Bun refused the module. One writer,
  `JsStringLiteral`, now spells every string the transpiler writes from a value, a template's text,
  an interpolation's format, a skipped parameter's default, a resource lookup and `nameof` among
  them, escaping each code unit that does not show as itself, and a char literal and `nameof` are
  their value. The review found a raw interpolated string's doubled braces collapsed
  (`$$$"""a{{b}}"""` was `a{b}`), a UTF-8 literal spliced into the module as C# (refused with EQ1004
  now), and the C# 13 escape's test reading ESC culture-aware, which finds a control at position 0
  of anything. 31 of the 41 conformance cases fail on main, and a frame thrown after an interpolated
  string holding U+2028 leads to its own line, where main led it two lines down
  ([#491](https://github.com/eQuantic/equantic-ui/issues/491)). Filed on the way: a constant written
  outside a literal loses its type's representation
  ([#523](https://github.com/eQuantic/equantic-ui/issues/523)), a string read as a sequence is
  treated as an array ([#524](https://github.com/eQuantic/equantic-ui/issues/524)), a control in a
  C# file makes its source map invalid JSON
  ([#525](https://github.com/eQuantic/equantic-ui/issues/525)), and a page title holding a line break
  stops the client of every page ([#526](https://github.com/eQuantic/equantic-ui/issues/526)).
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-expressions`,
  `openspec/specs/transpiler-source-maps`).
- **2026-10-03 · A value hashes as it equals**: `x.GetHashCode()` was a call of a `getHashCode` nothing
  defines, for a string, a number and a record alike, and threw in the browser, and `HashCode.Combine`
  named a class nothing defines ([#519](https://github.com/eQuantic/equantic-ui/issues/519)). The
  runtime's hash (`$eq.hash`) agrees with `$eq.equals` case by case, so values `Equals` finds equal
  hash equal, a decimal of any scale and a date by its ticks included; a record's and a struct's
  twin carry a `getHashCode` written from the members their `equals` reads, a type that overrides
  `GetHashCode` answers its own, and a class that does not is hashed by its identity. .NET's own
  numbers are not stable across processes, so the browser keeps the contract and never the server's
  number. A Guid made from text is its canonical text, the lowercase `D` format, so two spellings
  of one Guid are one value to `==`, a dictionary and a set
  ([#459](https://github.com/eQuantic/equantic-ui/issues/459)), and a date leaving the calendar is
  refused in .NET's words, the operators naming their own parameter, where the twin built the invalid
  date without a word and `d -= span` subtracted two objects into NaN
  ([#424](https://github.com/eQuantic/equantic-ui/issues/424)). The hash strategy was written and not
  registered, and the conformance suite caught it: `StrategyRegistrationTests` now fails for any
  strategy the converter does not register. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
- **2026-10-03 · A string's own search compares as .NET does**: a string's methods that take a
  `StringComparison` read it from its spelling and lower-cased both sides
  ([#528](https://github.com/eQuantic/equantic-ui/issues/528)): the Kelvin sign matched a k under
  `OrdinalIgnoreCase`, a comparison held in a variable was dropped, `Replace` dropped its own and
  read `$&` in its replacement as a pattern, a start past the end clamped where .NET throws, and a
  sort written with `CompareTo` put every capital first. They now reach the runtime's `$eq.text`,
  chosen by the bound method and handed the comparison as the value it is: an ordinal search that
  ignores case ports .NET 10's `Ordinal` and `OrdinalCasing`, a start and a count are checked in
  .NET's words, `CompareTo` is the current culture's comparison, and a search by a culture
  comparison, which the browser has no form for, fails the build with EQ1004, or throws when the
  comparison arrives in a variable. On the way, an overload taking a `CultureInfo` slipped through:
  its parameter is `CultureInfo?`, and the type check compared a display name that carries the
  annotation, so `IsNamed` now reads a name without it. The components' pins also showed every call
  with a range bound in an arrow function to keep C#'s order, so the runtime now takes the arguments
  in the order C# writes them. Copilot's review found the comparison read from the last argument
  written, so a named start was refused as a culture comparison, and a conditional refused for the
  culture member it spells, though it is no constant. Its second round found an argument that awaits
  behind a null-conditional call that goes to a helper, wrapped in an arrow that was not async, so
  the module did not parse (`s?.Substring(await f())` too, on main); and its third, that the async
  arrow which first fixed it suspended where C# does not and handed back the result of a task the
  call returned. A local, a parameter or `this` is now guarded by a conditional with no function
  around the tail, and any other receiver refuses a tail that awaits, until a lowering binds it in
  the enclosing function ([#539](https://github.com/eQuantic/equantic-ui/issues/539)). 28 of the 48
  conformance cases fail on main, and the dashboard sample's payment filter, code editor, Markdown,
  Mermaid and diff pages were checked in a browser. Filed on the way: the overloads without a
  comparison search by the current culture in .NET
  ([#532](https://github.com/eQuantic/equantic-ui/issues/532)), a culture comparison that ignores
  case equates widths and kana types .NET keeps apart
  ([#533](https://github.com/eQuantic/equantic-ui/issues/533)), and a char search with a start
  clamps and drops its count ([#534](https://github.com/eQuantic/equantic-ui/issues/534)). Proposed
  and archived through OpenSpec (`openspec/specs/transpiler-bcl`).
- **2026-10-03 · A tab bar, a radio group, a combobox and a dialog are themselves on Photon**: the
  semantics walk announced every `Adjustable` as a slider, so a `Tabs` and a `RadioGroup` reached
  VoiceOver and TalkBack as one unnamed slider and their tabs and radios were never read
  ([#500](https://github.com/eQuantic/equantic-ui/issues/500)); a `Select`'s field reached them as a
  button that expands and an open modal layer as a plain group, where the web says `combobox` and
  `dialog` ([#501](https://github.com/eQuantic/equantic-ui/issues/501)). `SemanticRole` gains
  `TabBar`, `RadioGroup`, `ComboBox`, `Dialog` and `AlertDialog`, appended at 17 to 21 in Flutter's
  words. A tab strip and a radio group are containers a reader walks into, each tab and radio a stop
  with its state, while the bar stays the keyboard's one stop; the pressable a listbox panel hangs
  from is the combobox, the web's rule; an open modal layer is a dialog, or an alert dialog when it
  interrupts. `NativeRole` takes Core-AAM's words for the containers and the dialogs, and gives the
  select-only combobox each platform's drop-down, `AXPopUpButton` and `Spinner`, rather than
  Core-AAM's editable pair. Found on the way and fixed: the layer Photon opens for an anchored panel
  was modal by default, so an unnamed group stood in front of every open menu and select, and would
  have read as a dialog; it is one now only for the date picker's calendar. AppKit's own description
  of the dialog's `AXGroup` is "group", read back from AppKit, so the subrole is what says dialog.
  `AnnouncementParityTests` compares the web's ARIA and the native tree for these components, a
  slice of [#339](https://github.com/eQuantic/equantic-ui/issues/339), and `AppKitAccessibilityTests`
  reads the elements of real components back from AppKit. The handoff's `native-roles` request is
  shipped. Position in a set is still [#502](https://github.com/eQuantic/equantic-ui/issues/502).
  Proposed and archived through OpenSpec (`openspec/specs/native-accessibility`).
- **2026-10-03 · An enum reads as .NET reads it**: an enum has no object of its own in the browser,
  a member being held as its camelCase name and a `[Flags]` one as its number, and every place that
  turned that back into what .NET answers did it on its own, or not at all. A flags enum's
  `ToString()` answered null and a nullable enum's printed the camelCase name
  ([#452](https://github.com/eQuantic/equantic-ui/issues/452)), `"," + rank` wrote `,undefined`
  ([#535](https://github.com/eQuantic/equantic-ui/issues/535)), the statics of `Enum` named an object
  no module declares and threw ([#480](https://github.com/eQuantic/equantic-ui/issues/480)), and a
  dictionary keyed by an enum was refused by EqJson both ways
  ([#442](https://github.com/eQuantic/equantic-ui/issues/442)). The runtime's enum functions
  (`$eq.enums`) read the enum's shape, which eqc writes at the call from one member table
  (`EnumShape`), the one the casts, the arithmetic and the ordering read too. The review found the
  formats ignored (`ToString("D")` wrote the name), `IsDefined` over an `object`, the `TryParse` that
  takes a `Type` leaving the default where .NET leaves null, a cast from an `object` holding the enum
  answering undefined, a value no member names printing `undefined`, and an unknown name read as the
  enum's first member, each fixed and run on both sides. Filed on the way: an enum's table is built
  at every call instead of once per module ([#547](https://github.com/eQuantic/equantic-ui/issues/547)).
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-bcl`).
- **2026-10-03 · A test compilation shares its references**: the suites turned the .NET framework into
  new metadata references for every compilation they created, and `MetadataReference.CreateFromFile`
  copies an assembly into native memory the GC does not count, so one test host held tens of
  gigabytes ([#481](https://github.com/eQuantic/equantic-ui/issues/481)). One owner,
  `tests/Shared/TestReferences.cs`, keeps one reference per assembly for the process, and a guard
  fails on a `CreateFromFile` anywhere else under `tests/`. Measured with a sampler reading every test
  host once a second, each suite alone: the compiler suite's peak went from 25 to 35 GB to 6.1 GB and
  its run from 20 to 47 seconds to 4, the conformance suite's from 34 GB to 5.6 GB, and the web
  suite's from 3 to 5 GB to 1.2 GB. Under load, with the conformance suite alongside, 12 runs of the
  compiler suite in a row completed, where one on a branch without the change aborted the same day
  ([#473](https://github.com/eQuantic/equantic-ui/issues/473)).
- **2026-10-03 · LINQ reads any sequence**: every operator was an array method on its receiver as
  converted, so a `HashSet`, the runtime's sorted set, queue, stack and linked list, and a sorted
  dictionary or list threw on their first one, `queue.First()` called a `first` the queue does not have
  ([#434](https://github.com/eQuantic/equantic-ui/issues/434)), a dictionary read as its pairs met the
  same templates ([#439](https://github.com/eQuantic/equantic-ui/issues/439)), and a string read as a
  sequence threw on `Count`, `Where` and `Select`, was iterated by code point by `foreach` and
  `ToCharArray`, could not be built from chars, and refused `Count(char.IsDigit)`
  ([#524](https://github.com/eQuantic/equantic-ui/issues/524)). One place reads an operator's source
  (`LinqSource`, `$eq.linq.seq`), `ToList` and `ToArray` always copy, and the queue and the stack
  enumerate in .NET's order. The local review found the copy trusting operators that hand their source
  back (`Cast`, `DefaultIfEmpty`) and a read-only face hiding a list, and both copy now. Each is a
  conformance case on both sides that failed against main. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-sequences`).
- **2026-10-04 · A class and a record are built as C# builds them**: a plain class that declared no
  member got no module while its users imported one ([#423](https://github.com/eQuantic/equantic-ui/issues/423)),
  a record that declared only methods got no twin ([#428](https://github.com/eQuantic/equantic-ui/issues/428)),
  an instance indexer reached no twin and `grid[3]` read a property named "3"
  ([#427](https://github.com/eQuantic/equantic-ui/issues/427)), and a positional record that declared
  its own property for a parameter emitted it twice and did not load
  ([#546](https://github.com/eQuantic/equantic-ui/issues/546)). The twin's constructor took one
  argument per member with each initializer as its default, so an object initializer skipped the
  initializer of the member it set, a `with` ran them again and an explicit constructor's body never
  ran ([#413](https://github.com/eQuantic/equantic-ui/issues/413)); a nested collection initializer
  replaced the member's collection ([#462](https://github.com/eQuantic/equantic-ui/issues/462)); and
  statics were defined where declared, so `A = B + 1` above `B = 2` answered NaN
  ([#417](https://github.com/eQuantic/equantic-ui/issues/417)). The twin's constructor is now a branch
  per C# constructor on how many arguments arrive, one with a body of its own and one that chains
  alike, and a count two of them share is refused (EQ1009); the object initializer is applied once it
  returns, its parts the arguments of the function that applies it, so an `await` in one still parses;
  a `with` is a copy, a record's equality and text are .NET's, an indexer is the twin's `item` and
  `setItem`, and a type's statics are one initializer that starts them at their zero and runs them in
  order on first use, before any static member of a type with a static constructor is used, in the
  record, class and component emitters alike. One predicate decides a plain class's module for the
  parser and the resolver, by the chain of bases. Each is a conformance case on both sides that failed
  against main, the class cases through the module graph an app's build writes. Proposed and archived
  through OpenSpec (`openspec/specs/transpiler-records`, `openspec/specs/transpiler-interfaces`). Its review, at max effort before the pull request (eleven finders, verifiers and a sweep, every
  finding measured on both sides with a probe over the real conformance harness), found twenty-odd
  defects in the new emission, seven of them regressions against main, all fixed in the same pull
  request with a case that failed before: a refused class's simple name vetoed a component's import,
  and the resolver judged an interface or a library's base by its name; a record or a struct marked
  [ServerOnly] got a twin; a static constructor ran inside its type initializer's block (an early return
  threw, a local named `slots` did not parse, a throw left the type half-initialized, a static event did
  not start it), a constant-valued static read a later constant as NaN, statics initialized every field
  before every property, and a component's static constructor ran per instance; a struct's zero ran its
  constructor, its static constructor or an all-optional alternate, and a generic struct's was undefined;
  a constructor's arguments landed by written position in `new`, `: this(…)`, `: base(…)` and a base
  clause (BoundArguments places them as the bound tree binds them), an alternate bound its parameters as
  constants, and generated locals met members' names; an indexer's keys landed by written position, its
  assignment answered the setter's own value, `^n`, a step on an enum and a deconstruction into an
  indexer bypassed it or did not parse (one place every writer takes now); EQ1007 misjudged an indexer's
  names, and refuses two members of a record or a struct on one name; a nested initializer's extension
  Add, an ICollection member's Add and one under `?.` went wrong; a record printed an override twice and
  dropped a non-public getter; a copy constructor was refused with EQ1009; `this =` in a struct and a
  `with` on a plain struct or the code engine's records did not work. What predates the batch is filed:
  [#582](https://github.com/eQuantic/equantic-ui/issues/582) to [#587](https://github.com/eQuantic/equantic-ui/issues/587),
  [#589](https://github.com/eQuantic/equantic-ui/issues/589), [#591](https://github.com/eQuantic/equantic-ui/issues/591) and
  [#592](https://github.com/eQuantic/equantic-ui/issues/592), and so is one consequence of it,
  [#590](https://github.com/eQuantic/equantic-ui/issues/590): a hydrated record has none of the private fields its
  equality now compares. Copilot's first review found two more, fixed in the same pull request: an
  initializer applied every element after evaluating all their parts, where C# applies each before
  evaluating the next, and a record over a `[ServerOnly]` record got a twin. Its second found three: a
  lone empty partial class, record or struct got no module or twin, eleven named arguments out of
  their order ran in the signature's, and a nested initializer read its member after the element's
  parts, so the initializer is now a sequence over a temporary of its function, which
  [#588](https://github.com/eQuantic/equantic-ui/pull/588) brought. Its third found two: a field-like event's
  delegate was no state of a record's equality, and a generic struct's zero held its open type
  parameter's default (`default(Pair<int>).First` was null), so `$zero` takes each type argument's zero.
- **2026-10-04 · The compiler suite's aborts are gone**: under load its test host crashed in 2 of 6
  runs on macOS arm64, and `dotnet test` still printed `Passed!` with the count that ran
  ([#473](https://github.com/eQuantic/equantic-ui/issues/473)). Measured after #481 cut the suite's own
  test host from 29 GB to about 6 GB: 27 runs on main, 12 of them beside a full conformance run and
  seven of those beside two or three other test hosts, all exit 0 with Total 1439. No runtime setting
  is called for, and nothing under `scripts/` or `.github/` reads the `Passed!` line.
- **2026-10-04 · Two pages of one name each serve their own route**: the server held its pages by their
  simple name, so of two `Dashboard` pages in two namespaces the one registered last answered at both
  routes, with no error ([#514](https://github.com/eQuantic/equantic-ui/issues/514)). Each endpoint
  carries its page's type to the shell, the navigation state and the SSR, and the rendering service
  holds its pages by type: `IServerRenderingService` takes a `Type` where it took a name, a break.
  `SameNamedPagesTests` serves both over the real pipeline, and the shop's route rendered the admin's
  page against main. Recorded in `openspec/specs/page-routes`.
- **2026-10-04 · A source map keeps every line it came from**: three carriers still wrote what they
  held as text before any writer saw it, so none of their statements had a segment of its own: the
  arrow a body with an `out` or `ref` parameter runs in
  ([#487](https://github.com/eQuantic/equantic-ui/issues/487)), a member's expression body, and an
  object creation, initializer or anonymous object
  ([#492](https://github.com/eQuantic/equantic-ui/issues/492)), every line of a lambda's block they
  held included. The wrapper is IR, built once for a method, a lambda and a local function, its own
  lines the declaration's, and an iterator's buffer with it; an expression body takes the concise
  body's one lowering; a creation and an object literal are nodes of their own (`JsNew`, `JsObject`),
  and the three strategies leave the text baseline (81 to 78). The shared components' lambda
  statements with no line of their own went from 59 to 0. A class that takes the default its interface
  supplies wrote the default's lines into the class's file, at lines that file does not have
  ([#490](https://github.com/eQuantic/equantic-ui/issues/490)): each mapping carries its tree now, and
  the map names every file, each with its own text. The map escaped five characters by hand and the
  JSON writer behind the web manifest and the asset catalog three, so a form feed in a C# comment or a
  tab in an app's name made a file no JSON reader opens
  ([#525](https://github.com/eQuantic/equantic-ui/issues/525)): every JSON the build lays out goes
  through `JsonWriter`, whose escape is System.Text.Json's, and both maps through one map writer on top
  of it. Each case failed on main, the frames read through the composed map of a Bun bundle among
  them. The review found the iterator's buffer left out of the declaration's lines, its two lines
  mapped to nothing, and they map to the declaration now. Merged after #561, the out parameter's
  wrapper leaves the functions the lowerings write by hand, the part of
  [#539](https://github.com/eQuantic/equantic-ui/issues/539) it named, and so does one of the
  creation's. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-source-maps`, `openspec/specs/generated-files`).
- **2026-10-04 · A deconstruction writes each part as C# does**: a deconstruction was destructuring,
  which writes each part straight into its target, so a dictionary's entry as a target wrote its read,
  a SyntaxError that cost the module, and an int part into a long stayed a number, which the next long
  arithmetic refused, in an assignment, a declaration and a loop alike
  ([#542](https://github.com/eQuantic/equantic-ui/issues/542)). Each part is converted to its target's
  type as the bound tree's `DeconstructionInfo` says, and written by what its target is, an entry
  through its class. Each is a conformance case on both sides that failed against main. Proposed and
  archived through OpenSpec (`openspec/specs/transpiler-records`).
- **2026-10-04 · A find bar's Escape closes its own editor's bar**: the code editor's Escape was a
  page-wide chord mounted with the find bar, so with two bars open it closed the one mounted last,
  wherever the keyboard was ([#457](https://github.com/eQuantic/equantic-ui/issues/457)). Made the
  editor's own, around the code, it would also have taken Escape from a dialog around an editor whose
  bar is closed, and mounted only with the bar, around the code, it would have moved the code in the
  tree. `Shortcut.Enabled` gives a chord the state Flutter gives an `Action` that is not enabled: in
  the tree, binding nothing, on the web (SSR and the TypeScript twin) and on Photon. The Escape now
  wraps the layers beside ⌘F, focus-scoped and enabled while the bar is open. The local review found
  the web answering a dialog's Escape before the open bar's inside it, where Photon closed the bar:
  of two nested chords the web listed the inner one first, and a binding now takes its place before
  its child lowers, as Photon emits it. Each test failed against main's editor, against its realizer
  without the guard, or against the old order, and the dialog's against an Escape that is always
  enabled. Proposed and archived through OpenSpec (`openspec/specs/keyboard-shortcuts`).
- **2026-10-04 · Opening a panel leaves the focus where it was**: `Select`, `Menu`, `TimePicker` and
  `DatePicker` mounted the chords their panel answers to with the panel, a `Shortcut` per chord, so
  opening moved the trigger down the tree, and on Photon the keyboard focus named a path the trigger
  had left: no ring while the panel was up
  ([#567](https://github.com/eQuantic/equantic-ui/issues/567), found in the local review of #457).
  Their chords stay around the tree now, enabled while the panel is open, and each of the four failed
  against the previous component. Recorded in `openspec/specs/keyboard-shortcuts`.
- **2026-10-03 · A catch tests its type and its filter**: each catch clause was a JavaScript catch of
  its own, so two clauses were a SyntaxError that cost the module, and one took every exception, its
  type never tested and its filter dropped ([#474](https://github.com/eQuantic/equantic-ui/issues/474));
  an exception carried no type at all, so a type pattern over one was a null check, and `throw;` was a
  SyntaxError too. A .NET exception is now an Error carrying its .NET types (`$eq.exceptions`), built
  by `new T(…)` from T's symbol and thrown with .NET's type by every .NET twin of the runtime, a spec
  refusing a new untyped throw there and a conformance test comparing the runtime's table with
  .NET's hierarchy. The clauses are one catch that tries each by its type and its filter, a filter
  that throws answering false, and `throw;` rethrows the exception caught. Five lowerings wrote a
  JavaScript function around C#, so an await in `checked`, a throw expression, `Trim`'s characters,
  `Range` or `Repeat` did not parse, and `Range(Start(), 3)` called `Start` three times
  ([#539](https://github.com/eQuantic/equantic-ui/issues/539)): each takes its C# as arguments now,
  and `IntroducedFunctionsCoverageTests` counts the functions the lowerings still write by hand, per
  file, against a baseline that may only shrink. #539 stays open for two of them: the body an out
  parameter runs in, which #566 makes IR, and the null-conditional tail behind a receiver that is not
  a local, after #536. 51 of the 62 conformance cases fail on main. Typed catches exposed five
  `Parse` twins of the date and time types, `string.Format` and `new string(char[], int, int)`
  reading a null argument through null, a NullReferenceException where .NET refuses it by name with
  an ArgumentNullException, and each refuses it now. One difference stays, the platform's: .NET runs
  a filter before the `finally` blocks it unwinds, and JavaScript after. Proposed and archived through
  OpenSpec (`openspec/specs/transpiler-exceptions`, `openspec/specs/transpiler-expressions`).
- **2026-10-03 · A dictionary adds and pairs as .NET's does**: a dictionary's `Add` lowered to the
  class's `set`, which replaces, and the constructor seeded through it, so `Add`, a collection
  initializer and the constructor that copies pairs kept the last value of a key twice, where .NET
  refuses the second ([#440](https://github.com/eQuantic/equantic-ui/issues/440), the rest of
  [#395](https://github.com/eQuantic/equantic-ui/issues/395)); and `new KeyValuePair<K, V>(…)` named a
  class nothing defines ([#433](https://github.com/eQuantic/equantic-ui/issues/433)). The runtime's
  dictionaries add and refuse in each collection's words, measured on .NET 10 (a `SortedDictionary`
  names the pair it was handed, a `SortedList` the key and its parameter), an object initializer's
  `[key] = value` is assigned by the indexer, and a pair built by hand is the pair a dictionary yields.
  Proposed and archived through OpenSpec (`openspec/specs/runtime-dictionaries`).
- **2026-09-26 · A constant is its value in its C# type**: `decimal.MaxValue` emitted
  `decimal.maxValue`, a ReferenceError, because the strategy that writes a const field as its value
  left decimals to a strategy that never wrote one
  ([#444](https://github.com/eQuantic/equantic-ui/issues/444)). The same function wrote a `long`
  constant in a number's range as a number, which the first long it met threw on
  (`t / TimeSpan.TicksPerSecond`), a decimal literal went through its text (`1_000.5m`), and a
  parameter's default filled in for a skipped argument had a second writer that dropped a decimal's
  and a long's type, a char's quotes and a string's escapes. One writer now answers a constant in its
  C# type for all three paths, a const with no source is inlined under a `using static` too, the
  primitive table keeps only what is not a constant, and a narrow integer and a `ulong` annotate as
  `number` and `bigint`. The author's review found three more on the same paths: a decimal constant
  in a pattern or a `case` compared by identity and never matched, a constant of an enum type was
  written as its number (and a flags default as a name), and a lone surrogate left the module
  unwritable, and Copilot's second round a char literal written from its source text, whose `\e`,
  `\a` and `\x041` JavaScript reads as other characters: both were spelled on main by the time this
  merged, by the one writer of a string ([#520](https://github.com/eQuantic/equantic-ui/issues/520)),
  which a constant's char and string now go through. 47 of the 92 conformance cases failed on main
  when this opened, and 27 when it merged: the text, the long constants and some of the patterns,
  defaults and enum constants had been fixed on the way by the pull requests before it. The BCL audit
  grades `decimal`'s static surface: its nine translated members are proved, and the 38 it fences are
  left to their own issue ([#449](https://github.com/eQuantic/equantic-ui/issues/449)), as are an
  `is` over a named constant ([#451](https://github.com/eQuantic/equantic-ui/issues/451)) and an
  enum's `ToString` ([#452](https://github.com/eQuantic/equantic-ui/issues/452)). Proposed and
  archived through OpenSpec (`openspec/specs/transpiler-constants`).
- **2026-10-02 · A hover moves a box and changes its shadow on every target**: a `StyleDiff` could
  tint a box and nothing more, and not even that whole
  ([#507](https://github.com/eQuantic/equantic-ui/issues/507), the first slice of
  [#504](https://github.com/eQuantic/equantic-ui/issues/504)). It gains `Transform`, replacing the
  base's while the state is active, and `Shadows`, replacing the base's custom shadows. On Photon a
  hover applied three of its seven members; one effective style now feeds the wrapper that fades and
  moves a box and the chrome that fills and shadows it. On the web a hover's backdrop blur was never
  written, and a hover that raised the elevation or swapped the gradient replaced the whole
  `box-shadow` or `background-image`, taking the glow, the inset highlight and the grid pattern away
  under the pointer; a state now writes those lists again from its members and the base's. The two
  web producers wrote the base's custom shadows in opposite orders and a shadow with no geometry
  differently (a `none` inside the list in C#, which CSS rejects with the whole declaration), and an
  element listed a vendor pair's one class twice; the component parity fixture compares a box that
  sets every part. The perf harness refused the first shape: the diff was a struct, a `BoxStyle`
  carries two, and every box paid for the new members, 77,474 bytes a frame against a 74 KB ceiling,
  so `StyleDiff` is a class and the frame measures 73.2 KB. Filed: a pinned header's scrolled style,
  half drawn on the web and not at all on Photon
  ([#506](https://github.com/eQuantic/equantic-ui/issues/506)), and the second slice, with a custom
  shadow that snaps on Photon where the browser glides it
  ([#508](https://github.com/eQuantic/equantic-ui/issues/508)); and, from the review, a draggable's
  resting offset and its box's own transform taking the same CSS property, so a hover lift closes an
  open row on the web ([#511](https://github.com/eQuantic/equantic-ui/issues/511)), and, from
  Copilot's, Photon hit-testing a transformed box where it was laid out rather than where it is
  drawn, which the base transform always did and a lift now shows
  ([#513](https://github.com/eQuantic/equantic-ui/issues/513)). Proposed and archived through
  OpenSpec (`openspec/specs/interaction-states`).
- **0.2.0-preview.60 released** from `c12b761f`: the hydration contract the build derives (#515, #522),
  a hover that moves and shadows a box on every target (#512), a route's title and a page held by
  its type (#537, #538, #572), the native bridges' roles (#503, #557), the find bar's Escape and
  `Shortcut.Enabled` (#568), source maps that keep every line (#493, #566), and the browser's
  answers made .NET's for dictionaries (#443, #559), LINQ and strings (#545, #536, #527), enums
  (#548), exceptions (#561), hashes, Guids and dates (#550, #472), constants (#450), patterns,
  loops and deconstruction (#543, #562), a record's members (#464) and names read by their symbol
  (#553), with the test suites' shared references (#549) and the release's own issue (#574).
  *([v0.2.0-preview.60](https://github.com/eQuantic/equantic-ui/releases/tag/v0.2.0-preview.60))*
- **2026-10-05 · A fragment a pixel above the top, and an ordinal dictionary**: two things
  equantic-web met on 0.2.0-preview.60. A fragment link under its floating header landed BEHIND the
  header on a warm load ([#576](https://github.com/eQuantic/equantic-ui/issues/576)): the target's
  document top was 2471.796875, the browser's jump scrolled to 2472, and the cold-load correction,
  whose band started at 0, refused the -0.203125 it saw on every frame of its watch. A layout
  position is fractional and a scroll offset is whole pixels, so which side of the top a jump lands
  on is a coin toss on the fraction; the band starts one device pixel above it now, never less than
  one CSS pixel, which a page zoomed out to 50% needs (found in review). Measured with DevTools
  logpoints, which leave the cache alone (a route that served an instrumented runtime moved the race
  and hid it), and proved on the site itself, published on 0.2.0-preview.60 with the package's
  Server and then this branch's: four of six cases, then six of six. And
  `new Dictionary<string, T>(StringComparer.Ordinal)`, the default for a string key, failed the build
  with EQ1004 ([#577](https://github.com/eQuantic/equantic-ui/issues/577)): #443's construction
  refused every comparer parameter, a stricter copy of the fence that already passed that one, and
  `CollectionComparerFenceTests` checked only for the fence's own code. The construction skips a
  comparer now and a sorted one keeps the order it asks for; the fence decides alone. Found on the
  way: a page route answers `HEAD` with a 404 ([#575](https://github.com/eQuantic/equantic-ui/issues/575)).
  Proposed and archived through OpenSpec (`openspec/specs/links`, `openspec/specs/runtime-dictionaries`).
- **2026-10-05 · A null-conditional binds its receiver in its own function**: a null-conditional call
  whose translation is a helper bound a receiver that is not a local with an arrow invoked on the
  spot, where an `await` in its arguments made a module JavaScript refuses to parse, so #536 refused
  that shape with EQ1004 and asked for a local
  ([#539](https://github.com/eQuantic/equantic-ui/issues/539)). The receiver is now assigned to a
  temporary the statement around it declares, `(($n0 = get()) == null ? null : $eq.text.trim($n0))`,
  as Roslyn keeps it in a local of the method, so the tail runs in the method it is written in and an
  `await` in it is the method's own. A concise lambda that binds one declares it in a block of its
  own, so every call keeps its own receiver, and a loop a label names leaves it to the label:
  declared between them, `continue outer` is a SyntaxError, measured with that rule removed. A
  translation that names a temporary is never served from the node cache. The arrow stays only where
  no statement can declare one, an initializer and a record's base call, which C# never lets await.
  The 6 conformance cases with an `await` behind a call, a property and a guard in the tail of
  another, in a lambda run three times at once over an array's elements, and behind a call that
  answers null and must not suspend the method, fail on main. #539's other sites closed in #561 and
  #566. Proposed and archived through OpenSpec (`openspec/specs/transpiler-expressions`).
- **2026-10-06 · A page answers HEAD as it answers GET**: every page route and the modules under
  `/_equantic/` were mapped with `MapGet`, so a HEAD fell through to the fallback and answered 404
  where the GET answered 200, measured in the 0.2.0-preview.60 release proof on a fresh app
  ([#575](https://github.com/eQuantic/equantic-ui/issues/575)): an uptime monitor, a link checker or
  a crawler that asks with HEAD read every page as missing. A `[Page]` route and its culture twin, a
  `MapPage<T>` route, `runtime.js`, an app's module and its source map are mapped for GET and HEAD
  now, with the GET's handler, and Kestrel writes no body for a HEAD. `HeadRequestTests` runs on
  Kestrel, because the test host answers a HEAD with the whole body (measured), and its four HEAD
  cases fail against main's routes. Proposed and archived through OpenSpec
  (`openspec/specs/page-routes`).
- **2026-10-05 · A list, a set and a join answer as .NET's do**: a `List<T>` sorted by its elements'
  text and stably, `RemoveAll` threw a ReferenceError, `BinarySearch` was a `findIndex`, a comparer
  named a class nothing defines, `FindIndex`'s range reached its predicate, `CopyTo` wrote nowhere and
  `Find` answered undefined ([#488](https://github.com/eQuantic/equantic-ui/issues/488)); `IndexOf`
  compared with `===`, so a NaN, a record and a tuple were never found, and a tuple's array compared
  element by element ([#425](https://github.com/eQuantic/equantic-ui/issues/425)); a `HashSet` was a
  `Set`, appending where .NET reuses a freed slot ([#438](https://github.com/eQuantic/equantic-ui/issues/438))
  and finding a date, a decimal, a tuple and a record by reference
  ([#531](https://github.com/eQuantic/equantic-ui/issues/531)); `string.Join` called `join`, which a set
  and a linked list lack ([#429](https://github.com/eQuantic/equantic-ui/issues/429)), and wrote each
  value as JavaScript writes it ([#441](https://github.com/eQuantic/equantic-ui/issues/441)). One
  equality decided from the element type (`ElementEquality`, `utils/key-equality.ts`) serves a list's
  search, a set's elements and a dictionary's keys; the set and the dictionary share one slot table
  with .NET's capacity (`utils/slots.ts`, `utils/hash-set.ts`); the sort is .NET's introspective sort,
  both helpers, traced comparison for comparison against .NET through its heap sort (`utils/sort.ts`,
  `utils/list.ts`); and a join converts each value as a concatenation does. 185 of the 214 conformance
  cases, run on both sides, failed against main; the other 29 are neighbours that already held, kept as
  pins. The batch waited out the .60 cut, and main's later rules met it at the merge: its runtime threw
  plain Errors that #561's typed catch let through, and throws .NET's types now (14 of 15 catch cases
  failed before); a key compared by value is found through #550's hash, where a walk over every slot
  cost 2,000 tuples four million comparisons; a module imports `$eq` wherever its body names it, which
  a generated equality in a hydration map or a `ContainsValue` did not register; and `CopyTo` through
  an `ICollection<T>` copies a set. The served runtime grew about 7 KB gzipped. Proposed and archived
  through OpenSpec (`openspec/specs/runtime-sets`, `runtime-dictionaries`, `transpiler-bcl`).
- **2026-10-06 · A class is built as C# builds it**: a plain class kept its widest constructor and
  called its base's with no arguments, and a C# 12 primary constructor on a class was not read, so
  `Money() : this(100)` built no cents, `: base(x * 2)` passed nothing and `new Greeter("ada").Hello()`
  answered "hi " ([#583](https://github.com/eQuantic/equantic-ui/issues/583)); a derived class ran its
  initializers after its base's constructor ([#571](https://github.com/eQuantic/equantic-ui/issues/571));
  an object initializer was a trailing config object, evaluated before the constructor ran, and a
  property's initializer ran before every field's ([#582](https://github.com/eQuantic/equantic-ui/issues/582));
  an exception dropped its initializer ([#587](https://github.com/eQuantic/equantic-ui/issues/587));
  and the vocabulary's types the runtime transpiles (the spreadsheet's and the forms' models) were
  taken by an app for hand-written twins, so `new CellRef(1, 2) { Col = 3 }` kept its 2 and
  `default(CellRef)` threw ([#592](https://github.com/eQuantic/equantic-ui/issues/592)).
  The record twin's constructor (#413) moved into one builder written as IR, which a plain class's
  constructor comes from too: every constructor is reached by its counts, the class's state starts in
  declaration order, each member a class field with no initializer that the constructor writes, a
  derived class's initializers run before `super()` and land after it, and a construction builds, then
  applies its initializer. A class's held primary parameter on another member's name is EQ1007, as a
  struct's is. The vocabulary marks its transpiled types `[TwinIsTranspiled]`, a test holding the mark
  on exactly those, and every decision about a twin eqc writes reads it: without it, this change would
  have dropped `new SheetController(10, 4) { Changed = … }`'s handler too. Of the 25 conformance cases
  of a class this adds, run through the module graph on both sides, 18 fail on #608's head, a `params`
  constructor not even loading (its rest parameter was written before the config), and the other 7 are
  neighbours kept as pins; of the 6 of a transpiled vocabulary type, 3 fail there. Found on the way:
  an app exception's own members
  ([#611](https://github.com/eQuantic/equantic-ui/issues/611)) and enumerating a class that implements
  `IEnumerable<T>` ([#612](https://github.com/eQuantic/equantic-ui/issues/612)). Proposed and archived
  through OpenSpec (`openspec/specs/transpiler-classes`, `openspec/specs/transpiler-exceptions`).
- **2026-10-06 · A property keeps its value in a store of its own**: a twin's constructor wrote each
  member onto the instance under its name, which reached any accessor of that name along the chain. A
  record's auto-property over a base's computed `virtual` one, or a computed override over a base's
  auto-property, threw at `new` ([#591](https://github.com/eQuantic/equantic-ui/issues/591)), and a plain
  class of those shapes answered its base's value; an override that declared only a getter lost the
  setter it inherits, and one that read `base.Name` read nothing; and a record's or a struct's property
  that uses `field` ran none of its accessors' bodies, so `new FRec { X = 5 }` over
  `set => field = value * 2` held 5 for 10 ([#615](https://github.com/eQuantic/equantic-ui/issues/615)).
  Building a class as C# builds it had also declared its state for TypeScript only, so
  `int count; int Count => count;`, one name in the twin, threw at `new`, where #608's head answered
  right. A class's state is a class field with no initializer again, which JavaScript defines before
  the constructor writes it, in the order the constructor always wrote it; a property that can be
  overridden, or that has an accessor of its own over a backing field, keeps a store, `$name`, under
  accessors on the prototype, which the constructor starts, a record compares and copies and a
  struct's zero zeroes; an override that declares one accessor forwards the other to `super`; and a
  twin with a store answers `toJSON` with `$eq.json`, which writes the store under its property's
  name, as System.Text.Json writes the property (a class's `field` property was sent to a server action
  as `$name`). Of the 15 conformance cases this adds, 10 fail on #608's head, 3 of them throwing, and
  the one of a field beside its property, which passes there, pins the regression. One difference stays,
  documented: `base.Name` over an auto-property overridden by another reads the override's value.
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-classes`,
  `openspec/specs/transpiler-records`).
- **2026-10-06 · The code engine completes**: the engine half of the code editor's slice 3
  ([#296](https://github.com/eQuantic/equantic-ui/issues/296)). `CodeCompletion`, a session on the
  controller, asks its providers once when a word starts, filters and ranks what they answered on
  every keystroke (`CodeFuzzyMatch`), asks an incomplete answer again, and drops a late answer by its
  generation while its request is cancelled; the keymap routes the list's keys, Enter accepting only
  what changes the text; the contracts are LSP's, typed; the language's words and the document's are
  built-in providers, and an editor starts with none until the view
  ([#297](https://github.com/eQuantic/equantic-ui/issues/297)) draws a list. Held by keystroke
  sequences, by Roslyn's answer in the playground recorded as a fixture, and by the twin compared with
  .NET in the embedded Bun over 6,000 patterns and 60 seeded sessions. Found on the way: eqc called a
  method named `Invoke` as a delegate, named an exception, an interface, an enum and a delegate by
  their C# names in annotations no module defines (one rule, `TsStandIn`, decides it on every path
  now), and knew nothing of the cancellation trio, which is now the runtime's, measured with
  `dotnet fsi` and run on both sides by the conformance suite; and the shared library's twins were
  transpiled with three of the SDK's seven implicit usings. The author's review found ten defects,
  each proved failing without its fix: the trap on Tab after ⌃Space, a provider's cancellation that
  threw into the keystroke, a commit during an input method's composition, a duplicate that hid a
  match, the filter's work per keystroke, and the document's words read whole at every word started
  (46 ms in Bun for 45,000 lines, now 50,000 characters nearest the caret) among them. The first
  review round found three more: a provider's range that moved with the caret, a minified line read
  from its start, and a linked source that kept its callbacks on a long-lived token; the second, a
  cancellation a provider threw by itself taken for the request's own, a `TimeProvider` source that
  compiled and dropped its clock, and a disposed source that kept its callbacks; the third, a word of
  megabytes that walked past the window, a month's delay that a browser's timer fired at once, and a
  default registration that was undefined. Proposed and archived through OpenSpec
  (`openspec/specs/code-completion`).
- **2026-10-06 · A target under a pointer keeps a 24dp floor**: under a mouse a target was its visual
  bounds, so a Checkbox or a Radio without a label was a 20dp target on Photon and on the web, under
  the 24 × 24 WCAG 2.2 SC 2.5.8 asks, and the cross-pin passed because both agreed on 20
  ([#430](https://github.com/eQuantic/equantic-ui/issues/430), decided by Edgar on 2026-09-26).
  `Touch.MinPointerTarget` is the floor Photon's Compact hit rect, `Sizing.HitTarget` and a fine
  pointer's slop on the web grow to, published in the handoff at `touch.minPointerTarget`. Measuring
  it in a browser found the web's slop drawn over the control's content: under a fine pointer the
  centre of a Button hit the button element itself, so its box never matched `:hover` and no button
  showed its hover fill, and a Pressable around an IconButton took the inner control's hits. The slop
  is now the `::before` with the content lifted above it, and answers only around the control.
  Proposed and archived through OpenSpec (`openspec/specs/hit-targets`).
- **2026-10-06 · A component hears the server on a typed topic**: the only door from the browser to
  the server was a Server Action, so no component could hear what someone else did, and an empty
  SignalR hub, a CDN script nothing called and an unbundled client stood where the door would be
  ([#291](https://github.com/eQuantic/equantic-ui/issues/291)). `ServerTopic<T>` names a topic and its
  payload's type; `IServerEvents` is the capability a component subscribes through, over one
  Server-Sent Events stream per page that reconnects by itself and binds every live topic again
  before it reports connected; `IServerEventPublisher` publishes from any server code; a topic is
  authorized by route templates with ASP.NET Core policies (the topic as their resource), anonymous
  access or a delegate, closed by default; `IServerEventBackplane` and `IServerEventHandler` are the
  seams for several instances and for presence; the limits bind from `EQuantic:ServerEvents`. eqc
  hands the twin the payload's hydration spec through `[HydratesTypeArgument]`, and two defects of its
  own met on the way went at the root: a generic vocabulary type was imported from a sibling module,
  and a target-typed one whose argument is a list was built as a collection expression. Proving it in
  a browser found five defects of the server's half, each fixed with a test that fails without it: an
  app's fallback policy refused the stream, a bind authorized after its stream ended was never
  released, binds sent at once passed the topic limit, a payload's U+2028 split its JSON, and a stream
  held a graceful shutdown until the host's timeout. The review before it opened found nine more,
  each fixed with a test that fails without it: a bind read a body of any type, so another site could
  bind its visitor's topics to a stream it opened itself; a bind that met the end of its stream was
  refused for good; a topic built in a generic helper lost its payload's type with a green build (now
  EQ2013), and one that crossed the wire lost it too; limits nothing could run with started cleanly; a
  second `UseServerEvents` made every route ambiguous; a page whose app served no events retried
  silently forever; the refusal followed the app's JSON naming; and the body had no cap. Copilot's
  first round found seven: a record none of whose members needed coercion crossed as a plain object,
  as a topic's payload and as a Server Action's result alike, and is now rebuilt on its twin wherever
  it crosses; `topic with { }` lost its spec and `DataPalette.Default with { }` threw; a handler still
  awaiting a join heard the leave first; an overflow waited for the write it was blocking; an
  unanswered release left the topic held; a request that never answered held the next one; and
  connected came before a topic subscribed meanwhile was bound. The equality of two topics of one name
  and different payload types is the erasure every generic record has (#651). Its second round found
  six: a topic's policies ran against the request's principal without their own schemes, so the
  cookie's user met a bearer-only policy, and they are now evaluated as the authorization middleware
  evaluates an endpoint's; a generic record's type argument crossed unrevived, and a constructed one is
  now described by its own members; a bind whose answer was lost was never released; a reading and a
  refusal nobody assigned were null in the browser; a template's defaults never reached its matcher;
  and the test stream's wait for an event never failed while heartbeats came. Its third found one: of
  two rules for one topic, a library's and the app's, the first registered decided alone, so a
  library's anonymous topic hid the app's that required a user; every rule that fixes as much of a topic
  now rules on it, each allowing it or not. The served runtime grew 3,124 gzipped bytes.
  Migration: `ServerActionHub`, its route `/_equantic/hub` and the `AddSignalR()` call `AddUI` made
  are gone; an app that injected `IHubContext<ServerActionHub>`, which nothing documented, publishes
  through `IServerEventPublisher`, and an app that maps hubs of its own calls `AddSignalR()` itself. Proposed and archived through OpenSpec (`openspec/specs/server-events`).
- **2026-10-06 · A char's search checks its start and its count**: `IndexOf(char, int)`,
  `IndexOf(char, int, int)`, `LastIndexOf(char, int)` and `LastIndexOf(char, int, int)` were
  JavaScript's `indexOf` and `lastIndexOf`, which take no count and clamp a start outside the string,
  so `"abcabc".IndexOf('c', 0, 2)` answered 2 where .NET answers -1 and `"abc".IndexOf('a', 4)`
  answered -1 where .NET throws ([#534](https://github.com/eQuantic/equantic-ui/issues/534)). They
  reach the runtime's `indexOfChar` and `lastIndexOfChar`, ported from .NET 10's
  `String.Searching.cs` and measured with `dotnet fsi`: `IndexOf`'s start may stand at the end of the
  string, `LastIndexOf`'s must stand on a char of it (the string overloads step back from one past
  the end instead), an empty string's `LastIndexOf` answers -1 for any start and count, and each
  refusal is in .NET's words, the start checked before the count. The call is built by the runtime
  call the comparing overloads use. 9 of the 16 conformance cases fail on main. The Markdown, Mermaid
  and curly-brace parsers and the SDK's strings, which search a char from a start, now throw where
  the same C# throws on the server. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
- **2026-10-06 · The conformance harness compares a value as the runtime holds it**: the .NET side
  wrote a value as System.Text.Json writes it and the JS side as `JSON.stringify` writes the runtime's,
  so a long, a decimal, an enum and a float compared backwards: a long that crossed as a BigInt
  printed `"5"` against `5` and failed, and one that became a JS number passed
  ([#596](https://github.com/eQuantic/equantic-ui/issues/596)). The cases returned `.ToString()`
  instead, which hid it. The .NET side writes each kind as the runtime holds it now (`RuntimeJson`: a
  BigInt's digits, a decimal's text, an enum's twin name, a double as JavaScript's `Number::toString`
  writes it, a tuple and a pair as arrays), and the JS side prints a BigInt the same way whether or
  not it imported the runtime, where `return 5L;` threw. 33 of the first 37 cases returning each kind
  directly failed against the old harness, and the double writer matches bun's `String` on 5,000 doubles. Of
  the 3,557 cases already there, one newly failed, and it was a real bug:
  `DateTimeOffset.ToUnixTimeSeconds()` answered a JS number for a long, and rounded an instant before
  1970 toward the epoch; it answers a BigInt cut as .NET cuts it now. Found on the way: the date
  constructors past the second ([#606](https://github.com/eQuantic/equantic-ui/issues/606)).
  Proposed and archived through OpenSpec (`openspec/specs/runtime-dates`).

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
