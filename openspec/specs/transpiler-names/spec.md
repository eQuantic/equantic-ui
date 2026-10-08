# transpiler-names Specification

## Purpose
How eqc names what it writes: a local function, a reserved word, and every declaration a reader
reads, so that no name it changes lands on one its scope already holds.

## Requirements

### Requirement: A renamed local function lands on no name its scope holds

A local function SHALL be camel-cased and made a legal JavaScript identifier, and SHALL take a `$`
where the member around it already declares that name. Its declaration, a method group and a direct
call SHALL all use the same name.

#### Scenario: A local and a local function that differ by case

- **WHEN** `string log = ""; var d = new Dictionary<string, int> { ["a"] = 1 }; int D() { log += "d"; return 7; } d.GetValueOrDefault("a", D()); return log;` runs
- **THEN** it answers `"d"`, as in .NET, and the module loads

#### Scenario: A parameter named like the function

- **WHEN** `int D() => 7; int Use(int d) => d + D(); return Use(1);` runs
- **THEN** it answers 8, as in .NET

### Requirement: A reserved word is escaped with a `$`

A name that is a JavaScript reserved word or literal SHALL be escaped with a `$`, which no C#
identifier can hold, on every kind of declaration that writes a name.

#### Scenario: A C# name that ends like the old escape

- **WHEN** `int package = 1; int package_ = 2; return package * 10 + package_;` runs
- **THEN** it answers 12, as in .NET

#### Scenario: A verbatim keyword

- **WHEN** `int @class = 3; return @class;` runs
- **THEN** it answers 3, as in .NET

### Requirement: A declaration and its readers use one spelling

Every parameter (of a component's constructor, a record's method or constructor, and a server
action's stub and the call it forwards), local, and pattern, deconstruction, loop or catch variable
SHALL be declared under the same legal name its readers use.

#### Scenario: A record method with a verbatim keyword parameter

- **WHEN** `public record Box(int N) { public int Plus(int @class) => N + @class; }` runs `new Box(3).Plus(4)`
- **THEN** it answers 7, as in .NET

### Requirement: A parameter the body never reads keeps its spelling free

The spelling of a parameter the body never reads (its name with an underscore in front) SHALL be
reserved, so that no local function takes it.

#### Scenario: An unused verbatim parameter beside an underscored function

- **WHEN** `public int M(int @class) { int _class() => 1; return _class(); }` is transpiled
- **THEN** the parameter is `_class$` and the function a different name, so the module declares each once

### Requirement: A name the emitter adds lands on no binding the constructor holds

The config object a constructor takes last SHALL be `props`, and SHALL be `$props` where the C#
constructor already binds `props`, as a parameter or anywhere in its body, since the body shares
the block its parameters are declared in.

#### Scenario: A constructor local named props

- **WHEN** `public TickPage(string label) { var props = label; _label = props; }` is transpiled
- **THEN** the constructor is `constructor(label?: any, $props?: any)` and the module declares `props` once

### Requirement: With no model, a reference reaches a binding of a scope around it

A reference converted with no semantic model SHALL read a parameter, local or local function only
where a scope around it declares that name, and SHALL NOT treat a declaration in a sibling scope of
the same member as its binding.

#### Scenario: A lambda parameter beside a member read

- **WHEN** `System.Func<int, int> f = Component => Component + 1; return Component;` is converted with no model
- **THEN** the lambda reads its parameter and the return reads the component's `this._component`

### Requirement: A base type is named after its twin

The base a class, a component, a primitive or a record extends SHALL be emitted under its twin's
name, the class's own simple name read from its symbol, whatever the source's spelling of it: a
namespace, an alias or `global::`. The module SHALL import that name as it imports every other.

#### Scenario: A primitive whose base is qualified

- **WHEN** an app declares `public sealed class Tag : eQuantic.UI.Web.HtmlElement`
- **THEN** its module declares `class Tag extends HtmlElement` and imports `HtmlElement`

#### Scenario: A base through an alias

- **WHEN** an app writes `using Pail = App.Models.Bucket;` and declares `public class Bin : Pail`
- **THEN** its module declares `class Bin extends Bucket`

### Requirement: A type's own Count is its own

A `Count` read SHALL be spelled by the receiver's symbol: an array and a type of .NET's own
namespaces (`System`, `Microsoft`), a list or a lookup among them, read the array's `length` (or the
size or count of the class the browser holds it as), and a type declared in the app or in a library
it references reads its twin's `count`.

#### Scenario: A library's type

- **WHEN** a component reads `_tally.Count` of `Lib.Tally { public int Count { get; set; } }`, a type of
  a library the app references, and `_rows.Count` of a `List<int>`
- **THEN** its module reads `this._tally.count` and `this._rows.length`

### Requirement: A type reached through its namespace is imported by what it binds

A type the C# reaches through its namespace, part of it, the whole of it or `global::`, or through a
using alias, SHALL be written by its own name and imported by the symbol the model binds, as a type
reached through a using is, whatever member the expression reads from it. An enum or an interface
reached that way SHALL import no module.

#### Scenario: A type reached through part of its namespace

- **WHEN** inside `namespace App.Chat`, `Portal.Fold.Text(2)`, `Portal.Fold.Max`, `new Portal.Tally().N` and `Portal.Tally.Zero` name the types of `App.Portal`
- **THEN** the module imports `Fold` and `Tally`, and each answers what .NET answers

#### Scenario: A type reached through its whole namespace, and through global::

- **WHEN** `App.Portal.Fold.Text(1)` and `global::App.Portal.Tally.Zero`
- **THEN** the module imports `Fold` and `Tally`, and each answers what .NET answers

#### Scenario: A type reached through a using alias

- **WHEN** `using F = App.Portal.Fold;` and `F.Text(4)`, `F.Max`
- **THEN** the module writes and imports `Fold`, and each answers what .NET answers

#### Scenario: An enum's member reached through its namespace

- **WHEN** `Portal.Mood.Loud` and `App.Portal.Mood.Calm`
- **THEN** each is its member's value, and the module imports no module of the enum's name
