# Tasks

## 1. A fragment a fraction of a pixel above the top

- [x] 1.1 Measure the failing warm load on equantic-web with DevTools logpoints: the target at -0.203125 on every frame, refused by the band
- [x] 1.2 Start the correction's band one pixel above the top, as a named constant carrying the measurement (`sticky-offset.ts`)
- [x] 1.3 Pin it in `sticky-offset.spec.ts`: the measured fraction corrected by the measurement and by the frame window, and a reader a whole pixel past the target left alone; the first two fail against main's band
- [x] 1.4 Prove it on the real site: equantic-web on 0.2.0-preview.60, published, with the package's Server and then with this branch's (the embedded runtime the only difference), cold and warm, through the parity session's `anchors.cjs`: 4/6 and then 6/6

## 2. A dictionary built with a comparer that asks for the default

- [x] 2.1 Skip the comparer argument in the dictionary construction, read from the bound operation, and give a sorted dictionary or list the order its comparer asks for (`DictionaryStrategy.cs`)
- [x] 2.2 Strengthen `CollectionComparerFenceTests`: a comparer that asks for the default builds with no error at all, and one that changes equality fails with EQ2007 and no EQ1004; 10 of its 15 cases fail against main's strategy
- [x] 2.3 Run both sides in `DictionaryEnumerationConformanceTests`: ordinal, copied, with a capacity, the default comparer, a tuple value, and sorted with the ordinal and the default comparer; all 9 fail against main's strategy

## 3. Documentation and the suites

- [ ] 3.1 The wiki's Upgrading page, English and Portuguese, on the wiki branch of this pull request: the ordinal dictionary's workaround for an app on 0.2.0-preview.60
- [ ] 3.2 `docs/LEDGER.md`: one line for this event citing #576 and #577, and the 0.2.0-preview.60 release line
- [ ] 3.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
