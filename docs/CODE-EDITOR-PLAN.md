# The code editor — an IDE's editor, written once

## Why a plan, and why now

`eQuantic.Code` (`../equantic-code`) is a native IDE built on the SDK, and its editor is ours:
`CodeBlock` to read code, `CodeEditor` to write it, one engine under both. The IDE can only be as
good as that component, and the bar is stated: **the editor an IDE is built on — IntelliSense,
completion, signature help, hover, diagnostics, go-to-definition and the jumps between places —
at the level of VS Code and past it where the architecture lets us go further**.

Before anything was added, the existing editor was driven for real: in a browser (the dashboard's
`/code` screen) and through the Photon host in tests. It is a good model with a thin, duplicated
shell around it, and the shell is where it breaks — eighteen defects found below, most of them
in code that exists twice, once per host. So the plan starts with the structure that removes the
copies, then fixes what is broken on top of it, and only then builds the intelligence.

Two decisions taken with Edgar (2026-09-23) shape everything else:

- **The engine gets an assembly of its own**, `eQuantic.UI.Code`, between `Primitives` and
  `Components`. `CodeSurface` stays in the vocabulary and depends only on an interface that a
  realizer reads and drives. This closes the question `ARCHITECTURE-AUDIT.md` §4 left open (27% of
  the vocabulary assembly was two editors' models) for the code editor.
- **C# intelligence comes from Roslyn, in an opt-in package**, `eQuantic.UI.Code.CSharp`. The IDE
  and the playground consume the same one; the SDK core only defines the contracts.

## What it has to beat — the parity bar

The union of what VS Code (Monaco) and JetBrains Rider offer in the editor itself, against the
slice that delivers it. "Further" is where the design is meant to be better, not merely equal.

| capability | VS Code | Rider | slice | further |
|---|---|---|---|---|
| Typing, IME, dead keys, emoji, paste, AltGr | ✓ | ✓ | 1 | |
| Selection by click, double/triple click, drag, shift-click | ✓ | ✓ | 0 | one pointer map for every host |
| Keyboard conventions per platform (⌥/⌘ on Apple, Ctrl elsewhere) | ✓ | ✓ | 1 | |
| Custom key bindings, commands callable from menus and palettes | ✓ | ✓ | 2 | the keymap is data, typed |
| Tabs, wide characters, grapheme-safe caret | ✓ | ✓ | 1 | |
| Line operations (move, copy, delete, join, insert above/below) | ✓ | ✓ | 2 | |
| Multiple cursors, add next occurrence, column selection | ✓ | ✓ | 2 | |
| Snippets with tab stops, placeholders, choices, mirrors | ✓ | ✓ | 2 | |
| Find / replace — case, whole word, regex, in selection | ✓ | ✓ | 1 (find) / 6 (regex) | |
| Completion — triggers, fuzzy filter, ranking, details, docs | ✓ | ✓ | 3 | frecency + expected type + provider rank, filtered locally per keystroke |
| Signature help — overloads, active parameter | ✓ | ✓ | 4 | |
| Hover — signature, documentation, diagnostics | ✓ | ✓ | 4 | |
| Diagnostics — squiggles, gutter, problems navigation | ✓ | ✓ | 4 | ranges travel with edits until the provider answers again |
| Go to definition / declaration / type / implementation, ⌘-click | ✓ | ✓ | 5 | |
| Find references, peek | ✓ | ✓ | 5 | |
| Navigation history (back/forward), go to line, go to symbol | ✓ | ✓ | 5 | a jump list with meaning: only significant moves are recorded |
| Document highlights, rename, code actions, formatting | ✓ | ✓ | 6 | |
| Semantic highlighting, inlay hints | ✓ | ✓ | 6 | |
| Folding, indent guides, bracket pair colours, sticky scroll | ✓ | ✓ | 6 | |
| Inline (ghost text) suggestions, partial accept | ✓ | ✓ | 8 | the same session as completion, so the two never fight |
| Diff — side by side and inline, word-level changes, collapsed unchanged regions, change navigation | ✓ | ✓ | 2b | the same engine and highlighter as the editor, so a diff pane IS an editor: its modified side edits, and every language colours in it |
| C# intelligence | extension, out of process | in process | 7 | in process on native: no JSON-RPC hop between a keystroke and Roslyn |
| One editor on the web and in a native window | — | — | all | write-once, the same engine and the same tests on both |

## What is broken today — measured (2026-09-23)

Lines 1–14 were reproduced before they were written down, and 16–18 were found while proving slice 0 in the browser: in Chromium through the dashboard's
`/code` screen, or through `PhotonHost` in a test. Line 15 was found by reading the model; its slice
reproduces each one in a test before fixing it. The slice is where it is fixed.

| # | defect | where | evidence | slice |
|---|---|---|---|---|
| 1 | Text that arrives as input rather than as a key — a dead key (´ + a), an IME, an emoji picker, dictation, autocorrect — never reaches the document | web | the surface only listens to `keydown`; `beforeinput`/`insertText` events arrive and nothing handles them | 1 |
| 2 | Typing `{ [ @` with AltGr on a European layout on Windows is dropped | web | `event.key.length === 1 && !event.ctrlKey` — AltGr is Ctrl+Alt | 1 |
| 3 | Pasting from outside the editor is impossible | web | ⌘V is claimed and `preventDefault`ed, so the browser never fires `paste`; the clipboard read is the editor's own buffer | 1 |
| 4 | `MaxHeight` does not scroll: the wheel does nothing, the line window never narrows | web | the vertical `ScrollView` grew to 2827px inside the 520px box (`height:100%` of an indefinite height); only `scrollIntoView` moved the clipped box. Photon honours the cap — a realizer disagreement | 1 |
| 5 | No drag selection, no shift-click | web; shift-click on Photon too | no `pointermove` handler; both hosts set the caret and ignore Shift | 0 |
| 6 | Escape does not release the Tab trap | web | the handoff says Esc releases it; the web surface does not claim Escape and keeps focus | 1 |
| 7 | The caret leaves the viewport under the arrows and nothing follows it | Photon | 40 × ArrowDown in a 200dp editor: caret at y=732, viewport 0–200, offset unchanged | 1 |
| 8 | ⌘F kills the editor: the find field never gets focus, typing goes nowhere | Photon | opening find wraps the surface in a `Stack`, so its PATH changes; the host's `_textPath` points at nothing, and autofocus is refused because something is "already focused" | 1 |
| 9 | Opening find resets the scroll; Escape does not close it; "next" does not scroll to the match and does not raise `OnSelectionChanged` | both | the same tree-shape change remounts the surface; the bar has no Escape; `Step` sets `Selection` inside `SetState` only | 1 |
| 10 | `CodeEditor.Caption` never appears | both | it is handed to a non-standalone `CodeBlock`, which draws the caption only when standalone | 1 |
| 11 | Without `MaxHeight` — how the IDE uses it — every keystroke builds every line | both | 3000-line file on Photon: 100–213 ms per keystroke, against 1–3 ms with a window | 1 |
| 12 | A tab or a wide character puts the caret beside the wrong glyph; Backspace on an emoji leaves half of it | both | the model counts one column per UTF-16 unit, the browser draws a tab to the next 8-column stop; `DeleteBackward` removes one code unit | 1 |
| 13 | On Windows and Linux, Ctrl+← goes to the start of the line | both | the keymap is Apple's: `Command` (= Ctrl there) + ← is `LineBoundary` | 1 |
| 14 | The server renders nothing where a code editor goes | web SSR | `WebLoweringVisitor.Visit(CodeSurface) => null` — `ARCHITECTURE-AUDIT.md` §2, still open | 1 |
| 15 | Model defects: Tab over several lines restores the wrong selection; ⌘/ drops the selection; `}` never outdents although `OutdentOn` says it does; a whole-line copy pastes mid-line; C# raw strings (`"""`) are not coloured | both | read in `CodeEditorController` and the C# tokenizer | 1 |
| 16 | A double or triple click never selected the word or the line | web | Chrome reports `detail: 0` on every `pointerdown`; the click count exists only on `mousedown` | 0 |
| 17 | A press in the editor scrolled the page, so the second click of a double click landed on another line | web | `focus()` scrolls the focused element into view by default: 78px measured on the first click | 0 |
| 18 | The selection and the caret vanish whenever the code carries a decoration — which bracket matching, on by default, adds as soon as the caret touches a bracket | web; Photon likely | the block wraps its lines in a `Stack` whose layer takes `z-index: 2`, painting over the surface's marks (measured on the band over `Invoice(`); Photon draws the marks BEFORE the child, under the active line's opaque wash. The fix is an explicit paint order on both hosts — washes, then selection, then text, then caret — with a draw-order test | 1 |

## The shape, decided

### 1. Three assemblies, and one protocol between the engine and the hosts

```
eQuantic.UI.Primitives   CodeSurface + ICodeSurfaceModel — what a realizer reads and drives
        ▲
eQuantic.UI.Code         the ENGINE: document, selections, history, languages, highlighter,
        ▲                keymap and commands, pointer map, view model, intelligence sessions
eQuantic.UI.Components   CodeBlock, CodeEditor, and the widgets over them (completion, hover…)

eQuantic.UI.Code.CSharp  (opt-in) Roslyn behind the engine's provider contracts
```

*How does Flutter solve it?* `EditableText` (widgets) owns the editing protocol, `RenderEditable`
(rendering) paints what it is told, and `TextInputClient` is the narrow door the platform talks
through. Ours takes the same road: the vocabulary node depends on `ICodeSurfaceModel`, and a realizer
may do exactly two things with it — hand it what the platform said (a key, text, a composition, a
pointer, focus) and paint what it answers (caret and selection rectangles, in the surface's own
coordinates, plus a reveal request). **No host does column arithmetic or decides what a click
means.** Today each host carries its own copy of both — `lowering.ts` and `PhotonHost` each turn
(line, column) into pixels and each decide what a double click selects — which is why defects 5, 7,
12 and 13 exist on one side and not the other.

The engine is transpiled into the runtime like the rest of the shared model, so the web and the
window run the same C#. The node is still a leaf of the vocabulary; the engine is a library that
knows nothing about any host.

### 2. Keys are commands, and the keymap is data

A key resolves to a COMMAND (`CodeCommand`), and the command is what runs. The keymap is a table
from chords to commands, with one layout per keyboard convention — Apple (⌥ words, ⌘ line ends) and
Standard (Ctrl words, Home/End line ends) — which is what defect 13 needs. The same commands are
what an IDE's menu, command palette or its own key bindings call; rebinding a key is replacing an
entry, never re-implementing a behaviour. Commands that need UI (find, go to line, rename) are
raised to the component, which owns the widgets; commands that need a language (go to definition)
go to the provider.

### 3. The view model: a document position is not a screen column

Column 7 of a line is not the seventh cell on screen once a tab, a wide character or a folded region
is involved. The engine keeps the mapping — document position ↔ visual (row, cell) — in one place:
tabs expand to the next stop, East Asian wide and emoji characters take two cells, combining marks
and surrogate halves take none and are never split by the caret. Folds (hidden rows) and inlay hints
(injected cells) are the same mapping with more inputs, which is why they are designed in now and
built later. The block draws the display text this mapping produces, so both realizers draw the same
cells, and hit-testing is the inverse of the same function.

### 4. Text arrives through the platform's own door

On the web the surface keeps a hidden `textarea` at the caret and reads TEXT from it —
`beforeinput`, the composition events, `paste`, `copy` and `cut` — and reads only COMMANDS from
`keydown`. That is how Monaco and CodeMirror take a dead key, an IME candidate, AltGr, a mobile
soft keyboard and a screen reader's view of the current line, and it is the only way to get all of
them. Photon already receives text as text (`TextInput`, `SetMarkedText`); what it lacks is the
composition drawn inline, which the engine now owns (the marked text is injected into the display).

### 5. Intelligence: one contract per capability, sessions in the engine

Each capability is its own provider interface, shaped after the Language Server Protocol so an LSP
client is an adapter and nothing more, and typed where LSP is stringly: completion (+ resolve),
signature help, hover, definition/declaration/type definition/implementation, references, document
highlights, rename, code actions, formatting, semantic tokens, inlay hints, document symbols,
folding ranges, diagnostics. The STATE of each interaction — the open completion list, the active
signature, the pending hover — is a session in the engine, so it is driven by keystroke sequences in
a test with no screen, and so the keymap can route ↑/↓/Enter/Tab/Escape to whichever session is
open.

### 6. Latency is the design constraint

A provider answers asynchronously and possibly slowly; the list must not wait for it. A session
asks once at its trigger (a trigger character, an identifier's first letter, ⌃Space), then filters
and re-ranks LOCALLY on every keystroke, re-asking only when the provider said its answer was
incomplete or the word left the range it answered for. Stale answers are dropped by generation, not
by hoping cancellation arrived. On native the C# provider runs in the same process as the editor,
which is the hop VS Code cannot remove.

### 7. Overlays live in the code's own coordinate space

The completion list, the signature panel, the hover card and the find widget are components,
positioned from the engine's geometry inside the same scrolling layer as the code — so they move
with it, frame for frame, and are clamped (flipped above the caret, shifted left) against the visible
viewport the editor already tracks. The vocabulary's `Anchored` cannot do this on the web (it does
not escape an `overflow:hidden` scroller); nothing here needs it to.

### 8. The net

- The engine's behaviour is tested headless, as sequences of commands and input — no host.
- Each host is tested through its real input path: `PhotonHost` events in xUnit, DOM events against
  the real lowering in vitest.
- The transpiled engine is pinned byte for byte (`SharedComponentTranspilationTests`) and executed by
  the runtime suite, so the web half is the same code, not a promise of it.
- A running page and a running window are the proof of anything visual; a screenshot goes with every
  slice that changes what is drawn.

## Slices

| slice | what lands | proof |
|---|---|---|
| **0** | `eQuantic.UI.Code`: the engine moves out of `Primitives`, one type per file. `ICodeSurfaceModel` replaces the concrete controller on `CodeSurface`; the pointer map and the mark geometry move from the two hosts into the engine | the transpiled twins identical but for imports and the protocol; drag and shift-click on the web by construction (defect 5) |
| **1** | Hardening: the textarea input path; SSR for `CodeSurface`; reveal on Photon; keyboard conventions; the view model (tabs, wide characters, graphemes); `Height = Fill` with the line window; the web `MaxHeight` scroll; the find widget rebuilt (stable tree, focus, Escape, reveal, events); the model defects; the composition drawn inline ([#305](https://github.com/eQuantic/equantic-ui/issues/305)) | defects 1–4, 6–15, each with the test that failed first |
| **2** | Editing power: the command set and custom keymaps, line operations, multiple cursors, snippets | command sequences in the engine tests |
| **2b** | The diff: a line diff (Myers) and a word diff within changed lines, in the engine; `CodeDiff` in Components — side by side with aligned rows and filler, or inline; collapsed unchanged regions; next/previous change; both gutters' numbers; an editable modified side; from two texts or from a unified diff | the diff is pinned against `git diff`'s own output on real files |
| **3** | IntelliSense core: the provider contracts, the completion session and scorer, the completion widget, and built-in providers (document words, language keywords, snippets) so an editor with no language service still completes ([#296](https://github.com/eQuantic/equantic-ui/issues/296), [#297](https://github.com/eQuantic/equantic-ui/issues/297)) | keystroke sequences; a recorded Roslyn answer round-trips |
| **4** | Signature help, hover (pointer hover through the protocol), diagnostics that travel with edits, wavy squiggles, problem navigation ([#298](https://github.com/eQuantic/equantic-ui/issues/298), [#299](https://github.com/eQuantic/equantic-ui/issues/299)) | |
| **5** | Navigation: definitions and ⌘-click, references, the jump list, go to line, go to symbol | |
| **6** | Semantic richness: highlights, rename, code actions, formatting, semantic tokens, inlay hints, folding, indent guides, bracket colours, sticky scroll, regex find and replace | |
| **7** | `eQuantic.UI.Code.CSharp`: Roslyn behind every contract | the IDE and the playground on the same package |
| **8** | Inline suggestions: ghost text, partial accept, composed with the completion preview | |

## Where the slices stand

The track is [#295](https://github.com/eQuantic/equantic-ui/issues/295) on the board; each slice
closes the stories named beside it above.

| slice | state |
|---|---|
| 0 | delivered by [#359](https://github.com/eQuantic/equantic-ui/pull/359): the engine in `eQuantic.UI.Code`, one type per file; `ICodeSurfaceModel`; the pointer map and the mark geometry in the engine; on the web, drag selection, shift-click, double and triple click, a press that no longer scrolls the page, and a drag that no longer starts the browser's own selection over the bands (defects 5, 16, 17). The transpiler gaps it exposed are fixed where they live: a plain class's unassigned field took no default, `bool \| bool` emitted a number and its compound forms stored one, a struct began `null` instead of its zero, a record's module missed the app types its body names, and a type pattern over the engine was a presence check |

## Fenced, on purpose

- **Proportional fonts.** The grid is monospaced, and that is what makes every caret, selection and
  decoration arithmetic instead of measurement. A code editor that wants a proportional face is a
  different component.
- **Word wrap** is designed into the view model (a document line may become several visual rows)
  and deliberately not built in the first slices: a wrapped line of code has lost what its
  indentation was saying, and every IDE ships it off.
- **An LSP client in the SDK.** The contracts are LSP-shaped so the adapter is mechanical, but
  spawning and speaking to language servers is the IDE's business, not the component's.
