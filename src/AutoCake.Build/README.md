# Build scripts

This is a standardized build script that provides reliable builds to 
projects that follow some basic standard conventions. 

The build script will compile the projects for all allowed and defined 
architectures, run tests, build human readable reports and finally build
and publish NuGet packages.

To use these build settings, add the reference to the two main-entry points
to your main script:

    #load "build-scripts/dependencies.cake"
    #load "build-scripts/build-tasks.cake"

You can then add your configuration code in either the setup action
or directly in your script. 

The build script will place all generated binaries in the defined 
output directory:

    ./build-artefacts/compiled/MSIL/Release/Example/Example.dll
    ./build-artefacts/compiled/MSIL/Release/Example.Tests.NUnit2/Example.Tests.NUnit2.dll
    ./build-artefacts/reports/MSIL/Release/Example.Tests.NUnit2/MSIL/Release/Example.Tests.NUnit2.nunit2.html
    ./build-artefacts/tests/MSIL/Release/Example.Tests.NUnit2/MSIL/Release/Example.Tests.NUnit2.nunit2.xml

## Build execution

The build follows a standard build sequence:


    
## Configuration

The build can be configured using a set of public properties on the
`BuildConfig`, `UnitTestActions` and `PublishActions` classes.  

BuildConfig governs global settings related to how projects are built
and where build artefacts are produced.

    BuildConfig.TargetDirectory = "./build-artefacts"
    
When building the project, you have several options to specify what is
going to be built:

1. Use the automatic mode (default)

   This mode is active is neither a solution or a list of projects is 
   given. The build script will attempt to locate a single *.sln file
   in the project directory. 
   
   If more than one *.sln file is found, the build is aborted. We
   cannot be sure whether the build would pick the correct one or whether
   the many solution files would conflict with each other.  

2. Specify the path to a solution

       BuildConfig.Solution = "./src/Example.sln";

   The build script will invoke MSBuild or XBuild against this solution.
   Projects can be contained in may solutions at once. If you have multiple
   solutions in your project, create a solution with all projects for your 
   automated build-process.
    
