- **2026-10-29 · A twin holds each member once along its chain**: three bugs under #849 and #1174
  ([#1172](https://github.com/eQuantic/equantic-ui/issues/1172),
  [#1173](https://github.com/eQuantic/equantic-ui/issues/1173),
  [#1175](https://github.com/eQuantic/equantic-ui/issues/1175)). A partial property, indexer or event was written
  twice, its defining half as a member of its own: a class held `level` on each instance over the accessors, so
  `g.Level = 3` read back 3 where C# reads 6, a record's constructor ran the setter and its equality counted the
  state twice, a partial indexer was refused as an overload of itself, and a partial event was written as a field.
  Every emitter now writes the member from its implementing half. A static field beside a static member of its
  name, the backing-field idiom `private static int count;` beside `public static int Count`, was refused, where an
  instance field moves to a slot of its own: it moves to `count$` now, in every type eqc writes a twin of, past the
  statics of its chain, a constant included. Along a class chain only two methods were compared, so a derived
  `size()` beneath a base's `Size` passed the build and answered a function; every pair where one is not a method
  is refused now with EQ1007, naming the base's file and line. Proved against main both ways: the 34 compiler tests
  that ask for the change fail there, and so does the conformance test, by the refusal of the statics and, with
  those set aside, by a partial property's setter that never ran. Proposed and archived through OpenSpec
  (`transpiler-names`, `transpiler-classes`, `transpiler-interfaces`).
