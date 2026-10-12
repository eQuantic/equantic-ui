# Proposal

Closes #851, a Bug under #160 (Instruments that fail, not warn).

## Why

`scripts/check-commit-messages.sh` refuses, by name, the shapes the first leaks of a tool's
attribution into a commit message took: a wrapper's tags, a generated-with line with its emoji and
an assistant's co-authorship. A pull request opened from a Claude Code project carries three other
shapes in its body: a comment naming the attribution, a link to the conversation it was requested
in, and a signature line that links to the tool, in two of them to a session. A squash merge takes
the body, and six squash commits on main carry all three (7c9c3951, 18509220, 20069215, 35263110,
f11bd062 and c7f8bc04), each one passed by the job that exists to refuse it. In the last two the
squash composer wrapped the body at 72 columns and broke the signature's link text in two, so a rule
that reads one line at a time was also one wrap away from missing a named phrase.

## What Changes

- The guard's named list gains the three shapes: the attribution comment by its token, any link into
  the tool's own pages (`claude.ai/code`, a session's and a conversation's included) and a
  generated-by line naming the tool. A commit has no reason to link there; prose that names the
  tool, or quotes CLAUDE.md's first line, writes no scheme and stays clean.
- The named list is read once more over the message with its line breaks taken out, so a phrase a
  wrap split is refused as one on a single line is; such a match is reported as `wrapped:`.
- The self-test gains a fixture that isolates each new entry, one for the reading across a line
  break, and one of prose that names the tool, which must pass; it counts its fixtures rather than
  printing a number written by hand.

## Impact

- `scripts/check-commit-messages.sh`.
- The six squash commits stay on main, whose history is not rewritten: the job reads a push's own
  range, so it reports the next one and none of these.
