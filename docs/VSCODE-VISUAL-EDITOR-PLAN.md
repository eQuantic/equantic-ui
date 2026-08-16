# Track E — VS Code extension: the visual editor

> Plan doc for the track the ROADMAP opened on 2026-07-31. Started 2026-08-14.
> Every claim carrying a `file:line` was opened and read; every number was measured on this machine.

## What this is

A first-party VS Code extension that turns the SDK into a **visual** development environment: live
preview of a screen, click a rendered element to select the C# that produced it, a property panel
that edits that C#, and eventually insert/move/delete from a component palette.

Zero third-party by construction. The preview is the **real web realizer** running the **real
compiled module** in a webview — never a lookalike. Nothing new renders pixels.

---

## The premise the ROADMAP had, and what is actually true

The ROADMAP's Track E entry says click-to-select would work "via the V3 source maps eqc already
emits". **That path is dead**, and it was worth finding out before building on it.

The whole `Build` body is converted to one flat string and emitted through a single call —
`c.Raw(jsBody, component.BuildMethodNode.Body)` at `TypeScriptEmitter.cs:252` (and `:387`, `:949`).
So the finest position a source map can name is *the start of the method body*. Measured:
`__transpiled__/Badge.ts` line 26 is **942 characters**. Teaching ~150 conversion strategies to emit
newlines would take weeks and still only reach a statement, never an element.

Two other premises checked out and one more did not:

| Premise | Verdict |
|---|---|
| The compiler can be driven in-process, from a buffer | ✅ `ComponentCompiler.CompileSource(text, path)`, `ComponentCompiler.cs:148` |
| A live compile loop is fast enough | ✅ **measured p50 293 ms** on the 662-line `PaymentsPage`, warm |
| Source maps give element identity | ❌ dead, see above |
| Real screens are declarative trees a canvas can represent | ❌ **they are imperative** — see the open decision below |

### Real screens are imperative, and that caps what a canvas can do

| Screen | lines | `new X(` | `.Add(` | `children:` |
|---|---:|---:|---:|---:|
| `samples/WalletMobile/WalletApp.cs` | 686 | 208 | 126 | **0** |
| `samples/PhotonDesktop/Studio/Gallery.cs` | 1070 | 206 | 171 | **0** |

Repo-wide, only `samples/DefaultUIDashboard/Screens/DeclarativeScreen.cs` and two template files use
the declarative `children:` form the docs teach. The authoring surface the framework advertises is
**not** the one its own screens are written in.

