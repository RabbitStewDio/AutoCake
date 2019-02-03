#addin Cake.Unity
#load "src/AutoCake.Build/dependencies.cake"
#load "src/AutoCake.Build/build-tasks.cake"

BuildConfig.GeneratedAssemblyMetaDataReference = "BuildMetaDataAssemblyInfo.cs";
PublishActions.PackSettings.Symbols = true;
PublishActions.PackSettings.Tool = true;
PublishActions.PackSettings.IncludeReferencedProjects = false;
PublishActions.PackSettings.Verbosity = NuGetVerbosity.Detailed;

/// NuGet push to the symbol server always returns 400
/// There is no documentation why that happens, nor any other help.
/// Therefore disable that broken thing so that at least the normal
/// packages can go through.
PublishActions.PushSettings.NoSymbols = true;

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
RunTarget(target);
