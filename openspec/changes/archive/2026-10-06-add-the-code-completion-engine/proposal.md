# Proposal

Closes #296, a User Story under the feature #295 (Track I, the code editor's intelligence), itself
under the epic #288 (the roadmap ahead).

## Why

Slice 3 of `docs/CODE-EDITOR-PLAN.md` is the core of IntelliSense, and its engine half is the model:
what a completion list holds, which entry is selected, when the providers are asked, and what
accepting writes. The contract's seeds (`ICodeCompletionProvider`, `CodeCompletionItem`) were in the
engine and nothing used them. An IDE built on the SDK and the playground both need the model before
either can draw a list, and the plan decides its shape: a session in the engine driven by keystroke
sequences (§5), asking once and filtering locally because latency is the constraint (§6).

## What Changes

- `CodeEditorController.Completion`, a `CodeCompletion`: it asks its providers once when a word
  starts (a provider's trigger character, a word started outside a comment or a string, ⌃Space),
  filters and ranks what they answered on every keystroke after that, asks again only a provider that
  called its answer incomplete, and drops an answer whose word was left or that a newer request
  replaced, by its generation, while the request is cancelled. An answer that comes back on another
  thread is posted to the UI thread, as `SetState` posts.
- The keymap routes the list's keys while one shows: the arrows and page keys walk it, Tab accepts,
  Enter accepts only what changes the text, and Escape closes the list and only the list.
- The contracts are the Language Server Protocol's, typed: `ICodeCompletionProvider.CompleteAsync`
  takes a `CodeCompletionContext` (why it was asked, the character, the editor's language) and answers
  a `CodeCompletionList` that may be incomplete, and `ResolveAsync` fills in the selected item.
  `CodeCompletionItem` gains `FilterText`, `Preselect` and `CommitCharacters`, and
  `CodeCompletionKind` every kind LSP has.
- `CodeFuzzyMatch`, the filter, written once for the list and for anything that narrows as a person
  types.
- `CodeKeywordCompletionProvider` offers the language's own words, `ICodeLanguage.Keywords`, the
  tables its colours already read, and `CodeWordCompletionProvider` the document's, read nearest the
  caret and no more than 50,000 characters of it, since it answers before the keystroke returns. An
  editor starts with no provider, so no key goes to a list nothing draws: the view (#297) says what it
  offers.
- eqc crosses what the engine is the first write-once code to use: the cancellation trio
  (`CancellationTokenSource`, `CancellationToken`, `CancellationTokenRegistration`) as the runtime's,
  refusing the members it does not carry (EQ2004); a method named `Invoke` is a method, where it was
  called as a delegate; and an annotation names a type whose C# name names nothing in TypeScript by
  what it crosses as (an exception as `Error`, an interface as `any`, an enum as its members, a
  delegate as its function), one rule on every path that writes one, where each path named types no
  module defines.
- The runtime carries the cancellation trio (`utils/cancellation.ts`), `UiDispatcher` with a null
  `current`, and delegate helpers typed by the delegate.

A break, allowed in preview: `ICodeCompletionProvider.CompleteAsync` takes a `CodeCompletionContext`
and answers a `CodeCompletionList`, `TriggerCharacters` declares none by default, and
`CurlyBraceLanguage`'s protected `Keywords` set is `ReservedWords`, `Keywords` being the language's
public list of words. Migration line: a provider takes the context and returns
`new CodeCompletionList(items)`, and a language derived from `CurlyBraceLanguage` renames its
`Keywords` override to `ReservedWords`.

## Capabilities

### New Capabilities

- `code-completion`: the completion of a code editor, when it asks, how it filters and ranks, what
  its keys do and what accepting writes, the same on .NET and on the web.

### Modified Capabilities

- `transpiler-bcl`: the cancellation trio, against .NET's answer.
- `transpiler-expressions`: a method named `Invoke`, and one rule for the annotation of a type with
  no twin of its name.

## Impact

The code engine (`eQuantic.UI.Code`), eqc (a strategy for the cancellation trio, the invocation
strategy, and `TsStandIn`, which the class and record emitters, the locals and the local functions
annotate through), the runtime (cancellation, `UiDispatcher`, the delegate helpers, the exception
table, the twins), and the tests that transpile the shared library, which now take one file of all
seven implicit usings of the SDK. The public surface moves (the new types and the breaks
above), and the developer surface does not.
