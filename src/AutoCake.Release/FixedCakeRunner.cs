using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Cake.Common.Tools.Cake;
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
public sealed class FixedCakeRunner : Tool<CakeSettings>
{
    readonly ICakeEnvironment _environment;
    readonly IFileSystem _fileSystem;
    readonly IGlobber _globber;
    readonly ICakeLog log;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FixedCakeRunner" /> class.
    /// </summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="globber">The globber.</param>
    /// <param name="processRunner">The process runner.</param>
    /// <param name="tools">The tool locator.</param>
    public FixedCakeRunner(IFileSystem fileSystem,
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
    public void ExecuteScript(FilePath scriptPath, CakeSettings settings = null)
    {
        if (scriptPath == null)
            throw new ArgumentNullException("scriptPath");

        scriptPath = scriptPath.MakeAbsolute(_environment);
        if (!_fileSystem.GetFile(scriptPath).Exists)
            throw new FileNotFoundException("Cake script file not found.", scriptPath.FullPath);

        settings = settings ?? new CakeSettings();
        Run(settings, GetArguments(scriptPath, settings));
    }

    /// <summary>
    ///     Executes supplied cake code expression in own process and supplied settings
    /// </summary>
    /// <param name="cakeExpression">Code expression to execute</param>
    /// <param name="settings">optional cake settings</param>
    public void ExecuteExpression(string cakeExpression, CakeSettings settings = null)
    {
        if (string.IsNullOrWhiteSpace(cakeExpression))
            throw new ArgumentNullException("cakeExpression");
        DirectoryPath tempPath = _environment.GetEnvironmentVariable("TEMP") ?? "./";
        var tempScriptFile = _fileSystem.GetFile(tempPath
            .CombineWithFilePath(string.Format(CultureInfo.InvariantCulture, "{0}.cake", Guid.NewGuid()))
            .MakeAbsolute(_environment));
        try
        {
            using (var stream = tempScriptFile.OpenWrite())
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                {
                    streamWriter.WriteLine(cakeExpression);
                }
            }
            ExecuteScript(tempScriptFile.Path.FullPath, settings);
        }
        finally
        {
            if (tempScriptFile.Exists)
                tempScriptFile.Delete();
        }
    }

    ProcessArgumentBuilder GetArguments(FilePath scriptPath, CakeSettings settings)
    {
        var builder = new ProcessArgumentBuilder();
        builder.AppendQuoted(scriptPath.MakeAbsolute(_environment).FullPath);

        if (settings.Verbosity.HasValue)
            builder.Append(string.Concat("-verbosity=", settings.Verbosity.Value.ToString()));

        if (settings.Arguments != null)
            foreach (var argument in settings.Arguments)
                builder.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "-{0}={1}",
                    argument.Key,
                    (argument.Value ?? string.Empty).Quote()));
        return builder;
    }

    /// <summary>
    ///     Gets the name of the tool.
    /// </summary>
    /// <returns>The name of the tool.</returns>
    protected override string GetToolName()
    {
        return "Cake";
    }

    /// <summary>
    ///     Gets the name of the tool executable.
    /// </summary>
    /// <returns>The tool executable name.</returns>
    protected override IEnumerable<string> GetToolExecutableNames()
    {
        return new[] {"Cake.exe"};
    }
}