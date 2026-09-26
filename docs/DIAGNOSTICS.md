# Diagnostics

Every build error and warning eqc can print, with what it means and what to do about it. The
codes are stable: they are what you search for, and what a suppression would name.

A guard keeps this page honest — `DiagnosticsDocumentedTests` fails the build when a code is
reported from `src/` and has no row here, when a row names a code that no longer exists, or when
a code gains a new reporting site. The last one is not a style rule: **EQ2101 was once two
unrelated errors** (a resx translation mismatch and `System.IO` in a client component), each
pinned by its own green test, because the two never met in one compilation.

Diagnostics print in MSBuild-canonical form, so the IDE and `dotnet build` both link them:

```text
Pages/Counter.cs(14,9): error EQ2002: C# 'goto' cannot be transpiled to JavaScript …
```

## EQ0xxx — the build host

The `eqc` process itself, before or around a compilation.

| Code | Meaning | What to do |
|---|---|---|
| `EQ0000` | A compilation error arrived with no code of its own. | Read the message — the code is missing, not the diagnosis. |
| `EQ0001` | eqc crashed. | A bug in the compiler. The message carries the exception; please report it with the source that triggered it. |
| `EQ0002` | The MSBuild reference list is empty, so the semantic model would be built from an incomplete compilation and named arguments could be emitted in the wrong order. | Rebuild (`dotnet build`). If it persists, `CompileEQuanticUI` ran without `FindReferenceAssembliesForReferences`. |

## EQ1xxx — no translation exists yet

A gap in the compiler, not in your code. The construct is legal C# and could be translated; today
nothing does it. These are the codes that shrink as the compiler grows.

| Code | Meaning | What to do |
|---|---|---|
| `EQ1001` | A C# **expression** kind has no transpilation strategy. | Rewrite it in a transpilable form, or add a conversion strategy for the construct. |
| `EQ1002` | A C# **statement** kind has no transpilation strategy. | As above. |
| `EQ1003` | A C# **node** (neither expression nor statement) has no strategy. | As above. |
| `EQ1004` | A strategy **matched** the construct but this exact form has no emission — the translation would be silently wrong, so it stops. | Rewrite the form, or add the missing case to the strategy. |
| `EQ1005` | Two types share a twin filename — either the same name twice, or two names that differ only in case (one file on Windows and macOS). Their twins would be ONE file and the second would overwrite the first. | Rename one of the types. |
| `EQ1006` | *(warning)* One type is declared in more than one place and eqc emits one module per declaration, so the twin holds only the first declaration's members. | Combine them into a single declaration, or keep the members a component uses together in one. Harmless when the other halves are server-only, which is why it does not stop the build. |
| `EQ1007` | A method name declared twice in one type: two overloads, or two names that lower to one (`Foo` and `foo`). C# tells overloads apart by their parameters, and a JavaScript class has one member per name, so the twin would keep one of them and every call would reach it: a class, a record or a static class kept the LAST, a component the FIRST, all in silence. A static and an instance method may share a name, a partial method's two halves are one method, and an explicit interface implementation takes the interface's name, which no rename can change, so it is left out. Two defaults a class takes from different interfaces, or a default and a member the class declares, land on one name the same way, and so do the same pairs along the class chain: a member a derived class declares on the name of a default its base takes (unless it implements that default's interface member, the class listing the interface again), a default on the name of a member a base declares, and two defaults of different interface members, one taken by a base and one by the derived class. | Give each overload its own name (`Compare` and `CompareLines`). The bound tree says which overload each call binds, so this is a gap the compiler can close by naming overloads apart, as it does user-defined operators. |
| `EQ1008` | A class relies on a default interface member of an interface compiled into a referenced assembly outside the SDK. JavaScript has no interfaces, so eqc writes each default a class takes into its twin: converted from the interface's source when the compilation has it, and delegated to the runtime's copy for the SDK's own interfaces (`IAppTheme`, `ICodeLanguage`, `ICodeCompletionProvider`). Any other assembly's interface has neither, so the twin has no such member and a call to it in the browser would meet undefined. The same for a default that uses a static member of an interface, which has no JavaScript form to hold it. | Declare the member in the class, or keep the class out of client code. |

