# Release scripts

This package contains a git-flow based release template.
Starting from either the develop branch or an release-branch,
this script will attempt to validate a release build. 

The script uses GitVersion to calculate the release version number 
and takes care of all the necessary branching and tagging during 
the build.

Building happens via a Cake action, and normally simply calls
out to the standard 'build.cake' file for the project.

If successful, it will merge that release into the master-branch and 
rebuild the actual release to create the release artefacts. On success of 
that step, the release branch will be merged back into the develop branch 
to start the next development cycle.

If the release fails, this script will undo all changes to master, but will 
leave the release branch open so that the release can be fixed. Subsequent 
release attempts will reuse the existing release branch, even if started 
from the develop branch.

Start a release by invoking

  .\build.[ps1|sh] -script release-scripts\release.cake

## Configuration

The default configuration assumes that a git-flow branching model is used. 
The model assumes that the development branch is called "develop", release 
branches are named "release-a.b.c" and the final release is pushed into 
"master".

The script understands several arguments:

  -release <branch-name> 
    
   The target branch that will receive the finished release.
   Defaults to "master". Set to "support-a.x" when building patch
   releases for older product versions.

  -dev <branch-name>
  
   The name of the development branch where feature branches get
   merged to. This branch will be the basis for the release-a.b.c
   branches.
   
  -push <remote>
  
   The name of a GIT remote repository identifier (eg. "origin").
   If given, then both the final release and the associated tags
   with that release will be pushed there.
  

### Configuring the build action

The default example code here will attempt to build your code using a default 
"build.cake" script. The code that invokes the build is defined by submitting 
an action to `GitFlow.RunBuildTarget`.     

The CakeRunnerAlias#RunCake method allows you to invoke an external cake 
script with custom parameter. The invokation will inherit all arguments given 
to the release-script, except for the `target` argument.

If you want to remove an argument from this inheritance, set the argument to 
an empty string.

    CakeRunnerAlias.RunCake(Context, "./build.cake", new CakeSettings() 
        {
            Arguments = 
            {
                {"target", "Default"}
                {"remove-this-argument", ""}
            }
        });
   
## Implementation note

The cake task system makes it rather difficult to write testable complex 
work flows where the same code would need to be executed multiple times in 
a different context. Debugging duplicate code here is not fun either and 
is the same as with Gradle and similar build systems.

The bulk of the build code here is contained in *.cs files. These files are
valid C# code and the standard compiler is able to deal with them.

The cake-tasks provided here direcly call static methods in the build helper 
classes.

There are a few caveats when editing these classes, due to the fact that
Cake runs them as scripts at runtime.

* Be aware that the Cake script compiler does not allow newer C# 6.0 features 
and that the Mono based parser easily stumbles over syntacticall valid constructs.
Don't trust the IDE's compiler and have a final test with Cake itself.
* Do not declare namespaces. The script mode compiler does not support this.
* Do not use `'\"'` as a constant - the Mono parser does not like it. 
* Do not use `nameof(..)`, this is a C# 6 feature.
* Do not declare extension methods. They are not supported. This means you 
cannot define Cake-Aliases. You have to pass the ICakeContext instance manually into the classes.
 
After making changes to these files in your IDE and compiling it there, 
it is a good idea to additionally check them via a Cake dryrun with both the 
MS DotNet runtime and the Mono runtime. They *will* flag up additional errors. 
