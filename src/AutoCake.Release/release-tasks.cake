
GitVersioningAliases.Configure(Context);
GitFlow.Configure(Context);

//
// Internal tasks. These are building blocks of the public tasks. Internal tasks are marked with the prefix "_".
//
// There is a lot of duplication in the depends-on statements here. This is intentional to make the flow easier to
// understand. Cake will sort out duplicate entries and ensures that everything is executed in the right order.
Task("_Record-Build-State")
  .Does(GitFlow.RecordBuildState);

Task("_Prepare-Staging-Branch")
  .IsDependentOn("_Record-Build-State")
  .IsDependentOn("Verify-No-Uncommited-Changes")
  .Does(GitFlow.PrepareStagingBranch);

Task("_Attempt-Staging-Build")
  .IsDependentOn("_Record-Build-State")
  .IsDependentOn("_Prepare-Staging-Branch")
  .Does(GitFlow.AttemptStagingBuild);

Task("_Finalize-Release-PrepareBranch")
  .IsDependentOn("_Record-Build-State")
  .IsDependentOn("_Prepare-Staging-Branch")
  .Does(GitFlow.FinalizeRelease_PrepareBranchAndMerge);

Task("_Finalize-Release-Build")
  .IsDependentOn("_Finalize-Release-PrepareBranch")
  .Does(GitFlow.FinalizeRelease_Build);

Task("_Finalize-Release-Push")
  .IsDependentOn("_Finalize-Release-Build")
  .WithCriteria(GitFlow.PushTargetDefined)
  .Does(GitFlow.FinalizeRelease_Build);

Task("_Finalize-Release")
  .IsDependentOn("_Attempt-Staging-Build")
  .IsDependentOn("_Finalize-Release-Build")
  .IsDependentOn("_Finalize-Release-Push")
  .OnError(GitFlow.FinalizeRelease_OnError)
  .Finally(GitFlow.FinalizeRelease_CleanUpProcessTag);

Task("_Continue-Development")
  .IsDependentOn("_Finalize-Release")
  .Does(GitFlow.ContinueDevelopment);


Task("Verify-No-Uncommited-Changes")
  .Does(GitFlow.EnsureNoUncommitedChanges);

Task("Create-Staging-Branch")
  .Description("Creates a staging branch from the development branch or updates the  \n" +
               "version information of an existing staging branch and optionally pushes  \n" +
               "the new branch to a remote git-reference")
  .IsDependentOn("Verify-No-Uncommited-Changes")
  .IsDependentOn("_Record-Build-State")
  .IsDependentOn("_Prepare-Staging-Branch")
  .Does(GitFlow.PushStagingBranch);

Task("Validate-Release")
  .Description("Attempts to build the full release but does not change branches or merge the release")
  .IsDependentOn("Verify-No-Uncommited-Changes")
  .IsDependentOn("_Record-Build-State")
  .Does(GitFlow.ValidateBuild);


Task("Attempt-Release")
  .Description("Performs a full release.\n" +
               "This task can either be executed on the release staging branch or the  \n" +
               "develop branch. If it is executed on develop, a new release branch is  \n" +
               "created in the process. The release branch will be deleted if the build  \n" +
               "is successfully released on the release target branch.")
  .IsDependentOn("Verify-No-Uncommited-Changes")
  .IsDependentOn("_Record-Build-State")
  .IsDependentOn("_Attempt-Staging-Build")
  .IsDependentOn("_Finalize-Release")
  .IsDependentOn("_Continue-Development");


GitFlow.RunBuildTarget = () => 
{
  Error("Please configure a build step. Use ");
};

