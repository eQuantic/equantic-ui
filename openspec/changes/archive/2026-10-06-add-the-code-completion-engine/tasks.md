# Tasks

## 1. The contracts

- [x] 1.1 Reshape `ICodeCompletionProvider` after LSP: a context in, a list that may be incomplete out,
      and a resolve with nothing to add by default
- [x] 1.2 Give `CodeCompletionItem` its filter text, preselection and commit characters, and
      `CodeCompletionKind` every kind LSP has
- [x] 1.3 Check: the runtime's interface defaults hold the new members (`interface-defaults.spec.ts`)

## 2. The session

- [x] 2.1 `CodeCompletion` on the controller: ask once per word, filter locally, ask an incomplete
      answer again, drop an answer by its generation and cancel its request
- [x] 2.2 Apply an answer from another thread through `UiDispatcher`
- [x] 2.3 Route the list's keys in `CodeKeymap`: arrows, page keys, Tab, Enter that changes the text,
      Escape for the list alone, ⌃Space
- [x] 2.4 Accept over the word or the provider's moved range, a commit character, a preselected entry,
      the selected entry resolved once
- [x] 2.5 Check: keystroke sequences with no host (`CodeCompletionTests`), Roslyn's recorded answer
      from the playground among them

## 3. The filter and the built-in providers

- [x] 3.1 `CodeFuzzyMatch`, the best way a pattern lies over a word
- [x] 3.2 `ICodeLanguage.Keywords` from the tables the colours read, and the two built-in providers
- [x] 3.3 Check: the filter pinned by hand (`CodeFuzzyMatchTests`), and the twin against .NET over
      6,000 patterns and 60 seeded sessions in the embedded Bun (`CodeCompletionTwinTests`)

## 4. The translation

- [x] 4.1 The cancellation trio as the runtime's, every other member refused (EQ2004)
- [x] 4.2 A method named `Invoke` called as a method
- [x] 4.3 An exception annotated as `Error` and an interface as `any`, then one rule for every type with
      no twin of its name on every annotation path (`TsStandIn`)
- [x] 4.4 Transpile the shared library with the SDK's seven implicit usings
- [x] 4.5 Check: the conformance suite on both sides (`CancellationConformanceTests`, failing with the
      callbacks in the wrong order), the BCL audit's 23 verdicts, and the runtime's `tsc` over every
      twin

## 5. Documentation

- [x] 5.1 The wiki's code editor page, in English and Portuguese
- [x] 5.2 The plan's slice 3 rows and the `docs/LEDGER.md` line citing #296
- [x] 5.3 Archive this change before the merge, so `openspec/specs` on main matches the code

## 6. The author's review

- [x] 6.1 Review the whole diff (`/code-review high`) and fix each defect it confirms, proved failing
      without its fix: the trap on Tab, a cancellation that throws, a commit during a composition, a
      copy that does not match, the filter's per-keystroke work, the document's words read whole, a
      method group of the trio, a late registration, the annotation rule and the implicit usings
