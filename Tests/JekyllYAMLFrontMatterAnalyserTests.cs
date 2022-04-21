using System.Diagnostics;
using Xunit;

namespace Tests;

public sealed class JekyllYAMLFrontMatterAnalyserTests {
    // TODO: All cases
    // TODO: frontmatterignore
    // TODO: No errors
    // TODO: Ignoring rules
    const string JekyllBasePath = "../../../jekyll_sites/";

    [Theory]
    [InlineData("", "JE0001")]
    [InlineData(JekyllBasePath + "no-post-dir", "JE0002")]
    [InlineData(JekyllBasePath + "no-posts", "JE0003")]
    [InlineData(JekyllBasePath + "no-tags-dir", "JE0004")]
    [InlineData(JekyllBasePath + "date-missing", "DA0001")]
    public void VerifyChecks(string arguments, params string[] expectedSubstrings) {
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

        Assert.Equal(1, p.ExitCode);
        Assert.Empty(errors);
        foreach (var expectedSubstring in expectedSubstrings) {
            Assert.Contains(expectedSubstring, output);
        }
    }
}