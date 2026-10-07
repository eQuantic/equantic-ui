# hydration-contract Specification

## Purpose
What a component rendered on the server carries to the browser so that its first build there draws
what the server drew: which of its values cross, under which name and in which shape, and what the
build refuses because it cannot cross.

## Requirements

### Requirement: A value crosses under the name the browser reads it by

Every value a component carries to the browser SHALL cross under the name its browser-side code reads
it by, whether the C# declares it as a field, an auto-property or a captured primary-constructor
parameter, for a server render and a client navigation alike.

#### Scenario: An auto-property a prefetch fills

- **WHEN** a page declares `public long Downloads { get; private set; }`, its `PrefetchAsync` sets it to
  `42`, and its `Build` writes `Downloads`
- **THEN** the served HTML shows `42`, and the browser's first build after hydration shows `42` too,
  where it showed the property's default before

#### Scenario: A captured primary-constructor parameter a prefetch rewrites

- **WHEN** a component `Quote(string symbol)` is built with `"acme"`, its `PrefetchAsync` sets
  `symbol = "ACME"`, and its `Build` writes `symbol`
- **THEN** the browser's first build writes `ACME`, as the server did

#### Scenario: The same names on a client navigation

- **WHEN** the browser navigates, without a reload, to the page with the auto-property above
- **THEN** the state the router receives carries `42` under the name the page reads, and the page draws
  `42`

### Requirement: A server service crosses only as what the browser reads of it

A value a page receives from the server's container SHALL NOT be written into the page whole. What
crosses SHALL be its projection: null when it is null, and otherwise only the members the page's
browser-side code reads, each in the same shape. This holds when the page reads the service directly,
through a member the page assigned it to, and through a child component it passes the service to. A
read SHALL end at a value that crosses whole: a scalar, or a list, a dictionary, a set, a sorted set,
a queue, a stack or a linked list of scalars, which the browser rebuilds as its own collection. Any
other value, a struct or a pair included, SHALL be read member by member.

#### Scenario: Only whether it is null

- **WHEN** a page takes `SiteIdentity? identity` from the container, where `SiteIdentity` has an
  `Authority` property, and its `Build` reads only `identity is null`
- **THEN** the served HTML contains no value of any `SiteIdentity` member, and the browser's first build
  takes the same branch the server took

#### Scenario: A null service

- **WHEN** the same page is served with no `SiteIdentity` registered
- **THEN** the browser's first build takes the null branch, as the server did

#### Scenario: One member read through a field

- **WHEN** a page's constructor assigns `_options = options` from a container-provided `SiteOptions`
  with `Title` and `ApiKey`, and its `Build` writes `_options.Title`
- **THEN** the served HTML carries the title and not the API key, and the browser's first build writes
  the title

#### Scenario: A dictionary of longs

- **WHEN** a page writes `report.Prices["a"] * 2`, where the container-provided `report` holds a
  `Dictionary<string, long>` as `Prices`
- **THEN** the dictionary crosses whole, and the browser's first build writes the number the server
  wrote

#### Scenario: A set of roles

- **WHEN** a page writes `report.Roles.Contains("admin") ? "admin" : "member"` and `report.Roles.Count`,
  where the container-provided `report` holds a `HashSet<string>` as `Roles`
- **THEN** the build succeeds, the set crosses whole, and the browser's first build writes what the
  server wrote

#### Scenario: Read by a child component

- **WHEN** a page passes `identity` to a child component whose `Build` writes `identity.DisplayName`
- **THEN** the served HTML carries `DisplayName` and no other member of `SiteIdentity`, and the child
  draws it in the browser

### Requirement: A dependency the browser resolves itself never crosses

A constructor parameter whose type is an interface from outside `System` SHALL NOT cross, since the
browser resolves its own implementation of it.

#### Scenario: A clock

- **WHEN** a page takes `IClock clock` and starts `clock.Every(TimeSpan.FromSeconds(1), Tick)` in the
  browser-side code
- **THEN** the served HTML carries nothing for `clock`, and the browser's twin resolves its own `IClock`

### Requirement: A service the build cannot project fails the build

