namespace eQuantic.UI;

/// <summary>
/// The schemes the workspace builder's NAMED doors open, by the method that opens each.
///
/// <para>
/// Two places have to agree, and they live in assemblies that cannot reference each other: the
/// builder an app calls (<c>PhotonWorkspaceBuilder</c>, net10.0), which widens the policy the app
/// runs with, and the generator that reads that call (netstandard2.0), which writes the scheme
/// where a phone's build puts it in the manifest. If <c>OpensMail()</c> opened one scheme at run
/// time and declared another at build time, a phone would open the link and answer "nothing opens
/// it" to the question, in an app whose source looks correct. The same shape as
/// <see cref="BundleFactRule"/>, for the same reason. The scheme an app names itself,
/// <c>Opens("slack")</c>, goes through <see cref="BundleFactRule.Scheme"/> on both sides.
/// </para>
/// </summary>
internal static class OpenedSchemeRule
{
    /// <summary><c>mailto:</c>, the mail app.</summary>
    internal const string Mail = "mailto";

    /// <summary><c>tel:</c>, the dialer.</summary>
    internal const string Phone = "tel";

    /// <summary><c>sms:</c>, the messages app.</summary>
    internal const string Messages = "sms";

    /// <summary>The scheme a named door opens, or null for a method that is none of them.</summary>
    internal static string? Named(string method) => method switch
    {
        "OpensMail" => Mail,
        "OpensPhone" => Phone,
        "OpensMessages" => Messages,
        _ => null,
    };
}