3. Specify a list of projects.

       BuildConfig.SolutionDir = "./src/";
       BuildConfig.Projects.Add("./src/Example/Example.csproj";
       BuildConfig.Projects.Add("./src/Example/Example.Test.NUnit3.csproj";
       
   This will build the projects in the given order. This mode is very low-level
   and MSBuild may have problems resolving project dependencies if the projects
   are not given in the correct order.
   
   When specifying the projects, also provide the solution directory where
   packages will be resolved to. 
    
### Generating Assembly Properties

The build system allows you to generate a set of AssemblyMetaData properties
for each project recording build information. This feature is disabled by
default.  

To enable this feature, set the property 
`BuildConfig.GeneratedAssemblyMetaDataReference` to the name of the .cs-file 
that will contain the properties. 

The generated information will replace all contents in that file. Therefore
the file should be 
* located in your project's "Properties" folder, 
* included in your project items for compilation
* NOT the main "AssemblyInfo.cs" file.

### Compiling projects

The build system will compile the solution projects with each of the supported
platform architectures. When building a multi-architecture solution, you will
get all binaries for all platforms in `build-artects/compiled/{platform}/{config}/{AssemblyName}`.
This target directory will contain the binaries, any dependencies and all additional
content. 

Due to the incestuous relationship between NuGet and MSBuild, the build script
generates all NuGet packages directly after the compilation of the project.

### Building NuGet packages

NuGet packages are built during the compilation process and placed into 
`build-artects/packages/{platform}/{config}/{AssemblyName}`.

The build script assumes that a project should be packaged into a NuGet package
when there is a nuspec file having the same name as the project file in the 
project directory. So for a project `foo.csproj`, if there is a `foo.nuspec`
file next to the project file, the build will create a nupkg package.

NuGet cannot handle the common case where a solution contains projects built
for both MSIL and a native architecture where the native project depends on
the MSIL package. If the MSIL project does not contain a configuration for
the native architecture, NuGet will not be able to locate binaries and will
fail with 

     Error occurred when processing file '[..]\src\Example\Example.csproj': 
     Unable to find 'Example.dll'. Make sure the project has been built.

When building such solutions, disable `IncludeReferencedProjects` and simply
fill in the dependencies node in the NuSpec file manually. 

Generating the correct package and validating the contents can be a painful
process (that is not helped a bit by NuGets helpful, but barely documented
automatisms). After generating the NuGet package, the build script therefore
extracts the NuSpec file and a list of file contents from the NuPkg file to
aid manual validation and debugging.

You can specify additional nuspec files for the build via

    PublishActions.AdditionalPackages("/path/to/custom.nuspec", null); // use default settings

or

    PublishActions.AdditionalPackages("/path/to/custom.nuspec", new NuGetXPackSettings {
        // provide custom settings settings
    }); 

### Testing

After the project is built, the build script will attempt to run all 
test-assemblies with a suitable test-runner. At the moment, the test
system detects `NUnit2`, `NUnit3`, `XUnit1` and `XUnit2`. The tests
all produce both the native result-xml format as well as a NUnit2 
format. 

The build will fail if any of the tests fail.

MSTest is not supported at this point in time. 

### Generating Documentation is not supported

There are no cross-platform documentation generators or for that part any 
sensible option that would run in a non-Windows host. 

## System Requirements

### Windows 

There are no special requirements on Windows. You obviously need the
relevant development tools, but that's pretty much it.

For a Windows based build server, you need to install the Windows SDK
or Visual Studio (which brings in the Windows-SDK) to get all the build 
tools in the correct version.

### Linux

Most distributions ship a Mono runtime version that is ancient. To actually
get to run modern tools for the build, you will need a newer version of
Mono. Follow the Mono-Projects [installation instructions](http://www.mono-project.com/docs/getting-started/install/linux/#debian-ubuntu-and-derivatives) to install the 
latest stable version.


For Unix, you need DotNet-Core (to get MSBuild), Mono, NuGet and whatever
additional tool you use. Cake has no direct support for invoking DotNet
exe-files via the Mono-Runtime, so you have to add Mono to the LinuxABI
to allow Cake to run external tools. 

The Mono-Project has a [guide for activating the direct execution of DotNet
binaries](http://www.mono-project.com/archived/guiderunning_mono_applications/).


### Target Platform selection

If not defined otherwise, the build script will assume that the build
machine can run all test assemblies. It will detect whether the operating
system is a 64-Bit version and will skip tests if the build machine would
not be able to actually run 64-Bit binaries. 

The script does not attempt to identify whether it runs on an ARM system
and the automatic detection will most likely fail in that case.

Automatism has limits, therefore we allow you to define the supported 
Platforms uing either code

    BuildConfig.Platforms.Add(PlatformTarget.MSIL); 
    BuildConfig.Platforms.Add(PlatformTarget.x86); 
    BuildConfig.Platforms.Add(PlatformTarget.x64); 
    BuildConfig.Platforms.Add(PlatformTarget.ARM); 
    BuildConfig.Platforms.Add(PlatformTarget.Win32); 

or via the "platforms" commandline option. This option takes a semicolon
deliminated string of all supported platforms.

    .\Build.ps1 -platforms x86;x64
    
This defines the available platforms for building the code. If available 
the build-script also scans the solution file to detect available build
configurations.

DotNet nor NuGet have a meaningful way of stating an Operating System requirement
for libraries. If you are building a library for a different operating system,
you most likely wont be able to run the tests. In that case, exclude the 
platform dependent project from the tests via
 
    UnitTestActions.ExcludeFromUnitTests("src/MyWindowsProject.csproj");


## Implementation note

The cake task system makes it rather difficult to write testable complex 
work flows where the same code would need to be executed multiple times in 
a different context. Debugging duplicate code here is not fun either and 
is the same as with Gradle and similar build systems.

The bulk of the build code here is contained in *.cs files. These files are
valid C# code and the standard compiler is able to deal with them.

The cake-tasks provided here direcly call static methods in the build 
helper classes.

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


## Todo

* Add support for DotCover 
* Add StyleCop support
* Investigate how to generate documentation and other sites
