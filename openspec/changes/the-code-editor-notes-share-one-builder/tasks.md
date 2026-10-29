# Tasks

## 1. One builder

- [ ] 1.1 Add `CodeNoteBuilder`, owning the frame, the measuring, the whole-pixel width, the stacked parts and the placement. Verify: the components project builds with no warning
- [ ] 1.2 Build the hover card, its not-found message and the signature panel through it, and place all three through `CodeNoteBuilder.Place`. Verify: neither card measures, frames or places on its own any more, and the hover card's members are private but `SeverityColor`, which the editor's decorations read
- [ ] 1.3 Regenerate the twins with `EQ_UPDATE_TRANSPILED=1` on `SharedComponentTranspilationTests`. Verify: the twins change only where the C# did, and the suite passes without the variable

## 2. The transpiler bugs the builder met

- [ ] 2.1 Decide a class is a component by its chain of bases wherever the compilation binds it, in the parser and in the resolver (#1164). Verify: `ComponentByItsBasesTests` fails without the change (the builder's twin extends `StatelessComponent`) and with the resolver's half alone reverted, and passes with it; the class over an unbound base is a component on both sides
- [ ] 2.2 Say an `undefined` start is one where TypeScript reads the member as never null (#1169). Verify: `EmittedTypeSurfaceTests.AStructWithNoZeroOfItsOwn_StartsAsTheUndefinedAMemberTakes` fails without the change and passes with it, and the runtime's `tsc` takes `CodeNoteBuilder.ts`
- [ ] 2.3 Run the Compiler and Conformance suites whole. Verify: each passes, read from its exit code and its summary

## 3. Nothing a person sees changes

- [ ] 3.1 Render the goldens `code-problems-*` and `code-signature-*` again with `EQ_UPDATE_GOLDENS=1`. Verify: git sees no change in the four files, byte for byte
- [ ] 3.2 Run `CodeEditorProblemsTests`, `CodeEditorSignatureTests`, `code-editor.spec.ts` (through `-t:TestRuntime`) and the Web, Server and Native.Engine suites. Verify: each passes, read from its exit code and its summary

## 4. Documentation

- [ ] 4.1 One entry under `docs/ledger/` citing #1141, #1164 and #1169
