using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The upload vocabulary answers in the browser what it answers in .NET (#1214). An app declares its
/// <c>UploadPolicy</c> once, in code the page and the server both read, so a page's module constructs
/// it and the <c>FileFilter</c>s inside it: the formats C# gathers from <c>params</c>, the largest size
/// a <c>long</c>, the refusals of what the C# constructor refuses. An <c>UploadedFile</c> is what the
/// page holds of an upload and hands a Server Action.
/// </summary>
public class UploadVocabularyConformanceTests
{
    [SkippableTheory]
    // ---- a policy as an app declares it ----
    [InlineData("var models = new UploadPolicy(\"models\", filters: [new FileFilter(\"3D models\", \"stl\", \"obj\", \"3mf\")], maxSize: 250 * 1024 * 1024); return models.Name + \"|\" + models.Filters[0].Label + \"|\" + string.Join(\",\", models.Filters[0].Extensions) + \"|\" + models.MaxSize + \"|\" + models.RequireSignedIn;")]
    [InlineData("var members = new UploadPolicy(\"members\", [], 1L << 40, requireSignedIn: true); return members.Filters.Count + \"|\" + (members.MaxSize + 1) + \"|\" + members.RequireSignedIn;")]
    [InlineData("var formats = new[] { \"png\", \"jpg\" }; var images = new FileFilter(\"Images\", formats); return images.Extensions.Length + images.Extensions[1];")]
    [InlineData("var any = new FileFilter(\"Anything\"); return any.Extensions.Length;")]
    // ---- a filter is a record: a copy keeps what it holds ----
    [InlineData("var images = new FileFilter(\"Images\", \"png\", \"jpg\"); var copy = images with { Label = \"Pictures\" }; return copy.Label + string.Join(\",\", copy.Extensions);")]
    // ---- what the constructor refuses ----
    [InlineData("try { _ = new UploadPolicy(\"models\", [], 0); return \"built\"; } catch (System.ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { _ = new UploadPolicy(\" \", [], 1); return \"built\"; } catch (System.ArgumentException e) { return e.Message; }")]
    [InlineData("try { _ = new UploadPolicy(null!, [], 1); return \"built\"; } catch (System.ArgumentNullException e) { return e.Message; }")]
    [InlineData("try { _ = new UploadPolicy(\"models\", null!, 1); return \"built\"; } catch (System.ArgumentNullException e) { return e.Message; }")]
    // ---- the policy's own rule, which the page asks before it sends and the server asks again ----
    [InlineData("var archives = new UploadPolicy(\"archives\", [new FileFilter(\"Archives\", \".tar.gz\", \"ZIP\")], 10); return archives.Accepts(\"backup.TAR.GZ\") + \"|\" + archives.Accepts(\"backup.gz\") + \"|\" + archives.Accepts(\"photos.zip\") + \"|\" + archives.Accepts(\"zip\");")]
    [InlineData("var models = new UploadPolicy(\"models\", [new FileFilter(\"3D models\", \"stl\", \"obj\"), new FileFilter(\"Again\", \"STL\", \"\")], 10); return models.RefusalOf(\"setup.exe\", 1) + \"|\" + models.RefusalOf(\"part.stl\", 11) + \"|\" + (models.RefusalOf(\"part.obj\", 10) ?? \"accepted\");")]
    [InlineData("var any = new UploadPolicy(\"any\", [], 3_000_000_000); return any.Accepts(\"anything\") + \"|\" + any.RefusalOf(\"a\", 3_000_000_001) + \"|\" + (any.RefusalOf(\"a\", 3_000_000_000) ?? \"accepted\");")]
    [InlineData("try { _ = new UploadPolicy(\"models\", [], 1).Accepts(null!); return \"accepted\"; } catch (System.ArgumentNullException e) { return e.Message; }")]
    // ---- an uploaded file as the page holds it ----
    [InlineData("var file = new UploadedFile(\"0123456789abcdef0123456789abcdef\", \"bracket.stl\", \"model/stl\", 3_000_000_000, \"models\"); return file.Id + \"|\" + file.Name + \"|\" + file.MimeType + \"|\" + (file.Size * 2) + \"|\" + file.Policy;")]
    [InlineData("try { _ = new UploadedFile(\"id\", \"a.stl\", \"model/stl\", -1, \"models\"); return \"built\"; } catch (System.ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { _ = new UploadedFile(\"\", \"a.stl\", \"model/stl\", 1, \"models\"); return \"built\"; } catch (System.ArgumentException e) { return e.Message; }")]
    [InlineData("try { _ = new UploadedFile(\"id\", \"a.stl\", null!, 1, \"models\"); return \"built\"; } catch (System.ArgumentNullException e) { return e.Message; }")]
    public void TheUploadVocabularyAnswersAsInDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }
}
