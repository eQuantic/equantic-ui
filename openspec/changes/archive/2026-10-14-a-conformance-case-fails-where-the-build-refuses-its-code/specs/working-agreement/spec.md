# Spec Delta

## ADDED Requirements

### Requirement: A conformance case runs only C# the build accepts

The conformance harness SHALL read, for every case it transpiles, what the build refuses in the
case's C#, where the build reads it and by the build's own rule: the converter's diagnostics over
the text the case would run, an error refusing and a warning not, and the build's own verdict on the
declarations of the case's prelude, every check a build runs beside the converter included. A case
whose C# the build refuses SHALL fail before either side runs, naming each refusal by its id and
text, unless the case declares the refusal it pins. A case that declares one SHALL pass only when its
C# compiles, the build refuses the case's own C# with that id, and every other refusal of the case or
of its prelude carries the same id, and SHALL run nothing on either side.
A path of the harness that compiles a source as an app's build does SHALL read the errors of every
module before it keeps the modules that wrote text.

#### Scenario: A construct the build refuses, measured as a translation

- **WHEN** a case compares `return (from a in new[] { 1, 2 } let f = a * 2 select f).Sum();` with
  .NET
- **THEN** it fails before anything runs, naming EQ2008 and its text, where it ran the query's
  source and answered 3 against .NET's 6

#### Scenario: A case written for a fence

- **WHEN** a case declares that the build refuses that query with EQ2008
- **THEN** it passes, and nothing runs on either side

#### Scenario: The fence lifts

- **WHEN** a case declares a refusal and the build accepts the case's own C#
- **THEN** it fails, saying the build accepts the case's own C# and showing the JavaScript the build
  writes

#### Scenario: A refusal the case does not name

- **WHEN** a case declares EQ2004 and the build refuses its C# with EQ2008
- **THEN** it fails, naming the EQ2008 refusal

#### Scenario: A refusal of the prelude alone

- **WHEN** a case declares EQ1007, its prelude declares a record with two methods of one name, and
  the build accepts the case's own C#
- **THEN** it fails, saying the build accepts the case's own C#, where the prelude's refusal used to
  stand in for it and would have kept the case passing after its own fence lifted

#### Scenario: A prelude refused for the same reason

- **WHEN** a case declares EQ2008, its own C# holds a query with a `let`, and its prelude declares a
  record whose method holds one too
- **THEN** it passes

#### Scenario: A prelude refused for another reason

- **WHEN** a case declares EQ2008, its own C# holds a query with a `let`, and its prelude declares a
  record with two methods of one name
- **THEN** it fails, naming EQ1007 and the record

#### Scenario: A case that is not C#

- **WHEN** a case declares a refusal and its C# does not compile
- **THEN** it fails, saying the case must be C# that compiles

#### Scenario: A warning

- **WHEN** the converter reports only a warning for a case's C#, as EQ2110 for a number read with no
  culture
- **THEN** the case runs and is compared with .NET

#### Scenario: A prelude the build refuses

- **WHEN** a case's prelude declares a record with two methods of one name
- **THEN** the case fails before anything runs, naming EQ1007 and the record

#### Scenario: A module refused before it writes text

- **WHEN** a source the harness compiles as an app's build does holds a class with two methods of
  one name, and no case names the class
- **THEN** the source fails, naming EQ1007 and the class, where the module was dropped for having no
  text and every case passed
