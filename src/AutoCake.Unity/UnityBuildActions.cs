using System;
using System.Linq;
using System.Text;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Unity;
using LogLevel = Cake.Core.Diagnostics.LogLevel;
using Verbosity = Cake.Core.Diagnostics.Verbosity;

public static class UnityBuildActions
{
    public static ICakeContext Context { get; private set; }
    public static UnityEditorToolSettings Settings { get; private set; }
    public static UnityEditorToolArguments Arguments { get; private set; }
    public static FilePath PackageFilePath { get; set; }

    static UnityBuildActions()
    {
        Settings = new UnityEditorToolSettings();
        Arguments = new UnityEditorToolArguments();
        Arguments.BuildTargetExecutable = "Game";
        Arguments.BuildTargetPath = "bin";
    }

    public static void Configure(ICakeContext c)
    {
        Context = c;
        Arguments.ProjectPath = Context.Environment.WorkingDirectory;
    }

    public static void Initialize()
    {
        string version = DetectProjectVersion();
        var editors = Context.FindUnityEditors()
                             .Where(e => e.Version.ToString() == version)
                             .ToList();
        if (editors.Count > 0)
        {
            Context.Log.Write(Verbosity.Normal, LogLevel.Information, 
                              "Auto-Selected Unity Editor {0} at location {1}", editors[0].Version.ToString(), editors[0].Path);
            Settings.ToolPath = editors[0].Path;
        }
    }

    public static void CleanBinaries()
    {
        var targetPath = Arguments.BuildTargetPath;
        if (targetPath == null)
        {
            throw new ArgumentException();
        }

        Context.CleanDirectory(targetPath);
    }

    public static string DetectProjectVersion(DirectoryPath projectPath = null)
    {
        if (projectPath == null)
        {
            projectPath = Arguments.ProjectPath ?? Context.Environment.WorkingDirectory;
        }

        var path = projectPath.MakeAbsolute(Context.Environment); 
        var projectVersion = path.Combine(DirectoryPath.FromString("ProjectSettings")).CombineWithFilePath("ProjectVersion.txt");
        if (!Context.FileSystem.GetFile(projectVersion).Exists)
        {
            throw new ArgumentException("Project path " + projectPath + " is not a valid Unity project.");
        }

        var file = Context.FileSystem.GetFile(projectVersion);
        var projectVersionText = string.Join("\n", file.ReadLines(Encoding.UTF8));
        if (!projectVersionText.StartsWith("m_EditorVersion: "))
        {
            throw new ArgumentException("Project does not contain valid version information.");
        }

        return projectVersionText.Substring("m_EditorVersion: ".Length).Trim();
    }

    public static void UnityEditor(FilePath unityEditor = null, UnityEditorToolArguments arguments = null)
    {
        var tool = new UnityEditorTool(Context.FileSystem, Context.Environment, Context.ProcessRunner, Context.Tools, Context.Log);
        tool.RunUnityEditor(unityEditor, UnityEditorToolArguments.MergeArguments(arguments, Arguments), Settings);
    }

    public static void PackageProject()
    {
        if (PackageFilePath != null)
        {
            Context.CreateDirectory(PackageFilePath.GetDirectory());
            Context.Zip(Arguments.BuildTargetPath, PackageFilePath);
        }
    }

    public static void CompileProject()
    {
        UnityEditor();
    }
}