using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cake.Common.Tools.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public class ExtendedSolutionParser
{
    static Dictionary<string, string> ParseSolutionForSupportedPlatformsPerProject(ICakeContext context,
                                                                                    FilePath solutionPath)
    {
        if (solutionPath == null)
        {
            throw new ArgumentNullException("solutionPath");
        }
        if (solutionPath.IsRelative)
        {
            solutionPath = solutionPath.MakeAbsolute(context.Environment);
        }

        // Get the release notes file.
        var file = context.FileSystem.GetFile(solutionPath);
        if (!file.Exists)
        {
            const string format = "Solution file '{0}' does not exist.";
            var message = string.Format(CultureInfo.InvariantCulture, format, solutionPath.FullPath);
            throw new CakeException(message);
        }

        bool inConfigSection = false;
        Dictionary<string,string> props = new Dictionary<string, string>();
        foreach (var line in file.ReadLines(Encoding.UTF8))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("GlobalSection(ProjectConfigurationPlatforms)"))
            {
                inConfigSection = true;
                continue;
            }
            if (inConfigSection && trimmed.StartsWith("EndGlobalSection"))
            {
                inConfigSection = false;
                continue;
            }

            // line is in format {project-guid}.ConfigPlatform.property = value
            // where the interesting property is "Build.0". The value then is the mapped target property.
            int separator = trimmed.IndexOf('=');
            if (separator == -1)
            {
                continue;
            }
            string key = trimmed.Substring(0, separator).Trim();
            string val = trimmed.Substring(separator + 1).Trim();
            props[key] = val;
        }
        return props;
    }

    public XBuildHelper.ParseResult Filter(ICakeContext context,
                                            FilePath solutionPath,
                                            XBuildHelper.ParseResult result,
                                            string configuration)
    {
        var activeConfs = ParseSolutionForSupportedPlatformsPerProject(context, solutionPath);

        var list = result.ProjectsByPlatform.ToList();
        foreach (var pair in list)
        {
            var platform = pair.Key;
            var projects = pair.Value.ToList();
            foreach (var p in projects)
            {
                string pid = p.Project.ProjectGuid;
                string config = configuration;
                string projectPlatform = ConvertPlatformTargetToString(p.Platform);
                if (projectPlatform == "AnyCPU")
                {
                    projectPlatform = "Any CPU";
                }
                string projectConf = config + "|" + projectPlatform;
                string key = pid + "." + projectConf + ".Build.0";
                string buildConfig;
                if (activeConfs.TryGetValue(key, out buildConfig))
                {
                    if (string.Equals(projectConf, buildConfig))
                    {
                        context.Log.Debug("Preserved project " +
                                          p.ProjectFile);
                        continue;
                    }
                }

                // project is not valid.
                context.Log.Debug("Removed project " +
                                  p.ProjectFile +
                                  " as the project config (" +
                                  projectConf +
                                  ") does match the defined build config in the solution (" +
                                  key +
                                  ")");
                pair.Value.Remove(p);

            }
            if (pair.Value.Count == 0)
            {
                result.ProjectsByPlatform.Remove(platform);
            }
        }
        foreach (var p in result.PlatformBuildOrder.ToList())
        {
            if (!result.ProjectsByPlatform.ContainsKey(p))
            {
                context.Log.Debug("Removed platform " + p + " after having no project to build for it.");
                result.PlatformBuildOrder.Remove(p);
            }
        }
        return result;
    }

    public static string ConvertPlatformTargetToString(PlatformTarget target)
    {
        if (PlatformTarget.MSIL == target)
            return "AnyCPU";
        return target.ToString();
    }

}
