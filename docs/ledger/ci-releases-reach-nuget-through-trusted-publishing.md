- **2026-11-05 · A release reaches nuget.org with no key that expires**: the tag run of
  `0.2.0-preview.66` passed every job and then met nuget.org's 403 for an expired key on all 52
  packages, since nuget.org now caps a key's life at weeks. Both workflows that publish log in
  through nuget.org's trusted publishing instead, in the `nuget` environment, with a key that lives
  an hour, and `scripts/check-publish-packages.sh` fails a pull request that brings a stored key
  back ([#1224](https://github.com/eQuantic/equantic-ui/issues/1224)). Proposed and archived
  through OpenSpec (`openspec/specs/release-publishing`).
