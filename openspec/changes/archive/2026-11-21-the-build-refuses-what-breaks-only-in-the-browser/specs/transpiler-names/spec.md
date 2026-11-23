## ADDED Requirements

### Requirement: A module never imports one name for two types

eqc SHALL refuse with EQ1005, before any module of it is written, a module that names a class or a
struct of the source whose twin it writes and a type the runtime carries under one twin name, naming
both types, at the module's declaration. What only server code names (a `[ServerOnly]` member, a Server
Action's body), `nameof`, an attribute and a constant written as its value SHALL NOT count. Two
declarations of one name in one file SHALL be refused with EQ1005, naming both full names, when their
namespaces differ, and with EQ2009 when they are the halves of one type. A name a module takes from one
place only SHALL be imported from there.

#### Scenario: The app's record beside the vocabulary's node

- **WHEN** a page builds `new Text(new Shop.Model.Text("y").Body, TypeRole.BodyM)`, `Shop.Model.Text`
  being the app's record
- **THEN** the build fails with EQ1005: `'Text' names two types in the module of 'Home': 'Shop.Model.Text', and the SDK's 'eQuantic.UI.Primitives.Text'. …`

#### Scenario: The app's record alone

- **WHEN** a helper builds `new Shop.Model.Text("y")` and names no type of the SDK's called `Text`
- **THEN** its module imports `Text` from `./Text`

#### Scenario: Server code

- **WHEN** only a page's Server Action body builds the app's `Text`, and its `Build` the vocabulary's
- **THEN** nothing is reported

#### Scenario: Two records of one name in one file

- **WHEN** one file declares `Shop.A.Card` and `Shop.B.Card`
- **THEN** both are refused with EQ1005, naming `'Shop.A.Card' and 'Shop.B.Card'`
