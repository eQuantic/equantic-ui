# Tasks

## 1. The slots

- [x] 1.1 `NavigationTheme`, `ListTheme`, `TableTheme`, `BadgeTheme`, `CalendarTheme` and `MarkdownTheme`, the six slots on `IAppTheme`, forwarded by `OverriddenTheme` and taken by `With`. Verify: `With_DeclaresEveryMemberOfTheTheme`, `EveryMemberOfEverySlot_CrossesUnderItsName`
- [x] 1.2 The twenty components read their slot, and `Markdown.Style` comes ahead of the theme's. Verify: `AValueInAFamilysSlot_ReachesTheWebsLowering`, `AValueInAFamilysSlot_ReachesPhotonsLayout`, `AMarkdownsOwnStyle_ComesAheadOfTheSlot_MemberByMember`
- [x] 1.3 `PhotonTheme` and `MaterialTheme` fill their slots. Verify: the pins byte for byte under Photon, and Material 3's sizes on both targets

## 2. The browser

- [x] 2.1 The runtime's mirrors, builders, defaults, exports and the generated Photon theme carry the six slots. Verify: `EveryMemberOfTheTheme_CrossesToTheBrowser`, the pinned JSON, and the vitest round trip

## 3. Documentation

- [x] 3.1 The wiki's DesignSystem and Markdown pages in five languages, and the Upgrading page in two
- [x] 3.2 One entry for the ledger citing #1244, as a file of its own under `docs/ledger/`
