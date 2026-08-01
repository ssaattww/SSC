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
    /// スマートフォン向け単一HTMLを専用branchへ保存し、GitHub Pagesへ公開することを確認します。
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_PublishesMobileReportWithoutChangingPullRequestHead()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(
            repositoryRoot,
            ".github",
            "workflows",
            "pr-xunit-tests.yml");
        var generatorPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "generate-mobile-coverage-report.py");
        var workflow = File.ReadAllText(workflowPath);
        var testJob = GetJobBlock(workflow, "dotnet-tests");
        var publishJob = GetJobBlock(workflow, "publish-coverage-report");
        var coverageStep = GetStepBlock(
            workflow,
            "Generate merged code coverage report");

        Assert.True(File.Exists(generatorPath));
        Assert.Contains("Checkout PR head", testJob);
        Assert.Contains(
            "ref: ${{ github.event.pull_request.head.sha || github.sha }}",
            testJob);
        Assert.Contains("contents: read", testJob);
        Assert.DoesNotContain("git push", testJob);
        Assert.Contains("generate-mobile-coverage-report.py", coverageStep);
        Assert.Contains("coverage/mobile/code-coverage.html", workflow);
        Assert.Contains("actions/upload-pages-artifact@v4", testJob);
        Assert.Contains("path: artifacts/pages", testJob);
        Assert.Contains("needs: dotnet-tests", publishJob);
        Assert.Contains(
            "github.event.pull_request.head.repo.full_name == github.repository",
            publishJob);
        Assert.Contains("contents: write", publishJob);
        Assert.Contains("pages: write", publishJob);
        Assert.Contains("id-token: write", publishJob);
        Assert.Contains("environment:", publishJob);
        Assert.Contains("name: github-pages", publishJob);
        Assert.Contains("actions/download-artifact@v4", publishJob);
        Assert.Contains("REPORT_BRANCH: coverage-pages", publishJob);
        Assert.Contains("target_report=\"index.html\"", publishJob);
        Assert.Contains("EXPECTED_HEAD_SHA", publishJob);
        Assert.Contains("stale report will not be published", publishJob);
        Assert.Contains("git switch --orphan \"$REPORT_BRANCH\"", publishJob);
        Assert.Contains("git push origin \"HEAD:$REPORT_BRANCH\"", publishJob);
        Assert.Contains("actions/configure-pages@v5", publishJob);
        Assert.Contains("actions/deploy-pages@v4", publishJob);
        Assert.Contains("group: coverage-pages", publishJob);
        Assert.DoesNotContain("reports/pr-$PR_NUMBER", publishJob);
        Assert.DoesNotContain("HEAD:$PR_HEAD_REF", publishJob);
        Assert.DoesNotContain("htmlpreview.github.io", workflow);
        Assert.DoesNotContain("\n  push:", workflow);
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

    private static string GetJobBlock(string workflow, string jobName)
    {
        var jobHeader = $"  {jobName}:";
        var startIndex = workflow.IndexOf(jobHeader, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Could not locate workflow job '{jobName}'.");

        var nextJobIndex = workflow.IndexOf(
            "\n  ",
            startIndex + jobHeader.Length,
            StringComparison.Ordinal);
        while (nextJobIndex >= 0
               && nextJobIndex + 3 < workflow.Length
               && char.IsWhiteSpace(workflow[nextJobIndex + 3]))
        {
            nextJobIndex = workflow.IndexOf(
                "\n  ",
                nextJobIndex + 3,
                StringComparison.Ordinal);
        }

        return nextJobIndex < 0
            ? workflow[startIndex..]
            : workflow[startIndex..nextJobIndex];
    }
}
