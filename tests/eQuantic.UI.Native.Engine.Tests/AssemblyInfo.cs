using Xunit;

// The culture is a PROCESS-wide setting, and two tests here move it on purpose: a Photon window
// switching languages sets `CultureInfo.DefaultThreadCurrentUICulture` so every thread it starts
// inherits the choice, and asserting that IS the test. Meanwhile xunit runs other classes in
// parallel, and any of them that renders a localized string — `SdkStrings.NothingSelected` in the
// list-detail screen, for one — can be rendered under one culture and asserted under another.
//
// That is a genuine race and it failed exactly once in a full matrix, then passed twice in a row:
// the shape of flake that gets re-run rather than read, and reddens a release run eventually. This
// assembly runs its tests one at a time instead. It costs about a second — the whole suite is
// ~1000 tests in two — and buys back an answer that means the same thing every time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
