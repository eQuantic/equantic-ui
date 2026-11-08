# Tasks

## 1. The field

- [x] 1.1 Write the dots into the node and add `Obscured`, in `SemanticNode` and `SemanticsVisitor`
- [x] 1.2 Map it on Android (`Password`) and macOS (`AXSecureTextField`); iOS reads the dots as the value
- [x] 1.3 Prove it both ways: `AnObscuredField_HoldsDotsInTheTree_AndSaysSo` fails with the visitor as it stood and passes
- [ ] 1.4 Check it on a real platform: `uiautomator dump` on the Android emulator shows `password="true"`, or leave it to the owner with the reason

## 2. Documentation

- [x] 2.1 One ledger entry under `docs/ledger/` citing #817; the wiki states no gap here, so no page changes