## EQ2001–EQ2011 — the construct cannot cross

Not a gap: there is no browser equivalent, or a translation would have to be a guess. These do
not shrink with compiler work — they are the shape of the target.

| Code | Meaning | What to do |
|---|---|---|
| `EQ2001` | A C# construct with no runtime equivalent in the browser (typed-reference intrinsics: `__makeref`, `__reftype`, `__refvalue`, `stackalloc`, pointers). | Restructure without it. |
| `EQ2002` | `goto`. | Restructure with loops and conditionals, or a labelled `break`/`continue` (those DO translate). |
| `EQ2003` | `new T()` on a type parameter — generic arguments are erased at runtime in JavaScript, so the concrete type is unknown. | Pass a factory (`Func<T>`) or the constructed value in. |
| `EQ2004` | A member declared outside the compilation that no strategy translates, so nothing emits it: a BCL member with no JavaScript form (`Convert.FromBase64String`, `DateOnly.ParseExact`), an extension method whose declaring class is not part of this compilation, or a `System.Range` value stored rather than indexed with. | Use a member that translates (index with the range directly, `text[a..b]`), move the call behind a `[ServerAction]`, or bring the declaring source into the compilation. |
| `EQ2005` | An infinite iterator. Iterators are MATERIALISED into an array, so the loop would run forever instead of yielding lazily. | Give the loop an end (a bound, a `yield break`), or take what you need inside the method. |
| `EQ2006` | The member does not bind in the semantic model, so any translation would be a guess. | Either the code does not compile, or eqc is missing references / generated sources. Never guessed — see [Compiler](https://github.com/equantic/equantic-ui/wiki/Compiler). |
| `EQ2007` | A comparer that changes what a collection considers equal or in order: a collection expression's `with(…)` argument beyond a capacity hint, or a comparer handed to a collection's constructor (`new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase)`). A JS array, object, `Set` or runtime map compares with `===` and ordinal strings, and takes no comparer. The default comparers and `StringComparer.Ordinal` ask for exactly that, and pass. | Drop the comparer, or normalize the keys yourself (lower-case them on the way in and on every lookup). |
| `EQ2008` | Query syntax using `join`, `let`, a second `from`, or `into` — its C# translation runs through compiler-generated transparent identifiers. Also the fenced initializer forms. | Rewrite in method syntax, where every operator is supported. |
| `EQ2009` | A component is declared more than once in one file (partial declarations). eqc emits one module per declaration and cannot merge them. | Combine the members into a single declaration. |
| `EQ2011` | A member that would take a name the runtime's component already uses. C# keeps a primary-constructor parameter, a field and a property apart, and spells them differently from the runtime's camelCase; JavaScript folds all of it onto `this.<name>`, so the assignment replaces the runtime's member and the page fails only in the browser. The list is read from the live runtime (`core/runtime-members.spec.ts` → `Resources/runtime-members.txt`), never typed. | Rename the member. An `override` is exempt — a component's `Build` is meant to replace the runtime's `build`, and replacing a base member is what one is for — but a plain method is not: `public void Mount() { }` overrides nothing and still emits `mount()` over the runtime's own, so the component never mounts. |
| `EQ2010` | A symbol the framework keeps on the HOST — a type or a single member marked `[ServerOnly]` — named from client code. The runtime ships no twin, so the reference would compile, emit, and fail at hydration on "does not provide an export named" while SSR kept answering 200. | Call it from server code: a `[ServerAction]`, a `[ServerOnly]` class, or a realizer. Never from a component's `Build`. Unlike `EQ2004` this is a DECISION, not a gap — adding a strategy is not the answer. |
| `EQ2012` | A `[ServerAction]` on an ABSTRACT component. The server registers an action under the name of the CONCRETE component carrying it — `ServerActionRegistry.ScanAssembly` skips abstract types and walks inherited methods — so it serves `Tile/Load` while a stub emitted on the base would invoke `CardBase/Load`. Nothing serves that id and the page gets "action not found", on both sides silently. | Declare the action on the concrete component. An alias is not the answer: the descriptor carries the component TYPE to invoke on, so two children inheriting one action give the base's id two answers — the owner of an inherited action is a decision, not a patch. |

## EQ2100–EQ2101 — resx templates

The localization contract, checked on the build machine rather than when a visitor arrives.

| Code | Meaning | What to do |
|---|---|---|
| `EQ2100` | About **this call**: the template must be a valid composite format whose specifiers are inside the supported subset, and the call must pass every hole it declares. | Fix the argument count, or drop the out-of-subset specifier (alignment such as `{0,10}` is outside v1). |
| `EQ2101` | About the **translations**: every culture's resx is held against the neutral one, which is the arity contract. | Fix the culture's template — a dropped or extra `{n}` fails the build, not the page. |

## EQ2102–EQ2107, EQ2112 — the client/server boundary

A client component reached for an API that only exists on a server. The bridge is `[ServerAction]`.

| Code | Meaning |
|---|---|
| `EQ2102` | Direct database access (`Microsoft.EntityFrameworkCore`, `System.Data`). |
| `EQ2103` | Networking (`System.Net.Http`, `System.Net.Sockets`). |
| `EQ2104` | OS threading and locking (`Thread`, `Monitor`, `Mutex`, `Semaphore` — not `Task`). |
| `EQ2105` | Spawning processes (`System.Diagnostics.Process`). |
| `EQ2106` | Native interop / P-Invoke (`System.Runtime.InteropServices`). |
| `EQ2107` | Runtime IL generation (`System.Reflection.Emit`). |
| `EQ2112` | File-system access (`System.IO`). |

`EQ2112` sits apart from its family on purpose: `System.IO` was reported as `EQ2101` until that
code turned out to belong to the resx check above, which the wiki already published. It is not a
numbering slip to tidy back.

## EQ2108–EQ2111 — one meaning per target, or none

The construct translates, but would READ differently on the server and in the browser. Rather
than pick for you, eqc asks.

| Code | Meaning | What to do |
|---|---|---|
| `EQ2108` | A `CultureInfo` the browser cannot follow: formatting (`ToString`, `string.Format`) crosses `InvariantCulture` and `CurrentCulture`, and reading a number (`Parse`/`TryParse` on any numeric type, `Convert.ToInt32` and its siblings of a string, `Convert.ToDecimal` of a string or of a value that may hold one, such as an `object`) only `InvariantCulture`. `Convert.ToBoolean` never consults its provider but C# evaluates it, so it crosses `InvariantCulture`, `CurrentCulture` and a null, which are reads with no effect, and any other provider an app writes, while another `CultureInfo` (`GetCultureInfo(name)`, `new CultureInfo(name)`), which may throw, has no twin to evaluate it. The culture is the property the provider binds to, however it is spelled: a member of another type that is also called `InvariantCulture` is refused, since it may return any culture. | Format with an explicit specifier (`ToString("N2")` follows the app's culture on both targets), or convert and read with the invariant culture. |
| `EQ2109` | `ToString(CultureInfo.CurrentCulture)` with no specifier — the general format is outside the tested `Intl` subset. | Name the format: `ToString("N2")`, `ToString("F1")`. |
| `EQ2110` | A fractional number written as text, or a number read from it (or from a value that may hold text, such as an `object`), with no culture at all: C# follows the request's culture (a comma, in `pt`) and the browser is always invariant. | Say which you mean. |
| `EQ2111` | `GetService` with no type argument to cross — the registry is keyed by the interface NAME, and there is nothing to key on. | Call the generic overload. |
| `EQ2113` | `ConfigureAwait` is dropped — there is one context to resume on — and dropping it would discard a NON-CONSTANT argument without evaluating it. | Pass a constant, or evaluate the expression into a local first. |

## EQ3001–EQ3006 — the Photon app generator

| Code | Meaning |
|---|---|
| `EQ3001` | A Photon app has no program. |
| `EQ3002` | A Photon app has more than one program. |
| `EQ3003` | A capability's reason must be a constant. |
| `EQ3004` | An entitlement's key must be a constant — it is signed into the app at build time, so a key built at run time never reaches the signature. |
| `EQ3005` | A bundle fact must be constant — the Info.plist is written at build time, so an argument built at run time never reaches the app's manifest. Either argument counts, and whatever the method takes: a string, a bool, an `AppCategory`. |
| `EQ3006` | A Photon app writes its own entry point. The SDK generates one from `CreateApp`, so an app that kept its `Main` (or its top-level statements) has two — which the compiler reports as `CS0017` against a generated file the app has never seen. Delete the app's: whatever it did first belongs in `CreateApp`, which is the only half Android runs. |

## EQ3101–EQ3105 — the source generators

The generated factory surface and form models.

| Code | Meaning |
|---|---|
| `EQ3101` | A component elects more than one factory constructor (`[UiFactory]`). |
| `EQ3102` | Two components share a name. |
| `EQ3103` | A form model property has a type no text box can hold. |
| `EQ3104` | A validation attribute has no rule to become. |
| `EQ3105` | A `[Compare]` names a property this form does not have. |

## EQ4xxx — the SDK build

Raised by the SDK's own MSBuild targets, before or after the compiler runs.

| Code | Meaning | What to do |
|---|---|---|
| `EQ4001` | The eQuantic icon tool — `eqicon`, which writes app icons, the vector catalog, the capability manifest and the macOS bundle — is missing where the SDK looks for it. In a build from the repository's source tree, `eQuantic.UI.Native.Build` did not build before this project: a broken build graph. From a package, the package shipped without `tools/net10.0/eqicon.dll`. | Neither is fixed by building twice. From source, the tool is a `ProjectReference` of every app project, so look at the errors above this one — the tool's own build failed — or at whoever removed the edge. From a package, clear the eQuantic cache (`dotnet msbuild -t:ClearEQuanticCache`), restore again, and report the package version if it persists. |
| `EQ4002` | The eQuantic compiler — `eqc`, which turns the app's components into JavaScript — is missing where the SDK looks for it: in a build from the repository's source tree, `eQuantic.Build` did not build before this project; from a package, the package shipped without `tools/net10.0/eqc.dll`; or an explicit `EqcCliPath` names a file that is not there. This used to be a warning, and a page with no JavaScript. | As EQ4001: not fixed by building twice. From source, read the errors above this one or restore the `ProjectReference`; from a package, clear the eQuantic cache and restore; with an explicit `EqcCliPath`, fix the path. |
| `EQ4003` | *(warning)* The app will be signed with a hardened-runtime exception (`com.apple.security.cs.*` — the family that relaxes a protection the hardened runtime imposes) while the build is not hardened. The key reaches the signature and the system never consults it. The SDK signs with `--options runtime` only when `EQuanticHardenedRuntime` is `true`, so any other non-empty value is an unhardened build — `false`, `off`, and the typo `ture` alike — and the message quotes back what was written. An EMPTY value is an ordinary development build and is never reported. | Set `EQuanticHardenedRuntime=true`, or drop the declaration. Not `EQuanticSigningIdentity`: that turns hardening on only where the property is empty, so it does not answer this. Note what this does NOT mean: the App Sandbox's permissions (`com.apple.security.network.*`, `…files.*`, `…device.*`) answer to `PhotonEntitlements.AppSandbox`, not to the hardened runtime, and an ad-hoc development build honours them — so they are never reported here. |