This is a product question, not an implementation detail, and it is the one open decision on this
track — see [The open decision](#the-open-decision).

---

## Architecture

```
VS Code extension (TypeScript)              eqdesign (.NET, long-lived)
──────────────────────────────              ───────────────────────────
extension.ts   activation, commands  ──┐
preview.ts     webview + host doc      │   newline-delimited JSON over stdio
sidecar.ts     JSON-RPC client       ──┴──▶ Program.cs      protocol loop
project.ts     find csproj/refs/runtime    DesignSession.cs the project compilation
                                            └─ eQuantic.UI.Compiler (as a library)
```

**Why a sidecar and not `eqc`.** `eqc` reads files and `HotReloadService` watches the filesystem, so
neither can show the text you are currently looking at — only the last text you saved. `CompileSource`
takes a string, and `SemanticModelProvider.GetSemanticModel` swaps the buffer's tree in for the file's
own by path (`SemanticModelProvider.cs:84-93`), so an unsaved edit compiles against the real project:
its other types, its global usings, its generated sources, its exact MSBuild reference set. `eqc`'s
own `--watch` also builds its compilation once before the loop and never refreshes it.

**Why stdio.** No port to choose, nothing to authorise, nothing left listening if the window dies, and
it works unchanged over Remote-SSH because the extension host runs on the remote machine anyway. The
host's stdout is the protocol and nothing else — `Console.Out` is redirected to stderr at startup
(`Program.cs`), because one stray line of prose desynchronises the reader for the whole session.

**Measured, on `samples/DefaultUIDashboard`:**

| | |
|---|---|
| `initialize` (16 files, 316 references) | **271 ms**, once |
| `compile`, cold (first, JIT) | 2226 ms |
| `compile`, warm — edit loop, distinct buffer each time | **p50 293 ms** · min 206 · max 560 |
| `diagnose` (bind only, no transpile) | **36 ms** |
| syntax error → refusal with a mark | **36 ms** |

Two cadences follow from those numbers: `diagnose` on a 150 ms debounce (feels instant, fills the
Problems panel), `compile` on 400 ms.

---

## Phases

### Phase 0 — the landmines ✅ done

Small, boring, and everything after it is wrong without it.

- **`equantic.refs.txt` truncated to 0 bytes on every hot-reload save.** `CompileEQuanticUI` consumes
  `@(ReferencePathWithRefAssemblies)`, which is produced by `FindReferenceAssembliesForReferences` and
  **not** by `ResolveReferences`. In a full build the target runs after `Compile` so the item is
  populated; invoked on its own — exactly what `HotReloadService.cs:61` runs — it was empty, and
  `Overwrite="true"` truncated a good 316-line file. eqc then fell back to a bin scan, lost the
  semantic model, and **passed named arguments through in syntactic order**: a component rendered
  with its values in the wrong slots, with no error anywhere. Reproduced 316 lines → 0 bytes, fixed
  by the dependency, seat-belted by a non-empty condition, and eqc now **fails** (EQ0002) rather than
  degrading silently.
- **Converter state never dropped between files.** `ConversionContext._cache` is keyed by `SyntaxNode`,
  so each entry keeps a whole syntax tree reachable. Measured over 200 compiles: **38.4 KB retained
  per compile before, 4.7 KB after**. Invisible in a CLI that exits; not survivable in a host that
  stays up all day.
- **`CodeWriter.CurrentLine` counted one line per call**, whatever it was handed. It feeds
  `GeneratedLine` in the source map (`TypeScriptCodeBuilder.cs:80`), so any multi-line write shifted
  every mapping after it.
- **The extension's snippets taught an API that does not exist** — `Container`, `ComponentTree`,
  `Style { BackgroundColor = "white" }`: zero hits for any of them in `src/`. Rewritten against the
  real surface.

### Phase 1 — the design host and a live preview ✅ done

`src/eQuantic.UI.Design` (`eqdesign`) + `extensions/vscode`. Compiles the **unsaved buffer**, holds
the last good render when an edit does not compile, and reports C# errors as `vscode.Diagnostic`.

Proved end-to-end in a real browser, through the same code path the webview uses (compiled module →
blob → dynamic import → `materializeTheme` → `mount`):

- `DeclarativeScreen` mounts, paints with real atomic classes, and **is interactive** — clicking `Up`
  runs `SetState` through the reconciler and the text becomes `Count: 1`.
- `PaymentsPage` — 662 lines, imperative, with dependencies — renders **complete**: sidebar, KPI
  cards, bar chart, activity feed, 1965 DOM nodes, no console errors.

**Three real bugs surfaced by doing it**, all in the plain-JavaScript emission path that the
playground, the conformance harness and this host all consume (in TypeScript mode a stray annotation
is stripped by the bundler; in plain mode the browser rejects the module at parse time, so *nothing*
runs and the only symptom is an empty frame):

1. Lazy-static backing slots emitted `static _x: T | undefined;` — `Raw` output that never asked
   about `TypeAnnotations`. Seven sibling sites had the same defect (`abstract`/`declare` members,
   annotated getters and setters).
2. `abstract class X {` emitted for plain JavaScript (`TypeScriptCodeBuilder.cs:46`).
3. Hoisted pattern variables emitted `let x: any;` (`PatternVariableScanner.cs:34`). The flag now has
   **no default** at that call site, so the next caller has to decide rather than get it wrong quietly.

Guarded by `PlainJavaScriptSyntaxTests`, where the TypeScript-mode test asserts each path is actually
exercised — so no guard can pass vacuously.

**Not yet verified:** the extension running inside a real VS Code Extension Development Host. The
render path, the protocol and the latency are proven; the webview wiring is written and type-checks
but has not been launched.

### Phase 2 — `VisualNode.Origin` (the pivotal phase) ✅ done

Identity that is **exact**, not correlated, and target-neutral so native inherits it.

1. `VisualNode.Origin` — `string?`, beside `Key`. A string and not a file/span type: this layer speaks
   no target's language, and each realizer carries an opaque string into its own world.
2. `ComponentCompiler.DesignMode`, beside `TypeAnnotations`. The stamp is applied at ONE hook —
   `CSharpToJsConverter.ConvertExpression`, where every strategy's result comes back — restricted to
   *construction* expressions (`new X(…)`, `X(…)`) whose type derives from `VisualNode`. A reference
   is never stamped: `column` where it is merely mentioned would overwrite the construction's origin
   with the span of a use, and the editor would select the wrong line.
3. `$eq.origin(node, "path|startLine:startCol|endLine:endCol")` sets the field and returns the node,
   so the wrapper never changes what an expression means.
4. `WebRealizer.LowerNode` (C#) and `lowerNode` (TS) each attach `data-eq-origin` at their single
   dispatch — one place rather than thirty branches, and symmetric by construction.
5. Native gets it free: `LayoutNode.Source` is the `VisualNode`.

Loops need no special case: a row built five hundred times in a `foreach` carries the span of the one
expression that built it — which is the only editable thing anyway.

**Proved:**
- Every origin, applied back to the source text, lands on the construction it was stamped on
  (`DesignOriginTests`) — the contract click-to-select rests on, and the one that anything merely
  *correlated* would fail on the first loop.
- A `foreach`-built node is stamped with the loop body's expression, by line number.
- Both realizers pinned in their own words: `DesignOriginAttributeTests` (C#) and
  `design-origin.spec.ts` (TS) — same attribute name, same "only when set" rule.
- **Design mode off leaves production untouched**: a real SSR render of `PaymentsPage` contains
  **zero** `data-eq-origin`, and `ssr-hydration.spec.ts` — which fingerprints every element's sorted
  classes across SSR and hydration — is green.
- End to end in a browser: 7 of 13 elements of the rendered `DeclarativeScreen` carry an origin, and
  the spans resolve to the `Box(`, the `Column(` and each `Text(` that made them. The 6 without one
  are DOM the components generate internally — which is exactly why Phase 3 walks UP to the nearest
  origin-bearing ancestor rather than assuming 1:1.

**Found while doing it:** `ssr-hydration.spec.ts` had been failing since `ca34dfb` on an assertion
nothing had updated — `SegmentedControl` deliberately became a radiogroup (`role="radio"` +
`aria-checked`, one Tab stop), and the test still looked for `getByRole('button')` + `aria-pressed`.
An explicit role overrides the implicit one, so the locator matched nothing. Updated to the contract
the component actually promises.

### Phase 3 — click-to-select 🔄 core done, tiers ahead

`postMessage` bridge, gated to an **Inspect mode** rather than a modifier: the preview is a running
app, and a click that both pressed a button and moved the editor's cursor would be two gestures
wearing one costume. With inspect on, pointer events are taken in the capture phase and the app never
sees them. Hovering frames the element; clicking opens the file the origin names and puts the
selection on the exact expression.

**Proved in a browser**, over the rendered `PaymentsPage`: **356 simulated clicks** — each one a real
`elementFromPoint` hit at an element's centre, then the walk-up — **356 resolved**, **58 distinct
origins**. Twenty of those spans were applied back to the source and every one landed on a
construction: `new Button("Export", …)`, `new Text(day.Label, …)` from inside the chart's loop,
`HeaderTitle(theme)` and `KpiGrid(theme, columns: 4)` (helper calls that return nodes, stamped at the
call site), and spans in `ConsoleShell.cs` — **a different file from the one being previewed**, which
is the payoff of stamping at the construction: you land where the thing is actually written.

The walk-up is not optional: a `Button` is `button > div > div > span`, so the clicked node is
usually not the stamped one. 1:1 DOM↔node would select nothing most of the time.

Fixed on the way: an origin built from a relative source path came out relative, and `Uri.file` of a
relative path opens nothing. Origins are rooted at the stamp now, guarded by a test.

**Still ahead in this phase** — the tiers. Classify each node by asking Roslyn whether its origin has
a loop/conditional/lambda ancestor **within** the Build method:

| Tier | Canvas affordance |
|---|---|
| **Literal** — unconditional construction | select · inspect · edit · insert beside |
| **Derived** — inside a loop or conditional | select · inspect · edit the source expression · no structural insert |
| **Foreign** — another method or file | select · "defined in `EntryRow()` → jump" |

Without them, clicking a loop-generated row still sends you to the right line — it just does not say
*why* three hundred rows share one. The tiers are what stop a later phase from offering to "delete
this row" when there is no such thing to delete.

### Phase 4 — read-only inspector ✅ done, by a different route

The plan here was a generated `components.manifest.json`. **The inspector does not use one**, and the
reason is worth recording: a catalogue answers *"what does Row have"*, and the panel needs *"what does
**this** Row say, and what may I change"*. Those are different questions, and only the second is
useful next to a selected node. So `inspect` reads the **semantic model** at the origin's span:

- the constructed type, and whether the call is written as a factory or as `new`;
- each constructor parameter, whether this call supplies it, and the argument **as written** —
  matched by NAME first, because the declarative surface is written with named arguments and their
  order is the author's, not the signature's;
- every settable member **including inherited ones** — a `Row`'s `Width` lives on `FlexNode` and its
  `Key` on `VisualNode`, and asking only the type itself listed none of what an author reaches for;
- enum members as a closed set, so the panel offers a list rather than a text box;
- the doc prose, off the symbol.

And the honest half: an init-only member on a factory call is reported **unreachable, with the
reason** — it needs an object initializer, which the factory surface exists so nobody writes.
Rewriting `Row(…)` into `new Row(…) { … }` behind the author's back is the thing this refuses to do.

**Two corrections this phase forced**, both of which had been written down confidently and wrongly:

1. `GenerateDocumentationFile` **was** needed. The earlier reasoning — that the SDK hands the compiler
   the component library as source, so prose is in the tree — is true for eqc and false for the design
   host, which deliberately does not add component sources (they are already in the reference set, and
   adding both defines every type twice). Framework symbols arrive as metadata, so their comments come
   from XML or not at all. It is on now, with CS1591 off.
2. Turning it on was **not enough**. MSBuild hands over REF assemblies (`obj/…/ref/X.dll`) and the
   documentation is written a directory above them, so Roslyn looked beside the file it was given,
   found nothing, and every framework symbol came back undocumented. The host re-attaches each
   reference's XML by hand.

**Proved:** 15 tests over a real `Initialize` — a temp project, a written reference list, a live
compilation. They pin the argument-as-written, the unset-but-reachable parameter, the enum's members,
the inherited members, the unreachable init-only member *with its reason*, and that a framework
component arrives documented (which is what pins the XML discovery).

**Still ahead**, and correctly belonging to the palette rather than here: the generated manifest of
every component (the palette needs to list what does not exist on screen yet), categories seeded from
`Gallery.cs:44-80`, and the **22 components with no factory** (Accordion, AppBar, BottomSheet,
Breadcrumb, CodeBlock, CodeEditor, DataTable, FormInput, FormSubmit, List, ListItem, ListView, Menu,
PageIndicator, Pagination, Popover, PullToRefresh, RadioGroup, SegmentedControl, Spreadsheet,
SwipeableRow, Table) — so the palette does not emit two insertion forms depending on what you drag.

One gap worth naming: the framework's **factories document no parameters**. The prose plumbing works
(188 `<param>` comments exist, mostly on records), so writing them on `UI.cs` would light up the
inspector immediately at no cost to this code.

### Phase 5 — property write-back, fenced ✅ done

The host computes the edit; the **editor applies it**, as one `WorkspaceEdit` per gesture. That split
is the whole design: the edit lands in the document's own undo stack, so one Ctrl+Z reverses it, and
an unsaved buffer stays unsaved. A host writing the file itself would be fighting the editor for the
same document and would win in the worst way. Never `fs.writeFile`.

Three shapes, and deliberately nothing else:

1. **Replace an argument already written** — just its expression, so trivia and the name colon survive.
2. **Add a parameter the call omitted**, as a NAMED argument placed before a trailing `children:` so
   the tree stays last, which is how every screen in the repo reads.
3. **Set a member of the value an argument carries** — `style.Padding`. This one is not a nicety: the
   things an author reaches for (padding, background, corner radius) are on a `BoxStyle`, which is
   *data rather than tree* and so carries no origin — a click on a Box resolves to the Box. Without
   descending one level the panel could offer a Box's `gap` and nothing anyone came for. And
   `BoxStyle` is a `readonly record struct` with no positional parameters, so every member of it is a
   plain add-or-replace with no ordinal arithmetic anywhere.

**Two gates before an edit is even offered:** the value must parse as a C# expression, and re-parsing
the whole file with the change applied must introduce no error the file did not already have. The
second compares a MULTISET of (code, message) rather than a count or a position — an edit shifts
every line after it, and a file with a pre-existing error would otherwise have that error handed back
as though this edit caused it. A panel that writes a broken file and lets the preview report it has
still broken the file.

**No form transformation.** Setting `Width` on `Row(gap: …)` needs an object initializer the call does
not have, and adding one rewrites `Row(…)` into `new Row(…) { … }` — the authoring form the whole
factory surface exists to avoid. The panel says so, with the reason, and does not do it.

Values are asked for through **VS Code's own input**: an enum becomes a quick pick of its members
*qualified* (`CrossAlign.Center`, never `Center` — what the panel offers is what gets written), and
anything else an input box seeded with the current text. Keyboard-navigable and screen-reader-correct
for free, which a hand-rolled webview field is not.

**Exit, proved:** setting every written argument to the value it already has returns the file
**byte-identical**. That is the guard the phase needs — a span off by one character passes every other
test here and quietly eats a bracket. 23 tests in all, over a real `Initialize` with a live
compilation.

### Phase 6 — insert from the palette, fenced ✅ done

Insert only into a `children: [...]` collection expression — the one structurally uniform slot.
Refuse imperative `.Add()` bodies with a clear message rather than corrupting a method.

### Phase 7 — the canvas acts ✅ done

The panel could already insert, move and remove; the pointer could only select. That split is
backwards — the canvas is where the tree is legible, and reaching to a list at the bottom of the
screen to move the row under your hand is the gesture a visual editor exists to remove.

**Drag to reorder.** A drag reports the GAP it was dropped into; the host turns that into the position
the node ends up at, which is one less whenever it travelled downwards. That arithmetic lives in C#
on purpose: it is the one subtle step, and in a pointer handler it could only ever be found by
dragging things about. `ReorderChild` is the general form and `MoveChild` is now a special case of it,
so one drag across four positions is **one** edit and **one** undo.

**Insert where the pointer is.** Hovering a child offers a `+` at each end of it, along the
container's own axis. Which axis is read off the RESULT rather than the CSS — a Row is flex, a Grid is
grid, a Stack is absolute, and once on screen all three answer "are these side by side?" the same way.

Both stand on one new idea: the canvas **caches what the host knows about each origin**, filled by a
question asked when the pointer settles (120 ms) rather than on every move. Cleared on every
recompile, because an origin is a span in the text.

Two refusals are drawn before the gesture, never after it:

- The screen and the list must line up child for child. A child that renders as several elements, or
  as none, breaks the correspondence, so an index computed from pixels would name a different element
  in the file — the affordance is **withheld** rather than guessed.
- A drop outside the container says *"outside its list"* and does nothing. Moving a node to a
  different parent is a remove and an insert of its own text, which is a bigger edit than a reorder.

And one bug this closed on the way: an origin is a span, so a move invalidates the one the panel was
holding — asking about it afterwards described **the sibling it had just been swapped with**. Edits
that move a node now answer with where it landed, and the selection follows it there.

### Phase 8 — out of one container and into another ✅ done

A reorder cannot express this: the node leaves one list and joins a different one, which is a removal
**and** an insertion. Written as ONE replacement spanning both — not because the text between them is
interesting, but because one span is one `WorkspaceEdit` and therefore one Ctrl+Z, and because two
edits into the same document raise an ordering question this simply does not have.

The drop target is now recomputed from the pointer on every move rather than fixed when the drag
starts, so the caret follows into whatever list it is over. Refused, visibly, for: a node into its own
subtree, a container that takes no list, a list in another file (that would edit two documents at
once), and the list it is already in — which is a reorder.

**What may be carried across is decided by the compiler, not by a rule.** The moved text is
re-indented to its new depth and otherwise moved verbatim, so a node written against a local of the
method it came from stops compiling in its new home — and `Guarded` refuses with the compiler's own
sentence (`CS0103 The name 'label' does not exist…`). There is no list anywhere of what is portable.

Three defects fell out of writing the tests, and two were live:

- **`RemoveChild` broke a list with one child.** Every list in this repo is written with a trailing
  comma, so taking the element alone left `[ , ]`, which does not parse. The existing test emptied a
  slot in a list of four, where the comma *before* it is taken instead — so the bug sat behind the
  panel's own remove button.
- **`Locate` parses a fresh tree per call**, so resolving two origins gave two nodes describing the
  same text that were not each other. "Is this the list it is already in?" answered no, and the move
  wrote a duplicate. Both ends now resolve in one tree.
- The compiler's real refusal was being masked by the first bug's syntax error.

### Phase 9 — the mark and the palette ✅ done

Edgar's ask: a mark showing where something can be inserted, and a `+` that opens the choice of every
component available — the framework's **and** the developer's own.

The canvas now answers "where could something go" without anyone guessing. Over a child of a list it
offers a `+` at each end of it (before this one / after this one); over a container's own space,
including an **empty** one, the gap nearest the pointer is marked with a dashed line and a single `+`.
The dash is deliberate: the caret marks a commitment, the dash marks a possibility, and the eye should
not need a label to tell them apart. Both share one geometry function, because the pair has to line up
exactly and two of them would drift.

**And the palette was only half a palette.** It scanned `eQuantic.UI.Components.UI` and nothing else,
so a project's own components — the ones the developer wrote this morning — were not offered at all.
They are generated into an `AppUI` surface in the shortest namespace the app's components share, which
cannot be looked up by a fixed metadata name; the compilation's own assembly is walked for it instead.
The app's entries come first, under their own heading, because a flat list buries them among a hundred
framework names.

The test for it failed on the first run for the right reason: the probe declared an `AppUI` but not
the `global using static` the generator writes beside it, so the palette offered a component that did
not compile where it landed — and the guard that inserts every entry into a real list caught it. The
probe was wrong, not the palette, but the guard proved it is the palette that would have been blamed.

### Phase 10 — the other lists, and the values inside a property ✅ done

Two fences that turned out to be around words rather than around anything real.

**A list is a list.** A `Grid`'s `columns` is a collection expression whose elements have real spans,
exactly like a `children`. So is a menu's `items` and a dialog's `actions`. Everything built for
children now works on any of them: `inspect` reports every list a call is written with, and insert and
remove take the name. What differs is what goes IN them — a `GridTrack` is data, it never renders, so
nothing on the canvas carries its span and a palette of components has nothing to offer. Both follow
from reading the signature: a data list is addressed by INDEX from the panel, and its palette is the
element type's own ways of being written (`GridTrack.Flex()`, `GridTrack.Auto`, target-typed `new(…)`
— the framework gives value records no factory on purpose).

One defect fell out: `Addition` always broke the line. `columns: [Flex(), Fixed(120)]` is written on
one line and reads that way, and adding a track reformatted the author's file to make room for a tool.
A list written on one line stays on one line.

**A value is not a string.** `BoxStyle` reached the panel as one long line of C# in a single cell —
the thing an author most wants to change, in the one place they could not change it. `inspect` now
reports the members of a value written as `new T { … }` as ordinary properties: name, type, what is
written there, what it may be. The panel renders them as a small sheet under the row that carries
them, keys on the left and a select of everything not set yet on the last line. `UnsetProperty` takes
a member back out, because a sheet you can only add rows to leaks.

### Phase 11 — a pile is not a row ✅ done

The last container whose gestures were lying. A `Stack`'s children overlap, and spec A3 makes **paint
order the child order** — so its list is meaningful and its geometry is not. Between two boxes sitting
on top of each other there is no gap for a caret to sit in, "3 of 5" counts something nobody can see,
and "move up" means "send backward".

The canvas cannot work this out: two overlapping children look exactly like two children nobody has
laid out. So the host says it — `IsLayered` resolves `eQuantic.UI.Primitives.Stack` through the
compilation and walks the base chain, which fails safe to "no" rather than matching a name in the
author's file. `inspect` reports it twice over: on each list (*this one's order is depth*) and on the
node itself (*your position here is your depth*).

With that known, everything else is presentation:

- The drop mark becomes a **cover** over the child the node would come to rest in front of, rather
  than a line between two that do not have a between.
- The slot is read as "the topmost child under the pointer, plus one" — over the stack's own space,
  the top of the pile.
- The badge says *in front of Card* rather than *3 of 5*.
- The panel's arrows keep their gesture and stop mis-naming it: **Send backward** and **Bring
  forward**, in the tooltip and in the accessible name.

### Phase 12 — duplicate, and a keyboard ✅ done

Two gaps that had nothing to do with the hard parts, and everything to do with whether this is
pleasant to use.

**Duplicate** is the commonest gesture in any editor and had no answer here: a second row like the
first meant picking one out of the palette and setting every property again. The element's text is
copied verbatim and lands immediately after it, and the edit answers with the COPY's origin — what
stays selected is the thing just made, not the thing copied.

**The keyboard** turns every existing gesture into a key rather than adding a second implementation of
it. Arrows WALK the tree the author wrote (up to the parent, down to the first child, left and right
along the siblings), Alt+Arrows move the node instead, Delete removes it, Cmd/Ctrl+D duplicates it,
and Escape lets go — first of the selection, then of the mode. All of it respects the same guards the
pointer does: nothing structural while the tree is settling, and nothing at all when the pointer is
over the panel.

### Phase 13 — installable ✅ done

Until now the extension resolved the design host out of the repository's own build output, so it
worked for whoever had cloned the repository and for nobody else. `npm run package` produces a
`.vsix`: **10.7 MB**, 158 files, with the host published into it.

Framework-dependent on purpose. A self-contained publish is ~70 MB per platform and would have to ship
three of them, and this is a tool for people writing a .NET application — the one dependency it assumes
is the one they are already using.

Two things that would otherwise be found by a user rather than by us:

- **The host is checked into the archive, and then RUN.** `verify-package.mjs` reads the entry list out
  of the zip itself (its central directory, thirty lines, no dependency) rather than asking the tool
  that wrote it what it thinks it wrote — then spawns the packaged host and speaks its protocol,
  asserting an unknown method comes back as an error for the right id. A `.vscodeignore` that drops the
  host produces an extension that installs, activates, and fails on the first preview.
- **In development the repo's build wins.** Packaging leaves a Release host in `host/`, and preferring
  it afterwards would run yesterday's host against today's source — silently, until something the
  protocol has gained since is asked for. `ExtensionMode.Development` decides.

CI packages it on every push and keeps the `.vsix` as an artifact.

The readme is what the Marketplace shows, and it still said "simple extension providing snippets" —
rewritten, and its claims checked against the manifest, which caught it advertising an `eq-style`
snippet that does not exist.

### Phase 14 — the screen as a list ✅ done

The canvas can only offer what is under the pointer, and on a full screen there are nodes that are
hard to hit and nodes that are impossible — a container filled edge to edge by its own children has no
pixel of its own. Every editor worth comparing to has a layers list for exactly that.

It needed **nothing new from the design host**. The rendered DOM already carries an origin and a
component name on every node, which IS a layers list: the canvas walks it on every render and posts it
up, and a `TreeDataProvider` turns that into a tree. Each row says where it is written —
`PaymentsPage.cs:28` — which is the thing a list can say that a picture cannot.

Both directions go through the door that already exists. Picking a row posts a `reveal` to the canvas,
which selects it exactly as a click does, so the outline, the panel and the editor's cursor all follow
from one path rather than three. Selecting on the canvas marks the row.

It lives in the **Explorer**, beside the Outline and the Timeline, rather than in a container of its
own: a container wants a monochrome 24×24 icon, the logo is a colour mark that would be a blue smudge
at that size, and inventing branding is not a thing to do quietly.

Two tests, and the first is the cheap one that matters: VS Code registers `<viewId>.focus` for every
contributed view, so focusing it fails when the view is not in the manifest or its container is
misspelled — which otherwise shows up as an empty sidebar nobody can explain. The second waits for the
list to fill from a real render, through a small API the extension exports for no other reason.

### Phase 15 — a toolbar of its own, and a format ✅ done

Edgar, on the title-bar buttons: a toolbar of our own would give room for more controls and for
formats. He is right, and the reason is structural — VS Code's title bar holds COMMANDS. A mode can be
faked there with two commands that swap places, which is what inspect was doing; a format selector is
a control with state and a list, and the trick stops working at the third format. So the mode moved
into a toolbar built from the editor's own variables, pressed state and all, and the title bar keeps
the commands for the palette and for keybindings.

**The format selector, and what a format actually is.** The framework has exactly two responsive axes,
and neither is a device: `WindowSizeClass { Compact, Medium, Expanded }` at 600dp and 840dp, and
`Density { Comfortable, Compact }` — which the spec calls a property of the TARGET, never of the call
site. So the presets are named after devices for the reader, and what they apply is those two, which
is what the bar says: *compact width · comfortable density · web realizer*.

Both had to be applied properly, and one of them is the whole reason a device frame could have been a
lie:

- **Density is read while a component BUILDS**, so it is baked into the tree and no stylesheet can
  change it — a format change re-mounts the last frame.
- **The adaptive gates are viewport media queries.** An `AdaptiveNode` emits every variant it declares,
  each wrapped in a gate whose rules key off the WINDOW — so a 390px box inside a wide panel still
  shows the expanded variant. A phone shell around that would hide the one design deviation it
  exists to reveal. The gate's range is in its NAME (`eq-vc600`, `eq-vm600-840`, `eq-vx1024`), so the
  same decision is made against the chosen width and written into a sheet that comes later and wins.

What the frame is NOT is a native render: there is no way to put Photon's own raster in a webview yet.
It is the web realizer showing the same component tree at that size, with the size class and density
that shape of target would have — which, given the SDK's promise, is the same tree the native target
builds. The bar says "web realizer" for exactly that reason.

### Phase 16 — a native project previews too ✅ done

Edgar asked for the preview to recognise whether a project is web or native. Detection alone would
have decorated a lie: a NATIVE project could not preview at all, and its refusal — "build the project
once" — was an instruction no amount of building could satisfy, because the native SDK never wrote
`equantic.refs.txt` and a native project has no `wwwroot/runtime.js`.

Three small pieces made it true instead:

- **The native SDK writes the reference list** (`EQuanticDesignRefs`, for tooling — it still never
  runs eqc). Same rules the web SDK learned the hard way: the item comes from
  `FindReferenceAssembliesForReferences`, declared as a dependency, and an empty set is never written
  over a good one.
- **The extension bundles the browser runtime as a fallback.** A web project's own copy always wins
  (it matches the project's SDK version); the bundled one exists for projects that legitimately have
  none. Dev mode prefers the repository's build, same as the design host.
- **`detectTarget` reads the .csproj text** — the SDK attribute, the `<Sdk>` element, or the local
  `Sdk.props` import. Native is tested FIRST, because `eQuantic.UI.Sdk.Native` contains
  `eQuantic.UI.Sdk` — the same prefix trap that once put the extension's release tag through the
  framework's pipeline.

The toolbar says what it is looking at — *native project · compact width · comfortable density · web
realizer* — and a native project opens in the phone shell, because that is the shape it ships in. A
default, not a cage: once a hand touches the selector, the project stops having an opinion.

Proof, end to end: the design host initialized against WalletMobile's new reference list (205 refs,
14 files) and compiled the real 686-line imperative `WalletApp` to 55 KB of JavaScript, zero marks.

### Phase 17 — state survives the recompile ✅ done

Every recompile remounted fresh, so a counter clicked to 7 went back to 0 on the next keystroke — the
single most irritating thing about actually using the preview. The fix rides the framework's own
mechanic: capture the page's fields, remount, and hand them back through `window.__INITIAL_STATE__`,
the same door the SSR handoff and the framework's hot reload already use. Guarded by class name the
way the boot guards by URL, so a renamed page starts fresh instead of inheriting a stranger's fields.

The proof falsified the first TWO implementations before passing, which is why it exists:

- Copying the boot's capture verbatim (`_state` bag only) carried **nothing** — a write-once page
  keeps no `_state` bag; its C# fields compile to plain instance fields. The framework's own hot
  reload shares this gap.
- Probing `runtime.StatefulComponent` for "framework keys" probed the WRONG class — the compiled page
  extends the *shared* base — and let `_lifecycleMounted: true` through, which silences a fresh
  instance's `OnMount`.

The capture that ships probes **the instance's own prototype chain**: a sacrificial instance of the
page's direct base, constructed AND mounted (half the framework's keys only appear at mount), says
which keys are the framework's; everything else on the instance is the author's. Proven in a real
Chromium through the exact blob-import path the webview uses: click to 2 → remount with carry → still
2, and the capture is exactly `{"_count":2}`; remount without carry → 0, so the assertion cannot pass
on persistence the component had on its own.

