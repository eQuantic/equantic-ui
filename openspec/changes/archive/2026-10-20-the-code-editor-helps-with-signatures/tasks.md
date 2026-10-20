# Tasks

## 1. The completion's answers, one by one (#620)

- [x] 1.1 A request per provider: asking a provider again cancels its own request alone, and an answer is
      applied while its list is open and its request is its provider's newest
- [x] 1.2 Each answer applied as it lands, listed in the order of `Providers`, the selected entry kept,
      the list closed for want of offers only once every provider asked has answered, `IsLoading` while
      one is out
- [x] 1.3 Check: keystroke sequences in the engine's tests (a provider that never answers beside one that
      answers at once, a later answer, an incomplete answer asked again beside a slow provider), and the
      completion's twin executed against .NET

## 2. Signature help in the engine (#298)

- [x] 2.1 The contract: `ICodeSignatureProvider`, `CodeSignatureContext`, `CodeSignatureTrigger`,
      `CodeSignatureHelp`, `CodeSignature`, `CodeSignatureParameter`, and the parameter's span found in
      its signature's label
- [x] 2.2 `CodeSignatureHelpSession`: the trigger characters, the retriggers, the delay, one request out
      and the generation, the first provider that answers something, the user's overload kept, closing
      on an empty answer, Escape, the keyboard leaving and another document
- [x] 2.3 The keymap: ⌘⇧Space, ↑ and ↓ over the overloads, Escape after the list and the card
- [x] 2.4 Check: keystroke sequences in the engine's tests (walking `Column(` to `gap:`), and the twin
      executed against .NET

## 3. The panel

- [x] 3.1 `CodeSignatureCard`: the label with the active parameter's span marked, the overload count, the
      parameter's documentation and the signature's, a polite live region
- [x] 3.2 `CodeEditor.SignatureProviders`, adopted from a parent's next build; the panel placed above the
      caret's line or below it, on the other side of a completion list, hidden behind a hover card
- [x] 3.3 The dashboard sample's code page explains its calls
- [x] 3.4 Check: the component through `PhotonHost`, vitest through the real lowering, a running page in a
      browser, and the Photon pixels on the reference backend, light and dark

## 4. Documentation

- [ ] 4.1 docs/CODE-EDITOR-PLAN.md: slice 4 delivered
- [x] 4.2 The wiki's CodeEditor page in every language it is written in
- [x] 4.3 One ledger entry, a file under docs/ledger/, citing #298 and #620
- [x] 4.4 Archive this change before the merge
