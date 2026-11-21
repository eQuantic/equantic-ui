## ADDED Requirements

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
