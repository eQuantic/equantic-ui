# Tasks

## 1. The runtime's dictionary

- [ ] 1.1 A dictionary class with .NET's slots and freed-slot stack, found by identity or by value, whose pairs destructure and carry `.key` and `.value`, with `toJSON` and reference `equals`
- [ ] 1.2 Retire the plain-object helpers (`entries`, `dictGet`) and the old `ValueMap`, and build `ToDictionary`'s result as the class
- [ ] 1.3 Hydration builds the class from `{ dict, key }`, its keys in their type
- [ ] 1.4 Check: runtime specs for slot order, removal and re-insertion, typed keys, both comparisons and the wire

## 2. One lowering

- [ ] 2.1 One map-backed owner for every `Dictionary`, `IDictionary` and `IReadOnlyDictionary`, its factory chosen by the key's comparison, and the plain-object lowering removed
- [ ] 2.2 Construction by copy and by either initializer, `foreach` and deconstruction over pairs, and `TryAdd`, `ContainsValue`, `Keys.Contains` and `Keys.Count`
- [ ] 2.3 The entry as a target of `?[k] =` and `out`, on the class
- [ ] 2.4 `ToDictionary` keyed by comparison, and a hydration spec with keys for every dictionary
- [ ] 2.5 Dictionary annotations degrade to `any`
- [ ] 2.6 Check: conformance cases run on both sides over insertion order, removal and re-insertion, int and string keys, `Keys`, `Values`, `foreach`, `Count`, `ContainsKey`, `TryGetValue` and `ToDictionary`, failing before

## 3. The DOM escape hatch and the other hand-written readers

- [ ] 3.1 `BuildAttributes` and `BuildEvents` answer the class, and the renderer, `DynamicElement`, analytics and route values read a bag in either form
- [ ] 3.2 Check: the runtime's renderer specs and the web suite

## 4. Documentation

- [ ] 4.1 The six twins regenerated, and the served runtime's budget recording the new size
- [ ] 4.2 `docs/DOTNET-COVERAGE-PROGRAM.md` and the wiki's SupportedFeatures dictionary rows in English and Portuguese, published at the merge
- [ ] 4.3 One `docs/LEDGER.md` line citing #435
- [ ] 4.4 Archive this change in the same pull request
