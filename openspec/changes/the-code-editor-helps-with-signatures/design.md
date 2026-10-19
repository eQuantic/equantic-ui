## Context

docs/CODE-EDITOR-PLAN.md's slice 4 is the editor that explains what it holds. #1008 delivered its
problems and its hover: two sessions on the controller, providers asked once and dropped by their
generation (§6), a card drawn as a note of the code surface (§7). Signature help is the third piece
(the parity bar's "Signature help, overloads, active parameter" row), and #620 is a debt the completion
took on in slice 3: it waits for every provider before it shows the first answer.

## How Flutter solves it

Flutter has no code editor, and no piece of it answers signature help: the closest are
`RawAutocomplete`, whose options view is an overlay a field keeps beside itself, and `Tooltip`, which
explains what is under the pointer. Both are already answered here, by the completion list and the
hover card, and the panel takes their shape: drawn by the code surface in the code's own coordinates,
not by an `Anchored` (§7 of the plan says why: it does not escape a scroller's clip on the web).

VS Code is the parity bar's other column, and it decides the behaviour, measured in its source
(`parameterHintsModel.ts`, `parameterHints.ts`):

- The model asks the providers on a trigger character, on its explicit command, and, while the widget
  shows, on every content change and every cursor move, through one throttle of 120 ms
  (`ParameterHintsModel.DEFAULT_DELAY`). Every request after the first is a retrigger, and carries what
  shows (LSP's `SignatureHelpContext.isRetrigger`, `activeSignatureHelp`).
- The provider is the authority on the active signature and parameter: the client counts no commas.
  Counting them would answer wrong wherever a comma is not an argument's (`Dictionary<int, string>`, a
  lambda's parameters), and a language server answers right.
- An empty answer closes the widget; that is how a provider says the caret left the call.
- With more than one signature, ↑ and ↓ (or Alt+↑, Alt+↓) walk them; Escape closes the widget; ⇧⌘Space
  (Ctrl+Shift+Space) asks at the caret.
- The widget stands above the caret, below it when there is no room, and the suggest widget below the
  line, so the two show together.

## Decisions

- **A session, as the completion and the hover are.** `CodeSignatureHelpSession` lives on the
  controller (`SignatureHelp`), driven by keys, text and moves in a test with no host. `CodeSignatureHelp`
  is the provider's answer, as LSP's `SignatureHelp` is.
- **The contract is LSP's, typed.** `ICodeSignatureProvider.SignatureHelpAsync(document, position,
  context, cancellation)` with `TriggerCharacters` (`(` by default) and `RetriggerCharacters` (`,` by
  default); `CodeSignatureContext` carries the trigger (`Invoked`, `Character`, `ContentChange`), the
  character, whether it is a retrigger and what shows. A parameter is its label, which the editor finds in
  the signature's label in order: LSP allows a label or its offsets, and a label is what an app author
  writes; an adapter turns offsets into the label they cover.
- **Asked at the trigger, and again while it shows.** A trigger character asks the providers that declare
  it, at once. While the panel shows, a retrigger character asks at once and every other edit or move
  asks after `Delay` (120 ms), the next one cancelling the request out; an answer is applied only while
  its generation is the newest. Providers are asked in order and the first that answers something is
  shown, as VS Code's registry orders them.
- **The provider closes it.** An answer with no signature closes the panel; so do Escape, the editor
  losing the keyboard and another document. A press in the code moves the caret, which asks again, as
  VS Code does: the panel never decides on its own that the caret left the call.
- **The user's overload is kept.** ↑ and ↓ pick a signature; a later answer keeps that choice while it
  holds as many signatures, as VS Code keeps `activeSignature` on a retrigger, and takes the provider's
  otherwise.
- **Where the panel stands.** Above the caret's line, its left edge at the caret, inside the view
  sideways; below the line when there is no room above. With the completion list showing, it stands on
  the other side of the line, and is not drawn when that side has no room. A hover card showing hides it,
  since the card is what the person asked for last.
- **What the panel says.** The signature's label in the code face, the active parameter's span in the
  accent colour, and "1/3" when there are overloads; then the active parameter's label and its
  documentation; then the signature's documentation, muted. It is a polite live region, so the parameter
  the caret moved to is read out, its label being in the text.
- **#620: answers one by one.** Each provider's answer is applied as it arrives and listed in the order of
  `Providers` whatever the order it lands in, so the ranking's last tie stays the providers' order and the
  twin stays deterministic. A request belongs to its provider: asking a provider again (an incomplete
  answer and a changed word) cancels that provider's request alone, so a slow provider's answer still
  lands. The selected entry stays selected when an answer lands; the list closes once every provider
  has answered and none offered anything; `IsLoading` is "a provider is still out".

## Risks and trade-offs

- A provider that answers slowly leaves the panel showing the parameter it answered for until it
  answers again; the panel shows what is known rather than guessing ahead of the provider.
- A provider that never answers null keeps the panel open past the call's end until Escape: the panel
  trusts its providers, as the completion trusts its answers.
- A list that keeps its selection when an answer lands may keep a weaker match selected over a better one
  that just arrived; Enter then accepts what the person was looking at, which is the point.
