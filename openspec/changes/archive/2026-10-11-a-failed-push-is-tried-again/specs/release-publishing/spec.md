# Spec Delta

## Purpose

How a release's packages reach nuget.org from the run that built them: what a push that fails for
a reason outside the package does, how a release that stops halfway is finished from its own run's
packages without a new version, and how its release page is written.

## ADDED Requirements

### Requirement: A package nuget.org already lists is not pushed again

Publishing a release SHALL skip every package whose version nuget.org's package index already
lists, and SHALL push only the others.

#### Scenario: A release that stopped after 33 of 50 packages

- **WHEN** the 50 packages of a release are published and nuget.org already lists 33 of them at
  that version
- **THEN** 17 pushes are made, one for each package nuget.org does not list

### Requirement: A push that fails for a reason outside the package is tried again

When a push fails and nuget.org did not refuse the key, publishing SHALL go on to the other
packages and then try again, together, the ones that failed, after a wait, a fixed number of
times, each wait longer than the one before, and SHALL pause between one push and the next. The
waits SHALL be spent once for the whole list, so that the time publishing can wait does not grow
with the number of packages. A package the index lists by the time it is tried again SHALL NOT be
pushed again.

#### Scenario: nuget.org answers 500 and then accepts the package

- **WHEN** the push of a package fails twice with a 500 and succeeds on the third try
- **THEN** the other packages are pushed first, and that one is pushed after one wait and then a
  longer one

#### Scenario: nuget.org answers 500 to everything

- **WHEN** every push of each of three packages fails
- **THEN** the three are tried five times in turn, each wait is spent once and not once per
  package, and publishing fails naming the three

#### Scenario: nuget.org took the package before it answered 500

- **WHEN** a push fails and the index lists that package at the next round
- **THEN** the package is not pushed again and counts as published

### Requirement: Publishing goes on after a package it could not push and fails at the end

A package that still fails after the last try SHALL NOT stop the packages after it from being
pushed. Publishing SHALL then fail, naming every package it could not push.

#### Scenario: One package never goes through

- **WHEN** every try of one package fails and the packages after it succeed
- **THEN** the packages after it are pushed, and publishing exits with a failure that names that
  package

### Requirement: A push refused for the key is not tried again, and the list goes on

When nuget.org refuses a push for the key (401 or 403), publishing SHALL NOT try that package
again, since waiting changes nothing, SHALL go on to the packages after it, and SHALL fail at the
end naming each package that was refused and saying the key refused them.

#### Scenario: A key whose scope lacks one new package

- **WHEN** nuget.org answers 403 to one package and accepts the others
- **THEN** that package is pushed once, the others are published, and publishing fails naming it
  as refused for the key

#### Scenario: The key has expired

- **WHEN** nuget.org answers 403 to every push
- **THEN** each package is pushed once with no wait spent on any, and publishing fails naming all
  of them

### Requirement: Everything the run packed is published or publishing fails

Publishing a release with no package ids given SHALL refuse, before any push, a set of packages
that holds one at a version other than the release's, naming it. A package SHALL be looked for by
the release's version as NuGet writes it in a package's file name (no build metadata, three numeric
parts at least, a fourth only when it is not zero, no leading zero), and SHALL be found listed by
that same version lower-cased, as the index writes it.

#### Scenario: A package packed at another version

- **WHEN** the run's packages hold `Gamma.1.0.0.nupkg` beside the release's own
- **THEN** publishing fails naming that file, and nothing is pushed

#### Scenario: A version NuGet writes otherwise than it was given

- **WHEN** a release is given the version `1.0`, or `9.9.9-CHECK.1+build.7`, and its packages are
  `Alpha.1.0.0.nupkg`, or `Alpha.9.9.9-CHECK.1.nupkg`, as `dotnet pack` names them
- **THEN** those packages are the release's, not strays, and one the index lists at `1.0.0`, or at
  `9.9.9-check.1`, is not pushed again

### Requirement: A release that stopped halfway is finished by hand from its own run's packages

A workflow run by hand SHALL take the id of the run that built a release, the release's tag and,
optionally, the ids of the packages to publish. It SHALL refuse, before it downloads anything, a
run that is not the push of that tag at the tag's commit, since another run's packages at the same
version are other bytes. It SHALL download that run's packages and publish,
by the requirements above, the packages it was given, or every one of them nuget.org does not list
yet when it was given none, and SHALL then write the tag's release page. It SHALL refuse a package
id that the run's packages do not hold before it pushes anything, and the key SHALL be read only
from the repository's secrets.

#### Scenario: Finishing 0.2.0-preview.61

- **WHEN** the workflow is run with run 38132878034, tag `v0.2.0-preview.61` and no package ids
- **THEN** it pushes the packages nuget.org does not list at `0.2.0-preview.61` and writes the
  release page of `v0.2.0-preview.61`

#### Scenario: A pull request's run at the same version

- **WHEN** the workflow is run with the id of a pull request's run and tag `v0.2.0-preview.61`
- **THEN** it fails saying which run it was given and which one the release's is, and nothing is
  downloaded or pushed

#### Scenario: A package id the run did not build

- **WHEN** the workflow is given the package id `eQuantic.UI.Missing`
- **THEN** it fails naming that id, and no package is pushed

### Requirement: The release page is written from the tag's annotation, whoever writes it

The release page SHALL be titled `eQuantic.UI <version>`, SHALL carry the body of the tag's
annotation (its subject line left out), SHALL be marked a pre-release when the version has a
pre-release suffix, and SHALL be edited in place, not created twice, when it exists already. The
tag's own pipeline and the workflow run by hand SHALL write it the same way.

#### Scenario: A page that exists already

- **WHEN** the release page of `v0.2.0-preview.61` exists and the page is written again
- **THEN** that page's title and body are set from the annotation, and no second page is created

### Requirement: Publishing is exercised on every pull request

The pipeline SHALL run the publishing logic on every pull request and on `main` against a stub of
the push and of nuget.org's package index, and SHALL fail when any requirement above does not hold.

#### Scenario: A change that stops publishing at the first failure

- **WHEN** a pull request makes publishing stop at the first package it cannot push
- **THEN** that pull request's checks fail, naming the requirement it broke
