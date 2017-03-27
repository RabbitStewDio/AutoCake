using Cake.Common.IO;
using Cake.Common.Tools.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public class ParsedProject
{
    ParsedProject(ExtProjectParserResult parsedProject, FilePath p, string configuration, PlatformTarget platform)
    {
        Configuration = configuration;
        Platform = platform;
        ProjectFile = p;
        Project = parsedProject;
    }

    public string Configuration { get; private set; }
    public PlatformTarget Platform { get; private set; }
    public FilePath ProjectFile { get; private set; }
    public ExtProjectParserResult Project { get; private set; }

    public static ParsedProject Parse(ICakeContext context, FilePath p, string configuration, PlatformTarget platform)
    {
        var file = PreProcessProject(context, platform, p);
        var project = new ExtProjectParser(context).Parse(file, p, configuration, PlatformAsString(platform));
        if (project == null)
        {
            context.Log.Verbose("Skipped project " + p + " as the parser did not find a valid configuration.");
            return null;
        }
        return new ParsedProject(project, p, configuration, platform);
    }

    static string PlatformAsString(PlatformTarget t)
    {
        if (t == PlatformTarget.MSIL)
            return "AnyCPU";
        return t.ToString();
    }

    static FilePath PreProcessProject(ICakeContext context, PlatformTarget target, FilePath input)
    {
        var targetDirName = string.Format("{0}/build-analysis/{1}/{2}",
            BuildConfig.TargetDirectory,
            target,
            BuildConfig.Configuration);
        var directory = context.Directory(targetDirName);
        context.CreateDirectory(directory);
        var targetFile = directory.Path.CombineWithFilePath(input.GetFilename().ChangeExtension("tmp.xml"))
            .MakeAbsolute(context.Environment);
        if (XBuildHelper.DotNetExists)
        {
            var buildSettings = CreatePreProcessingMSBuildSettings(target, targetFile);

            var tool = new DotNetGeneralTool(context.FileSystem,
                context.Environment,
                context.ProcessRunner,
                context.Tools);
            tool.DotNetMSBuild(input, buildSettings);
            return targetFile;
        }
        if (context.Environment.Platform.Family == PlatformFamily.Windows)
        {
            var buildSettings = CreatePreProcessingMSBuildSettings(target, targetFile);
            context.MSBuild(input, buildSettings);
            return targetFile;
        }
        return input;
    }

    static MSBuildSettings CreatePreProcessingMSBuildSettings(PlatformTarget target, FilePath targetFile)
    {
        var settings = new AnyBuildSettings(BuildConfig.BuildSettings);
        settings.PlatformTarget = target;
        var buildSettings = XBuildHelper.CreateMSBuildSettings(settings);
        var argsCustomizer = buildSettings.ArgumentCustomization;
        buildSettings.ArgumentCustomization = args =>
        {
            if (argsCustomizer != null)
                args = argsCustomizer.Invoke(args);
            args.AppendQuoted("/preprocess:" + targetFile.FullPath);
            return args;
        };
        return buildSettings;
    }
}