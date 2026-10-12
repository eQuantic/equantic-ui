# Tasks

## 1. A value written for a constant keeps its conversion (#833)

- [x] 1.1 Write the cases over a solution on disk: a constant and `nameof` handed to an indexer over a converted type import the type, and a field handed there is the control
- [x] 1.2 The reader of a module's names records the conversion of a node it leaves out
- [x] 1.3 Take the change back and confirm both cases fail, then run the referenced-project tests

## 2. An interpolation hole's alignment is its value (#834)

- [x] 2.1 Write the conformance cases, executed in .NET and in JavaScript: a constant by name, a negated one, a sum, and an interpolated string handed to a `StringBuilder`
- [x] 2.2 One function writes the alignment from the model's constant, for both writers
- [x] 2.3 Take the change back and confirm the cases fail, then run the conformance suite and read the component pins for a change

## 3. A handed list that holds no project stops the build (#835)

- [x] 3.1 Write the case: an empty handed list is EQ0001, with the cases of the list that does not hold
- [x] 3.2 The reader of the list throws on it, and the fixture hands no list for a solution with no library
- [x] 3.3 Take the change back and confirm the case fails

## 4. Proof and documents

- [x] 4.1 Run the Compiler, Web, Server and Conformance suites, build the dashboard sample and compare it file by file with a build of main, and build the shared-library sample and run its check
- [x] 4.2 One `docs/LEDGER.md` line citing the three issues, and the wiki's EQ0001 row in English and Portuguese
- [x] 4.3 Archive the change with `openspec archive a-constant-keeps-its-conversion --yes`
