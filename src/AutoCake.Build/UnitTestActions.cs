using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.MSBuild;
using Cake.Common.Tools.NUnit;
using Cake.Common.Tools.XUnit;
using Cake.Common.Xml;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;

public static class UnitTestActions
{
    const string NUnit2Reference = "nunit.framework, Version=2";
    const string NUnit3Reference = "nunit.framework";
//    const string MSTestReference = "Microsoft.VisualStudio.TestPlatform.TestFramework";  // Nah, that beast is just way to awkward to work with ..
    const string Xunit2Reference = "xunit.core";
    const string Xunit1Reference = "xunit";

    static UnitTestActions()
    {
        ExcludeFromUnitTests = new List<string>();
        XUnit2ToolArgs = new ToolSettings();
        XUnitToolArgs = new ToolSettings();
        NUnitToolArgs = new ToolSettings();
        NUnit3ToolArgs = new ToolSettings();
    }

    public static ICakeContext Context { get; private set; }

    // Use those if you need to provide environment variables or need to manually set the tool location.
    public static ToolSettings XUnit2ToolArgs { get; set; }
    public static ToolSettings XUnitToolArgs { get; set; }
    public static ToolSettings NUnit3ToolArgs { get; set; }
    public static ToolSettings NUnitToolArgs { get; set; }

    public static bool SkipTests { get; set; }

    /// <summary>
    ///     This list should contain either a relative path to the project (relative to the working directory of the Cake
    ///     script) or
    ///     the GUID of the project, or the assembly name.
    /// </summary>
    public static List<string> ExcludeFromUnitTests { get; private set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
        SkipTests = Context.Argument("skiptests", false);
    }

    /// <summary>
    ///     Executes all unit-tests on all assemblies that reference a supported unit-test framework.
    ///     All output is written into
    ///     "build-artefacts/tests/{Platform}/{Configuration}/{AssemblyName}/{AssemblyName}.{type}.xml
    ///     The steps generate the native XML format for the framework (xunit, xunit2, nunit2 and nunit3) and a transformed
    ///     copy
    ///     in the NUnit2 format.
    ///     Use "build-artefacts/tests/**/*.nunit2.xml" as selector in your CI environment to collect all unit-test results.
    /// </summary>
    public static void RunTests()
    {
        RunTests(BuildConfig.UnitTestSettings ?? new AnyUnitTestSettings());
    }

    public static void RunTests(AnyUnitTestSettings settings)
    {
        if (Context == null)
            throw new ArgumentException();

        var config = BuildConfig.Config;
        foreach (var projectWithPlatform in config.ProjectsByPlatform)
        foreach (var project in projectWithPlatform.Value)
            InvokeUnitTest(project, project.Platform, settings);
    }

