using eQuantic.UI.Primitives;
using eQuantic.UI.Native.Shell.iOS;
using eQuantic.Wallet;

// eQUANTIC WALLET, on the device it was drawn for. The app is the SAME two files the desktop head
// compiles — the tree, the data and every screen — and this is the whole difference between them.
// The safe-area insets are no longer guessed at 390×844: the phone reports its own notch and home
// indicator, and the SafeArea nodes lay out against those.
PhotonApp.Run(args, () => new WalletApp(), PhotonTheme.Instance);
