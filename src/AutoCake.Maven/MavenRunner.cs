using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;

/// <summary>
///     Cake out process runner. The original cake runner produced garbage when running on Windows,
///     trying to execute a DLL instead of the Cake.exe as AssemblyHelper.GetExecutingAssembly().Location
///     does not produce the desired results.
///     This version simply assumes that there is a Cake.exe in the tools directory and uses that one.
///     No magic.
/// </summary>
public sealed class MavenRunner : Tool<MavenSettings>
{
    readonly ICakeEnvironment _environment;
    readonly IFileSystem _fileSystem;
    readonly IGlobber _globber;
    readonly ICakeLog log;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MavenRunner" /> class.
    /// </summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="globber">The globber.</param>
    /// <param name="processRunner">The process runner.</param>
    /// <param name="tools">The tool locator.</param>
    public MavenRunner(IFileSystem fileSystem,
                           ICakeEnvironment environment,
                           IGlobber globber,
                           IProcessRunner processRunner,
                           IToolLocator tools,
                           ICakeLog log)
        : base(fileSystem, environment, processRunner, tools)
    {
        _environment = environment;
        _fileSystem = fileSystem;
        _globber = globber;
        this.log = log;
    }

    /// <summary>
    ///     Executes supplied cake script in own process and supplied settings
    /// </summary>
    /// <param name="scriptPath">Path to script to execute</param>
    /// <param name="settings">optional cake settings</param>
    public void ExecuteScript(MavenSettings settings = null)
    {
        settings = settings ?? new MavenSettings();
        var wd = settings.WorkingDirectory ?? _environment.WorkingDirectory;
        var filePath = wd.CombineWithFilePath("pom.xml").MakeAbsolute(_environment);
        if (!_fileSystem.GetFile(filePath).Exists)
            throw new FileNotFoundException("pom.xml file not found.", filePath.FullPath);

        Run(settings, GetArguments(settings));
    }
    
    ProcessArgumentBuilder GetArguments(MavenSettings settings)
    {
        var builder = new ProcessArgumentBuilder();

        var verbosity = settings.Verbosity.GetValueOrDefault(this.log.Verbosity);
        switch (verbosity)
        {
            case Verbosity.Quiet:
                builder.Append("-q");
                break;
            case Verbosity.Minimal:
                builder.Append("-e");
                break;
            case Verbosity.Normal:
                break;
            case Verbosity.Verbose:
                break;
            case Verbosity.Diagnostic:
                builder.Append("-X");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        if (!string.IsNullOrEmpty(settings.SettingsFile))
        {
            builder.Append("-s");
            builder.AppendQuoted(settings.SettingsFile);
        }
        if (settings.BatchMode != false)
        {
            builder.Append("-B");
        }
        if (settings.ForceUpdate == true)
        {
            builder.Append("-U");
        }
        if (settings.NonRecursive == true)
        {
            builder.Append("-N");
        }
        if (settings.Offline == true)
        {
            builder.Append("-o");
        }

        if (settings.Profiles.Count != 0)
        {
            string profiles = string.Join(",", settings.Profiles);
            builder.Append("-P");
            builder.AppendQuoted(profiles);
        }

        foreach (var argument in settings.Properties)
        {
            builder.AppendQuoted(string.Format(
                                CultureInfo.InvariantCulture,
                                "-D{0}={1}",
                                argument.Key, argument.Value ?? string.Empty));
        }

        foreach (var g in settings.Goal)
        {
            builder.AppendQuoted(g);
        }
        foreach (var p in settings.Phase)
        {
            builder.AppendQuoted(p);
        }
        return builder;
    }

    /// <summary>
    ///     Gets the name of the tool.
    /// </summary>
    /// <returns>The name of the tool.</returns>
    protected override string GetToolName()
    {
        return "Maven";
    }

    /// <summary>
    ///     Gets the name of the tool executable.
    /// </summary>
    /// <returns>The tool executable name.</returns>
    protected override IEnumerable<string> GetToolExecutableNames()
    {
        return new[] { "mvn.cmd", "mvn.bat", "mvn" };
    }
}