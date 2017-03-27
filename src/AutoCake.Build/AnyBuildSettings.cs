using System.Collections.Generic;
using System.Linq;
using Cake.Common.Tools.MSBuild;
using Cake.Core.Diagnostics;
using Cake.Core.Tooling;

/// <summary>
///     A generic build settings class that unifies both XBuild and MSBuild settings.
/// </summary>
public class AnyBuildSettings : ToolSettings
{
    readonly Dictionary<string, IList<string>> properties;

    public AnyBuildSettings()
    {
        properties = new Dictionary<string, IList<string>>();
        Targets = new List<string>();
        Verbosity = Verbosity.Minimal;
    }

    /// <summary>
    ///     Copy-Constructor to easily produce derived configurations.
    /// </summary>
    /// <param name="s"></param>
    public AnyBuildSettings(AnyBuildSettings s) : this()
    {
        if (s != null)
        {
            foreach (var p in s.Properties)
            {
                var value = p.Value.ToArray();
                WithProperty(p.Key, value);
            }
            Targets.AddRange(s.Targets);
            PlatformTarget = s.PlatformTarget;
            Configuration = s.Configuration;
            Verbosity = s.Verbosity;
            NoConsoleLogger = s.NoConsoleLogger;
            MSBuildPlatform = s.MSBuildPlatform;
            MaxCpuCount = s.MaxCpuCount;

            ToolPath = s.ToolPath;
            ToolTimeout = s.ToolTimeout;
            WorkingDirectory = s.WorkingDirectory;
            ArgumentCustomization = s.ArgumentCustomization;
            if (s.EnvironmentVariables != null)
                EnvironmentVariables = new Dictionary<string, string>(s.EnvironmentVariables);
        }
    }

    // Using a list to preserve order. I trust the user not to enter duplicates or the build tool to handle
    // those duplicates gracefully.
    public List<string> Targets { get; private set; }

    /// <summary>
    ///     If the platform target is defined here, MSBuild will choose a different implementation of the
    ///     MSBuild runner for compilation. This also causes the "Platform" property to be set.
    ///     For XBuild, we also honor this setting for updating the property based on whether the call target
    ///     a project or solution file.
    /// </summary>
    public PlatformTarget? PlatformTarget { get; set; }

    public string Configuration { get; set; }

    public Verbosity Verbosity { get; set; }

    public bool NoConsoleLogger { get; set; }

    public MSBuildPlatform MSBuildPlatform { get; set; }

    public int? MaxCpuCount { get; set; }

    public IDictionary<string, IList<string>> Properties
    {
        get { return properties; }
    }

    /// <summary>
    ///     Adds a property to the configuration.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="values">The property values.</param>
    /// <returns>The same <see cref="AnyBuildSettings" /> instance so that multiple calls can be chained.</returns>
    public AnyBuildSettings WithProperty(string name, params string[] values)
    {
        IList<string> currentValue;
        currentValue = new List<string>(
            Properties.TryGetValue(name, out currentValue) && currentValue != null
                ? currentValue.Concat(values)
                : values);

        Properties[name] = currentValue;
        return this;
    }
}