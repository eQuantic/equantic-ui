# Tasks

## 1. The proof

- [x] 1.1 A conformance case builds `TextRun` with a named argument beside an initializer, every argument named out of order beside one, a named argument alone, every argument positional beside one, and a named argument skipping one beside one, on both sides in both modes. On the code before the cure the three named forms with an initializer lose it

## 2. The cure

- [x] 2.1 The value-type path fills the defaults from the arguments it emitted
- [x] 2.2 The three paths that hand an initializer to the twin as its config share one helper for it
