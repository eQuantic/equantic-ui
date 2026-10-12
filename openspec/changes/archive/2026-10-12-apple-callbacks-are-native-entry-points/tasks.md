# Tasks

## 1. The block

- [x] 1.1 `ObjCBlock(IntPtr invoke, object context)` stores a `GCHandle` after the descriptor and `ObjCBlock.Context<T>` reads it back; `ObjCBlock(Delegate)` goes. Verify: `ObjCBlockTests` on macOS: the runtime calls the entry point per item with its arguments, two blocks keep their own contexts, and a copied and released block still answers
- [x] 1.2 The network status, motion sensor, biometric reply and the camera's access request call static `[UnmanagedCallersOnly]` entry points; the two replies let only their context go (`PendingReply<T>`, `ObjCBlock.ReleaseContext`), and the motion sensor reuses its block. Verify: `ObjCBlockTests.AReplyLetsItsContextGo_AndTheFrameworksReleaseStillFindsTheBlock`, and `NetworkStatusTests`, `MotionSensorTests` on macOS, where the network monitor's first report arrives through the new entry point

## 2. Registered methods

- [x] 2.1 The camera's frame delegate, location's three delegate methods and the deep-link handler are static entry points of an `ObjCDelegateClass`, each finding its object in the delegate's ivar. Verify: `AppleCallbackTests.EachDelegateObjectAnswersForItsOwnContext`, two instances of one runtime class called through `objc_msgSend`; `AnAppleEvent_ReachesTheRelay_ThroughTheRegisteredEntryPoint`, an AppleEvent sent to the installed handler; and `LocationTests`

## 3. The guard

- [x] 3.1 `AppleCallbackTests.NoDelegateBecomesAFunctionPointer` reads the shared Apple shell and the iOS shell, and `NoPInvokeTakesADelegate` reads the compiled shared shell for a P/Invoke that marshals a delegate. Verify: the first fails against main's Apple shell and passes here

## 4. The phones

- [x] 4.1 The iOS shell and the Wallet build for the simulator with the new Apple shell, and the Wallet presents its self-test frames on a fresh iOS 26.5 simulator. The run on a physical iPhone that exercises the five capabilities is the device pass's, #792

## 5. Documentation and the suites

- [x] 5.1 `docs/platform-catalog/ios.md`'s camera, location and Face ID rows, and one `docs/LEDGER.md` line citing #795. No wiki page describes the callbacks
- [x] 5.2 The Native.Engine suite, alone and read by its exit code
