# docs/

What lives here, and in what order to read it. The user-facing documentation is the
[wiki](https://github.com/eQuantic/equantic-ui/wiki); this folder holds what the wiki does not — the
measured audits of the SDK's own structure, the plans for the work in flight, and the record of the
plans that finished.

## Architecture, measured

Read these first, in this order. Every claim in the first two is held by a test, and both say which.

| Document | What it answers |
|---|---|
| [ARCHITECTURE-AUDIT.md](ARCHITECTURE-AUDIT.md) | *Where is our own structure weak?* — the layering, the dispatches over the vocabulary, the weight of each assembly, the duplications, and an order of attack. Pinned by `VocabularyCoverageTests`, `AssemblyLayeringTests`, `HandoffVocabularyTests`. |
| [FLUTTER-PARITY.md](FLUTTER-PARITY.md) | *How does Flutter solve this?* — one row per Flutter concept, with a verdict (SAME, DIFFERENT, PARTIAL, GAP) measured against the tree. Pinned by `FlutterParityPinTests`. |
| [VOCABULARY-DISPATCH-PLAN.md](VOCABULARY-DISPATCH-PLAN.md) | The plan for the audit's step 2: one visitor per realizer, slice by slice. |
| [HANDOFF-FIDELITY-AUDIT.md](HANDOFF-FIDELITY-AUDIT.md) | The design system's component blocks compared with the shipped components, claim by claim. A snapshot (2026-08-16) with a "closed since" ledger. |
| [DIAGNOSTICS.md](DIAGNOSTICS.md) | Every `EQxxxx` diagnostic the compiler and the build raise, held to the code by `DiagnosticsDocumentedTests`. |
| [design/](design/README.md) | The Photon Design System handoff itself — the normative `tokens.json` and the design pages — corrected here and pinned to the implementation. |

## Tracks in flight

Plans for work that is still landing. Each names its slices and which are done.

| Document | Track |
|---|---|
| [NATIVE-GPU-ENGINE-PLAN.md](NATIVE-GPU-ENGINE-PLAN.md) | Photon — the native GPU engine, its backends and shells. |
| [DESKTOP-PLAN.md](DESKTOP-PLAN.md) | Track W — Photon on the desktop: packaging, signing, notarization, Windows. |
| [SHARED-COMPONENTS-PLAN.md](SHARED-COMPONENTS-PLAN.md) | The write-once architecture: one vocabulary, realized per target. The plan that produced the current layering. |
| [STYLE-SEMANTICS-PLAN.md](STYLE-SEMANTICS-PLAN.md) | Track S — CSS-free authoring and the atomic style engine. |
| [CHARTS-PLAN.md](CHARTS-PLAN.md) | The chart library, written once and drawn by both realizers. |
| [I18N-PLAN.md](I18N-PLAN.md) | Track L — localization the .NET way. |
| [BOUND-TREE-PLAN.md](BOUND-TREE-PLAN.md) | Compiler phase 5 — translating from the bound tree. |
| [COVERAGE-PLAN.md](COVERAGE-PLAN.md) | Compiler phase 6 — deriving the BCL mapping, with baselines that may only shrink. |

## History

What happened and when, and the one finished plan still kept for the reasoning it records. A plan
whose slices are all delivered is retired into the ledger — its dated lines condensed, what it still
owed turned into an issue — unless code or another document cites it.

| Document | What it is |
|---|---|
| [LEDGER.md](LEDGER.md) | The chronology: one line per event, oldest first, each with the pull request, release, issue or section that holds the evidence; the retired plans' status lines; a snapshot of the [board](https://github.com/orgs/eQuantic/projects/11) naming every issue. Held to the folder by `DocsIndexTests`. |
| [DOTNET-COVERAGE-PROGRAM.md](DOTNET-COVERAGE-PROGRAM.md) | The program behind phases 5 and 6: maximal C# → JS fidelity, three mechanisms per construct. |

## The rules these documents follow

- **Measured, not recalled.** A number says where it comes from, or it is re-measured before it is
  quoted; a claim about the tree is a test where a test can be written.
- **Bilingual where the wiki is.** This folder is English, as the code is. The wiki is English with a
  Portuguese twin per page, edited in the same commit.
- **Never "widget".** The project's word is *component*; Flutter's word appears only in Flutter's
  column of the parity audit.
