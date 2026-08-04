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
