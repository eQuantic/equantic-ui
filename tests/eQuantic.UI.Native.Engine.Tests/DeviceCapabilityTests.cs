using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A capability is an INTERFACE, which is the whole point: a page that scans a document is testable
/// by handing it a library that returns a fixture — no simulator, no camera roll, no waiting for a
/// human to tap something. This pins that the contract is usable that way, and that the states a
/// permission can be in are all reachable.
/// </summary>
public class DeviceCapabilityTests
{
    /// <summary>The kind of stand-in an app's own tests would write.</summary>
    private sealed class FakeLibrary(params ImageData[] answers) : IPhotoLibrary
    {
        public int Asked { get; private set; }

        public bool IsAvailable => true;

        public ValueTask<PermissionState> GetPermissionAsync(CancellationToken cancellationToken = default) =>
            new(PermissionState.Granted);

        public ValueTask<ImageData?> PickImageAsync(CancellationToken cancellationToken = default)
        {
            Asked++;
            return new ValueTask<ImageData?>(answers.Length > 0 ? answers[0] : null);
        }

        public ValueTask<IReadOnlyList<ImageData>> PickImagesAsync(int limit = 0,
            CancellationToken cancellationToken = default)
        {
            Asked++;
            IReadOnlyList<ImageData> picked = limit > 0 ? answers.Take(limit).ToArray() : answers;
            return new ValueTask<IReadOnlyList<ImageData>>(picked);
        }
    }

    /// <summary>A page as an app would write one: the capability arrives through the constructor.</summary>
    private sealed class PickerPage(IPhotoLibrary library)
    {
        public ImageData? Picked { get; private set; }

        public async Task ChooseAsync() => Picked = await library.PickImageAsync();
    }

    [Fact]
    public async Task APageTakesTheCapability_AndIsTestableWithoutADevice()
    {
        var fixture = new ImageData([1, 2, 3], "image/png", 4, 4, "fixture.png");
        var library = new FakeLibrary(fixture);
        var page = new PickerPage(library);

        await page.ChooseAsync();

        page.Picked.Should().BeSameAs(fixture);
        library.Asked.Should().Be(1);
    }

    [Fact]
    public async Task ChoosingNothing_IsNull_NotAnException()
    {
        // Cancelling and refusing look the same to an app, and both are ordinary — a picker the
        // user closes is not an error path.
        var page = new PickerPage(new FakeLibrary());
        await page.ChooseAsync();
        page.Picked.Should().BeNull();
    }

    [Fact]
    public async Task ALimitIsHonoured()
    {
        var library = new FakeLibrary(
            new ImageData([1], "image/png"), new ImageData([2], "image/png"), new ImageData([3], "image/png"));

        (await library.PickImagesAsync(2)).Should().HaveCount(2);
        (await library.PickImagesAsync()).Should().HaveCount(3);
    }

    [Fact]
    public void APermissionIsThreeWay_NotABoolean()
    {
        // The reason the enum exists: a first ask and a permanent refusal are different situations,
        // and `if (!granted)` reads them the same — so an app either nags or gives up in silence.
        PermissionState.NotDetermined.Should().NotBe(PermissionState.Denied);
        Enum.GetValues<PermissionState>().Should().Contain(
            [PermissionState.Granted, PermissionState.Limited, PermissionState.Unavailable]);
    }

    [Fact]
    public void ADeclarationCarriesItsREASON()
    {
        // Not boilerplate: this sentence is what the user reads at the moment they decide.
        var declared = new PhotonCapabilityAttribute(DeviceCapability.Camera, "Reads the code on your card.");
        declared.Capability.Should().Be(DeviceCapability.Camera);
        declared.Reason.Should().NotBeNullOrWhiteSpace();
    }
}

/// <summary>
/// The declaration an app writes fluently, beside everything else it configures. The generator
/// reads these calls at COMPILE time — a permission is written into a manifest the OS reads at
/// install time, long before any of this runs — and that is the whole reason the reason has to be
/// a constant.
/// </summary>
public class CapabilityDeclarationTests
{
    [Fact]
    public void ItReadsLikeEverythingElseOnTheBuilder()
    {
        var capabilities = new eQuantic.UI.Native.Hosting.PhotonCapabilitiesBuilder();

        capabilities
            .UsePhotoLibrary("Attach a receipt to a payment.")
            .UseBiometrics("Confirm a transfer with Face ID.");

        capabilities.Declared.Should().HaveCount(2);
        capabilities.Declared[DeviceCapability.PhotoLibrary].Should().Be("Attach a receipt to a payment.");
    }

    [Fact]
    public void DeclaringTheSameThingTwice_KeepsTheLastWord()
    {
        var capabilities = new eQuantic.UI.Native.Hosting.PhotonCapabilitiesBuilder();
        capabilities.UseCamera("first").UseCamera("second");

        capabilities.Declared.Should().HaveCount(1);
        capabilities.Declared[DeviceCapability.Camera].Should().Be("second");
    }

    [Fact]
    public void AnEmptyReason_IsRefusedWhereItIsWritten()
    {
        // Not pedantry: a platform that receives an empty usage string rejects the app, and finding
        // that out at submission is a bad afternoon.
        var capabilities = new eQuantic.UI.Native.Hosting.PhotonCapabilitiesBuilder();

        var refuse = () => capabilities.UseCamera("   ");
        refuse.Should().Throw<ArgumentException>().WithMessage("*shown to the user*");
    }

    [Fact]
    public void TheGeneralFormReachesWhatTheNamedOnesDoNot()
    {
        var capabilities = new eQuantic.UI.Native.Hosting.PhotonCapabilitiesBuilder();
        capabilities.Use(DeviceCapability.Contacts, "Find the people you already pay.");

        capabilities.Declared.Should().ContainKey(DeviceCapability.Contacts);
    }
}
