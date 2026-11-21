# Design

## The stateful section: fixed, not warned about

The issue proposed a warning: a stateful component built by a page that implements `IServerPrefetch`
and handed values through its constructor. Two things decided against it.

- **Measured, the defect is the server's.** `ServerDataRounds` draws the page, asks its loaders, and
  draws again. A position keeps a stateful instance through its parent's build and hands it the fresh
  one's configuration through `AdoptConfig`, which copies nothing unless the component overrides it.
  So the section the first frame built, with the page's defaults, was the one drawn. The tester, which
  runs the same rounds, showed `Name Title ana` beside `Hello `.
- **A warning could not tell the cases apart.** A section built only once the data is there (behind
  `_items.Count == 0 ? Empty() : List(_items)`, or projected from the loaded list) is right, and a
  warning on the argument would fire on it.

Flutter keeps a `State` and hands it the new `Widget`, which the state reads through `widget`. Our
`StatefulComponent` is both, so a kept instance sees new configuration only through `AdoptConfig`.
That is right for a tree a person uses, and wrong for the server's rounds, where nothing has happened
to an instance between two frames except that its parent's data arrived. So only the rounds change:
they arm `BuildOwner.KeepsInstance` with "is it a loader", a loader keeps its instance (what it loaded
is the page's data), and any other stateful component takes the fresh one. On a live owner the old
instance leaves its position and the new one enters it, as `FirstBuild` and `OnDeactivated` already do;
the tester mounts after the frame that settles the rounds, so the discarded one never mounted.

The predicate is a delegate set from `eQuantic.UI.Web`, not an interface test in the engine: the
engine is transpiled into the browser, where `KeepsInstance` stays null and the twin behaves as before.

## The icon's size: .NET's `[AllowedValues]`

`Icon` throws for a size off the scale, by design (the spec calls arbitrary sizes an error). The build
could only learn the scale from a list somewhere. A list in the analyzer would be a second copy of the
rule; `System.ComponentModel.DataAnnotations.AllowedValuesAttribute` on the parameter states it where
it lives, and the analyzer reads any parameter so marked, the app's included. The constructor's own
check reads the same `IconSize` constants, and a factory parameter must carry its constructor
parameter's mark and words (`UiFactoryConformanceTests`), since a screen calls the factory. A typed
size (an enum) would make the mistake unwritable, at the cost of every call site in every app; the
analyzer makes it a build error at none.

Numbers compare by value: a mark written with ints on a double parameter is still right.

## A type named like the SDK's: refused, not renamed

The issue preferred emitting colliding types under namespace-qualified names. A module's file, its
export, every import of it and the server's page-to-module map (`TwinName.OfType`) all key on the
simple twin name, computed as a pure function of the symbol in a dozen places, and the server has no
view of which types eqc wrote. Qualifying only the colliding names would need a manifest both sides
read; qualifying every name would rename every module of every app. Both are larger than this change
and neither is needed to stop the silent failure, which the issue accepts as its fallback.

The bundle already refused two of the app's types of one name (EQ1005, `EmittedTwins`). What it could
not see is a type the runtime carries, whose twin the app's build never writes. So the check lives at
the module: the walk that already decides what a module names for referenced projects
(`NamedSourceTypes`, which leaves out server code, `nameof`, attributes and inlined constants) now
also records the runtime's types it meets, and a name held by one of each is the clash. A build none
of whose types takes the name of one of the runtime's (`DeclaresATypeNamedLikeTheRuntimes`, once a
compilation, walking only `eQuantic` namespaces of the references) walks nothing.

The same code, EQ1005, because it is the same error: two types of one name cannot both be in the
browser. The case of two such types in one file was EQ2009 with a message about partial declarations.
