- **2026-10-29 · Every member of a type holds a name of its own on its twin, whatever kind it is**: a bug under
  #849 ([#1146](https://github.com/eQuantic/equantic-ui/issues/1146)). EQ1007 refused two methods of one name and
  a record's state beside a member of its name; every other pair that lowered to one key of the twin passed the
  build, and the browser kept one of them. Measured on every emitter: a static field beside a static property or
  method of its name, which took its place when the class was defined, a component's field beside its property,
  whose getter it hid, a property beside a method, an event beside either, an extension block's property or
  indexer beside a method, two `operator +` of one type, a conversion beside `FromInt`, and members on names the
  twin writes itself (`With(int)` on a record, `ToString(string)`, `Constructor()`, `static Prototype`). A new pass
  puts every member on the key the emitters write it under and refuses the second, keeping what the twin holds as
  one member (a moved field, a property's accessors, an override of `Equals(object)`, `GetHashCode()` or
  `ToString()`). The record emitter now writes such an override in place of its own and nothing else there, and a
  `[ServerOnly]` override, which is no member of the twin, leaves the twin's own in place: a server-only
  `GetHashCode()` had left a record no hash at all, so two equal records hashed apart. Every
  SDK source the runtime embeds, every sample and every template transpile with it, refused nowhere. Proposed and
  archived through OpenSpec (`transpiler-names`).
