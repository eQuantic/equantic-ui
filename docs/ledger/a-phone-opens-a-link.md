- **2026-10-14 · A phone opens a link**: `IUrlLauncher` (`OpenUrlAsync`, `CanOpen`) is a contract of
  its own, registered by every Photon shell, and `IWorkspace` keeps the doors for a file. On the
  Android emulator `tel`, `sms`, `mailto`, `geo` and `https` open the dialer, Messages, Gmail, Google
  Maps and Chrome; on the iOS simulator `https`, `sms` and a map open Safari, Messages and Maps. The
  build writes what a phone asks about from `builder.Workspace.Opens…`: `LSApplicationQueriesSchemes`
  on iOS and a `queries` overlay on Android, without which every `CanOpen` on the emulator answered
  false, `https` included. A scheme built at run time is `EQ3007` on a phone
  ([#778](https://github.com/eQuantic/equantic-ui/issues/778)). Proposed and archived through
  OpenSpec (`openspec/specs/opening-links`).
