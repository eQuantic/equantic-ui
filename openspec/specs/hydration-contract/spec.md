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
take its elements in the order it does on the server. A collection holding a comparer other than
its element type's default, or the ordinal one for a string, SHALL NOT cross, since the browser's
copy compares with the default: the server SHALL leave it out and log the member.

#### Scenario: A set a prefetch loaded

- **WHEN** a page's `PrefetchAsync` sets a `HashSet<string>` field to `admin` and `editor`, and its
  `Build` writes `_roles.Contains("admin")` and `_roles.Count`
- **THEN** the browser's first build after hydration, and after a client navigation, writes `True` and
  `2`, as the server did

#### Scenario: A stack keeps its top

- **WHEN** a prefetch pushes 1, 2 and 3 onto a `Stack<int>` field, and the page writes `_stack.Peek()`
- **THEN** the browser writes `3`, as the server did

#### Scenario: A set with its own comparer

- **WHEN** a prefetch sets a `HashSet<string>` field to one made with `StringComparer.OrdinalIgnoreCase`,
  or a container-provided value holds one that the page reads
- **THEN** the served payload carries nothing of that set, and the server's log names the member

#### Scenario: A queue of longs

- **WHEN** a prefetch enqueues `9007199254740993` and `2` on a `Queue<long>` field, and the page writes
  `_queue.Peek() + 1`
- **THEN** the browser writes `9007199254740994`, as the server did
