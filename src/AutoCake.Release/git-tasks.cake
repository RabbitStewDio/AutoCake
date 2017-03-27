
GitVersioningAliases.Configure(Context);

Task("Fetch-Version")
    .Does(() =>
{
    var versionInfo = GitVersioningAliases.FetchVersion();

    Information("Retrieved Version Information from Git");
    Information("  Version: " + versionInfo.MajorMinorPatch);
    Information("  Tag    : " + versionInfo.PreReleaseTag);
    Information("  NuGet  : " + versionInfo.NuGetVersion);
    Information("  Full   : " + versionInfo.InformationalVersion);
    Information("  Branch : " + versionInfo.BranchName);
    Information("  Sha1   : " + versionInfo.Sha);
    
});
    
Task("Apply-Version")
    .IsDependentOn("Fetch-Version")
    .Does(() =>
{
    GitVersion(new GitVersionSettings
    {
        UpdateAssemblyInfo = true,
        LogFilePath = "console",
        OutputType = GitVersionOutput.BuildServer
    });
});
