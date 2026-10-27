## Context

Slice 3 gave the engine its provider contracts and their sessions (completion, hover, signature help,
diagnostics), and #1083 gave slice 5 its first half: the go-to field over the code, which previews a place
centred and washed and gives the view back on Escape, and the jump list, which records every jump and is
carried through every edit. What is left of the slice asks a language service about a NAME: where it is
defined and where it is used, by key and by ⌘-click.

The engine has every input for it but two. The hover hears the pointer resting on the code, but no host says
which modifiers the pointer passes with (Photon's hover carries none, the web's reads ⌃ as ⌘ on a Mac), and
none says it again when a modifier is pressed with the pointer at rest. And nothing on a code surface can
change what the pointer looks like: the web draws the text cursor over the whole surface, and Photon answers
the text cursor for every code region. A sheet surface already answers `CursorAt`, which is the precedent.

## Goals / Non-Goals

**Goals:**

- F12, ⌘F12, ⇧F12, F4 and ⇧F4, and the ⌘-click gesture, as VS Code has them, on both targets from the same C#.
- Contracts an LSP adapter maps one to one, and that slice 7 (Roslyn) answers.
- A list of places that reuses the go-to field, its preview, its keys and its accessibility.
- Every jump recorded in the jump list; a place in another document handed to the app.

**Non-Goals:**

- VS Code's peek: an editor embedded in the code, between its lines. The list here shows each place's line
  and previews it in the editor itself, as the go-to field previews a symbol.
- The definition's source in a hover while ⌘ is held, the flash of the target after a jump, opening to the
  side (⌘⌥-click), and walking the rest of a single answer with F12 again (`gotoNextSymbolFromResult`):
  each a follow-up of its own if an app asks.
