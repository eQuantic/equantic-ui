# Tasks

## 1. A class's state defined, then written

- [x] 1.1 A plain class declares its state as class fields with no initializer, from the list its constructor starts (`ClassBuilder.State`). Verified by a conformance case for `int count; int Count => count;`, which threw on this branch before it and passes on #608's head and here, and by the embedded bun bundling `count!: number;` as a class field

## 2. Where a property keeps its value

- [x] 2.1 One rule (`PropertyStore`): a store of its own for an auto-property declared `virtual` or `override` and for a property with a backing field and an accessor of its own, the property's own name for every other auto-property, nothing for an abstract or a computed one
- [x] 2.2 A class's property that keeps a store: accessors over it, the compiler's where C# writes no body, a setter for a get-only one; its constructor starts the store. Verified by conformance cases for an auto-property over a computed one, the reverse, two auto-properties, an override that reads its base's and an abstract base, 4 of 7 failing on #608's head and green here
- [x] 2.3 A record's and a struct's: the store declared, started, compared, hashed and zeroed, accessors over it, the bodies of a property that uses `field` written. Verified by conformance cases for #591's two shapes, a `with` over a computed override, two auto-properties, and #615's record, getter and struct, 6 of 7 failing on #608's head (3 throwing) and green here
- [x] 2.4 An override that declares one accessor forwards the other to its base. Verified by the conformance case writing through `override string Kind => "d"`

## 3. JSON

- [x] 3.1 `$eq.json` writes a store under its property's name, read through the property, copies a `$` key that names no member as it is, and defines every key. Verified by its runtime test
- [x] 3.2 A twin that keeps a store has `toJSON`, inherited by the types derived from it

## 4. The runtime and the real thing

- [x] 4.1 Regenerate the runtime's transpiled classes (`EQ_UPDATE_TRANSPILED=1`), run `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, the Compiler, Server, Web and Conformance suites
- [ ] 4.2 The dashboard sample in a browser: the code editor (its languages keep their rules in stores), the form, the sheet and the markdown screen exercised, with no console error

## 5. Documentation and archive

- [x] 5.1 The wiki's SupportedFeatures page in English and Portuguese, on the wiki branch named like this change's branch: an overridable property's store, and `base.Name` over an overridden auto-property as the one difference left
- [ ] 5.2 One docs/LEDGER.md line, and the change archived with `openspec archive`
