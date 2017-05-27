using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.Tools.DotNetCore.Restore;
using Cake.Common.Tools.MSBuild;
using Cake.Common.Tools.NuGet;
using Cake.Common.Tools.NuGet.Restore;
using Cake.Common.Tools.XBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;

public static class XBuildHelper
{
    static bool? dotNetExists;
    public static ICakeContext Context { get; private set; }

    public static DotNetCoreRestoreSettings DotNetCoreRestoreSettings { get; set; }
    public static bool NeverUseDotNetCore { get; set; }

    public static bool DotNetExists
    {
        get
        {
            if (NeverUseDotNetCore)
            {
                return false;
            }
            if (dotNetExists == null)
            {
                var tool = new DotNetGeneralTool(Context.FileSystem,
                    Context.Environment,
                    Context.ProcessRunner,
                    Context.Tools);
                dotNetExists = tool.DotNetExists(null);
            }
            return dotNetExists.Value;
        }
    }

    public static void Configure(ICakeContext context)
    {
        Context = context;
    }

    public static DotNetCoreRestoreSettings ConvertRestoreSettings(NuGetRestoreSettings settings)
    {
        var s = new DotNetCoreRestoreSettings();
        if (settings == null)
            return s;

        ApplyToolSettings(s, settings);
        s.ConfigFile = settings.ConfigFile;
        s.PackagesDirectory = settings.PackagesDirectory;
        s.Verbosity = Convert(settings.Verbosity);
        s.DisableParallel = settings.DisableParallelProcessing;
        s.NoCache = settings.NoCache;
        if (settings.Source != null)
            s.Sources = new List<string>(settings.Source);
        if (settings.FallbackSource != null)
            s.FallbackSources = new List<string>(settings.FallbackSource);

        return s;
    }

    static DotNetCoreRestoreVerbosity? Convert(NuGetVerbosity? verbosity)
    {
        if (verbosity == null)
            return null;
        switch (verbosity.Value)
        {
            case NuGetVerbosity.Detailed:
                return DotNetCoreRestoreVerbosity.Verbose;
            case NuGetVerbosity.Quiet:
                return DotNetCoreRestoreVerbosity.Warning;
            default:
                return DotNetCoreRestoreVerbosity.Information;
        }
    }

    public static MSBuildSettings CreateMSBuildSettings(AnyBuildSettings settings, string target = null)
    {
        if (settings == null)
            return new MSBuildSettings();

        var s = new MSBuildSettings();
        ApplyToolSettings(s, settings);
        s.ArgumentCustomization = settings.ArgumentCustomization;

        s.Configuration = settings.Configuration ?? BuildConfig.Configuration;
        s.Verbosity = settings.Verbosity;
        if (target != null)
            s.Targets.Add(target);
        foreach (var e in settings.Properties)
            s.Properties[e.Key] = new List<string>(e.Value);
        s.NoConsoleLogger = settings.NoConsoleLogger;
        s.PlatformTarget = settings.PlatformTarget;

        // MSBuild specific settings
        s.MSBuildPlatform = settings.MSBuildPlatform;
        s.MaxCpuCount = settings.MaxCpuCount;
        return s;
    }

    public static XBuildSettings CreateXBuildSettings(AnyBuildSettings settings, FilePath targetFile, string target)
    {
        if (settings == null)
            return new XBuildSettings();

        var s = new XBuildSettings();
        ApplyToolSettings(s, settings);

        s.Configuration = settings.Configuration ?? BuildConfig.Configuration;
        s.Verbosity = settings.Verbosity;
        s.Targets.Add(target);
        foreach (var e in settings.Properties)
            s.Properties[e.Key] = new List<string>(e.Value);

        // Hack in support for supressing console outputs. The command line option is there,
        // so it would be a shame not to actually use it.
        s.ArgumentCustomization = x =>
        {
            if (settings.ArgumentCustomization != null)
                x = settings.ArgumentCustomization(x);
            if (settings.NoConsoleLogger)
                x.Prepend("/noconsolelogger");
            return x;
        };

        if (settings.PlatformTarget.HasValue)
        {
            var platform = settings.PlatformTarget.Value;
            var isSolution = string.Equals(targetFile.GetExtension(), ".sln", StringComparison.OrdinalIgnoreCase);
            s.WithProperty("Platform", GetPlatformName(platform, isSolution));
        }
        return s;
    }

