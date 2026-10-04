# Tasks

## 1. A shortcut that is not enabled binds nothing

- [x] 1.1 Add `Shortcut.Enabled` to the vocabulary and its TypeScript twin, and bind nothing for one that is not enabled in the web's SSR lowering, its TypeScript twin and Photon's emit visitor. Verify: `S8ShortcutRealizerTests.AShortcutThatIsNotEnabled_MarksNothing`, the runtime's "S8 shortcuts that are not enabled" and `S8ShortcutNativeTests.ABindingThatIsNotEnabled_LeavesTheKeyToTheOneAroundIt`, each failing with its realizer's guard removed

- [x] 1.2 List a web binding before the ones inside it, as Photon's frame does. Verify: the runtime's "lets the inner of two nested bindings of one chord answer, as Photon does" fails with the binding declared after its child, and `S8ShortcutNativeTests.OfTwoNestedBindings_TheInnerOneAnswers` pins Photon's side

## 2. The find bar's Escape is its editor's own

- [x] 2.1 Wrap the editor's layers in an Escape beside ⌘F, focus-scoped and enabled only while the bar is open, and regenerate the transpiled pin. Verify: `CodeEditorFinishTests.Escape_ClosesTheFindBarOfTheEditorTheKeyboardIsIn` on Photon and the runtime's "closes the bar of the editor the keyboard is in, before a dialog around it" on the web, both failing against main's editor, the web's also with the binding declared after its child; `Escape_ReachesADialogAroundAnEditorWhoseBarIsClosed` failing against an Escape that is always enabled, and `Escape_ClosesTheBarBeforeADialogAroundTheEditor` on Photon
- [x] 2.2 Document `Enabled` and the find bar's Escape on the wiki's Components page, in English and Portuguese
