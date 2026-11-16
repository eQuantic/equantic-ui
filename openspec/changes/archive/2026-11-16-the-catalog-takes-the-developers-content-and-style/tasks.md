## 1. The design

- [x] 1.1 Answer #1545's three questions and settle content, builders, style and constructor parameters in `design.md`
- [x] 1.2 Re-cut the families by slot record, and cut #1577 for the content defaults every family's content needs

## 2. The rule where every session reads it

- [x] 2.1 CLAUDE.md, beside the product principle: "Every component is composed, and takes the developer's content, builders and style"
- [x] 2.2 `docs/PRINCIPLES.md`: its short form, principle 15

## 3. The guard

- [x] 3.1 `CatalogCustomizationTests`: the census of text members and missing styles over the catalog and its item types, ownership, placement, decided reasons, and a regenerator that only removes
- [x] 3.2 Nine family files, one per family, every entry written by hand with its reason (173 on today's tree)
- [x] 3.3 Prove it fails against the real catalog, built: a new `string` property on `Card`, a new component no family owns, an entry moved to another family, an undecided reason and a stale entry each fail, the regenerator removes the stale entry and nothing else, and today's tree passes

## 4. Docs and archive

- [x] 4.1 A ledger entry citing #1546, as a file of its own under `docs/ledger/`
- [x] 4.2 Validate with `openspec validate --all --strict`, then archive the change into `openspec/specs/catalog-customization`