    public static string GetPlatformName(PlatformTarget platform, bool isSolution)
    {
        switch (platform)
        {
            case PlatformTarget.MSIL:
                // Solutions expect "Any CPU", but projects expect "AnyCPU"
                return isSolution ? "\"Any CPU\"" : "AnyCPU";
            case PlatformTarget.x86:
                return "x86";
            case PlatformTarget.x64:
                return "x64";
            case PlatformTarget.ARM:
                return "arm";
            case PlatformTarget.Win32:
                return "Win32";
            default:
                throw new ArgumentOutOfRangeException("platform", platform, "Invalid platform");
        }
    }

    public static void ApplyToolSettings(ToolSettings target, ToolSettings source)
    {
        if (source == null)
            return;
        if (source.EnvironmentVariables != null)
            target.EnvironmentVariables = new Dictionary<string, string>(source.EnvironmentVariables);

        target.ToolPath = source.ToolPath;
        target.WorkingDirectory = source.WorkingDirectory;
        target.ToolTimeout = source.ToolTimeout;
    }

    public static ParseResult ParseProjects(ICakeContext context,
        string configuration,
        List<PlatformTarget> platforms,
        List<FilePath> effectiveProjects)
    {
        var c = new ParseResult();
        foreach (var platform in platforms)
        {
            var projects = effectiveProjects
                .Select(p => ParsedProject.Parse(context, p, configuration, platform))
                .Where(IsValidProjectForPlatform(context))
                .ToList();

            if (projects.Count > 0)
            {
                c.ProjectsByPlatform[platform] = projects;
                c.PlatformBuildOrder.Add(platform);
            }
        }
        return c;
    }

    static Func<ParsedProject, bool> IsValidProjectForPlatform(ICakeContext context)
    {
        return p =>
        {
            if (p == null)
                return false;

            // this is the closest matching platform entry in the project filtered by the standard condition format VisualStudio uses.
            var platformTargetText = p.Project.PlatformTarget;
            if (string.IsNullOrEmpty(platformTargetText))
                platformTargetText = "AnyCPU";

            context.Log.Debug("Testing if valid " + p.ProjectFile + " for " + platformTargetText);
            var platformTarget = ParsePlatformFromProjectString(platformTargetText);
            return platformTarget == p.Platform;
        };
    }

    // Because consistency and Microsoft does not go well together ...
    static PlatformTarget ParsePlatformFromProjectString(string v)
    {
        if (string.Equals(v, "x86", StringComparison.InvariantCultureIgnoreCase))
            return PlatformTarget.x86;
        if (string.Equals(v, "x64", StringComparison.InvariantCultureIgnoreCase))
            return PlatformTarget.x64;
        if (string.Equals(v, "ARM", StringComparison.InvariantCultureIgnoreCase))
            return PlatformTarget.ARM;
        if (string.Equals(v, "AnyCPU", StringComparison.InvariantCultureIgnoreCase))
            return PlatformTarget.MSIL;
        throw new Exception("Requested platform target '" + v + "' is not supported here.");
    }

    public class ParseResult
    {
        public ParseResult()
        {
            ProjectsByPlatform = new Dictionary<PlatformTarget, List<ParsedProject>>();
            PlatformBuildOrder = new List<PlatformTarget>();
        }

        public Dictionary<PlatformTarget, List<ParsedProject>> ProjectsByPlatform { get; private set; }
        public List<PlatformTarget> PlatformBuildOrder { get; private set; }
    }
}