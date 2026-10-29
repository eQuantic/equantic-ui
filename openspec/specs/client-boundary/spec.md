# client-boundary Specification

## Purpose
Which code the build reads as client code, what it refuses there, and what it never refuses because it
never crosses: what a page's module holds is exactly what the build checks.

## Requirements

### Requirement: The build reads the code that crosses as client code, and nothing else

The build SHALL check against the client boundary exactly the code a module holds: every member of a
component its twin holds, a stateless component's as much as a stateful one's, and every class, record
and static class that becomes a module. It SHALL NOT check code that never crosses: the body of a
component's `[ServerAction]`, whose twin is a stub written from its signature, a method, an operator or a
conversion marked `[ServerOnly]`, whatever type declares it, an extension block's member included, and a
class marked `[ServerOnly]`. Such a member SHALL be no member of any twin, and what it names SHALL be neither
imported nor refused by a module. Client code that calls such a member, explicitly or through a pattern
(`Deconstruct` in deconstruction or a positional pattern, `Dispose` in `using`, and `GetEnumerator`
with the `MoveNext`, `Current` and `Dispose` of the enumerator it answers wherever the sequence is
read: a `foreach`, a spread `[.. xs]` and a LINQ operator over it), takes it as a method group, bare or
qualified, or applies such an operator or
conversion SHALL fail the build with EQ2010 where it does: a binary, a unary or a compound operator, an
increment, a tuple equality, and a conversion, implicit, spelled as a cast, a foreach's element or a
deconstruction's part.

#### Scenario: A Server Action that reads a file

- **WHEN** a stateful or a stateless page declares `[ServerAction] public Task<bool> Archived() => Task.FromResult(File.Exists(Path.Combine(Path.GetTempPath(), "receipt-1.txt")));`, expression-bodied or block-bodied
- **THEN** the build succeeds, and the page's module holds the action's stub and no name of `File` or `Path`

#### Scenario: A server-only member of a page

- **WHEN** a page declares `[ServerOnly] public bool Warm() => File.Exists("x");`, or a block-bodied one that builds an `HttpClient` and sleeps a `Thread`
- **THEN** the build succeeds, and the page's module holds no member of that name

#### Scenario: A server-only class beside a page in its file

- **WHEN** a page's file declares `[ServerOnly] public static class Disk { public static bool Has(string p) => File.Exists(p); }`
- **THEN** the build succeeds, and writes no module for `Disk`

#### Scenario: A server-only member of a class, a static class or a record

- **WHEN** a class, a static class or a record declares a `[ServerOnly]` method or operator that reads a file
- **THEN** the build succeeds, and its twin holds no such member

#### Scenario: A server-only member of an extension block

- **WHEN** a static class's `extension(string s)` block declares `[ServerOnly] public string Shout() => s + "!";`, or one that reads a file, beside a member that crosses
- **THEN** the build succeeds and the class's twin holds no such member, where the literal body was written into it and the one that reads a file refused (EQ2112); a page that calls `"x".Shout()` fails the build with one EQ2010 at the call, where it built and called `Exts.shout('x')`

#### Scenario: A server-only operator or conversion that client code applies

- **WHEN** a class or a record struct declares `[ServerOnly]` conversions and operators, and a page writes `Amount a = 7`, `(Amount)"abc"`, passes `3` to an `Amount` parameter, writes `a += b`, `(a, 1) == (b, 1)`, `foreach (Amount a in new[] { 1, 2 })` or `(Amount b, int m) = Pair()`
- **THEN** each fails the build with one EQ2010 at the page, where each built and called a static no twin carries (`Amount.fromInt(7)`), as `a + b`, `-a`, `a++` and `a == b` already failed

#### Scenario: A server-only pattern method that client code calls implicitly

- **WHEN** a class declares a `[ServerOnly]` `Deconstruct`, `GetEnumerator` or `Dispose`, and a page writes `var (a, b) = new Pair()`, `(a, b) = new Pair()`, `foreach (var (a, b) in new[] { new Pair() })`, `foreach (var x in new Bag())`, `[.. new Bag()]`, `using (new Lease()) { }` or `using var lease = new Lease();`
- **THEN** each fails the build with one EQ2010 at the page naming the method, where each built and called a method no twin carries

