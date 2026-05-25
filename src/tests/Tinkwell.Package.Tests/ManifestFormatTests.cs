using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class ManifestFormatTests
{
    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ManifestFormat.Parse(null!));
    }

    [Fact]
    public void Parse_InvalidFormatVersion_Throws()
    {
        const string text = """
            package "x" {
              format-version = not-a-number
            }
            """;

        var ex = Assert.Throws<PackageException>(() => ManifestFormat.Parse(text));
        Assert.Contains("format-version", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ManifestFormat.Write(null!));
    }

    [Fact]
    public void Parse_FullManifest_AllFieldsParsed()
    {
        const string text = """
            package "my-runlet" {
              format-version = 1
              version = "1.2.0"
              author = "Jane Doe"
              author-email = "jane@example.com"
              company = "Acme Corp"
              company-website = "https://acme.example.com"
              company-email = "info@acme.example.com"
              support-email = "support@acme.example.com"
              description = "A test package"
              license = "MIT"
              license-url = "https://opensource.org/licenses/MIT"
              copyright = "Copyright 2026 Acme Corp"
              contributors = "John Smith, Alice Brown"
              project-website = "https://example.com"
              documentation-website = "https://docs.example.com"
              terms-url = "https://example.com/terms"
              custom-key = "custom-value"
            }
            """;

        var manifest = ManifestFormat.Parse(text);

        Assert.Equal("my-runlet", manifest.Name);
        Assert.Equal(1, manifest.FormatVersion);
        Assert.Equal("1.2.0", manifest.Version);
        Assert.Equal("Jane Doe", manifest.Author);
        Assert.Equal("jane@example.com", manifest.AuthorEmail);
        Assert.Equal("Acme Corp", manifest.Company);
        Assert.Equal("https://acme.example.com", manifest.CompanyWebsite);
        Assert.Equal("info@acme.example.com", manifest.CompanyEmail);
        Assert.Equal("support@acme.example.com", manifest.SupportEmail);
        Assert.Equal("A test package", manifest.Description);
        Assert.Equal("MIT", manifest.License);
        Assert.Equal("https://opensource.org/licenses/MIT", manifest.LicenseUrl);
        Assert.Equal("Copyright 2026 Acme Corp", manifest.Copyright);
        Assert.Equal("John Smith, Alice Brown", manifest.Contributors);
        Assert.Equal("https://example.com", manifest.ProjectWebsite);
        Assert.Equal("https://docs.example.com", manifest.DocumentationWebsite);
        Assert.Equal("https://example.com/terms", manifest.TermsUrl);
        Assert.Equal("custom-value", manifest.Properties["custom-key"]);
    }

    [Fact]
    public void Parse_MinimalManifest_NameOnly()
    {
        const string text = """
            package "minimal" {
            }
            """;

        var manifest = ManifestFormat.Parse(text);
        Assert.Equal("minimal", manifest.Name);
        Assert.Equal(1, manifest.FormatVersion);
        Assert.Null(manifest.Version);
        Assert.Null(manifest.Author);
        Assert.Empty(manifest.Properties);
    }

    [Fact]
    public void Parse_UnquotedName_Works()
    {
        const string text = """
            package my-runlet {
              version = "1.0.0"
            }
            """;

        var manifest = ManifestFormat.Parse(text);
        Assert.Equal("my-runlet", manifest.Name);
    }

    [Fact]
    public void Parse_WithComments_Ignored()
    {
        const string text = """
            # Top-level comment
            package "test" {
              // Inline comment
              version = "1.0.0"
              # Another comment
              author = "Test"
            }
            """;

        var manifest = ManifestFormat.Parse(text);
        Assert.Equal("test", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("Test", manifest.Author);
    }

    [Fact]
    public void Parse_WrongBlockType_Throws()
    {
        Assert.Throws<PackageException>(() =>
            ManifestFormat.Parse("""config "test" { }"""));
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new PackageManifest
        {
            Name = "round-trip-test",
            FormatVersion = 1,
            Version = "2.0.0",
            Author = "Author",
            AuthorEmail = "author@example.com",
            Company = "Test Corp",
            CompanyWebsite = "https://testcorp.example.com",
            CompanyEmail = "contact@testcorp.example.com",
            SupportEmail = "help@testcorp.example.com",
            Description = "Description here",
            License = "Apache-2.0",
            LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0",
            Copyright = "Copyright 2026 Test Corp",
            Contributors = "Dev A, Dev B",
            ProjectWebsite = "https://project.example.com",
            DocumentationWebsite = "https://docs.project.example.com",
            TermsUrl = "https://testcorp.example.com/terms",
            Properties = new Dictionary<string, string> { ["target"] = "firmware" },
        };

        var text = ManifestFormat.Write(original);
        var parsed = ManifestFormat.Parse(text);

        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.Version, parsed.Version);
        Assert.Equal(original.Author, parsed.Author);
        Assert.Equal(original.AuthorEmail, parsed.AuthorEmail);
        Assert.Equal(original.Company, parsed.Company);
        Assert.Equal(original.CompanyWebsite, parsed.CompanyWebsite);
        Assert.Equal(original.CompanyEmail, parsed.CompanyEmail);
        Assert.Equal(original.SupportEmail, parsed.SupportEmail);
        Assert.Equal(original.Description, parsed.Description);
        Assert.Equal(original.License, parsed.License);
        Assert.Equal(original.LicenseUrl, parsed.LicenseUrl);
        Assert.Equal(original.Copyright, parsed.Copyright);
        Assert.Equal(original.Contributors, parsed.Contributors);
        Assert.Equal(original.ProjectWebsite, parsed.ProjectWebsite);
        Assert.Equal(original.DocumentationWebsite, parsed.DocumentationWebsite);
        Assert.Equal(original.TermsUrl, parsed.TermsUrl);
        Assert.Equal("firmware", parsed.Properties["target"]);
    }
}
