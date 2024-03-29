﻿using System;
using System.Diagnostics;
using Xunit;

namespace Tests;

public sealed class JekyllYAMLFrontMatterAnalyserTests {
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
    [InlineData(JekyllBasePath + "title", "TI0001", "TI0002", "TI0003", "TI0004")]
    [InlineData("", "JE0001")]
    [InlineData(JekyllBasePath + "no-post-dir", "JE0002")]
    [InlineData(JekyllBasePath + "no-posts", "JE0003")]
    [InlineData(JekyllBasePath + "apps", "AP0001", "AP0002", "AP0003", "AP0004", "AP0005", "AP0006", "AP0007", "AP0008", "AP0009", "AP0010", "AP0011", "AP0012", "AP0013", "AP0014", "AP0015", "AP0016", "AP0017", "AP0018", "AP0019", "AP0023", "AP0024")]
    [InlineData(JekyllBasePath + "app-images", "AP0020", "AP0021")]
    [InlineData(JekyllBasePath + "app-dates-not-updated", "AP0022")]
    [InlineData(JekyllBasePath + "privacy", "PR0001", "PR0002", "PR0003", "PR0004", "PR0005", "PR0006", "PR0007", "PR0008", "PR0009", "PR0010")]
    public void VerifyChecks(string arguments, params string[] expectedSubstrings) {
        var (output, errors, exitCode) = RunAnalyser(arguments);

        Assert.Equal(1, exitCode);
        if (errors?.Length > 0) {
            Console.WriteLine(errors);
        }

        Assert.Empty(errors);
        foreach (var expectedSubstring in expectedSubstrings) {
            Assert.Contains(expectedSubstring, output);
        }
    }

    [Theory]
    [InlineData(JekyllBasePath + "no-tags-dir")]
    [InlineData(JekyllBasePath + "correct-site")]
    [InlineData(JekyllBasePath + "ignored-rules")]
    [InlineData(JekyllBasePath + "frontmatterignore")]
    [InlineData(JekyllBasePath + "ignored-app-rules")]
    [InlineData(JekyllBasePath + "white-listed-app")]
    public void CorrectSite(string arguments) {
        var (output, errors, exitCode) = RunAnalyser(arguments);

        Assert.Contains("No errors 😃", output);
        Assert.Equal(0, exitCode);
        Assert.Empty(errors);
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