#### Scenario: A server-only method taken as a bare method group

- **WHEN** a class or a page declares `[ServerOnly] string Read()` and takes `Read` as a bare method group, or a class takes as one its own virtual `Read`, which a subclass overrides with a `[ServerOnly]` method
- **THEN** the build fails with one EQ2010 at the group, where it built and wrote `this.read.bind(this)`, as its call and its group through `this.` already failed; a bare group of a method that crosses, and of a virtual in a type no `[ServerOnly]` override can be, builds

#### Scenario: A framework host-only type read by a Server Action

- **WHEN** a page's Server Action returns `Task.FromResult(Matrix2D.Identity.M11)`
- **THEN** the build succeeds, where it was refused with EQ2010 as a type position of the page

#### Scenario: A server-only member of the enumerator a sequence answers

- **WHEN** a class's `GetEnumerator` answers an enumerator whose `MoveNext` is `[ServerOnly]`, or one whose `Dispose` is, and a page spreads the class into an array, `[.. bag]`
- **THEN** each fails the build with one EQ2010 at the page naming that member, where the module would call a member no twin carries

#### Scenario: A LINQ operator over a sequence whose iteration stays on the server

- **WHEN** a class's `GetEnumerator` is `[ServerOnly]`, and a page writes `new Bag().ToArray()`, or `new[] { 1 }.Concat(new Bag())`, which hands the class to an operator as the sequence it reads
- **THEN** each fails the build with one EQ2010 at the page naming `GetEnumerator`, and the array the second starts from is not told

### Requirement: A call through a contract a server-only method fulfils is refused where it is written

Client code whose call or method group binds to an interface member, or to a method that can be
overridden, SHALL fail the build with EQ2010 at that site, naming the method, when a `[ServerOnly]`
method of a type that crosses implements or overrides that member and the call's receiver can be that
type: the receiver's declared type, or for a bare call the type the call is declared in, is that type
or a type it derives from or implements, with matching type arguments when the type implements or
derives from a closed generic (`Bag : IEnumerable<int>`). A covariant parameter matches a receiver's
type argument that the type's own argument converts to by reference (`StringSource : ISource<string>`
behind `ISource<object>`), and a contravariant one a receiver's type argument that converts to the
type's own (`ObjectSink : ISink<object>` behind `ISink<string>`). A receiver whose type is not known, a
type parameter, SHALL be taken to be any. The contracts SHALL be the whole build's, the app's and every
referenced project's, so a project's call through its own contract, which the app fulfils on the server,
is refused where the project writes it. A call bound to a method that is not `[ServerOnly]`, a call
through `base`, a call whose receiver no such type can be, a contract only types that never cross
fulfil on the server, a contract no client code calls, and server code SHALL build. Each site SHALL be
told once, a call through `?.` where the source writes it.

#### Scenario: A call through an interface

- **WHEN** `Circle : IShape` declares `[ServerOnly] public double Area()` and a stateless or a stateful page shows `((IShape)new Circle()).Area().ToString()`, or calls `Area` on an `IShape` parameter in a block body, through a constraint `where T : IShape`, as a method group or through `?.`
- **THEN** the build fails with one EQ2010 at the page naming `Circle.Area`, in the app as in a referenced project, where it built and the browser threw a TypeError

#### Scenario: A call through a base

- **WHEN** `Derived : Named` declares `[ServerOnly] public override string Name()` and a page shows `((Named)new Derived()).Name()`
- **THEN** the build fails with one EQ2010 at the page naming `Derived.Name`, where the browser ran `Named`'s method and answered "base" where .NET answers "derived"

#### Scenario: An explicit implementation and one a base declares

- **WHEN** a page shows `((IPriced)new Gem()).Price()` over `[ServerOnly] double IPriced.Price()`, or `((IRound)new Disc()).Radius()` over `Disc : Round, IRound`, whose base `Round` declares a `[ServerOnly] Radius()`
- **THEN** each fails the build with one EQ2010 at the page, naming `Gem.Price` and `Round.Radius`

