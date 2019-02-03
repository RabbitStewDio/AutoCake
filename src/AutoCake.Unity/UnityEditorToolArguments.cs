using System;
using System.Collections.Generic;
using System.Dynamic;
using Cake.Core;
using Cake.Core.IO;

/// <summary>
/// Command-line arguments accepted by Unity Editor.
/// </summary>
public class UnityEditorToolArguments
{
    readonly ExpandoObject customArguments = new ExpandoObject();

    public UnityEditorToolArguments()
    {
    }

    /// <summary>
    /// <para>Run Unity in batch mode. You should always use this in conjunction with the other command line arguments, because it ensures no pop-up windows appear and eliminates the need for any human intervention. When an exception occurs during execution of the script code, the Asset server updates fail, or other operations fail, Unity immediately exits with return code 1. </para>
    /// <para>Note that in batch mode, Unity sends a minimal version of its log output to the console. However, the Log Files still contain the full log information. You cannot open a project in batch mode while the Editor has the same project open; only a single instance of Unity can run at a time. </para>
    /// <para>Tip: To check whether you are running the Editor or Standalone Player in batch mode, use the Application.isBatchMode operator. </para>
    /// </summary>
    public bool? BatchMode { get; set; }

    public UnityBuildTargetType? BuildTarget { get; set; }

    /// <summary>
    /// Build a 32-bit standalone Windows player (for example, -buildWindowsPlayer path/to/your/build.exe).
    /// </summary>
    public DirectoryPath BuildTargetPath { get; set; }

    public string BuildTargetExecutable { get; set; }

    /// <summary>
    /// Execute the static method as soon as Unity opens the project, and after the optional Asset server update is complete. You can use this to do tasks such as continous integration, performing Unit Tests, making builds or preparing data. To return an error from the command line process, either throw an exception which causes Unity to exit with return code 1, or call EditorApplication.Exit with a non-zero return code. To pass parameters, add them to the command line and retrieve them inside the function using System.Environment.GetCommandLineArgs. To use -executeMethod, you need to place the enclosing script in an Editor folder. The method you execute must be defined as static.
    /// </summary>
    public string ExecuteMethod { get; set; }

    /// <summary>
    /// Specify where the Editor or Windows/Linux/OSX standalone log file are written. If the path is ommitted, OSX and Linux will write output to the console. Windows uses the path %LOCALAPPDATA%\Unity\Editor\Editor.log as a default.
    /// </summary>
    public FilePath LogFile { get; set; }

    /// <summary>
    /// Open the project at the given path.
    /// </summary>
    public DirectoryPath ProjectPath { get; set; }

    /// <summary>
    /// Quit the Unity Editor after other commands have finished executing. Note that this can cause error messages to be hidden (however, they still appear in the Editor.log file).
    /// </summary>
    public bool? Quit { get; set; }

    /// <summary>
    /// <para>Custom arguments which can further be processed in Unity Editor script by calling System.Environment.GetCommandLineArgs method. </para>
    /// <para>They are supplied among other arguments in format "--key=value". </para>
    /// <para>Expected to be used in conjunction with ExecuteMethod argument. </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var arguments = new UnityEditorArguments { ExecuteMethod = "Build.RunCommandLineBuild" };
    /// arguments.Custom.buildNumber = 42;
    /// arguments.Custom.packageTitle = "Game of Builds";
    /// </code>
    /// </example>
    public dynamic Custom
    {
        get { return customArguments; }
    }

    /// <summary>
    /// <para>Custom arguments which can further be processed in Unity Editor script by calling System.Environment.GetCommandLineArgs method. </para>
    /// <para>They are supplied among other arguments in format "--key=value". </para>
    /// <para>Expected to be used in conjunction with ExecuteMethod argument. </para>
    /// </summary>
    /// <example>
    /// <code>
    /// new UnityEditorArguments
    /// {
    ///     ExecuteMethod = "Build.RunCommandLineBuild",
    ///     SetCustomArguments = x =>
    ///     {
    ///         x.buildNumber = 42;
    ///         x.packageTitle = "Game of Builds";
    ///     },
    /// };
    /// </code>
    /// </example>
    public Action<dynamic> SetCustomArguments
    {
        set { value.Invoke(customArguments); }
    }



    public static UnityEditorToolArguments MergeArguments(UnityEditorToolArguments arguments,
                                                          UnityEditorToolArguments globalDefaults)
    {
        arguments = arguments ?? new UnityEditorToolArguments();

        var retval = new UnityEditorToolArguments();
        retval.BatchMode = arguments.BatchMode ?? globalDefaults.BatchMode;
        retval.Quit = arguments.Quit ?? globalDefaults.Quit;
        retval.BuildTarget = arguments.BuildTarget ?? globalDefaults.BuildTarget;
        retval.BuildTargetPath = arguments.BuildTargetPath ?? globalDefaults.BuildTargetPath;

        var dict = retval.customArguments as IDictionary<string, object>;
        foreach (var k in arguments.customArguments)
        {
            dict.Add(k);
        }
        foreach (var k in globalDefaults.customArguments)
        {
            dict.Add(k);
        }

        retval.ExecuteMethod = arguments.ExecuteMethod ?? globalDefaults.ExecuteMethod;
        retval.LogFile = arguments.LogFile ?? globalDefaults.LogFile;
        retval.ProjectPath = arguments.ProjectPath ?? globalDefaults.ProjectPath;
        return retval;
    }
}