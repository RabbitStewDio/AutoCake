using System;
using System.Globalization;
using Cake.Common.Tools.NuGet;
using Cake.Common.Tools.NuGet.Push;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.IO.NuGet;
using Cake.Core.Tooling;

/// <summary>
///     The NuGet package pusher.
/// </summary>
public sealed class NuGetXPusher : NuGetTool<NuGetXPushSettings>
{
    readonly ICakeEnvironment _environment;
    readonly ICakeLog _log;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NuGetPusher" /> class.
    /// </summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="processRunner">The process runner.</param>
    /// <param name="tools">The tool locator.</param>
    /// <param name="resolver">The NuGet tool resolver.</param>
    /// <param name="log">The logger.</param>
    public NuGetXPusher(
        IFileSystem fileSystem,
        ICakeEnvironment environment,
        IProcessRunner processRunner,
        IToolLocator tools,
        INuGetToolResolver resolver,
        ICakeLog log) : base(fileSystem, environment, processRunner, tools, resolver)
    {
        _environment = environment;
        _log = log;
    }

    /// <summary>
    ///     Pushes a NuGet package to a NuGet server and publishes it.
    /// </summary>
    /// <param name="packageFilePath">The package file path.</param>
    /// <param name="settings">The settings.</param>
    public void Push(FilePath packageFilePath, NuGetXPushSettings settings)
    {
        if (packageFilePath == null)
            throw new ArgumentNullException("packageFilePath");
        if (settings == null)
            throw new ArgumentNullException("settings");

        Run(settings, GetArguments(packageFilePath, settings));
    }

    ProcessArgumentBuilder GetArguments(FilePath packageFilePath, NuGetXPushSettings settings)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("push");

        builder.AppendQuoted(packageFilePath.MakeAbsolute(_environment).FullPath);

        if (settings.ApiKey != null)
            builder.AppendSecret(settings.ApiKey);

        builder.Append("-NonInteractive");

        if (settings.ConfigFile != null)
        {
            builder.Append("-ConfigFile");
            builder.AppendQuoted(settings.ConfigFile.MakeAbsolute(_environment).FullPath);
        }

        if (settings.Source != null)
        {
            builder.Append("-Source");
            builder.AppendQuoted(settings.Source);
        }
        else
        {
            _log.Verbose("No Source property has been set.  Depending on your configuration, this may cause problems.");
        }

        if (settings.Timeout != null)
        {
            builder.Append("-Timeout");
            builder.Append(Convert.ToInt32(settings.Timeout.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        }

        if (settings.Verbosity != null)
        {
            builder.Append("-Verbosity");
            builder.Append(settings.Verbosity.Value.ToString().ToLowerInvariant());
        }

        if (settings.NoSymbols)
            builder.Append("-NoSymbols");

        if (settings.SymbolsApiKey != null)
        {
            builder.Append("-SymbolsApiKey");
            builder.AppendQuoted(settings.SymbolsApiKey);
        }

        if (settings.SymbolsSource != null)
        {
            builder.Append("-SymbolsSource");
            builder.AppendQuoted(settings.SymbolsSource);
        }

        return builder;
    }
}