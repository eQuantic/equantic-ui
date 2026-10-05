# Tasks

## 1. A fragment a fraction of a pixel above the top

- [x] 1.1 Measure the failing warm load on equantic-web with DevTools logpoints: the target at -0.203125 on every frame, refused by the band
- [x] 1.2 Start the correction's band one device pixel above the top, never less than one CSS pixel, carrying the measurement in its documentation (`sticky-offset.ts`)
- [x] 1.3 Pin it in `sticky-offset.spec.ts`: the measured fraction corrected by the measurement and by the frame window, a reader a whole pixel past the target left alone, and the same two edges on a page zoomed out to a device-pixel ratio of 0.5; the corrections fail against main's band, and the zoomed one against a fixed one-pixel band
- [x] 1.4 Prove it on the real site: equantic-web on 0.2.0-preview.60, published, with the package's Server and then with this branch's (the embedded runtime the only difference), cold and warm, through the parity session's `anchors.cjs`: 4/6 and then 6/6

## 2. A dictionary built with a comparer that asks for the default

- [x] 2.1 Skip the comparer argument in the dictionary construction, read from the bound operation, and give a sorted dictionary or list the order its comparer asks for (`DictionaryStrategy.cs`)
- [x] 2.2 Strengthen `CollectionComparerFenceTests`: a comparer that asks for the default builds with no error at all, and one that changes equality fails with EQ2007 and no EQ1004; 10 of its 15 cases fail against main's strategy
- [x] 2.3 Run both sides in `DictionaryEnumerationConformanceTests`: ordinal, copied, with a capacity, the default comparer, a tuple value, sorted with the ordinal and the default comparer, named arguments out of parameter order, and the pairs a constructor copies; the first 9 all fail against main's strategy
- [x] 2.4 Answer "which argument is the comparer" once (`IsCollectionComparer`), for the fence, the dictionary construction and the sorted set's, and make `DictionaryStrategyTests` pin the fence's verdict where it pinned #443's refusal (found in review)

## 3. Documentation and the suites

- [x] 3.1 The wiki's Upgrading page, English and Portuguese, on the wiki branch of this pull request: the ordinal dictionary's workaround for an app on 0.2.0-preview.60
- [x] 3.2 `docs/LEDGER.md`: one line for this event citing #576 and #577, and the 0.2.0-preview.60 release line
- [x] 3.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
