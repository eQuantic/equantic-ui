# deep-links Specification

## Purpose
What a declared URL scheme writes on each system, what `IDeepLinks.Launch` answers and who hears a
link on a phone, and how what an iPhone app declares reaches its `Info.plist`.

## Requirements

### Requirement: One declared scheme opens the app on every system

A scheme an app declares with `builder.Bundle.UrlScheme` SHALL be written where each system reads
it: the Mac's bundle, the iOS app's `Info.plist` as a URL type named by the app's identifier, and the
Android launcher activity as a filter for VIEW with the DEFAULT and BROWSABLE categories. An app that
declares none SHALL get none.

#### Scenario: The Android launcher

- **WHEN** an app declares `acme`, `Acme-Beta` and `acme` again
- **THEN** the launcher the generator writes carries one filter with the schemes `acme` and
  `acme-beta`, in that order

#### Scenario: No scheme

- **WHEN** an app declares no scheme
- **THEN** the launcher carries no filter for links

#### Scenario: The iOS partial manifest

- **WHEN** the manifest is written for an assembly that declares two schemes and no capability
- **THEN** it carries one `CFBundleURLTypes` entry with both schemes, named by the identifier given

#### Scenario: A scheme the app did not declare

- **WHEN** a link on an undeclared scheme is opened on the Android emulator
- **THEN** the system resolves it to nothing

#### Scenario: An intent aimed at the launcher by name

- **WHEN** another sender aims a VIEW intent at the exported launcher by name, with a URL on a scheme
  the app did not declare, with the app running and again with it stopped
- **THEN** no subscriber hears it, and `Launch` is null for the start it caused

### Requirement: A phone tells the app what it was started with

On a phone `IDeepLinks.Launch` SHALL answer the URL the app was started with, readable when the first
tree is built, and `null` for an app started normally, whatever arrives later. A URL that arrives
while the app runs SHALL be delivered to its subscribers and SHALL NOT become `Launch`. Each start
SHALL replace the answer.

#### Scenario: Started normally

- **WHEN** a relay is told the app was started with nothing, and is then offered a URL
- **THEN** a subscriber hears the URL and `Launch` is null

#### Scenario: Started again

- **WHEN** a relay is told the app was started with a URL, then with nothing, then with another URL
- **THEN** `Launch` answers the first, then null, then the third

#### Scenario: Started by a link on a device

- **WHEN** a link on a declared scheme is opened with the app not running, on the Android emulator
  and on the iOS simulator
- **THEN** the root component's constructor reads that URL from `Launch`

#### Scenario: A link while the app runs on a device

- **WHEN** a link is opened while the app runs, on the Android emulator and on the iOS simulator
- **THEN** a subscriber hears it, `Launch` is unchanged, and on Android there is still one activity
  in the same process

#### Scenario: A return after the system took the process

- **WHEN** an app started by a link is sent home, its process is killed, and the app is opened again
- **THEN** `Launch` is null: the person opened the app, not the link again

#### Scenario: A re-creation in a live process

- **WHEN** an app started by a link has its activity re-created in the same process (the font size
  changes)
- **THEN** the new tree reads the same `Launch`, and no subscriber is told of a new link

### Requirement: The Android launcher of an app that links open is a single task

The launcher activity the generator writes SHALL be a single task when the app declares a scheme, and
SHALL keep Android's own launch mode when it declares none.

#### Scenario: The launcher's launch mode

- **WHEN** the generator writes the launcher for an app with a scheme, and for one with none
- **THEN** the first declares the single-task launch mode and the second declares no launch mode

### Requirement: A tree is taken down with its surface

A host SHALL be able to take its tree down, telling every component that mounted that it left, once.
The Android shell SHALL do so when an activity is destroyed, and for the activity before it when a
new one is created first, so that a link is heard once, by the tree that is alive.

#### Scenario: A host unmounted

- **WHEN** a host that drew a root and a nested stateful component is unmounted, twice
- **THEN** each of the two is told it left, once

#### Scenario: A subscription the tree held

- **WHEN** a component subscribed to the relay in its mount, its host is unmounted, and a URL is
  offered
- **THEN** the component does not hear it

#### Scenario: A link after the activity before it ended

- **WHEN** an app is opened from its icon, left with Back, started again by a link, and handed a link
  while it runs, on the Android emulator
- **THEN** the link is heard once

### Requirement: What an iPhone app declares reaches its Info.plist

The partial manifest of what an app declares SHALL be written after the compiler and handed over
where the iOS SDK writes the final manifest, so that its keys are in the built `Info.plist`. Handing
it over SHALL NOT bring the compiler forward in the build. The file SHALL be rewritten only when what
it says changed, and with nothing declared a file from an earlier build SHALL be removed.

#### Scenario: The shape of the targets

- **WHEN** the SDK's targets are read
- **THEN** the manifest is written after `Compile` with no dependency on it, the early list of
  manifests is not touched, and the file is added to the late list before the final manifest is
  written

#### Scenario: An app with a resource file

- **WHEN** an iPhone app with a `.resx` is built from a clean tree
- **THEN** its assembly carries the resource, and its `Info.plist` carries what the app declared

#### Scenario: Written again with nothing changed

- **WHEN** the manifest is written twice for the same declarations
- **THEN** the second leaves the file's time as it was

#### Scenario: The sample's declared sentences

- **WHEN** the Wallet sample is built for the simulator from a clean tree
- **THEN** every key the SDK wrote from its declarations is in the built `Info.plist` with the same
  value

#### Scenario: Nothing declared any more

- **WHEN** the manifest is written for an assembly that declares nothing, over a file an earlier
  build left
- **THEN** the file is removed
