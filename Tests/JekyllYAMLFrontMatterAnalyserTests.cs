using System.Diagnostics;
using Xunit;

namespace Tests;

public sealed class JekyllYAMLFrontMatterAnalyserTests {
    // TODO: frontmatterignore
    // TODO: Ignoring rules
    const string JekyllBasePath = "../../../jekyll_sites/";

    [Theory]
    [InlineData(JekyllBasePath + "categories", "CA0001", "CA0002")]
    [InlineData(JekyllBasePath + "date-missing", "DA0001")]
    [InlineData(JekyllBasePath + "dates", "DA0001", "DA0002", "DA0003")]
    [InlineData(JekyllBasePath + "last-modified-at", "DA0004", "index.html", "archives.html")]
    [InlineData(JekyllBasePath + "description", "DE0001", "DE0002", "DE0003")]
    [InlineData(JekyllBasePath + "image", "IM0001", "IM0002")]
    [InlineData(JekyllBasePath + "layout", "LA0001")]
    [InlineData(JekyllBasePath + "tags", "TA0001", "TA0002", "not-found")]
    [InlineData(JekyllBasePath + "title", "TI0001", "TI0002")]
    [InlineData("", "JE0001")]
    [InlineData(JekyllBasePath + "no-post-dir", "JE0002")]
    [InlineData(JekyllBasePath + "no-posts", "JE0003")]
    public void VerifyChecks(string arguments, params string[] expectedSubstrings) {
        var (output, errors, exitCode) = RunAnalyser(arguments);

        Assert.Equal(1, exitCode);
        Assert.Empty(errors);
        foreach (var expectedSubstring in expectedSubstrings) {
            Assert.Contains(expectedSubstring, output);
        }
    }

    [Theory]
    [InlineData(JekyllBasePath + "no-tags-dir")]
    [InlineData(JekyllBasePath + "correct-site")]
    public void CorrectSite(string arguments) {
        var (output, errors, exitCode) = RunAnalyser(arguments);

        Assert.Equal(0, exitCode);
        Assert.Empty(errors);
        Assert.Contains("No errors 😃", output);
    }

    static (string output, string errors, int exitCode) RunAnalyser(string arguments) {
        Process p = new();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.FileName = "dotnet";
        p.StartInfo.Arguments = "script ../../../../main.csx -- " + arguments;

        p.Start();

        string output = p.StandardOutput.ReadToEnd();
        string errors = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (output, errors, p.ExitCode);
    }
}

