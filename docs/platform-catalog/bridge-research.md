# Reaching every platform API from C# on Photon (iOS and Android)

Research report for eQuantic.UI, as of 2026-10-11. It asks how the SDK can offer every platform
capability (Swift-only Apple frameworks, Kotlin-first Android libraries) to a developer who writes
only C#, `appsettings` and fluent `Program.cs`. It is measured against `CLAUDE.md`'s product
principle.

**Owner constraint (applied throughout): no MAUI dependency.** No `Microsoft.Maui.*` package, no
MAUI Essentials, and no CommunityToolkit `Maui.NativeLibraryInterop` package or build task appear in
any recommendation. MAUI's source and docs are read as prior art only. Every mechanism recommended
here is either part of the plain .NET for iOS (dotnet/macios, `Microsoft.iOS`) and .NET for Android
(dotnet/android, `Mono.Android`) workloads, or is our own MSBuild targets and our own Swift/Kotlin
shims. **§14 ("MAUI audit")** names each item and says where it comes from.

---

## 0. TL;DR

* **iOS bridge.** The SDK owns one thin Swift shim per capability family, exported through the
  **C ABI** (`@c` from Swift 6.3, or `@_cdecl`). SDK CI precompiles it into a **dynamic
  `.xcframework`**. It ships as a `NativeReference` of a small **binding-style project**
  (`IsBindingProject=true`), so it lands in the NuGet as `<Assembly>.resources.zip` and every app
  links and embeds it with no action of its own. C# calls it with `LibraryImport`. Swift calls back
  into C# through `[UnmanagedCallersOnly]` function pointers and a `GCHandle` context. **Per-app
  Swift is generated only where Apple reads the source at build time**: App Intents metadata, and
  extension configuration that cannot be generic. The app's own Xcode compiles it with
  `xcrun swiftc`. That Xcode is already required.
* **Android bridge.** Use Java-friendly APIs straight through the existing **dotnet/android-libraries**
  bindings (`Xamarin.AndroidX.*`, `Xamarin.Google.*`, `Xamarin.GooglePlayServices.*`). These are not
  MAUI. Write a **Kotlin shim** only for `suspend`/`Flow`/Compose-only surfaces. SDK CI compiles it
  into an **`.aar`**, which ships beside a small `net10.0-android` assembly (`AndroidLibrary`). Its
  Maven dependencies are expressed as **NuGet dependencies** on the Xamarin.* packages, never bundled
  as jars. **Per-app code is C# first.** A source generator writes `[Service]`/`[BroadcastReceiver]`
  subclasses, and the Android build turns them into Java Callable Wrappers and manifest entries.
  Per-app Java (`AndroidJavaSource Bind="false"`) comes second, compiled by the `javac` every
  Android build already runs. **Never per-app Kotlin**: kotlinc/Gradle are not part of the
  developer's toolchain.
* **The developer's machine needs nothing new.** That means Xcode for iOS, and the Android SDK plus
  a JDK for Android. Gradle, Kotlin and Objective Sharpie are needed only on the SDK's CI and on
  framework contributors' machines.
* **Hard limits.** Some UI is drawn in another process by SwiftUI or RemoteViews: widgets, Live
  Activities, Control Center controls. Some APIs exist only as compile-time metadata: App Intents,
  `@Generable`, SwiftData `@Model`, Android AppFunctions (KSP), Compose and Glance. None of this can
  run C# or Photon. It can be reached only through a **generic precompiled extension driven by data**
  or through **per-app generated Swift**. Android KSP and Compose are a real wall.

---

## 1. How .NET for iOS reaches Objective-C, and what it cannot reach

