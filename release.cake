#load "src/AutoCake.Release/dependencies.cake"
#load "src/AutoCake.Release/release-tasks.cake"
#load "src/AutoCake.Release/git-tasks.cake"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Attempt-Release");

GitFlow.RunBuildTarget = () => 
{
  // See release-scripts/README.md for additional configuration options
  // and details on the syntax of this call.
  var versionInfo = GitVersioningAliases.FetchVersion();
  CakeRunnerAlias.RunCake(Context, new CakeSettings {
        Arguments = new Dictionary<string, string>()
        {
            { "targetdir", "build-artefacts/" + versionInfo.FullSemVer }
        }
  });
};

var target = Argument("target", "Default");
RunTarget(target);
