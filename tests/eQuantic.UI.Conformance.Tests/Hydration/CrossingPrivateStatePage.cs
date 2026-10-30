using System;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose state holds a record with private fields and a struct with a public one. Hydration rebuilt each from
/// the payload's keys alone, so the record equalled no value built here and <c>Bump</c> answered NaN (#590).
/// </summary>
[Page("/crossing-private-state")]
public sealed class CrossingPrivateStatePage : StatefulComponent, IServerPrefetch
{
    private CrossingProduct _product = new(0, "");
    private CrossingSpot _position;

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _product = new CrossingProduct(1, "x");
        _position = new CrossingSpot { Y = 2 };
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var same = new CrossingProduct(1, "x");
        var copy = _product with { };
        var page = new Column();
        page.Add(new Text($"equal {_product == same} {_product.GetHashCode() == same.GetHashCode()}", TypeRole.BodyM));
        page.Add(new Text($"state {_product.Tag()} {copy.Bump()}", TypeRole.BodyM));
        page.Add(new Text($"position {_position.X} {_position.Y}", TypeRole.BodyM));
        return page;
    }
}
