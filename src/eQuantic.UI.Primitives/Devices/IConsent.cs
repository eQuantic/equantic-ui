namespace eQuantic.UI.Primitives;

/// <summary>
/// The visitor's answer to NON-ESSENTIAL tracking — the one question GDPR and LGPD both require an
/// app to ask before an analytics cookie is set. A capability, not a node, exactly like
/// <see cref="IAnalytics"/>: a page asks for it the way it asks for a camera, shows the question
/// while the answer is <see cref="ConsentState.Unknown"/>, and never learns who stores the reply.
/// <para>
/// The realizations agree on ONE cookie, <c>eq-consent</c>, so every side reads the same answer:
/// the browser writes it and announces the change on the document as <c>eq:consent</c>; the server
/// reads it from the request, which is why a visitor who answered last week gets a first paint
/// with no banner in it; and the GTM installer's head script loads the container only when the
/// answer is granted — a denied or unanswered visitor never downloads a tag manager at all, which
/// is what "no cookie until consent" means in practice.
/// </para>
/// <para>
/// Consent is about the VISITOR'S browser, so on the server the mutations are no-ops (the reply
/// belongs to the request that follows), and a native host that registers no realization leaves
/// the capability absent — a consent component draws nothing there, because there is no tag
/// manager to gate.
/// </para>
/// </summary>
public interface IConsent
{
    /// <summary>What the visitor has said so far. <see cref="ConsentState.Unknown"/> is the state
    /// that shows the question; both answers hide it.</summary>
    ConsentState State { get; }

    /// <summary>The visitor accepts non-essential tracking. Stores the answer and announces it, so
    /// a collector waiting on consent can start.</summary>
    void Grant();

    /// <summary>The visitor declines. Stored too — a "no" that is asked again on every visit is a
    /// dark pattern, and the regulations name it as one.</summary>
    void Deny();
}

/// <summary>The three states a consent question can be in. Unknown is the only one that asks.</summary>
public enum ConsentState
{
    /// <summary>Not answered yet — show the question, set nothing.</summary>
    Unknown,

    /// <summary>Accepted: collectors may set their cookies.</summary>
    Granted,

    /// <summary>Declined: the site stays fully usable and nothing non-essential is set.</summary>
    Denied,
}
