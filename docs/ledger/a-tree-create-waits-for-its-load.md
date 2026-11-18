- **2026-11-18 · A tree's create-in-place test waits for the load it asserts**: the case that creates under a node waits
  for the children's load to be asked for, then for the field, where it read the load as soon as it saw the field,
  while the load starts on the scheduler's next turn and the field shows in the build that opened the node. On a
  loaded machine the field came a frame first and the case failed
  ([#1600](https://github.com/eQuantic/equantic-ui/issues/1600)).
