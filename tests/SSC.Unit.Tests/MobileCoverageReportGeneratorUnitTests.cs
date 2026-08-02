using System.Diagnostics;

namespace SSC.Unit.Tests;

/// <summary>
/// スマートフォン向けcoverage HTMLの表示契約を検証します。
/// </summary>
public sealed class MobileCoverageReportGeneratorUnitTests
{
    /// <summary>
    /// 各methodが行カバー済み、一部カバー、未実行のどれかを明示することを確認します。
    /// </summary>
    [Fact]
    public void GenerateReport_ShowsExplicitCoverageStateForEveryMethod()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ssc-mobile-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "coverage.xml");
            var outputPath = Path.Combine(temporaryDirectory, "index.html");
            File.WriteAllText(inputPath, SampleCobertura);

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

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Python coverage report generator.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(
                process.ExitCode == 0,
                $"Generator failed with exit code {process.ExitCode}. stdout: {standardOutput} stderr: {standardError}");

            var report = File.ReadAllText(outputPath);
            Assert.Contains("<th>状態</th>", report);
            Assert.Contains("行カバー済み", report);
            Assert.Contains("一部カバー", report);
            Assert.Contains("未実行", report);
            Assert.Contains("完全カバー 1", report);
            Assert.Contains("部分カバー 1", report);
            Assert.Contains("未カバー 1", report);
            Assert.Contains("value=\"covered\">行カバー済み</option>", report);
            Assert.Contains("value=\"partial\">一部カバー</option>", report);
            Assert.Contains("value=\"uncovered\">未実行</option>", report);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    /// <summary>
    /// ソースコードの各行がcoverage状態を明示し、スマートフォンで約40行を確認できる密度で表示されることを確認します。
    /// </summary>
    [Fact]
    public void GenerateReport_ShowsCoverageStateForEverySourceLine()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ssc-mobile-line-coverage-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(temporaryDirectory, "source");
        var sourcePath = Path.Combine(sourceRoot, "src", "SSC", "CoverageSample.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "coverage.xml");
            var outputPath = Path.Combine(temporaryDirectory, "index.html");
            File.WriteAllText(inputPath, SourceLineCobertura);
            File.WriteAllText(
                sourcePath,
                "namespace SSC;\npublic static class CoverageSample\n{\n    public static int Value => 1;\n}\n");

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

            var index = File.ReadAllText(outputPath);
            Assert.Contains("files/", index);
            Assert.Contains("CoverageSample.cs", index);
            Assert.DoesNotContain("public static class CoverageSample", index);

            var sourcePages = Directory.GetFiles(
                Path.Combine(temporaryDirectory, "files"),
                "*.html");
            var report = File.ReadAllText(Assert.Single(sourcePages));
            Assert.Contains("<th>行</th><th>行状態</th><th>Hits</th><th>Source</th>", report);
            Assert.Contains("data-line-status=\"covered\"", report);
            Assert.Contains("data-line-status=\"uncovered\"", report);
            Assert.Contains("data-line-status=\"not-coverable\"", report);
            Assert.Contains("実行済み", report);
            Assert.Contains("未実行", report);
            Assert.Contains("対象外", report);
            Assert.Contains("public static class CoverageSample", report);
            Assert.Contains("public static int Value =&gt; 1;", report);
            Assert.Contains(".source-table-wrap { max-height:76vh;", report);
            Assert.Contains(".source-table { width:100%; border-collapse:collapse; font-size:10px; line-height:1.05;", report);
            Assert.Contains(".source-table th,.source-table td { padding:0 3px;", report);
            Assert.Contains(".source-code code { display:block; padding:0 3px;", report);
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
        <coverage line-rate="0.5" branch-rate="0.5" lines-covered="3" lines-valid="6" branches-covered="1" branches-valid="2">
          <packages>
            <package name="SSC" line-rate="0.5" branch-rate="0.5">
              <classes>
                <class name="SSC.CoverageSample" filename="src/SSC/CoverageSample.cs" line-rate="0.5" branch-rate="0.5">
                  <methods>
                    <method name="FullyCovered" signature="()" line-rate="1" branch-rate="1">
                      <lines><line number="10" hits="1" /></lines>
                    </method>
                    <method name="PartiallyCovered" signature="()" line-rate="0.5" branch-rate="0.5">
                      <lines><line number="20" hits="1" /><line number="21" hits="0" /></lines>
                    </method>
                    <method name="Uncovered" signature="()" line-rate="0" branch-rate="0">
                      <lines><line number="30" hits="0" /></lines>
                    </method>
                  </methods>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    private const string SourceLineCobertura = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.5" branch-rate="1" lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="0">
          <packages>
            <package name="SSC" line-rate="0.5" branch-rate="1">
              <classes>
                <class name="SSC.CoverageSample" filename="src/SSC/CoverageSample.cs" line-rate="0.5" branch-rate="1">
                  <methods>
                    <method name="get_Value" signature="()" line-rate="0.5" branch-rate="1">
                      <lines><line number="3" hits="1" /><line number="4" hits="0" /></lines>
                    </method>
                  </methods>
                  <lines>
                    <line number="3" hits="2" />
                    <line number="4" hits="0" />
                  </lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
