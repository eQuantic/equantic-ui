## Why

Closes #298 and #620, both sub-issues of #295 (Track I · CodeEditor intelligence), the rest of slice 4
of docs/CODE-EDITOR-PLAN.md after #1008. An editor that completes a name and explains a problem still
says nothing while a call is being written: which overloads there are, and which argument the caret is
on. And the completion waits for its slowest provider before it shows anything, so a language server
that hangs holds back the language's words and the document's for every word typed after it (#620).

## What Changes

For a developer using the SDK:

- `CodeEditor.SignatureProviders` says what explains the call being written
  (`ICodeSignatureProvider`, LSP's `textDocument/signatureHelp`, typed). Typing a provider's trigger
  character (`(` by default) opens a panel over the caret: the signature with the argument the caret is
  on marked, that parameter's documentation, the signature's, and "2/3" with the arrows when there are
  overloads. While it shows, every edit and every move asks again after a short pause (120 ms, VS
  Code's), so the provider moves the active parameter as the arguments are typed, `gap:` included,
  and closes the panel by answering nothing once the caret leaves the call. ↑ and ↓ walk the overloads
  when there is more than one, ⌘⇧Space (Ctrl+Shift+Space) asks at the caret, and Escape closes it.
- A signature is written as its label and its parameters' labels:
  `new CodeSignature("Column(float gap, MainAlign main)") { Parameters = [new("float gap"), new("MainAlign main")] }`.
  The editor finds each parameter in the label in order, so `int x, int xy` marks the right one.
- The controller gains the session, `CodeEditorController.SignatureHelp` (`CodeSignatureHelpSession`),
  driven with no screen as the completion is.
- The completion shows each provider's answer as it arrives, in the order of `Providers` whatever the
  order they land in, keeps the selected entry selected when another answer lands, and closes only once
  every provider has answered and none offered anything. `IsLoading` reads "a provider is still out".
  A provider asked again because it called its answer incomplete no longer drops what another provider
  still owes.

The parts it reaches: the engine (`eQuantic.UI.Code`: the contracts, the session, the keymap, the
completion's requests), the components (`CodeEditor` and the new `CodeSignatureCard`), and through them
both realizers, the panel being a note of the code surface (#1008). Nothing new in the vocabulary, eqc
or the runtime. The public surface grows; the developer surface does not move.

No break: `CodeCompletion.IsLoading` keeps its name and type and answers the question its documentation
always asked, now that answers arrive one by one.

## Capabilities

### New Capabilities

- `code-signature-help`: the panel that explains the call being written, when it opens, what it
  holds, how it follows the arguments, where it stands and how it closes.

### Modified Capabilities

- `code-completion`: each answer shows as it arrives, and an answer is dropped only when its own
  provider was asked again or the list closed.
