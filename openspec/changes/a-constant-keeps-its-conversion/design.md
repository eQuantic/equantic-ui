# Design

## Context

See proposal.md for the three defects. Two of them sit in what #827 added: the reader of a module's
names (`NamedSourceTypes`), which leaves out syntax that writes nothing, and the reader of the list of
projects the SDK hands the build. The third is in the two places that write an interpolation hole.

Flutter has no counterpart to ask: these are rules of the C# to JavaScript translation, not of the
component model.

## Goals / Non-Goals

**Goals:**

- A value written in place of a constant or of `nameof` names what its conversion names.
- An alignment is written from the model's constant, by one function for both of its writers.
- A handed list that holds no project stops the build.

**Non-Goals:**

- An import that carries the symbol it was written for (#830). The reader of names stays, with one
  more thing it reads.
- The hand-off as a file a shell never reads, and a list a bar cannot split (#836).

## Decisions

**A node that is left out hands over its conversion.** The reader leaves a node out when nothing of
what it NAMES is written. What is written in its place is a value, and C# may still convert that
value: the dispatcher settles every strategy's result, the inlining one included, and writes a
user-defined conversion around it. That conversion is a call, so it is read as a call is: the model
is asked for the conversion at that node, and its method is recorded with the type that declares it.

Two alternatives were set aside. Recording the node's converted type is close and not the same thing:
a conversion may be declared by its source type, and the type written is the one that declares it.
Recording the parameter types of an indexer and of a constructor initializer would fix the two
callers the review found and leave the next one: the value is converted wherever C# converts it, and
only the node knows where that is.

**The alignment asks the model.** C# takes a constant expression as an alignment and nothing else,
so the model always has its value. One function answers both writers (the hole of an interpolated
string, and the hole of one handed to a `StringBuilder`), which took the source's text each in its
own line. Where the model cannot be asked, the text is what there is, as before: a rewritten node has
no bound tree.

**No project in a handed list is a list that does not hold.** The SDK passes the list only when it
wrote at least one line, so a file with none is a list lost between its write and its read, like the
file that is not there. The fixture handed an empty file for a solution with no library, which the
SDK never does, and hands none now.

## Risks / Trade-offs

- An alignment was text every app's build wrote verbatim. A literal is written as it was. A constant
  by name changes from a name that read as `undefined` to its number, which is the fix. The component
  pins and the conformance suite, executed on both sides, are the net.
- The reader asks the model one more question at each node it leaves out. Those are few: a constant
  written as its value, and `nameof`.
