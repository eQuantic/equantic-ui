using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The Reference renders of the <see cref="GoldenScenes"/> catalog matched against the repo goldens —
/// the normative output every backend is measured against. <see cref="MetalParityTests"/> runs the
/// same catalog on the GPU.
/// </summary>
public class GoldenSceneTests
{
    [Theory]
    [MemberData(nameof(Scenes))]
    public void Scene(string name)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(GoldenScenes.Width, GoldenScenes.Height);
        backend.Render(GoldenScenes.Build(name), surface);
        GoldenImage.Match(surface, name);
    }

    public static TheoryData<string> Scenes => GoldenScenes.Names;
}
