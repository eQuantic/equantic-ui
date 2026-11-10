- **2026-11-10 · A web save is announced once, and says why it hydrates or draws over**: a Bug under #1174
  ([#1373](https://github.com/eQuantic/equantic-ui/issues/1373)). The events of one save could straddle the start
  of its rebuild, and the debounce asked for a second rebuild after the first had taken the save; with no save to
  read a time from, it waited for an edit after it was asked, found none, and told the page to mount, so the page
  the first rebuild had reloaded into hydrating markup reloaded again and drew over it, about once in four saves of
  the `samples` job. A rebuild now answers only the saves made since the last one, an event for a path already gone no longer dates a save after its edit, and the server writes why each
  rebuild hydrates or mounts; the edit-to-view measurement records the frames each document heard, what the reload
  carried and whether the server rendered it, and names them when a save draws over.
