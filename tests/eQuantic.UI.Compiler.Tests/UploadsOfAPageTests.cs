using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An app's uploads as eqc builds them (#1214): a policy declared in a static class the page and the
/// server both read, and a page whose Server Action receives the file and reads it. The policy's module
/// constructs the vocabulary's twins, the action's body stays on the server, and client code that
/// reaches for the content is refused at the build.
/// </summary>
public sealed class UploadsOfAPageTests
{
    private const string Policies = """
        using eQuantic.UI.Primitives;

        namespace App.Model;

        public static class Uploads
        {
            public static readonly UploadPolicy Models = new("models",
                filters: [new FileFilter("3D models", "stl", "obj", "3mf")],
                maxSize: 250 * 1024 * 1024);
        }
        """;

    private static string Page(string shown = "Uploads.Models.Name", string action = "") => $$"""
        using eQuantic.UI.Primitives;
        using App.Model;

        namespace App.Screens;

        public sealed class Upload : StatelessComponent
        {
            [ServerAction]
            public async Task<long> SaveModel(UploadedFile model)
            {
                await using var content = await model.OpenRead();
                var buffer = new byte[8192];
                long length = 0;
                int read;
                while ((read = await content.ReadAsync(buffer)) > 0) length += read;
                await model.Delete();
                return length;
            }
        {{action}}
            public override VisualNode Build(ComponentContext context) =>
                Text({{shown}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private static Solution App(params (string Path, string Text)[] files) =>
        new Solution().App("App", globalUsings: SharedLibrary.Factories, files: files);

    [Fact]
    public void APolicyAndAnActionThatReceivesTheFile_Build_AndOnlyTheStubCrosses()
    {
        using var solution = App(("Model/Uploads.cs", Policies), ("Screens/Upload.cs", Page()));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Uploads").Should().NotBeNull()
            .And.Contain("new UploadPolicy(").And.Contain("new FileFilter(")
            .And.Contain("262144000n", "the largest size is a long, which the browser holds as a BigInt");
        built.Module("Upload").Should().NotBeNull()
            .And.Contain("Upload/SaveModel", "the page calls the action through its stub")
            .And.NotContainAny(["openRead", "readAsync", "delete("], "the action's body stays on the server");
    }

    [Theory]
    [InlineData("new UploadedFile(\"id\", \"a.stl\", \"model/stl\", 1, \"models\").OpenRead().IsCompleted.ToString()")]
    [InlineData("new UploadedFile(\"id\", \"a.stl\", \"model/stl\", 1, \"models\").Delete().IsCompleted.ToString()")]
    public void ClientCodeThatReachesForTheContent_IsRefused(string shown)
    {
        using var solution = App(("Model/Uploads.cs", Policies), ("Screens/Upload.cs", Page(shown)));
        var built = solution.Build();

        built.Failed.Should().BeTrue("the content is the server's");
        built.Errors.Should().Contain("EQ2010");
    }
}
