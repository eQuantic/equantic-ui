using Xunit;

// Photon has one UI thread a process, as an app does, so the tests that measure on it run one at a
// time: the rule PhotonTester states, and the line an app's test project writes for it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
