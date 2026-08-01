namespace SSC.Unit.Tests;

/// <summary>
/// Issue #54 のコードカバレッジ可視化workflow契約を検証します。
/// </summary>
public sealed class Issue54CodeCoverageWorkflowContractUnitTests
{
    /// <summary>
    /// PR workflowがcoverageを収集し、統合レポートと診断情報をartifactへ保存することを確認します。
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_CollectsAndPublishesCodeCoverageReport()
    {
        var workflowPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "pr-xunit-tests.yml");
        var workflow = File.ReadAllText(workflowPath);
        var testStep = GetStepBlock(
            workflow,
            "Restore and run tests with code coverage");
        var coverageStep = GetStepBlock(
            workflow,
            "Generate merged code coverage report");
        var uploadStep = GetStepBlock(
            workflow,
            "Upload .NET test results for ChatGPT review");

        Assert.Contains("--collect:\"XPlat Code Coverage\"", testStep);
        Assert.Contains("coverage.cobertura.xml", testStep);
        Assert.Contains("coverage/raw", testStep);
        Assert.Contains(
            "if: ${{ always() && steps.discover.outputs.has_tests == 'true' }}",
            coverageStep);
        Assert.Contains("dotnet-reportgenerator-globaltool", coverageStep);
        Assert.Contains("REPORTGENERATOR_VERSION: \"5.5.10\"", coverageStep);
        Assert.Contains(
            "-reporttypes:Html;Cobertura;MarkdownSummaryGithub;TextSummary",
            coverageStep);
        Assert.Contains("-assemblyfilters:+SSC;+SSC.Generators", coverageStep);
        Assert.Contains("coverage/report/index.html", workflow);
        Assert.Contains("coverage/report/Cobertura.xml", workflow);
        Assert.Contains("reportgenerator.stdout.log", coverageStep);
        Assert.Contains("reportgenerator.stderr.log", coverageStep);
        Assert.Contains("coverage-inputs.txt", coverageStep);
        Assert.Contains("Pull request head:", coverageStep);
        Assert.Contains("$GITHUB_STEP_SUMMARY", coverageStep);
        Assert.Contains("path: artifacts/test-results", uploadStep);
    }

    /// <summary>
    /// テスト成功時にcoverageが欠落した場合、workflowが成功扱いしないことを確認します。
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_FailsWhenSuccessfulTestsProduceNoCoverage()
    {
        var workflowPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "pr-xunit-tests.yml");
        var workflow = File.ReadAllText(workflowPath);
        var coverageStep = GetStepBlock(
            workflow,
            "Generate merged code coverage report");

        Assert.Contains("TEST_STEP_OUTCOME: ${{ steps.tests.outcome }}", coverageStep);
        Assert.Contains("if [[ \"$TEST_STEP_OUTCOME\" == \"success\" ]]", coverageStep);
        Assert.Contains("No raw Cobertura coverage files were available.", coverageStep);
        Assert.Contains("exit 1", coverageStep);
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

    private static string GetStepBlock(string workflow, string stepName)
    {
        var stepHeader = $"      - name: {stepName}";
        var startIndex = workflow.IndexOf(stepHeader, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Could not locate workflow step '{stepName}'.");

        var nextStepIndex = workflow.IndexOf(
            "\n      - name:",
            startIndex + stepHeader.Length,
            StringComparison.Ordinal);
        return nextStepIndex < 0
            ? workflow[startIndex..]
            : workflow[startIndex..nextStepIndex];
    }
}