    static void InvokeUnitTest(ParsedProject project, PlatformTarget platformTarget, AnyUnitTestSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException("settings");
        if (project == null)
            throw new ArgumentNullException("project");
        var path = project.ProjectFile;
        var relativeProjectFile = Context.Environment.WorkingDirectory.GetRelativePath(path);

        // parse referenced dependencies. We assume that if you reference a unit-test dll in your project, then that project
        // in fact contains tests. But there are always exceptions, and thus we allow you to exclude projects as well.
        if (ExcludeFromUnitTests.Contains(project.Project.AssemblyName) ||
            ExcludeFromUnitTests.Contains(project.Project.ProjectGuid) ||
            ExcludeFromUnitTests.Contains(relativeProjectFile.ToString()))
        {
            Context.Log.Verbose(string.Format("    Skipping project {0} as it was explicitly excluded from testing.",
                relativeProjectFile));
            return;
        }

        Context.Log.Information(string.Format("Searching for tests in {0} with platform {1}", 
            relativeProjectFile,
            platformTarget));

        var binDir = BuildActions.ComputeProjectBinPath(project);
        var testDir = ComputeProjectUnitTestPath(project);
        var testDll = binDir.CombineWithFilePath(ComputeTargetFile(project));

        if (HasTestFramework(project, NUnit2Reference))
        {
            Context.Log.Verbose(string.Format("    Testing with NUnit 2: {0}.", relativeProjectFile));

            Context.CreateDirectory(testDir);
            Context.NUnit(new[] {testDll}, BuildNUnitSettings(project, settings, testDir));
        }
        else if (HasTestFramework(project, NUnit3Reference))
        {
            Context.Log.Verbose(string.Format("    Testing with NUnit 3: {0}.", relativeProjectFile));

            Context.CreateDirectory(testDir);
            Context.NUnit3(new[] {testDll}, BuildNUnit3Settings(project, settings, testDir));
        }
        else if (HasTestFramework(project, Xunit2Reference))
        {
            Context.Log.Verbose(string.Format("    Testing with XUnit 2: {0}.", relativeProjectFile));

            Context.CreateDirectory(testDir);
            Context.XUnit2(new[] {testDll}, BuildXUnit2Settings(project, settings, testDir));

            var inputFile = testDir.CombineWithFilePath(project.Project.AssemblyName + ".xunit2.xml");
            var outputFile = testDir.CombineWithFilePath(project.Project.AssemblyName + ".nunit2.xml");
            Context.XmlTransform(Context.File("tools/xunit.runner.console/tools/NUnitXml.xslt"), inputFile, outputFile);
        }
        else if (HasTestFramework(project, Xunit1Reference))
        {
            Context.Log.Verbose(string.Format("    Testing with XUnit 1: {0}.", relativeProjectFile));

            Context.CreateDirectory(testDir);
            var xunitSettings = BuildXUnitSettings(project, settings, testDir);
            var xunitRunner = new FixedXUnitRunner(Context.FileSystem, Context.Environment, Context.ProcessRunner,
                Context.Tools);
            xunitRunner.Run(testDll, xunitSettings, settings.ForceX86 || project.Platform == PlatformTarget.x86);

            var inputFile = testDir.CombineWithFilePath(project.Project.AssemblyName + ".xunit.xml");
            var outputFile = testDir.CombineWithFilePath(project.Project.AssemblyName + ".nunit2.xml");
            Context.XmlTransform(Context.File("tools/xunit.runners/tools/NUnitXml.xslt"), inputFile, outputFile);
        }
        else
        {
            Context.Log.Verbose(
                string.Format("    Skipping project {0} as it does not reference a supported unit-testing framework.",
                    relativeProjectFile));
        }
    }

    public static DirectoryPath ComputeProjectUnitTestPath(ParsedProject project)
    {
        return BuildConfig.ComputeOutputPath("tests", project);
    }

    static FilePath ComputeTargetFile(ParsedProject project)
    {
        string extension;
        if ("Exe".Equals(project.Project.OutputType, StringComparison.InvariantCultureIgnoreCase) ||
            "Winexe".Equals(project.Project.OutputType, StringComparison.InvariantCultureIgnoreCase))
            extension = ".exe";
        else
            extension = ".dll";
        return project.Project.AssemblyName + extension;
    }

