using System.Diagnostics;

namespace SSC.Unit.Tests;

/// <summary>
/// iOS Safariで行別coverage表が自動拡大されない表示契約を検証します。
/// </summary>
public sealed class MobileCoverageIosTextScalingUnitTests
{
    /// <summary>
    /// 行別coverage表が小さい固定文字と詰めた行高を維持することを確認します。
    /// </summary>
    [Fact]
    public void GenerateReport_DisablesIosTextAutosizingAndUsesCompactRows()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ssc-mobile-ios-scaling-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(temporaryDirectory, "source");
        var sourcePath = Path.Combine(sourceRoot, "src", "SSC", "CoverageSample.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "coverage.xml");
            var outputPath = Path.Combine(temporaryDirectory, "index.html");
            File.WriteAllText(inputPath, SampleCobertura);
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

            var report = File.ReadAllText(outputPath);
            Assert.Contains("-webkit-text-size-adjust:none; text-size-adjust:none;", report);
            Assert.Contains(".source-table { width:100%; border-collapse:collapse; font-size:9px; line-height:1.05;", report);
            Assert.Contains(".source-table th,.source-table td { padding:0 3px;", report);
            Assert.Contains(".source-table .line-state { min-width:40px; padding:0 2px; font-size:7px; line-height:1;", report);
            Assert.Contains(".source-code { min-width:320px;", report);
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
