# Design

## How Flutter answers it

pub.dev, Flutter's registry, offers automated publishing from GitHub Actions, which exchanges the
job's OIDC token for a temporary credential, with no stored secret. nuget.org's
trusted publishing does the same for `publish-nuget` since #1224. The Marketplace now offers the
same mechanism, and this change takes it rather than keeping a token that expires.

## No fallback

`vsce publish --oidc` never falls back to a personal access token, and the job keeps no secret to
fall back to. A policy that disagrees with the workflow fails the release loudly on its first step
that needs the Marketplace, which is the failure worth having: a fallback would keep a credential
alive whose only job is to make that failure quiet.

## An instrument that fails

`check-publish-packages.sh` already fails a workflow that brings a stored NuGet key back. It now reads
the `publish-extension` job too and fails on a secret read there, a publish without `--oidc`, or a
job not granted `id-token: write`. Each case has a control that breaks a copy of the workflow and must
fail, so the guard cannot go quiet when the job's text moves. Comments are not read, since the job's
own comment names the permission.
