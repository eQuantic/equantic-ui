# Tasks

## 1. The workflows log in

- [x] 1.1 Create the trusted publishing policies on nuget.org: `equantic-ui (ci.yml)` and `equantic-ui (publish-release.yml)`, owner `eQuantic`, repository `eQuantic/equantic-ui`, environment `nuget`, packages `eQuantic.UI.*`. Verify each is listed Active with the repository's permanent ids (#6192270, #1139934644).
- [x] 1.2 Give `publish-nuget` and `publish-release.yml`'s job the `nuget` environment, `id-token: write` and a `NuGet/login` step pinned by commit, and hand the push the step's key. Verify the workflows read `secrets.NUGET_API_KEY` nowhere.

## 2. The guard

- [x] 2.1 Teach `scripts/check-publish-packages.sh` the workflows' promises, with a control for each that must fail. Verify the guard passes on this branch and fails on `main`'s workflows, naming all five broken promises in each file.

## 3. The docs

- [x] 3.1 Say in CLAUDE.md's Version Management section where the key comes from. No wiki page describes the release pipeline, so the wiki does not change. Verify the docs guards pass (`dotnet test tests/eQuantic.UI.Web.Tests`).
- [x] 3.2 Write the ledger entry under `docs/ledger/`, citing #1224.

## 4. The real thing

- [x] 4.1 Say in the pull request that its proof is the run of `Publish a release` after the merge, with run 41999444013 and `v0.2.0-preview.66`: it logs in through the policy for `publish-release.yml` and publishes the packages the expired key could not. A workflow run by hand is dispatched only from `main`, and a throwaway branch that ran it on push would publish the release from a branch nobody reviewed, so the login is proven where it will run.
