using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The consent question a site under GDPR or LGPD has to ask before a non-essential cookie is set —
/// drawn only while the answer is <see cref="ConsentState.Unknown"/>, and gone the moment the
/// visitor gives one, on this visit and every visit after (the answer lives in the shared
/// <c>eq-consent</c> cookie, which the server reads too, so a returning visitor's first paint has
/// no banner in it).
/// <para>
/// A CARD, not a layer: the app decides where it sits — a <c>Pinned</c> at the bottom of the page
/// is the usual home — because a consent surface that pins itself would fight every shell that
/// already owns the bottom edge. It asks for <see cref="IConsent"/> through the context like any
/// capability; where none is registered (a native host with no tag manager to gate) it draws
/// nothing, because there is nothing to consent to.
/// </para>
/// <para>
/// The copy is the SDK's localized default (title, body, both buttons, the policy link) and every
/// piece can be overridden; the policy link appears only when the app names a destination, since a
/// consent card without a policy behind it is a promise the site cannot keep.
/// </para>
/// </summary>
public sealed class CookieConsent(string? policyHref = null) : StatefulComponent
{
    /// <summary>Where the privacy policy lives (an absolute or root-relative URL). Null = no link.</summary>
    public string? PolicyHref { get; init; } = policyHref;

    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? AcceptLabel { get; init; }
    public string? RejectLabel { get; init; }
    public string? PolicyLabel { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var consent = context.GetService<IConsent>();
        if (consent is null || consent.State != ConsentState.Unknown)
            return Spacer.Fixed(0);

        var theme = context.Theme;
        var actions = new Row(gap: Space.S2) { Cross = CrossAlign.Center, Wrap = true, RunGap = Space.S2 };
        actions.Add(new Button(AcceptLabel ?? SdkStrings.AcceptCookies, Variant.Primary, SizeVariant.Small,
            () => Answer(consent.Grant)));
        actions.Add(new Button(RejectLabel ?? SdkStrings.RejectCookies, Variant.Secondary, SizeVariant.Small,
            () => Answer(consent.Deny)));
        if (PolicyHref is { Length: > 0 } href)
        {
            actions.Add(new Link(href,
                new Text(PolicyLabel ?? SdkStrings.PrivacyPolicy, TypeRole.LabelSmall,
                    theme.Colors(Variant.Primary).Base, maxLines: 1)));
        }

        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(new Text(Title ?? SdkStrings.CookieConsentTitle, TypeRole.Label, theme.TextPrimary, maxLines: 2));
        column.Add(new Text(Body ?? SdkStrings.CookieConsentBody, TypeRole.BodyM, theme.TextSecondary, maxLines: 6));
        column.Add(actions);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Large)),
            Elevation = 2,
        }, column);
    }

    // The answer is stored by the capability; the rebuild that follows reads it back and draws
    // nothing — the component keeps no copy of the state, so two tabs cannot disagree.
    private void Answer(Action reply) => SetState(reply);
}
