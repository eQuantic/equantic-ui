namespace eQuantic.UI.Native.Hosting;

/// <summary>How a value is written into the app's manifest — a plist is TYPED, and the reader that
/// asks it for a boolean gets the wrong answer from <c>&lt;string&gt;true&lt;/string&gt;</c>.</summary>
public enum PhotonBundleValueKind
{
    /// <summary>A plain string value.</summary>
    Text,

    /// <summary>A real boolean.</summary>
    Flag,

    /// <summary>A URL scheme this app answers to. These do not each get a key: they are collected
    /// into the one <c>CFBundleURLTypes</c> array the system reads, which is why the kind exists
    /// rather than the caller building the array.</summary>
    UrlScheme,
}