---

## The seam, tested

Three defects in a row reached the user rather than a test, and all three lived in the seam with the
editor: a message posted before the webview was listening, a template literal eating its escapes, and
a callback touching a `const` the constructor it ran inside had not finished assigning. None can fail
a unit test, because none lives on the side a unit test can reach.

`npm test` in `extensions/vscode` now downloads a VS Code, loads the extension into it on the
dashboard sample, and drives it. One dev dependency — the editor's own `@vscode/test-electron` — and
**no test framework**: the harness asks for a module exporting `run()` that rejects on failure, and
`node:assert` covers that.

Six checks, and the third is most of the value: `openPreview` awaits the design host's `initialize`,
so a panel existing afterwards means the host started, read the reference list and built the project
compilation — and then awaits the first render, which waits on the webview announcing itself. **A
broken handshake does not fail there, it hangs**, so each test is bounded and a hang reports as *"it
did not fail, it never answered"*.

Verified by putting each bug back: the dead-zone one fails two checks with "no preview panel is
open", and the lost handshake times out.

Two things the harness needed that are worth knowing. It still composes
`Contents/MacOS/Electron`, which VS Code has since renamed to `Code`, so the executable is looked for
where it actually is. And VS Code opens a Unix socket inside its user-data directory, which caps at
103 characters — left under the repo it overflows and the editor dies at startup with `EINVAL`, so
the tests name a short one.

