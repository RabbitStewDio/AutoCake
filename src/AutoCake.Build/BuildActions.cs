using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cake.Common.IO;
using Cake.Common.Solution.Project;
using Cake.Common.Tools.MSBuild;
using Cake.Common.Tools.NuGet;
using Cake.Common.Tools.XBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public class BuildActions
{
    public static ICakeContext Context { get; private set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
    }

    public static void CleanBinaries()
    {
        var projectFiles = BuildConfig.Config.ProjectFiles;
        Context.Log.Verbose(string.Format("Cleaning {0} project build directories.", projectFiles.Count));
        foreach (var p in projectFiles)
        {
            // working by convention here. We delete the "obj" directories.
            // This does not care about platform or build configurations - we delete it all.
            var obj = p.GetDirectory().Combine("obj");
            var bin = p.GetDirectory().Combine("bin");
            if (Context.DirectoryExists(bin))
            {
                Context.Log.Debug(string.Format("  Cleaning {0}.", BuildConfig.AsRelativePath(bin)));
                Context.DeleteDirectory(bin, true);
            }
            if (Context.DirectoryExists(obj))
            {
                Context.Log.Debug(string.Format("  Cleaning {0}.", BuildConfig.AsRelativePath(obj)));
                Context.DeleteDirectory(obj, true);
            }
        }
    }

    public static void CleanPackages()
    {
        if (BuildConfig.Config.SolutionDir != null)
        {
            var packagesDir = BuildConfig.Config.SolutionDir.Combine("packages");
            Context.Log.Verbose(string.Format("Cleaning packages directory: {0}",
                BuildConfig.AsRelativePath(packagesDir)));
            if (Context.DirectoryExists(packagesDir))
            {
                Context.Log.Debug(string.Format("  Cleaning {0}.", BuildConfig.AsRelativePath(packagesDir)));
                Context.DeleteDirectory(packagesDir, true);
            }
        }
    }

    public static void CleanArtefacts()
    {
        var packagesDir = BuildConfig.TargetDirectory;
        Context.Log.Verbose(string.Format("Cleaning build-artefacts directory: {0}", Context.MakeAbsolute(packagesDir)));
        if (Context.DirectoryExists(packagesDir))
            Context.DeleteDirectory(packagesDir, true);
    }

    public static void RestorePackages()
    {
        var effectiveBuildConfig = BuildConfig.Config;
        if (effectiveBuildConfig.Solution != null)
        {
            Context.Log.Verbose("Restoring solution NuGet packages.");
            RestoreTarget(effectiveBuildConfig.Solution);
        }
        else
        {
            foreach (var p in effectiveBuildConfig.ProjectFiles)
            {
                Context.Log.Verbose(string.Format("Restoring NuGet packages for project {0}.",
                    Context.Environment.WorkingDirectory.GetRelativePath(p)));
                RestoreTarget(p);
            }
        }
    }

    static void RestoreTarget(FilePath p)
    {
        // Lets hope tht NuGet is smart enough to handle Visual Studio 2017. They seem to be part of the 
        // same developer crew, they should be able to talk to each other, right?
        Context.NuGetRestore(p, BuildConfig.RestoreSettings);
    }

    public static void UpdateBuildMetaData()
    {
        if (string.IsNullOrEmpty(BuildConfig.GeneratedAssemblyMetaDataReference))
        {
            Context.Log.Verbose("Skipping update of build metadata, no GeneratedAssemblyMetaDataReference specified.");
            return;
        }

        var effectiveBuildConfig = BuildConfig.Config;
        var metadata = new Dictionary<string, string>(BuildConfig.AssemblyProperties);
        var template = BuildConfig.GeneratedAssemblyMetaDataTemplate;
        if (string.IsNullOrEmpty(template))
            template = BuildConfig.DefaultGeneratedAssemblyMetaDataTemplate;

        foreach (var p in effectiveBuildConfig.Projects)
        {
            var relativeProjectFile = Context.Environment.WorkingDirectory.GetRelativePath(p.ProjectFile);
            var metaDataFile = FindMetaDataFile(p);
            if (metaDataFile == null)
            {
                Context.Log.Verbose(
                    string.Format(
                        "Skipping update of build metadata for project '{0}' as none of the project files matches '{1}'.",
                        relativeProjectFile,
                        BuildConfig.GeneratedAssemblyMetaDataReference));
                continue;
            }

            var path = p.ProjectFile;
            var proj = p.Project;
            var projectMeta = BuildConfig.ProjectAssemblyProperties(proj, path, new Dictionary<string, string>(metadata));
            var sortedMetaData = new SortedDictionary<string, string>(projectMeta, StringComparer.InvariantCulture);
            var relativePath = p.ProjectFile.GetRelativePath(metaDataFile.FilePath);

            var b = new StringBuilder();
            b.Append("using System.Reflection;");
            b.Append(Environment.NewLine);
            b.Append("using System.Runtime.InteropServices;");
            b.Append(Environment.NewLine);
            b.Append(Environment.NewLine);

            foreach (var entry in sortedMetaData)
            {
                b.Append("[assembly: AssemblyMetadata(@\"");
                b.Append(EscapeLiteralString(entry.Key));
                b.Append("\", @\"");
                b.Append(EscapeLiteralString(entry.Value));
                b.Append("\")]");
                b.Append(Environment.NewLine);
            }

            var file = Context.FileSystem.GetFile(metaDataFile.FilePath);
            Context.Log.Information(string.Format("Generating build metadata for project '{0}' in '{1}'.",
                relativeProjectFile, relativePath));
            using (var writer = file.OpenWrite())
            {
                using (var sw = new StreamWriter(writer, Encoding.UTF8))
                {
                    sw.Write(template, b);
                    Context.Log.Debug(string.Format("Generated metadata:\n{0}.", string.Format(template, b)));
                }
            }
        }
    }

    static string EscapeLiteralString(string input)
    {
        return input.Replace("\"", "\"\"");
    }

    static ProjectFile FindMetaDataFile(ParsedProject p)
    {
        var projectFiles = p.Project.Files;
        return
            projectFiles.FirstOrDefault(
                f => f.Compile && f.RelativePath.EndsWith(BuildConfig.GeneratedAssemblyMetaDataReference));
    }

    /// <summary>
    ///     Forward call to satisfy the Action() requirement on Cake's ActionTask.
    /// </summary>
    public static void CompileProject()
    {
        CompileProject(BuildConfig.BuildSettings);
    }

    /// <summary>
    ///     Compiles the projects and then copies the output into a well-defined and well-known directory structure.
    ///     This shields against weird projects that try to be smart with their "outputDir" property.
    /// </summary>
    /// <param name="settings"></param>
    public static void CompileProject(AnyBuildSettings settings)
    {
        var effectiveBuildConfig = BuildConfig.Config;

        if (effectiveBuildConfig.Solution != null)
            foreach (var projectWithPlatform in effectiveBuildConfig.ProjectsByPlatform)
            {
                InvokeXBuild(effectiveBuildConfig.Solution, settings, projectWithPlatform.Key);

                foreach (var p in projectWithPlatform.Value)
                {
                    SalvageBuildResults(p);
                    PublishActions.ProduceNuGetPackage(p);
                }
            }
        else
            foreach (var projectWithPlatform in effectiveBuildConfig.ProjectsByPlatform)
            foreach (var p in projectWithPlatform.Value)
            {
                InvokeXBuild(p.ProjectFile, settings, projectWithPlatform.Key);
                SalvageBuildResults(p);
                PublishActions.ProduceNuGetPackage(p);
            }
    }

    internal static DirectoryPath ComputeProjectBinPath(ParsedProject project)
    {
        return BuildConfig.ComputeOutputPath("compiled", project);
    }

    public static void SalvageBuildResults(ParsedProject project)
    {
        var relativeProjectFile = Context.Environment.WorkingDirectory.GetRelativePath(project.ProjectFile);
        Context.Log.Verbose(string.Format("Salvaging build result for {0} with platform {1}", relativeProjectFile,
            project.Platform));

        var outputDir = project.ProjectFile.GetDirectory().Combine(project.Project.OutputPath);
        var targetDir = ComputeProjectBinPath(project);
        Context.CopyDirectory(outputDir, targetDir);
    }

    public static void InvokeXBuild(FilePath path, AnyBuildSettings settings, PlatformTarget platformTarget)
    {
        settings = new AnyBuildSettings(settings);
        settings.PlatformTarget = platformTarget;

        if (settings == null)
            throw new ArgumentNullException("settings");

        var targets = new List<string>();
        targets.AddRange(settings.Targets);
        if (targets.Count == 0)
            targets.Add("Build");

        var relativeProjectFile = Context.Environment.WorkingDirectory.GetRelativePath(path);
        Context.Log.Verbose(string.Format("Starting build for {0} with platform {1}", relativeProjectFile,
            platformTarget));

        foreach (var target in targets)
            if (Context.Environment.Platform.Family == PlatformFamily.Windows && !BuildConfig.UseXBuildOnWindows)
                Context.MSBuild(path, XBuildHelper.CreateMSBuildSettings(settings, target));
            else
                Context.XBuild(path, XBuildHelper.CreateXBuildSettings(settings, path, target));
        Context.Log.Verbose(string.Format("Finished build for {0} with platform {1}", relativeProjectFile,
            platformTarget));
    }
}