# Track D, second round: a phone's capabilities in C#

Written 2026-10-11. Track D's first round (D1–D7, closed 2026-08-04, its plan retired into
[`LEDGER.md`](LEDGER.md)) made device capabilities services: a contract in `Primitives`, a
declaration on `builder.Capabilities` that the generator turns into Info.plist keys and Android
permissions, a realization per shell, and `null` or `IsAvailable == false` where a host has none.
This round asks what that model owes a PHONE, measured against everything iOS and Android offer a
developer today, and how C# reaches the part no .NET binding reaches.

The work is the epic [#774](https://github.com/eQuantic/equantic-ui/issues/774); its children are
D8 onward. The measurement behind every number here is the platform catalog:

| Catalog | Rows | What a row says |
|---|---:|---|
| [`platform-catalog/ios.md`](platform-catalog/ios.md) | 355 | An iOS 26 or 27 capability, its framework, the minimum iOS, whether it is AI, how code outside Swift reaches it, whether dotnet/macios binds it, and what covers it in eQuantic |
| [`platform-catalog/android.md`](platform-catalog/android.md) | 323 | An Android 16 or 17, Jetpack, Play services or Google SDK capability, its layer, the minimum API, whether it is AI, how C# reaches it, the .NET binding today, and what covers it in eQuantic |
| [`platform-catalog/bridge-research.md`](platform-catalog/bridge-research.md) | — | How .NET reaches Swift and Kotlin, how a native library travels in a package, extensions, App Intents outside Xcode, coroutines, AOT, prior art, the hard limits and the risks, each with its source |

Each catalog cites its primary sources (Apple's and Google's documentation, the API diffs,
dotnet/macios, dotnet/android-libraries) at its head, and marks what they did not confirm as
`unverified`. Refreshing them is [#805](https://github.com/eQuantic/equantic-ui/issues/805).

## Where the phones stand

On `main` at `2006921`: twelve device contracts exist; the iOS shell realizes eight, the Android
shell seven. Android has no `ICamera`; neither phone realizes `ITextClipboard` or `IDeepLinks`;
`IWorkspace` and `IFileDialogs` are desktop-only by their own contracts. Four declarations
(`UseContacts`, `UseNotifications`, `UseMicrophone`, `UsePhotoLibraryAdd`) write a permission no
API uses. Nothing has run on a physical device. Since then the Android shell realizes `ICamera`
([#775](https://github.com/eQuantic/equantic-ui/issues/775)), and the wiki's Capabilities page keeps
the count per contract and shell, compared with the registrations by `WikiShellRealizationsTests`.

Two defects found while cataloguing come before any new capability on iOS:

- [#796](https://github.com/eQuantic/equantic-ui/issues/796): built with the iOS 27 SDK, an app did
  not launch, because the iOS shell had no scene life cycle (TN3187). Fixed: the window belongs to a
  scene delegate and the SDK declares the scene manifest.
- [#795](https://github.com/eQuantic/equantic-ui/issues/795): every Apple callback went through
  `Marshal.GetFunctionPointerForDelegate`, the shape Mono full-AOT does not prepare, and a device
  Release build is the first to run it. Fixed: a block and a registered method call static
  `[UnmanagedCallersOnly]` entry points that find their object in a handle the block or the delegate
  carries. The device run itself is [#792](https://github.com/eQuantic/equantic-ui/issues/792)'s.

## The routes, counted

A route is how C# reaches a capability. It is derived from each catalog's reachability and binding
columns; the two catalogs' tables are the evidence.

| Route | iOS | Android | What it costs |
|---|---:|---:|---|
| Bound today | 179 | 193 | A contract and a realization; the binding already ships in `Microsoft.iOS`, `Mono.Android` or a dotnet/android-libraries package |
| Bound in part, or a binding to write | 18 | 24 | Objective-C, C or Java with no binding: the Apple shell already calls Objective-C directly, and an Android binding is ours to write |
| Swift only | 85 | — | The Apple bridge, [#798](https://github.com/eQuantic/equantic-ui/issues/798) |
| Kotlin only (`suspend`, `Flow`) | — | 16 | The Android bridge, [#799](https://github.com/eQuantic/equantic-ui/issues/799) |
| Generated per app | 8 | 25 | iOS: Swift the developer's Xcode compiles (App Intents). Android: a service, receiver or provider the generator writes in C# |
| An app extension | 40 | — | 20 Objective-C extension points that need their own target, 20 SwiftUI surfaces drawn by the system |
| API 37 and later | — | 36 | `net10.0-android` stops at 36.1, [#804](https://github.com/eQuantic/equantic-ui/issues/804) |
| Compose or KSP only | — | 12 | No C# route; the platform's equivalent where there is one |
| Written by the SDK's build | 13 | — | Info.plist, entitlements, icons, launch screen |
| A web service | 11 | — | Called from server C#; no bridge |
| Unverified, community, NDK, no API | 1 | 17 | See the catalog |

54 Android rows need Google Play services, so the contract answers unavailable on Huawei, Amazon and
AOSP devices; 24 need a Play Console declaration. 60 iOS rows need an entitlement, some of them
Apple's approval. AI is 54 rows on iOS and 75 on Android, and most of what is new in it is on the
bridged side.

## The bridge

The contract comes first and the connector is an implementation detail. A contract speaks the
vocabulary (no vendor's word in a member name); a shim behind it can be replaced by a future .NET
Swift projection, or by a vendor's own .NET SDK, without a public break. Nothing here comes from
MAUI: not Essentials, not the Community Toolkit's interop build tasks, not as a build dependency.
Everything below is the plain .NET for iOS and .NET for Android workloads, dotnet/runtime,
dotnet/java-interop and dotnet/android-libraries; the audit is section 14 of the research.

### Swift (#798)

- One Swift file per capability family, exported through the C ABI (`@c`, or `@_cdecl`), each with
  an `eq_<family>_is_available()` guarded by `if #available`.
- Compiled by the SDK's CI on a macOS runner, with a pinned toolchain in the manner of
  `slang-toolchain.sh`, into a **dynamic, weak-linked** `EqBridge.xcframework`. Dynamic because the
  SDK does not dictate the developer's Xcode (`ValidateXcodeVersion=false`): the app's `ld` links
  either kind, but a static archive hands it Xcode N's object code, which an older `ld` can refuse,
  while a framework hands it only load commands and exports. That narrows the dependency without
  removing it, so a guard links a probe app against the framework with the oldest Xcode the SDK
  supports and fails when that `ld` refuses it. Weak because the app's floor is iOS 15 and
  Foundation Models starts at 26.
- Carried as a `NativeReference` of an `IsBindingProject=true` project, the one shape in which
  .NET for iOS packs a native framework into a NuGet, referenced by `Shell.iOS` so an app links and
  embeds it without declaring anything.
- C# calls through `LibraryImport`; Swift calls back through `[UnmanagedCallersOnly]` static entry
  points with a `GCHandle` context. Text crosses as UTF-8, a buffer as a pointer and a length, an
  error as a code and a message, structured data as source-generated JSON, cancellation through a
  handle, a stream as an `IAsyncEnumerable` over a channel.
- One writer, like the shaders: the Swift in the repository is normative, the script is the only
  thing that writes the binary, and a guard fails when the two disagree.

### Kotlin (#799)

- An `.aar` built by a pinned Gradle (wrapper checksum) in CI, carried as an `AndroidLibrary` of a
  `net10.0-android` project.
- Its Maven dependencies are NuGet dependencies on the `Xamarin.*` packages, never bundled jars: a
  bundled `androidx.core` is "type defined multiple times" the moment another package brings it.
- A Java-shaped facade: a callback and a cancel handle for a `suspend` function, a collector feeding
  a C# channel for a `Flow`. Where the vendor ships a Java variant (`ListenableFuture`, `*Async`
  with an executor), that is used and no Kotlin is written.
- Consumer R8 keep rules for every class C# reaches by name.

### Generated per app

- **Android: C# first.** The generator writes `[Service]` and `[BroadcastReceiver]` subclasses and
  the Android build writes their Java Callable Wrappers and manifest entries, as it already does
  for `MainActivity`. Resource XML comes from `eqicon`. `AndroidJavaSource Bind="false"` is the
  escape hatch; `javac` already runs in every Android build. Never per-app Kotlin: kotlinc is not
  in the developer's toolchain.
- **iOS: Swift only where Apple reads the source at build time**, which today means App Intents.
  The SDK generates the intent types from a C# declaration, their `perform()` calls back into C#,
  and the developer's Xcode compiles them inside an MSBuild target and runs
  `appintentsmetadataprocessor` (Bazel's `rules_apple` proves it runs without an Xcode project).
  The tool is undocumented and its flags move with Xcode, and an iOS build paired from Windows
  cannot run it.

### A generic extension (#803)

Home Screen surfaces and Live Activities are SwiftUI the system draws in another process. The SDK
writes one generic `.appex`, ships it precompiled, and per app rewrites its bundle identifier,
writes the App Group entitlement and embeds it with `AdditionalAppExtensions`; it renders what the
app writes to the App Group. A Live Activity's attributes must be the same Swift type in the app and
in the extension, so there is one, defined by the SDK, whose state carries the payload. On Android
the `AppWidgetProvider` behind a `RemoteViews` surface is C#, and Live Updates are notifications,
bound today.

### The developer's machine

Nothing new. Xcode and the Android SDK with a JDK are already required. The bridge's Swift, and all
of its Kotlin and Gradle, are compiled on the SDK's CI; the one Swift compiled per app, App Intents'
generated types, is compiled by the Xcode the developer already has. The csproj stays
`<Project Sdk="eQuantic.UI.Sdk">`. Heavy dependencies (ML Kit GenAI's fourteen packages, Health
Connect, Play Billing) are opt-in packs on the icon-pack precedent, since a `PackageReference` is
settled at restore before any generator runs, with an analyzer error when a builder call needs a
pack the project does not reference. The SDK logs what it adds to a manifest, a plist or the
entitlements, as it does for entitlements on macOS.

## On-device AI

| What an app wants | iOS | Android | Issue |
|---|---|---|---|
| A language model on the device: sessions, streaming | Foundation Models (iOS 26; needs Apple Intelligence), Swift only | Gemini Nano through ML Kit GenAI's Prompt API (AICore, supported devices, foreground only, a quota); the .NET binding is `1.0.0-alpha1` against Google's `beta4` | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| A typed answer from a C# type | `@Generable` is a macro; `DynamicGenerationSchema` built at run time from the C# type | The schema travels with the prompt (the typed-output API is KSP) | #800 |
| Speech to text, text to speech | `SFSpeechRecognizer` and `AVSpeechSynthesizer` bound; `SpeechAnalyzer` (26) Swift only | `SpeechRecognizer` and `TextToSpeech` bound; GenAI speech recognition Kotlin only | [#801](https://github.com/eQuantic/equantic-ui/issues/801) |
| Translation | `TranslationSession` Swift only, usable without SwiftUI from 26.4 | ML Kit Translate and the platform service (API 31) bound | #801 |
| Summarize, proofread, rewrite, describe an image | Foundation Models; Writing Tools in system text views | ML Kit GenAI feature APIs (beta), no binding | #801 |
| Text, faces and codes in an image | Vision, bound | ML Kit, bound | [#785](https://github.com/eQuantic/equantic-ui/issues/785), [#786](https://github.com/eQuantic/equantic-ui/issues/786) |
| An assistant acts in the app | App Intents (Siri, Apple Intelligence's schemas, Siri AI in 27) | AppFunctions: the platform `AppFunctionService` (36) is Java; the Jetpack library is KSP | [#802](https://github.com/eQuantic/equantic-ui/issues/802) |

## What no bridge reaches

1. **UI the system draws in another process**: WidgetKit's surfaces, Live Activities, the Dynamic
   Island, Control Center controls, Siri snippets, Glance. Photon does not draw there; the generic
   extension above is the route.
2. **APIs that exist only at compile time**: App Intents, `@Generable`, SwiftData's `@Model`,
   Jetpack AppFunctions (KSP), anything `@Composable`. Generated Swift reaches the iOS ones; KSP and
   Compose are a wall, met with the platform's equivalent (`AppFunctionService`, `RemoteViews`,
   SQLite) where it exists.
3. **C# inside an extension process**: .NET builds share and notification-service extensions, but
   the memory ceiling is tens of megabytes and Photon is not there.
4. **Provisioning**: App Groups, HealthKit, CarPlay and Family Controls must exist in the
   developer's Apple account; the SDK writes every file and grants nothing.

## Risks

- Two native CI legs (macOS with Xcode; JDK, Gradle and Kotlin), each pinned.
- The runtime under the bridge changes: .NET 11 moves Android and iOS to CoreCLR. A C-ABI bridge
  with `[UnmanagedCallersOnly]` does not care, and since #795 neither do the shell's callbacks.
- Lagging bindings (GenAI Prompt, Biometric, Firebase Messaging, LiteRT); the Kotlin bridge is also
  how the SDK stops depending on their cadence.
- "Every capability" has no end, so the order below, and a catalog refreshed after each WWDC and
  each Google I/O (#805).

## Order

1. **The defects**: #796 and #795.
2. **What needs no bridge**: the epic's contracts D8–D23 and the phone prerequisites outside it
   (text input [#770](https://github.com/eQuantic/equantic-ui/issues/770), the lifecycle
   [#771](https://github.com/eQuantic/equantic-ui/issues/771), the back gesture
   [#772](https://github.com/eQuantic/equantic-ui/issues/772), image decoding on Android
   [#302](https://github.com/eQuantic/equantic-ui/issues/302)).
3. **The two bridges**, each landing with one small consumer proven on a device: #798 with
   translation, #799 with the GenAI text tools.
4. **On-device AI**: #800 and #801.
5. **Assistants and agents**: #802.
6. **The system's surfaces**: #803.
7. **Decisions**: API 37 and .NET 11 (#804), augmented reality
   ([#791](https://github.com/eQuantic/equantic-ui/issues/791)), and the physical-device pass
   ([#792](https://github.com/eQuantic/equantic-ui/issues/792)).
