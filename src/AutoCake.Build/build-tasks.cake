BuildConfig.Configure(Context);
BuildActions.Configure(Context);
PublishActions.Configure(Context);
ReportActions.Configure(Context);
UnitTestActions.Configure(Context);
XBuildHelper.Configure(Context);
TaskActions.Configure(Context, Tasks);

Task("_Clean-Packages")
    .Does(BuildActions.CleanPackages);

Task("_Clean-Binaries")
    .Does(BuildActions.CleanBinaries);

Task("_Clean-Artefacts")
    .Does(BuildActions.CleanArtefacts);

Task("Clean")
    .Description("Removes all build artefacts and resolved packages.")
    .IsDependentOn("_Clean-Artefacts")
    .IsDependentOn("_Clean-Binaries")
    .IsDependentOn("_Clean-Packages");

Task("RestorePackages")
    .Description("Restores all NuGet packages for the solution or projects")
    .Does(BuildActions.RestorePackages);

Task("UpdateBuildMetaData")
    .Description("Generates metadata")
    .Does(BuildActions.UpdateBuildMetaData);

Task("Compile")
    .Description("Compiles the project.")
    .Does(BuildActions.CompileProject);

Task("Test")
    .Description("Runs all unit-tests.")
    .IsDependentOn("Compile")
    .WithCriteria(UnitTestActions.SkipTests == false)
    .Does(UnitTestActions.RunTests);

Task("Coverage")
    .Description("[TODO] Runs all unit-tests with code coverage.")
    .IsDependentOn("Compile")
    .WithCriteria(UnitTestActions.SkipTests == false)
    .Does(UnitTestActions.RunTests);

Task("Report")
    .Description("Generates human readable reports of all tests and validations.")
    .IsDependentOn("Test")
    .IsDependentOn("Coverage")
    .Does(ReportActions.GenerateHtmlTestReports);

Task("Package")
    .Description("Generate additional NuGet packages.")
    .Does(PublishActions.BuildPackages);
    
Task("Publish")
    .Description("Publish all generated NuGet packages.")
    .Does(PublishActions.PublishNuGetPackages);
    
Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("RestorePackages")
    .IsDependentOn("UpdateBuildMetaData")
    .IsDependentOn("Compile")
    .IsDependentOn("Test")
    .IsDependentOn("Package");
