- **2026-11-15 · A tree's test holds its load**: the TreeView browser test that expected a folder's loading item two
  frames after the click raced its own loader, which resolved at once, and lost on loaded CI runners. It now holds
  the load until it has seen the loading item, then lets it go and waits for the children
  ([#1553](https://github.com/eQuantic/equantic-ui/issues/1553)).
