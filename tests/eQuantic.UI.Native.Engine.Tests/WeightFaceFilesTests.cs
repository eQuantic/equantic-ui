using System.Runtime.CompilerServices;
using eQuantic.UI.Tests;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The faces the weight tests read (<see cref="FontWeightPhotonTests"/>, <see cref="FontWeightWindowsTests"/>)
/// are committed as files, since a shell reads a face from the assembly it is embedded in, and each is the
/// one <see cref="TestFont"/> builds from the arguments here: a change to the builder, or to a file, fails
/// this until the files are written again with <c>EQ_UPDATE_TEST_FONTS=1</c> and the diff is read.
/// </summary>
public class WeightFaceFilesTests
{
    public static TheoryData<string> Files => new(Faces().Select(face => face.File));

    private static IEnumerable<(string File, byte[] Bytes)> Faces()
    {
        yield return ("WeightFace-300.ttf", TestFont.Build("eQuantic Weight Face", weight: 300, advance: 700));
        yield return ("WeightFace-400.ttf", TestFont.Build("eQuantic Weight Face", weight: 400, advance: 1000));
        yield return ("WeightFace-700.ttf", TestFont.Build("eQuantic Weight Face", weight: 700, advance: 1300));
        yield return ("WeightFace-Variable.ttf",
            TestFont.Build("eQuantic Variable Face", weightAxis: (100, 900), axisAdvances: (500, 1500)));
        // Google's static Thin and ExtraLight of Inter: one weight class, 250, and their own weight in STAT.
        yield return ("ThinFace-100.ttf", TestFont.Build("eQuantic Thin Face", weight: 250, advance: 500, stat: [new(100)]));
        yield return ("ThinFace-200.ttf", TestFont.Build("eQuantic Thin Face", weight: 250, advance: 600, stat: [new(200)]));
    }

    private static string Folder([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "Shared", "Fonts", "Weights"));

    [Theory]
    [MemberData(nameof(Files))]
    public void ACommittedFace_IsTheOneTheBuilderWrites(string file)
    {
        var built = Faces().Single(face => face.File == file).Bytes;
        var path = Path.Combine(Folder(), file);
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_TEST_FONTS") == "1")
        {
            File.WriteAllBytes(path, built);
            return;
        }

        File.ReadAllBytes(path).Should().Equal(built,
            "the committed face is TestFont's; one changed on purpose is written again with EQ_UPDATE_TEST_FONTS=1");
    }
}
