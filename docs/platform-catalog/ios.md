# iOS / iPadOS developer capability catalog (iOS 26 and iOS 27), with reachability from C#

Compiled 2026-10-11 for eQuantic.UI's native track. 355 rows. iOS 27 shipped on 14 September 2026; iOS 27.1 is in beta. Its foldable-iPhone APIs are marked "27.1, beta".

The catalog keeps Apple's own names, "widget" included, where it names what Apple ships or cites it: such a name is Apple's product, never an eQuantic.UI component.

## Sources

Apple:
- Technologies index, read as the DocC JSON behind https://developer.apple.com/documentation/technologies (`https://developer.apple.com/tutorials/data/documentation/technologies.json`). 399 framework pages and about 200 symbol pages were fetched from the same `/tutorials/data/documentation/<path>.json` endpoint. Their `metadata.platforms` gives the minimum iOS version, and their `variants`/`variantOverrides` show whether an Objective-C (`occ`) surface really exists.
- What's new in iOS 27: https://developer.apple.com/ios/whats-new/
- WWDC26 iOS guide: https://developer.apple.com/wwdc26/guides/ios/
- Documentation updates per framework (June 2025 = iOS 26, June/September 2026 = iOS 27/27.1): https://developer.apple.com/documentation/updates and https://developer.apple.com/documentation/updates/wwdc2025
- AI/ML technology overviews: https://developer.apple.com/documentation/technologyoverviews/ai-machine-learning, …/generative-models, …/apple-intelligence, …/built-in-intelligence
- Newsroom, WWDC26: https://www.apple.com/newsroom/2026/06/apple-aids-app-development-with-new-intelligence-frameworks-and-advanced-tools/
- Newsroom, WWDC25: https://www.apple.com/newsroom/2025/06/apple-supercharges-its-tools-and-technologies-for-developers/
- WWDC26 session 256, "Discover generated subtitles and subtitle styles": https://developer.apple.com/videos/play/wwdc2026/256/

