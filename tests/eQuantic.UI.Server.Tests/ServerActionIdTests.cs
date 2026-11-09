using System.Reflection;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A Server Action's id is unique by construction (#1290): the component's CLR full name, which eqc reads
/// off the same declaration, and the action's name. By the component's simple name, two of one name shared
/// one id, and the registry kept the last one without a word: two nested in two types, as here, or two of
/// one name in two namespaces of two scanned assemblies (one project refuses those, EQ3102).
/// </summary>
public class ServerActionIdTests
{
    [Fact]
    public void TwoComponentsOfOneName_InTwoNamespaces_HaveAnIdEach()
    {
        var registry = new ServerActionRegistry();
        registry.ScanAssembly(typeof(ServerActionIdTests).Assembly);

        var first = registry.GetAction($"{typeof(Billing.Invoices).FullName}/Export");
        var second = registry.GetAction($"{typeof(Archive.Invoices).FullName}/Export");

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.ComponentType.Should().Be(typeof(Billing.Invoices));
        second!.ComponentType.Should().Be(typeof(Archive.Invoices));
        registry.GetAction("Invoices/Export").Should().BeNull();
    }

    [Fact]
    public void TwoMethodsUnderOneId_StopTheAppAtStart_NamingBoth()
    {
        var registry = new ServerActionRegistry();
        registry.RegisterAction("Shop/Save", Descriptor(nameof(Overloaded.Save), [typeof(string)]));

        var register = () => registry.RegisterAction("Shop/Save", Descriptor(nameof(Overloaded.Save), [typeof(string), typeof(int)]));

        register.Should().Throw<InvalidOperationException>()
            .WithMessage("*Save(String)*Save(String, Int32)*");
    }

    [Fact]
    public void TheSameMethodRegisteredTwice_IsOneAction()
    {
        var registry = new ServerActionRegistry();
        registry.RegisterAction("Shop/Save", Descriptor(nameof(Overloaded.Save), [typeof(string)]));

        var again = () => registry.RegisterAction("Shop/Save", Descriptor(nameof(Overloaded.Save), [typeof(string)]));

        again.Should().NotThrow();
    }

    private static ServerActionDescriptor Descriptor(string name, Type[] parameters) => new()
    {
        ActionId = "Shop/Save",
        ComponentType = typeof(Overloaded),
        Method = typeof(Overloaded).GetMethod(name, BindingFlags.Public | BindingFlags.Instance, parameters)!,
        ParameterTypes = parameters,
    };

    public static class Billing
    {
        public sealed class Invoices : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

            [ServerAction]
            public string Export() => "billing";
        }
    }

    // Internal, so no factory is written for it and the generator does not refuse the pair (EQ3102), as it
    // would not for a component of the same name in another scanned assembly.
    internal static class Archive
    {
        internal sealed class Invoices : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

            [ServerAction]
            public string Export() => "archive";
        }
    }

    public sealed class Overloaded : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text(string.Empty);

        public void Save(string name) { }

        public void Save(string name, int copies) { }
    }
}
