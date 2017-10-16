using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.GitVersion;
using Cake.Common.Tools.NuGet;
using Cake.Common.Tools.NuGet.Pack;
using Cake.Common.Tools.NuGet.Push;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public static class PublishActions
{
    // Some useful defaults.
    public const string DefaultSymbolServerUrl = "https://nuget.smbsrc.net/";
    public const string V3FeedUrl = "https://api.nuget.org/v3/index.json";
    public const string V2FeedUrl = "https://www.nuget.org/api/v2/";

    static PublishActions()
    {
        PushSettings = new NuGetXPushSettings();
        PackSettings = new NuGetXPackSettings();
        AdditionalPackages = new Dictionary<FilePath, NuGetXPackSettings>();
    }

    public static Dictionary<FilePath, NuGetXPackSettings> AdditionalPackages { get; private set; }

    public static ICakeContext Context { get; private set; }
    public static NuGetXPushSettings PushSettings { get; set; }
    public static NuGetXPackSettings PackSettings { get; set; }

    public static Func<NuGetXPackSettings, ParsedProject, NuGetXPackSettings> PackSettingsCustomisation { get; set; }

    /// <summary>
    ///     Use this for real low-level access to the packaging. Sane processes should not require that.
    /// </summary>
    public static Func<NuGetPackSettings, ParsedProject, NuGetPackSettings> NuGetPackSettingsCustomisation { get; set; }

    /// <summary>
    ///     Version used when building and publishing NuGet packages. If null or empty, this will use the
    ///     assembly information (AssemblyInformalVersion or AssemblyVersion), which in many cases is not
    ///     what you'd want.
    /// </summary>
    public static string Version { get; set; }

    /// <summary>
    ///     Version string used when building documentation.
    /// </summary>
    public static string ApiVersion { get; set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
        Version = Context.Argument("version", AutoConfigureVersion());
        ApiVersion = Context.Argument("api-version", AutoConfigureApiVersion());

        PushSettings.SymbolsApiKey = Context.Environment.GetEnvironmentVariable("NUGET_SYMBOLS_API_KEY");
        PushSettings.SymbolsSource = Context.Environment.GetEnvironmentVariable("NUGET_SYMBOLS_SOURCE");
        PushSettings.ApiKey = Context.Environment.GetEnvironmentVariable("NUGET_API_KEY");
        PushSettings.Source = Context.Environment.GetEnvironmentVariable("NUGET_SOURCE");
    }

    /// <summary>
    ///     Thanks to NuGet being an extension of VisualStudio instead of a proper standalone
    ///     tool (just look at the tight binding of 'nuget pack .xxproj' into MSBuild internals
    ///     and the massive amount of code just to manage that stuff), we cannot build NuGet
    ///     packages from existing binaries by just looking at the project and nuspec files.
    ///     Therefore we preemtively produce nuget packages as soon as the compiler finished
    ///     building the project (and before any testing has been done). We then copy the
    ///     NuGet package into a safe place before eventually pushing it to a server or achiving
    ///     the files by other means.
    /// </summary>
    /// <param name="project"></param>
    public static void ProduceNuGetPackage(ParsedProject project)
    {
        var nuspec = project.ProjectFile.ChangeExtension(".nuspec");
        if (!Context.FileExists(nuspec))
        {
            Context.Log.Verbose("Skipping package as there is no *.nuspec file for project "
                                + BuildConfig.AsRelativePath(project.ProjectFile));
            return;
        }

        var settings = new NuGetXPackSettings(PackSettings);

        if (PackSettingsCustomisation != null)
            settings = PackSettingsCustomisation.Invoke(settings, project);

        var nugetSettings = new NuGetPackSettings();
        XBuildHelper.ApplyToolSettings(nugetSettings, settings);
        nugetSettings.Verbosity = settings.Verbosity;
        nugetSettings.Symbols = settings.Symbols.GetValueOrDefault();
        nugetSettings.IncludeReferencedProjects = settings.IncludeReferencedProjects.GetValueOrDefault();
        nugetSettings.Properties = new Dictionary<string, string>(settings.Properties);
        nugetSettings.ArgumentCustomization = settings.ArgumentCustomization;

        if (NuGetPackSettingsCustomisation != null)
        {
            nugetSettings = NuGetPackSettingsCustomisation.Invoke(nugetSettings, project);
            if (nugetSettings.Properties == null)
                nugetSettings.Properties = new Dictionary<string, string>();
        }

        var targetPath = BuildConfig.ComputeOutputPath("packages", project);
        Context.CreateDirectory(targetPath);
        nugetSettings.WorkingDirectory = targetPath;
        nugetSettings.Properties["Configuration"] = BuildConfig.Configuration;
        nugetSettings.Properties["Platform"] = BuildConfig.ConvertPlatformTargetToString(project.Platform);
        if (!string.IsNullOrEmpty(Version))
        {
            Context.Log.Information("Publishing package as version " + Version);
            nugetSettings.Properties["version"] = Version;
            nugetSettings.Version = Version;
        }

        if (settings.Tool.GetValueOrDefault())
        {
            var argCustomization = nugetSettings.ArgumentCustomization;
            nugetSettings.ArgumentCustomization = args =>
            {
                if (argCustomization != null)
                    args = argCustomization.Invoke(args);
                args.Append("-Tool");
                return args;
            };
        }

        Context.NuGetPack(project.ProjectFile, nugetSettings);

        var assembly = LoadZipAssembly();
        if (assembly != null)
        {
            Context.Log.Information("Unzipping nuget package: " + targetPath.FullPath + "/*.nupkg");
            foreach (var file in Context.Globber.GetFiles(targetPath.FullPath + "/*.nupkg"))
            {
                Context.Log.Information("Unzipping " + file);
                Unzip(file, targetPath, assembly);
            }
        }
    }

    static Assembly LoadZipAssembly()
    {
        try
        {
            return Assembly.Load
                ("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
        }
        catch
        {
            try
            {
                return Assembly.Load
                    ("System.IO.Compression.FileSystem, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }
            catch
            {
                return null;
            }
        }
    }

    public static void Unzip(FilePath zipPath, DirectoryPath outputPath, Assembly a)
    {
        if (zipPath == null)
            throw new ArgumentNullException("zipPath");
        if (outputPath == null)
            throw new ArgumentNullException("outputPath");

        /*
         * The code with reflection down below is the same as this clean code here.
         * Cake does not add the System.IO.Compression assembly to the script and 
         * therefore these standard classes are not directly callable. 
         * 
         * Thank god for reflection ..
         */
        //var zipArchive = System.IO.Compression.ZipFile.OpenRead(zipPath.FullPath);
        //var nuSpecEntry = zipArchive.Entries.FirstOrDefault(e => e.Name.EndsWith(".nuspec"));
        //if (nuSpecEntry != null)
        //{
        //    var target = outputPath.CombineWithFilePath(nuSpecEntry.Name).MakeAbsolute(Context.Environment).FullPath;
        //    using (var s = nuSpecEntry.Open())
        //    {
        //        using (var f = File.Open(target, FileMode.Create))
        //        {
        //            s.CopyTo(f);
        //        }
        //    }
        //}
        try
        {
            var entryNames = new List<string>();
            var zipFileType = a.GetType("System.IO.Compression.ZipFile");
            var methodInfo = zipFileType.GetMethod("OpenRead", new[] {typeof(string)});
            var archive = methodInfo.Invoke(null, new object[] {zipPath.FullPath});
            var s = archive as IDisposable;
            using (s)
            {
                var entries =
                    archive.GetType().GetProperty("Entries").GetMethod.Invoke(archive, new object[0]) as IEnumerable;
                foreach (var entry in entries)
                {
                    var fullName = entry.ToString();
                    if (fullName.EndsWith(".nuspec") && !fullName.Contains("/"))
                    {
                        // found the file..
                        Context.Log.Debug(fullName);
                        var stream = entry.GetType().GetMethod("Open").Invoke(entry, new object[0]) as Stream;
                        using (stream)
                        {
                            var target =
                                outputPath.CombineWithFilePath(fullName).MakeAbsolute(Context.Environment).FullPath;
                            // There is a auto-generated File method in the current script context. So we need to fully qualify that reference here.
                            // ReSharper disable once RedundantNameQualifier
                            Stream f = System.IO.File.Open(target, FileMode.Create);
                            using (f)
                            {
                                stream.CopyTo(f);
                            }
                        }
                    }
                    entryNames.Add(fullName);
                }
            }

            var contentsPath = zipPath.AppendExtension(".contents.txt");
            // ReSharper disable once RedundantNameQualifier
            System.IO.File.WriteAllLines(contentsPath.FullPath, entryNames);
        }
        catch (Exception e)
        {
            if (Context.Log.Verbosity > Verbosity.Verbose)
                Context.Log.Information("There was an error extracting the final nuspec file from the nupkg file.", e);
            else
                Context.Log.Information("There was an error extracting the final nuspec file from the nupkg file.");
        }
    }

    public static string AutoConfigureVersion()
    {
        try
        {
            var gitVersion = Context.GitVersion();
            return gitVersion.NuGetVersion;
        }
        catch
        {
            Context.Log.Verbose("Unable to compute version information. Maybe this is not a GitVersion style project?");
            return null;
        }
    }

    public static string AutoConfigureApiVersion()
    {
        try
        {
            var gitVersion = Context.GitVersion();
            return gitVersion.Major + "." + gitVersion.Minor;
        }
        catch
        {
            Context.Log.Verbose("Unable to compute version information. Maybe this is not a GitVersion style project?");
            return null;
        }
    }

    public static void BuildPackages()
    {
        foreach (var p in AdditionalPackages)
        {
            NuGetXPackSettings settings;
            if (p.Value == null)
                settings = new NuGetXPackSettings(PackSettings);
            else
                settings = new NuGetXPackSettings(p.Value);

            if (!string.IsNullOrEmpty(Version))
                settings.Properties["version"] = Version;

            var targetPath = Context.Directory(string.Format("{0}/{1}/{2}",
                BuildConfig.TargetDirectory,
                "user-packages",
                BuildConfig.Config.Configuration));

            var nugetSettings = new NuGetPackSettings();
            XBuildHelper.ApplyToolSettings(nugetSettings, settings);
            nugetSettings.Symbols = settings.Symbols.GetValueOrDefault();
            nugetSettings.Properties = new Dictionary<string, string>(settings.Properties);
            nugetSettings.ArgumentCustomization = settings.ArgumentCustomization;

            Context.CreateDirectory(targetPath);
            nugetSettings.WorkingDirectory = targetPath;
            nugetSettings.Properties["Configuration"] = BuildConfig.Configuration;
            if (!string.IsNullOrEmpty(Version))
                nugetSettings.Properties["version"] = Version;

            if (settings.Tool.GetValueOrDefault())
            {
                var argCustomization = nugetSettings.ArgumentCustomization;
                nugetSettings.ArgumentCustomization = args =>
                {
                    if (argCustomization != null)
                        args = argCustomization.Invoke(args);
                    args.Append("-Tool");
                    return args;
                };
            }

            Context.NuGetPack(p.Key, nugetSettings);
        }
    }

    public static void PublishNuGetPackages()
    {
        var filePaths = Context.Globber.GetFiles(BuildConfig.TargetDirectory + "/**/*.nupkg").ToList();
        foreach (var p in filePaths)
        {
            var pushSettings = new NuGetPushSettings();
            XBuildHelper.ApplyToolSettings(pushSettings, PushSettings);
            pushSettings.ArgumentCustomization = PushSettings.ArgumentCustomization;
            pushSettings.Timeout = PushSettings.Timeout;
            pushSettings.ConfigFile = PushSettings.ConfigFile;
            pushSettings.Verbosity = PushSettings.Verbosity;

            var pathAsString = p.ToString();
            if (pathAsString.EndsWith(".symbols.nupkg"))
            {
                if (PushSettings.NoSymbols)
                    continue;

                // symbol package
                pushSettings.ApiKey = PushSettings.SymbolsApiKey;
                pushSettings.Source = PushSettings.SymbolsSource;

                if (string.IsNullOrEmpty(pushSettings.Source))
                {
                    Context.Log.Information("Skipping package " + BuildConfig.AsRelativePath(p)
                                            + " as no valid NuGet symbol server is defined.");
                    continue;
                }
            }
            else
            {
                pushSettings.ApiKey = PushSettings.ApiKey;
                pushSettings.Source = PushSettings.Source;

                if (string.IsNullOrEmpty(pushSettings.Source))
                {
                    Context.Log.Information("Skipping package " + BuildConfig.AsRelativePath(p)
                                            + " as no valid NuGet target server is defined.");
                    continue;
                }
            }

            var argsOrg = pushSettings.ArgumentCustomization;
            pushSettings.ArgumentCustomization = args =>
            {
                if (argsOrg != null)
                    args = argsOrg.Invoke(args);
                args.Append("-NoSymbols");
                args.Append("-NonInteractive");
                return args;
            };

            Context.NuGetPush(p, pushSettings);
        }
    }
}