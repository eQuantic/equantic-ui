# Tasks

## 1. The slots

- [x] 1.1 `SizeLadder`, `DensityLadder`, `ControlsTheme` and `ButtonTheme`, the two slots on `IAppTheme`, and `AppThemeExtensions.With`. Verify: `With_DeclaresEveryMemberOfTheTheme`, `With_ReplacesTheSlotItIsGiven_AndForwardsTheRest`
- [x] 1.2 `ControlMetrics` and every control that read the ladder, the Button's own members. Verify: the medium height on the web and on Photon, and the list of controls held to the sources
- [x] 1.3 `PhotonTheme` and `MaterialTheme` fill their slots. Verify: `PhotonsControlsSlot_IsTheLadder`, and the pins byte for byte

## 2. The browser

- [x] 2.1 `ThemeBridge` writes the slots and the code palette, the runtime materializes them, its mirrors, defaults and generated Photon theme carry them. Verify: `EveryMemberOfTheTheme_CrossesToTheBrowser`, the pinned JSON, and the vitest round trip

## 3. Documentation

- [x] 3.1 The wiki's DesignSystem page in five languages
- [x] 3.2 One entry for the ledger citing #1241 and #936, as a file of its own under `docs/ledger/`