## The lesson the probes could not teach

A node that is a call's **only argument** — `card.Add(new FormInput(…))`, which is how imperative code
adds children and therefore how most of a real screen is written — resolved to the *call* rather than
to the node. The panel introduced a `FormInput` as **`Void`** and offered to edit `child`, which is
`Add`'s parameter. Roslyn's `FindNode` returns the outermost of a span tie unless asked for the
innermost, and an `ArgumentSyntax` has exactly the span of the construction inside it.

Every hand-written probe missed it, and not by accident: they were declarative, and a declarative
container passes its children through a *named* argument, whose span differs. **A probe inherits the
assumptions of whoever wrote it.**

So the standing guard is a sweep over the REAL screens (`RealScreensTests`): compile each one in
design mode, take every stamp the compiler emitted, and ask the host to resolve it. It is a
cross-examination rather than a smoke test — the compiler labels the node at emission, the host
resolves it from the span afterwards, and **two independent answers to the same question have to
agree**. Over 200 nodes across the sample's screens. Reverting the fix makes it name the file, the
line and the disagreement.

## The decision — settled 2026-08-14

**Declarative is the preferred form, and the fence stays there. But UNDERSTANDING is not fenced.**
The editor opens pre-existing code, and pre-existing code is elaborate: helper methods, and whole
screens split across files. So reading, selecting, inspecting and property-editing work everywhere,
and only *structural insertion* is held to `children: [...]`.

