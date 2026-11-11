# How a change is proved

A pull request says what it ran and what that showed. This page is how the project decides that a
change is right, learned from the times a green suite was wrong. The design rules are the other half,
in [PRINCIPLES.md](PRINCIPLES.md).

## 1. Done means it runs

A green test is not evidence that the code runs. Before calling a change done, find the entrypoint
the shipped thing actually takes and confirm the change is on it: codebases keep a legacy path beside
the live one, gated by a flag or an environment variable, and its own comment usually says which is
which. When the behaviour depends on something a unit test replaces with a fake (a browser, a shell,
a simulator, a daemon, the build of a consumer's solution), the proof is a run against the real one.

## 2. A/B, both ways, every round

A test that pins a fix must fail without the fix. That is half of it: it must also fail with the most
plausible wrong fix, or it does not tell the two apart. A date buffer's test passed with the naive
rule because `en-US` writes `7/1/2026` exactly as it was typed; it took leading zeros to make it
discriminate. When a case passes on the old side, read why before moving on: the usual cause is input
for which both answers coincide. Do one A/B per review round, not one per pull request, since a
reviewer's fix needs its own.

To revert and restore during an A/B, copy the file first and restore from the copy, then `touch` it:
`git checkout -- <file>` brings back the committed file and drops every uncommitted change in it, and
a restored file with an older timestamp is skipped by the incremental build, so the next run tests
stale code.

## 3. .NET is the oracle, and every claim is a hypothesis

What C# does is decided by running .NET, not by reading about it, and that holds for a reviewer's
claim, an issue's and your own. A review said .NET refuses an invalid `MidpointRounding` whatever the
value; measured, that holds for half the overloads, and a conformance case written from the claim was
wrong. `dotnet fsi` answers most questions in a second. The conformance suite executes both sides,
C# under .NET and the JavaScript the compiler emits, and compares them.

## 4. Was the instrument ever exercised in the condition it exists for?

An instrument whose whole purpose is the bad path tends to be run only on the good one, and then it is
right in every condition anyone tried and wrong in the one it is for. Ask it of every guard, pin and
fallback, and answer it with a command. The common case is not the clean case: a background tab keeps
`requestAnimationFrame` defined and stops calling it, so the obvious `typeof` check passes exactly when
it should not.

## 5. Suspect the instrument first

When a differential generator or the conformance suite reports a new failure, the first hypothesis is
that the instrument is broken. In one phase the score was four instrument faults to two compiler bugs:
a generated name shadowed a variable, a wider switch diluted a case below the corpus's noise, a test
wrapper lacked a `using`. A test run that aborted can still print `Passed!`: read the counts.

## 6. Sets are derived, never listed

A pin that scans assemblies or branches derives its set from what is there (the output directory,
the symbols that reference the vocabulary) and asserts that the set holds what it must; a list
written by hand is incomplete from its first day. The branches of the transpiler that can return a
name were counted in a comment, and the count was wrong, always low, in every version: three missing
branches failed at once, one of them writing `"[object Object][object Object]"` for `a + b` in
silence. And a pin only refuses the line it does not find: moving a type out of the scanned assembly
erases its coverage without a failure, so a move is followed by a look at what the pin still sees.

## 7. A guard refuses the mechanism, not only the symptom

A guard that reads the output passes an equivalent output written the wrong way. The generators'
guard reads their source and refuses `StringBuilder` and `Environment.NewLine` there, so a local
helper that produced byte-identical files was refused, and the fix moved 134 places onto
`CodeWriter`. When a rule is about how something is built, the guard reads how it is built.

## 8. Goldens prove geometry and colour, not state

A golden image recorded without the pressed tint and the focus ring blessed both bugs: a button that
never darkens is still a well-drawn button. When a test is about a state (pressed, focused, hovered,
selected, a caret), put an assertion beside the image and compare against the same frame without the
state, since a colour on screen can be there by coincidence.

## 9. Agreement is not correctness

The C# realizer and its TypeScript twin can read the same wrong field and agree perfectly: a
paragraph made of styled runs has an empty `Content` (the words are in the runs), and four places read
`Content` on both sides, so a fixture comparing the two passed while a styled label reached assistive
technology with no name. Where there is no outside oracle, a green cross-pin reassures more than it
should; find one (.NET, the browser, the platform's accessibility tree).

## 10. Run both sides, the way they are driven

A node written for the web and for Photon is tested on both, driven as each is driven: a vitest spec
sends `keydown` with its flags and `pointerdown` with coordinates, not only a DOM golden; a Photon test
crosses a frame, because Photon builds every frame and the web does not. The compiler's gate proves
the output compiles, not that it works.

## 11. Observe, never sample

A fix that depends on something not yet there (layout that settles, a bar that grows, a jump the
browser makes) observes it, with `IntersectionObserver`, `ResizeObserver` or the frame loop, rather
than sampling it once at a moment that may come too early. Of three things the runtime schedules after
a pass, the two built on observers held for years and the one built on a sample broke twice.

## 12. Real cases find what tests do not

A unit test asks what its author already imagined. A real page asks what nobody did: `StudioWalkTests`
walks every section of the Studio sample as a person would (every Tab stop, every field typed into)
and asserts invariants of the pipeline. For a change to the build, measure on a copy of a consumer's
solution as well as on the samples: it found two defects the samples could not. The browser is the
arbiter of what the web does.

## 13. When shared state becomes per item, test the way back

Replacing one shared piece of state with state per item drops every transition the shared one
handled by accident. List them before writing the fix, the backward ones above all (Backspace, undo,
a move to the left), and test each: a completion that moved its "asked word" onto each answer stopped
asking again after Backspace, which the shared word had covered.
