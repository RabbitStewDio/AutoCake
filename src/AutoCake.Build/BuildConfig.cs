using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Solution;
using Cake.Common.Tools.MSBuild;
using Cake.Common.Tools.NuGet.Restore;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

/// <summary>
///     This is a static holder for all build related configuration properties. Project/Solution related parts of this
///     configuration
///     are compiled and sanitized into a EffectiveBuildConfiguration.
/// </summary>
public static class BuildConfig
{
    public static DirectoryPath ComputeOutputPath(string type, ParsedProject project)
    {
        return Context.Directory(string.Format("{0}/{4}/{1}/{2}/{3}",
            TargetDirectory,
            project.Platform,
            Config.Configuration,
            project.Project.AssemblyName,
            type));
    }

    public static string AsRelativePath(FilePath p)
    {
        var abs = p.MakeAbsolute(Context.Environment);
        return Context.Environment.WorkingDirectory.GetRelativePath(abs).ToString();
    }

    public static string AsRelativePath(DirectoryPath p)
    {
        var abs = p.MakeAbsolute(Context.Environment);
        return Context.Environment.WorkingDirectory.GetRelativePath(abs).ToString();
    }

    #region Public members - as no one wants to read the boring bits.

    public const string DefaultGeneratedAssemblyMetaDataTemplate = @"
#region Build script generated code

{0}

#endregion
";

    /// <summary>
    ///     Specifies the target directory that will hold all build artefacts.
    ///     This defaults to "./build-artefacts".
    /// </summary>
    public static DirectoryPath TargetDirectory { get; set; }

    /// <summary>
    ///     Force the use of XBuild on Windows. This will enforce the use of
    ///     Mono for compilation and is a cheap way of detecting the use of
    ///     APIs that are not supported by Mono.
    /// </summary>
    public static bool UseXBuildOnWindows { get; set; }

    /// <summary>
    ///     Defines the list of supported target platforms. This list will be
    ///     used to filter project platforms when compiling solutions and projects.
    ///     It is OK to list all platforms supported by the build system here.
    ///     If none of the projects contains a build-configuration for the
    ///     given Platform/Configuration combination, the entry will be ignored.
    /// </summary>
    public static List<PlatformTarget> Platforms { get; private set; }

    /// <summary>
    ///     Allows to customize the generated assembly template for the build-metadata
    ///     attributes. Defaults to a single region declaration.
    ///     <see cref="DefaultGeneratedAssemblyMetaDataTemplate" />
    /// </summary>
    public static string GeneratedAssemblyMetaDataTemplate { get; set; }

    /// <summary>
    ///     The name of the file holding assembly level attributes.
    ///     See the documentation section on "Generating Assembly Properties"
    ///     for details on this feature.
    /// </summary>
    public static string GeneratedAssemblyMetaDataReference { get; set; }

    /// <summary>
    ///     A set of properties. When writing the metadata, the properties will
    ///     be sorted alphabetically using the key values given here.
    /// </summary>
    public static Dictionary<string, string> AssemblyProperties { get; private set; }

    /// <summary>
    ///     A generator for a set of per-project properties. When writing the
    ///     metadata, the properties will be sorted alphabetically using the key
    ///     values given here.
    ///     Example:
    ///     <code><![CDATA[
    ///      BuildConfig.ProjectAssemblyProperties = (project, path, metadata) =>
    ///      {
    ///         metadata.Add("ProjectPath", path.ToString());
    ///         metadata.Add("ProjectPlatform", project.Platform.ToString());
    ///      };
    ///     ]]>
    ///    </code>
    /// </summary>
    public static Func<ExtProjectParserResult, FilePath, Dictionary<string, string>, Dictionary<string, string>>
        ProjectAssemblyProperties
    {
        get { return projectAssemblyProperties; }
        set { projectAssemblyProperties = value ?? DefaultAssembliesFunc; }
    }

