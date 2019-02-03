BuildConfig.Configure(Context);
BuildActions.Configure(Context);
PublishActions.Configure(Context);
ReportActions.Configure(Context);
UnitTestActions.Configure(Context);
UnitTestActions.SkipTests = true;
XBuildHelper.Configure(Context);
TaskActions.Configure(Context, Tasks);
UnityBuildActions.Configure(Context);

Task("_Init")
    .Does(UnityBuildActions.Initialize);

Task("_Clean-Binaries")
    .Does(UnityBuildActions.CleanBinaries);

Task("_Clean-Artefacts")
    .Does(BuildActions.CleanArtefacts);

Task("Clean")
    .Description("Removes all build artefacts and resolved packages.")
    .IsDependentOn("_Clean-Artefacts")
    .IsDependentOn("_Clean-Binaries");

Task("Compile")
    .IsDependentOn("_Init")
    .Description("Compiles the project.")
    .Does(UnityBuildActions.CompileProject);

Task("Test")
    .Description("Runs all unit-tests.")
    .IsDependentOn("_Init")
    .IsDependentOn("Compile")
    .WithCriteria(UnitTestActions.SkipTests == false)
    .Does(UnitTestActions.RunTests);

Task("Package")
    .Description("Creates a ZIP file of the build output.")
    .IsDependentOn("_Init")
    .IsDependentOn("Compile")
    .Does(UnityBuildActions.PackageProject);


    
Task("Build")
    .IsDependentOn("_Init")
    .IsDependentOn("Clean")
    .IsDependentOn("Compile")
    .IsDependentOn("Test")
    .IsDependentOn("Package");
