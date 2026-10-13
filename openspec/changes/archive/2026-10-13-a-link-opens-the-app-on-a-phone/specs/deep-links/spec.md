## ADDED Requirements

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

### Requirement: The Android launcher is a single task

The launcher activity the generator writes SHALL be a single task, whether or not the app declares a
scheme.

#### Scenario: The launcher's launch mode

- **WHEN** the generator writes the launcher for an app with no scheme
- **THEN** it declares the single-task launch mode

### Requirement: What an iPhone app declares reaches its Info.plist

The partial manifest of what an app declares SHALL be contributed where the iOS SDK collects partial
manifests, so that its keys are in the built `Info.plist`. With nothing declared, a file from an
earlier build SHALL be removed.

#### Scenario: The sample's declared sentences

- **WHEN** the Wallet sample is built for the simulator from a clean tree
- **THEN** every key the SDK wrote from its declarations is in the built `Info.plist` with the same
  value

#### Scenario: Nothing declared any more

- **WHEN** the manifest is written for an assembly that declares nothing, over a file an earlier
  build left
- **THEN** the file is removed
