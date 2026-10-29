## ADDED Requirements

### Requirement: A method is server-only when its symbol says so

The build SHALL treat a method, an operator or a conversion as marked `[ServerOnly]` when its symbol
carries the attribute, wherever a semantic model holds its tree, and SHALL read its attribute lists by
name only where none does. A method so marked through an alias, or on the defining half of a partial
method whose implementing half carries no attribute, SHALL be no member of any twin, of a class, a static
class, a record, a struct, a static class nested in a component or a component, nor of the twin of a class or
a record that takes it as an interface's default, nor SHALL a private helper of the interface that only such a
default calls, and what it names SHALL be neither imported nor refused by the module; it SHALL take no name the EQ1007 guard counts, and client code
that calls it SHALL fail the build with EQ2010 where it calls it.

#### Scenario: A method marked through an alias

- **WHEN** a class, a static class, a record, a struct, a static class nested in a component or a component declares `[Keep] public string LoadFromDisk(System.Net.Http.HttpClient client) => File.ReadAllText(new ColdStore().Path + client.BaseAddress);` under `using Keep = eQuantic.UI.Primitives.ServerOnlyAttribute;`, built as eqc builds an app or compiled on its own
- **THEN** the build succeeds, the type's twin holds no `loadFromDisk` and names no `HttpClient`, `File` or `ColdStore`, and the module imports no `ColdStore`

#### Scenario: A partial method marked on its definition

- **WHEN** the same types declare `[ServerOnly] public partial string LoadFromDisk(System.Net.Http.HttpClient client);` and its implementation, with the same body and no attribute
- **THEN** the build succeeds, and the twin and the module's imports hold none of it

#### Scenario: Other members marked through an alias

- **WHEN** a class or a record declares an operator or a conversion marked `[Keep]`, a static class's `extension(string s)` block a member marked `[Keep]`, or an interface a default `string Read()` marked `[Keep]` that a class or a record takes, each reading a file and constructing `ColdStore`
- **THEN** the build succeeds, the twin holds no such member and names no `File` or `ColdStore`, and the module imports no `ColdStore`

#### Scenario: A server-only default a record takes

- **WHEN** a record takes `[ServerOnly] string Read() => File.ReadAllText(new ColdStore().Path);` from an interface, the attribute written on the default
- **THEN** the build succeeds and the record's twin holds no `read`, where the record was refused with EQ1008 as relying on a body nothing could read

#### Scenario: A private helper only a server-only default calls

- **WHEN** an interface declares `[Keep] string Read() => Load();`, or the same marked `[ServerOnly]`, and `private string Load() => File.ReadAllText(new ColdStore().Path);`, and a record or a class takes the default
- **THEN** the build succeeds and the twin holds no `load`, where the helper crossed and its file access was refused (EQ2112)

#### Scenario: A member named like a server-only default

- **WHEN** `record Note(int Read) : IReads` takes `IReads.Read()`, marked `[ServerOnly]` or `[Keep]`
- **THEN** no EQ1007 is reported, where the record was refused as two members on one name

#### Scenario: A server-only overload known by its symbol

- **WHEN** a class, a static class, a record or a struct declares such a `Label(HttpClient)` beside `public string Label(string s) => s;`
- **THEN** no EQ1007 is reported, and the twin holds one `label`

#### Scenario: A client call to such a method

- **WHEN** a page shows `new App.Model.Disk().LoadFromDisk(null!)` over a class whose method is marked through an alias or on its partial definition
- **THEN** the build fails with one error, EQ2010 at the call in the page's file
