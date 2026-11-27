using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public static class EmittedRoutes
{
    private const string Checkout = "/checkout";
    public static string Pay(string order) => $"{Checkout}?order={Uri.EscapeDataString(order)}";
}
