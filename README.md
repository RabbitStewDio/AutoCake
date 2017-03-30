# AutoCake

The auto-cake build system is a set of standardized build scripts that allow
you to build DotNet projects via a Cake build runner. 

The script is intentionally prescriptive and relies on adherance to some 
well-known standards normally found in Visual Studio projects.

# Usage 

AutoCake consists of two parts. 

[`AutoCake.Build`](src/AutoCake.Build/README.md) is responsible for building
a project. Building the project means that any previous output is cleared,
the project is compiled, tested and where defined, NuGet packages are built.

The build-script produces Nunit2 compatible test-reports that can be consumed
by most CI-servers and can publish the NuGet packages to a NuGet server.


[`ÀutoCake.Release`](src/AutoCake.Release/README.md) is a smart wrapper around 
the build script. The release script requires that the project uses Git as 
source versioning tool, that the project follows the GitFlow style of branching 
and that the GitVersion tool has been properly configured for the project.

GitVersion is able to calculate the project's current version based on the 
git commits and tags that it finds in the current repository. 

The release script will create a release-branch (or reuse an existing branch
for the current project version) and attempts to build the project for this
release. If that build succeeds, the release is promoted to the master-branch
and rebuilt to include the final release-version numbers. Once completed, the
release branch is merged back into the development branch and the development
version number is incremented.

# No Linux support

You can use the build script to compile and test your code, but you won't be
able to actually build any NuGet packages.

"NuGet pack" is intimately mixed up with MSBuild internals. NuGet currently 
does not work on Linux, and the MSBuild project does not produce standalone
releases that may work on Linux, therefore it is impossible to actually produce 
NuGet packages in a controlled fashion. 

The alternative "dotnet pack" command makes no mention of honouring nuspec 
files and as usual the documentation is thin on how it works internally.
