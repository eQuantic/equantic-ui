- **2026-10-14 · A ToString method group and a composite key answer as .NET does**: a LINQ operator that
  holds its keys or its elements held them in a JavaScript `Map` or `Set`, which find a key by
  reference, and `Distinct` found a record or a struct by the text `JSON.stringify` writes of it. A join
  by an anonymous object or a tuple key matched nothing, `Union`, `Intersect`, `Except` and the `By`
  forms kept every anonymous object and tuple, a record, a decimal and a date were compared by
  reference, a class that overrides `Equals` was never asked it, two records holding different arrays of
  the same items were one by their JSON, and `GroupBy` made each NaN a group of its own
  ([#922](https://github.com/eQuantic/equantic-ui/issues/922)). Every operator now reads the decision
  `ElementEquality` makes for a dictionary, a set, `Contains` and `IndexOf`: a join and the set
  operators hold their keys in the runtime's dictionary and set by the key type's equality, `Distinct`
  too, and `GroupBy`, `ToLookup` and a lookup's indexer compare a key by the comparison it writes, with
  `===` kept where that answers the same. A null key matches nothing in a join, as .NET's join lookup
  skips it, where a `Map` paired two null keys, a string's on main already. The `ToString` method group
  of an array, a list, a dictionary, a set, a delegate and an enum wrote JavaScript's text where the
  call writes the type's or the member's name
  ([#913](https://github.com/eQuantic/equantic-ui/issues/913)), and now writes what the call writes,
  through `$eq.text.group`. Both were found by the second round of review on #906, which merged before
  they did. 19 of the 21 conformance cases fail at dfce1158, the two that pass being a null array's
  group and a dictionary keyed by a tuple that already answered as .NET, and the first version of this
  fix, which held by value only what `LinqKeys.ComparesByValue` names, fails 7 of them. Proposed and
  archived through OpenSpec (`openspec/specs/transpiler-sequences`,
  `openspec/specs/transpiler-expressions`).
