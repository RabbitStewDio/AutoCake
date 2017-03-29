using System;
using Cake.Common;
using Cake.Common.Tools.GitVersion;
using Cake.Core;
using Cake.Core.Diagnostics;

public static class GitFlow
{
    public static string StagingBranchPattern { get; set; }

    public static string VersionTagPattern { get; set; }

    public static Action RunBuildTarget { get; set; }

    public static string ReleaseTargetBranch { get; set; }

    public static string DevelopBranch { get; set; }

    public static string PushTarget { get; set; }

    public static ICakeContext Context { get; private set; }

    public static bool PushTargetDefined
    {
        get { return !string.IsNullOrEmpty(PushTarget); }
    }

    public static BuildState State { get; set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
        StagingBranchPattern = "release-{0}";
        VersionTagPattern = "v{0}";
        ReleaseTargetBranch = context.Argument("release", "master");
        DevelopBranch = context.Argument("dev", "develop");
        PushTarget = context.Argument<string>("push", null);
    }

    public static void EnsureNoUncommitedChanges()
    {
        if (!GitAlias.CheckUncommitedChanges(Context))
            throw new Exception(
                "There are uncommited changes in the working tree. Please commit or remove these before proceeding.");
    }

    public static void PrepareStagingBranch()
    {
        if (State == null)
            throw new ArgumentNullException("state");

        var versionInfo = GitVersioningAliases.FetchVersion();
        if (versionInfo.BranchName == ReleaseTargetBranch)
            throw new Exception(
                "Cannot initiate a release from the release-target branch. Switch to a develop or release-xxx branch.");

        // if on development branch, create a release branch.
        // if you work in a support-xx branch, treat it as your develop-branch.
        var stageBranchName = State.StagingBranch;
        if (versionInfo.BranchName == DevelopBranch)
        {
            if (GitAlias.CheckBranchExists(Context, stageBranchName))
            {
                Context.Log.Information("Switching to staging branch from " + versionInfo.BranchName + " as branch " +
                                        stageBranchName);
                GitAlias.Checkout(Context, stageBranchName);
            }
            else
            {
                Context.Log.Information("Creating new staging branch from " + versionInfo.BranchName + " as branch " +
                                        stageBranchName);
                GitAlias.Branch(Context, stageBranchName);

                // We created a new branch, and that branch may be a long living branch. So lets change the version number
                // information once for our developers.
                UpdateVersionNumbers("staging");
            }
        }
        else
        {
            if (versionInfo.BranchName != stageBranchName)
                throw new Exception(
                    "This command must be exist run from the development branch or an active release branch.");
        }
    }

    public static void PushStagingBranch()
    {
        var versionInfo = GitVersioningAliases.FetchVersion();
        var stageBranchName = string.Format(StagingBranchPattern, versionInfo.MajorMinorPatch);
        if (!string.IsNullOrEmpty(PushTarget))
        {
            Context.Log.Information("Publishing staging branch to public source repository.");
            GitAlias.Push(Context, PushTarget, stageBranchName);
        }
    }

    public static void UpdateVersionNumbers(string target)
    {
        var versionInfo = GitVersioningAliases.FetchVersion();
        Context.Log.Information("Updating version information for all assemblies");
        Context.GitVersion(new GitVersionSettings
        {
            UpdateAssemblyInfo = true,
            LogFilePath = "console",
            OutputType = GitVersionOutput.BuildServer
        });

        GitAlias.Commit(Context, "Updating version info for " + target + " branch " + versionInfo.SemVer, true);
    }

    public static void ValidateBuild()
    {
        if (RunBuildTarget == null)
            throw new Exception("RunBuildTarget action is not configured.");

        EnsureNoUncommitedChanges();

        Context.Log.Information("Running target build script.");
        RunBuildTarget();

        Context.Log.Information("Restoring original assembly version files.");
        GitAlias.Reset(Context, GitAlias.ResetTypeHard);
        EnsureNoUncommitedChanges();
    }

    public static void DefaultBuildAction()
    {
        CakeRunnerAlias.RunCake(Context);
    }

    /// <summary>
    ///  These properties are only valid during a build.
    /// </summary>
    public static bool IsStagingBuild
    {
        get
        {
            if (State == null)
                throw new ArgumentNullException("state");

            Context.Log.Verbose("Validating that the build is on the release branch.");
            var versionInfo = GitVersioningAliases.FetchVersion();
            return versionInfo.BranchName == State.StagingBranch;
        }
    }

    /// <summary>
    ///  These properties are only valid during a build.
    /// </summary>
    public static bool IsReleaseBuild
    {
        get
        {
            if (State == null)
                throw new ArgumentNullException("state");

            Context.Log.Verbose("Validating that the build is on the release branch.");
            var versionInfo = GitVersioningAliases.FetchVersion();
            return versionInfo.BranchName == ReleaseTargetBranch;
        }
    }

