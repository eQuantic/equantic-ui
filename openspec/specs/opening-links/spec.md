# opening-links Specification

## Purpose
What handing a link to the system answers on every Photon shell, the rule applied before the
platform is asked, and what a phone's build writes so that asking whether something opens a link
tells the truth.

## Requirements

### Requirement: Every Photon shell opens a link through one contract

Every Photon shell (macOS, Windows, iOS, Android) SHALL register an `IUrlLauncher`, whose
`OpenUrlAsync(url)` hands an absolute URL to whatever the system says claims its scheme and answers
whether the system took it, and whose `CanOpen(url)` answers whether something claims it without
acting. `IWorkspace` SHALL carry only the doors for a file.

#### Scenario: A phone opens what its apps claim

- **WHEN** the Android emulator's app opens `tel:`, `sms:`, `mailto:`, `geo:` and `https:` links its
  `Program.cs` opens
- **THEN** each answers true and brings the dialer, the messages app, the mail app, the map app and
  the browser to the front

#### Scenario: Nothing claims the scheme

- **WHEN** an app opens a scheme its policy allows and no installed app claims
- **THEN** the answer is false and the app stays in front

#### Scenario: The query form on a Mac

- **WHEN** `CanOpen` is asked about `https` and about a scheme nothing claims, both allowed
- **THEN** it answers true and false, and nothing is shown on the screen

### Requirement: The rule before the platform is the same on every shell

Before a system is asked, a launcher SHALL throw for a null or relative URL, SHALL answer false for
a scheme the app's `OpenUrlPolicy` does not hand over without asking the system, logging the scheme
and the line that would open it, and SHALL hand the system the URL's absolute form. `CanOpen` SHALL
apply the same policy and log nothing.

#### Scenario: A scheme the app does not open

- **WHEN** an app whose policy is the web's opens `tel:+351000000000`
- **THEN** the answer is false, the system is never asked, and one warning names
  `builder.Workspace.Opens("tel")`

#### Scenario: The question applies the same policy

- **WHEN** an app that opens `mailto` asks `CanOpen` about a `mailto:` and a `tel:` link
- **THEN** only the `mailto:` link reaches the system, and nothing is logged

#### Scenario: The caller's mistake

- **WHEN** a relative URL or null is handed to either method
- **THEN** it throws, and the system is never asked

#### Scenario: What reaches the system

- **WHEN** `https://acme.example/a b` is opened
- **THEN** the system is handed `https://acme.example/a%20b`

### Requirement: A phone's build lists the schemes the app may ask about

Each scheme a `builder.Workspace.Opens…` line opens, and each an `OpenUrlPolicy.Allowing(…)` in the
app's code widens by, SHALL be declared on the compiled assembly, once,
lower-cased, in ordinal order. The iOS partial manifest SHALL list those schemes and the app's own
under `LSApplicationQueriesSchemes`. An Android app SHALL carry a `queries` element with a VIEW intent
for `http`, `https` and each opened scheme, merged into the manifest it ships, and an added or removed
line SHALL reach the next build's manifest.

#### Scenario: The declarations

- **WHEN** an app calls `OpensPhone().OpensMail()` and `Opens("Slack").OpensMessages().OpensMail()`
- **THEN** the assembly declares `mailto`, `slack`, `sms` and `tel`

#### Scenario: A policy the app registers itself

- **WHEN** an app registers `OpenUrlPolicy.Web.Allowing("slack").Allowing(["Teams", "zoommtg"])`
  itself, which the builder steps aside for
- **THEN** the assembly declares `slack`, `teams` and `zoommtg`, and the Android app's merged manifest
  lists them

#### Scenario: Every named door lists what it opens

- **WHEN** each public `Opens…` method of the workspace builder that takes no argument is called
- **THEN** the schemes the generator declares for that call are the ones the policy opens at run time

#### Scenario: Without the list

- **WHEN** an Android app is built with the `queries` element left out
- **THEN** `CanOpen` answers false on the emulator for every scheme, `https` included, and with it
  answers true for each scheme an installed app claims

#### Scenario: Another app's scheme on an iPhone

- **WHEN** an iPhone app that opens `eqbench` asks `CanOpen` about it, with an app that claims it
  installed, on the iOS simulator
- **THEN** it answers true, where an app built without `LSApplicationQueriesSchemes` answers false

#### Scenario: A line added later

- **WHEN** `Opens("whatsapp")` is added to an Android app and it is built again
- **THEN** the merged manifest lists `whatsapp`, and lists it no more once the line is removed

### Requirement: A scheme a phone app opens is a constant

A scheme passed to `builder.Workspace.Opens(…)` or to `OpenUrlPolicy.Allowing(…)` that the build
cannot read SHALL be `EQ3007` at
that line when the compilation is an iOS or an Android app, and SHALL NOT be reported for a desktop
app. A name the builder refuses SHALL declare nothing.

#### Scenario: Built at run time on a phone

- **WHEN** an Android or an iOS app calls `Opens(scheme)` with a scheme read from its arguments
- **THEN** the build reports `EQ3007`, and the constant schemes beside it are still declared

#### Scenario: Built at run time on a desktop

- **WHEN** a desktop app calls the same line
- **THEN** nothing is reported

#### Scenario: A constant

- **WHEN** an iOS app calls `Opens(Chat)` with `const string Chat = "slack"`
- **THEN** nothing is reported and `slack` is declared

#### Scenario: .NET's own scheme names

- **WHEN** an iOS app calls `Opens(Uri.UriSchemeMailto)` and `Opens(Uri.UriSchemeHttps)`, which are
  `static readonly` and not `const`
- **THEN** nothing is reported and `https` and `mailto` are declared, and `Opens(Uri.UriSchemeFile)`
  declares nothing

#### Scenario: A call through a conditional access

- **WHEN** an app writes `builder.Workspace?.OpensMail()`, `builder.Bundle?.UrlScheme("acme")`,
  `builder.Capabilities?.UseCamera(…)` and `builder.Entitlements?.RequireNetworkClient()`
- **THEN** each is declared on the assembly as the same call through `.` is
