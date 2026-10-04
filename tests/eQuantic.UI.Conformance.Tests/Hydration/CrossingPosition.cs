namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>A struct whose state is a public field, which the server's serializer never writes.</summary>
public struct CrossingPosition
{
    public int X;
}
