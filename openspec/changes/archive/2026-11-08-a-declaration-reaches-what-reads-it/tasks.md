## 1. A capability an API uses

- [x] 1.1 The four methods and their mappings go, and the generator refuses the general form with `EQ3008`. Verify: `ACapabilityWithNoApiTests` fails with the refusal taken out

## 2. A fact reaches the phone

- [x] 2.1 `CapabilityManifest.Write` writes what an iPhone reads, and names what only a Mac reads. Verify: `AFactAnIPhoneReads_ReachesTheManifest_AndOneOnlyAMacReadsIsNamed`, and `PhonePartialManifestTests` still removes an empty manifest

## 3. Against the real thing

- [x] 3.1 `samples/WalletMobile` declares `ITSAppUsesNonExemptEncryption`, and `scripts/check-ios-launch.sh` in CI's `samples` job compares every key of the partial manifest with the built bundle

## 4. Documentation

- [x] 4.1 The wiki's Capabilities, Photon and Diagnostics pages in all five languages, Upgrading in both, and `docs/DIAGNOSTICS.md`
- [x] 4.2 One entry for the ledger citing #779 and #878, as a file of its own under `docs/ledger/`