- `originSelectionRange` (LSP's `LocationLink`): the link is the word under the pointer, as VS Code draws it
  when a provider answers plain locations, which is what Roslyn's language server answers.
- A history across documents: the editor holds one document, and its jump list and its walk hold places in
  it. An IDE that holds several documents owns its own history, as VS Code's lives in its workbench.

## Decisions

- **One provider for the four definitions, told which is asked.** LSP has four requests of one shape, and VS
  Code four registries; `ICodeDefinitionProvider.DefinitionsAsync(document, position, kind, cancellation)`
  is the four in one, typed by `CodeDefinitionKind` (`Definition`, `Declaration`, `TypeDefinition`,
  `Implementation`), and a provider that does not answer a kind answers nothing. An adapter maps a kind to its
  request. References are their own contract, since they answer a different question and VS Code asks them
  with the declaration included; nothing in the editor asks without it, so the parameter LSP has is not one
  here.
- **A location names its document, null for the editor's own.** `CodeLocation(string? Document,
  CodeRange Range)`: the providers are the editor's (each knows the document it serves, as every contract
  here takes a `CodeDocument` and no name), so a place in the same document needs no name, and one in another
  carries the name its language service gives it (a path, a URI). The editor never opens another document:
  it raises `OpenRequested`, and the app opens it.
- **The answers are joined as VS Code's `ReferencesModel` joins them.** Every provider is asked at once and
  the answer waits for the last; a provider that throws counts as answering nothing and is reported through
  `Failed`, as `CodeSymbols` does. The places are ordered by document (the editor's own first, then by name,
  ordinally) and by where they start, and a place equal to the one before it is dropped. A request is
  cancelled when the text changes or the caret moves (`CodeEditorStateFlag.Value | Position`), and a late
  answer is dropped by its generation.
- **One answer is gone to, several are listed, none is said** (`SymbolNavigationAction.run`). Going to a
  place selects its start, records a jump, and brings it near the top of the view when it is out of it
  (`TextEditorSelectionRevealType.NearTopIfOutsideViewport`): a gap of a fifth of the view above it, five
  lines at least, and never pushing the place out of the view (`viewLines.ts`). When the caret already stands
  in the only definition, F12 finds the references instead, VS Code's `alternativeDefinitionCommand`, and so
  do a declaration and a type definition; an implementation has none. With no answer, a card at the caret
  says so in VS Code's words ("No definition found for 'amount'"), shown as the keyboard shows a card, so it is
  announced and closes as a card closes; the ⌘-click mutes it, as VS Code's does.
- **The list is the go-to field in a mode of its own.** `CodeGoTo` gains `Definitions` and `References`,
  modes the text does not choose: the field holds a filter, with no prefix, and its rows are places. A row is
  the place's line with its leading white space left out, the name marked, and its line and column beside it;
  a place in another document shows the document's name instead, since its text is not the editor's to read.
  With nothing typed, the place nearest the caret is selected (`ReferencesModel.nearestReference`: the same
  document first, then the distance in lines and columns); typing keeps the document's order and selects the
  first row left. A place in the editor's document is previewed as a symbol is, centred and washed; one in
  another is not previewed. Enter goes to the selected place, through the same path as a single answer; the
  list reuses the field's keys, its listbox semantics and its view given back on Escape.
- **F4 walks the last list, shown or not.** VS Code's F4 needs its peek or its references view open. Here the
  places of the last list are held by the session, carried through every edit as the jump list's are, until
  another list replaces them or the document is replaced. While the list shows, F4 and ⇧F4 select the next and
  the previous row, wrapping, and preview it; once it is closed they go to the place after or before the one
  last gone to, wrapping, each a jump. With nothing held, the keys are not claimed.
- **The link is VS Code's `ClickLinkGesture`.** The trigger is the engine's Command: ⌘ on Apple, Ctrl
  elsewhere, where ⌥ adds carets on both (#1012). While it is held alone and the pointer rests on a word, the
  definitions there are asked at once, not after the hover's pause, and a word already asked is not asked
  again; an answer with a place shows the link, the word underlined in the accent colour, and `CursorAt`
  answers the hand over it. The link goes when the trigger is released, another key is pressed with it, the
  pointer leaves the word or the surface, a press starts a drag, the text changes, or the view scrolls. A
  press with the trigger alone (one click) places the caret as a press does; its release on the same line,
  without a drag, goes to the definition at the press, the message muted.
- **The hosts report modifiers, and the hover again when they change.** The engine cannot hear a modifier key
  released, and no interface member is needed for it: a host that sees the held modifiers change while the
  pointer rests over a code surface hands the surface a hover at the pointer's last place with the new
  modifiers. On the web, the code surface's pointer reads the modifiers as its keys do (`doorModifiers`: ⌘ is
  Command and ⌃ is Control on a Mac), and its text door's `keydown` and `keyup` of a modifier key re-send the
  hover. On Photon, `PointerMove` takes the modifiers, the macOS shell reads them on every mouse move and on
  `flagsChanged`, and the Windows shell on `WM_MOUSEMOVE` and on the Ctrl key's `WM_KEYDOWN` and `WM_KEYUP`;
  `PhotonHost` re-sends the hover when they change. The macOS shell's press read only ⇧ and ⌘, so ⌥ and a
  press, which adds a caret (#1012), never reached a code surface in a macOS window: it now reads all four.
- **A code surface answers `CursorAt`, as a sheet surface does.** The web lowering sets the surface's cursor
  from it as the pointer moves; Photon's `CursorAt` asks it over a code region instead of answering the text
  cursor. A drawn box cannot say it: what the component draws over the code takes no pointer.

## Risks / Trade-offs

- A provider asked on every word the pointer crosses with ⌘ held. → Asked only while the trigger is held, once
  per word, the previous request cancelled; VS Code asks as often.
- The places of another document cannot be carried through its edits, nor shown with their text. → They keep
  the range the provider gave, and their row names the document and the line; the app that opens them owns
  their text.
- F4 claimed after a list closed, where VS Code leaves it. → Claimed only while places are held, and a new
  document drops them; an app that binds F4 itself binds before the defaults, as every key.
- `CodeGoToEntry.Kind` nullable is a change of shape. → In preview, a signature widens and the old one goes;
  nothing in the repository but the view reads it.