Microsoft / .NET:
- dotnet/macios `main`: `tools/common/Frameworks.cs` (iOS framework table with versions) https://github.com/dotnet/macios/blob/main/tools/common/Frameworks.cs, `src/frameworks.sources` (`IOS_FRAMEWORKS`) https://github.com/dotnet/macios/blob/main/src/frameworks.sources, and `src/<framework>.cs` binding definitions, grepped symbol by symbol for partial coverage.
- dotnet/macios releases (".NET 10 – Xcode 27.0 support 27.0.10722", 29 Sep 2026): https://github.com/dotnet/macios/releases
- Xamarin.iOS extensions doc (ObjC principal-class extensions written in C#): https://learn.microsoft.com/en-us/previous-versions/xamarin/ios/platform/extensions
- .NET runtime's own Swift shim over CryptoKit: https://github.com/dotnet/runtime/blob/main/src/native/libs/System.Security.Cryptography.Native.Apple/pal_swiftbindings.swift
- Third-party Swift-ABI bindings, not Microsoft and not Apple: https://www.nuget.org/packages/SwiftBindings.Apple

Secondary sources, used only for dates:
- https://9to5mac.com/2026/09/09/apple-confirms-ios-27-release-date-september-14/

## Legend

- **Min iOS**: the framework's or symbol's `introducedAt` for iOS in Apple's documentation JSON. "NEW in iOS 26" or "NEW in iOS 27" marks capabilities introduced in that release, including its point releases.
- **AI**: "yes" means it runs Apple Intelligence or on-device ML. That includes Apple's built-in models, like Vision and Speech, and models you bring yourself.
- **Reachability from outside Swift**:
  - `objc`: an Objective-C or C API exists and can be called through the ObjC runtime or P/Invoke.
  - `swift-only`: there is no ObjC surface. This was verified in Apple's JSON: the page has no `occ` variant, or its `occ` variant contains only `…VersionNumber`/`…VersionString` or articles.
  - `extension (objc)`: needs a separate app-extension target whose principal class is an ObjC-compatible class. It can be written in C# as a .NET appex project.
  - `extension (swift)`: the extension's entry point is Swift or SwiftUI (`@main` WidgetBundle, AppIntentsExtension, ExtensionKit `AppExtension`, …). It cannot be authored in C# today.
  - `build-time`: needs compile-time processing, such as Swift macros, `appintentsmetadataprocessor`, `actool`, Info.plist keys, or entitlements.
  - `entitlement: <name>`: needs an entitlement, Apple approval, or both.
  - `rest`: an equivalent web or REST API exists.
- **.NET for iOS binding**: whether dotnet/macios `main`, which already carries the Xcode 27 SDK, ships a C# binding.
  - `yes`: the framework is in `IOS_FRAMEWORKS` and the named types are present in `src/*.cs`.
  - `partial`: some of the named APIs are bound and some are not.
  - `no`: not bound.
  - `n/a`: nothing to bind.
- Anything not confirmed by a primary source says **unverified** in the cell.

- **eQuantic**: what covers the row on `main` today, or the issue that owes it. ✅ a contract realized on this platform, ⚠️ realized with a limit that matters, 🐛 a bug that blocks it; an issue number alone is work owed under [#774](https://github.com/eQuantic/equantic-ui/issues/774). Empty where nothing is planned yet. Added on 2026-10-11 from `Primitives/Devices` and each shell's `*Capabilities.cs`.

## Caveats found while verifying

- Apple's index tags some frameworks with both `occ` and `swift` even though they have no usable ObjC API: AlarmKit, ProximityReader, RoomPlan, Compute Graph, ContactProvider, Assignables, CoreTransferable, WidgetKit (constants only), and LockedCameraCapture (one constant). These are classified `swift-only` here.
- The reverse also happens. ExtensionKit is tagged `swift`, but `EXHostViewController` has ObjC headers on iOS 26 and macios binds it.
- `LinkSecurity` (iOS 27) is bound by macios but is not in Apple's online technologies index.

| # | Category | Capability | Framework / main API | Min iOS (NEW) | AI | Reachability from outside Swift | .NET for iOS binding | Note | eQuantic |
|---|---|---|---|---|---|---|---|---|---|
| 1 | App shell & UI (UIKit) | App and scene life cycle (launch, foreground/background, multiple windows) | UIKit `UIApplication`, `UIScene`/`UIWindowScene`, `UISceneDelegate` | 2.0 / scenes 13.0 | no | objc | yes | iOS 27: apps built with the iOS 27 SDK MUST use the scene-based life cycle or they fail to launch (UIKit updates, June 2026). | ✅ the scene life cycle ([#796](https://github.com/eQuantic/equantic-ui/issues/796)) |
| 2 | App shell & UI (UIKit) | Liquid Glass materials, glass buttons, scroll-edge effects, background extension | UIKit `UIGlassEffect`, `UIGlassContainerEffect`, `UIScrollEdgeEffect`, `UIBackgroundExtensionView` | NEW in iOS 26 | no | objc | yes | iOS 27 adds a user transparency slider (system setting, nothing to adopt). |  |
| 3 | App shell & UI (UIKit) | iPad menu bar (swipe-down full app menu) | UIKit `UIMainMenuSystem`, `UIMenuBuilder` | NEW in iOS 26 | no | objc | yes | iPadOS 26 windowing + menu bar. |  |
| 4 | App shell & UI (UIKit) | Foldable / hinge awareness (angle, closed/partially open/fully open) | UIKit `UIHingeInteraction`, `UIHinge`; SwiftUI `onHingeChange` | NEW in iOS 27 (27.1, beta) | no | objc | no (not yet bound) | Apple docs name the device "iPhone Duo". Beta API as of Oct 2026. |  |
| 5 | App shell & UI (UIKit) | Avoid hardware-reserved regions (camera cutout, hinge) | UIKit `UIView.ReservedRegion`, `reservedRegions(kind:options:)` | NEW in iOS 27 (27.1, beta) | no | objc | no (not yet bound) | Kinds `.occlusion`, `.division`. |  |
| 6 | App shell & UI (UIKit) | Adaptive primary/secondary arrangements (split, overlay) | UIKit `UIArrangementViewController`, `UISplitArrangement`, `UIOverlayArrangement`; SwiftUI `ArrangementView` | NEW in iOS 27 (27.1, beta) | no | objc | no (not yet bound) | Successor pattern to split view for foldables. |  |
| 7 | App shell & UI (UIKit) | Layouts that adapt for iPhone Mirroring | UIKit (size classes, scene geometry) | NEW in iOS 27 | no | objc | yes (unverified for new members) | Guidance: use size classes / scene geometry, not idiom or `UIScreen.main`. |  |
| 8 | App shell & UI (UIKit) | System share sheet | UIKit `UIActivityViewController`, `UIActivity` | 6.0 | no | objc | yes | Custom activities are ObjC subclasses. | [#780](https://github.com/eQuantic/equantic-ui/issues/780) |
| 9 | App shell & UI (UIKit) | Open/save/import documents (Files) | UIKit `UIDocumentPickerViewController`, `UIDocumentBrowserViewController`, `UIDocument` | 8.0 / 11.0 | no | objc | yes | Info.plist `LSSupportsOpeningDocumentsInPlace`, `UIFileSharingEnabled` (build-time keys). | [#782](https://github.com/eQuantic/equantic-ui/issues/782) |
| 10 | App shell & UI (UIKit) | Clipboard read/write, privacy-preserving paste button | UIKit `UIPasteboard`, `UIPasteControl` | 3.0 / paste control 16.0 | no | objc | yes | Programmatic reads trigger the paste-permission prompt; `UIPasteControl` avoids it. | `ITextClipboard`, not realized here: [#776](https://github.com/eQuantic/equantic-ui/issues/776) |
| 11 | App shell & UI (UIKit) | Haptic feedback (impact, selection, notification, canvas) | UIKit `UIImpactFeedbackGenerator`, `UISelectionFeedbackGenerator`, `UINotificationFeedbackGenerator`, `UICanvasFeedbackGenerator` | 10.0 / canvas 17.5 | no | objc | yes | Custom waveforms: Core Haptics (separate row). | [#345](https://github.com/eQuantic/equantic-ui/issues/345) |
| 12 | App shell & UI (UIKit) | Screen brightness | UIKit `UIScreen.brightness` | 5.0 | no | objc | yes | — | [#783](https://github.com/eQuantic/equantic-ui/issues/783) |
| 13 | App shell & UI (UIKit) | Keep screen awake | UIKit `UIApplication.isIdleTimerDisabled` | 2.0 | no | objc | yes | — | [#783](https://github.com/eQuantic/equantic-ui/issues/783) |
| 14 | App shell & UI (UIKit) | Device info, battery level/state, orientation, proximity sensor | UIKit `UIDevice` | 2.0 / battery 3.0 | no | objc | yes | Battery monitoring must be enabled (`isBatteryMonitoringEnabled`). | [#783](https://github.com/eQuantic/equantic-ui/issues/783) |
| 15 | App shell & UI (UIKit) | Home Screen quick actions | UIKit `UIApplicationShortcutItem` | 9.0 | no | objc | yes | Static ones are Info.plist (build-time). |  |
| 16 | App shell & UI (UIKit) | Alternate app icons | UIKit `setAlternateIconName(_:completionHandler:)` | 10.3 | no | objc; build-time | yes | Icons must be in the asset catalog (build-time). iOS 26 icons come from Icon Composer `.icon` files. |  |
| 17 | App shell & UI (UIKit) | Deep links, custom URL schemes, universal links | UIKit `UIApplication.open`, `NSUserActivity`; Associated Domains | 3.0 / universal links 9.0 | no | objc; entitlement: com.apple.developer.associated-domains | yes | apple-app-site-association file on the web server (rest-side). | ✅ `IDeepLinks`: the app's own schemes. ✅ `IUrlLauncher`: another app's link, `UIApplication.open` and `canOpenURL` with `LSApplicationQueriesSchemes` written from `builder.Workspace.Opens…` ([#778](https://github.com/eQuantic/equantic-ui/issues/778)). Universal links: [#873](https://github.com/eQuantic/equantic-ui/issues/873) |
| 18 | App shell & UI (UIKit) | Drag and drop | UIKit `UIDragInteraction`, `UIDropInteraction`, `NSItemProvider` | 11.0 | no | objc | yes | iOS 27 adds `allowsPointerDragBeforeLiftDelay`. |  |
| 19 | App shell & UI (UIKit) | Context menus and edit menu | UIKit `UIContextMenuInteraction`, `UIEditMenuInteraction` | 13.0 / 16.0 | no | objc | yes | — |  |
| 20 | App shell & UI (UIKit) | Find and replace in text | UIKit `UIFindInteraction` | 16.0 | no | objc | yes | — |  |
| 21 | App shell & UI (UIKit) | Printing / AirPrint | UIKit `UIPrintInteractionController` | 4.2 | no | objc | yes | — |  |
| 22 | App shell & UI (UIKit) | Color and font pickers | UIKit `UIColorPickerViewController`, `UIFontPickerViewController` | 14.0 / 13.0 | no | objc | yes | HDR colors in the color picker: iOS 26. |  |
| 23 | App shell & UI (UIKit) | Full-page screenshots (PDF) | UIKit `UIScreenshotService` | 13.0 | no | objc | yes | — |  |
| 24 | App shell & UI (UIKit) | Hardware keyboard shortcuts | UIKit `UIKeyCommand`, `UIPress` | 7.0 / 13.4 | no | objc | yes | Shown in the iPad menu bar on iOS 26. |  |
| 25 | App shell & UI (UIKit) | Pointer / trackpad interactions | UIKit `UIPointerInteraction`, `UIHoverGestureRecognizer` | 13.4 | no | objc | yes | — |  |
| 26 | App shell & UI (UIKit) | Apple Pencil (double-tap, squeeze, hover, barrel roll) | UIKit `UIPencilInteraction`, `UIPencilHoverPose` | 12.1 / squeeze 17.5 | no | objc | yes | Apple's "Apple Pencil" technology page is Swift-tagged but the UIKit API is ObjC. |  |
| 27 | App shell & UI (UIKit) | Text system (TextKit 2, attributed text, tables in text) | UIKit `UITextView`, `NSTextLayoutManager`, `NSTextTable` (iOS 27) | 7.0 / TextKit 2 15.0 | no | objc | yes (iOS 27 text tables unverified) | iOS 27 brings `NSTextTable`/`NSTextBlock` and viewport rendering surfaces to iOS. |  |
| 28 | App shell & UI (UIKit) | Scan text from camera into a text field | UIKit `UIAction.captureTextFromCamera` | 15.0 | yes | objc | yes | On-device Live Text in a responder. | [#786](https://github.com/eQuantic/equantic-ui/issues/786) |
| 29 | App shell & UI (UIKit) | Writing Tools (proofread/rewrite/summarize) in text views | UIKit `UITextView.writingToolsBehavior`, `UIWritingToolsCoordinator`, `UIBarButtonItem.SystemItem.writingTools` | 18.0 / coordinator 18.2 | yes | objc | yes | Apple Intelligence; standard text views get it for free. | [#801](https://github.com/eQuantic/equantic-ui/issues/801) |
| 30 | App shell & UI (UIKit) | Genmoji in text | UIKit/Foundation `NSAdaptiveImageGlyph`, `supportsAdaptiveImageGlyph` | 18.0 | yes | objc | yes | Persist as attributed-string attachments. |  |
| 31 | App shell & UI (UIKit) | Smart Reply context for messaging/mail apps | UIKit `UIMessageConversationContext`, `UIMailConversationContext` | 18.4 | yes | objc | yes | Apple Intelligence inserts a generated reply. |  |
| 32 | App shell & UI (UIKit) | Dynamic Type, dark mode, traits, size classes | UIKit `UITraitCollection`, `UIFontMetrics` | 8.0 / 13.0 | no | objc | yes | — | ⚠️ `IThemeController`, `WindowSizeClass` |
| 33 | App shell & UI (UIKit) | Orientation lock, status bar, home indicator | UIKit `UIViewController` overrides, `UIWindowScene.requestGeometryUpdate` | 2.0 / 16.0 | no | objc | yes | — |  |
| 34 | App shell & UI (UIKit) | External displays and Stage Manager windows | UIKit `UIWindowScene`, `UIScreen` connection, `requestGeometryUpdate` | 13.0 / Stage Manager 16.0 | no | objc | yes | — |  |
| 35 | App shell & UI (UIKit) | AutoFill hints (passwords, one-time codes from SMS/Mail, addresses) | UIKit `UITextContentType` | 10.0 / one-time code 12.0 | no | objc | yes | — |  |
| 36 | App shell & UI (Foundation) | Low Power Mode and thermal state | Foundation `ProcessInfo.isLowPowerModeEnabled`, `thermalState` | 9.0 / 11.0 | no | objc | yes | Notifications `NSProcessInfoPowerStateDidChange`, thermal state change. | [#783](https://github.com/eQuantic/equantic-ui/issues/783) |
| 37 | App shell & UI (Foundation) | Handoff / user activities (also feeds Siri suggestions) | Foundation `NSUserActivity` | 8.0 | no | objc | yes | iOS 18.2+: `appEntityIdentifier` links an activity to an App Intents entity (Swift side). |  |
| 38 | App shell & UI (Foundation) | Localization | Foundation `Bundle`, String Catalogs (`.xcstrings`) | 2.0 / catalogs 17 | no | build-time | yes | String catalogs are compiled by Xcode; .NET resx is the C# path. | ✅ `.resx` catalogs |
| 39 | App shell & UI (SwiftUI) | Declarative UI framework (needed by widgets, Live Activities, snippets) | SwiftUI | 13.0 | no | swift-only | no | Hosting SwiftUI from C# needs a Swift shim (`UIHostingController` is generic Swift). |  |
| 40 | App shell & UI (SwiftUI) | Assistive Access scene (simplified UI mode) | SwiftUI `AssistiveAccess` scene | NEW in iOS 26 | no | swift-only | no | UIKit apps declare support via Info.plist; full scene API is SwiftUI. |  |
| 41 | App shell & UI (SwiftUI) | Document apps with direct disk access, reorderable containers, swipe actions anywhere | SwiftUI `ReadableDocument`, `reorderable()`, `swipeActionsContainer()` | NEW in iOS 27 | no | swift-only | no | Listed for completeness of the iOS 27 UI story. |  |
| 42 | Build-time platform artifacts | Usage-description strings for every protected resource (camera, mic, location, contacts, photos, health, Bluetooth, tracking, Face ID, motion, NFC...) | Info.plist `NS*UsageDescription` keys | 2.0+ | no | build-time | n/a | Missing key = crash on access. The SDK should derive these from C# declarations. | ✅ written from `builder.Capabilities` |
| 43 | Build-time platform artifacts | Entitlements and provisioning (capabilities) | `.entitlements` plist + provisioning profile | 2.0+ | no | build-time; entitlement | n/a | Every "entitlement" cell in this table lands here. | ⚠️ `builder.Entitlements` |
| 44 | Build-time platform artifacts | Privacy manifest and required-reason APIs | `PrivacyInfo.xcprivacy` | 17.0 (App Store enforcement 2024) | no | build-time | n/a | Required for apps and SDKs that use required-reason APIs (file timestamps, UserDefaults, disk space, boot time...). |  |
| 45 | Build-time platform artifacts | App icon (Liquid Glass layered icons) | Icon Composer `.icon` / asset catalog `AppIcon` | asset catalogs 7.0 / `.icon` NEW in iOS 26 | no | build-time | n/a | Compiled by `actool`. | ✅ `AppIcon` (eqicon) |
| 46 | Build-time platform artifacts | Launch screen | Info.plist `UILaunchScreen` / launch storyboard | 9.0 / plist launch screen 14.0 | no | build-time | n/a | — | ✅ `PhotonApp.plist` |
| 47 | Build-time platform artifacts | Asset catalogs (colors, images, symbols, data) and on-demand resources | Asset catalog (`.xcassets`), `NSBundleResourceRequest` | 7.0 / ODR 9.0 | no | build-time; objc (`NSBundleResourceRequest`) | yes | — |  |
| 48 | Notifications & background | Local and remote (push) notifications, badges, actions | UserNotifications `UNUserNotificationCenter` (`setBadgeCount` 16) | 10.0 | no | objc; entitlement: aps-environment | yes | APNs provider side is rest (HTTP/2). | [#781](https://github.com/eQuantic/equantic-ui/issues/781) (`UseNotifications` already declares it: [#779](https://github.com/eQuantic/equantic-ui/issues/779)) |
| 49 | Notifications & background | Modify/decrypt push payloads before display | UserNotifications `UNNotificationServiceExtension` | 10.0 | no | extension (objc) | yes | Notification Service Extension; writable in C# as a .NET appex. |  |
| 50 | Notifications & background | Custom notification UI | UserNotificationsUI `UNNotificationContentExtension` | 10.0 | no | extension (objc) | yes | Notification Content Extension. |  |
| 51 | Notifications & background | Communication notifications (sender avatar) | Intents `INSendMessageIntent` + `UNNotificationContent.updating(from:)` | 15.0 | no | objc; entitlement: com.apple.developer.usernotifications.communication | yes | — |  |
| 52 | Notifications & background | Time-sensitive and critical alerts | UserNotifications `UNNotificationInterruptionLevel` | 15.0 / critical 12.0 | no | objc; entitlement: com.apple.developer.usernotifications.critical-alerts (Apple approval), time-sensitive | yes | — |  |
| 53 | Notifications & background | Background refresh and processing tasks | BackgroundTasks `BGTaskScheduler`, `BGAppRefreshTask`, `BGProcessingTask` | 13.0 | no | objc; build-time (Info.plist `BGTaskSchedulerPermittedIdentifiers`) | yes | — |  |
| 54 | Notifications & background | Continue a user-started job in the background with system progress UI | BackgroundTasks `BGContinuedProcessingTask` | NEW in iOS 26 | no | objc; entitlement: background GPU access (optional) | yes | GPU use needs the Background GPU Access entitlement. |  |
| 55 | Notifications & background | Background URL transfers | Foundation `URLSession` background configuration | 7.0 | no | objc | yes | .NET `NSUrlSessionHandler` for HttpClient. |  |
| 56 | Notifications & background | Background modes (audio, location, VoIP, BLE, fetch, remote-notification) | Info.plist `UIBackgroundModes` | 4.0 | no | build-time | n/a | Pure Info.plist declaration. |  |
| 57 | Notifications & background | Asset packs downloaded outside the app (self- or Apple-hosted, managed) | BackgroundAssets `BAAssetPackManager`, `BADownloadManager`, `BADownloaderExtension` | 16.0 / managed 26.0 | no | objc; extension (objc) | yes | iOS 26 Managed Background Assets (Apple-hosted, 200 GB per membership). Swift `AssetPackManager` mirror exists. |  |
| 58 | Notifications & background | VoIP pushes (wake app for calls) | PushKit `PKPushRegistry` | 8.0 | no | objc; entitlement: VoIP background mode | yes | Must report to CallKit immediately. |  |
| 59 | Notifications & background | Live Activities and Dynamic Island | ActivityKit `Activity`, `ActivityAttributes`; UI in a WidgetKit extension | 16.1 / scheduled start 26.0 | no | swift-only; extension (swift) | no | Also appear in CarPlay and the Mac menu bar (iOS 26). Push updates are rest (APNs `liveactivity`). | [#803](https://github.com/eQuantic/equantic-ui/issues/803) |
| 60 | Notifications & background | Home Screen / Lock Screen / StandBy / CarPlay widgets | WidgetKit `Widget`, `TimelineProvider`, `WidgetCenter` | 14.0 | no | swift-only; extension (swift) | no | ObjC surface is only the `WGWidgetUserInfoKey*` constants. iOS 27: customization through App Intents and dynamic styling. | [#803](https://github.com/eQuantic/equantic-ui/issues/803) |
| 61 | Notifications & background | Interactive widgets and Control Center / Lock Screen controls | WidgetKit `ControlWidget`, `ControlWidgetButton`/`Toggle` + App Intents | 17.0 / controls 18.0 | no | swift-only; extension (swift); build-time | no | Buttons run App Intents. | [#803](https://github.com/eQuantic/equantic-ui/issues/803) |
| 62 | Notifications & background | Widget reloads via push | WidgetKit `WidgetPushHandler` | NEW in iOS 26 | no | swift-only; rest | no | — |  |
| 63 | Notifications & background | Legacy Today widgets | NotificationCenter `NCWidgetProviding` | 8.0 (deprecated, use WidgetKit) | no | extension (objc) | yes | Deprecated; do not build on it. |  |
| 64 | Notifications & background | Alarms and countdown timers with system alarm UI | AlarmKit `AlarmManager`, `AlarmAttributes` | NEW in iOS 26 | no | swift-only; extension (swift, Live Activity UI) | no | Apple's index tags it "occ" but the ObjC surface is only the version symbols. |  |
| 65 | Notifications & background | In-app feature tips | TipKit `Tip`, `TipView` | 17.0 | no | swift-only | no | — |  |
| 66 | Siri, Shortcuts & Apple Intelligence | Expose app actions to Siri, Shortcuts, Spotlight, widgets | App Intents `AppIntent`, `AppEntity`, `AppEnum` | 16.0 | yes | swift-only; build-time (appintentsmetadataprocessor writes Metadata.appintents) | no | Intents cannot be declared at runtime; metadata is extracted from Swift at compile time. | [#802](https://github.com/eQuantic/equantic-ui/issues/802) |
| 67 | Siri, Shortcuts & Apple Intelligence | Zero-setup Siri phrases | App Intents `AppShortcutsProvider` | 16.0 | yes | swift-only; build-time | no | Phrases localized via `AppShortcuts.xcstrings`. | [#802](https://github.com/eQuantic/equantic-ui/issues/802) |
| 68 | Siri, Shortcuts & Apple Intelligence | Run intents out of process | App Intents extension (`AppIntentsExtension`) | 16.0 | no | extension (swift) | no | iOS 27 `allowedExecutionTargets` picks app / extension / widget. |  |
| 69 | Siri, Shortcuts & Apple Intelligence | Siri AI: natural-language actions on app content (intent and entity schemas) | App Intents app schema domains (`@AppIntent(schema:)`, `@AppEntity(schema:)`) | 18.0 (assistant schemas) / Siri AI NEW in iOS 27 | yes | swift-only; build-time | no | Siri AI shipped Sept 2026 as English beta; not initially in the EU. | [#802](https://github.com/eQuantic/equantic-ui/issues/802) |
| 70 | Siri, Shortcuts & Apple Intelligence | Onscreen awareness: map views to entities | App Intents View Annotations API; `NSUserActivity.appEntityIdentifier` (18.2) | 18.2 / view annotations NEW in iOS 27 | yes | swift-only | no | Exact iOS 27 symbol names unverified. |  |
| 71 | Siri, Shortcuts & Apple Intelligence | Long-running, cancellable and undoable intents | App Intents `LongRunningIntent`, `CancellableIntent`, `UndoableIntent` | UndoableIntent 26.0 / LongRunningIntent NEW in iOS 27 | no | swift-only | no | — |  |
| 72 | Siri, Shortcuts & Apple Intelligence | Interactive snippets | App Intents `SnippetIntent` | NEW in iOS 26 | no | swift-only | no | SwiftUI views. |  |
| 73 | Siri, Shortcuts & Apple Intelligence | Focus filters | App Intents `SetFocusFilterIntent` | 16.0 | no | swift-only | no | — |  |
| 74 | Siri, Shortcuts & Apple Intelligence | Action button, Camera Control, side-button conversational app | App Intents hardware interactions (`CameraCaptureIntent`, `AudioRecordingIntent`) | 17.0 / 18.0 / side button 26.x (select regions) | no | swift-only | no | — |  |
| 75 | Siri, Shortcuts & Apple Intelligence | Legacy SiriKit domains (messaging, calls, workouts, payments, lists) | Intents / IntentsUI `INExtension`, `INInteraction` | 10.0 | no | objc; extension (objc) | yes | Still used for communication notifications and some domains. |  |
| 76 | Siri, Shortcuts & Apple Intelligence | Siri media search and playback requests | Media Intents | NEW in iOS 27 | yes | swift-only | no | Results returned through App Intents. |  |
| 77 | Siri, Shortcuts & Apple Intelligence | Spotlight indexing of app content | CoreSpotlight `CSSearchableIndex`, `CSSearchableItem` | 9.0 | no | objc | yes | — |  |
| 78 | Siri, Shortcuts & Apple Intelligence | Semantic (meaning-based) in-app search | CoreSpotlight `CSUserQuery` | 16.0 / semantic 18.0 | yes | objc | yes | — |  |
| 79 | Siri, Shortcuts & Apple Intelligence | Index App Intents entities into Spotlight | App Intents `IndexedEntity`, `IndexedEntityQuery` (27) | 18.0 | yes | swift-only; build-time | no | — | [#802](https://github.com/eQuantic/equantic-ui/issues/802) |
| 80 | Siri, Shortcuts & Apple Intelligence | Give an LLM access to the app's Spotlight index | CoreSpotlight `SpotlightSearchTool`, `CSSearchableIndexDescription` | NEW in iOS 27 | yes | swift-only (tool); objc (index description) | partial | Tool plugs into Foundation Models. |  |
| 81 | Siri, Shortcuts & Apple Intelligence | Spotlight delegate extension: Apple Intelligence summaries/priority for indexed items | CoreSpotlight `CSIndexExtensionRequestHandler` | 18.4 (unverified) | yes | extension (objc) | yes (unverified) | — |  |
| 82 | Siri, Shortcuts & Apple Intelligence | Test intents, entities and queries through real system pathways (Siri, Spotlight) | App Intents Testing (`AppIntentsTesting`) | NEW in iOS 27 | no | swift-only | no | Test-time only; no UI automation needed. |  |
| 83 | On-device AI (Apple Intelligence) | On-device LLM: prompts, sessions, streaming | Foundation Models `SystemLanguageModel`, `LanguageModelSession` | NEW in iOS 26 | yes | swift-only | no | Model updated with iOS 26.4 and again with iOS 27; requires Apple Intelligence-capable device. | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| 84 | On-device AI (Apple Intelligence) | Guided generation (typed structured output) | Foundation Models `@Generable`, `@Guide` | NEW in iOS 26 | yes | swift-only; build-time (Swift macros) | no | — | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| 85 | On-device AI (Apple Intelligence) | Tool calling | Foundation Models `Tool`, `GenerationOptions.ToolCallingMode` (27) | NEW in iOS 26 | yes | swift-only | no | — | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| 86 | On-device AI (Apple Intelligence) | Any LLM provider (Claude, Gemini, ...) behind one API | Foundation Models `LanguageModel` protocol | NEW in iOS 27 | yes | swift-only | no | Providers ship Swift packages; `CoreAILanguageModel`, `MLXLanguageModel` open-source adapters. | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| 87 | On-device AI (Apple Intelligence) | Apple Foundation Model on Private Cloud Compute | Foundation Models `PrivateCloudComputeLanguageModel` | NEW in iOS 27 | yes | swift-only; entitlement: com.apple.developer.private-cloud-compute | no | No cloud API cost for Small Business Program apps under 2M first-time downloads. |  |
| 88 | On-device AI (Apple Intelligence) | Multimodal prompts (images) with Vision tools | Foundation Models + Vision `OCRTool`, `BarcodeReaderTool` | NEW in iOS 27 | yes | swift-only | no | — | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| 89 | On-device AI (Apple Intelligence) | Swap model/tools/instructions within a session | Foundation Models `LanguageModelSession.DynamicProfile` | NEW in iOS 27 | yes | swift-only | no | — | [#800](https://github.com/eQuantic/equantic-ui/issues/800) |
| 90 | On-device AI (Apple Intelligence) | Evaluate quality of AI features (datasets, metrics, model-judge) | Evaluations | NEW in iOS 27 | yes | swift-only | no | Mostly a development/test-time tool. |  |
| 91 | On-device AI (Apple Intelligence) | Run your own models (any size, incl. full LLMs) on CPU/GPU/ANE | Core AI `AIModel` | NEW in iOS 27 | yes | swift-only; build-time (`.aimodel` via coreai-torch / coreai-optimization) | no | Positioned above Core ML for deep-learning models. |  |
| 92 | On-device AI (Apple Intelligence) | Image Playground sheet / view controller | ImagePlayground `ImagePlaygroundViewController`, `imagePlaygroundSheet` | 18.1 | yes | swift-only | no | — |  |
| 93 | On-device AI (Apple Intelligence) | Programmatic image generation | ImagePlayground `ImageCreator` | 18.4 (deprecated in iOS 27) | yes | swift-only | no | Docs: "Use ImagePlaygroundViewController or imagePlaygroundSheet." |  |
| 94 | On-device AI (Apple Intelligence) | Visual Intelligence search results from your app | VisualIntelligence `SemanticContentDescriptor` + App Intents `IntentValueQuery` | NEW in iOS 26 | yes | swift-only; build-time | no | Camera Control / screenshot search. |  |
| 95 | On-device AI (Apple Intelligence) | In-app translation | Translation `TranslationSession`, `translationPresentation` | 17.4 / programmatic 18.0 | yes | swift-only | no | — | [#801](https://github.com/eQuantic/equantic-ui/issues/801) |
| 96 | On-device AI (Apple Intelligence) | Be the system default translation app | TranslationUIProvider | 18.4 | yes | extension (swift) | no | — |  |
| 97 | On-device AI (Apple Intelligence) | Contextual quick actions under messages | Suggested Actions | NEW in iOS 27 | yes | swift-only; entitlement: Suggested Actions | no | On-device analysis of message text. |  |
| 98 | On-device AI (Apple Intelligence) | Detect coercion/social-engineering risk on transactions | Trust Insights | NEW in iOS 27 | yes (unverified) | swift-only; entitlement: Trust Insights | no | Payments, account operations, etc. |  |
| 99 | On-device AI (Apple Intelligence) | Group faces in images, find engaging video segments | Media Intelligence | NEW in iOS 27 | yes | swift-only | no | — |  |
| 100 | On-device AI (Apple Intelligence) | Music analysis (rhythm, key, loudness, instruments) | Music Understanding | NEW in iOS 27 | yes | swift-only | no | File or streaming analysis. |  |
| 101 | On-device AI (Apple Intelligence) | Generated subtitles (transcribe/translate) in playback | AVKit / AVFoundation / MediaAccessibility | NEW in iOS 27 | yes | objc (unverified) | unverified | Works automatically for HLS/VOD/file playback (WWDC26 session 256). |  |
| 102 | Machine learning & perception | Run Core ML models | Core ML `MLModel`, `MLState`, `MLComputePlan` | 11.0 | yes | objc | yes | — |  |
| 103 | Machine learning & perception | Train models on device | Create ML, Create ML Components | 15.0 / 16.0 | yes | swift-only | no | — |  |
| 104 | Machine learning & perception | Prepare tabular training data | TabularData `DataFrame` | 15.0 | no | swift-only | no | — |  |
| 105 | Machine learning & perception | Classic Vision requests (faces, landmarks, text, barcodes, body/hand pose, saliency, segmentation) | Vision `VNImageRequestHandler`, `VN*Request` | 11.0 | yes | objc | yes | Apple now files this as "original Objective-C and Swift API". | [#786](https://github.com/eQuantic/equantic-ui/issues/786) |
| 106 | Machine learning & perception | Face detection and landmarks | Vision `VNDetectFaceRectanglesRequest`, `VNDetectFaceLandmarksRequest`, `VNDetectFaceCaptureQualityRequest` | 11.0 | yes | objc | yes | Swift-only twins (`DetectFaceRectanglesRequest`) since iOS 18. | [#786](https://github.com/eQuantic/equantic-ui/issues/786) |
| 107 | Machine learning & perception | Text recognition (OCR) | Vision `VNRecognizeTextRequest` | 13.0 | yes | objc | yes | — | [#786](https://github.com/eQuantic/equantic-ui/issues/786) |
| 108 | Machine learning & perception | Swift Vision API: document structure, lens smudge, aesthetics | Vision `RecognizeDocumentsRequest`, `DetectLensSmudgeRequest` | 18.0 / these NEW in iOS 26 | yes | swift-only | no | — |  |
| 109 | Machine learning & perception | Document scanner camera UI | VisionKit `VNDocumentCameraViewController` | 13.0 | yes | objc | yes | — |  |
| 110 | Machine learning & perception | Live camera scanning of text and barcodes | VisionKit `DataScannerViewController` | 16.0 | yes | swift-only | no | Workaround: AVFoundation metadata output + Vision (objc). | [#785](https://github.com/eQuantic/equantic-ui/issues/785) |
| 111 | Machine learning & perception | Live Text, subject lifting and Visual Look Up on images | VisionKit `ImageAnalyzer`, `ImageAnalysisInteraction` | 16.0 | yes | swift-only | no | — |  |
| 112 | Machine learning & perception | Language ID, tokenization, tagging, embeddings | NaturalLanguage `NLTagger`, `NLEmbedding`, `NLContextualEmbedding` | 12.0 / contextual 17.0 | yes | objc | yes | — |  |
| 113 | Machine learning & perception | Speech-to-text (legacy recognizer, custom vocabulary) | Speech `SFSpeechRecognizer`, `SFSpeechLanguageModel` | 10.0 / custom LM 17.0 | yes | objc | yes | — | [#801](https://github.com/eQuantic/equantic-ui/issues/801) |
| 114 | Machine learning & perception | Long-form on-device transcription, dictation, voice activity | Speech `SpeechAnalyzer`, `SpeechTranscriber`, `DictationTranscriber`, `SpeechDetector` | NEW in iOS 26 (input providers NEW in iOS 27) | yes | swift-only | no | iOS 27 adds `AssetInputSequenceProvider`, `CaptureInputSequenceProvider`, `AnalyzerInputConverter`. | [#801](https://github.com/eQuantic/equantic-ui/issues/801) |
| 115 | Machine learning & perception | Text-to-speech, Personal Voice | AVFAudio `AVSpeechSynthesizer` | 7.0 / Personal Voice 17.0 | no | objc | yes | — | [#801](https://github.com/eQuantic/equantic-ui/issues/801) |
| 116 | Machine learning & perception | Sound classification | SoundAnalysis `SNAudioStreamAnalyzer`, `SNClassifySoundRequest` | 13.0 | yes | objc | yes | — |  |
| 117 | Machine learning & perception | Song recognition and custom audio catalogs | ShazamKit `SHSession`, `SHManagedSession` | 15.0 | yes | objc | yes | Device-only before iOS 16 simulator support. |  |
| 118 | Machine learning & perception | Detect nudity / violence in images and video streams | SensitiveContentAnalysis `SCSensitivityAnalyzer`, `SCVideoStreamAnalyzer` (26) | 17.0 | yes | objc; entitlement: com.apple.developer.sensitivecontentanalysis.client | yes (iOS 27 `detectedTypes` unverified) | iOS 27 distinguishes `sexuallyExplicit` vs `goreOrViolence`. |  |
| 119 | Machine learning & perception | Data detection (dates, addresses, flights, tracking numbers) | DataDetection `DDMatch`; Swift `DataDetector` (26) | 15.0 | yes | objc (DDMatch); swift-only (DataDetector) | partial | — |  |
| 120 | Machine learning & perception | Low-level ML on CPU/GPU | Accelerate BNNS / BNNSGraph, Metal Performance Shaders Graph, Metal tensors (Metal 4) | 4.0 / MPSGraph 14.0 / MTLTensor 26.0 | yes | objc (C for BNNS) | partial | `MTLTensor`, `MTL4MachineLearningCommandEncoder` are bound. |  |
| 121 | Machine learning & perception | ML Compute (deprecated) | MLCompute | 14.0 (deprecated) | yes | objc | yes | Use BNNS / MPS / Core ML instead. |  |
| 122 | Commerce & payments | In-App Purchases and subscriptions (StoreKit 2) | StoreKit `Product`, `Transaction`, `AppTransaction`, `AppStore` | 15.0 | no | swift-only; rest (App Store Server API) | no | The single largest Swift-only gap for app developers. |  |
| 123 | Commerce & payments | Original In-App Purchase API | StoreKit `SKPaymentQueue`, `SKProduct` | 3.0 (deprecated in 18.0) | no | objc | yes | Deprecated: "No longer supported." |  |
| 124 | Commerce & payments | Merchandising views (subscription store, product views) | StoreKit `SubscriptionStoreView`, `ProductView` | 17.0 | no | swift-only | no | SwiftUI. |  |
| 125 | Commerce & payments | Request an App Store review | StoreKit `SKStoreReviewController` (deprecated 18), `RequestReviewAction`, `AppStore.requestReview` | 10.3 | no | objc (deprecated) / swift-only | partial | — |  |
| 126 | Commerce & payments | App Store overlay and product page | StoreKit `SKOverlay`, `SKStoreProductViewController` | 14.0 / 6.0 | no | objc | yes | — |  |
| 127 | Commerce & payments | Ad attribution | StoreKit `SKAdNetwork` (objc); AdAttributionKit | 11.3 / AdAttributionKit 17.4 | no | objc / swift-only | partial | AdAttributionKit not bound. |  |
| 128 | Commerce & payments | Apple Ads attribution token | AdServices `AAAttribution` | 14.3 | no | objc | yes | — |  |
| 129 | Commerce & payments | IDFA and tracking authorization | AdSupport `ASIdentifierManager` + AppTrackingTransparency | 6.0 / 14.0 | no | objc | yes | Info.plist `NSUserTrackingUsageDescription`. |  |
| 130 | Commerce & payments | External purchase links / alternative payment (regional) | StoreKit `ExternalPurchase`, `ExternalPurchaseCustomLink`, `ExternalLinkAccount` | 15.4 / 17.4 / 18.1 | no | swift-only; entitlement (regional StoreKit external purchase entitlements) | no | — |  |
| 131 | Commerce & payments | Alternative app marketplace (EU) | MarketplaceKit | 17.4 | no | swift-only; entitlement | no | — |  |
| 132 | Commerce & payments | Apple Pay in app | PassKit `PKPaymentAuthorizationController`, `PKPaymentButton` | 8.0 | no | objc; entitlement: com.apple.developer.in-app-payments | yes | — |  |
| 133 | Commerce & payments | Add/read Wallet passes | PassKit `PKPassLibrary`, `PKAddPassesViewController`, `PKAddPassButton` | 6.0 | no | objc; rest (Wallet Passes web service) | yes | — |  |
| 134 | Commerce & payments | Wallet orders | Wallet Orders + FinanceKitUI `AddOrderToWalletButton` | 16.0 / 17.0 | no | rest; swift-only | no | — |  |
| 135 | Commerce & payments | Push a card into Wallet (in-app provisioning) | PassKit `PKAddPaymentPassViewController` | 9.0 | no | objc; entitlement (issuer, Apple approval) | yes | — |  |
| 136 | Commerce & payments | Verify with Wallet (ID verification in app) | PassKit `PKIdentityAuthorizationController` | 16.0 | no | objc; entitlement | yes | — |  |
| 137 | Commerce & payments | Apple Pay Later merchandising | PassKit `PKPayLaterView` | 17.0 | no | objc | yes | — |  |
| 138 | Commerce & payments | Tap to Pay on iPhone | ProximityReader `PaymentCardReader` | 15.4 | no | swift-only; entitlement: Tap to Pay on iPhone | no | ObjC surface is only the version symbols. |  |
| 139 | Commerce & payments | ID Verifier (read mobile driver's licenses) | ProximityReader `MobileDocumentReader` | 17.0 | no | swift-only; entitlement | no | — |  |
| 140 | Commerce & payments | Tap to Share (merchant iPhone exchanges info with customer iPhone) | ProximityReader `CustomerEngagementSession` | NEW in iOS 27 | no | swift-only; entitlement: Tap to Pay + Tap to Share capability | no | iPhone 12 and later. |  |
| 141 | Commerce & payments | Present identity documents; Digital Credentials API in browsers | IdentityDocumentServices, IdentityDocumentServicesUI | NEW in iOS 26 | no | swift-only; extension (swift); entitlement | no | — |  |
| 142 | Commerce & payments | Host card emulation / contactless credentials in the Secure Element | Core NFC `CardSession`; SecureElementCredential | 17.4 / SecureElementCredential 18.x (unverified) | no | swift-only; entitlement (NFC & SE platform, regional, Apple approval) | no | — |  |
| 143 | Commerce & payments | Apple Card / Apple Cash / Wallet financial data | FinanceKit | 17.0 (index) | no | swift-only; entitlement: com.apple.developer.financekit | no | — |  |
| 144 | Commerce & payments | Car keys in Wallet | CarKey | 16.0 | no | swift-only; entitlement | no | — |  |
| 145 | Cloud & data | iCloud databases, sharing, subscriptions | CloudKit `CKContainer`, `CKDatabase`, `CKShare` | 8.0 | no | objc; rest (CloudKit Web Services / CloudKit JS); entitlement: iCloud | yes | — |  |
| 146 | Cloud & data | Sync engine for CloudKit | CloudKit `CKSyncEngine` | 17.0 | no | objc | yes | Bound in macios (101 references). |  |
| 147 | Cloud & data | iCloud key-value store and iCloud Drive documents | Foundation `NSUbiquitousKeyValueStore`, `FileManager` ubiquity containers | 5.0 | no | objc; entitlement: iCloud | yes | — |  |
| 148 | Cloud & data | Object graph persistence (+ CloudKit mirroring) | Core Data `NSPersistentCloudKitContainer` | 3.0 / 13.0 | no | objc | yes | — |  |
| 149 | Cloud & data | Swift-native persistence | SwiftData | 17.0 | no | swift-only; build-time (`@Model` macro) | no | — |  |
| 150 | Cloud & data | Game save sync in iCloud | GameSave `GSSyncedDirectory` | NEW in iOS 26 | no | objc | yes | — |  |
| 151 | Cloud & data | Shared with You shelf and collaboration | SharedWithYou / SharedWithYouCore | 16.0 | no | objc | yes | — |  |
| 152 | Cloud & data | Share data between app and extensions | App Groups (`containerURL(forSecurityApplicationGroupIdentifier:)`, shared `UserDefaults`) | 8.0 | no | objc; entitlement: com.apple.security.application-groups | yes | Required by nearly every extension design. |  |
| 153 | Cloud & data | Migrate on-device data to/from Android | AppMigrationKit | 26.1 | no | swift-only; extension (swift) | no | Only non-Apple platforms. |  |
| 154 | Personal data & health | Calendars and reminders | EventKit `EKEventStore` (full vs write-only access 17) | 4.0 | no | objc | yes | — |  |
| 155 | Personal data & health | Calendar event editing/choosing UI | EventKitUI `EKEventEditViewController` | 4.0 | no | objc | yes | — |  |
| 156 | Personal data & health | Contacts read/write | Contacts `CNContactStore` (limited access 18) | 9.0 | no | objc | yes | — | [#788](https://github.com/eQuantic/equantic-ui/issues/788) (`UseContacts` already declares it: [#779](https://github.com/eQuantic/equantic-ui/issues/779)) |
| 157 | Personal data & health | Contact picker / contact card UI | ContactsUI `CNContactPickerViewController`; `ContactAccessButton` (SwiftUI, 18) | 9.0 / 18.0 | no | objc / swift-only | partial | Limited-access picker is SwiftUI only. | [#788](https://github.com/eQuantic/equantic-ui/issues/788) |
| 158 | Personal data & health | Provide app-managed contacts to the system | ContactProvider | 18.0 | no | swift-only; extension (swift) | no | — |  |
| 159 | Personal data & health | Photo library read/write, change observation | Photos `PHPhotoLibrary`, `PHAsset` | 8.0 | no | objc | yes | — | [#787](https://github.com/eQuantic/equantic-ui/issues/787) (`UsePhotoLibraryAdd` already declares it: [#779](https://github.com/eQuantic/equantic-ui/issues/779)) |
| 160 | Personal data & health | Privacy-preserving photo picker | PhotosUI `PHPickerViewController` | 14.0 | no | objc | yes | No permission prompt needed. | ✅ `IPhotoLibrary` |
| 161 | Personal data & health | Photo editing extension | PhotosUI `PHContentEditingController` | 8.0 | no | extension (objc) | yes | — |  |
| 162 | Personal data & health | Health data read/write, workouts | HealthKit `HKHealthStore`, `HKLiveWorkoutBuilder` (iOS 26), medications (26) | 8.0 | no | objc; entitlement: com.apple.developer.healthkit | yes | — |  |
| 163 | Personal data & health | Workout zones, menopause data | HealthKit `HKWorkoutZoneGroup`, `menopausalState` | NEW in iOS 27 | no | swift-only (zones); objc (unverified for menopause) | no (not yet bound) | — |  |
| 164 | Personal data & health | Activity rings UI | HealthKitUI `HKActivityRingView` | 9.3 | no | objc | yes | — |  |
| 165 | Personal data & health | Research-grade sensor data | SensorKit `SRSensorReader` | 14.0 | no | objc; entitlement: com.apple.developer.sensorkit.reader.allow (Apple approval) | yes | Research studies only. |  |
| 166 | Personal data & health | Motion, activity, pedometer, altimeter, headphone motion, fall/crash data | CoreMotion `CMMotionManager`, `CMPedometer`, `CMHeadphoneMotionManager` | 4.0 | no | objc | yes | iOS 27: a `UIView` can align sensor data with UI orientation. | ⚠️ `IMotionSensor` (accelerometer and gyroscope); the rest is [#790](https://github.com/eQuantic/equantic-ui/issues/790) |
| 167 | Personal data & health | Workout compositions synced to Apple Watch | WorkoutKit | 17.0 | no | swift-only | no | — |  |
| 168 | Personal data & health | Journaling suggestions picker | JournalingSuggestions | 17.2 (iPadOS 26.0) | yes | swift-only; entitlement | no | — |  |
| 169 | Personal data & health | Child/parent permission requests | PermissionKit | NEW in iOS 26 | no | swift-only | no | iMessage-based asks. |  |
| 170 | Personal data & health | Age range of the user (child safety) | Declared Age Range `AgeRangeService` | NEW in iOS 26 | no | swift-only; entitlement: com.apple.developer.declared-age-range | no | Required in some jurisdictions for age assurance. |  |
| 171 | Personal data & health | Screen Time authorization and app/website pickers | FamilyControls `AuthorizationCenter`, `FamilyActivityPicker` | 15.0 / individual 16.0 | no | swift-only; entitlement: com.apple.developer.family-controls (Apple approval) | no | — |  |
| 172 | Personal data & health | Shield/restrict apps and websites | ManagedSettings `ManagedSettingsStore`; ManagedSettingsUI `ShieldConfigurationDataSource` | 15.0 | no | swift-only; extension (swift) | no | — |  |
| 173 | Personal data & health | Monitor device activity schedules and render usage reports | DeviceActivity `DeviceActivityCenter`, `DeviceActivityMonitor`, `DeviceActivityReport` | 15.0 / reports 16.0 | no | swift-only; extension (swift) | no | — |  |
| 174 | Personal data & health | Web usage reporting for browsers | ScreenTime `STWebpageController` | 14.0 | no | objc | yes | — |  |
| 175 | Media playback & music | Music library playback and queries | MediaPlayer `MPMusicPlayerController`, `MPMediaQuery` | 3.0 | no | objc | yes | — |  |
| 176 | Media playback & music | Lock Screen / Control Center / CarPlay now-playing and remote commands | MediaPlayer `MPNowPlayingInfoCenter`, `MPRemoteCommandCenter`, `MPNowPlayingSession` | 5.0 / 7.1 / 16.0 | no | objc | yes | — |  |
| 177 | Media playback & music | Model-based now-playing (Lock Screen, Control Center, Dynamic Island, CarPlay, Watch) | Now Playing | NEW in iOS 27 | no | swift-only | no | Modern replacement path; MediaPlayer stays available. |  |
| 178 | Media playback & music | Apple Music catalog, library, subscription playback | MusicKit `MusicCatalogSearchRequest`, `ApplicationMusicPlayer` | 15.0 | no | swift-only; rest (Apple Music API) | no | — |  |
| 179 | Media playback & music | Media playback engine (files, HLS, FairPlay) | AVFoundation `AVPlayer`, `AVQueuePlayer`, `AVContentKeySession` | 2.2 | no | objc | yes | — | [#789](https://github.com/eQuantic/equantic-ui/issues/789) |
| 180 | Media playback & music | Player UI, Picture in Picture | AVKit `AVPlayerViewController`, `AVPictureInPictureController` | 8.0 / 9.0 | no | objc | yes | — |  |
| 181 | Media playback & music | AirPlay route picker | AVKit `AVRoutePickerView` | 11.0 | no | objc | yes | — |  |
| 182 | Media playback & music | Custom route picker / third-party protocol discovery | AVRouting; DeviceDiscoveryExtension | 16.0 | no | objc; extension (objc) | yes | — |  |
| 183 | Media playback & music | Send media to third-party TVs/speakers via media device extensions | AVSystemRouting `AVSystemRouteController` | NEW in iOS 27 | no | objc | yes | Device-only in macios. |  |
| 184 | Media playback & music | Be a cast target in the system media device picker | Media Device (extension) | NEW in iOS 27 | no | swift-only; extension (swift); entitlement: com.apple.developer.media-device-extension | no | Info.plist `MDESupportedProtocols`. |  |
| 185 | Media playback & music | Configure HomePod to stream from your service | MediaSetup `MSSetupSession` | 14.0 | no | objc | yes | — |  |
| 186 | Media playback & music | TV provider single sign-on, automatic sign-in | VideoSubscriberAccount | 10.0 / automatic sign-in 26.0 | no | objc; entitlement | yes | — |  |
| 187 | Media playback & music | Captions and accessibility media preferences, Music Haptics | MediaAccessibility | 7.0 | no | objc (C) | yes | — |  |
| 188 | Media capture | Camera capture (photo, video, depth, RAW, ProRAW) | AVFoundation `AVCaptureSession`, `AVCapturePhotoOutput` | 4.0 | no | objc | yes | Info.plist `NSCameraUsageDescription`. | ⚠️ `ICamera` (preview and photo); [#784](https://github.com/eQuantic/equantic-ui/issues/784) |
| 189 | Media capture | Camera Control button integration | AVFoundation `AVCaptureControl`, `AVCaptureSlider`, `AVCaptureIndexPicker` | 18.0 | no | objc | yes | — |  |
| 190 | Media capture | Spatial Audio capture, cinematic video capture, AirPods remote capture | AVFoundation (`AVCaptureSpatialAudioMetadataSampleGenerator`, ...) | NEW in iOS 26 | no | objc | yes (spatial audio verified; others unverified) | — |  |
| 191 | Media capture | Center Stage front camera, high-resolution photo, fast-launch camera | AVFoundation | iOS 27 additions | no | objc | unverified | WWDC26 sessions; symbol names unverified. |  |
| 192 | Media capture | Camera facing relative to a view (foldable) | AVKit `AVCaptureDeviceDirectionCoordinator` | NEW in iOS 27 (27.1, beta) | no | objc | no (not yet bound) | — |  |
| 193 | Media capture | Camera experience from the Lock Screen | LockedCameraCapture `LockedCameraCaptureExtension` | 18.0 | no | swift-only; extension (swift) | no | ObjC surface is one activity-type constant. |  |
| 194 | Media capture | Simple camera/picker UI | UIKit `UIImagePickerController` | 2.0 | no | objc | yes | — |  |
| 195 | Media capture | Recording, editing, export, composition | AVFoundation `AVAssetWriter`, `AVMutableComposition`, `AVAssetExportSession` | 4.0 | no | objc | yes | — |  |
| 196 | Media capture | Audio session, recording, audio engine | AVFAudio `AVAudioSession`, `AVAudioRecorder`, `AVAudioEngine` | 3.0 (framework split 14.5) | no | objc | yes | Bound inside the AVFoundation namespace. | [#789](https://github.com/eQuantic/equantic-ui/issues/789) (`UseMicrophone` already declares it: [#779](https://github.com/eQuantic/equantic-ui/issues/779)) |
| 197 | Media capture | Low-level audio and Audio Unit v3 plug-ins | AudioToolbox, AudioUnit, CoreAudioKit, Core Audio Types | 2.0 / AUv3 9.0 | no | objc (C); extension (objc) for AUv3 | yes | — |  |
| 198 | Media capture | MIDI | CoreMIDI | 4.2 | no | objc (C) | yes | — |  |
| 199 | Media capture | Spatial / environmental audio for games | PHASE | 15.0 | no | objc | yes | Simulator support from 26.0. |  |
| 200 | Media capture | Custom haptic patterns (AHAP), audio-haptic sync | CoreHaptics `CHHapticEngine` | 13.0 | no | objc | yes | — | [#345](https://github.com/eQuantic/equantic-ui/issues/345) |
| 201 | Media capture | Screen recording and live broadcast | ReplayKit `RPScreenRecorder`, `RPBroadcastActivityViewController` | 9.0 | no | objc | yes | — |  |
| 202 | Media capture | Broadcast upload extension | ReplayKit `RPBroadcastSampleHandler` | 10.0 | no | extension (objc) | yes | — |  |
| 203 | Media capture | Screen capture with fine-grained filters | ScreenCaptureKit | NEW in iOS 27 (on iOS) | no | objc | yes | Device-only in macios. Entitlement `com.apple.developer.persistent-content-capture` exists for persistent capture. |  |
| 204 | Media capture | Cinematic mode playback/editing | Cinematic `CNAssetInfo`, `CNRenderingSession` | 17.0 | no | objc | yes | Device-only. |  |
| 205 | Media capture | Hardware video encode/decode | VideoToolbox | 8.0 | no | objc (C) | yes | — |  |
| 206 | Media capture | Image processing and filters, RAW (version 9 in iOS 27) | Core Image `CIFilter`, `CIRAWFilter` | 5.0 | no | objc | yes | — |  |
| 207 | Media capture | Image read/write (HEIC, metadata) | ImageIO | 4.0 | no | objc (C) | yes | — |  |
| 208 | Media capture | Barcode / QR / face metadata from the live camera feed | AVFoundation `AVCaptureMetadataOutput` | 7.0 | yes | objc | yes | The ObjC path to live barcode scanning that DataScannerViewController does not offer from C#. | [#785](https://github.com/eQuantic/equantic-ui/issues/785) |
| 209 | Media capture | Hardware button capture events (volume buttons, AirPods stem) | AVKit `AVCaptureEventInteraction` | 17.2 | no | objc | yes | AirPods remote capture added with iOS 26 (unverified binding of new members). |  |
| 210 | Media capture | Live Photos display | PhotosUI `PHLivePhotoView` | 9.1 | no | objc | yes | — |  |
| 211 | Drawing & documents | Pencil drawing canvas and tool picker | PencilKit `PKCanvasView`, `PKToolPicker` | 13.0 | no | objc | yes | iOS 27: stroke identity, selection, erase paths (Swift additions). |  |
| 212 | Drawing & documents | Handwriting recognition and ink search | PencilKit `PKStrokeRecognizer` | NEW in iOS 27 | yes | swift-only | no | — |  |
| 213 | Drawing & documents | Markup (shapes, text boxes, images over ink) | PaperKit `PaperMarkupViewController`, `PaperMarkup` | NEW in iOS 26 | no | swift-only | no | — |  |
| 214 | Drawing & documents | PDF display, annotation, search | PDFKit `PDFView`, `PDFDocument` | 11.0 | no | objc | yes | — |  |
| 215 | Drawing & documents | File previews | QuickLook `QLPreviewController`; preview extension `QLPreviewingController` | 4.0 | no | objc; extension (objc) | yes | — |  |
| 216 | Drawing & documents | Thumbnails (incl. thumbnail extension) | QuickLookThumbnailing `QLThumbnailGenerator`, `QLThumbnailProvider` | 13.0 | no | objc; extension (objc) | yes | — |  |
| 217 | Drawing & documents | Rich link previews | LinkPresentation `LPLinkView`, `LPMetadataProvider` | 13.0 | no | objc | yes | — |  |
| 218 | Drawing & documents | SF Symbols and symbol animations | Symbols `NSSymbolEffect`; UIKit `UIImage(systemName:)` | 13.0 / effects 17.0 | no | objc | yes | — |  |
| 219 | Drawing & documents | Uniform Type Identifiers | UniformTypeIdentifiers `UTType` | 14.0 | no | objc | yes | — |  |
| 220 | Drawing & documents | Transferable model types | CoreTransferable `Transferable` | 16.0 | no | swift-only | no | — |  |
| 221 | Drawing & documents | Charts (2D and 3D, iOS 26) | Swift Charts `Chart`, `Chart3D` (26) | 16.0 | no | swift-only | no | SwiftUI-based. |  |
| 222 | Graphics, games & AR | GPU rendering and compute (Metal 4 in iOS 26) | Metal `MTLDevice`, `MTL4CommandQueue`, `MTL4Compiler`, `MTLTensor` | 8.0 / Metal 4 26.0 | no | objc | yes | Metal 4 types are bound. | ✅ Photon draws with Metal |
| 223 | Graphics, games & AR | Metal views and texture loading | MetalKit `MTKView`, `MTKTextureLoader` | 9.0 | no | objc | yes | — |  |
| 224 | Graphics, games & AR | Upscaling, frame interpolation, denoising | MetalFX | 16.0 / interpolation+denoise 26.0 | no | objc | yes | Device-only in macios. |  |
| 225 | Graphics, games & AR | GPU image/ML kernels | Metal Performance Shaders, MPSGraph | 9.0 / 14.0 | yes | objc | yes | — |  |
| 226 | Graphics, games & AR | Layer compositing and animation | Core Animation (QuartzCore) `CALayer`, `CAMetalLayer` | 2.0 | no | objc | yes | — |  |
| 227 | Graphics, games & AR | 2D drawing and text layout | Core Graphics, Core Text | 2.0 / 3.2 | no | objc (C) | yes | — |  |
| 228 | Graphics, games & AR | 2D game engine | SpriteKit | 7.0 | no | objc | yes | — |  |
| 229 | Graphics, games & AR | 3D scene graph (legacy) | SceneKit | 8.0 (deprecated in iOS 26) | no | objc | yes | Apple: "SceneKit is deprecated, use RealityKit instead." |  |
| 230 | Graphics, games & AR | 3D rendering, physics, AR content, spatial | RealityKit | 13.0 | no | swift-only | no | The SceneKit successor is Swift-only. |  |
| 231 | Graphics, games & AR | Node-graph particle and GPU compute simulations for RealityKit | Compute Graph | NEW in iOS 27 | no | swift-only (ObjC side is C macros/structs only) | no | Apple tags it occ; the occ surface is helper macros. |  |
| 232 | Graphics, games & AR | Author and compose USD scenes | USDKit | NEW in iOS 27 | no | swift-only | no | — |  |
| 233 | Graphics, games & AR | 3D asset import/export | Model I/O | 9.0 | no | objc | yes | — |  |
| 234 | Graphics, games & AR | AR world/face/body/image tracking, LiDAR scene reconstruction, geo anchors | ARKit `ARSession`, `ARWorldTrackingConfiguration` | 11.0 | no | objc | yes | iOS 27: orientation-independent frames, `metadataObjects`, `trackingObjects`. | [#791](https://github.com/eQuantic/equantic-ui/issues/791) (a decision) |
| 235 | Graphics, games & AR | On-device photogrammetry / guided object capture | RealityKit `ObjectCaptureSession`, `PhotogrammetrySession` | 17.0 | no | swift-only | no | LiDAR devices. |  |
| 236 | Graphics, games & AR | Room scanning to parametric 3D | RoomPlan `RoomCaptureView`, `RoomCaptureSession` | 16.0 | no | swift-only | no | Apple tags it occ; ObjC side is articles only. |  |
| 237 | Graphics, games & AR | Game controllers, keyboards, mice, spatial controllers, stylus | GameController `GCController`, `GCStylus` (26) | 7.0 | no | objc | yes | — |  |
| 238 | Graphics, games & AR | On-screen touch controls for Metal games | TouchController `TCTouchController` | NEW in iOS 26 | no | objc | yes | — |  |
| 239 | Graphics, games & AR | Game Center (leaderboards, achievements, multiplayer, activities, challenges) | GameKit `GKLocalPlayer`, `GKGameActivity` (26), `GKChallengeDefinition` (26) | 3.0 | no | objc; entitlement: Game Center | yes | iOS 26 Apple Games app. |  |
| 240 | Graphics, games & AR | Game logic utilities (pathfinding, state machines, randomization) | GameplayKit | 9.0 | no | objc | yes | — |  |
| 241 | Graphics, games & AR | 3D math primitives | Spatial | 16.0 | no | objc (C) | no | System.Numerics covers most needs. |  |
| 242 | Graphics, games & AR | Hierarchical Variable Font outlines | hvf | 18.4 | no | objc (C) | no | Font tooling niche. |  |
| 243 | Communication | VoIP call UI integrated with the system (incl. live call translation) | CallKit `CXProvider`, `CXCallController`, `CXSetTranslatingCallAction` (26) | 10.0 | no | objc | yes | — |  |
| 244 | Communication | Call blocking and caller identification | CallKit `CXCallDirectoryProvider` | 10.0 | no | extension (objc) | yes | Call Directory extension. |  |
| 245 | Communication | Modern VoIP conversations, default dialer app | LiveCommunicationKit `ConversationManager`, `TelephonyConversationManager` (26) | 17.4 | no | swift-only; entitlement: Default Dialer App | no | — |  |
| 246 | Communication | Walkie-talkie style apps | PushToTalk `PTChannelManager` | 16.0 | no | objc; entitlement: com.apple.developer.push-to-talk | yes | — |  |
| 247 | Communication | Compose SMS/iMessage and email | MessageUI `MFMessageComposeViewController`, `MFMailComposeViewController` | 3.0 | no | objc | yes | — | [#778](https://github.com/eQuantic/equantic-ui/issues/778) |
| 248 | Communication | iMessage apps and stickers | Messages `MSMessagesAppViewController` | 10.0 | no | extension (objc) | yes | Sticker packs need no code. |  |
| 249 | Communication | SMS/MMS filtering and spam reporting | IdentityLookup `ILMessageFilterExtension`, `ILClassificationUIExtensionViewController` | 11.0 / 12.0 | no | extension (objc) | yes | — |  |
| 250 | Communication | Default carrier messaging app (SMS/MMS/RCS) | TelephonyMessagingKit | NEW in iOS 26 | no | swift-only; entitlement: Default Carrier Messaging App | no | EU-only; iPhone only. |  |
| 251 | Communication | Cellular carrier info | CoreTelephony | 4.0 | no | objc | yes | Most carrier fields deprecated/redacted. |  |
| 252 | Communication | Anticipated cellular degradation notices | WirelessInsights | NEW in iOS 26 | no | swift-only | no | — |  |
| 253 | Communication | SharePlay shared experiences | GroupActivities `GroupActivity`, `GroupSession` | 15.0 | no | swift-only | no | — |  |
| 254 | Communication | Peer-to-peer sessions (Bluetooth/Wi-Fi) | MultipeerConnectivity | 7.0 | no | objc | yes | — |  |
| 255 | Communication | Read the user's Focus status (communication apps) | Intents `INFocusStatusCenter` | 15.0 | no | objc; entitlement: Communication Notifications (unverified) | yes (unverified) | — |  |
| 256 | Location & maps | Location, heading, visits, region monitoring, beacons | CoreLocation `CLLocationManager`, `CLMonitor` (17), `CLLocationUpdater` (17), `CLServiceSession` (18), `CLBackgroundActivitySession` (17) | 2.0 | no | objc | yes | The Swift async `CLLocationUpdate.liveUpdates` is Swift-only; its ObjC counterpart `CLLocationUpdater` is bound, as are `CLMonitor` and `CLServiceSession`. | ✅ `ILocation` |
| 257 | Location & maps | One-tap temporary location permission button | CoreLocationUI `CLLocationButton` | 15.0 | no | objc | yes | — |  |
| 258 | Location & maps | Maps, annotations, overlays, directions, search, Look Around, place cards | MapKit `MKMapView`, `MKLookAroundViewController`, `MKMapItemDetailViewController` | 3.0 | no | objc | yes | MapKit for SwiftUI is Swift-only. | [#778](https://github.com/eQuantic/equantic-ui/issues/778) (open in the maps app) |
| 259 | Location & maps | Geocoding and structured addresses | MapKit `MKGeocodingRequest`, `MKReverseGeocodingRequest`, `MKAddress` | NEW in iOS 26 | no | objc | yes | Replaces `CLGeocoder`. |  |
| 260 | Location & maps | Place descriptors portable across map providers | GeoToolbox `PlaceDescriptor` | NEW in iOS 26 | no | swift-only | no | — |  |
| 261 | Location & maps | Weather conditions, forecasts, alerts | WeatherKit `WeatherService` | 16.0 | no | swift-only; rest (WeatherKit REST API); entitlement: com.apple.developer.weatherkit | no | REST path is a full alternative from C#. |  |
| 262 | Location & maps | Server-side geocoding/search/ETA | Apple Maps Server API | n/a | no | rest | n/a | — |  |
| 263 | Connectivity & accessories | Bluetooth Low Energy central/peripheral | CoreBluetooth `CBCentralManager` | 5.0 | no | objc | yes | — |  |
| 264 | Connectivity & accessories | Privacy-preserving accessory pairing (BLE, Wi-Fi) | AccessorySetupKit `ASAccessorySession` | 18.0 | no | objc | yes | — |  |
| 265 | Connectivity & accessories | Wi-Fi Aware (NAN) peer-to-peer with accessories | Wi-Fi Aware | NEW in iOS 26 | no | swift-only; entitlement: com.apple.developer.wifi-aware | no | Uses the Swift-only Network `NetworkConnection` API. |  |
| 266 | Connectivity & accessories | Join/configure Wi-Fi hotspots, current network info | NetworkExtension `NEHotspotConfigurationManager`, `NEHotspotNetwork` | 11.0 / 14.0 | no | objc; entitlement: hotspot configuration | yes | — |  |
| 267 | Connectivity & accessories | Share Wi-Fi credentials to a paired accessory | Wi-Fi Infrastructure | 26.2 | no | swift-only; extension (swift) | no | EU-only for customer installs. |  |
| 268 | Connectivity & accessories | Secure data transport to your accessory | Accessory Transport Extension | 26.2 | no | swift-only; extension (swift) | no | EU-only for customer installs; iPhone only. |  |
| 269 | Connectivity & accessories | Forward iOS notifications to your accessory | Accessory Notifications | 26.5 | no | swift-only; extension (swift) | no | EU interoperability (DMA). |  |
| 270 | Connectivity & accessories | Forward Live Activities to your accessory | Accessory Live Activities | 26.5 | no | swift-only; extension (swift) | no | EU interoperability (DMA). |  |
| 271 | Connectivity & accessories | Automatic audio switching for third-party headphones | AudioAccessoryKit | 26.4 | no | swift-only | no | EU interoperability (DMA). |  |
| 272 | Connectivity & accessories | NFC tag read/write (NDEF, ISO 7816, MIFARE, FeliCa) | CoreNFC `NFCNDEFReaderSession`, `NFCTagReaderSession` | 11.0 / 13.0 | no | objc; entitlement: com.apple.developer.nfc.readersession.formats | yes | Weak-linked in macios. |  |
| 273 | Connectivity & accessories | Ultra Wideband precise distance/direction | NearbyInteraction `NISession` | 14.0 | no | objc | yes | — |  |
| 274 | Connectivity & accessories | MFi accessories (Lightning/USB-C/Bluetooth Classic) | ExternalAccessory `EAAccessoryManager` | 3.0 | no | objc; build-time (Info.plist protocols) | yes | MFi program required. |  |
| 275 | Connectivity & accessories | Smart home | HomeKit `HMHomeManager` | 8.0 | no | objc; entitlement: com.apple.developer.homekit | yes | — |  |
| 276 | Connectivity & accessories | Matter device control | Matter `MTRDeviceController`, `MTRBaseDevice` | 16.0 / 16.1 | no | objc | no | Has an ObjC API but macios does not bind it. |  |
| 277 | Connectivity & accessories | Matter commissioning into your own ecosystem | MatterSupport `MatterAddDeviceRequest`, extension | 16.1 | no | swift-only; extension (swift) | no | — |  |
| 278 | Connectivity & accessories | Thread network credentials | ThreadNetwork `THClient` | 15.0 | no | objc | yes | Device-only. |  |
| 279 | Connectivity & accessories | Motorized camera stands | DockKit | 17.0 | no | swift-only | no | — |  |
| 280 | Connectivity & accessories | Connect to nearby devices (picker) | DeviceDiscoveryUI `DDDevicePickerViewController` | NEW in iOS 26 (on iOS) | no | objc | yes | — |  |
| 281 | Connectivity & accessories | Apple Watch companion messaging | WatchConnectivity `WCSession` | 9.0 | no | objc | yes | — |  |
| 282 | Connectivity & accessories | CarPlay apps (audio, communication, navigation, EV, parking, quick food, fueling) | CarPlay `CPTemplateApplicationScene`, templates | 12.0 / templates 14.0 | no | objc; entitlement: com.apple.developer.carplay-* (Apple approval) | yes | — |  |
| 283 | Connectivity & accessories | Home energy forecasts, EV charging insights, Home app energy data | EnergyKit | NEW in iOS 26 | no | swift-only; entitlement: EnergyKit (LoadEvents in iOS 27) | no | — |  |
| 284 | Connectivity & accessories | Crash detection and emergency SOS events | SafetyKit `SACrashDetectionManager` | 16.0 | no | objc; entitlement (Apple approval) | yes | — |  |
| 285 | Connectivity & accessories | USB/PCI drivers on M-series iPad | DriverKit | 16.0 | no | objc (C++) | no | iPad with M1 or later. |  |
| 286 | Connectivity & accessories | Read Wallet loyalty passes over NFC (VAS) | CoreNFC `NFCVASReaderSession` | 13.0 | no | objc; entitlement (Apple approval) | yes | — |  |
| 287 | Networking | TCP/UDP/QUIC/TLS/WebSocket connections, Bonjour, path monitoring | Network (C API `nw_connection`, `nw_listener`, `nw_browser`, `nw_path_monitor`) | 12.0 | no | objc (C) | yes | Bound as `Network.NWConnection` etc. | ✅ `INetworkStatus` |
| 288 | Networking | Structured-concurrency networking API | Network `NetworkConnection`, `NetworkListener`, `NetworkBrowser` | NEW in iOS 26 | no | swift-only | no | — |  |
| 289 | Networking | HTTP(S) | Foundation `URLSession` | 7.0 | no | objc | yes | .NET `HttpClient` + `NSUrlSessionHandler`. |  |
| 290 | Networking | VPN, packet tunnel, content filters, DNS proxy, relays, app proxy | NetworkExtension `NEPacketTunnelProvider`, `NEFilterDataProvider`, `NEDNSProxyProvider`, `NERelayManager` | 8.0+ | no | extension (objc); entitlement: com.apple.developer.networking.networkextension | yes | — |  |
| 291 | Networking | Privacy-preserving URL filtering | NetworkExtension `NEURLFilterManager` | NEW in iOS 26 | no | swift-only; entitlement | no | iOS 27 adds reporting (supervised devices only). |  |
| 292 | Networking | Local push connectivity (push without internet) | NetworkExtension `NEAppPushManager`, `NEAppPushProvider` | 14.0 | no | extension (objc); entitlement | yes | iOS 27: MCX network slice. |  |
| 293 | Networking | 5G network slicing / carrier-constrained (satellite) networks | Entitlements (5G Network Slicing App Category, carrier-constrained app category) | 17.0 / 26.0 | no | entitlement | n/a | Pure entitlement declarations. |  |
| 294 | Networking | Distributed actors across devices | Distributed | 16.0 | no | swift-only | no | Swift language library. |  |
| 295 | Networking | Local network permission and Bonjour declarations | Info.plist `NSLocalNetworkUsageDescription`, `NSBonjourServices` | 14.0 | no | build-time | n/a | Pure Info.plist keys. |  |
| 296 | Security & identity | Face ID / Touch ID / passcode authentication | LocalAuthentication `LAContext`, `LARight` | 8.0 / 16.0 | no | objc | yes | Info.plist `NSFaceIDUsageDescription`. | ✅ `IBiometrics` |
| 297 | Security & identity | Passkeys (sign in, register) | AuthenticationServices `ASAuthorizationPlatformPublicKeyCredentialProvider` | 15.0 | no | objc; entitlement: associated-domains (webcredentials) | yes | — |  |
| 298 | Security & identity | Passkey-first account creation | AuthenticationServices `ASAuthorizationAccountCreationProvider` | NEW in iOS 26 | no | swift-only | no | — |  |
| 299 | Security & identity | Import/export credentials between managers (Credential Exchange) | AuthenticationServices `ASCredentialImportManager`, `ASCredentialExportManager` | NEW in iOS 26 | no | swift-only | no | — |  |
| 300 | Security & identity | OAuth / web login session | AuthenticationServices `ASWebAuthenticationSession` | 12.0 | no | objc | yes | — |  |
| 301 | Security & identity | Sign in with Apple | AuthenticationServices `ASAuthorizationAppleIDProvider` | 13.0 | no | objc; rest (Sign in with Apple REST); entitlement: com.apple.developer.applesignin | yes | — |  |
| 302 | Security & identity | Hardware security keys (FIDO) | AuthenticationServices `ASAuthorizationSecurityKeyPublicKeyCredentialProvider` | 15.0 | no | objc | yes | — |  |
| 303 | Security & identity | Password AutoFill / credential provider app | AuthenticationServices `ASCredentialProviderViewController` | 12.0 | no | extension (objc); entitlement: com.apple.developer.authentication-services.autofill-credential-provider | yes | — |  |
| 304 | Security & identity | Keychain storage | Security `SecItemAdd`/`SecItemCopyMatching` (keychain services) | 2.0 | no | objc (C) | yes | macios `SecRecord`/`SecKeyChain`. | ✅ `ISecretStore` |
| 305 | Security & identity | Secure Enclave keys, certificates, trust evaluation | Security `SecKey`, `SecTrust`, `SecCertificate` | 2.0 | no | objc (C) | yes | — |  |
| 306 | Security & identity | Swift cryptography (AES-GCM, ChaChaPoly, P-256, Curve25519, HPKE, post-quantum) | CryptoKit | 13.0 | no | swift-only | no | .NET's own runtime reaches it through a small `@_cdecl` Swift shim (System.Security.Cryptography.Native.Apple). |  |
| 307 | Security & identity | App Attest and DeviceCheck | DeviceCheck `DCAppAttestService`, `DCDevice` | 11.0 / App Attest 14.0 | no | objc; rest (Apple verification servers) | yes | — |  |
| 308 | Security & identity | Smart cards and token-based credentials | CryptoTokenKit | 13.0 | no | objc | yes | — |  |
| 309 | Security & identity | Flag/check URLs needing extra security | LinkSecurity `LSLinkSecurityManager` | NEW in iOS 27 | no | objc | yes | Bound in macios; not listed in Apple's online technologies index (unverified docs). |  |
| 310 | Security & identity | Enhanced security / hardened process (memory tagging, hardened heap) | Entitlements `com.apple.security.hardened-process*` | NEW in iOS 26 | no | entitlement; build-time | n/a | Relevant to any runtime that allocates executable or tagged memory. |  |
| 311 | Security & identity | Managed-deployment secrets and configuration | ManagedApp | 18.4 | no | swift-only | no | MDM context. |  |
| 312 | Security & identity | Exam lockdown mode | AutomaticAssessmentConfiguration | 13.4 | no | objc; entitlement | yes | — |  |
| 313 | Security & identity | Exposure notifications (public health authorities) | ExposureNotification | 13.5 | no | objc; entitlement (government apps only) | no | Effectively retired. |  |
| 314 | Accessibility | VoiceOver, custom actions, rotors, Dynamic Type, accessibility notifications | UIKit `UIAccessibility` informal protocol | 3.0 | no | objc | yes | — | ⚠️ `SemanticNode` |
| 315 | Accessibility | Custom content, audio graphs, Braille translation, settings | Accessibility `AXCustomContent`, `AXChartDescriptor`, `AXBrailleTranslator` (26), `AXSettings` | 14.0 | no | objc | yes | — |  |
| 316 | Accessibility | Accessibility Nutrition Labels | App Store Connect metadata | 2025 (with iOS 26) | no | n/a (App Store Connect) | n/a | Declared per app on the product page. |  |
| 317 | System services & diagnostics | Performance and crash diagnostics | MetricKit `MXMetricManager` | 13.0 | no | objc | yes | — |  |
| 318 | System services & diagnostics | Async metric/diagnostic reports | MetricKit `MetricManager`, `MetricReport`, `DiagnosticReport` | NEW in iOS 27 | no | swift-only | no | Replaces `MXMetricManager` going forward. |  |
| 319 | System services & diagnostics | App-defined states for diagnostics and Instruments | StateReporting `SRStateReporter` | NEW in iOS 27 | no | objc | yes | Feeds MetricKit state-segmented metrics. |  |
| 320 | System services & diagnostics | Out-of-process crash handler | CrashReportExtension | NEW in iOS 27 | no | swift-only; extension (swift) | no | Reads the crashed process over a read-only Mach port. |  |
| 321 | System services & diagnostics | Unified logging and signposts | OSLog / os `os_log`, `OSLogStore` | 10.0 / OSLog 15.0 | no | objc (C) | yes | — |  |
| 322 | System services & diagnostics | Darwin notifications (cross-process) | Darwin Notify `notify_post` | 9.3 | no | objc (C) | partial (P/Invoke) | — |  |
| 323 | System services & diagnostics | Compression and archives | Compression; Apple Archive | 9.0 / 14.0 | no | objc (C) | partial (Compression yes, AppleArchive no) | — |  |
| 324 | System services & diagnostics | Low-level IPC | XPC | 17.4 | no | objc (C) | no | Used by BrowserEngineKit processes. |  |
| 325 | System services & diagnostics | JavaScript engine embedding | JavaScriptCore `JSContext` | 7.0 | no | objc | yes | — |  |
| 326 | System services & diagnostics | Accelerated math, DSP, image processing | Accelerate (vDSP, vImage, BNNS, simd) | 4.0 | no | objc (C) | partial | — |  |
| 327 | Web | In-app web view | WebKit `WKWebView`, `WKWebExtension` | 8.0 | no | objc | yes | SwiftUI `WebView`/`WebPage` (iOS 26) are Swift-only. | `WebFrame`, not realized on Photon |
| 328 | Web | In-app Safari | SafariServices `SFSafariViewController` | 9.0 | no | objc | yes | — | [#778](https://github.com/eQuantic/equantic-ui/issues/778) |
| 329 | Web | Safari web extensions | Safari Web Extension (manifest + JS) + `SFSafariExtensionHandler`/`NSExtensionRequestHandling` | 15.0 | no | extension (objc native handler; JS/HTML payload) | partial | Payload is web code by nature. |  |
| 330 | Web | Safari content blockers | SafariServices `SFContentBlockerManager`, content blocker extension (JSON rules) | 9.0 | no | extension (objc) | yes | — |  |
| 331 | Web | Alternative browser engines (EU, Japan) | BrowserEngineKit, BrowserEngineCore | 17.4 | no | objc; extension (objc); entitlement: com.apple.developer.web-browser-engine.* | yes | — |  |
| 332 | Web | Export browsing data / check engine eligibility | BrowserKit `BEBrowserDataExportManager` | 18.4 | no | objc | no | Not bound in macios. |  |
| 333 | Web | Default browser / mail / navigation / translation / password manager app | Default-app entitlements (`com.apple.developer.web-browser`, ...) | 14.0+ (more in 18.x) | no | entitlement | n/a | — |  |
| 334 | App extensions & distribution | Share extension | `NSExtensionContext`, `UIViewController` principal class (`com.apple.share-services`) | 8.0 | no | extension (objc) | yes | Writable in C#; low memory budget. |  |
| 335 | App extensions & distribution | Action extension | `com.apple.ui-services` | 8.0 | no | extension (objc) | yes | — |  |
| 336 | App extensions & distribution | Custom keyboard | UIKit `UIInputViewController` (`com.apple.keyboard-service`) | 8.0 | no | extension (objc) | yes | No network unless "Requests Open Access". |  |
| 337 | App extensions & distribution | File Provider (cloud storage in Files) | FileProvider `NSFileProviderReplicatedExtension` (16) / `NSFileProviderExtension` | 11.0 | no | extension (objc) | yes | — |  |
| 338 | App extensions & distribution | File Provider UI actions | FileProviderUI `FPUIActionExtensionViewController` | 11.0 | no | extension (objc) | yes | — |  |
| 339 | App extensions & distribution | Spotlight import extension | CoreSpotlight `CSImportExtension` | 15.0 | no | extension (objc) | yes (unverified) | — |  |
| 340 | App extensions & distribution | Host third-party extension UI in your app | ExtensionKit `EXHostViewController`; ExtensionFoundation | 16.1 (iOS 26 per macios) | no | objc (host side); extension (swift) for extension side | partial | macios binds ExtensionKit for iOS 26.0. |  |
| 341 | App extensions & distribution | App Clips | AppClip `APActivationPayload`; separate App Clip target | 14.0 | no | objc; build-time (second app target) | yes | Size limits: 15 MB classic, up to 100 MB on newer iOS (iOS 26 demo clips 100 MB). .NET runtime size matters here. |  |
| 342 | App extensions & distribution | Universal Links into App Clips, App Clip Codes | Associated Domains `appclips:` | 14.0 | no | entitlement | n/a | — |  |
| 343 | App extensions & distribution | Managed app distribution / automated enrollment (enterprise) | Managed App Distribution; Automated Device Enrollment | 17.2 / 16.1 | no | swift-only | no | MDM vendors. |  |
| 344 | App extensions & distribution | Education (Schoolwork progress, assignables) | ClassKit; Assignables | 11.4 / 15.4 | no | objc (ClassKit); swift-only (Assignables) | partial | ClassKit bound. |  |
| 345 | App extensions & distribution | Reseller account linking | ServicesAccountLinking | 16.4 | no | objc | no | — |  |
| 346 | Web services (REST) | Push notifications provider (APNs), Live Activity pushes, broadcast pushes | APNs HTTP/2 provider API | n/a | no | rest | n/a | Server side; any language. |  |
| 347 | Web services (REST) | App Store Server API, Server Notifications, receipts, Retention Messaging, Advanced Commerce | App Store server APIs | n/a | no | rest | n/a | Complements StoreKit 2 (verify transactions server-side). |  |
| 348 | Web services (REST) | Apple Music API | Apple Music API | n/a | no | rest | n/a | Developer token + user token (user token minting needs MusicKit or MusicKit JS). |  |
| 349 | Web services (REST) | CloudKit Web Services / CloudKit JS | CloudKit Web Services | n/a | no | rest | n/a | — |  |
| 350 | Web services (REST) | WeatherKit REST API | WeatherKit REST | n/a | no | rest | n/a | Full alternative to the Swift-only WeatherKit. |  |
| 351 | Web services (REST) | Sign in with Apple REST (token validation, revocation) | Sign in with Apple REST API | n/a | no | rest | n/a | — |  |
| 352 | Web services (REST) | Wallet passes and orders web services | Wallet Passes / Wallet Orders web services | n/a | no | rest | n/a | — |  |
| 353 | Web services (REST) | Apple Maps Server API, Maps Web Snapshots | Apple Maps Server API | n/a | no | rest | n/a | — |  |
| 354 | Web services (REST) | DeviceCheck / App Attest server validation | DeviceCheck server API | n/a | no | rest | n/a | — |  |
| 355 | Web services (REST) | Apple Pay merchant token management | Apple Pay Merchant Token Management API | n/a | no | rest | n/a | — |  |
