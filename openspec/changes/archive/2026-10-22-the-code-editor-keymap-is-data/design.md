## Context

`CodeKeymap.Handle` is one function of about two hundred lines that decides, by hand and in order, what a
key means: the Tab trap, a chord half typed, an open completion list, ⌃Space, the signature panel, ⌘K ⌘I,
F8, Escape's chain, the letter chords, the moves and the edits. It transpiles, so both realizers call the
same code, and that part is right. What is wrong is that it is code: nothing outside it can bind, run or
free an editor action, and every slice after this one (line operations, carets, snippets) would add
branches to it.

## How Flutter solves it

`Shortcuts` maps an activator (`SingleActivator`, `LogicalKeySet`) to an `Intent`, and `Actions` maps an
intent's type to the `Action` that runs it; an action says whether it is enabled (`isEnabled`) and whether
it took the key (`consumesKey`), and the key goes on to the next handler up the focus tree when it did
not. The table is data, the behaviour is code, and the two meet by a name. The SDK already took the
first half: `Shortcut` and `KeyChord` in the vocabulary. The editor's keymap is the same idea inside one
component, with VS Code deciding what the defaults are.

VS Code's keybindings (`workbench/services/keybinding`, the default keymaps of the editor contributions)
set the rest: a command is a name, a binding is a chord to a command with a `when` clause, a chord of two
keys waits for its second half and anything else ends the wait, and an exact chord is what matches.

## Decisions

- **A command is a member of `CodeCommand`.** Typed, as the product principle asks, and append-only like
  every vocabulary enum (`EnumValueAbiTests`). The members are argument-free, as VS Code's are: a move
  with the selection extended is its own command (`CursorLeftSelect`), not a flag on a binding.
- **The `when` clause is the command's own state.** VS Code gives each binding a clause over context
  keys; here a command answers whether it ran (`Execute` returns false when it did nothing), and a chord
  may name several commands in order, the first that runs claiming the key. ↓ is the list's next entry,
  then the panel's next overload, then the caret's move down: what the hand-written keymap did with
  `if`s, and what an `Action`'s `isEnabled` and `consumesKey` do in Flutter, without a clause language.
- **A two-key chord waits only when its command could run.** ⌘K is claimed only while a card could
  show (`CanExecute(HoverShow)`), as it is today, so a browser's Ctrl+K stays the browser's when the
  editor has nothing to explain. Any key but a modifier ends the wait, first, before anything claims it.
- **Chords match exactly.** The modifiers of a key are the binding's, all of them. A single letter
  matches either case, since ⇧ with ⌘ reports "Z", and the slash matches as "/" or "Slash".
- **An app's bindings come first, and a chord it binds is its own.** `KeyBindings` on the controller
  (and on `CodeEditor`, adopted from a parent's next build) are tried before the defaults, and the
  defaults are not tried for a chord the app named: binding a chord to `None` frees it for the host.
- **The Tab trap and the chord's wait stay in the dispatch.** They are not commands: they are what a
  key does before any command runs, and they run first, as the comments that explained them say.
- **The defaults are two tables, written in C#.** One per `KeyboardConvention`, read by
  `CodeKeymap.Defaults`, so an IDE can list them in its own shortcut sheet. Nothing parses a string.

## The measure that keeps the table honest

`CodeKeymapBehaviourTests` presses every key an editor answers, under all sixteen combinations of the
four modifiers, in both conventions, from ten states (a selection, no clipboard, read-only, the trap
released, a list, a card, the panel, problems, a chord half typed), and writes down what changed. The
hand-written keymap wrote the first baseline; the table must write the same lines for every chord it
names, and the lines that leave are the loose combinations this change gives to the host, read one by
one in the diff.

## Risks and trade-offs

- A user who relied on a loose combination (⌘⌥A) loses it. Every IDE leaves those to the platform, and
  an app that wants one back binds it.
- One chord naming several commands puts the order in the table, where it is read rather than in
  nested `if`s. A wrong order is a line that moved in the baseline.