#### Scenario: A module a page reaches

- **WHEN** a class's `Told() => Name() + "!"` calls its own virtual, which a subclass overrides with a `[ServerOnly]` method, or a referenced project's `Paint(IShape shape) => shape.Area().ToString()` is handed the app's `Circle`, whose `Area` is `[ServerOnly]`
- **THEN** the build fails with one EQ2010 in the class's file, or in the project's

#### Scenario: A call that can reach no server-only method

- **WHEN** a page shows `new Square().Area()`; `new Plain().Name()` over another override that is not `[ServerOnly]`; `new Loud().Name()`, whose override calls `base.Name()`; `new Mute().Name()` or `new Echo().Twice()`, which calls `Name()` bare, over siblings of `Derived` that do not override it; `((ISealed)new Open()).Seal()` where only a `[ServerOnly]` class and an attribute keep their `Seal` on the server; iterates `IEnumerable<string>` where `Bag : IEnumerable<int>` declares a `[ServerOnly]` `GetEnumerator`; implements a contract with a `[ServerOnly]` method no client code calls; or calls through the contract from a Server Action or a `[ServerOnly]` member
- **THEN** the build succeeds

#### Scenario: A call through a variant interface

- **WHEN** a `[ServerOnly]` method implements `ISource<out T>.Read` on `StringSource : ISource<string>`, or `ISink<in T>.Accept` on `ObjectSink : ISink<object>`, and a page calls `source.Read()` on an `ISource<object>` or `sink.Accept("a")` on an `ISink<string>`
- **THEN** each fails the build with one EQ2010 at the page naming the method

#### Scenario: A receiver variance cannot reach

- **WHEN** the same page calls `Read()` on an `ISource<int>`, which no `ISource<string>` converts to, or `Take("x")` on an `INarrowSink<object>`, which only a `StringSink : INarrowSink<string>` fulfils on the server
- **THEN** the build succeeds

### Requirement: A comparison that reaches a server-only Equals is refused where it is written

Client code that calls `EqualityComparer<T>.Default.Equals`, `Nullable.Equals`, or `Equals` on a struct or a
nullable struct value, each of which asks the value's own equality, SHALL fail the build with one EQ2010 where
the call is written when that equality is marked `[ServerOnly]`, which no twin holds, naming it as a call bound
to it names it: an `Equals(object)` override the value's type declares or inherits, and a typed `Equals`
answering the type's `IEquatable<T>`, which the default comparer asks first and a record's synthesized
`Equals(object)` asks. Where the type does not say whose equality its value answers, a base class that
overrides nothing, an interface, `object` or a type parameter, whose comparer asks the type argument's
`IEquatable<T>` too, the comparison SHALL be refused as a call through the contract it reaches is. A
comparison that can reach no such member SHALL build.

#### Scenario: The default comparer over a class whose Equals is server-only

- **WHEN** a sealed class overrides `Equals(object)` with a `[ServerOnly]` method that answers false, and a page
  calls `EqualityComparer<Vaulted>.Default.Equals(c, c)`, or the same over a class that inherits such an
  override, or over a class whose typed `Equals` answering `IEquatable<T>` is `[ServerOnly]`
- **THEN** each fails the build with one EQ2010 at the call naming the method, as `c.Equals(c)` does, where it
  built, fell back to identity and answered True

#### Scenario: A comparison that may reach one

- **WHEN** `Closed : Open` overrides `Equals(object)` with a `[ServerOnly]` method where `Open` overrides
  nothing, and a page calls `EqualityComparer<Open>.Default.Equals(x, x)`, or the same through an interface
  `Closed` implements, `object` or a type parameter of generic code
- **THEN** each fails the build with one EQ2010 at the call, saying `'object.Equals' may reach 'Closed.Equals'`,
  and generic code in a build whose one server-only equality is a typed one is refused through
  `IEquatable<T>.Equals`

#### Scenario: A struct whose equality is server-only

