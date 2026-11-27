- **2026-11-26 · A README capture that cannot be taken says why, at once**: `scripts/capture-readme.sh ios`
  built the Wallet, launched it and then ended naming nothing. On a Mac with the repository on an external
  volume the simulator's screenshot could not be written under `/Volumes`, and the `python3` had no Pillow,
  and both were sent to `/dev/null`: the target ended with no line, or with "no capture there" after a
  minute of retries. Pillow is now asked for before anything is built (`readme-captures.py ready`), only a
  picture of nothing is waited out (it exits with its own code, 3), a check that refuses for another
  reason and a screenshot that fails stop the target at once with their own words, and the simulator writes
  its screenshot into the temporary directory, from where it is moved into the raw captures.
  `scripts/check-capture-readme.sh` proves each case against stubs in CI
  ([#1198](https://github.com/eQuantic/equantic-ui/issues/1198)). Proposed and archived through OpenSpec
  (`openspec/specs/working-agreement`).
