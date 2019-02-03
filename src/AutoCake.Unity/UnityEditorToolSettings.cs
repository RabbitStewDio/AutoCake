using System.Collections.Generic;
using System.Data;
using Cake.Core.Tooling;

public class UnityEditorToolSettings : ToolSettings
{
    /// <summary>
    /// <para>Forwards log file to output in real time.</para>
    /// <para>Requires LogFile argument to be specified.</para>
    /// </summary>
    public bool? RealTimeLog { get; set; }

    public UnityEditorToolSettings()
    {
    }

    public UnityEditorToolSettings(UnityEditorToolSettings settings)
    {
        Merge(settings);
    }

    void Merge(UnityEditorToolSettings copy)
    {
        if (copy.RealTimeLog != null)
            RealTimeLog = copy.RealTimeLog;
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