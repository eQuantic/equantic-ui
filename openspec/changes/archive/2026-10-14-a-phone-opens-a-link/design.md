# Design

## How Flutter solves it

Flutter's `url_launcher` plugin has the same two calls, `launchUrl` and `canLaunchUrl`, and `launchUrl`
answers a `Future<bool>` for the same reason this one is a `ValueTask<bool>`: iOS answers in a
callback. The manifest half is the developer's: its README tells them to add
`LSApplicationQueriesSchemes` to `Info.plist` and a `queries` element to `AndroidManifest.xml`, by
hand, for every scheme `canLaunchUrl` will be asked about, and a missing entry is a silent `false`.
Here the same lines that decide what the app opens write both. The row is `MethodChannel` in
`docs/FLUTTER-PARITY.md`: a typed contract per capability, realized per shell.

## Decisions

**A contract of its own, not a phone sibling of `IWorkspace`.** The issue left the choice to this
change. "Open this link" is the same sentence on every system, and the two file doors are not: a
sibling would have given a phone a second interface with the same two members under another name, and
a component written for both would have taken both. So the link methods leave `IWorkspace`, which
keeps the file doors and stays desktop-only, and `IUrlLauncher` is registered by every shell. The
browser registers none: there a link is an anchor and the browser already routes it, as `IDeepLinks`
is the address bar there.

**Asynchronous because one system is.** UIKit's synchronous `openURL:` is retired, and the current one
answers in a completion handler on the main thread. An answer given at once on an iPhone could only be
a guess, so the contract waits for it, and the shells that answer at once return a finished task.

**The rule is written once.** The relative-URL check, the policy and its logged refusal were written
twice while two desktops answered, and the copies had already drifted once (a check that answered
`false` to a relative URL where the contract says to throw). Four shells would have been four copies.
`UrlLauncher` holds the order (who was wrong, what the app opens, then the system), and a shell writes
`Hand` and `Claimed`, the two calls only it knows.

**The manifest is written from the declaration, as `builder.Bundle` already is.** The generator reads
`builder.Workspace.Opens…` and writes an assembly attribute; `eqicon` reads it off the compiled
assembly with the metadata reader and writes each system's file. The named doors and the generator
share one rule (`OpenedSchemeRule`, compiled into both, like `BundleFactRule`), so `OpensMail()` cannot
open one scheme at run time and declare another at build time.

**iOS lists what the app opens and its own schemes; Android adds the web's.** Measured on the iOS 27
simulator: `canOpenURL:` answers for `https`, `sms` and `maps` with nothing listed, and answers `false`
for a third-party app's scheme until it is listed. On Android 11 and later an app that lists nothing is
told that nothing opens even `https`, so the web's two schemes are always there. The app's own schemes
are not listed on Android, where an app always sees itself.

**Android's element is an overlay.** .NET for Android writes the manifest from attributes and has none
for `queries`. Its own item for a fragment it merges over the generated manifest is
`AndroidManifestOverlay`, so the SDK writes the fragment before the merge and adds it there, and the
app keeps no manifest of its own. The merge does not count its overlays among its inputs: measured, an
added `Opens("whatsapp")` rewrote the fragment and left the merged manifest without it. A merged
manifest older than the fragment is removed, which is what makes the merge run.

**A constant, or an error where a manifest is written.** A scheme built at run time can widen the
policy and can never reach a manifest, and on a phone the app would open the link and be told that
nothing opens it. That is `EQ3007` at the line, reported only when the compilation is a phone's; a
desktop reads the line at run time and nothing else needs it.

## Not here

- One call that opens a place in the person's map on every system: today it is a link per system
  (`https://maps.apple.com` on an iPhone, `geo:` on Android). #916.
- A write-once `Link` to another app's address on Photon, which is inert on every shell because no
  shell sets the navigation seam. #917.
- A physical phone. The iOS simulator has no Phone and no Mail app. #792.
