- **2026-11-09 · An enum is held and combined at its underlying type's width**: two Bugs under #164
  ([#551](https://github.com/eQuantic/equantic-ui/issues/551),
  [#555](https://github.com/eQuantic/equantic-ui/issues/555)). A `long`- or `ulong`-backed enum was held
  in the browser as a JavaScript number, the one 64-bit value the SDK held otherwise, so a member past
  2^53 was a number that is not its value, in its casts, its text, its statics and on the wire; and an
  enum's operators were JavaScript's own, in signed 32 bits, so a `uint` flag's high bit read negative
  and a long's flags past bit 31 vanished. A 64-bit enum is a BigInt now, every writer of an enum's value
  goes through its width (`EnumShape`), every conversion into or out of an enum is the numeric
  conversion between the underlying types (`EnumConversion`), and every operator C# defines on an enum
  computes on its values at that width (`EnumOperators`), `HasFlag`, `CompareTo` and a relational pattern
  included. The server writes a 64-bit flags enum as its digits and a page's top-level enum field
  through EqJson, where a flags field crossed by its members' names. Measured on main, 32 of the 47
  conformance cases first fail there. Proposed and archived through OpenSpec (`transpiler-bcl`).
