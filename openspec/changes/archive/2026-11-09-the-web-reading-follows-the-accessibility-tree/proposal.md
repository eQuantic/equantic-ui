# Proposal

Closes #339, under the 1.0 epic #898.

## Why

The same component feeds ARIA on the web and the semantics tree Photon hands VoiceOver and TalkBack, and
nothing compared the two whole: a radio, a tab and three more roles were a Button on every native platform
with every suite green. The comparison needs a reading of the web that is the accessibility tree a browser
builds, and the component tester's reading (#1272) missed three of its rules, which the comparison found
the moment it ran.

## What Changes

- **The component tester reads the web as the browser's accessibility tree is built.** `aria-owns` moves an
  owned element under its owner, after its own children, so a table written in bands is read row by row; a
  field with no label is named by its `title` and then its `placeholder`, HTML's last resorts; and a live
  region (`status`, `log`, `alert`, or `aria-live`) is announced as the vocabulary's `Group`, the stop
  Photon hands the platform's reader to mark live. `ComponentTester.Semantics` changes accordingly: a page
  with a toast now announces the group around its message.
- **A semantics parity fixture** (`SemanticsParityTests`, in the native suite) reads every public component
  of every assembly beside it (built with plausible arguments), every `PressableRole`, and curated cases with
  real content and opened disclosures, on both targets, and compares role, name, value, the five states and
  order. A difference fails unless `semantics-parity.baseline.txt` names it with its reason, and the list may
  only shrink. Its 72 entries wait on #1328 to #1333, the six families of differences it found. It replaces
  `AnnouncementParityTests`, whose cases it carries.

## Capabilities

### Modified Capabilities

- `component-testing`: the web reading follows the accessibility tree's rules for ownership, the name's
  last resorts and live regions.

## Impact

- `eQuantic.UI.Testing`: the reader only, no public signature moves.
- Tests: the native suite gains the fixture and its baseline and loses `AnnouncementParityTests`; it links
  `FactoryArguments` from the web suite and references `eQuantic.UI.Testing`.
- eqc, the runtime, the realizers, the Photon shells, the SDKs and the templates: no change.
- Docs: a ledger entry. Nothing is removed from the public surface, so no app needs a migration line.
