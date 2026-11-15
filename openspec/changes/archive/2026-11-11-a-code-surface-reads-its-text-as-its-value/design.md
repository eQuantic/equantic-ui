# Design

## How Flutter solves it

`EditableText` hands its semantics node the text as `value` and the selection as `textSelection`, so a
reader hears a field and its text as one node and reads the line the caret is on. A code surface here draws
its text itself, so the realizer cannot read it off a native control: the model has to say it, which is
`ICodeSurfaceModel.Reading`.

## Decision

The model answers a `TextReading`: some text, and the anchor and focus of the selection inside it, in
UTF-16 code units because that is the unit a browser's `setSelectionRange` and .NET's strings count in.
The text is the lines around the primary caret, ten on each side, the way a code editor in a browser hands
its screen reader a page of lines: a keystroke in a file of thousands of lines then writes a page, not the
file, and a reader moving by line still finds the next one in the field.

The web writes the reading as the `textarea`'s content, as the server writes it, and after each render sets
the live value and the selection, which exist only on the element. The selection moving is what makes a
reader say the line the caret moved to. Nothing is written while an input method composes, because moving
the selection under a composition cancels it. Every edit still reaches the model through `beforeinput` and
the composition events, which are prevented or taken out, so the door holding text changes nothing of what
typing does.

Not under a finger. A phone's keyboard reads the text around its caret, composes words in it and
replaces a word it corrects, and the door hands the model what is typed, never the range a correction
meant to replace. A door holding the text would have the keyboard rewrite words already written, so where
the page's primary pointer is coarse the door holds nothing, as it always did, and a phone's screen reader
waits for the door to report a replacement's range, which is its own change.

Photon's `SemanticNode` carries a value and no text selection, so Photon reads the text as the field's value
and not yet where the caret stands in it; a selection on Photon's semantics is a change to every shell and
is not this one.

## Not changed

The spreadsheet's status region, #1444 (see the proposal).