- **WHEN** a struct overrides `Equals(object)` with a `[ServerOnly]` method, or a record struct's typed `Equals`
  is `[ServerOnly]`, and a page calls `v.Equals(v)`, `a.Equals(b)` over two nullable values,
  `Nullable.Equals(a, b)` or the default comparer of the struct or of its nullable values
- **THEN** each fails the build with one EQ2010 at the call naming the method, where it built and compared the
  struct's members, or called a typed method no twin carries

#### Scenario: A comparison that can reach none

- **WHEN** the same build compares a sealed class with no override and a struct with no `Equals` of its own by
  their default comparer, two nullable values of that struct by `Equals` and `Nullable.Equals`, and two `int`s
  by theirs
- **THEN** each builds and lowers as it did

### Requirement: Client code that reaches an API only a server has is refused once, where it is written

Client code that calls, reads, constructs, groups or extends a member of an API only a server has SHALL
fail the build at that site, with the code of the API: `EQ2112` for `System.IO`, `EQ2102` for
`Microsoft.EntityFrameworkCore` and `System.Data`, `EQ2103` for `System.Net.Http` and
`System.Net.Sockets`, `EQ2104` for `Thread`, `Monitor`, `Mutex` and `Semaphore` of `System.Threading`,
`EQ2105` for `Process` of `System.Diagnostics`, `EQ2106` for `System.Runtime.InteropServices` and
`EQ2107` for `System.Reflection.Emit`. The API SHALL be decided by the namespace of the type that
declares the member, a whole namespace or a type of one, and never by a prefix of the type's name. An
exception type of those namespaces SHALL NOT be refused: it is a value the browser builds and tests by
its type names. Each site SHALL be told once, never also as a member with no translation (EQ2004).

#### Scenario: A call in a stateless page

- **WHEN** a stateless page's Build shows `File.Exists("x") ? "y" : "n"`
- **THEN** the build fails with one EQ2112 at the call, where it was EQ2004

#### Scenario: A construction, a read and a method group

- **WHEN** a page passes `new FileInfo("x")` to a method, shows `Path.DirectorySeparatorChar.ToString()`, or holds `Func<string, bool> Has = File.Exists`
- **THEN** each fails the build with one EQ2112 at the site, where each built and named a class the browser does not have

#### Scenario: A member of a page that crosses

- **WHEN** a field's initializer, a lambda, a private method or a local function of a page calls `File.Exists`
- **THEN** the build fails with one EQ2112 there

#### Scenario: A class a page reaches

- **WHEN** a class, a static class or a record of the app calls `File.Exists`
- **THEN** the build fails with one EQ2112 at the class's file, where it was EQ2004

#### Scenario: An extension and a call through using static

- **WHEN** a page calls `client.GetFromJsonAsync<int>("x")` on an `HttpClient`, or `Exists("x")` under `using static System.IO.File`
- **THEN** the build fails with one EQ2103, or one EQ2112, at the call

#### Scenario: What is no server API

- **WHEN** a page subscribes to an `IObservable<int>`, throws and catches an `IOException`, and holds a field of type `FileInfo?`
- **THEN** none is refused as a server API: the `Subscribe` call is EQ2004, a member with no translation, and the rest builds

#### Scenario: A read through a conditional access

- **WHEN** a page reads `info?.Exists` of a `FileInfo?`
- **THEN** the build fails with one EQ2112 on the line that writes it, where it was told at the file's first line

### Requirement: A type the build writes no module for is refused where client code names it

A type of source, the app's or a referenced project's, for which the build writes no module because it
or a class it derives from is marked `[ServerOnly]` or is an attribute, or because it is declared inside
such a type, SHALL fail the build with EQ2010 at every site where client code writes its name: a call, a
static read, a name qualified by its namespace, `using static`, a method group, a construction, an
operator, an extension's home, a type test, an `as`, a struct's zero, a local's annotation, a member's
annotation in a class or a record, and a Server Action's answer or parameter. Each site SHALL be told
once. A position the module writes as nothing SHALL be free: a component's field or parameter, which is
annotated `any`, a cast, a list of the type, a reference type's zero, the `nameof` operator, a string
literal that holds its name, a type argument of a component's or a class's base list, which a twin
does not write, and a dictionary's key or value type, which a stub annotates `any` as it does the
dictionary. An instance member of such a type read off a value,
which no twin holds, SHALL fail the build with EQ2010 at the read, naming the member, unless the value's
expression holds a refusal already. A component SHALL cross whatever it is marked, where it is not
declared inside a type that never crosses.

