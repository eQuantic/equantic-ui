# Tasks

## 1. The rule asks the symbol

- [x] 1.1 `ComponentParser.StaysOnTheServer` takes the model of the compilation that holds the node, asks the method's symbol, and reads the attribute lists by name only where no model holds the tree; a member that writes no attribute and is no partial method binds nothing. Verify: `ServerOnlyMethodBySymbolTests`, failing before the change with EQ2112 and EQ2103 in every case
- [x] 1.2 Every reader hands it its model: the parser's component path, the class, static-class and record emitters, method lowering, `NamedSourceTypes`, `RuntimeProvidedTypeScanner` and `ServerOnlyContracts`. Verify: the twin and the module's imports in `ServerOnlyMethodBySymbolTests`, and the EQ2010 refusal of a client call
- [x] 1.3 The record emitter leaves a server-only interface default out in the arm that writes a default, as the class emitter does, where the arm's condition sent it to the refusal of a body nothing could read (EQ1008), the attribute written on it included. Verify: `ServerOnlyMethodBySymbolTests.AServerOnlyMemberKnownByItsSymbol_IsLeftOutOfItsTwin`, failing before with EQ1008
- [x] 1.4 The EQ1007 guard asks the same rule with its model. Verify: `OverloadedMethodTests.AServerOnlyMethodKnownByItsSymbol_TakesNoName`, failing before the change with EQ1007

## 2. Proof against the real thing

- [x] 2.1 The Compiler and Conformance suites whole, and every case built as eqc builds an app (`Solution`), as well as compiled on its own as the playground compiles it

## 3. Documentation

- [x] 3.1 A ledger entry under `docs/ledger/` citing #1140. No wiki page changes: they already say a `[ServerOnly]` member never crosses, which this makes true for every spelling of the attribute
