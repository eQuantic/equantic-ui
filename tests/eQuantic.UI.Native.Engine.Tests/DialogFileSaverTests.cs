using System.Text;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <see cref="IFileSaver"/> on a desktop (#1022): the save panel the shell already opens, then a write.
/// The proof READS the file back, because a save that returned <c>Saved</c> and wrote nothing is the
/// failure worth catching.
/// </summary>
public sealed class DialogFileSaverTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("eq-file-saver-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Stands in for the person at the save panel: chooses <see cref="Choice"/>, or cancels
    /// when it is null, and remembers what the panel was opened on.</summary>
    private sealed class Panel : IFileDialogs
    {
        public string? Choice { get; init; }
        public string? SuggestedName { get; private set; }
        public IReadOnlyList<FileFilter>? Filters { get; private set; }

        public Task<string?> PickSavePath(string? suggestedName = null, string? title = null,
            IReadOnlyList<FileFilter>? filters = null, string? startIn = null)
        {
            SuggestedName = suggestedName;
            Filters = filters;
            return Task.FromResult(Choice);
        }

        public Task<string?> PickFile(string? title = null, IReadOnlyList<FileFilter>? filters = null,
            string? startIn = null) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> PickFiles(string? title = null,
            IReadOnlyList<FileFilter>? filters = null, string? startIn = null) => throw new NotSupportedException();

        public Task<string?> PickFolder(string? title = null, string? startIn = null) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task SavingText_WritesItAsUtf8_WhereThePersonChose()
    {
        var chosen = Path.Combine(_folder, "kept.txt");
        var panel = new Panel { Choice = chosen };

        var outcome = await new DialogFileSaver(panel).SaveTextAsync("recovery-codes.txt", "ABCD-1234\nÉ€");

        outcome.Should().Be(FileSaveOutcome.Saved);
        (await File.ReadAllBytesAsync(chosen)).Should().Equal(Encoding.UTF8.GetBytes("ABCD-1234\nÉ€"),
            "UTF-8, with no byte-order mark a person's other tools would trip on");
        panel.SuggestedName.Should().Be("recovery-codes.txt");
        panel.Filters.Should().ContainSingle().Which.Should().BeEquivalentTo(new FileFilter("TXT", "txt"));
    }

    [Fact]
    public async Task SavingBytes_WritesThemAsTheyAre()
    {
        var chosen = Path.Combine(_folder, "scan.bin");

        var outcome = await new DialogFileSaver(new Panel { Choice = chosen }).SaveBytesAsync("scan", [0, 1, 254, 255]);

        outcome.Should().Be(FileSaveOutcome.Saved);
        (await File.ReadAllBytesAsync(chosen)).Should().Equal([0, 1, 254, 255]);
    }

    [Fact]
    public async Task ANameWithNoExtension_OffersAnyKind()
    {
        var panel = new Panel();

        await new DialogFileSaver(panel).SaveTextAsync("README", "x");

        panel.Filters.Should().BeNull();
    }

    [Fact]
    public async Task APanelClosedWithoutChoosing_WritesNothing()
    {
        var outcome = await new DialogFileSaver(new Panel()).SaveTextAsync("codes.txt", "x");

        outcome.Should().Be(FileSaveOutcome.Cancelled);
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
    }

    /// <summary>The Mac offers the saver over the panel it registers, and one an app registered
    /// first is the panel it uses. The Windows shell registers it the same way, which
    /// <c>WikiShellRealizationsTests</c> reads from its source on every host.</summary>
    [SkippableFact]
    public async Task TheMac_OffersTheSaver_OverItsSavePanel()
    {
        Skip.IfNot(OperatingSystem.IsMacOS(), "The macOS shell's capabilities register on a Mac.");
        var chosen = Path.Combine(_folder, "kept.txt");
        var services = new ServiceCollection();
        services.AddSingleton<IFileDialogs>(new Panel { Choice = chosen });

        // The guard the platform analyzer reads; Skip above already ended the test elsewhere.
        if (OperatingSystem.IsMacOS()) new eQuantic.UI.Native.Shell.MacOS.MacOSCapabilities().Register(services);
        var saver = services.BuildServiceProvider().GetRequiredService<IFileSaver>();

        saver.IsAvailable.Should().BeTrue();
        (await saver.SaveTextAsync("codes.txt", "ABCD")).Should().Be(FileSaveOutcome.Saved);
        (await File.ReadAllTextAsync(chosen)).Should().Be("ABCD");
    }
}
