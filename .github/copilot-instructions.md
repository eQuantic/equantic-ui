# Review instructions

eQuantic.UI is a .NET SDK: C# components compiled to JavaScript for the web (no WASM) and drawn by its
own GPU engine (Photon) on native. The rules that decide designs are in docs/PRINCIPLES.md and how a
change is proved in docs/PROVING.md; CLAUDE.md states the main ones at length. Hold every pull request
to them, and name the rule when you flag something.

Flag, as a defect:

- A developer of an app made to write a platform artifact: Swift, a plist or an entitlements file, an
  Android manifest, JavaScript, HTML or CSS, or a setting whose value is a platform incantation the SDK
  could derive. Everything an app writes is C#, appsettings or fluent configuration in Program.cs.
- A target's word in the abstract vocabulary (eQuantic.UI.Primitives, eQuantic.UI.Components): would
  the name exist if the web did not? HtmlElement is the exception: it mirrors the DOM one to one.
- A component or node implemented or tested on one target only. Components are written once and
  realized per target, and both sides are driven.
- A fix shaped by one consumer app's case instead of the general capability it reveals.
- A workaround for a gap in the compiler or the SDK (C# rewritten to fit the transpiler, a sample
  bending around a missing feature) instead of the gap being fixed or filed.
- A compatibility member kept alive while the version is a preview: an overload, a forwarder, an
  [Obsolete] member, a Deconstruct kept beside a widened one. The old shape goes, and a migration
  note says what to write instead.
- A decision made on a type's name or a fixed list where the Roslyn semantic model can be asked; a
  new static XxxHelper class in the C# where an extension method or a base class would do.
- A generated file built from concatenated strings instead of a writer on CodeWriter.
- A patch where a structural fix exists, a home-grown mechanism where .NET or MSBuild has one, a
  warning where a failing check would do, or a pin that rewrites itself without an EQ_UPDATE_* variable.
- A test that cannot fail: one that passes without the fix, or with the most plausible wrong fix;
  a golden that is the only proof of a state (pressed, focus, selection); a pin over a hand-written
  list where the set can be derived; a guard on the good path only.
- A claim about what .NET does (rounding, formatting, parsing, ordering) that the change relies on
  without having measured it: .NET is the oracle.
- The word "widget" for our types or in prose (the term is component), and a new identifier that
  carries the company's name (the short identity is Eq: eqc, $eq, EQ diagnostics).

Also check, as the workflow requires: the title is `emoji type: description` in English and the body
says `Closes #N`; a behaviour change carries an OpenSpec change archived before the merge; a ledger
entry is a file of its own under docs/ledger/; no attribution or generated-with line anywhere.
Everything committed is in English, code comments included.

Do not flag as defects: deliberate breaking changes in preview that come with a migration note, the
TypeScript runtime's $eq helpers, and HtmlElement's DOM names.
