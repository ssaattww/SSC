using System.Diagnostics;

namespace SSC.Unit.Tests;

/// <summary>
/// スマートフォン向けcoverage reportがsource fileごとにpageを分割する契約を検証します。
/// </summary>
public sealed class MobileCoveragePerFilePagesUnitTests
{
    /// <summary>
    /// index pageとsource fileごとのline coverage pageが生成されることを確認します。
    /// </summary>
    [Fact]
    public void GenerateReport_CreatesOneLineCoveragePagePerSourceFile()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ssc-mobile-per-file-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(temporaryDirectory, "source");
        var outputDirectory = Path.Combine(temporaryDirectory, "report");
        var outputPath = Path.Combine(outputDirectory, "index.html");
        var firstSourcePath = Path.Combine(sourceRoot, "src", "SSC", "FirstSample.cs");
        var secondSourcePath = Path.Combine(sourceRoot, "src", "SSC", "SecondSample.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(firstSourcePath)!);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "coverage.xml");
            File.WriteAllText(inputPath, SampleCobertura);
            File.WriteAllText(
                firstSourcePath,
                "namespace SSC;\npublic static class FirstSample\n{\n    public static int Value => 1;\n}\n");
            File.WriteAllText(
                secondSourcePath,
                "namespace SSC;\npublic static class SecondSample\n{\n    public static int Value => 2;\n}\n");

            var startInfo = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(Path.Combine(
                repositoryRoot,
                "scripts",
                "generate-mobile-coverage-report.py"));
            startInfo.ArgumentList.Add("--input");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("--repository");
            startInfo.ArgumentList.Add("ssaattww/SSC");
            startInfo.ArgumentList.Add("--ref");
            startInfo.ArgumentList.Add("0123456789abcdef");
            startInfo.ArgumentList.Add("--source-root");
            startInfo.ArgumentList.Add(sourceRoot);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Python coverage report generator.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(
                process.ExitCode == 0,
                $"Generator failed with exit code {process.ExitCode}. stdout: {standardOutput} stderr: {standardError}");

            Assert.True(File.Exists(outputPath));
            var index = File.ReadAllText(outputPath);
            var filesDirectory = Path.Combine(outputDirectory, "files");
            Assert.True(Directory.Exists(filesDirectory));

            var filePages = Directory.GetFiles(filesDirectory, "*.html");
            Assert.Equal(2, filePages.Length);
            Assert.Contains("files/", index);
            Assert.Contains("FirstSample.cs", index);
            Assert.Contains("SecondSample.cs", index);
            Assert.DoesNotContain("public static class FirstSample", index);
            Assert.DoesNotContain("public static class SecondSample", index);

            var pages = filePages.Select(File.ReadAllText).ToArray();
            Assert.Contains(pages, page => page.Contains("FirstSample.cs", StringComparison.Ordinal)
                && page.Contains("public static class FirstSample", StringComparison.Ordinal)
                && page.Contains("data-line-status=\"covered\"", StringComparison.Ordinal));
            Assert.Contains(pages, page => page.Contains("SecondSample.cs", StringComparison.Ordinal)
                && page.Contains("public static class SecondSample", StringComparison.Ordinal)
                && page.Contains("data-line-status=\"uncovered\"", StringComparison.Ordinal));
            Assert.All(pages, page => Assert.Contains("../index.html", page));
            Assert.Contains("files/", index);
            Assert.Contains("#source-", index);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SSC.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, ".github")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the SSC repository root from the test output directory.");
    }

    private const string SampleCobertura = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.5" branch-rate="1" lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="0">
          <packages>
            <package name="SSC" line-rate="0.5" branch-rate="1">
              <classes>
                <class name="SSC.FirstSample" filename="src/SSC/FirstSample.cs" line-rate="1" branch-rate="1">
                  <methods>
                    <method name="get_Value" signature="()" line-rate="1" branch-rate="1">
                      <lines><line number="4" hits="1" /></lines>
                    </method>
                  </methods>
                  <lines><line number="4" hits="1" /></lines>
                </class>
                <class name="SSC.SecondSample" filename="src/SSC/SecondSample.cs" line-rate="0" branch-rate="1">
                  <methods>
                    <method name="get_Value" signature="()" line-rate="0" branch-rate="1">
                      <lines><line number="4" hits="0" /></lines>
                    </method>
                  </methods>
                  <lines><line number="4" hits="0" /></lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
