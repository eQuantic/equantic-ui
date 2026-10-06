# Design

## How Flutter solves it, and why the session is VS Code's

Flutter has no code editor, and `docs/FLUTTER-PARITY.md` has no row for completion. Its nearest
piece is `RawAutocomplete`: an `optionsBuilder` called on every change of the text, a future's
options shown only when it is the latest, a highlighted option walked by the arrow intents, Escape
dismissing. Its keys and its rule for a stale answer are the ones here. Its asking is not: a code
editor cannot ask a language service on every keystroke, so a list asks once when a word starts and
filters what it was given (the plan's §6, and VS Code's suggest model), asking again only when the
provider said its answer was incomplete.

## A session in the engine, read by any host

`CodeCompletion` lives on the controller and is moved by the controller's own doors: the selection
changed (every edit ends in one), a character typed (`HandleText`), a key (`CodeKeymap`), focus lost,
the document replaced. So it is tested with keystrokes and no screen, it is the same code on the web
(transpiled) and on Photon, and a view only reads `IsOpen`, `IsLoading`, `Items`, `Selected` and
`Start`, and listens to `Changed`.

An editor starts with NO provider. A list that nothing draws must not take Enter, Tab or the arrows,
and this change draws nothing: the view (#297) is what says an editor offers a list.

## Latency: one question per word, and answers dropped by generation

Every request gets a number and a `CancellationTokenSource`. A newer request, or closing the list,
increments the number and cancels the source; an answer is applied only if its number is still the
current one. Cancellation is a courtesy to the provider, the generation is the guarantee: an answer
that arrives anyway is dropped, which is the plan's "dropped by generation, not by hoping
cancellation arrived".

A provider answers on whatever thread it finishes on. The list is read by the render thread, so an
answer that finishes elsewhere is posted to the UI thread through `UiDispatcher`, the rule
`SetState` follows; on the web the dispatcher's twin is null, which is what one thread looks like.

## The keys

VS Code's, with one choice of its own made the default: Enter accepts only what changes the text
(VS Code's `acceptSuggestionOnEnter: smart`), so a word typed out in full ends its line instead of
being "accepted" into itself. Tab always accepts. An arrow with one entry showing moves the caret, as
in VS Code. Escape closes the list and only the list, so the trap on Tab stays armed and a dialog
around the editor stays open; Escape with nothing showing drops what is on its way and goes on to
mean what it means.

## The filter

The pattern's characters in order, case aside, the first where a part of the word starts (its start,
after a separator, a camel hump, the last capital of an acronym), and the best of every way to lay
one over the other: each character 1, one more in its typed case, 8 at the word's start, 6 at a
part's start, 5 next to the previous match, 3 off for each gap. Ties go to the provider's sort text,
then the label, then the order the providers answered in, so the sort is total and the same on both
sides however each sorts.

## Measured, not assumed

The cancellation trio's behaviour was measured with `dotnet fsi` before the runtime was written:
callbacks run once in reverse registration order, a late registration runs at once, throwing
callbacks are gathered into an `AggregateException` with .NET's message, a disposed source refuses
`Cancel` and `Token`, and `new CancellationToken(true)` is one token. The conformance suite runs the
same C# on both sides, and fails 2 of its 14 cases when the callbacks run first registered first.

The engine's twin is compared with .NET by the embedded Bun: the filter over 6,000 patterns cut from
Roslyn's recorded answer, and 60 seeded sessions over a real file, state by state. Reversing the
label order in the twin fails 3 of the sessions.

The shared library was transpiled in its tests with three of the seven implicit usings the SDK
writes, so `CancellationTokenSource` bound in the build and not there, and its twin was written by
the path for a type nothing knows. Every test pipeline takes one file of all seven now, and no other
twin moved.

## What the author's review changed

The review of the whole diff found ten defects, each fixed with a test that fails without its fix:

- Every key but Tab and Escape arms the trap on Tab again BEFORE the list's keys and ⌃Space are
  routed: they returned ahead of the line that re-armed it, so Escape, ⌃Space and Escape left Tab
  moving focus out of the editor.
- Cancelling runs at once what the providers registered on the token. A callback that throws is that
  provider's failure, raised through `Failed`, and never the keystroke's.
- A commit character accepts once an input method's composition is out of the document: taking it
  out afterwards wrote over the range the accepted entry had moved.
- Offers that are one entry are grouped once per answer, and the list takes the first copy of a group
  that MATCHES, so a copy that does not match hides none that does. Sort keys are lowercased once per
  answer, and the filter's search keeps its tables between calls instead of allocating two per entry
  on every keystroke.
- The document's words are read nearest the caret first, up to 50,000 characters: reading every line
  cost 46 ms in Bun at every word started in a file of 45,000 lines. VS Code reads every word, off the
  UI thread, up to ten thousand of them; this answer is given on it, so it is bounded by what it reads.
- In the translation: a method group of the trio is bound to its receiver, read once; a registration
  after the cancellation is the default one, as .NET's; and one rule, `TsStandIn`, names a type with
  no twin of its name on every path that writes an annotation, where each path had named a part of
  them by their C# names.
