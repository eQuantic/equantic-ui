- **2026-11-10 · The Wallet draws alike on every target**: recorded on the web, iOS, Android and
  macOS, the same screens drew three ways. On the web a `SafeArea` hugged its content where Photon
  gives it its child's size, so the cards page stood at 481px of 1100 and the rail stopped at its
  last destination; it now takes the fill and the cap its child lowered to, through an adaptive
  node's arms, in the C# lowering and its TypeScript twin
  ([#1342](https://github.com/eQuantic/equantic-ui/issues/1342)). On Photon a `Stack` measured its
  positioned children before it had a size, so a `SwipeableRow`'s action was a 40dp strip on a
  78dp row; positioned children are now measured against the size the flow gave the stack
  ([#1343](https://github.com/eQuantic/equantic-ui/issues/1343)). The three readers that decide
  whether a node fills now read the vocabulary's layout transparency instead of lists kept by
  hand, and a test fails when the browser's list leaves a wrapper out. Proving it on Android found
  #1344: two layers sharing an edge on a fractional pixel bleed through each other there.