**Mechanism.** `Microsoft.iOS` is a set of generated bindings. Binding authors write `ApiDefinition.cs`
interfaces (`[BaseType]`, `[Export("selector:")]`, `[Static]`, `[Async]`). The `bgen` tool turns
them into C# that sends messages through per-signature `objc_msgSend` P/Invokes. C# subclasses of
`NSObject` become Objective-C classes through `[Register]`/`[Export]` and the *registrar*. The
registrar is static at build time, managed-static for NativeAOT, or dynamic. Third-party Objective-C
libraries are bound the same way, in a binding project (`IsBindingProject=true`,
`ObjcBindingApiDefinition`, `NativeReference`). Objective Sharpie bootstraps the API definition from
headers. In 2026 Sharpie is distributed as an open-source .NET tool inside dotnet/macios
(`tools/sharpie`) ([Objective Sharpie](https://learn.microsoft.com/dotnet/maui/ios/objective-sharpie)).
A community post argues it lags modern Xcode and has no Swift support
([dev.to/wojo](https://dev.to/feed/wojo)).

**What is NOT bound because it is Swift-only.** dotnet/macios tracks these under the
[`requires-swift` label](https://github.com/dotnet/macios/issues?q=label%3Arequires-swift):
WidgetKit (#8933, #9215 for `reloadTimelines`), ActivityKit (#17038), StoreKit 2 (#16893), TipKit
(#20273), MusicKit (#17894), GroupActivities (#15597), CryptoKit/RealityKit (#6239), SwiftUI (#6235),
VisionKit `DataScannerViewController` (#22147) and `ImageAnalyzer` (#18318), Declared Age Range
(#24089), LockedCameraCapture (#20752) and MLX (#21811). **FoundationModels has no issue at all**:
a search of dotnet/macios returned zero results. App Intents, Translation (`TranslationSession`),
`SpeechAnalyzer` (iOS 26) and the Swift-concurrency surfaces of many other frameworks belong to the
same class. Rule of thumb: anything that is a Swift `struct`/`enum`/`actor`, a protocol with
associated types, an `async` function, a macro, or a SwiftUI `View` has no Objective-C header, so
`bgen` cannot see it.

## 2. .NET's direct Swift interop (CallConvSwift) in 2026

* **Primitives (.NET 9+).** The runtime adds four pieces:
  [`CallConvSwift`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callconvswift);
  [`SwiftSelf`/`SwiftSelf<T>`](https://learn.microsoft.com/en-ca/dotnet/api/system.runtime.interopservices.swift.swiftself?view=net-9.0)
  (the self register; `<T>` is a frozen struct);
  [`SwiftError`](https://learn.microsoft.com/en-au/dotnet/api/system.runtime.interopservices.swift.swifterror?view=net-9.0)
  (the error register, so `throws` works); and
  [`SwiftIndirectResult`](https://learn.microsoft.com/en-au/dotnet/api/system.runtime.interopservices.swift.swiftindirectresult)
  (the return buffer for non-frozen structs). They were designed in
  [dotnet/runtime#64215](https://github.com/dotnet/runtime/issues/64215), for P/Invoke and reverse
  P/Invoke.
* **Production proof.** dotnet/runtime itself ships Swift in every Apple app.
  `System.Security.Cryptography.Native.Apple/pal_swiftbindings.swift` exports CryptoKit functions with
  `@_silgen_name`, and C# calls them with
  `[LibraryImport] [UnmanagedCallConv(CallConvs=[typeof(CallConvSwift)])] … out SwiftError error`
  ([Swift](https://github.com/dotnet/runtime/blob/main/src/native/libs/System.Security.Cryptography.Native.Apple/pal_swiftbindings.swift),
  [C#](https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/OSX/System.Security.Cryptography.Native.Apple/Interop.Aead.cs)).
  For the same reason dotnet/macios now sets **`LinkWithSwiftSystemLibraries=true` unconditionally**
  ("Runtime now requires swift"), so every .NET iOS link line already carries the Swift library
  search paths
  ([Xamarin.Shared.Sdk.targets, `_AddSwiftLinkerFlags`](https://github.com/dotnet/macios/blob/main/dotnet/targets/Xamarin.Shared.Sdk.targets)).
* **Projection tooling.** The official effort, the runtimelab `feature/swift-bindings` branch with
  experimental `Swift.Bindings` packages, was "experimental in .NET 10". Its tracking issue
  [#95633](https://github.com/dotnet/runtime/issues/95633) closed in April 2025. The umbrella issue
  [#95638](https://github.com/dotnet/runtime/issues/95638) calls .NET 9 support "very limited and
  most of it is experimental" and keeps it Apple-only. A June 2026 report says the runtimelab tool
  "hasn't been updated for over a year" and cannot bind system frameworks
  ([In The Hand](https://inthehand.com/2026/06/06/net-maui-matter-and-swift/)).
* **The active successor is community-owned.**
  [justinwojo/swift-dotnet-bindings](https://github.com/justinwojo/swift-dotnet-bindings) is a fork
  of runtimelab with 1,000+ commits, .NET 10 only and Xcode 26+. It covers classes, frozen and
  non-frozen structs, enums with payloads, protocols (witness tables), generics, closures, and
  **async through *generated Swift wrappers***. It publishes `SwiftBindings.Apple.*` for StoreKit 2,
  ActivityKit, Translation, TipKit, MusicKit, WeatherKit, CryptoKit, FamilyControls, RealityKit and
  others. FoundationModels and WidgetKit are not in that list. 94–98% of its calls go through
  **plain C calling conventions** (thunks and `@_cdecl` wrappers). Direct `CallConvSwift` is the
  "last resort" (`SB0001`).
  [Known limitations](https://github.com/justinwojo/swift-dotnet-bindings/wiki/Known-Limitations):
  * only dynamic xcframeworks, built with `BUILD_LIBRARY_FOR_DISTRIBUTION`, and macOS with Xcode
    for authors *and* app builders;
  * Mono JIT crashes and assertions on the simulator, and an "untyped `SwiftSelf`" risk under Mono
    full-AOT;
  * NativeAOT struct-lowering bugs (float fields in GPRs, integer structs larger than 16 bytes);
  * no PATs/`Self` requirements, Combine, variadic generics, deep SwiftUI state, or
    `[String: Any]`;
  * and, decisive for us: **"Per-app C#-defined `ActivityAttributes` types [unsupported]. Live
    Activities work through a fixed Swift-defined attributes type."**
* **Reachability verdict for 2026:**

  | Swift feature | Reachable from C# |
  |---|---|
  | Synchronous free functions, frozen structs, `throws` | Yes, directly (CallConvSwift) |
  | Classes / ARC | Yes, with manual `swift_retain`/`swift_release` or tooling |
  | Generics and protocols | Only with tooling that passes type metadata and witness tables |
  | `async` | **No direct path**: the Swift async ABI (async context, `swiftasync` CC) has no .NET counterpart, so every tool wraps it in Swift |
  | Macros, SwiftUI `some View`, property wrappers | No |

  **Recommendation.** Do not build on the projection tools. The SDK needs a few dozen hand-designed
  entry points. A C-ABI shim behaves the same on Mono AOT (.NET 10), CoreCLR (.NET 11) and NativeAOT,
  and depends on no experimental or single-maintainer project. `CallConvSwift` + `SwiftError` stay
  an option for synchronous `throws`, where the runtime is the precedent.

## 3. The Native Library Interop ("slim bindings") pattern, as prior art only

* **What it is.** You write a thin wrapper in the native language, exposing only primitives and
  platform types .NET already knows. You build it with the native toolchain and bind the tiny
  surface
  ([overview](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/)).
  On iOS the wrapper is `@objc(Name) public class … : NSObject` with `@objc(sel:)` methods using
  `NSData`/`String`/`NSError`/completion blocks. Sharpie reads `Module-Swift.h` to produce
  `ApiDefinition.cs`, which is hand-maintained afterwards
  ([get started](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/get-started)).
  On Android it is a Java/Kotlin class with static methods, and the binding generator binds it
  automatically.
* **The MSBuild part now lives in the base workloads, not in MAUI.**
  * **`@(XcodeProject)`** (dotnet/macios, .NET 9) runs `xcodebuild` archive + `-create-xcframework`
    for each item in `_BuildXcodeProjects`, then adds the output as `NativeReference`. Metadata:
    `SchemeName`, `Configuration` (Release), `CreateNativeReference` (true), `OutputPath`, and the
    forwarded `Kind`/`Frameworks`/`SmartLink`
    ([iOS build items](https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-items)).
  * **`@(AndroidGradleProject)`** (dotnet/android, .NET 9) runs `gradlew` on a
    `build.gradle(.kts)`. Metadata: `ModuleName`, `Configuration`, `OutputPath`,
    `CreateAndroidLibrary`. The AAR output becomes `AndroidLibrary`, with `Bind`/`Pack` forwarded
    ([Android build items](https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-items),
    [native library interop](https://learn.microsoft.com/en-us/dotnet/android/binding-libs/advanced-concepts/native-library-interop)).
  * The CommunityToolkit repo is now mostly templates and samples
    ([repo](https://github.com/CommunityToolkit/Maui.NativeLibraryInterop)).
* **Whose machine needs the toolchain.** Only the machine that builds the binding project: Xcode
  (with command-line tools), Android SDK, JDK 17, Gradle (bootstrapped by the wrapper, with network)
  and Sharpie. A **consumer of the packed NuGet** needs none of them. It receives the `.xcframework`
  inside `<asm>.resources.zip`, and the `.aar` beside the dll. The docs warn that *"gradle/maven
  dependencies … are not automatically bundled into your library"*, so the consumer must still
  satisfy them.
* **What to copy.** (a) The wrapper speaks only primitives, so the C# surface is ours and stable
  while vendor SDKs churn. (b) The native project is built *at pack time*, not at app build time.
  (c) Maven dependencies are verified, not bundled.
* **What to avoid.** (a) The `@objc` + Sharpie + hand-maintained `ApiDefinition` loop; a C ABI
  needs no generator. (b) Running `XcodeProject`/`AndroidGradleProject` in the consumer path; keep
  them, at most, for the SDK's own dev loop.

## 4. Exporting from Swift without Objective-C

* **`@_cdecl("name")`** has worked for years (underscored). **`@c`** formalises it:
  [SE-0495](https://forums.swift.org/t/accepted-se-0495-c-compatible-functions-and-enums/82833),
  shipped in **Swift 6.3 (March 2026)**. It writes the declaration into the generated C header,
  accepts a custom symbol (`@c(eq_fm_respond)`), supports `@c` enums with integer raw types, and
  pairs with `@implementation` to validate against an existing header
  ([Swift 6.3 release](https://swift.org/blog/swift-6.3-released/)). Use `@_cdecl` until the
  oldest Xcode the SDK supports carries Swift 6.3.
* **Only C-representable types cross:**
  * fixed-width ints, `Bool`, `Double`;
  * `Unsafe[Mutable]RawPointer`, `UnsafePointer<CChar>`, `OpaquePointer`;
  * `@convention(c)` function pointers.
  
  Not `String`, `Array`, `Data`, capturing closures, `throws` or `async`.
* **Async bridge pattern.** It is the one the Rust `foundation-models` crate uses over the same
  framework ([docs.rs](https://docs.rs/foundation-models)): *"Raw FFI declarations matching the Swift
  `@_cdecl` exports"*, with async futures, streaming through a callback, dropping to cancel, and
  `@Generable` replaced by a runtime `DynamicGenerationSchema`.

  ```swift
  public typealias EqDone = @convention(c) (UnsafeMutableRawPointer?, UnsafePointer<CChar>?, Int32, UnsafePointer<CChar>?) -> Void

  @c(eq_fm_respond)   // or @_cdecl("eq_fm_respond")
  public func eqFmRespond(_ session: OpaquePointer, _ prompt: UnsafePointer<CChar>,
                          _ ctx: UnsafeMutableRawPointer?, _ done: EqDone) -> OpaquePointer {
      let text = String(cString: prompt)
      let task = Task {
          do {
              let reply = try await box(session).respond(to: text).content
              reply.withCString { done(ctx, $0, 0, nil) }          // valid ONLY during the call
          } catch {
              "\(error)".withCString { done(ctx, nil, code(of: error), $0) }
          }
      }
      return OpaquePointer(Unmanaged.passRetained(TaskBox(task)).toOpaque()) // cancel handle
  }
  @c(eq_cancel) public func eqCancel(_ h: OpaquePointer) { Unmanaged<TaskBox>.fromOpaque(UnsafeRawPointer(h)).takeRetainedValue().task.cancel() }
  ```

  ```csharp
  [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
  private static unsafe partial nint eq_fm_respond(nint session, string prompt, nint ctx,
      delegate* unmanaged<nint, byte*, int, byte*, void> done);

  [UnmanagedCallersOnly]                       // static, registered at compile time: AOT-safe everywhere
  private static unsafe void Done(nint ctx, byte* text, int code, byte* error)
  {
      var tcs = (TaskCompletionSource<string>)GCHandle.FromIntPtr(ctx).Target!;
      GCHandle.FromIntPtr(ctx).Free();
      if (text != null) tcs.TrySetResult(Utf8StringMarshaller.ConvertToManaged(text)!);
      else tcs.TrySetException(new PlatformException(code, Utf8StringMarshaller.ConvertToManaged(error)));
  }
  ```

  **Marshalling rules worth fixing once, in one place:**
  * strings cross as UTF-8 `const char*`, valid only for the callback, and the receiver copies;
  * byte buffers cross as `(ptr, len)` under the same rule;
  * anything structured crosses as **JSON**, through `System.Text.Json` source generation (trim-
    and AOT-safe), or a blittable struct;
  * errors cross as `(code, message)`, or `SwiftError` for synchronous `throws`;
  * cancellation is an exported `eq_cancel(handle)` hooked to `CancellationToken.Register`;
  * `AsyncSequence` becomes a per-element callback feeding a bounded
    `System.Threading.Channels.Channel<T>`, exposed as `IAsyncEnumerable<T>`;
  * `@MainActor` APIs are hopped *inside Swift* (`Task { @MainActor in … }`), so C# never has to
    know.

## 5. Shipping a precompiled `.xcframework` so the app "just links it"

* **The packaging convention.** A `NativeReference` in a binding project is copied, at build time,
  into a *binding resource package*: `$(AssemblyName).resources/` or `.resources.zip`. The zip is
  the default (`CompressBindingResourcePackage=auto`), and "this also applies to how native
  references are stored inside NuGets". `NoBindingEmbedding=true` is the default and is required
  for xcframeworks
  ([iOS build properties](https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties)).
  The consuming app treats a `.resources(.zip)` beside an assembly as `AppleBindingResourcePackage`:
  *"we'll link with static libraries, link with and embed dynamic libraries and frameworks"*
  ([BundleContents.md](https://github.com/dotnet/macios/blob/main/dotnet/BundleContents.md)).
* **It must be a binding project.** `_CreateBindingResourcePackage` is wired into
  `CompileDependsOn` only when `IsBindingProject == true`
  ([Xamarin.Shared.targets](https://github.com/dotnet/macios/blob/main/msbuild/Xamarin.Shared/Xamarin.Shared.targets)).
  A plain class library's `NativeReference` does not travel in its NuGet. The SDK therefore needs
  a dedicated `IsBindingProject=true` project. Its C# can be ordinary `LibraryImport` code; an
  `ApiDefinition` is unnecessary for a C ABI.
* **`NativeReference` metadata:**
  * `Kind` = `Framework` | `Static` | `Dynamic`;
  * `ForceLoad` (`-force_load`; needed for a static lib reached only by P/Invoke/`dlsym`);
  * `Frameworks` / `WeakFrameworks` (system frameworks to link);
  * `LinkerFlags`, `SmartLink`, `IsCxx`;
  * `LinkWithSwiftSystemLibraries`, now moot because it is forced on.
  
  Raw extra linker arguments use `LinkerArgument` items. Reference the `.xcframework`, never the
  `.framework` inside it.
* **The Swift runtime is not a problem.** It is in the OS since iOS 12.2, and the SDK's floor is
  iOS 15.0, which also covers Swift Concurrency without back-deploy libraries. The remaining
  compatibility static libs (`libswiftCompatibility*`) are found through the `-L` paths .NET now
  always adds.
* **Static vs dynamic, for this SDK specifically.** eQuantic deliberately does not dictate the
  developer's Xcode (`ValidateXcodeVersion=false` in `Sdk.targets`). A **static** archive built by
  Xcode N can fail or warn when linked by an older Xcode's `ld`. A **dynamic framework** is linked
  once, at our build, and only needs dyld at run time. → Ship **dynamic** for precompiled shims;
  swift-dotnet-bindings made the same choice. Static is fine for *per-app* Swift, because the
  developer's own Xcode compiles it.
* **Weak linking is mandatory.** FoundationModels, Translation's direct initializer and
  SpeechAnalyzer exist only on iOS 26 or 26.4+; the app floor is 15.0. The shim must be linked
  with `-weak_framework X`, or use `@_weakLinked import`, and every entry point must be guarded
  with `if #available`. Each entry exports an `eq_*_is_available()` the C# capability reads. The
  `TranslationSession(installedSource:target:)` initializer, which needs no SwiftUI, is
  **iOS 26.4+**
  ([Apple](https://developer.apple.com/documentation/translation/translationsession/init(installedsource:target:preferredstrategy:).md)).
  Before that, a session is reachable only through the SwiftUI `.translationTask` modifier.
* **App Store hygiene for a shipped framework.** It must be signed; the .NET build re-signs
  embedded frameworks. It must carry a `PrivacyInfo.xcprivacy` if it touches required-reason APIs
  (UserDefaults, file timestamps…).

## 6. App extensions, Live Activities, App Intents

* **Can extensions be C#?** .NET for iOS can build app-extension projects (`IsAppExtension`; see
  `IsAppExtension` in the build-properties page) for share, notification service/content and action
  extensions. Each embeds a whole .NET runtime in a process with a small memory ceiling (tens of
  MB). Photon is unavailable there, and each needs its own project, which conflicts with
  "`<Project Sdk=…>` and nothing else".
* **WidgetKit, Live Activity / Dynamic Island UI and Control widgets cannot be C#.** Their UI is a
  SwiftUI view tree archived and rendered by the system. Microsoft's own Dec 2025 sample writes the
  widget in **Swift in Xcode**, builds the `.appex` with `xcodebuild`, embeds it with
  **`AdditionalAppExtensions`** (`Include` = build dir, `Name`, `BuildOutput` per
  device/simulator, `CodesignEntitlements`), shares data through an **App Group** `UserDefaults`,
  and reloads timelines from C# via a third-party NuGet proxy
  ([devblog](https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/)).
  Flutter's `home_widget` makes the same admission: "does not allow writing Widgets with Flutter
  itself" ([skills.sh](https://www.skills.sh/flutter/skills/flutter-home-screen-widget)).
* **Live Activities.** `ActivityAttributes` is a Swift `Codable` type that must be *the same type*
  in the app (to `request` the activity) and in the widget extension (to render it). C# cannot
  define it. The only general solution is a **fixed SDK-defined attributes type** whose
  `ContentState` carries a payload (a kind plus key/values or small JSON), as swift-dotnet-bindings
  concluded independently.
* **App Intents metadata outside Xcode: yes, and it has been done.** Bazel's `rules_apple`
  produces `Metadata.appintents` with no Xcode project in four steps
  ([app_intents.bzl](https://github.com/bazelbuild/rules_apple/blob/master/apple/internal/resource_actions/app_intents.bzl),
  [issue #1766](https://github.com/bazelbuild/rules_apple/issues/1766)):
  1. Compile the intent sources with `swiftc … -emit-const-values`, producing `.swiftconstvalues`.
  2. Run `/usr/bin/xcrun appintentsmetadataprocessor --compile-time-extraction --module-name M
     --output DIR --sdk-root … --platform-family iOS --deployment-target 15.0 --target-triple …
     --xcode-version <build> --source-file-list … --swift-const-vals-list … --binary-file <any>`.
     `--binary-file` is required but ignored, Apple bug FB347041279.
  3. Sort the JSON for determinism.
  4. Optionally, run a separate NL-training step for App Shortcuts flexible matching
     (`nlu.appintents`).
  
  Constraints: one module name, Swift **source** (C# attributes are invisible to it), and an
  undocumented CLI whose flags moved with Xcode 15.3. ⇒ For eQuantic, App Intents are reachable
  **only by generating Swift intent structs per app** from C# declarations. Their `perform()` calls
  back into C# through a function-pointer registry. They are compiled and processed by the
  developer's own Xcode inside an MSBuild target. Feasible, but the highest-maintenance item in this
  report.

## 7. Android: how .NET binds Java, and where Kotlin breaks it

* **Items.** `AndroidLibrary` (`.jar` → `EmbeddedJar`/`AndroidJavaLibrary`, `.aar` →
  `LibraryProjectZip`/`AndroidAarLibrary`; `Bind="false"` embeds without generating C#; `Pack`).
  `AndroidMavenLibrary` (.NET 9; `Repository` = Central | Google | URL) downloads the artifact and
  its POMs and becomes an `AndroidLibrary`. **Java dependency verification** (.NET 9) reads POMs and
  fails with *"Java dependency 'x:y' version 'v' is not satisfied"* (XA4241/XA4242), naming the
  Microsoft package that would satisfy it. It is satisfied by a `PackageReference` (the package
  advertises `artifact=g:a:v` in its tags, or you set `JavaArtifact=`), a `ProjectReference`, or an
  `AndroidLibrary … JavaArtifact=`, and silenced with `AndroidIgnoredJavaDependency`. It
  **verifies, it does not resolve transitively**
  ([verification](https://learn.microsoft.com/en-us/dotnet/android/binding-libs/advanced-concepts/java-dependency-verification),
  [resolving](https://github.com/dotnet/docs-mobile/blob/main/docs/android/binding-libs/advanced-concepts/resolving-java-dependencies.md)).
  Java libraries must sit **beside** the binding dll (no embedding since .NET 6)
  ([distributing](https://github.com/dotnet/docs-mobile/blob/main/docs/android/binding-libs/advanced-concepts/distributing.md)).
* **What Kotlin does to the generator.** The generator reads Kotlin metadata, which needs protobuf.
  Its problems come from bytecode shapes:
  * `internal` compiles to *public, module-mangled* names;
  * unsigned types and value classes produce *mangled* method names. These can be bound only as
    non-virtual, and an interface fails with XA4213
    ([dotnet/android commit](https://github.com/dotnet/android/commit/aa5e597eba)).
  * `suspend fun` becomes `Object f(…, Continuation<T>)`. It is callable, but C# must implement
    `Continuation` and handle `COROUTINE_SUSPENDED`
    ([why](https://www.baeldung.com/kotlin/suspend-functions-from-java));
  * `Flow` needs a `FlowCollector` that is itself a `suspend` callback;
  * default parameters exist only as `$default` synthetics, or overloads if `@JvmOverloads`;
  * `inline`/`reified` functions do not exist at run time;
  * `@Composable` functions take a compiler-inserted `Composer`, which makes them *uncallable from
    anything but the Compose compiler*.
  
  The working fix everywhere is a **Kotlin-side adapter**: `@JvmStatic`/`@JvmOverloads`, a
  callback interface, or a future.

## 8. Slim AAR for Android: who needs Gradle, and how dependencies travel

* `AndroidGradleProject` builds on whatever machine builds *that project*. It needs JDK 17, the
  Android SDK, a `gradlew` that downloads Gradle and AGP/Kotlin plugins, and network. Building it
  in the SDK's CI means the app developer never runs Gradle: the NuGet holds `lib/net10.0-android36.0/X.dll`
  plus `X.aar` beside it.
* **Maven dependencies: declare them as NuGet dependencies, never bundle jars.** Bundling
  `androidx.core` or `kotlin-stdlib` inside our package collides with any other package that brings
  them (D8/R8 "type defined multiple times"). Depending on the dotnet/android-libraries packages
  puts them in the NuGet graph and deduplicates them. `AndroidMavenLibrary` in a consumer build
  would mean network at build time and no transitive resolution, so keep it out of the consumer
  path.
* The AAR must carry **consumer ProGuard/R8 keep rules** for every class C# reaches by JNI name.
  R8 sees only Java-side references.

## 9. Direct JNI, and per-app Java in the same build

* **Small surfaces without generated bindings.** `Java.Interop.JniPeerMembers` with
  `JniInstanceMethods.InvokeVirtualObjectMethod("name.(sig)V", self, JniArgumentValue*)`, and the
  static and nonvirtual variants
  ([apisof](https://apisof.net/catalog/55943e04f1c330ece769b4d36bf5d75a?fx=net6.0)), plus the older
  `Android.Runtime.JNIEnv` helpers. The shell already does a raw JNI handoff
  (`ANativeWindow_fromSurface(JNIEnv.Handle, surface.Handle)` in `AndroidWindow.cs`). For our own
  AAR, letting the stock binding generator bind a Java-shaped shim API is simpler and type-checked.
* **Every Android build already compiles Java.** The Java Callable Wrappers for every
  `Java.Lang.Object` subclass are generated and compiled by `javac` (`_CompileJava`), and *"all
  `**/*.java` files … automatically have a Build action of `AndroidJavaSource`"* since .NET 7;
  `Bind="false"` skips binding them ([build items](https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-items),
  [JAVAC0000](https://learn.microsoft.com/da-dk/dotnet/android/messages/javac0000)).
  In `Xamarin.Android.Javac.targets`, `_CompileJava` compiles
  `@(AndroidJavaSource)` items with `Bind != True` *together with the JCW stubs*, with every
  referenced jar/aar on the classpath
  ([source](https://github.com/dotnet/android/blob/main/src/Xamarin.Android.Build.Tasks/MSBuild/Xamarin/Android/Xamarin.Android.Javac.targets)).
  ⇒ **A target `BeforeTargets="_CollectJavaSource"` that writes `obj/eQuantic/java/*.java` and adds
  them as `AndroidJavaSource Bind="false"` is a supported per-app path with zero new toolchain.**
  **Most per-app needs do not even need Java.** A C# `AppWidgetProvider`, `Service`,
  `BroadcastReceiver` or `TileService` subclass with `[BroadcastReceiver]`/`[Service]` attributes
  gets its JCW and its manifest entry from the build. eQuantic's generator already writes
  `MainActivity : PhotonActivity` and `[assembly: UsesPermission]` this way
  (`PhotonProgramGenerator.cs`). Per-app Kotlin, by contrast, needs kotlinc (and KSP or Compose
  plugins) that the developer does not have.

## 10. Kotlin coroutines and Flow → C# `Task` / `IAsyncEnumerable`

Ordered by preference:

1. **Use the vendor's Java-friendly variant when it exists.** ML Kit GenAI Prompt ships
   `GenerativeModelFutures` returning `ListenableFuture`, `DownloadCallback` and a streaming chunk
   callback ([Google](https://developers.google.com/ml-kit/genai/prompt/android/get-started)).
   `androidx.credentials` has `…Async(…, executor, callback)` and CameraX returns `ListenableFuture`.
   C# awaits a `ListenableFuture` with `AddListener(Runnable, Executor)`, using the
   `Xamarin.Google.Guava.ListenableFuture` binding.
2. **Shim to `CompletableFuture`.** It is API 24+, under our floor of 26, and bound in
   `Mono.Android`. Use `scope.future { … }` from `kotlinx.coroutines.future`, in **core since 1.7**,
   when the jdk8 module was merged
   ([changelog](https://android.googlesource.com/platform/external/kotlinx.coroutines/+/HEAD/CHANGES_UP_TO_1.7.md)),
   and await it via `WhenComplete(BiConsumer)`. Or use `kotlinx-coroutines-guava`'s `future {}`
   for a `ListenableFuture`.
3. **A callback interface plus a cancel handle.** This is the most explicit option and the one I
   recommend for our own shims:
   ```kotlin
   fun interface EqResult { fun done(value: String?, code: Int, message: String?) }
   class EqHandle internal constructor(private val job: Job) { fun cancel() = job.cancel() }
   object EqGenAi { @JvmStatic fun generate(prompt: String, cb: EqResult): EqHandle =
       EqHandle(scope.launch { runCatching { model.generateContent(prompt).text }
           .fold({ cb.done(it, 0, null) }, { cb.done(null, codeOf(it), it.message) }) }) }
   ```
4. **Flow → `IAsyncEnumerable`.** In Kotlin: `scope.launch { flow.buffer(N).collect { cb.next(it) };
   cb.complete() }`, plus `cancel()`. In C#: a bounded `Channel<T>` whose reader is the
   `IAsyncEnumerable`, with disposal calling `cancel()`. Backpressure is approximated by Kotlin-side
   `buffer` or `conflate`; a pull-per-item design costs a JNI round trip per element.

Threading: callbacks arrive on dispatcher threads, which the .NET runtime attaches automatically.
Hop to the Photon host's dispatcher before touching UI state.

## 11. ML Kit GenAI / Gemini Nano / Play services: bindings that exist today

From [dotnet/android-libraries `config.json`](https://github.com/dotnet/android-libraries) (717
artifacts, read 2026-10-11):

* **Present:**
  * `Xamarin.Google.MLKit.GenAI.Prompt` 1.0.0.4-**alpha1**, which lags Google's
    `genai-prompt:1.0.0-beta4`, and `Xamarin.Google.MLKit.GenAI.Common`;
  * `Xamarin.Google.AI.Edge.AICore` (exp) and `Xamarin.Google.MediaPipe.Tasks.GenAI`;
  * `Xamarin.AndroidX.Credentials` (+PlayServicesAuth) 1.6.0, `Xamarin.AndroidX.Camera.*` 1.6.2 and
    `Xamarin.AndroidX.Health.Connect.ConnectClient` 1.1.0;
  * `Xamarin.KotlinX.Coroutines.{Core,Android,Guava,Jdk8,Play.Services}` 1.11.0, and
    `Xamarin.Google.Guava.ListenableFuture`;
  * the classic ML Kit vision and translation packages;
  * `Xamarin.AndroidX.Compose.Runtime`, which is runtime-only and useless without the Compose
    compiler.
* **Absent:** GenAI **Summarization/Proofreading/Rewriting/Image description**, **Glance**,
  **AppFunctions**.
* `Xamarin.Google.MLKit.GenAI.Prompt` pulls 14 packages
  ([nuget](https://www.nuget.org/packages/Xamarin.Google.MLKit.GenAI.Prompt)):
  * Play Services Basement and Tasks, ML Kit Common, Firebase encoders and DataTransport;
  * Kotlin StdLib, KotlinX Coroutines Core, Guava and Reactive;
  * `Xamarin.Build.Download`.
  
  None of them is MAUI.
* **A slim GenAI wrapper needs:**
  * a Kotlin module on `genai-prompt` (and `genai-summarization` if wanted), minSdk 26;
  * `checkStatus`, `download(progress)`, `generate` returning a cancellable handle, and
    `stream(onChunk)`;
  * the device and quota facts mapped to a typed C# availability enum: AICore-backed Gemini Nano on
    supported devices only, not on unlocked bootloaders, input under 4000 tokens, and a per-app
    inference quota.
  
  Its NuGet dependencies are exactly the list above. For Prompt alone, the Java `Futures` API
  through the existing binding may make the Kotlin shim unnecessary.

## 12. NativeAOT/trimming, and how eQuantic's shells call native code today

**Runtime landscape:**
* .NET 10 mobile runs on Mono: full-AOT on iOS devices, JIT on the simulator.
* **.NET 11 (GA Nov 2026) makes CoreCLR the default and only runtime for Android/iOS/Mac Catalyst**
  ([devblog](https://devblogs.microsoft.com/dotnet/coreclr-progress-and-mono-timeline-dotnet-maui/),
  [XA1040](https://learn.microsoft.com/en-us/dotnet/android/messages/xa1040)).
* NativeAOT is supported on iOS. On Android it is **experimental** (warning XA1040) in .NET 10.
* Trimming on iOS cannot be disabled (`TrimMode=copy` is the off switch), and NativeAOT forces
  `full`.

Bridge implications:
* C-ABI `LibraryImport` and `[UnmanagedCallersOnly]` callbacks are safe on all of these runtimes.
* Reverse callbacks must be *registered at compile time* under Mono full-AOT. The documented rule is
  "flag callback methods with MonoPInvokeCallback… have to be static… no support for instance
  methods"
  ([Xamarin.iOS limitations](https://learn.microsoft.com/en-us/previous-versions/xamarin/ios/internals/limitations)).
* Android R8 needs keep rules for JNI-only classes.
* CallConvSwift has Mono-JIT/simulator caveats (swift-dotnet-bindings).

**How eQuantic calls native code today (read from the repo):**

| Piece | Target | How it calls native |
|---|---|---|
| `Native.Shell.Apple` (shared by macOS **and iOS**) | plain `net10.0` | Raw `LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint="objc_msgSend")` overloads (`ObjC.cs`), frameworks loaded by absolute path (e.g. LocalAuthentication), **hand-built global ObjC blocks** (`ObjCBlock.cs`), and **runtime-defined ObjC classes** (`objc_allocateClassPair`/`class_addMethod`) for delegates (AppleLocation, AppleCamera, AppleDeepLinks). |
| `Native.Shell.iOS` | `net10.0-ios` | `Microsoft.iOS` bindings (UIKit, Metal, CoreAnimation, PhotosUI/PHPicker; `[Register]` PhotonAppDelegate/PhotonViewController/PhotonView; `[Export]` accessibility), plus the shared Apple services above (`IosCapabilities` registers AppleNetworkStatus, AppleBiometry, AppleMotionSensor, AppleLocation, AppleCamera, AppleAppStorage, AppleSecretStore). |
| `Native.Shell.Android` | `net10.0-android` | `Mono.Android` bindings only, with **no AndroidX or other PackageReference** (framework `BiometricPrompt` chosen "rather than the AndroidX one" to avoid dependencies), plus one `LibraryImport("android")` for `ANativeWindow_fromSurface`. |
| `Sdk.Native` | MSBuild SDK | Adds the shell by TFM (`PackageReference`, or `ProjectReference` in dev mode); roots shells for the trimmer; merges `PhotonApp.plist`; `EQuanticCapabilityManifest` (AfterTargets=Compile) runs `eqicon capabilities` over the compiled assembly into a `PartialAppManifest`; Android permissions and `MainActivity` come from `PhotonProgramGenerator`; `ValidateXcodeVersion=false`. |

**⚠ Risk found in today's code.** Every Apple callback goes through
`Marshal.GetFunctionPointerForDelegate`, for both blocks and `class_addMethod`. That covers
`ObjCBlock` (whose `ReplyBlock` lambda *captures* state in `AppleBiometry`) and the static-lambda
IMPs in AppleLocation, AppleCamera and AppleDeepLinks. A C# `static` lambda still compiles to an
*instance* method on a cached closure class. Under Mono full-AOT, which is what an iOS **device**
Release build runs on .NET 10, this is the documented unsupported shape. The simulator uses the JIT
and will not show it. No test project references the shells. **Measure on a device in Release**.
The robust form is `[UnmanagedCallersOnly]` static entry points plus a `GCHandle` (or a static
instance registry) passed as context. The same form is what every new bridge callback should use.

**Where the new pieces plug in:**
* **Precompiled Apple shim.** Add a new `eQuantic.UI.Native.Bridge.Apple` project
  (`net10.0-ios`, later `-maccatalyst`; `IsBindingProject=true`; `NativeReference
  EqBridge.xcframework Kind="Framework"`, weak frameworks; C# = `LibraryImport` + callbacks).
  `Shell.iOS` references it, so it flows to apps transitively through the `PackageReference` that
  `Sdk.props` already adds. The xcframework is built by `scripts/build-apple-bridge.sh`, pinned like
  `slang-toolchain.sh` and run on a macOS CI job before pack. Its normative source is
  `src/eQuantic.UI.Native.Bridge.Apple/Swift/*.swift`, mirroring the shader rule (one writer,
  outputs never hand-edited). The macOS desktop shell is plain `net10.0` and has no
  `NativeReference`. It would load the macOS slice as a dylib via `NativeLibrary`, copied into
  `Contents/Frameworks` by the existing `eqicon bundle` step.
* **Precompiled Android shim.** Add `eQuantic.UI.Native.Bridge.Android` (`net10.0-android`,
  `AndroidLibrary eqbridge.aar`, `Bind` true for a Java-shaped API or false for raw JNI, plus
  `PackageReference` to `Xamarin.Kotlin.StdLib`/`Xamarin.KotlinX.Coroutines.*`). The aar is built
  by a pinned Gradle (wrapper `distributionSha256Sum`) in CI. Dev mode may use
  `AndroidGradleProject`, which is a dotnet/android item, not MAUI.
* **Heavy capability packs** (ML Kit GenAI with its 14 packages, Health Connect, Play Billing…)
  must not ride into every APK. `PackageReference`s are decided at restore, before any source
  generator runs, so a builder call cannot add one. Follow the existing **icon-pack precedent**: an
  opt-in `PackageReference` (`eQuantic.UI.Native.Intelligence`), plus an analyzer error when
  `builder.Intelligence.Use…()` is called without it. A
  build-time MSBuild property (`EQuanticOnDeviceAI`) is the alternative. Apple shims cost no
  third-party dependency (system frameworks only), so one xcframework can hold them all.
* **Per-app Swift (iOS).** A new `EQuanticAppleShims` target `AfterTargets="Compile"`, the same
  pattern as `EQuanticCapabilityManifest`, works as follows:
  1. `eqicon` reads the generator-written assembly attributes and emits Swift into
     `obj/eQuantic/swift/`;
  2. `xcrun -sdk iphoneos|iphonesimulator swiftc -parse-as-library -emit-library -static
     -emit-const-values …` compiles it, picking the RID's triple;
  3. the result is added as `NativeReference Kind=Static ForceLoad=true`;
  4. `appintentsmetadataprocessor` writes `Metadata.appintents`, added as `BundleResource` with a
     `LogicalName` at the bundle root.
  
  The exact internal hook before native-reference expansion must be verified against dotnet/macios.
  `CollectAppManifestsDependsOn` is the documented hook for manifests only.
* **Per-app extensions (iOS).** A **generic precompiled** `EqWidgets.appex` (Swift/SwiftUI) ships
  zipped in the bridge NuGet. It renders timeline entries from App Group storage: images the app
  pre-renders with Photon, or a constrained subset of the vocabulary as JSON. Per app, the SDK:
  * rewrites its `Info.plist` (`CFBundleIdentifier=$(ApplicationId).widgets`, versions);
  * writes its entitlements (`group.$(ApplicationId)`);
  * adds it as `AdditionalAppExtensions`, plus `CustomEntitlements` for the host app.
  
  The .NET build signs it with the app's identity.
* **Per-app Android:**
  * C# first: the generator writes receivers and services, and the build writes JCWs and the
    manifest;
  * resource XML (`res/xml/appwidget_info.xml`) comes from `eqicon`, using the literal-path
    `AndroidResource` trick already used for launcher icons;
  * `AndroidManifestOverlay` covers what attributes cannot say (`<property>`, `<queries>`);
  * `AndroidJavaSource Bind="false"` is the escape hatch.

## 13. Prior art: what to copy, what to avoid

* **Flutter.**
  * Platform channels are async message passing with a codec. **Pigeon** generates both ends
    (Swift/ObjC, Kotlin/Java, C++, GObject) from a Dart schema
    ([pigeon](https://pub.dev/packages/pigeon)).
  * Dart FFI with **ffigen** reaches Swift *only through an `@objc` header* that `swiftc` emits
    ([ffigen swift example](https://dart.googlesource.com/native/+show/HEAD/pkgs/ffigen/example/swift/README.md)).
  * **jnigen** binds Java/Kotlin over JNI and turns `suspend fun` into Dart `Future` with a custom
    `Continuation` (`suspend_fun_to_async`)
    ([jnigen example](https://dart.googlesource.com/native.git/+show/HEAD/pkgs/jnigen/example/kotlin_plugin/README.md)).
  * Plugins ship native source compiled by the app developer's Xcode and Gradle. Home-screen
    widgets stay native.
  
  *Copy:* one schema generating both native ends; jnigen's Continuation trick, if we ever bind
  `suspend` directly. *Avoid:* source plugins that make the developer's machine compile native
  code, and the serialization channel on hot paths, where FFI is the better primitive.
* **React Native / Expo.** TurboModules (codegen from typed JS specs, JSI calls) and the
  **Expo Modules API**, a Swift/Kotlin DSL: `Name`, `Function`, `AsyncFunction` (Promise in Swift,
  `Coroutine` suspend in Kotlin), `Events`, `View`
  ([module API](https://docs.expo.dev/modules/module-api/)). **Config plugins** edit `Info.plist`
  and the manifest during `expo prebuild`: "Continuous Native Generation", where nobody edits
  `ios/` or `android/` by hand ([config plugins](https://docs.expo.dev/config-plugins/introduction/)).
  *Copy:* CNG is exactly eQuantic's stance (the SDK owns the native project files). One DSL whose
  async maps to each platform's idiom. *Avoid:* compiling native modules on the developer's machine
  through CocoaPods/Gradle at prebuild, the main source of Expo build pain.
* **Kotlin Multiplatform.** `expect`/`actual` declares a common API with per-platform
  implementations, checked by the compiler. Objective-C export turns `suspend` into completion
  handlers. **Swift export** reached **Alpha in Kotlin 2.4 (June 2026)**, with `suspend` → Swift
  `async` and `Flow` → `AsyncSequence`
  ([summary](https://www.youngju.dev/transcribe/2026-07-16-kotlin-swift-export-alpha.en)).
  *Copy:* `expect/actual` is what eQuantic's vocabulary interfaces plus per-shell
  `IPhotonCapabilities` registrations already are; keep the contract compile-time checked. *Avoid:*
  bidirectional language export as the bridge; a narrow C ABI is the stable seam.
* **.NET MAUI Essentials** (studied, not used). One C# API per capability, with per-platform
  partial implementations over the Xamarin/.NET bindings. It never crosses into Swift or Kotlin, so
  Swift-only features are simply absent. Permissions require the developer to edit
  `Info.plist`/the manifest by hand. *Copy:* capability interfaces, explicit `IsSupported`, typed
  permission status. *Avoid:* everything that makes the developer touch platform files.
  eQuantic's generator-plus-`eqicon` route is already ahead here.

## 14. MAUI audit (owner constraint)

| Recommended item | Comes from | MAUI? |
|---|---|---|
| `NativeReference`, binding resource package, `IsBindingProject`, `XcodeProject`, `AdditionalAppExtensions`, `CustomEntitlements`, `PartialAppManifest`, `BundleResource`, `LinkerArgument` | dotnet/macios (`Microsoft.iOS` workload) | No |
| `AndroidLibrary`, `AndroidMavenLibrary`, `AndroidGradleProject`, `AndroidJavaSource`, `AndroidManifestOverlay`, Java dependency verification | dotnet/android (`Mono.Android` workload) | No |
| `Xamarin.AndroidX.*`, `Xamarin.Google.*`, `Xamarin.GooglePlayServices.*`, `Xamarin.Kotlin*`, `Xamarin.KotlinX.*` | dotnet/android-libraries: Microsoft, .NET Foundation, "Third Party Library Bindings for .NET for Android". They depend only on the Android workload, on each other, and on `Xamarin.Build.Download` (a netstandard2.0 MSBuild download helper with no dependencies) | **No.** "maui" is only a repo topic tag |
| `System.Runtime.InteropServices.Swift`, `LibraryImport`, `UnmanagedCallersOnly`, `Java.Interop` | dotnet/runtime, dotnet/java-interop | No |
| CommunityToolkit `Maui.NativeLibraryInterop`, MAUI Essentials | MAUI ecosystem | **Prior art only. Never referenced.** |
| `SwiftBindings.Apple.*` (swift-dotnet-bindings) | Single community maintainer | Not MAUI, but **not recommended as a dependency** (bus factor, Mono caveats). Usable as reference |

## 15. Hard limits (C# cannot do these even with bridges)

1. **UI rendered by the system in another process:**
   * WidgetKit widgets, Live Activities and the Dynamic Island, Control Center and Lock Screen
     controls, Siri snippets, watch complications: SwiftUI only;
   * Android Glance: Compose → RemoteViews, so Kotlin plus the Compose compiler;
   * classic RemoteViews widgets: the *provider* can be C#, but the views are a fixed set and
     bitmaps are size-capped by IPC.
   
   Photon cannot draw live in any of them. The options are pre-rendered images, or an SDK-generated
   view from a constrained vocabulary subset.
2. **Compile-time-metadata APIs:**
   * App Intents (Swift const extraction);
   * `@Generable`/`@Guide` (macros; a runtime `DynamicGenerationSchema` substitute exists);
   * SwiftData `@Model` and Observation (macros);
   * Android **AppFunctions** (KSP generates the service and the XML schema; alpha library);
   * any `@Composable`.
   
   iOS: reachable by generating Swift per app. Android KSP/Compose: **not reachable** without
   shipping a Kotlin toolchain, or re-implementing the processor's output ourselves (fragile).
3. **Type identity shared between app and extension.** `ActivityAttributes`, widget configuration
   intents and `AppEntity` types must be *Swift* types. Only fixed SDK-defined generic types work.
4. **C#-authored code inside extension processes** (widget timeline providers, intents
   extensions): the memory ceilings and the absence of Photon make it impractical even where
   .NET technically supports the extension type.
5. **Unprojectable language features at a call site:**
   * Swift `async` ABI, generics with associated types, opaque `some` types, property wrappers;
   * Kotlin `inline`/`reified`, value-class mangling, `suspend`, `Flow`.
   
   They are reachable only through a hand-written shim that flattens them.
6. **Provisioning is not code.** App Groups, extension bundle IDs, Family Controls, HealthKit,
   CarPlay and similar entitlements must exist in the developer's Apple account and provisioning
   profiles. The SDK can write every file but cannot grant capabilities. Some require an Apple
   request form.

## 16. Risks

* **Two new languages and two native CI legs** (macOS/Xcode, JDK/Gradle/AGP/Kotlin), each needing
  pinning in the style of `slang-toolchain.sh`. Linux CI cannot build the Apple shim; the shader
  precedent of committing the binary output shows one way.
* **Runtime churn under the bridge.** Mono AOT (.NET 10) → CoreCLR (.NET 11) on both platforms, and
  NativeAOT experimental on Android. Keeping to the C ABI plus `UnmanagedCallersOnly` makes the
  bridge runtime-agnostic. The current `GetFunctionPointerForDelegate` callbacks in `Shell.Apple`
  are a live device risk (§12).
* **Toolchain skew:**
  * static Swift archives versus the developer's Xcode, which the SDK deliberately does not pin.
    Use dynamic frameworks for precompiled code;
  * Kotlin stdlib and coroutines versions versus whatever other Xamarin.* packages an app pulls;
  * the dotnet/android-libraries cadence (GenAI Prompt still at alpha1 against Google's beta4).
* **Undocumented Apple tools** (`appintentsmetadataprocessor`) change their flags with Xcode.
  `rules_apple` carries a workaround for Apple bug FB347041279.
* **Duplicate classes and R8.** Bundling Maven jars instead of depending on the NuGet packages
  causes duplicate classes. Missing consumer keep rules let R8 strip the shim.
* **App Store and Play policy.** A privacy manifest and signing for the shipped xcframework;
  `QUERY_ALL_PACKAGES`-style Play policies if a bridge adds manifest entries implicitly. The SDK
  must log what it adds, as it already does for entitlements.
* **Windows-paired iOS builds.** Custom `Exec xcrun …` targets run where MSBuild runs, so per-app
  Swift generation limits iOS builds to macOS hosts.
* **Scope creep.** "Every platform capability" is unbounded. Each capability should be a typed C#
  interface first (`IOnDeviceModel`, `ILiveActivities`, `IPurchases`), with the shim as an
  implementation detail, so a future .NET Swift projection or a vendor's own .NET SDK can replace a
  shim without a public break.
