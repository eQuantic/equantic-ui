# The principles that decide designs

Every rule here was learned on a real change, usually by getting it wrong first, and each one is
stated with the case that produced it. Where a test holds a rule, the test is named: a rule that a
build can check is checked, because a written rule did not stop the same mistake from coming back.
Where [CLAUDE.md](../CLAUDE.md) states a rule at length, this page gives its short form and points
there; the long form wins.

How a change is shown to be right is the other half, in [PROVING.md](PROVING.md).

## What the developer writes

### 1. A developer never writes a platform artifact

Everything an app's developer writes is C#, `appsettings`, and fluent configuration in
`Program.cs`, inside ordinary .NET semantics: never Swift, an Xcode setting, an Android manifest, a
plist, JavaScript, HTML or CSS. The SDK knows the platform's details and resolves them; the developer
states only simple, meaningful settings. A feature that works but makes someone learn a platform
artifact is not done. The measured case: a framework-dependent app signed with the macOS hardened
runtime cannot launch without two entitlements, and the SDK declares them itself, because it already
knows whether the app is AOT and whether hardening is on.

At length: the product principle in [CLAUDE.md](../CLAUDE.md). Held by `DeveloperSurfaceContractTests`,
which pins every MSBuild property, configuration section and template parameter an app can write.

### 2. Think as an SDK, never as a solution made to measure

eQuantic.UI is an SDK for any kind of project. A request from a consumer app is evidence of a gap in
the SDK, not a specification: translate it into the general capability it reveals before designing
("a Select cannot group its options", never "the select of app X"), and let the consumer's case be
one test of the design, never its shape. A fix shaped by one app serves that app and leaves the next
project to meet the same gap in another form. When a request only makes sense for one app, say so,
and find the capability under it or decline it. A release is the list of gaps it closes.

### 3. The experience is .NET's own

What a developer configures is named and behaves as .NET's own: MSBuild properties named like
`PublishAot` (`EqcCliPath`, never a company prefix, see 8), settings through the standard
configuration binder, localization through `CultureInfo`, `.resx` with a typed accessor and satellite
assemblies, never a JavaScript catalog or a DSL of our own. The canonical design for localization is
[docs/I18N-PLAN.md](I18N-PLAN.md): a proposal is a dated edit of the plan, not a redesign in a pull
request. No component hardcodes a string a person reads, an accessibility label included.

## How the SDK is shaped

### 4. Written once, realized per target

A component is written once, against the vocabulary in `eQuantic.UI.Primitives`, in a shared
assembly; each realizer lowers it to its target (DOM and atomic CSS on the web, GPU paint on
Photon). Nothing is authored twice. A node written for two targets and run on one is half delivered:
the web lowering of the code surface was written before it ever ran in a browser and had five silent
defects, all absent on Photon, which had been driven from its first key. See
[docs/SHARED-COMPONENTS-PLAN.md](SHARED-COMPONENTS-PLAN.md).

### 5. The vocabulary speaks no target's language

A name in the vocabulary is asked one question: would this word exist if the web did not? If not,
it is a target's word, and the vocabulary uses a neutral one (`Pressable`, not "Button" or
"Clickable"; `TextRun.Destination`, not `Href`, because Apple and Android use the same shape, a run
with a navigation attribute, but neither uses that name). The one place where a target's words are right is
the escape hatch that transcribes the target: `HtmlElement` mirrors the DOM's attributes one to one,
and a collision with another type is resolved on the other side, never by renaming the mirror.

At length: "The vocabulary speaks NO target's language" in [CLAUDE.md](../CLAUDE.md).

### 6. When in doubt, ask how Flutter solves it, and then do better

Before inventing a mechanism or a name, look at Flutter's answer, recorded row by row in
[docs/FLUTTER-PARITY.md](FLUTTER-PARITY.md). The point is not to copy it: the answer is often more
precise than the question. Asked whether a child knows its parent, Flutter answers three times,
because it has three trees: a `Widget` does not (it is rebuilt every frame), an `Element` does (it is
the `BuildContext`), a `RenderObject` does. Our `VisualNode` is the first tree and must not gain a
parent; `LayoutNode` is the third. A question that does not say which tree gets the wrong answer.

### 7. The design handoff is the authority, and it lives here

