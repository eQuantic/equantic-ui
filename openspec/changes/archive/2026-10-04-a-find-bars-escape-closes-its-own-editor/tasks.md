# Tasks

## 1. A shortcut that is not enabled binds nothing

- [x] 1.1 Add `Shortcut.Enabled` to the vocabulary and its TypeScript twin, and bind nothing for one that is not enabled in the web's SSR lowering, its TypeScript twin and Photon's emit visitor. Verify: `S8ShortcutRealizerTests.AShortcutThatIsNotEnabled_MarksNothing`, the runtime's "S8 shortcuts that are not enabled" and `S8ShortcutNativeTests.ABindingThatIsNotEnabled_LeavesTheKeyToTheOneAroundIt`, each failing with its realizer's guard removed

## 2. The find bar's Escape is its editor's own

- [x] 2.1 Wrap the editor's layers in an Escape beside ⌘F, focus-scoped and enabled only while the bar is open, and regenerate the transpiled pin. Verify: `CodeEditorFinishTests.Escape_ClosesTheFindBarOfTheEditorTheKeyboardIsIn` on Photon and the runtime's "binds Escape only while its bar is open, for the editor the keyboard is in" on the web, both failing against main's editor; `Escape_ReachesADialogAroundAnEditorWhoseBarIsClosed` failing against an Escape that is always enabled
- [x] 2.2 Document `Enabled` and the find bar's Escape on the wiki's Components page, in English and Portuguese
