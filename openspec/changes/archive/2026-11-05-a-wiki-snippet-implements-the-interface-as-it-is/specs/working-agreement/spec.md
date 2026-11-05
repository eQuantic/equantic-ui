## ADDED Requirements

### Requirement: A wiki snippet implements an SDK interface as the interface is

Every C# block of every wiki page, in every language the page is written in, SHALL be compiled against
the SDK's assemblies, and a type it declares over an interface of the SDK SHALL declare each member it
names like one of that interface's with the interface member's signature: its kind, its type with its
nullability, and a method's parameters. A member the block leaves out SHALL NOT be an offence, since a
block is a fragment, and a type the compilation cannot bind SHALL compare by the name the page wrote.
An interface `src/` declares that the compilation cannot resolve SHALL be an offence, so that a snippet
over it is never passed unread. The guard SHALL fail when it compared no member.

#### Scenario: A theme example the interface moved away from

- **WHEN** a copy of the DesignSystem page writes `public string? TextFamily => "…"` in a theme over `IAppTheme`, whose `TextFamily` is a `FontFamily?`
- **THEN** `WikiImplementationsTests` fails, naming the copy, its line, the type written and the interface's

#### Scenario: An interface the test host does not carry

- **WHEN** a snippet implements an interface `src/` declares in an assembly `eQuantic.UI.Web.Tests` does not reference
- **THEN** the guard fails, naming the interface and saying to reference the project that declares it

#### Scenario: A fragment of a theme

- **WHEN** a snippet's theme declares three of `IAppTheme`'s members with their types and elides the rest
- **THEN** the guard passes
