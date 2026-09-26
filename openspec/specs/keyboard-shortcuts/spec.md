# keyboard-shortcuts Specification

## Purpose
Which chord a key press reaches: the page-wide `Shortcut`, a subtree's own, and the name every host
gives the key.

## Requirements

### Requirement: A focus-scoped chord answers for its own subtree

A `Shortcut` with `FocusScoped` set SHALL answer only while the keyboard focus is inside its subtree,
on the web and on Photon, and a key that no scope holds SHALL be left to the page, and on the web to
the browser. A `Shortcut` without it SHALL keep answering wherever the focus is.

#### Scenario: Two subtrees claim one chord

- **WHEN** two focus-scoped shortcuts bind F7 and the keyboard moves from the first to the second
- **THEN** F7 answers for the first, then for the second, and with the keyboard in neither it is not
  taken

#### Scenario: Two diffs on one page

- **WHEN** the keyboard is in the first of two diffs and F7 is pressed
- **THEN** the first diff steps to its next change and the second does not move

### Requirement: A key is named by what it is, on every host

A key press SHALL reach the host under the DOM's name for its key, whatever character it types, so
that a chord written once matches on every host. On macOS a function key SHALL skip the input
method, which composes nothing with it.

#### Scenario: F7 on a Mac

- **WHEN** the macOS shell receives the key code of F7, which types U+F70A
- **THEN** the host receives `F7`
