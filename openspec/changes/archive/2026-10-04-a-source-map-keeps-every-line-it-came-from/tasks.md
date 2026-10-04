# Tasks

## 1. A JSON file is JSON (#525)

- [x] 1.1 Escape every string `JsonWriter` writes, a member's name included, with System.Text.Json's encoder, and give it an array of strings
- [x] 1.2 Write eqc's map and the composed map through one map writer on top of `JsonWriter`
- [x] 1.3 Check: `CodeWriterTests.AStringHoldingEveryControlCharacter_IsJsonThatReadsBackAsWritten` and `TheWebManifest_ForANameHoldingATabAndACarriageReturn_IsJson`, `SourceMapModeTests.ASourceHoldingEveryControlCharacter_MapsToJsonThatReadsBackAsTheSource` and `AFileNameHoldingAQuote_IsNamedAsItIs` fail on main at 7d768a85 (`'0x00' is invalid within a JSON string`) and pass here; `SourceMapGeneratorTests.EveryControlCharacter_InANameAndAContent_ReadsBackAsWritten` and `SourceMapComposerTests.AContentHoldingEveryControlCharacter_ComposesToJson` hold the two map writers to it

## 2. A map names every file (#490)

- [x] 2.1 Carry each mapping's tree, number every file the mappings reach, and describe each one (its name inside the project, its text in a full map)
- [x] 2.2 Check: `SourceMapSourcesTests` (a default `IValidating.cs` supplies, written into Form's module) fails on main, where the map names Form.cs alone and leads the default's lines into it, and passes here; `SourceMapGeneratorTests.TwoFiles_AreTwoSources_AndEachSegmentNamesItsOwn` and `TwoTreesOfOnePath_AreOneSource`, and `SourceMapComposerTests.AnInnerMapOfTwoFiles_LeadsEachSegmentToItsOwnFile`, which holds the composition to the file index it already kept
- [x] 2.3 Check against the real pipeline: `StackFrameSourceMapTests.AFrameInsideADefaultAnInterfaceSupplies_LeadsToTheInterfacesFile` bundles with the embedded Bun and reads the thrown frame through the composed map: on main it read as line 12 of a ten-line Checked.cs, here as the throw in IChecked.cs

## 3. A body a lowering wraps (#487)

- [x] 3.1 Build the arrow an `out` or `ref` parameter's body runs in as IR, one builder for a method, a lambda and a local function, its lines the declaration's; and an iterator's buffer as IR
- [x] 3.2 Check: `StatementSourceMapTests.AStatementOfABodyWithAnOutOrRefParameter_MapsToItsOwnCSharpLine` (12 lines: an `out` method, a `ref` method, a lambda and a local function with `out`) and `AStatementOfAComponentsMethodWithAnOutParameter_MapsToItsOwnCSharpLine` (3) fail on main and pass here
- [x] 3.3 Check against the real pipeline: `StackFrameSourceMapTests.AFrameInsideABodyWithAnOutParameter_LeadsToItsOwnLine` leads the throw to its line, where main led it to the method's head

## 4. An expression body and a creation (#492)

- [x] 4.1 Give a member's expression body the concise body's one lowering, which knows a body that returns nothing, and stop converting inside `InBlock` at each caller
- [x] 4.2 Build an object creation, an object or collection initializer and an anonymous object as IR (`JsNew`, `JsObject`), and take the three strategies off the text baseline (81 to 78)
- [x] 4.3 Check: `StatementSourceMapTests.AStatementInALambdaThatAnExpressionBodyOrACreationHolds_MapsToItsOwnCSharpLine` (14 lines) and `AStatementInALambdaThatAComponentsExpressionBodyHolds_MapsToItsOwnCSharpLine` (3) fail on main and pass here; `LambdaStatementMapTests` counts 0 statements without a line in the shared components, where the baseline held 59; every transpiled pin holds, changed in whitespace only
- [x] 4.4 Check against the real pipeline: `StackFrameSourceMapTests.AFrameInsideALambdaAnExpressionBodyHolds_LeadsToTheStatementThatCalled` and `AFrameInsideALambdaACreationHolds_LeadsToTheStatementThatCalled` lead the lambda's frame to the statement that called, where main led it to the line holding the expression body and the creation

## 5. Documentation

- [x] 5.1 One `docs/LEDGER.md` line citing #487, #490, #492 and #525
- [x] 5.2 The wiki's Debug and Compiler pages, English and Portuguese, on a wiki branch named like this pull request's, saying what maps now and what still maps to its statement
- [x] 5.3 Check: `./scripts/check-openspec.sh` validates the change strictly, and the web suite's documentation guards read the wiki branch (`EQ_WIKI_DIR`)
