## 1. Style

- [ ] 1.1 `style:` on the twelve components, of their family's record, merged where the component read its slot; `calendarStyle:` on `DatePicker` and `DateTimePicker`; handed down by `FormInput`, `CultureSwitcher` and `DateTimePicker`; carried by `AdoptConfig`
- [ ] 1.2 The factories in `UI.cs` mirror the widened constructors; `./scripts/public-api.sh update`
- [ ] 1.3 Check: the family's `style` entries leave `inputs-and-pickers.baseline.txt` (`EQ_UPDATE_CATALOG_CUSTOMIZATION=1`), and a test per record shows a style the instance sets win over the theme's on Photon and on the web, failing without the merge

## 2. Builders

- [ ] 2.1 `optionBuilder` on `Select` (grouped or not, over the flat index) and `segmentBuilder` on `SegmentedControl`, drawn inside the part's chrome, the text kept as the accessible name
- [ ] 2.2 Check: tests open a select with a builder and pick an option, on Photon and on the web, and read a segmented control's segments, each failing without the builder

## 3. Both targets

- [ ] 3.1 The transpiled twins regenerated; the conformance suite and the variant pins unchanged with no style and no builder
- [ ] 3.2 Check: the Components, Web, Photon and Conformance suites pass, and vitest

## 4. Docs and archive

- [ ] 4.1 The wiki's Components page in every language it is written in; a ledger entry citing #1548
- [ ] 4.2 `./scripts/check-openspec.sh` passes, then `openspec archive inputs-and-pickers-take-a-style-and-builders --yes`
