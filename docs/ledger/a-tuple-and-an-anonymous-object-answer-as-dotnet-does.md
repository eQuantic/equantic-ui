- **2026-10-14 · A tuple and an anonymous object answer Equals and ToString as .NET does**: a value the
  browser holds without a class of its own answered its object members as JavaScript does. An
  anonymous object's `Equals` threw `equals is not a function`, as a call and as a method group
  ([#743](https://github.com/eQuantic/equantic-ui/issues/743)), and its text was `[object Object]`; a
  value tuple's text was `Array.prototype.toString`'s `1,2` where .NET writes `(1, 2)`, through
  `ToString`, a hole, a concatenation, `Join`, `Format`, a builder, `Convert`, `Zip`'s pairs and a
  record's member ([#762](https://github.com/eQuantic/equantic-ui/issues/762)), and its `Equals` method
  group and a `Nullable` tuple's `Equals` threw. eqc now writes both texts from the static type, each
  element and member as its type writes it in the culture in force, through `StringConversion`, which
  `ValueFlow` already called for a value on its way into text; a hole with a format or an alignment and
  `string.Format` hand the formatter that text, as neither type is `IFormattable`. An anonymous
  object's `Equals` is the comparison generated from its members' types, which takes only another
  anonymous object of the same members in the same order, and `StructuralEqualsStrategy` crossed to the
  IR. 63 of the 69 conformance cases fail at 85b0ab52, in the invariant culture, pt-BR and sv-SE; the six
  that pass are the `==`, `GetHashCode` and tuple `Equals` rows that already answered as .NET, and a fix
  sending an anonymous object's `Equals` to `$eq.equals` fails three of the others. A tuple boxed into a
  concatenation is read where it stands, without the copy a boxing takes, which taken around its text
  spread the string into its characters, `(,1,,, ,2,)`, while this was measured. A value held as
  `object` still prints as the browser holds it. A `Color` and a `Curve`, held as data too, write their
  record text in an aligned hole, a hole with a format and `string.Format`, where they wrote
  `[object Object]`, and `Distinct` over anonymous objects keeps the first of each, where a `Set` kept
  every one. The served runtime grows about 170 gzipped bytes.
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-records`).