That distinction turned out to be the difference between a tool that works on this codebase and one
that does not, because "elsewhere" is where most of a real screen lives — the classifier reports
almost everything on `PaymentsPage` as `foreign`.

What it took (done):

- **The origin decides the file, not the panel.** Inspect and edit used to refuse any origin outside
  the previewed buffer, which is most of what you can click on a composed screen. They now answer in
  whatever file the node was written in, and a property edit lands in that file's document.
- **Any project `.cs` repaints the preview**, not just the previewed one. A screen is composed of
  several files, and editing the shell beside it has to repaint the same.
- **Unsaved neighbours are the truth.** The host takes the editor's open buffers and rebuilds the
  compilation on top of them, so the semantic model agrees with what the author sees across every open
  file. Dependencies compiled from an open buffer are never cached by write time — their text changes
  without the file being touched, which is precisely the case a write-time cache answers wrongly and
  never notices. Close without saving and the overlay forgets it.

Structural insertion is still the one fenced gesture, and the fence now has a reason rather than a
shrug: inserting into an imperative `.Add()` body is statement-level dataflow — which local is the
parent, which statements mutate it, whether the insertion crosses a declaration it depends on — and
getting it wrong corrupts a file rather than annoying someone. A palette that refuses with
*"this container is built imperatively"* is honest; one that guesses is not.

