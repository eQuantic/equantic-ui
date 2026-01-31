using FluentAssertions;
using Xunit;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// Tests for resolving external types (classes defined in other files)
/// using project compilation.
/// </summary>
public class ExternalTypeResolutionTests
{
    [Fact]
    public void SemanticModel_WithoutProjectCompilation_CannotResolveExternalTypes()
    {
        // Arrange: Component references external User class
        var componentCode = @"
            using System;

            public class UserProfile
            {
                private User _user;

                public void Render()
                {
                    var name = _user.Name;
                }
            }";

        var provider = new SemanticModelProvider();
        var tree = CSharpSyntaxTree.ParseText(componentCode);

        // Act
        var semanticModel = provider.GetSemanticModel(tree);

        // Assert: User type cannot be resolved (no compilation context)
        var userType = semanticModel.Compilation.GetTypeByMetadataName("User");
        userType.Should().BeNull("User class is not in the semantic model without project compilation");
    }

    [Fact]
    public void SemanticModel_WithProjectCompilation_CanResolveExternalTypes()
    {
        // Arrange: Create a compilation with both files
        var userClassCode = @"
            public class User
            {
                public string Name { get; set; }
                public string Email { get; set; }
            }";

        var componentCode = @"
            using System;

            public class UserProfile
            {
                private User _user;

                public void Render()
                {
                    var name = _user.Name;
                }
            }";

        var userTree = CSharpSyntaxTree.ParseText(userClassCode, path: "User.cs");
        var componentTree = CSharpSyntaxTree.ParseText(componentCode, path: "UserProfile.cs");

        var compilation = CSharpCompilation.Create(
            "TestProject",
            new[] { userTree, componentTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var provider = new SemanticModelProvider();
        provider.SetProjectCompilation(compilation);

        // Act
        var semanticModel = provider.GetSemanticModel(componentTree);

        // Assert: User type CAN be resolved
        var userType = semanticModel.Compilation.GetTypeByMetadataName("User");
        userType.Should().NotBeNull("User class should be available in the full project compilation");
        userType!.Name.Should().Be("User");
    }

    [Fact]
    public void CSharpToJsConverter_WithProjectCompilation_ConvertsExternalTypeMembers()
    {
        // Arrange: Create compilation with User and component
        var userClassCode = @"
            public class User
            {
                public string Name { get; set; }
                public string Email { get; set; }
                public int Age { get; set; }
            }";

        var componentCode = @"
            public class UserProfile
            {
                private User _user;

                public void DisplayInfo()
                {
                    var name = _user.Name;
                    var email = _user.Email;
                    var age = _user.Age;
                }
            }";

        var userTree = CSharpSyntaxTree.ParseText(userClassCode, path: "User.cs");
        var componentTree = CSharpSyntaxTree.ParseText(componentCode, path: "UserProfile.cs");

        var compilation = CSharpCompilation.Create(
            "TestProject",
            new[] { userTree, componentTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(componentTree);
        var converter = new CSharpToJsConverter();
        converter.SetSemanticModel(semanticModel);

        // Find the first member access (_user.Name)
        var method = componentTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        var firstStatement = method.Body!.Statements[0];

        // Act
        var jsCode = converter.Convert(firstStatement);

        // Assert: Should correctly convert member access
        jsCode.Should().Contain("this._user.name", "property access should be converted with lowercase property name");
    }

    [Fact]
    public void ProjectCompilationHelper_CreateCompilationFromSources_Works()
    {
        // Arrange: Create temp files
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var userFile = Path.Combine(tempDir, "User.cs");
            var componentFile = Path.Combine(tempDir, "UserProfile.cs");

            File.WriteAllText(userFile, @"
                public class User
                {
                    public string Name { get; set; }
                }");

            File.WriteAllText(componentFile, @"
                public class UserProfile
                {
                    private User _user;
                }");

            // Act
            var compilation = ProjectCompilationHelper.CreateCompilationFromSources(
                new[] { userFile, componentFile },
                Array.Empty<string>(),
                "TestProject");

            // Assert
            compilation.Should().NotBeNull();
            compilation.SyntaxTrees.Should().HaveCount(2);

            var userType = compilation.GetTypeByMetadataName("User");
            userType.Should().NotBeNull("User class should be in the compilation");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ProjectCompilationHelper_GetProjectSourceFiles_ExcludesObjAndBin()
    {
        // Arrange: Create temp project structure
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "obj"));
        Directory.CreateDirectory(Path.Combine(tempDir, "bin"));

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Source.cs"), "// source");
            File.WriteAllText(Path.Combine(tempDir, "obj", "Generated.cs"), "// generated");
            File.WriteAllText(Path.Combine(tempDir, "bin", "Compiled.cs"), "// compiled");

            // Act
            var sourceFiles = ProjectCompilationHelper.GetProjectSourceFiles(tempDir).ToList();

            // Assert
            sourceFiles.Should().HaveCount(1);
            sourceFiles[0].Should().EndWith("Source.cs");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
