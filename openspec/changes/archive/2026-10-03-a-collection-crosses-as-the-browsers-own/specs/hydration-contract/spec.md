## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: A collection crosses as the class the browser's code reads

A component's value of a collection type the browser holds as one of its own classes SHALL reach the
browser's code as that class, whatever the server's serializer wrote: a `HashSet`, an `ISet` or an
`IReadOnlySet` as a set, and a `SortedSet`, a `Queue`, a `Stack` and a `LinkedList` as the
runtime's. Each element SHALL be hydrated by its own type, and the collection SHALL enumerate and
take its elements in the order it does on the server.

#### Scenario: A set a prefetch loaded

- **WHEN** a page's `PrefetchAsync` sets a `HashSet<string>` field to `admin` and `editor`, and its
  `Build` writes `_roles.Contains("admin")` and `_roles.Count`
- **THEN** the browser's first build after hydration, and after a client navigation, writes `True` and
  `2`, as the server did

#### Scenario: A stack keeps its top

- **WHEN** a prefetch pushes 1, 2 and 3 onto a `Stack<int>` field, and the page writes `_stack.Peek()`
- **THEN** the browser writes `3`, as the server did

#### Scenario: A queue of longs

- **WHEN** a prefetch enqueues `9007199254740993` and `2` on a `Queue<long>` field, and the page writes
  `_queue.Peek() + 1`
- **THEN** the browser writes `9007199254740994`, as the server did
