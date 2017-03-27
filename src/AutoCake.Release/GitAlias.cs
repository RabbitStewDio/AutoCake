using Cake.Common.Tools.GitVersion;
using Cake.Core;
using Cake.Core.IO;

public static class GitAlias
{
    // Mono cake compiler does not like enums in this code. 
    public const int ResetTypeDefault = 0;

    public const int ResetTypeSoft = 1;

    public const int ResetTypeHard = 2;

    public static void DeleteTag(ICakeContext context, string tagName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("tag");
        builder.Append("-d");
        builder.AppendQuoted(tagName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Tag(ICakeContext context, string tagName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("tag");
        builder.AppendQuoted(tagName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void CheckoutDetached(ICakeContext context, string branchName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("checkout");
        builder.Append("--detach");
        builder.AppendQuoted(branchName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Checkout(ICakeContext context, string branchName, bool force = false,
        GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("checkout");
        builder.AppendQuoted(branchName);
        if (force)
            builder.Append("-f");

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Branch(ICakeContext context, string branchName, bool force = false,
        GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("checkout");
        builder.Append("-b");
        builder.AppendQuoted(branchName);
        if (force)
            builder.Append("-f");

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void DeleteBranch(ICakeContext context, string branchName, bool force = false,
        GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("branch");
        builder.Append("-d");
        if (force)
            builder.Append("-f");

        builder.AppendQuoted(branchName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Merge(ICakeContext context, string branchName, bool allowFF = false,
        GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("merge");
        if (!allowFF)
            builder.Append("--no-ff");

        builder.AppendQuoted(branchName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void MergeOurs(ICakeContext context, string branchName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("merge");
        builder.Append("-s");
        builder.Append("ours");
        builder.AppendQuoted(branchName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    /// A merge that forces the contents of the release-branch into master while preserving master as a series of merges. This never fails.
    /// This is also a safe operation (in our git-flow context here), as master is the representation of releases and is fed from release-branches.
    /// Those release branches are simultaniously merged both into master and develop.
    /// 
    /// This merge script is based on the answer in StackOverflow here:
    /// http://stackoverflow.com/a/27338013
    public static void MergeRelease(ICakeContext context, string stageBranchName, GitToolSettings settings = null)
    {
        var assertedVersions = context.GitVersion(new GitVersionSettings {OutputType = GitVersionOutput.Json});

        var releaseBranch = assertedVersions.BranchName;

        Checkout(context, releaseBranch, true, settings);

        // Do a merge commit. The content of this commit does not matter, so use a strategy that never fails.
        // Note: This advances branchA.
        MergeOurs(context, stageBranchName, settings);

        // # Change working tree and index to desired content.
        // # --detach ensures branchB will not move when doing the reset in the next step.
        CheckoutDetached(context, stageBranchName, settings);

        // # Move HEAD to branchA without changing contents of working tree and index.
        Reset(context, ResetTypeSoft, releaseBranch, settings);

        // # 'attach' HEAD to branchA. # This ensures branchA will move when doing 'commit --amend'.
        Checkout(context, releaseBranch, false, settings);

        // # Change content of merge commit to current index (i.e. content of branchB).
        CommitAmend(context, settings);
    }

    public static void Push(ICakeContext context, string remoteName, string branchName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("push");
        builder.AppendQuoted(remoteName);
        builder.AppendQuoted(branchName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void PushTag(ICakeContext context, string remoteName, string tagName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("push");
        builder.Append("--tags");
        builder.AppendQuoted(remoteName);
        builder.AppendQuoted(tagName);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void CommitAmend(ICakeContext context, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("commit");
        builder.Append("--amend");
        builder.Append("-C");
        builder.Append("HEAD");

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Commit(ICakeContext context, string message, bool all = false, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("commit");
        if (all)
            builder.Append("-a");

        builder.Append("-m");
        builder.AppendQuoted(message);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Add(ICakeContext context, string file, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("add");
        builder.AppendQuoted(file);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Log(ICakeContext context, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("log");

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static void Reset(ICakeContext context, int type = ResetTypeDefault, string id = null,
        GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("reset");
        if (type == ResetTypeHard)
            builder.Append("--hard");
        else if (type == ResetTypeSoft)
            builder.Append("--soft");

        if (!string.IsNullOrEmpty(id))
            builder.AppendQuoted(id);

        var tool = new GitTool(context);
        tool.RunGit(settings ?? new GitToolSettings(), builder);
    }

    public static bool CheckUncommitedChanges(ICakeContext context, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("diff");
        builder.Append("--quiet");
        builder.Append("--exit-code");

        var tool = new GitTool(context);
        return tool.RunGitCheck(settings ?? new GitToolSettings(), builder) == 0;
    }

    public static bool CheckBranchExists(ICakeContext context, string branchName, GitToolSettings settings = null)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("show-ref");
        builder.Append("--quiet");
        builder.Append("--heads");
        builder.AppendQuoted(branchName);

        var tool = new GitTool(context);
        return tool.RunGitCheck(settings ?? new GitToolSettings(), builder) == 0;
    }
}