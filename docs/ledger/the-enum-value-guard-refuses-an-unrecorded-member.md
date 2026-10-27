- **2026-10-25 · The enum value guard refuses a member it has no line for**: a bug under #849
  ([#1096](https://github.com/eQuantic/equantic-ui/issues/1096)). `EnumValueAbiTests` refused a recorded member
  whose number moved or that vanished, and passed over one never recorded: 18 members of `CodeCommand`, 2 of
  `SheetEditMode` and 2 of `SheetEntryView` had been appended that way, unpinned, so the next member inserted
  above one of them would have renumbered it with every suite green. A member with no line fails now, named,
  until it is recorded with `EQ_UPDATE_ENUM_VALUES=1`, and the 22 are recorded beside `FontWeight`'s four.