---

## Fences for v1

| Fence | Why |
|---|---|
| No structural editing of imperative screens | Statement-level dataflow; 85–90 % of real code, so wrong is catastrophic rather than annoying |
| No factory ⇄ `new`+initializer transformation | Changes the authoring form the factory surface exists to avoid — a product decision, not the editor's |
| No native/Photon preview | Identity is already solved there; only transport is missing, so nothing is foreclosed |
| No CSS/style panel | Style is typed `BoxStyle` with tokens; a CSS panel would teach the wrong model and never round-trip to native |
| No raw colour picker | Trees hold **tokens**, never resolved colours — that is the theming invariant |
| No framework `CodeEditor` in the webview | It has no IME/composition events, no paste handler and no SSR path. Use VS Code's own editor |
| No `[ServerAction]` execution in preview | Action ids come from a startup assembly scan, so an ad-hoc preview class is never registered |
| No marketplace publish yet | Needs a version-derivation decision and a publisher identity; ship the `.vsix` as a release asset |

## Known limitation

The preview renders under the framework **baseline** theme (`MaterialTheme.Instance`). An app picks
its theme by calling `AddUI(…).UseTheme(…)` at startup, and reading that back means running the app's
composition root. Shapes and layout are exact; a rebranded palette is not yet reflected.
