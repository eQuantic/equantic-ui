namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// What the build makes of a case's C#: the JavaScript the converter wrote, and every reason the build
/// refuses the C# (<see cref="Refusal"/>). The runner reaches the text through <see cref="Accepted"/>
/// only, so text written where the build refused is never run.
/// </summary>
public sealed class Translation
{
    private readonly Lazy<IReadOnlyList<string>> _csharpErrors;

    public Translation(string js, IReadOnlyList<Refusal> refusals, Func<IReadOnlyList<string>> csharpErrors)
    {
        Js = js;
        Refusals = refusals;
        _csharpErrors = new Lazy<IReadOnlyList<string>>(csharpErrors);
    }

    /// <summary>What the converter wrote, a fallback wherever it refused.</summary>
    public string Js { get; }

    /// <summary>Every reason the build refuses the C#, none for C# it accepts.</summary>
    public IReadOnlyList<Refusal> Refusals { get; }

    /// <summary>
    /// The C# compiler's own errors over the model the converter read. Asked only by a case that
    /// expects a refusal: a case the C# compiler refuses says nothing of a fence, and every other case
    /// runs its C# on .NET, which refuses it there. Lazy, because binding a whole compilation for a
    /// case that never asks is time the suite would pay for every case.
    /// </summary>
    public IReadOnlyList<string> CSharpErrors => _csharpErrors.Value;

    /// <summary>The JavaScript of C# the build accepts. A case it refuses fails here, naming every
    /// refusal, and runs nothing.</summary>
    public string Accepted() => Refusals.Count == 0 ? Js : throw new RefusedByTheBuildException(Refusals);
}
