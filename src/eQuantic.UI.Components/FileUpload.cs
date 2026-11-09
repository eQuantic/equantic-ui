using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The whole of a file upload in one component: an area that takes the files dropped on it, a button
/// that asks the person for them, and a row for each file with its progress, its state and its controls,
/// sending each one to the app's server under <see cref="Policy"/>. <see cref="OnCompleted"/> receives
/// every file that arrived, as the <see cref="UploadedFile"/> a Server Action takes.
/// <code>
/// FileUpload(Uploads.Models, onCompleted: model =&gt; _ = SaveModel(model))
/// </code>
/// <para>
/// The button is the way in for a keyboard and a screen reader, since dropping is a pointer's gesture,
/// and what arrives or fails is announced. A file the policy does not accept is refused in its row,
/// before a byte is sent, with the reason the server would give.
/// </para>
/// <para>
/// It asks its context for <see cref="IFilePicker"/> and <see cref="IUploads"/>. Where either is
/// missing or unavailable (server rendering, a native shell with no transport yet) it draws the same
/// area, disabled, and the browser enables it when the page runs there. An upload whose area leaves the
/// screen pauses; choosing the same file again goes on from what the server holds.
/// </para>
/// </summary>
public sealed class FileUpload : StatefulComponent
{
    /// <summary>The configuration of the latest build, kept whole: this instance is retained across the
    /// parent's rebuilds, and every setting is read from the fresh instance a parent hands.</summary>
    private FileUpload _config;

    private readonly List<Upload> _uploads = new();
    private readonly List<IDisposable> _subscriptions = new();

    /// <summary>The uploads whose completion was already handed to <see cref="OnCompleted"/>.</summary>
    private readonly List<Upload> _reported = new();

    private bool _over;
    private string _announcement = "";

    /// <summary>Whether this instance is in a live tree, where a press and a drop can be answered.</summary>
    private bool _live;

    /// <summary>An upload area.</summary>
    /// <param name="policy">What the files are sent under: one of the policies the app's server accepts.
    /// The picker offers its formats, and the area refuses what it does not accept.</param>
    /// <param name="onCompleted">Receives each file once it arrived whole.</param>
    /// <param name="multiple">Whether a person may send several files at once.</param>
    /// <param name="label">The area's invitation, in place of the SDK's own.</param>
    /// <param name="hint">What the area says it accepts, in place of the formats and largest size the
    /// policy states.</param>
    public FileUpload(UploadPolicy policy, Action<UploadedFile> onCompleted, bool multiple = true,
        string? label = null, string? hint = null)
    {
        Policy = policy;
        OnCompleted = onCompleted;
        Multiple = multiple;
        Label = label;
        Hint = hint;
        _config = this;
    }

    /// <summary>What the files are sent under.</summary>
    public UploadPolicy Policy { get; init; }

    /// <summary>Receives each file once it arrived whole.</summary>
    public Action<UploadedFile> OnCompleted { get; init; }

    /// <summary>Whether a person may send several files at once.</summary>
    public bool Multiple { get; init; }

    /// <summary>The area's invitation; null for the SDK's own.</summary>
    public string? Label { get; init; }

