using System.Security.Cryptography;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using eQuantic.UI.Tests;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The cache a text engine that opens faces only from files reads them from, Android's (#1055): a face
/// is written once, named by its bytes, and only ever published whole. A cache that cannot take it is a
/// face that did not register, so its family falls back as any other that failed: it threw where text
/// was measured (Copilot's third review of #1062).
/// </summary>
public sealed class ShippedFaceCacheTests : IDisposable
{
    private static readonly FontFile Face = new("Acme.App", "eQuantic.UI.Fonts/Brand.ttf", 400, 400);
    private static readonly byte[] Bytes = TestFont.Build("Brand Sans");
    private readonly string _dir = Directory.CreateTempSubdirectory("eq-face-cache-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void AFace_IsCachedWhole_UnderTheHashOfItsBytes_AndNothingBesideIt()
    {
        var folder = Path.Combine(_dir, "eq-fonts");

        var path = ShippedFaces.CacheFile(folder, Face, Bytes);

        path.Should().Be(Path.Combine(folder, Convert.ToHexStringLower(SHA256.HashData(Bytes))[..16] + ".ttf"));
        File.ReadAllBytes(path!).Should().Equal(Bytes);
        Directory.GetFileSystemEntries(folder).Should().Equal([path], "a face is written beside its name and moved onto it");
    }

    [Fact]
    public void AFaceCachedBefore_IsOpenedWhereItIs()
    {
        var folder = Path.Combine(_dir, "eq-fonts");
        var path = ShippedFaces.CacheFile(folder, Face, Bytes)!;
        var written = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, written.AddDays(-1));

        ShippedFaces.CacheFile(folder, Face, Bytes).Should().Be(path);
        File.GetLastWriteTimeUtc(path).Should().Be(written.AddDays(-1), "the file is not written again");
    }

    [Fact]
    public void ACacheThatCannotTakeAFace_IsAFaceThatDidNotRegister()
    {
        var notAFolder = Path.Combine(_dir, "eq-fonts");
        File.WriteAllText(notAFolder, "a file where the folder would be");

        ShippedFaces.CacheFile(notAFolder, Face, Bytes).Should().BeNull();
    }

    /// <summary>A write that fails leaves nothing under the face's name, and nothing beside it: what a
    /// later launch finds under that name is only ever a face written whole.</summary>
    [Fact]
    public void AWriteThatFails_LeavesNothingUnderTheFacesName()
    {
        var folder = Path.Combine(_dir, "eq-fonts");
        var taken = Path.Combine(folder, Convert.ToHexStringLower(SHA256.HashData(Bytes))[..16] + ".ttf");
        Directory.CreateDirectory(taken);

        ShippedFaces.CacheFile(folder, Face, Bytes).Should().BeNull();
        Directory.GetFileSystemEntries(folder).Should().Equal([taken], "the partial file is removed");
    }
}
