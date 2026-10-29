## Why

Closes #1168, a bug under #849 (the transpiler's fences hold on every path, part three), under the epic #157,
found reviewing #1167 (#1145). `LookupStrategy` answered the indexer and `Contains` of
`System.Linq.ILookup<TKey, TElement>`, the type `ToLookup` answers, and nothing else. A receiver typed as the
concrete `System.Linq.Lookup<TKey, TElement>`, the class `ToLookup` builds, reached by a cast, an `as` or a type
pattern, was not claimed:

- its `Contains(key)` fell to `ContainsStrategy`, which looked for an ELEMENT equal to the key among the groups and
  answered false for every key: `((Lookup<string, string>)new[] { "a", "b" }.ToLookup(s => s)).Contains("b")` was
  `True` in .NET and `False` in the browser;
- its indexer fell to the element access of an array, which answered `undefined` for a key, so
  `l["o"].Count()` threw where .NET counts the group, and an absent key threw where .NET answers an empty sequence.

`ContainsStrategy` took every `Contains` a `System.Linq` type declares, because it asked the method's namespace and
not whether the method is an extension. A lookup's own `Contains` was taken for LINQ's by priority order alone.

Measured first: a cast to `Lookup<,>` lowers to the value as it is, an `as` to the value or null, and
`is Lookup<,> lk` to a test that the value is not null, so the receiver on the web is the lookup's array of groups
in every form, and only the members were wrong.

## What Changes

For a developer using the SDK, nothing to write: a lookup held as its class answers in the browser what it
answers in .NET.

- **A concrete lookup answers its indexer and `Contains` by its keys**, as the interface does: the indexer
  answers the group of that key or an empty sequence, `Contains` whether there is one, with the key type's
  equality and the key evaluated once.
- **`ContainsStrategy` takes LINQ's `Contains` only**, an extension `System.Linq` declares. A type there with a
  `Contains` of its own is another member: with no strategy claiming a lookup's, the call is EQ2004 at build, where
  it was a quiet `false`.

The part it reaches: eqc (`LookupStrategy`, `ContainsStrategy`). The runtime does not change, and neither does the
public surface of any assembly or the developer surface. Nothing an app wrote breaks: the calls that change threw
or answered false for every key on the web.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-sequences`: a lookup's `Contains` asks for a group of that key whether the receiver is typed as the
  interface or as the class, and so does its indexer.

## Impact

- `src/eQuantic.UI.Compiler/CodeGen/Strategies/Linq/LookupStrategy.cs`, `ContainsStrategy.cs`
- `tests/eQuantic.UI.Conformance.Tests/CollectionTailConformanceTests.cs`
- The wiki's SupportedFeatures page, in the five languages it is written in
