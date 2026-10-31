- **<date> · A value held as object keeps the type its answers depend on**: bugs under #849
  ([#552](https://github.com/eQuantic/equantic-ui/issues/552),
  [#742](https://github.com/eQuantic/equantic-ui/issues/742),
  [#768](https://github.com/eQuantic/equantic-ui/issues/768),
  [#954](https://github.com/eQuantic/equantic-ui/issues/954)). The browser holds an enum member as its key, a float
  as a number, a list, an array and a tuple as one array, and an anonymous object as plain data, so a value boxed
  into `object` was whatever it turned out to be: a member was a string of its name, a float printed a double's
  digits, two lists compared by their items, and an anonymous object with a color's members was that color. Edgar
  chose the box and the mark over documenting the four: an enum member and a float are boxed with their type
  (`utils/box.ts`) at every boxing conversion `ValueFlow` sees, every unboxing asks the box, a list and an array
  are marked as compared by reference where they go into `object`, and an anonymous object is marked where it is
  made (`utils/marks.ts`). JSON writes the boxed value as before. An int and a double, a char and a string, a long
  and a ulong held as `object` stay one kind each. Proved by the conformance suite on both sides. Proposed and
  archived through OpenSpec (`transpiler-expressions`).
