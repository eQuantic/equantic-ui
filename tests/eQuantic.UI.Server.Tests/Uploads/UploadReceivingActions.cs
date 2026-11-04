using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>A page's Server Actions that receive an uploaded file, as an app's would.</summary>
public sealed class UploadReceivingActions(ReceivedLog log) : StatelessComponent
{
    /// <summary>Reads the file whole and answers what the action saw: its members and its bytes.</summary>
    [ServerAction]
    public async Task<string> Receive(UploadedFile model)
    {
        Interlocked.Increment(ref log.Runs);
        await using var content = await model.OpenRead();
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        return $"{model.Id}|{model.Name}|{model.MimeType}|{model.Size}|{model.Policy}|{Convert.ToBase64String(copy.ToArray())}";
    }

    /// <summary>Takes the file and removes it from the staging folder, as an app does once it stored it.</summary>
    [ServerAction]
    public async Task<string> Keep(UploadedFile model)
    {
        Interlocked.Increment(ref log.Runs);
        await using (var content = await model.OpenRead())
        {
            await content.CopyToAsync(Stream.Null);
        }

        await model.Delete();
        return model.Name;
    }

    public override VisualNode Build(ComponentContext context) => new Column(gap: 0);
}
