namespace SSC.Unit.Tests;

/// <summary>
/// pull request workflow のテスト artifact 契約を検証します。
/// </summary>
public sealed class GitHubActionsTestArtifactContractUnitTests
{
    /// <summary>
    /// pull request workflow がレビュー用の保持期間付き診断 artifact を公開することを確認します。
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_PublishesRetainedDiagnosticArtifactForChatGptReview()
    {
        var workflowPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "pr-xunit-tests.yml");
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("Prepare test diagnostic artifact", workflow);
        Assert.Contains("-generator-restore.stdout.log", workflow);
        Assert.Contains("-generator-restore.stderr.log", workflow);
        Assert.Contains("-generator-build.stdout.log", workflow);
        Assert.Contains("-generator-build.stderr.log", workflow);
        Assert.Contains("--logger \"trx;LogFileName=", workflow);
        Assert.Contains("--results-directory \"$results_dir\"", workflow);
        Assert.Contains("-restore.stdout.log", workflow);
        Assert.Contains("-restore.stderr.log", workflow);
        Assert.Contains("-test.stdout.log", workflow);
        Assert.Contains("-test.stderr.log", workflow);
        Assert.Contains("dotnet-info.stdout.log", workflow);
        Assert.Contains("dotnet-info.stderr.log", workflow);
        Assert.Contains("git-status.stdout.log", workflow);
        Assert.Contains("git-status.stderr.log", workflow);
        Assert.Contains("runner-context.stdout.log", workflow);
        Assert.Contains("project-list.stdout.log", workflow);
        Assert.Contains(
            "PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}",
            workflow);
        Assert.Contains("Pull request head:", workflow);
        Assert.Contains("actions/upload-artifact@v4", workflow);
        Assert.Contains(
            "if: ${{ always() && (steps.discover.outputs.has_tests == 'true' || steps.discover.outputs.has_generators == 'true') }}",
            workflow);
        Assert.Contains("retention-days: 7", workflow);
        Assert.Contains("manifest.md", workflow);
        Assert.Contains("ChatGPT-assisted review", workflow);
        Assert.Contains("explicitly approved", workflow);
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
}
