- **2026-10-30 · A declaration reaches what reads it, and nothing asks for what no API uses**: `UseContacts`,
  `UseNotifications`, `UseMicrophone` and `UsePhotoLibraryAdd` go, and the general form is refused with `EQ3008`,
  since they wrote a permission no contract used and both stores reject that
  ([#779](https://github.com/eQuantic/equantic-ui/issues/779)). A fact stated on `builder.Bundle` reaches an iPhone's
  manifest through the partial manifest the schemes use, and the build names what only a Mac reads
  ([#878](https://github.com/eQuantic/equantic-ui/issues/878)). Proposed and archived through OpenSpec
  (`openspec/specs/native-manifests`).
