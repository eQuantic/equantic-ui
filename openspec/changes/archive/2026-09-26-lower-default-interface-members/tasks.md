# Tasks

## 1. The defaults a class takes

- [x] 1.1 Find them with `FindImplementationForInterfaceMember`, skipping what a base class takes, and carry the private members a default calls
- [x] 1.2 Convert a body under its interface's file (`CSharpToJsConverter.InFileOf`), and ask the node's own file's model for types
- [x] 1.3 Write them in the plain-class, record and component emitters, and scan them for imports
- [x] 1.4 Check: the conformance cases run on both sides, 9 of 9 failing without the change and passing with it

## 2. The defaults it cannot read

- [x] 2.1 Warn with EQ1008 for a default whose interface is compiled into a referenced assembly the runtime does not carry
- [x] 2.2 Document EQ1008 in `docs/DIAGNOSTICS.md` and regenerate the diagnostics baseline
- [x] 2.3 Check: the emission tests compile an interface into a separate assembly and see the warning, 4 of 4 failing without the change

## 3. The vocabulary's defaults

- [x] 3.1 Carry them in the runtime's `interface-defaults.ts`, exported under each interface's name
- [x] 3.2 Delegate a vocabulary default from metadata to its copy, in the plain-class, component and record emitters
- [x] 3.3 Cross-pin the list: reflection writes the fixture, and the runtime spec needs a function for each line
- [x] 3.4 Check: the site under this branch's eqc goes from three EQ1008 to none, its theme delegating, and the dashboard sample builds with none

## 4. The regression

- [x] 4.1 Regenerate the transpiled pins: `PlainTextLanguage` gains `rules`, and no other twin changes
- [x] 4.2 Render a plain-text `CodeBlock` on the client in the runtime suite
- [x] 4.3 Check: against the old twin the block throws the site's `Cannot read properties of undefined (reading 'indentWidth')`

## 5. Documentation

- [x] 5.1 The wiki's Diagnostics and SupportedFeatures pages in English and Portuguese, published at the merge
- [x] 5.2 One `docs/LEDGER.md` line citing #414
- [x] 5.3 Archive this change in the same pull request, so `openspec/specs` matches the code
