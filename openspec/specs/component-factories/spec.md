# component-factories Specification

## Purpose
The declarative surface a source generator writes for an app's own components (`AppUI.g.cs`): which
components get a factory, so that the file a developer never wrote always compiles and names only
components the browser has a module for.

## Requirements

### Requirement: A factory is written for a component the surface reaches

The generated declarative surface SHALL write a factory for a concrete, non-generic component in a
namespace that is not a page only when the component and every type it is declared in are public, and
the build writes the component a module: no type it is declared in is marked `[ServerOnly]`, itself
or through a class it derives from, or `[RuntimeProvided]`, or is an attribute, and a nested component
is not marked `[ServerOnly]` either, itself or through a class it derives from, since the attribute is
inherited. A top-level component SHALL keep its factory whatever it derives from, since
the component path ignores the attribute and a top-level component always ships. The surface SHALL
compile whatever the app declares.

#### Scenario: A public component inside an owner that is not public

- **WHEN** `public sealed class Door : StatelessComponent` is declared inside an `internal` class, a
  `private` one, a `protected internal` one, or a public one nested in an `internal` one
- **THEN** the surface writes no `Door` factory, writes the other components' factories, and the app
  compiles with it

#### Scenario: A component that never crosses

- **WHEN** the class a component is declared in is marked `[ServerOnly]` or derives from a class that
  is, or the nested component is marked so itself or through its base
- **THEN** the surface writes no factory for it

#### Scenario: A component inside an attribute or a type the runtime provides

- **WHEN** a component is declared inside a class deriving from `System.Attribute`, directly or through
  another attribute, or inside a class marked `[RuntimeProvided]`
- **THEN** the surface writes no factory for it, as eqc writes it no module

#### Scenario: A top-level component and the attribute

- **WHEN** a top-level component is marked `[ServerOnly]`, or derives from a component that is
- **THEN** the surface writes its factory, as eqc writes its module

#### Scenario: A public component inside a public owner

- **WHEN** `Door` is declared public inside a public class
- **THEN** the surface writes `public static global::Demo.Vault.Door Door(string label)`, and the app
  compiles with it

### Requirement: A component's statics are read from another namespace

The generator SHALL write, beside the factory surface, a `global using` alias of the component's name for
each component with a factory that declares a static member or a nested type the app may read, so that
`ProductEditor.Toggled(true)` binds to the component in a file that imports its namespace with a `using`
at the compilation unit's level, while `ProductEditor("a")` still binds to the factory. It SHALL write no
alias for a generic component, nor for a component whose name a namespace imported at the compilation
unit's level by any file of the app declares as another type, a class a file imports with `using static`
nests, an alias a file declares names, or the global namespace declares.

#### Scenario: A helper and a constant from another namespace

- **WHEN** `Shop.Components.ProductEditor` declares `public static bool Toggled(bool on)` and
  `public const int MaxName = 40`, and a file in `Shop.Pages` with `using Shop.Components;` writes
  `ProductEditor.Toggled(on) && ProductEditor.MaxName > 0 ? ProductEditor("a") : Plain("b")`
- **THEN** the app compiles, and the surface holds `global using ProductEditor = global::Shop.Components.ProductEditor;` and no alias for `Plain`

#### Scenario: A component named like a type a file imports

- **WHEN** a component is named `Timer` and declares a static helper, and a server file with
  `using System.Threading;` writes `new Timer(callback, null, 0, 1000)`
- **THEN** no alias is written, and the server file's `Timer` is `System.Threading.Timer`

### Requirement: A referenced project's components get factories

The factory surface SHALL give a factory to every public component, with the rules of the app's own
components, of an assembly the app references that came from a project the build compiles from source
(not the SDK's own projects, not an icon pack), and to none of an assembly that came from a package. The
surface SHALL stay in the namespace the app's own components share, or, for an app with none, in the
namespace its assembly is named by. A component of the app and one of a referenced project with one name
SHALL leave the factory to the app's, with EQ3102 naming the other.

#### Scenario: A library's component composed by a page

- **WHEN** a page of an app that references the project `Brand.Common` calls `Wordmark("Since 1998")`,
  and `Brand.Common` declares a public `Wordmark(string tagline = "")`
- **THEN** the surface holds `Wordmark(string tagline = "")` building `global::Brand.Common.Wordmark`, and
  the app compiles

#### Scenario: A package's component

- **WHEN** `Brand.Common` came from a package rather than a project
- **THEN** the surface holds no `Wordmark`, and the page's call does not compile

#### Scenario: The shared library sample

- **WHEN** `samples/SharedLibrary/SharedLibrary.Web` composes `Wordmark()` and is built
- **THEN** its `AppUI` builds the library's `Wordmark`, its module draws it, and the page shows it in each
  of its languages
