using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public interface IEmittedShaped
{
    int Area { get; }
    string Label { get; set; }
    string Describe(string unit);
}
