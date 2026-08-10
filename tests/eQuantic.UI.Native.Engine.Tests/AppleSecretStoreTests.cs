using eQuantic.UI.Native.Shell.Apple;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The Keychain realization, exercised against the REAL Security.framework — the only way to find
/// out whether an interop layer works, since every symbol here resolves at runtime and a wrong one
/// fails silently by answering null.
/// </summary>
[Trait("Platform", "macOS")]
public class AppleSecretStoreTests
{
    private static bool OnMac => OperatingSystem.IsMacOS();

    /// <summary>
    /// The load-bearing assumption. The <c>kSec*</c> keys are exported CFString constants read
    /// through dlsym; if a symbol name were wrong, every call would still compile and every read
    /// would answer null — a store that silently forgets everything, which is the worst possible
    /// failure for a credential.
    /// </summary>
    [Fact]
    public void TheKeychainConstants_Resolve()
    {
        if (!OnMac) return;

        var store = new AppleSecretStore();

        // Get on a key nothing wrote must answer null WITHOUT the store having decided it is
        // unavailable — this asserts it got far enough to ask.
        store.Get($"probe-{Guid.NewGuid()}").Should().BeNull();
        typeof(AppleSecretStore)
            .GetProperty("Available", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null).Should().Be(true, "every kSec* symbol has to resolve or the store forgets silently");
    }

    /// <summary>
    /// A real round-trip against the real Keychain — verified to work from an unsigned test host,
    /// which is why this asserts EQUALITY rather than tolerating null. A permissive version would
    /// pass just as happily if the store silently forgot everything, and silently forgetting is the
    /// worst thing a credential store can do: the app looks signed-in until it does not.
    /// </summary>
    [Fact]
    public void AStoredSecret_ReadsBackOrIsRefusedOutright()
    {
        if (!OnMac) return;

        var store = new AppleSecretStore();
        var key = $"eq-test-{Guid.NewGuid()}";

        try
        {
            store.Set(key, "s3cret-value");
            var read = store.Get(key);

            read.Should().Be("s3cret-value",
                "what goes into the Keychain has to come back out byte-identical");
        }
        finally
        {
            store.Remove(key);
        }
    }
}
