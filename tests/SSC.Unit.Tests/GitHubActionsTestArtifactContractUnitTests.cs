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
        const string uploadStepName = "Upload .NET test results for ChatGPT review";
        const string uploadCondition =
            "if: ${{ always() && (steps.discover.outputs.has_tests == 'true' || steps.discover.outputs.has_generators == 'true') }}";
        var uploadStep = GetStepBlock(workflow, uploadStepName);

        Assert.Contains("Prepare test diagnostic artifact", workflow);
        Assert.Contains("mkdir -p \"$results_dir/logs\"", workflow);
        Assert.Contains("logs_dir=\"$GITHUB_WORKSPACE/artifacts/test-results/logs\"", workflow);
        Assert.Contains("-generator-restore.stdout.log", workflow);
        Assert.Contains("-generator-restore.stderr.log", workflow);
        Assert.Contains("-generator-build.stdout.log", workflow);
        Assert.Contains("-generator-build.stderr.log", workflow);
        Assert.Contains("--logger \"trx;LogFileName=", workflow);
        Assert.Contains(
            "project_results_dir=\"$results_dir/test-runs/$result_name\"",
            workflow);
        Assert.Contains("--results-directory \"$project_results_dir\"", workflow);
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
        Assert.Contains("Workflow commit:", workflow);
        Assert.Contains(
            "PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}",
            workflow);
        Assert.Contains("Pull request head:", workflow);
        Assert.Contains("find \"$results_dir\" -type f ! -name manifest.md", workflow);
        Assert.Contains($"- name: {uploadStepName}", uploadStep);
        Assert.Contains(uploadCondition, uploadStep);
        Assert.Contains("uses: actions/upload-artifact@v4", uploadStep);
        Assert.Contains("path: artifacts/test-results", uploadStep);
        Assert.Contains("retention-days: 7", workflow);
        Assert.Contains("manifest.md", workflow);
        Assert.Contains("ChatGPT-assisted review", workflow);
        Assert.Contains("explicitly approved", workflow);
    }

    /// <summary>
    /// pull request workflow がtracked checkout source archiveとGit metadataを診断artifact配下へ保存することを確認します。
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_PreservesTrackedCheckoutSourceArchiveInDiagnosticArtifact()
    {
        var workflowPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "pr-xunit-tests.yml");
        var workflow = File.ReadAllText(workflowPath);
        const string sourceStepName = "Preserve checked-out source";
        const string uploadStepName = "Upload .NET test results for ChatGPT review";
        var sourceStep = GetStepBlock(workflow, sourceStepName);
        var uploadStep = GetStepBlock(workflow, uploadStepName);

        Assert.Contains($"- name: {sourceStepName}", sourceStep);
        Assert.Contains(
            "if: ${{ always() && (steps.discover.outputs.has_tests == 'true' || steps.discover.outputs.has_generators == 'true') }}",
            sourceStep);
        Assert.Contains(
            "source_dir=\"$GITHUB_WORKSPACE/artifacts/test-results/source\"",
            sourceStep);
        Assert.Contains(
            "source_archive=\"$source_dir/checked-out-source.tar\"",
            sourceStep);
        Assert.Contains(
            "git archive --format=tar HEAD > \"$source_archive\"",
            sourceStep);
        Assert.Contains(
            "git rev-parse HEAD > \"$source_dir/checked-out-head.txt\"",
            sourceStep);
        Assert.Contains(
            "git status --short --untracked-files=no > \"$source_dir/git-status.txt\"",
            sourceStep);
        Assert.DoesNotContain("git archive --format=tar HEAD |", sourceStep);
        Assert.DoesNotContain("tar -xf", sourceStep);
        Assert.Contains("path: artifacts/test-results", uploadStep);
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
