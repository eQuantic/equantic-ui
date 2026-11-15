namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A record whose equality compares private fields the wire does not carry, one of them read from its
/// parameters, on <see cref="CrossingPrivateStatePage"/>.</summary>
public sealed record CrossingProduct(int Id, string Name)
{
    private int _views;
    private string _tag = Name + "!";

    public int Bump() => ++_views;

    public string Tag() => _tag;
}