    /// <summary>What the area says it accepts; null for the policy's formats and largest size.</summary>
    public string? Hint { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is FileUpload fresh) _config = fresh;
    }

    /// <summary>
    /// The area is drawn able to send only from here. Its first build is the same as the server's, which
    /// had nothing to send with, because hydration adopts the server's markup as it finds it rather than
    /// drawing the first build over it: an area enabled in that build would stay disabled on screen.
    /// </summary>
    protected override void OnMount() => SetState(() => _live = true);

    protected override void OnUnmount()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
        // Nobody would see it go on, and choosing the same file again resumes it from the server.
        foreach (var upload in _uploads) upload.Pause();
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var config = _config;
        var picker = context.GetService<IFilePicker>();
        var uploads = context.GetService<IUploads>();
        var enabled = _live && picker is { IsAvailable: true } && uploads is { IsAvailable: true };

        var invitation = new Column(gap: Space.S2, cross: CrossAlign.Center);
        invitation.Add(new Text(config.Label ?? (config.Multiple ? SdkStrings.UploadDropHere : SdkStrings.UploadDropOneHere),
            TypeRole.Label, theme.TextPrimary, maxLines: 2, align: TextAlignment.Center));
        invitation.Add(new Text(config.Hint ?? Accepted(config.Policy), TypeRole.Caption, theme.TextSecondary,
            maxLines: 2, align: TextAlignment.Center));
        invitation.Add(new Button(config.Multiple ? SdkStrings.UploadChooseFiles : SdkStrings.UploadChooseFile,
            Variant.Secondary, SizeVariant.Small, () => _ = Choose(picker, uploads))
        {
            Disabled = !enabled,
        });

        // The invitation answers a drag carried over it, as a hover is answered.
        var area = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S6),
            Background = _over && enabled ? theme.Colors(Variant.Primary).Subtle : theme.SurfaceSubtle,
            BorderWidth = _over && enabled ? 2 : 1,
            BorderColor = _over && enabled ? theme.Colors(Variant.Primary).Base : theme.BorderStrong,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Large)),
            Opacity = enabled ? null : 0.6f,
        }, invitation);

        var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        column.Add(new DropTarget(area, files => Send(uploads, files))
        {
            OnDragOverChanged = over => SetState(() => _over = over),
            Enabled = enabled,
        });
        foreach (var upload in _uploads) column.Add(RowOf(upload, theme));
        column.Add(new LiveRegion(new Text(_announcement, TypeRole.Caption, theme.TextSecondary, maxLines: 3)));
        return column;
    }

    /// <summary>One file: its name and state, its progress, and what can be done with it now.</summary>
    private VisualNode RowOf(Upload upload, IAppTheme theme)
    {
        var state = upload.State;
        var failed = state == UploadState.Failed;
        var heading = new Row(gap: Space.S2) { Width = SizeValue.Fill };
        heading.Add(new Flexible(new Text(upload.File.Name, TypeRole.Label, theme.TextPrimary, maxLines: 1)));
        heading.Add(new Text(StatusOf(upload), TypeRole.Caption,
            failed ? theme.Colors(Variant.Destructive).Base : theme.TextSecondary, maxLines: 1));

        var row = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        row.Add(heading);
        row.Add(new ProgressBar(Fraction(upload), failed ? Variant.Destructive
            : state == UploadState.Completed ? Variant.Success : Variant.Primary)
        {
            Label = SdkStrings.UploadProgress(upload.File.Name),
        });
        if (failed && upload.Error is { Length: > 0 } error)
            row.Add(new Text(error, TypeRole.Caption, theme.Colors(Variant.Destructive).Base, maxLines: 3));

        var actions = new Row(gap: Space.S2);
        if (state == UploadState.Waiting || state == UploadState.Sending)
            actions.Add(new Button(SdkStrings.UploadPause, Variant.Ghost, SizeVariant.Small, upload.Pause));
        if (state == UploadState.Paused)
            actions.Add(new Button(SdkStrings.UploadResume, Variant.Ghost, SizeVariant.Small, upload.Resume));
        if (state == UploadState.Waiting || state == UploadState.Sending || state == UploadState.Paused)
            actions.Add(new Button(SdkStrings.UploadCancel, Variant.Ghost, SizeVariant.Small, upload.Cancel));
        row.Add(actions);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
        }, row);
    }

    /// <summary>Asks the person for files, from the press itself: a browser opens its picker only there.</summary>
    private async Task Choose(IFilePicker? picker, IUploads? uploads)
    {
        if (picker is null) return;
        var filters = _config.Policy.Filters;
        if (_config.Multiple)
        {
            Send(uploads, await picker.PickFilesAsync(filters));
            return;
        }

        var file = await picker.PickFileAsync(filters);
        if (file is not null) Send(uploads, [file]);
    }

    /// <summary>Starts a row and an upload for each file: one, unless the area takes several.</summary>
    private void Send(IUploads? uploads, IReadOnlyList<PickedFile> files)
    {
        if (uploads is null || files.Count == 0) return;
        var config = _config;
        var count = config.Multiple ? files.Count : 1;
        SetState(() =>
        {
            for (var i = 0; i < count; i++)
            {
                var upload = uploads.Start(files[i], config.Policy);
                _uploads.Add(upload);
                _subscriptions.Add(upload.Subscribe(Changed));
                // Refused before anything was sent: it changes no more, so it is announced now.
                if (upload.State == UploadState.Failed) _announcement = Announcement(upload);
            }
        });
    }

    private void Changed(Upload upload)
    {
        var arrived = upload.State == UploadState.Completed && !_reported.Contains(upload);
        SetState(() =>
        {
            if (arrived)
            {
                _reported.Add(upload);
                _announcement = Announcement(upload);
            }
            else if (upload.State == UploadState.Failed)
            {
                _announcement = Announcement(upload);
            }
        });
        // Handed over after this area's own change, so the app's answer to it builds on top of it.
        if (arrived && upload.Result is { } result) _config.OnCompleted(result);
    }

    private static string Announcement(Upload upload) =>
        upload.State == UploadState.Completed
            ? SdkStrings.UploadAnnounceCompleted(upload.File.Name)
            : SdkStrings.UploadAnnounceFailed(upload.File.Name, upload.Error ?? "");

    private static string StatusOf(Upload upload) =>
        upload.State switch
        {
            UploadState.Waiting => SdkStrings.UploadWaiting,
            UploadState.Sending => SdkStrings.UploadSending(SizeText(upload.Sent), SizeText(upload.Size)),
            UploadState.Paused => SdkStrings.UploadPaused,
            UploadState.Completed => SdkStrings.UploadCompleted,
            UploadState.Cancelled => SdkStrings.UploadCancelled,
            _ => "",
        };

    /// <summary>How much of the file the server holds, 0 to 1. An empty file is whole as soon as it starts.</summary>
    private static float Fraction(Upload upload) =>
        upload.Size == 0 ? 1f : (float)((double)upload.Sent / upload.Size);

    /// <summary>What a policy accepts, as the area says it: "STL, OBJ, up to 250 MB".</summary>
    private static string Accepted(UploadPolicy policy)
    {
        var formats = new List<string>();
        foreach (var filter in policy.Filters)
        {
            foreach (var extension in filter.Extensions)
            {
                var format = extension.TrimStart('.').ToUpperInvariant();
                if (format.Length > 0 && !formats.Contains(format)) formats.Add(format);
            }
        }

        var size = SizeText(policy.MaxSize);
        return formats.Count == 0 ? SdkStrings.UploadAcceptsAny(size) : SdkStrings.UploadAccepts(string.Join(", ", formats), size);
    }

    /// <summary>A size as a person reads it, in the page's culture: "512 B", "3.2 MB".</summary>
    private static string SizeText(long bytes)
    {
        if (bytes < 1024) return bytes.ToString(CultureInfo.CurrentCulture) + " B";
        var value = bytes / 1024.0;
        var unit = "KB";
        if (value >= 1024)
        {
            value /= 1024;
            unit = "MB";
        }

        if (value >= 1024)
        {
            value /= 1024;
            unit = "GB";
        }

        return value.ToString("0.#", CultureInfo.CurrentCulture) + " " + unit;
    }
}