    static bool HasTestFramework(ParsedProject project, string id)
    {
        return project.Project.References.Any(refs => refs.Include.StartsWith(id, true, CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     The xUnit.net (v1) test runner.
    /// </summary>
    public sealed class FixedXUnitRunner : Tool<XUnitSettings>
    {
        readonly ICakeEnvironment _environment;
        bool _useX86;

        /// <summary>
        ///     Initializes a new instance of the <see cref="XUnitRunner" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="runner">The runner.</param>
        /// <param name="tools">The tool locator.</param>
        public FixedXUnitRunner(
            IFileSystem fileSystem,
            ICakeEnvironment environment,
            IProcessRunner runner,
            IToolLocator tools) : base(fileSystem, environment, runner, tools)
        {
            _environment = environment;
        }

        /// <summary>
        ///     Runs the tests in the specified assembly.
        /// </summary>
        /// <param name="assemblyPath">The assembly path.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="useX86"></param>
        public void Run(FilePath assemblyPath, XUnitSettings settings, bool useX86)
        {
            if (assemblyPath == null)
                throw new ArgumentNullException("assemblyPath");
            if (settings == null)
                throw new ArgumentNullException("settings");

            _useX86 = useX86;
            Run(settings, GetArguments(assemblyPath, settings));
        }

        ProcessArgumentBuilder GetArguments(FilePath assemblyPath, XUnitSettings settings)
        {
            var builder = new ProcessArgumentBuilder();

            // Get the absolute path to the assembly.
            assemblyPath = assemblyPath.MakeAbsolute(_environment);

            // Add the assembly to build.
            builder.AppendQuoted(assemblyPath.FullPath);

            // No shadow copy?
            if (!settings.ShadowCopy)
                builder.AppendQuoted("/noshadow");

            // Silent mode?
            if (settings.Silent)
                builder.Append("/silent");

            return builder;
        }

        /// <summary>
        ///     Gets the name of the tool.
        /// </summary>
        /// <returns>The name of the tool.</returns>
        protected override string GetToolName()
        {
            return "xUnit.net (v1)";
        }

        /// <summary>
        ///     Gets the possible names of the tool executable.
        /// </summary>
        /// <returns>The tool executable name.</returns>
        protected override IEnumerable<string> GetToolExecutableNames()
        {
            return _useX86 ? new[] {"xunit.console.clr4.x86.exe"} : new[] {"xunit.console.clr4.exe"};
        }
    }

    #region NUnit support

    static NUnitSettings BuildNUnitSettings(ParsedProject projectUnderTest, AnyUnitTestSettings settings,
        DirectoryPath outputDir)
    {
        var s = new NUnitSettings();
        XBuildHelper.ApplyToolSettings(s, NUnitToolArgs);

        if (settings.ExcludedCategories.Count > 0)
            s.Exclude = string.Join(",", settings.ExcludedCategories);
        if (settings.IncludedCategories.Count > 0)
            s.Include = string.Join(",", settings.IncludedCategories);
        s.ShadowCopy = settings.ShadowCopyAssemblies;
        s.UseSingleThreadedApartment = settings.UseSingleThreadedApartment;
        s.X86 = settings.ForceX86 || projectUnderTest.Platform == PlatformTarget.x86;
        s.ResultsFile = outputDir.CombineWithFilePath(projectUnderTest.Project.AssemblyName + ".nunit2.xml");
        return s;
    }

    static NUnit3Settings BuildNUnit3Settings(ParsedProject projectUnderTest, AnyUnitTestSettings settings,
        DirectoryPath outputDir)
    {
        var whereClause = BuildNUnit3WhereClause(settings);

        var s = new NUnit3Settings();
        XBuildHelper.ApplyToolSettings(s, NUnitToolArgs);

        s.ShadowCopy = settings.ShadowCopyAssemblies;
        s.X86 = settings.ForceX86 || projectUnderTest.Platform == PlatformTarget.x86;
        var path = outputDir.CombineWithFilePath(projectUnderTest.Project.AssemblyName + ".nunit3.xml");

        NUnit3Result rxx = new NUnit3Result();
        rxx.FileName = path;
        rxx.Format = "nunit3";
        rxx.Transform = null;
        s.Results = new List<NUnit3Result>
        {
            rxx
        };
        s.ArgumentCustomization = args =>
        {
            if (whereClause != null)
            {
                args.Append("--where");
                args.AppendQuoted(whereClause);
            }
            // Additionally generate NUnit2 output.
            AppendNUnit3AlternativeOutput(args,
                outputDir.CombineWithFilePath(projectUnderTest.Project.AssemblyName + ".nunit2.xml"));
            return args;
        };
        return s;
    }

    static void AppendNUnit3AlternativeOutput(ProcessArgumentBuilder builder, FilePath result)
    {
        var results = new StringBuilder(result.MakeAbsolute(Context.Environment).FullPath);
        results.AppendFormat(";format={0}", "nunit2");
        builder.AppendQuoted(string.Format(CultureInfo.InvariantCulture, "--result={0}", results));
    }

    static string BuildNUnit3WhereClause(AnyUnitTestSettings settings)
    {
        var includes = BuildNUnit3TraitSelector(settings.IncludedTraits);
        var excludes = BuildNUnit3TraitSelector(settings.ExcludedTraits);
        string whereClause;
        if (string.IsNullOrEmpty(excludes))
        {
            if (string.IsNullOrEmpty(includes))
                whereClause = null;
            else
                whereClause = includes;
        }
        else
        {
            if (string.IsNullOrEmpty(includes))
                whereClause = string.Format("!({0})", excludes);
            else
                whereClause = string.Format("!({0}) && ({1})", excludes, includes);
        }
        return whereClause;
    }

    static string BuildNUnit3TraitSelector(Dictionary<string, List<string>> traits)
    {
        var included = new StringBuilder();
        foreach (var trait in traits)
        {
            var name = trait.Key;
            foreach (var value in trait.Value)
            {
                if (included.Length != 0)
                    included.Append(" && ");

                included.Append("(");
                included.Append(name);
                included.Append(" == ");
                included.Append(WrapInNunit3Quotes(value));
                included.Append(")");
            }
        }
        return included.ToString();
    }

    // Mono parser in cake barfs on an a single quote character
    const char Quote = (char) 0x22;

    static string WrapInNunit3Quotes(string s)
    {
        var quoteString = char.ToString(Quote);
        if (s.Contains("'") ||
            s.Contains(quoteString) ||
            s.Contains("\\") ||
            s.Contains("/"))
            return quoteString + s.Replace("\\", "\\\\").Replace(quoteString, "\\" + quoteString) + quoteString;
        return s;
    }

    #endregion

    #region XUnit Settings

    static XUnit2Settings BuildXUnit2Settings(ParsedProject projectUnderTest, AnyUnitTestSettings settings,
        DirectoryPath outputDir)
    {
        var outputFile = outputDir.CombineWithFilePath(projectUnderTest.Project.AssemblyName + ".xunit2.xml");

        var traitArgs = new List<KeyValuePair<string, string>>();
        AddTraits(traitArgs, "-notrait", settings.ExcludedTraits);
        AddTraits(traitArgs, "-trait", settings.IncludedTraits);

        var s = new XUnit2Settings();
        XBuildHelper.ApplyToolSettings(s, NUnitToolArgs);
        s.ShadowCopy = settings.ShadowCopyAssemblies;
        s.UseX86 = settings.ForceX86 || projectUnderTest.Platform == PlatformTarget.x86;
        s.ArgumentCustomization = args =>
        {
            foreach (var traitArg in traitArgs)
            {
                args.Append(traitArg.Key);
                args.AppendQuoted(traitArg.Value);
            }

            args.Append("-xml");
            args.AppendQuoted(outputFile.MakeAbsolute(Context.Environment).FullPath);
            return args;
        };
        return s;
    }


    static XUnitSettings BuildXUnitSettings(ParsedProject projectUnderTest, AnyUnitTestSettings settings,
        DirectoryPath outputDir)
    {
        var outputFile = outputDir.CombineWithFilePath(projectUnderTest.Project.AssemblyName + ".xunit.xml");

        var traitArgs = new List<KeyValuePair<string, string>>();
        AddTraits(traitArgs, "/-trait", settings.ExcludedTraits);
        AddTraits(traitArgs, "/trait", settings.IncludedTraits);

        var s = new XUnitSettings();
        XBuildHelper.ApplyToolSettings(s, NUnitToolArgs);
        s.ShadowCopy = settings.ShadowCopyAssemblies;
        s.ArgumentCustomization = args =>
        {
            foreach (var traitArg in traitArgs)
            {
                args.Append(traitArg.Key);
                args.AppendQuoted(traitArg.Value);
            }

            args.Append("/xml");
            args.AppendQuoted(outputFile.MakeAbsolute(Context.Environment).FullPath);
            return args;
        };
        return s;
    }

    static void AddTraits(List<KeyValuePair<string, string>> traitArgs, string arg,
        Dictionary<string, List<string>> traits)
    {
        foreach (var exclusion in traits)
        foreach (var value in exclusion.Value)
            traitArgs.Add(new KeyValuePair<string, string>(arg, string.Format("{0}={1}", exclusion.Key, value)));
    }

    #endregion
}