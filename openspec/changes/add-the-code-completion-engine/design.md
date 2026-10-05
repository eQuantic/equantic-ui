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
the path for a type nothing knows. The tests mirror all seven now, and no other twin moved.
