## MODIFIED Requirements

### Requirement: A verbatim name is written without its escape

A name written with C#'s verbatim escape SHALL be written without it: a member and a `with` key as the name itself (`class`), and a label and a type parameter, declaration and references, through the rule that renames a word JavaScript reserves (`package$`, `class$`). A type, a namespace, a member, a parameter and a local spelled verbatim SHALL be the name C# reads, the one every reference writes: the type's twin, its module, every import of it, every annotation and every scan that names it SHALL use that name, and a record SHALL print it. eqc SHALL read a name from syntax by its value, never by its spelling, and a guard SHALL fail on a source of the SDK that reads an identifier's spelling or a type's syntax as text.

#### Scenario: A member written @class

- **WHEN** `new R2(3).@class` is read, and `new R2(3) with { @class = 5 }` is printed
- **THEN** they answer 3 and `R2 { class = 5 }`, as in .NET

#### Scenario: A label named package

- **WHEN** a loop labeled `package` is left with `continue package` and `break package`
- **THEN** the module parses and the loop counts as in .NET

#### Scenario: A type parameter written @class, one level down

- **WHEN** a method declares `Count<@class>(IReadOnlyList<@class> values, @class[] more)`, in a plain
  class or in a component
- **THEN** it is declared `count<class$>`, its annotations say `class$[]`, the module parses, and it
  counts as in .NET

#### Scenario: A type spelled verbatim

- **WHEN** `namespace Store { public static class @Shelf { public static int @Raise() => 1; } }` is
  compiled as an app's build compiles it, and another module reads `Store.Shelf.Raise()` and
  `Store.@Shelf.@Raise()`
- **THEN** the module is `Shelf`, it declares `export class Shelf`, the reader imports it, and both
  calls answer 1, as in .NET

#### Scenario: A record and an exception spelled verbatim

- **WHEN** `public record @Point(int @X, int Y);` is printed, and `new Store.@Failure("m")`, whose class
  derives from `System.Exception`, is thrown and caught as `Store.Failure`
- **THEN** the record prints `Point { X = 1, Y = 2 }`, and the catch answers `"mTrue"` for
  `f.Message + (f is Store.@Failure)`, as in .NET

#### Scenario: Annotations of a type spelled verbatim

- **WHEN** a property is typed `@Point`, a parameter `@Box`, a local `Store.@Box`, and a cast, a
  pattern, an array and a `List<Store.@Box>` name the type, in a module written with type annotations
- **THEN** every annotation says `Point` or `Box`, the module bundles, and each answers as in .NET

#### Scenario: A source that reads a name by its spelling

- **WHEN** a source of the SDK reads `Identifier.Text`, or the compiler calls `ToString()` on a
  `TypeSyntax`
- **THEN** `NoNameReadAsSpelledTests` fails, naming the file and the line
