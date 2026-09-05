using eQuantic.UI.Web;

namespace eQuantic.UI.Charts;

public interface IChart : IComponent
{
    string? Title { get; set; }
    bool Responsive { get; set; }
}
