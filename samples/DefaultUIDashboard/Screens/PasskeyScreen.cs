using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A passkey from C# (#1017): the page asks its server for the options when it opens, hands them to
/// the device when the person presses, and hands the device's answer back to the server, each as the
/// relying party's own JSON, without reading a field of either.
/// <para>
/// The options are asked for BEFORE the press, and the press calls the ceremony first: Safari runs
/// one only inside the person's gesture, and a round trip to the server in between can spend it. A
/// challenge is good for one ceremony, so each ceremony asks for the next options once it is done.
/// </para>
/// </summary>
[Page("/passkeys", Title = "Passkeys — eQuantic Console")]
public sealed class PasskeyScreen : StatefulComponent
{
    private readonly IPasskeys? _passkeys;
    private string? _creation;
    private string? _request;
    private string _said = "";
    private bool _busy;
    private bool _navOpen;

    public PasskeyScreen(IPasskeys? passkeys = null)
    {
        _passkeys = passkeys;
    }

    protected override void OnMount() => _ = Prepare();

    [ServerAction]
    public Task<string> BeginRegistration() => Task.FromResult(DemoRelyingParty.CreationOptions());

    [ServerAction]
    public Task<string> FinishRegistration(string credential) => Task.FromResult(DemoRelyingParty.Register(credential));

    [ServerAction]
    public Task<string> BeginSignIn() => Task.FromResult(DemoRelyingParty.RequestOptions());

    [ServerAction]
    public Task<string> FinishSignIn(string credential) => Task.FromResult(DemoRelyingParty.SignIn(credential));

    private async Task Prepare()
    {
        if (_passkeys is not { IsAvailable: true }) return;
        await Renew(_said);
    }

    /// <summary>Asks for the next options of both ceremonies, the two requests at once, and shows
    /// what the last ceremony said.</summary>
    private async Task Renew(string said)
    {
        var creation = BeginRegistration();
        var request = BeginSignIn();
        var nextCreation = await creation;
        var nextRequest = await request;
        SetState(() =>
        {
            _said = said;
            _creation = nextCreation;
            _request = nextRequest;
            _busy = false;
        });
    }

    private async Task Create()
    {
        if (_busy || _passkeys is null || _creation is not { } options) return;
        _busy = true;
        // The ceremony first, inside the press; the server only after it.
        var result = await _passkeys.CreateAsync(options);
        var said = result.Outcome == PasskeyOutcome.Succeeded
            ? await FinishRegistration(result.Credential!)
            : Explain(result.Outcome);
        await Renew(said);
    }

    private async Task SignIn()
    {
        if (_busy || _passkeys is null || _request is not { } options) return;
        _busy = true;
        var result = await _passkeys.AuthenticateAsync(options);
        var said = result.Outcome == PasskeyOutcome.Succeeded
            ? await FinishSignIn(result.Credential!)
            : Explain(result.Outcome);
        await Renew(said);
    }

    private static string Explain(PasskeyOutcome outcome) => outcome switch
    {
        PasskeyOutcome.Cancelled => "Cancelled: the prompt was dismissed, or it lapsed.",
        PasskeyOutcome.AlreadyRegistered =>
            "This device already holds a passkey for this account. Sign in with it instead.",
        PasskeyOutcome.Unavailable => "This browser cannot use a passkey here.",
        _ => "The browser refused the ceremony.",
    };

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/identity", "Passkeys", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var available = _passkeys is { IsAvailable: true };
        return Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = theme.Background,
                Padding = EdgeInsets.All(Space.S6),
            },
            Column(gap: Space.S4, children: [
                Text("Passkeys", TypeRole.Heading, theme.TextPrimary),
                Text("Create a passkey for this account, then sign in with it. The server writes the options "
                    + "and reads the answer; the page only hands them across.", TypeRole.BodyM, theme.TextSecondary),
                available
                    ? Row(gap: Space.S3, children: [
                        new Button("Create a passkey", onPressed: () => _ = Create()) { Disabled = _creation is null },
                        new Button("Sign in with a passkey", Variant.Outline, onPressed: () => _ = SignIn())
                        {
                            Disabled = _request is null,
                        },
                    ])
                    : Text("A passkey needs a secure context: open this page over https, or on localhost.",
                        TypeRole.BodyM, theme.TextPrimary),
                Text(_said, TypeRole.BodyM, theme.TextPrimary),
            ]));
    }
}
