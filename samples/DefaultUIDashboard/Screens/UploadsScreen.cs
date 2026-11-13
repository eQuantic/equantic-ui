using System.Security.Cryptography;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// Files sent to the server in parts (#1215): the <c>FileUpload</c> component does the choosing,
/// the sending and the progress, and hands each file that arrived to a Server Action, which reads it as a
/// stream and answers how many bytes it read and their SHA-256, the proof that what arrived is what was
/// sent.
/// </summary>
[Page("/uploads", Title = "Uploads — eQuantic Console")]
public sealed class UploadsScreen : StatefulComponent
{
    private readonly List<string> _received = new();

    /// <summary>Reads the file the person uploaded, a part at a time, and removes it from the staging
    /// folder: an app would store it where it keeps its data first.</summary>
    [ServerAction]
    public async Task<string> Receive(UploadedFile file)
    {
        await using var content = await file.OpenRead();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await content.ReadAsync(buffer)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            length += read;
        }

        await file.Delete();
        return $"{file.Name}: {length} bytes, SHA-256 {Convert.ToHexStringLower(hash.GetHashAndReset())}";
    }

    private async Task Received(UploadedFile file)
    {
        string said;
        try
        {
            said = await Receive(file);
        }
        catch (Exception)
        {
            said = $"{file.Name}: the server could not read it.";
        }

        SetState(() => _received.Add(said));
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var column = Column(gap: Space.S4, children: [
            Text("Uploads", TypeRole.Heading, theme.TextPrimary),
            Text("Drop 3D models or notes, or choose them. Each one travels in parts the server takes in "
                + "turn, and a Server Action reads what arrived.", TypeRole.BodyM, theme.TextSecondary),
            FileUpload(SampleUploads.Models, onCompleted: file => _ = Received(file)),
        ]);
        foreach (var said in _received) column.Add(Text(said, TypeRole.BodyM, theme.TextPrimary));
        return Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = theme.Background,
                Padding = EdgeInsets.All(Space.S6),
            },
            column);
    }
}
