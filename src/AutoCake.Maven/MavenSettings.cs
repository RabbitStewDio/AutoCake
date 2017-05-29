using System.Collections.Generic;
using Cake.Core.Diagnostics;
using Cake.Core.Tooling;

public class MavenSettings : ToolSettings
{
    public List<string> Goal { get; private set; }
    public List<string> Phase { get; private set; }

    public Dictionary<string, string> Properties { get; private set; }
    public List<string> Profiles { get; private set; }

    public string SettingsFile { get; set; }
    public bool? BatchMode { get; set; }
    public bool? NonRecursive { get; set; }
    public bool? Offline { get; set; }
    public bool? ForceUpdate { get; set; }
    public Verbosity? Verbosity { get; set; }

    public MavenSettings()
    {
        Phase = new List<string>();
        Goal = new List<string>();
        Profiles = new List<string>();
        Properties = new Dictionary<string, string>();
    }

    public MavenSettings(MavenSettings copy) : this()
    {
        Merge(copy);
    }

    public void Merge(MavenSettings copy)
    {
        if (copy == null)
        {
            return;
        }

        Phase.AddRange(copy.Phase);
        Goal.AddRange(copy.Goal);
        Profiles.AddRange(copy.Profiles);
        foreach (var property in copy.Properties)
        {
            Properties[property.Key] = property.Value;
        }
        if (copy.SettingsFile != null)
            SettingsFile = copy.SettingsFile;
        if (copy.BatchMode != null)
            BatchMode = copy.BatchMode;
        if (copy.NonRecursive != null)
            NonRecursive = copy.NonRecursive;
        if (copy.Offline != null)
            Offline = copy.Offline;
        if (copy.ForceUpdate != null)
            ForceUpdate = copy.ForceUpdate;

        if (copy.ToolPath != null)
            ToolPath = copy.ToolPath;
        if (copy.ToolTimeout != null)
            ToolTimeout = copy.ToolTimeout;
        if (copy.WorkingDirectory != null)
            WorkingDirectory = copy.WorkingDirectory;
        if (copy.ArgumentCustomization != null)
            ArgumentCustomization = copy.ArgumentCustomization;
        if (copy.EnvironmentVariables != null)
        {
            if (EnvironmentVariables == null)
            {
                EnvironmentVariables = new Dictionary<string, string>(copy.EnvironmentVariables);
            }
            else
            {
                foreach (var pair in copy.EnvironmentVariables)
                {
                    EnvironmentVariables[pair.Key] = pair.Value;
                }
            }
        }
    }
}