The design system's handoff is in [docs/design/](design/), and it is corrected here, not in a tool
outside the repository. A consumer's own design is that consumer's product: it does not reshape the
SDK's ladder of sizes or its surfaces. When a sample cannot match the handoff exactly, the SDK is
missing something; a workaround in the sample is a bug left unfixed. See
[docs/HANDOFF-FIDELITY-AUDIT.md](HANDOFF-FIDELITY-AUDIT.md).

### 8. The project's words

The term is *component*, never *widget*, in code, documentation and prose. `Widget` is written only
as the name of Flutter's type when comparing. And no identifier carries the company's name: the
short identity of the SDK is `Eq` (`eqc`, `eqicon`, `$eq` in the runtime, `EQ` in the diagnostics),
in the way Microsoft prefixes with the product (`Maui`, `Android`), never with `Microsoft`. Names
from before this rule (`EQuanticAssetsDir` and its siblings) are owed a rename.

## How the code is built

### 9. The structural fix, the mechanism .NET already has, and the transpiler as the bar

Of two fixes, take the structural one: declaring a build dependency beats warning that it is
missing. Of two mechanisms, take the one .NET, MSBuild or Roslyn already has: a `ProjectReference`
with `ReferenceOutputAssembly=false` beats a `Condition="Exists(…)"` over the solution's own output.
Of two instruments, take the one that fails: a pin that compares and fails, regenerated only behind
an `EQ_UPDATE_*` variable, beats one that warns or rewrites itself. The transpiler (a strategy per
construct, an IR with one writer per level, coverage suites with baselines that may only shrink) is
the bar the rest of the SDK is measured against.

At length: "The bar for a decision" in [CLAUDE.md](../CLAUDE.md).

### 10. Discover, never hardcode

Decide from the semantic model, never from a name or a fixed list: walk `BaseType` with the symbol,
read `IsRecord` and `TypeKind`, and build a registry by scanning rather than keep a skip-list. A name
heuristic is legal only where the model cannot be asked, and an in-tree call the model cannot bind is
a build error (EQ2006), never a guessed translation. The case that made it a rule: one method asked
the model whether a class is a component, and forty lines later compared a string to three literals
to decide which kind it was.

### 11. Object orientation over static helpers, in the C#

In the compiler and the SDK's C#, a predicate or a transformation on a symbol is an extension method
(`type.IsStructuralValueType()`), shared behaviour is a base class, and a new static `XxxHelper`
class used as a drawer of functions is not written. The TypeScript runtime is the exception: its
`$eq` helpers carry .NET semantics JavaScript does not have.

### 12. Generated files are written by a writer

A generated file is written by one writer per file type, on the common `CodeWriter` of
`src/eQuantic.UI.Codegen`, whose `BeginScope` makes an opener without its closer impossible to write.
Never strings concatenated across the code. Held by `GeneratedFilesEndTheirLinesTheSameWayTests`,
which reads the generators' source and refuses `StringBuilder` and `Environment.NewLine` there: it
refuses the mechanism, so an equivalent output written another way does not pass.

### 13. In preview, break freely and leave nothing behind

While the version carries a prerelease label, a signature widens and the old one goes: no
compatibility overload, no `[Obsolete]` forwarder, no member kept to avoid a break, and a refactor
finished in the same change rather than left as two halves. Breaking is free; hiding is not: every
break goes into the release notes and the wiki's Upgrading page. Held by `NoOldShapeSurvivesTests`,
the public API analyzers (RS0016, RS0017) and `EnumValueAbiTests`, which keeps the vocabulary's enums
append-only for a reason that is not compatibility.

At length: "NO MEMBER SURVIVES TO KEEP AN OLD SHAPE ALIVE" in [CLAUDE.md](../CLAUDE.md).

## How a gap is treated

### 14. A gap in the SDK is reported, never worked around

When the transpiler or the SDK cannot do what idiomatic C# asks, do not rewrite the C# to fit it:
the workaround fixes one file and hides the gap from everyone who meets it next. Write the C# you
would write without a transpiler; when it fails, isolate the smallest case, file it with what was
expected and what came out (the `EQ` diagnostic or the JavaScript emitted), and fix it in the SDK, or
leave the app waiting behind a fence that names the issue. The samples exist to find these: every
deviation from the handoff a sample cannot avoid is a gap, not a compromise.
