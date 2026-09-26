# Tasks

## 1. The runtime's dictionary

- [x] 1.1 A dictionary class with .NET's slots and freed-slot stack, found by identity or by value, whose pairs destructure and carry `.key` and `.value`, with `toJSON`, reference `equals`, `tryAdd` and `containsValue`
- [x] 1.2 Retire the plain-object helpers (`entries`, `dictGet`) and the old `ValueMap`, and build `ToDictionary`'s result as the class
- [x] 1.3 Hydration builds the class from `{ dict, key, byValue, sorted }`, its keys in their type, read against the payload EqJson writes
- [x] 1.4 Check: runtime specs for slot order, removal and re-insertion, typed keys, both comparisons and the wire

## 2. One lowering

- [x] 2.1 One owner, `DictionaryStrategy`, for every `Dictionary`, `IDictionary`, `IReadOnlyDictionary`, `SortedDictionary` and `SortedList`, its factory chosen by the type and the key's comparison, and the plain-object lowering removed
- [x] 2.2 Construction by copy and by either initializer, `foreach` and deconstruction over pairs, and `TryAdd`, `ContainsValue`, `Remove(key, out value)`, `Keys.Contains` and `Keys.Count`
- [x] 2.3 The entry as a target of `?[k] =`, and a property pattern's `Count`, on the class (an indexer is never an `out` in C#)
- [x] 2.4 `ToDictionary` keyed by comparison, and a hydration spec with keys for every dictionary
- [x] 2.5 Dictionary annotations degrade to `any`, and a read from one is `any`
- [x] 2.6 Check: conformance cases run on both sides over insertion order, removal and re-insertion, int and string keys, `Keys`, `Values`, `foreach`, `Count`, `ContainsKey`, `TryGetValue` and `ToDictionary`, 34 of 52 failing before

## 3. The DOM escape hatch and the other hand-written readers

- [x] 3.1 `BuildAttributes` and `BuildEvents` answer the class, and the renderer, the lowering's foreign seam, `DynamicElement`, analytics and route values read a bag in either form
- [x] 3.2 Check: the runtime's renderer specs and the web suite

## 4. Documentation

- [x] 4.1 The six twins regenerated, and the served runtime's budget recording the new size
- [x] 4.2 `docs/DOTNET-COVERAGE-PROGRAM.md` and the wiki's SupportedFeatures and Compiler dictionary passages in English and Portuguese, on the wiki branch named like the pull request's
- [x] 4.3 One `docs/LEDGER.md` line citing #435
- [ ] 4.4 Archive this change in the same pull request
