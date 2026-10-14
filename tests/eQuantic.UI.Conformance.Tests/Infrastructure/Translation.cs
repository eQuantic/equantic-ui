namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// What the build makes of a case's C#: the JavaScript the converter wrote, and every reason the build
/// refuses the C# (<see cref="Refusal"/>), the text's own apart from the prelude's. The runner reaches
/// the text through <see cref="Accepted"/> only, so text written where the build refused is never run.
/// </summary>
public sealed class Translation
{
    private readonly Lazy<IReadOnlyList<string>> _csharpErrors;

    public Translation(string js, IReadOnlyList<Refusal> ownRefusals, IReadOnlyList<Refusal> preludeRefusals,
        Func<IReadOnlyList<string>> csharpErrors)
    {
        Js = js;
        OwnRefusals = ownRefusals;
        PreludeRefusals = preludeRefusals;
        Refusals = [.. ownRefusals, .. preludeRefusals];
        _csharpErrors = new Lazy<IReadOnlyList<string>>(csharpErrors);
    }

    /// <summary>What the converter wrote, a fallback wherever it refused.</summary>
    public string Js { get; }

    /// <summary>What the build refuses in the text this translation wrote: the converter's errors over
    /// it. For a case, its own C#, which is where a refusal the case pins has to come from.</summary>
    public IReadOnlyList<Refusal> OwnRefusals { get; }

    /// <summary>What the build refuses among the declarations of the case's prelude, by its own
    /// verdict.</summary>
    public IReadOnlyList<Refusal> PreludeRefusals { get; }

    /// <summary>Every reason the build refuses the case: its own first, then its prelude's. None for
    /// C# it accepts.</summary>
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
