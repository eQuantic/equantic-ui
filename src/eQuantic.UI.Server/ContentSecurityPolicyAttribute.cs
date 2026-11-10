namespace eQuantic.UI.Server;

/// <summary>
/// Says, in the app's assembly, that the app turned on the Content-Security-Policy (#1269). Written by
/// the SDK's source generator from the app's <c>UseContentSecurityPolicy</c> call in
/// <c>Program.cs</c>, never by hand, and read by the build, which refuses then the escape hatch's
/// inline handlers: a strict policy refuses an <c>onclick</c> written as text, and the build says so
/// where the property is set instead of the browser's console saying it on a page.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ContentSecurityPolicyAttribute : Attribute;
