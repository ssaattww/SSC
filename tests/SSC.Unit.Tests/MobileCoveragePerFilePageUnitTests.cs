using System.Diagnostics;

namespace SSC.Unit.Tests;

/// <summary>
/// スマートフォン向けcoverageがソースファイル別ページを生成する契約を検証します。
/// </summary>
public sealed class MobileCoveragePerFilePageUnitTests
{
    /// <summary>
    /// 各ソースファイルの行別coverageが個別HTMLへ分割され、一覧から遷移できることを確認します。
    /// </summary>
    [Fact]
    public void GenerateReport_CreatesOneLineCoveragePagePerSourceFile()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ssc-mobile-file-pages-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(temporaryDirectory, "source");
        var firstSourcePath = Path.Combine(sourceRoot, "src", "SSC", "FirstSample.cs");
        var secondSourcePath = Path.Combine(sourceRoot, "src", "SSC", "SecondSample.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(firstSourcePath)!);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "coverage.xml");
            var outputPath = Path.Combine(temporaryDirectory, "index.html");
            File.WriteAllText(inputPath, MultiFileCobertura);
            File.WriteAllText(firstSourcePath, "namespace SSC;\npublic static class FirstSample { }\n");
            File.WriteAllText(secondSourcePath, "namespace SSC;\npublic static class SecondSample { }\n");

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

            var pagesDirectory = Path.Combine(temporaryDirectory, "files");
            var sourcePages = Directory.GetFiles(pagesDirectory, "*.html");
            Assert.Equal(2, sourcePages.Length);

            var index = File.ReadAllText(outputPath);
            Assert.Contains("src/SSC/FirstSample.cs", index);
            Assert.Contains("src/SSC/SecondSample.cs", index);
            Assert.Contains("href=\"files/", index);
            Assert.DoesNotContain("class=\"source-row", index);

            var pageContents = sourcePages.Select(File.ReadAllText).ToArray();
            Assert.Contains(
                pageContents,
                page => page.Contains("src/SSC/FirstSample.cs", StringComparison.Ordinal));
            Assert.Contains(
                pageContents,
                page => page.Contains("src/SSC/SecondSample.cs", StringComparison.Ordinal));
            Assert.All(pageContents, page => Assert.Contains("href=\"../index.html\"", page));
            Assert.All(pageContents, page => Assert.Contains("class=\"source-row", page));
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

    private const string MultiFileCobertura = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.5" branch-rate="1" lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="0">
          <packages>
            <package name="SSC" line-rate="0.5" branch-rate="1">
              <classes>
                <class name="SSC.FirstSample" filename="src/SSC/FirstSample.cs" line-rate="1" branch-rate="1">
                  <methods>
                    <method name="First" signature="()" line-rate="1" branch-rate="1">
                      <lines><line number="2" hits="1" /></lines>
                    </method>
                  </methods>
                  <lines><line number="2" hits="1" /></lines>
                </class>
                <class name="SSC.SecondSample" filename="src/SSC/SecondSample.cs" line-rate="0" branch-rate="1">
                  <methods>
                    <method name="Second" signature="()" line-rate="0" branch-rate="1">
                      <lines><line number="2" hits="0" /></lines>
                    </method>
                  </methods>
                  <lines><line number="2" hits="0" /></lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