    /// <summary>
    ///     Allows customisation to the "NuGet restore" command.
    /// </summary>
    public static NuGetRestoreSettings RestoreSettings { get; set; }

    /// <summary>
    ///     The active build configuration. Usually either "Debug" or "Release".
    /// </summary>
    public static string Configuration
    {
        get { return configuration; }
        set
        {
            configuration = value;
            config = null;
        }
    }

    /// <summary>
    ///     The solution directory for restoring packages when manually build *.xxproj
    ///     files instead of complete solutions.
    /// </summary>
    public static DirectoryPath SolutionDir
    {
        get { return solutionDir; }
        set
        {
            solutionDir = value;
            config = null;
        }
    }

    /// <summary>
    ///     A path to a solution (*.sln) file to be built.
    ///     If neither projects nor a solution is given, the build script will attempt to
    ///     locate a solution file in the current directory or any of its sub-directories.
    ///     If more than one solution file is found, the build will abort with an error,
    ///     as we cannot guarantee that the build would pick the correct one.
    /// </summary>
    public static FilePath Solution
    {
        get { return solution; }
        set
        {
            solution = value;
            config = null;
        }
    }

    public static ObservableCollection<FilePath> Projects { get; private set; }

    public static AnyBuildSettings BuildSettings { get; set; }

    public static AnyUnitTestSettings UnitTestSettings { get; set; }

    public static bool Is64BitOS { get; private set; }

    public static ProcessorArchitecture Architecture { get; set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
        Configuration = Context.Argument("configuration", "Release");
        UseXBuildOnWindows = Context.Argument("forcexbuild", false);
        TargetDirectory = Context.Argument("targetdir", TargetDirectory);

        var platform = Context.Argument("platforms", Context.Argument<string>("platforms", null));
        if (platform != null)
        {
            var allPlatforms = platform.Split(';');
            foreach (var p in allPlatforms)
            {
                PlatformTarget parsed;
                if (Enum.TryParse(p, true, out parsed))
                {
                    Platforms.Add(parsed);
                }
                else
                {
                    var strings = Enum.GetNames(typeof(PlatformTarget));
                    throw new ArgumentException(
                        "Not a valid platform specifier. Valid values are " + string.Join(", ", strings) + ".",
                        "platform");
                }
            }
        }
    }

    public static EffectiveBuildConfig Config
    {
        get
        {
            if (config == null)
                config = BuildEffectiveConfiguration();
            return config;
        }
    }

    #endregion

    #region Implementation Details

    static ICakeContext Context { get; set; }

    static Dictionary<string, string> DefaultAssembliesFunc(ExtProjectParserResult p, FilePath f,
        Dictionary<string, string> input)
    {
        return input;
    }

    static BuildConfig()
    {
        // are we technically able to execute 64bit processes?
        // if that returns false, we cannot run unit-tests for that platform.
        Is64BitOS = Environment.Is64BitOperatingSystem;

        Projects = new ObservableCollection<FilePath>();
        Projects.CollectionChanged += (sender, args) => config = null;

        Platforms = new List<PlatformTarget>();

        RestoreSettings = new NuGetRestoreSettings();
        AssemblyProperties = new Dictionary<string, string>();
        ProjectAssemblyProperties = DefaultAssembliesFunc;

        GeneratedAssemblyMetaDataTemplate = DefaultGeneratedAssemblyMetaDataTemplate;

        TargetDirectory = "build-artefacts";
    }

    static List<FilePath> ParseSolution(FilePath solution)
    {
        var solutionParserResult = Context.ParseSolution(solution);

        var effectiveProjects = new List<FilePath>();
        effectiveProjects.AddRange(
            solutionParserResult.Projects.Where(p => p.Type != SolutionFolderTypeId).Select(p => p.Path));
        return effectiveProjects;
    }

