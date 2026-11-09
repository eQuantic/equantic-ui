## Context

The SDK holds a `long` as a BigInt everywhere: literals carry `n`, conversions go through
`ValueFlow.Numeric`, the server writes the digits, and hydration revives them with `long(...)`. An
enum was the one 64-bit value held otherwise, and an enum's operators were the one integer operators
`IntegerWidth` never settled, because `IntegerWidth.Of` reads a `SpecialType` and an enum has none.

## Decisions

- **The width decides the representation, once.** `EnumShape.Width` reads the underlying type, and
  `EnumShape.ValueLiteral` writes a value at it: `5`, or `5n` for a 64-bit enum. Every writer of an
  enum's value (a flags member, a default, a constant, a cast's constant, a pattern's bound, the
  shape, the key↔value maps) goes through it, so no writer can disagree with another.
- **The shape says nothing new.** A 64-bit enum's shape already writes `digits: 16`, which the runtime
  reads as "the values are BigInts"; no field was added for the runtime to keep in step.
- **One enumeration conversion.** `EnumConversion` reads every conversion into or out of an enum as
  the numeric conversion between the underlying types (§10.3.3), through `ValueFlow.Numeric`, the
  table every other numeric conversion takes, so a cast, a `foreach` element and an implicit zero
  agree, and the narrowing a number cast to an enum already had (#1196) is that table's too. The bound
  tree reports an enumeration conversion unchecked inside `checked(…)`, so a cast reads the context off
  the syntax (`ArithmeticContext.ChecksOverflow`, the reading that narrowing already made).
- **One home for the operators.** `EnumOperators` computes every operator C# defines on an enum on
  its values and settles the result with `IntegerWidth`, as the underlying type's own operator is
  settled; equality stays `===`, which compares what is held, a BigInt by value.
- **A non-flags 64-bit enum keeps its keys.** A member is its camelCase key on every width, so a
  64-bit enum differs from another only where a VALUE is held: a flags value and a value no member
  names.

Flutter has no answer to borrow here: a Dart enum is an object with an index and no underlying type,
so the rule is .NET's own, read from the C# specification and measured with the conformance suite.

## Risks

- **The wire changes for a 64-bit flags enum**: a JSON string where a number was. The transpiled
  client is the only reader that changes, and the server still reads a number from any other client.
