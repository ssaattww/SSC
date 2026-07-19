namespace SSC.Unit.Tests;

/// <summary>
/// pull request workflow のテスト artifact 契約を検証します。
/// </summary>
public sealed class GitHubActionsTestArtifactContractUnitTests
{
    /// <summary>
    /// pull request workflow がレビュー用の保持期間付き TRX artifact を公開することを確認します。
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_PublishesRetainedTrxArtifactForChatGptReview()
    {
        var workflowPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "pr-xunit-tests.yml");
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("--logger \"trx;LogFileName=", workflow);
        Assert.Contains("--results-directory \"$results_dir\"", workflow);
        Assert.Contains("actions/upload-artifact@v4", workflow);
        Assert.Contains(
            "if: ${{ always() && steps.discover.outputs.has_tests == 'true' }}",
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