    static EffectiveBuildConfig BuildEffectiveConfiguration()
    {
        List<FilePath> effectiveProjects;
        var c = new EffectiveBuildConfig();
        if (Projects != null && Projects.Count > 0)
        {
            Context.Log.Information("Using manually defined projects and solution directory for build.");

            effectiveProjects = new List<FilePath>();
            effectiveProjects.AddRange(Projects);
            if (SolutionDir == null || !Context.DirectoryExists(SolutionDir))
                Context.Warning(
                    "When specifying projects explicitly, provide the solution directory as well to allow auto-cleaning of resolved packages.");
            c.SolutionDir = SolutionDir;
        }
        else if (Solution != null && Context.FileExists(Solution))
        {
            if (SolutionDir != null)
                throw new Exception("When specifying a solution reference, do not specify a solution directory.");

            Context.Log.Information("Using manually defined solution for build: " +
                                    Context.Environment.WorkingDirectory.GetRelativePath(solution));
            effectiveProjects = ParseSolution(Solution);
            c.SolutionDir = Solution.GetDirectory();
            c.Solution = Solution;
        }
        else
        {
            var filePaths = Context.Globber.GetFiles("**/*.sln").ToList();
            switch (filePaths.Count)
            {
                case 0:
                    throw new Exception(
                        "Unable to find a solution file in this directory. Try to specify the solution file manually or list the projects.");
                case 1:
                    Context.Log.Information("Using automatically defined solution for build: " +
                                            Context.Environment.WorkingDirectory.GetRelativePath(filePaths[0]));

                    effectiveProjects = ParseSolution(filePaths[0]);
                    c.Solution = filePaths[0];
                    c.SolutionDir = c.Solution.GetDirectory();
                    break;
                default:
                    throw new Exception("Found more than one solution to build. Please specify the solution explicitly.");
            }
        }

        if (effectiveProjects.Count == 0)
            throw new Exception("No projects to build.");

        c.Configuration = Configuration ?? "Debug";
        c.ProjectFiles = effectiveProjects;
        foreach (var proj in effectiveProjects)
            Context.Log.Information("  Found project " + Context.Environment.WorkingDirectory.GetRelativePath(proj));

        // unless defined otherwise, we attempt to build every supported platform.
        var platforms = Platforms.Count > 0
            ? Platforms
            : new List<PlatformTarget>(new[]
                {PlatformTarget.MSIL, PlatformTarget.x86, PlatformTarget.x64, PlatformTarget.ARM});
        var parseResult = XBuildHelper.ParseProjects(Context, c.Configuration, platforms, c.ProjectFiles);
        c.PlatformBuildOrder = parseResult.PlatformBuildOrder;
        c.ProjectsByPlatform = parseResult.ProjectsByPlatform;

        Context.Log.Information("Project summary by platform type");
        Context.Log.Information("--------------------------------");
        if (c.Solution != null)
            Context.Log.Information("  Solution: " + Context.Environment.WorkingDirectory.GetRelativePath(c.Solution));
        if (c.SolutionDir != null)
            Context.Log.Information("  Solution-Directory: " +
                                    Context.Environment.WorkingDirectory.GetRelativePath(c.SolutionDir));

        foreach (var platform in platforms)
        {
            Context.Log.Information("Platform " + platform);
            List<ParsedProject> list;
            if (!c.ProjectsByPlatform.TryGetValue(platform, out list))
                Context.Log.Information("  <no projects defined>");
            else
                foreach (var project in list)
                    Context.Log.Information("  " +
                                            Context.Environment.WorkingDirectory.GetRelativePath(project.ProjectFile));
        }
        Context.Log.Information("--------------------------------");

        return c;
    }


    public static string ConvertPlatformTargetToString(PlatformTarget target)
    {
        if (PlatformTarget.MSIL == target)
            return "AnyCPU";
        return target.ToString();
    }

    const string SolutionFolderTypeId = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
    static EffectiveBuildConfig config;
    static DirectoryPath solutionDir;
    static FilePath solution;
    static string configuration;

    static Func<ExtProjectParserResult, FilePath, Dictionary<string, string>, Dictionary<string, string>>
        projectAssemblyProperties;

    #endregion
}