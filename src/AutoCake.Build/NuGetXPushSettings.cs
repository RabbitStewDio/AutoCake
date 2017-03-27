using System;
using Cake.Common.Tools.NuGet;
using Cake.Core.IO;
using Cake.Core.Tooling;

/// <summary>
///     Contains settings used by <see cref="NuGetXPusher" />.
/// </summary>
public sealed class NuGetXPushSettings : ToolSettings
{
    /// <summary>
    ///     Gets or sets the server URL. If not specified, nuget.org is used unless
    ///     DefaultPushSource config value is set in the NuGet config file.
    ///     Starting with NuGet 2.5, if NuGet.exe identifies a UNC/folder source,
    ///     it will perform the file copy to the source.
    /// </summary>
    /// <value>The server URL.</value>
    public string Source { get; set; }

    /// <summary>
    ///     Gets or sets the API key for the server.
    /// </summary>
    /// <value>The API key for the server.</value>
    public string ApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the timeout for pushing to a server.
    ///     Defaults to 300 seconds (5 minutes).
    /// </summary>
    /// <value>The timeout for pushing to a server.</value>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    ///     Gets or sets the verbosity.
    /// </summary>
    /// <value>The verbosity.</value>
    public NuGetVerbosity? Verbosity { get; set; }

    /// <summary>
    ///     Gets or sets the NuGet configuration file.
    /// </summary>
    /// <value>The NuGet configuration file.</value>
    public FilePath ConfigFile { get; set; }

    /// NuGet v3.5.0: New options only documented in source and command-line help.
    public string SymbolsSource { get; set; }

    public string SymbolsApiKey { get; set; }
    public bool NoSymbols { get; set; }
}