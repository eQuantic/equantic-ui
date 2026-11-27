using System;

namespace eQuantic.UI.Web.Tests.Fixtures;

// The statics of two closed types, reached from another class.
public sealed class EmittedCacheUser
{
    public int Use() { EmittedCache<int>.Keep(3); EmittedCache<string>.Keep("a"); Func<int, int> keep = EmittedCache<int>.Keep; return keep(EmittedCache<int>.Hits) + EmittedCache<string>.Count + EmittedCache<int>.Size; }
}
