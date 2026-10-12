using SharedLibrary.Domain;

namespace SharedLibrary.Common;

/// <summary>
/// What a Server Action answers. Its total is a record of the Domain project, which this library
/// references and the web app does not, and its number, instant and id cross the wire as text and
/// are read in the browser as what they are in C#.
/// </summary>
public sealed record Receipt(string Title, Money Total, long Number, DateTimeOffset IssuedAt, Guid Id);
