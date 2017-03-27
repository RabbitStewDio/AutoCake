using System.Collections.Generic;
using Cake.Common.Tools.NuGet;
using Cake.Core.Tooling;

public class NuGetXPackSettings : ToolSettings
{
    public NuGetXPackSettings()
    {
        Properties = new Dictionary<string, string>();
    }

    public NuGetXPackSettings(NuGetXPackSettings copy) : this()
    {
        if (copy == null)
            return;

        XBuildHelper.ApplyToolSettings(this, copy);
        ArgumentCustomization = copy.ArgumentCustomization;
        Tool = copy.Tool;
        Symbols = copy.Symbols;
        IncludeReferencedProjects = copy.IncludeReferencedProjects;
        Verbosity = copy.Verbosity;

        foreach (var property in copy.Properties)
            Properties[property.Key] = property.Value;
    }

    public bool? IncludeReferencedProjects { get; set; }
    public bool? Symbols { get; set; }
    public bool? Tool { get; set; }
    public IDictionary<string, string> Properties { get; private set; }
    public NuGetVerbosity? Verbosity { get; set; }
}