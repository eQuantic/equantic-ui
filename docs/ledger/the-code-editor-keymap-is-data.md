- **2026-10-20 · The code editor's keys are commands, and its keymap is data**: the first part of slice 2
  of `docs/CODE-EDITOR-PLAN.md`. Every action a key ran is a `CodeCommand`, and `CodeKeymap.Defaults` is
  a table of `CodeKeyBinding`s from the SDK's `KeyChord` to a command, one for each keyboard convention,
  a chord trying its commands in order until one runs and a binding of two keys waiting only while its
  command could run. `CodeEditorController.Execute` runs a command with no key, `CanExecute` says whether
  it would, and `CodeEditor.KeyBindings` puts an app's own bindings over the defaults, a chord bound to
  `None` going back to the host. A chord names its modifiers exactly: measured over every key and every
  combination of the four modifiers in both conventions and ten states (`CodeKeymapBehaviourTests`), the
  combinations the hand-written keymap claimed by accident went to the host, and every chord the tables
  name answers as before but three, on purpose (⇧Tab and ⇧Enter over a list, ⌥↑ ⌥↓ outside the
  signature panel) ([#1010](https://github.com/eQuantic/equantic-ui/issues/1010)). Proposed and
  archived through OpenSpec (`openspec/specs/code-keymap`, a new spec).