    public static void EnsureOnReleaseBranch()
    {
        if (State == null)
            throw new ArgumentNullException("state");

        Context.Log.Verbose("Validating that the build is on the release branch.");
        var versionInfo = GitVersioningAliases.FetchVersion();
        if (versionInfo.BranchName != State.StagingBranch)
            throw new Exception(
                "Not on the release branch. Based on the current version information I expect to be on branch '" +
                State.StagingBranch + "'");
    }

    public static void RecordBuildState()
    {
        EnsureNoUncommitedChanges();

        State = new BuildState();
    }

    public static void ValidateBuildState()
    {
        if (State == null)
            throw new ArgumentNullException("state");

        GitAlias.CheckBranchExists(Context, DevelopBranch);
        GitAlias.CheckBranchExists(Context, ReleaseTargetBranch);
    }

    public static void AttemptStagingBuild()
    {
        // We are now supposed to be on the release branch.
        EnsureOnReleaseBranch();

        Context.Log.Information("Building current release as release candidate.");
        ValidateBuild();
    }

    public static void FinalizeRelease_OnError(Exception e)
    {
        Context.Log.Error(
            "Error: Unable to build the release on the release branch. Attempting to roll back changes on release branch.");
        GitAlias.Reset(Context, GitAlias.ResetTypeHard, State.ProcessTag);
        GitAlias.Checkout(Context, DevelopBranch);
        throw new Exception("Unexpected error", e);
    }

    public static void FinalizeRelease_CleanUpProcessTag()
    {
        if (State != null)
            GitAlias.DeleteTag(Context, State.ProcessTag);
    }

    public static void FinalizeRelease_PrepareBranchAndMerge()
    {
        EnsureOnReleaseBranch();

        Context.Log.Information("Merging staging branch " + State.StagingBranch + " into " + ReleaseTargetBranch);
        GitAlias.Checkout(Context, ReleaseTargetBranch);

        GitAlias.Tag(Context, State.ProcessTag);

        // We always want to ensure that release-branch (master) now matches 100% with the staging branch (release-1.0.0).
        // We therefore do not use a normal merge, which can end in conflicts and so on.
        GitAlias.MergeRelease(Context, State.StagingBranch);
        UpdateVersionNumbers("release");
    }

    public static void FinalizeRelease_Build()
    {
        Context.Log.Information("Building final release with released version information.");
        ValidateBuild();

        // wait with tagging until after we know that the build is valid.
        GitAlias.Tag(Context, State.VersionTag);
    }

    public static void FinalizeRelease_Push()
    {
        if (!string.IsNullOrEmpty(PushTarget))
        {
            Context.Log.Information("Publishing release to public source repository.");
            GitAlias.Push(Context, PushTarget, ReleaseTargetBranch);
            GitAlias.PushTag(Context, PushTarget, State.VersionTag);
        }
    }

    public static void FinalizeRelease()
    {
        try
        {
            FinalizeRelease_PrepareBranchAndMerge();
            FinalizeRelease_Build();
            FinalizeRelease_Push();
        }
        catch (Exception e)
        {
            FinalizeRelease_OnError(e);
        }
        finally
        {
            FinalizeRelease_CleanUpProcessTag();
        }
    }

    public static void ContinueDevelopment()
    {
        Context.Log.Information("Merging " + State.StagingBranch + " back into development branch " + DevelopBranch);
        GitAlias.Checkout(Context, DevelopBranch);

        // This is an ordinary merge. Release can be a long living branch with additional changes. In a good environment, develop
        // should have all functional changes that the release-branch had, as patches should have been merged or cherry-picked into both branches.
        // If development on the next release continues while the release branch hardens, we dont want to loose those changes.
        GitAlias.Merge(Context, State.StagingBranch);

        UpdateVersionNumbers("development");

        // finally, delete the release branch. We dont need it anymore ..
        GitAlias.DeleteBranch(Context, State.StagingBranch);
    }

    public static void AttemptRelease()
    {
        RecordBuildState();
        PrepareStagingBranch();

        AttemptStagingBuild();

        FinalizeRelease();

        ContinueDevelopment();
    }

    public class BuildState
    {
        public BuildState()
        {
            Version = GitVersioningAliases.FetchVersion();
            StartingBranch = Version.BranchName;
            ProcessTag = "before-release-" + Guid.NewGuid();
        }

        public GitVersion Version { get; set; }

        public string StartingBranch { get; set; }

        public string VersionTag
        {
            get { return string.Format(VersionTagPattern, Version.MajorMinorPatch); }
        }

        public string StagingBranch
        {
            get { return string.Format(StagingBranchPattern, Version.MajorMinorPatch); }
        }

        public string ProcessTag { get; private set; }
    }
}