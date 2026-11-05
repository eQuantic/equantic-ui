## MODIFIED Requirements

### Requirement: A release that stopped halfway is finished by hand from its own run's packages

A workflow run by hand SHALL take the id of the run that built a release, the release's tag and,
optionally, the ids of the packages to publish. It SHALL refuse, before it downloads anything, a
run that is not the push of that tag at the tag's commit, since another run's packages at the same
version are other bytes. It SHALL download that run's packages and publish,
by the requirements above, the packages it was given, or every one of them nuget.org does not list
yet when it was given none, and SHALL then write the tag's release page. It SHALL refuse a package
id that the run's packages do not hold before it pushes anything, and the key SHALL be the one
nuget.org's trusted publishing issues to that run.

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

## ADDED Requirements

### Requirement: Publishing holds no key that expires

The tag's pipeline and the workflow run by hand SHALL push with a key that nuget.org's trusted
publishing issues to the run, exchanged for the run's OIDC token in the `nuget` environment right
before the push, and SHALL NOT read a key from the repository's secrets. The pipeline's checks
SHALL fail on a pull request that leaves a job pushing without that login, outside that
environment, without the grant of the token, or with a key from anywhere but the login.

#### Scenario: A stored key comes back

- **WHEN** a pull request makes `publish-nuget` push with `secrets.NUGET_API_KEY`
- **THEN** that pull request's checks fail, saying `ci.yml` reads a stored key

#### Scenario: The release a stored key stopped

- **WHEN** `Publish a release` runs with run 41999444013 and tag `v0.2.0-preview.66`, whose tag run
  met nuget.org's 403 for an expired key on all 52 packages
- **THEN** it logs in through the policy for `publish-release.yml` and publishes the packages
  nuget.org does not list at `0.2.0-preview.66`
