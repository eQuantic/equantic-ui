using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Layout;
using DefaultUIDashboard.Components;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// Route-params demo. <c>/users</c> lists people; each row is a SPA link to <c>/users/{id}</c>. The
/// detail page below reads the matched <c>id</c> from <see cref="RenderContext.Route"/> — the same value
/// whether the page was server-rendered (direct load / refresh) or reached by client navigation.
/// </summary>
[Page("/users", Title = "Users")]
public class Users : StatelessComponent
{
    // Static data field — emitted as a `static` class member and referenced as `Users.people`.
    private static readonly List<Person> People = new()
    {
        new Person(1, "Ada Lovelace", "Engineering"),
        new Person(2, "Alan Turing", "Research"),
        new Person(3, "Grace Hopper", "Compilers"),
        new Person(4, "Edsger Dijkstra", "Algorithms"),
    };

    public override IComponent Build(RenderContext context)
    {
        var list = new Box { ClassName = "eq-space-y-3" };
        foreach (var p in People)
            list.Children.Add(new UserRow(p.Id, p.Name, p.Team));

        return new DefaultDashboardShell
        {
            ActivePath = "/users",
            Children = {
                new Box {
                    ClassName = "eq-max-w-3xl eq-mx-auto eq-mt-12",
                    Children = {
                        new Heading("Users", 1) { ClassName = "eq-text-3xl eq-font-bold eq-mb-2" },
                        new Text($"{People.Count} people · click a row to open /users/{{id}}")
                        {
                            ClassName = "eq-text-secondary eq-text-sm eq-mb-8 eq-block"
                        },
                        list
                    }
                }
            }
        };
    }
}

/// <summary>One person model — a value-type record emitted as a named JS class.</summary>
public record Person(int Id, string Name, string Team);

/// <summary>A clickable row that SPA-navigates to the user's detail route.</summary>
public class UserRow : StatelessComponent
{
    private readonly int _id;
    private readonly string _name;
    private readonly string _team;

    public UserRow(int id, string name, string team)
    {
        _id = id;
        _name = name;
        _team = team;
    }

    public override IComponent Build(RenderContext context)
    {
        return new Link
        {
            Href = $"/users/{_id}",
            ClassName = "eq-flex eq-items-center eq-justify-between eq-surface-subtle eq-border eq-rounded-xl eq-px-6 eq-py-4 hover:eq-surface-hover eq-transition-colors",
            Children = {
                new Box {
                    Children = {
                        new Text(_name) { ClassName = "eq-font-semibold eq-block" },
                        new Text(_team) { ClassName = "eq-text-secondary eq-text-sm" }
                    }
                },
                new Box {
                    ClassName = "eq-text-secondary eq-text-sm",
                    Children = { new Text($"#{_id} →") }
                }
            }
        };
    }
}

/// <summary>
/// The params target: <c>/users/{id}</c>. Reads the route parameter (and an optional <c>?role=</c> query)
/// straight from <see cref="RenderContext.Route"/>.
/// </summary>
[Page("/users/{id:int}", Title = "User Detail")]
public class UserDetail : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var id = context.Route.Param("id") ?? "?";
        var role = context.Route.Query("role") ?? "member";

        return new DefaultDashboardShell
        {
            ActivePath = "/users",
            Children = {
                new Box {
                    ClassName = "eq-max-w-md eq-mx-auto eq-mt-20 eq-surface-subtle eq-border eq-rounded-2xl eq-p-10 eq-text-center",
                    Children = {
                        new Box {
                            ClassName = "eq-w-20 eq-h-20 eq-rounded-full eq-btn-primary eq-mx-auto eq-mb-6 eq-flex eq-items-center eq-justify-center eq-text-3xl eq-font-black",
                            Children = { new Text(id) }
                        },
                        new Heading($"User #{id}", 1) { ClassName = "eq-text-2xl eq-font-bold eq-mb-2" },
                        new Text($"Route param id = {id} · query role = {role}")
                        {
                            ClassName = "eq-text-secondary eq-text-sm eq-mb-8 eq-block"
                        },
                        new Link
                        {
                            Href = "/users",
                            ClassName = "eq-btn-outline eq-px-6 eq-py-2 eq-rounded-lg eq-inline-block",
                            Children = { new Text("← Back to users") }
                        }
                    }
                }
            }
        };
    }
}
