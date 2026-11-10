- **2026-11-09 · A web save reloads the page once, into markup that holds the edit**: each save
  reloaded the page twice. dotnet watch applied the edit to the server and refreshed the browser
  itself, with the modules from before the edit, and the rebuild reloaded it again seconds later into
  markup the boot could not trust, so it drew over it. The rebuild stream is now the page's one source
  of reloads: the shell keeps dotnet watch's refresh script out of the page by the guard the script
  keeps against connecting twice, and the stream names the server's run (a page that reconnects to
  another run reloads), says when the modules are new and whether the server holds the edit, counted
  through .NET's `MetadataUpdateHandler`, and carries a failed rebuild's errors to the development
  overlay. The boot hydrates the markup and replays the page's fields after it. Measured on the
  dashboard sample, one document per save where there were two, hydrated with no warning, and the
  replay went from 98 to 229ms on CI to 37 to 42ms on a loaded machine. A test reads the guard out of
  the SDK global.json selects, and the measurement fails a run that reloads twice
  ([#1275](https://github.com/eQuantic/equantic-ui/issues/1275)). Proposed through OpenSpec
  (`openspec/specs/hot-reload`).