#### Scenario: The app's own server-only class

- **WHEN** the app declares `[ServerOnly] public static class Vault { public static string Key() => "k"; }` and a stateless or a stateful page shows `Vault.Key()`, in an expression body or a block
- **THEN** the build fails with one EQ2010 at the page, where it built and the browser threw `Vault is not defined`

#### Scenario: Every way of naming it

- **WHEN** a page names the app's `[ServerOnly]` types through `App.Model.Vault.Key()`, `Vault.Name`, `new Ledger()`, `Ledger.Open()`, `Key()` under `using static`, `Func<string> f = Vault.Key`, `"x".Sealed()` over a `[ServerOnly]` extension class, `new Secret("x")` of a `[ServerOnly]` record, `new Savings()` over a `[ServerOnly]` base, `o is Ledger`, `o as Ledger`, `default(Amount)` of a `[ServerOnly]` struct, a field of that struct, a local `Ledger? held`, `@nameof(Vault.Key())` of a method named `nameof`, or a Server Action that answers or takes a `Secret`
- **THEN** each fails the build with one EQ2010 at the page, in the app as in a referenced project

#### Scenario: An attribute class

- **WHEN** a page shows `new MarkAttribute().Of` of the app's `class MarkAttribute : Attribute`
- **THEN** the build fails with EQ2010 at the page, where it built and named a class nothing wrote

#### Scenario: A struct's zero in a type that crosses

- **WHEN** `public static class Shop { public static Amount Price() => default; }` over a `[ServerOnly] struct Amount`, and a page shows `Shop.Price().V.ToString()`
- **THEN** the build fails with one EQ2010 at `Shop`'s file, where `Shop.Price()` answered undefined

#### Scenario: A member read off a value of it

- **WHEN** a page reads `ledger.Describe()`, `ledger.Total` or the group `ledger.Describe` off a `Ledger` parameter, `_ledger?.Describe()` off a field, `((Ledger)o).Total`, `secret.V` off a `[ServerOnly]` record, `amount.V` off a `[ServerOnly]` struct, or `Shop.Price().V`
- **THEN** each fails the build with one EQ2010 at the read naming the member, in the app as in a referenced project, where it built and called a member nothing defines, while `new Ledger().Total` and `Ledger.Open().Total` stay one site each, told where the type is named

#### Scenario: A position written as nothing

- **WHEN** a page declares a field `Ledger? _ledger`, a method taking `Ledger? ledger`, a `List<Ledger>` and its `FirstOrDefault()`, a cast `(Ledger)o`, or shows `nameof(Ledger)`
- **THEN** the build succeeds, and the page's module names no `Ledger`

#### Scenario: Server code names it freely

- **WHEN** a page's Server Action body or `[ServerOnly]` member, a `[ServerOnly]` method of a class that crosses, or another `[ServerOnly]` class names `Vault` and `Ledger`
- **THEN** the build succeeds, and no module names either

#### Scenario: A component marked server-only

- **WHEN** a page places `new Badge()`, a top-level component marked `[ServerOnly]`
- **THEN** the build succeeds, writes `Badge`'s module, and the page imports it

#### Scenario: A base list's type argument

- **WHEN** a page is declared `Home : StatelessComponent, IMark<Ledger>` or `Home : Shell<Ledger>` over a generic base component, or shows a class declared `LedgerHolder : Holder<Ledger>`, in the app or from a referenced project
- **THEN** the build succeeds, and no module names `Ledger`

#### Scenario: A dictionary a Server Action takes, and a string that names the type

- **WHEN** a page's `[ServerAction]` takes a `Dictionary<string, Ledger>`, or a record that crosses holds the literal `"Ledger"`
- **THEN** the build succeeds, the page's module names no `Ledger`, and the record's holds the literal and imports nothing of `Ledger`

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