The build SHALL refuse with EQ2114, an error, a page whose browser-side code uses a value from the
server's container in a way the projection cannot follow: a method of it called with an argument that changes in
the browser, the value passed where the analysis cannot see what is read of it, or a member a .NET type
declares read on a value that does not cross whole (the `Count` of a list or a set of objects, a
pair's `Value`), which the browser answers from its own form of that type. The message SHALL
name the page, the value, the expression where the analysis stopped, and the way out: deciding on the
server in `PrefetchAsync` or a `[ServerOnly]` member and keeping the result in a field, or a
`[ServerAction]` when the browser's state is an input.

#### Scenario: A call with an argument from the browser

- **WHEN** a page takes `ProductRepository repository` from the container and a handler calls
  `repository.Find(_selectedId)`
- **THEN** the build fails with EQ2114 naming the page, `repository` and `repository.Find(_selectedId)`

#### Scenario: The count of a list of objects

- **WHEN** a page writes `report.Items.Count`, where the container-provided `report` holds a
  `List<Product>` as `Items`
- **THEN** the build fails with EQ2114 naming `report.Items.Count`

#### Scenario: A value the analysis cannot follow

- **WHEN** a page's `Build` passes a container-provided `SiteIdentity` to a method of a referenced
  assembly the build has no source for
- **THEN** the build fails with EQ2114 naming the call

#### Scenario: Used only on the server

- **WHEN** the same repository is read only inside a `[ServerOnly]` `PrefetchAsync`
- **THEN** the build succeeds, and nothing of the repository crosses

### Requirement: The payload carries only what the build described

The server SHALL write a component's state only as the build described it. A component whose type the
build did not describe SHALL carry no state, and a value the server's container hands out as a service,
registered as its own type or as an interface or a base type it derives from outside `System`, SHALL never
be written whole, whatever the description says.

#### Scenario: A service behind a field typed object

- **WHEN** a component's field is declared `object`, so the build describes it as data, and at run time
  it holds an instance of a type the container registers as a service
- **THEN** the served HTML carries nothing for that field, and the server logs which field it left out
  and why

#### Scenario: A service registered under an interface it implements

- **WHEN** that field holds an instance the container hands out only under an interface its type
  implements, as `AddSingleton<IAccountView, AccountSecrets>()` registers it
- **THEN** the served HTML carries nothing for that field, and the server's log names the interface

### Requirement: A collection crosses as the class the browser's code reads

A component's value of a collection type the browser holds as one of its own classes SHALL reach the
browser's code as that class, whatever the server's serializer wrote: a `HashSet`, an `ISet` or an
`IReadOnlySet` as a set, and a `SortedSet`, a `Queue`, a `Stack` and a `LinkedList` as the
runtime's. Each element SHALL be hydrated by its own type, and the collection SHALL enumerate and
take its elements in the order it does on the server, a sorted one in its element type's order. A set
or a dictionary SHALL NOT cross when the browser's copy would answer differently: one holding a
comparer other than its element type's default equality (or the ordinal one, for a string), a sorted
one holding a comparer other than its element type's default order, read through a wrapper as well,
and, in a member the browser rebuilds as a set or a dictionary, one of a class whose equality cannot
be read. The server SHALL leave it out and log it; in a projected value, only the read that ends at it
SHALL be left out.

#### Scenario: A set a prefetch loaded

- **WHEN** a page's `PrefetchAsync` sets a `HashSet<string>` field to `admin` and `editor`, and its
  `Build` writes `_roles.Contains("admin")` and `_roles.Count`
- **THEN** the browser's first build after hydration, and after a client navigation, writes `True` and
  `2`, as the server did

#### Scenario: A stack keeps its top

- **WHEN** a prefetch pushes 1, 2 and 3 onto a `Stack<int>` field, and the page writes `_stack.Peek()`
- **THEN** the browser writes `3`, as the server did

#### Scenario: A sorted set keeps its order

- **WHEN** a prefetch adds `b`, `B` and `a` to a `SortedSet<string>` field and `10m`, `9m`, `1.0m` and
  `1.00m` to a `SortedSet<decimal>` field, and the page writes each one out in a loop
- **THEN** the browser writes `a`, `b`, `B` and three decimals in the order `1`, `9`, `10`, as the
  server did

#### Scenario: A set with its own comparer

- **WHEN** a prefetch sets a `HashSet<string>` field to one made with `StringComparer.OrdinalIgnoreCase`,
  an `IReadOnlySet<string>` field to a case-insensitive `ImmutableHashSet` or to a set of the app's own,
  or a `SortedSet<string>` field to one ordered by `StringComparer.Ordinal`
- **THEN** the served payload carries nothing of that field, and the server's log names it

#### Scenario: A projected value keeps the rest

- **WHEN** a container-provided value the page reads holds a name and a case-insensitive set
- **THEN** the served payload carries its name and nothing of the set, and the server's log names the
  read

#### Scenario: A queue of longs

- **WHEN** a prefetch enqueues `9007199254740993` and `2` on a `Queue<long>` field, and the page writes
  `_queue.Peek() + 1`
- **THEN** the browser writes `9007199254740994`, as the server did

### Requirement: A record crosses as its twin

An in-source record or struct that crosses into the browser, as a Server Action's result, a page's
state, a topic's payload or a member of any of them, SHALL be rebuilt on its twin whether or not any of
its members needs coercion, so its methods, its equality and `with` work on it there.

#### Scenario: A record none of whose members needs coercion

- **WHEN** a Server Action returns a `record Notice(string Text)` that declares a method
- **THEN** the browser receives a `Notice`, and the method answers on it as in C#

#### Scenario: A record held by a record

- **WHEN** a record that crosses holds another record among its members
- **THEN** the one it holds is rebuilt on its own twin too

#### Scenario: A generic record

- **WHEN** a `Box<long>` crosses, where `record Box<T>(T Value)`
- **THEN** its `Value` arrives as a long: the constructed type's members describe it, since the twin's
  own map cannot know `T`

### Requirement: A served page is built at the browser's density and hydrates at it

The server SHALL build a page at the density the browser's pointer asks for, as the browser reported
it, and SHALL say in the page's configuration which density it used. Hydration SHALL lower at that
density, and when the browser's own differs SHALL switch the whole page to it at once. A client
navigation's server data SHALL be found in the tree built at the same density.

#### Scenario: The first request of a session under a mouse

- **WHEN** a page is requested with no density cookie and hydrated under a fine pointer
- **THEN** it is served Comfortable, hydration adopts it as served, the whole page then switches to
  Compact together, and the session cookie says compact

#### Scenario: Every later request

- **WHEN** the same session requests a page again
- **THEN** the server builds it Compact, the configuration says so, and nothing switches

#### Scenario: A client navigation into a page that composes by its density

- **WHEN** a compact session navigates to a page whose components differ by density
- **THEN** the navigation carries the server data of the components the compact tree holds
