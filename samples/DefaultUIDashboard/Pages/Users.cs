using DefaultUIDashboard.Components;
using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using StatelessComponent = eQuantic.UI.Core.StatelessComponent;

namespace DefaultUIDashboard.Pages;

/// <summary>
/// Route-params demo, migrated: <c>/users</c> lists people (write-once ListItems inside SPA anchor
/// rows); the detail page reads the matched <c>id</c> from <see cref="RenderContext.Route"/> — the
/// same value whether server-rendered or reached by client navigation. Keeps the transpiler demos:
/// a value-type record emitted as a named JS class and a static data field referenced as
/// <c>Users.people</c>.
/// </summary>
[Page("/users", Title = "Users")]
public class Users : StatelessComponent
{
    private static readonly List<Person> People = new()
    {
        new Person(1, "Ada Lovelace", "Engineering"),
        new Person(2, "Alan Turing", "Research"),
        new Person(3, "Grace Hopper", "Compilers"),
        new Person(4, "Edsger Dijkstra", "Algorithms"),
    };

    public override IComponent Build(RenderContext context)
    {
        var header = new Column(gap: Space.S1);
        header.Add(new Text("Users", TypeRole.Heading));
        header.Add(new Text(
            $"{People.Count} people · click a row to open /users/{{id}}", TypeRole.Caption));

        var shell = new SampleShell { ActivePath = "/users" };
        shell.Children.Add(new VisualNodeComponent(header));

        foreach (var p in People)
        {
            // The row is a plain anchor (SPA link the router intercepts) wrapping a write-once
            // ListItem — navigation stays declarative, the visuals stay in the shared library.
            var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center, Width = SizeValue.Fill };
            row.Add(new Avatar(Initials(p.Name), SizeVariant.Medium, name: p.Name));
            var text = new Column(gap: 0);
            text.Add(new Text(p.Name, TypeRole.Label));
            text.Add(new Text(p.Team, TypeRole.Caption));
            row.Add(new Flexible(text));
            row.Add(new Text($"#{p.Id} →", TypeRole.Caption));

            // A WRITE-ONCE link row: the Card is the child; the Link supplies the semantics.
            var linked = new Column(gap: 0) { Padding = new EdgeInsets(0, Space.S3, 0, 0) };
            linked.Add(new Link($"/users/{p.Id}", new Card(row, CardKind.Outlined) { Width = SizeValue.Fill }));
            shell.Children.Add(new VisualNodeComponent(linked));
        }

        return shell;
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ');
        return parts.Length > 1 ? $"{parts[0][0]}{parts[1][0]}" : name[..1];
    }
}

/// <summary>One person model — a value-type record emitted as a named JS class.</summary>
public record Person(int Id, string Name, string Team);

/// <summary>
/// The params target: <c>/users/{id}</c>. Reads the route parameter (and an optional <c>?role=</c>
/// query) straight from <see cref="RenderContext.Route"/>.
/// </summary>
[Page("/users/{id:int}", Title = "User Detail")]
public class UserDetail : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var id = context.Route.Param("id") ?? "?";
        var role = context.Route.Query("role") ?? "member";

        var shell = new SampleShell { ActivePath = "/users" };
        shell.Children.Add(new VisualNodeComponent(new UserDetailView { Id = id, Role = role }));
        return shell;
    }
}
