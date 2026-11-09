using System.Net;
using System.Text;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using TusMetadata = tusdotnet.Models.Metadata;

namespace eQuantic.UI.Server;

/// <summary>
/// The upload endpoint's rules, as the tus engine applies them: who may send to it, what a new upload
/// must declare, and whose each upload is. The engine speaks the protocol (the parts, the offset an
/// interrupted upload resumes from, its expiry); this decides.
/// <list type="bullet">
/// <item>A request from another site is refused, by the rule the Server Actions apply
/// (<see cref="AllowedRequestOrigins"/>).</item>
/// <item>A new upload names its policy, its file and its type in its metadata (<c>policy</c>,
/// <c>filename</c>, <c>filetype</c>), states its size, and is refused when the policy does not accept
/// it, when its owner already has the unfinished uploads the app allows, or when the policy requires a
/// signed-in person and the request is a visitor's.</item>
/// <item>Only an upload's owner continues it, asks for its offset or deletes it.</item>
/// </list>
/// Every refusal carries a short reason in English, which the page shows as it is.
/// </summary>
internal sealed class UploadProtocol
{
    /// <summary>Where the request keeps the place its creation holds until the record is written.</summary>
    private static readonly object Reservation = new();

    private readonly UploadStore _store;
    private readonly UploadPolicies _policies;
    private readonly AllowedRequestOrigins _origins;
    private readonly UploadsOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<UploadProtocol> _logger;

    public UploadProtocol(UploadStore store, UploadPolicies policies, AllowedRequestOrigins origins,
        IOptions<UploadsOptions> options, TimeProvider time, ILogger<UploadProtocol> logger)
    {
        _store = store;
        _policies = policies;
        _origins = origins;
        _options = options.Value;
        _time = time;
        _logger = logger;
        Configuration = new DefaultTusConfiguration
        {
            Store = store.Tus,
            // The largest file any policy accepts, which the engine announces and holds every upload to;
            // each policy's own limit is checked when an upload names it.
            MaxAllowedUploadSizeInBytesLong = policies.LargestSize > 0 ? policies.LargestSize : null,
            Expiration = new AbsoluteExpiration(_options.Expiry),
            // An upload states its size when it starts, so its policy can weigh it, and is one file: a
            // concatenation of parts would make a file no creation declared.
            AllowedExtensions = TusExtensions.All.Except(TusExtensions.Concatenation, TusExtensions.CreationDeferLength),
            Events = new Events
            {
                OnAuthorizeAsync = AuthorizeAsync,
                OnBeforeCreateAsync = BeforeCreateAsync,
                OnCreateCompleteAsync = CreateCompleteAsync,
                OnFileCompleteAsync = context => _store.FinishAsync(context.FileId),
                OnDeleteCompleteAsync = context => _store.ForgetAsync(context.FileId),
            },
        };
    }

    /// <summary>The configuration the engine serves every request with.</summary>
    public DefaultTusConfiguration Configuration { get; }

    private async Task AuthorizeAsync(AuthorizeContext context)
    {
        var request = context.HttpContext.Request;
        if (!_origins.Allows(request))
        {
            _logger.LogWarning(
                "Upload request refused - it came from another site: Origin {Origin}, Sec-Fetch-Site {FetchSite}",
                AllowedRequestOrigins.ForLog(request.Headers.Origin.ToString()),
                AllowedRequestOrigins.ForLog(request.Headers["Sec-Fetch-Site"].ToString()));
            context.FailRequest(HttpStatusCode.Forbidden, "A page of another site cannot upload a file here.");
            return;
        }

        // A creation is weighed in BeforeCreateAsync, against the policy it names, and the options say
        // only what the endpoint supports.
        if (context.Intent is IntentType.CreateFile or IntentType.GetOptions) return;

        // Everything else names an upload, which is its owner's alone.
        var record = await _store.FindAsync(context.FileId);
        if (record is null)
            context.FailRequest(HttpStatusCode.NotFound, "There is no upload with this id: it was never made, or it expired.");
        else if (!UploadOwner.Owns(context.HttpContext, record.Owner))
            context.FailRequest(HttpStatusCode.Forbidden, "This upload belongs to someone else.");
    }

    private async Task BeforeCreateAsync(BeforeCreateContext context)
    {
        // A deferred length and a concatenation never reach here: the endpoint does not offer either
        // extension, and the engine refuses them itself.
        var policyName = Text(context.Metadata, "policy");
        if (_policies.Find(policyName) is not { } policy)
        {
            context.FailRequest(HttpStatusCode.BadRequest, policyName is null
                ? "The upload must name its policy."
                : $"The app accepts no uploads under the policy '{policyName}'.");
            return;
        }

        var owner = UploadOwner.Resolve(context.HttpContext);
        if (policy.RequireSignedIn && !UploadOwner.IsSignedIn(owner))
        {
            context.FailRequest(HttpStatusCode.Unauthorized, "Sign in to upload this file.");
            return;
        }

        if (FileName(context.Metadata) is not { } name)
        {
            context.FailRequest(HttpStatusCode.BadRequest, "The upload must name its file.");
            return;
        }

        // The policy's own rule, in the words the page already showed when it asked first: a format it
        // does not name, then a size past its largest.
        if (policy.RefusalOf(name, context.UploadLength) is { } refusal)
        {
            context.FailRequest(policy.Accepts(name) ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.UnsupportedMediaType,
                refusal);
            return;
        }

        if (await _store.ReserveAsync(owner, _options.MaxUnfinished) is not { } reservation)
        {
            context.FailRequest(HttpStatusCode.TooManyRequests,
                $"At most {_options.MaxUnfinished} uploads may be unfinished at a time.");
            return;
        }

        // Given back when the record is written, or when the request ends without one.
        context.HttpContext.Response.RegisterForDispose(reservation);
        context.HttpContext.Items[Reservation] = reservation;
    }

    private async Task CreateCompleteAsync(CreateCompleteContext context)
    {
        var http = context.HttpContext;
        var now = _time.GetUtcNow();
        // BeforeCreateAsync accepted the metadata, so the policy and the name are there.
        var record = new UploadRecord(
            context.FileId,
            UploadOwner.Resolve(http),
            Text(context.Metadata, "policy")!,
            FileName(context.Metadata)!,
            Text(context.Metadata, "filetype") ?? "application/octet-stream",
            context.UploadLength,
            now,
            now + _options.Expiry);
        await _store.AddAsync(record, http.Items[Reservation] as UploadReservation, context.CancellationToken);
        UploadOwner.Remember(http, _options.Expiry);
    }

    private static string? Text(Dictionary<string, TusMetadata> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && value.GetString(Encoding.UTF8) is { Length: > 0 } text ? text : null;

    /// <summary>The file's name without any folder a device put before it, on either kind of separator.</summary>
    private static string? FileName(Dictionary<string, TusMetadata> metadata) =>
        Text(metadata, "filename") is { } name && name[(name.LastIndexOfAny(['/', '\\']) + 1)..].Trim() is { Length: > 0 } file
            ? file
            : null;
}
