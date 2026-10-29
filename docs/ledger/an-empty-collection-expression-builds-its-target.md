- **2026-10-29 · An empty collection expression builds the collection its target names**: a bug under #849
  ([#1144](https://github.com/eQuantic/equantic-ui/issues/1144)). `Dictionary<string, int> ids = [];`, the form
  the IDE suggests for `new()`, went out as a JavaScript array, and so did a sorted dictionary, a sorted list, a
  queue, a stack and a linked list started with `[]` or `[with(…)]`: a dictionary's first write threw and its
  count was null, a queue had no `enqueue`. eqc builds each one as `new()` builds it now, with the capacity
  `with(…)` hands the constructor, and refuses with EQ1004 an element only an `Add` of the app's own could add.
  It was found measuring a report that a `Dictionary<string, T>` lowers to a plain object, whose `__proto__` key
  goes to the prototype's setter: since #443 every dictionary is the runtime's class, held by a `Map`, and
  conformance tests now pin `__proto__`, `constructor`, `toString`, `hasOwnProperty` and `valueOf` through every
  construction form and through the code diff engine. A lookup's `Contains`, wrong for every key, was found on
  the way and filed as #1145. Proposed and archived through OpenSpec (`runtime-dictionaries`,
  `runtime-collections`).
