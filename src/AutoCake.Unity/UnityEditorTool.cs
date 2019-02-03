using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Unity;

public class UnityEditorTool : Tool<UnityEditorToolSettings>
{
    private readonly IFileSystem fileSystem;
    private readonly ICakeEnvironment environment;
    private readonly ICakeLog log;

    public UnityEditorTool(IFileSystem fileSystem, ICakeEnvironment environment, IProcessRunner processRunner, IToolLocator tools, ICakeLog log)
        : base(fileSystem, environment, processRunner, tools)
    {
        this.fileSystem = fileSystem;
        this.environment = environment;
        this.log = log;
    }

    protected override string GetToolName()
    {
        return "Unity Editor";
    }

    protected override IEnumerable<string> GetToolExecutableNames()
    {
        return new[] {"Unity.exe"};
    }

    public void RunUnityEditor(UnityEditorDescriptor unityEditor, UnityEditorToolArguments arguments, UnityEditorToolSettings settings)
    {
        RunUnityEditor(unityEditor.Path, arguments, settings);
    }

    public void RunUnityEditor(UnityEditorToolArguments arguments, UnityEditorToolSettings settings)
    {
        RunUnityEditor((FilePath) null, arguments, settings);
    }

    public void RunUnityEditor(FilePath unityEditorPath, UnityEditorToolArguments arguments, UnityEditorToolSettings settings)
    {
        if (unityEditorPath != null)
        {
            settings = new UnityEditorToolSettings(settings);
            settings.ToolPath = unityEditorPath;
        }

        ErrorIfRealTimeLogSetButLogFileNotSet(settings, arguments);
        WarnIfLogFileNotSet(arguments);

        if (settings.RealTimeLog.GetValueOrDefault() && arguments.LogFile != null)
            RunWithRealTimeLog(arguments, settings);
        else
            RunWithLogForwardOnError(arguments, settings);
    }

    private void RunWithRealTimeLog(UnityEditorToolArguments arguments, UnityEditorToolSettings settings)
    {
        var logForwardCancellation = new CancellationTokenSource();

        var process = RunProcess(arguments, settings);

        System.Threading.Tasks.Task.Run(() =>
        {
            process.WaitForExit();
            logForwardCancellation.Cancel();
        });

        ForwardLogFileToOutputUntilCancel(arguments.LogFile, logForwardCancellation.Token);

        ProcessExitCode(process.GetExitCode());
    }

    private void RunWithLogForwardOnError(UnityEditorToolArguments arguments, UnityEditorToolSettings settings)
    {
        try
        {
            Run(arguments, settings);
        }
        catch
        {
            if (arguments.LogFile == null)
            {
                log.Error("Execution of Unity Editor failed.");
                log.Warning("Cannot forward log file to output because LogFile argument is missing.");
            }
            else
            {
                log.Error("Execution of Unity Editor failed.");
                log.Error("Please analyze log below for the reasons of failure.");
                ForwardLogFileToOutputInOnePass(arguments.LogFile);
            }

            throw;
        }
    }


    internal ProcessArgumentBuilder CustomizeCommandLineArguments(UnityEditorToolArguments args, 
                                                                  ProcessArgumentBuilder builder, 
                                                                  ICakeEnvironment environment)
    {
        if (args.BatchMode == null || args.BatchMode.Value)
            builder.Append("-batchmode");

        if (args.BuildTarget != null && args.BuildTarget != UnityBuildTargetType.None && args.BuildTargetPath != null)
        {
            var filePath = FilePath.FromString((args.BuildTargetExecutable ?? "Game") + SuffixFor(args.BuildTarget.Value));
            var path = args.BuildTargetPath.CombineWithFilePath(filePath);

            builder
                .Append("-build" + args.BuildTarget)
                .AppendQuoted(path.MakeAbsolute(environment).FullPath);
        }

        if (args.ExecuteMethod != null)
            builder
                .Append("-executeMethod")
                .Append(args.ExecuteMethod);

        if (args.LogFile != null)
            builder
                .Append("-logFile")
                .AppendQuoted(args.LogFile.FullPath);

        if (args.ProjectPath != null)
            builder
                .Append("-projectPath")
                .AppendQuoted(args.ProjectPath.MakeAbsolute(environment).FullPath);

        if (args.Quit == null || args.Quit.Value)
            builder
                .Append("-quit");

        foreach (var customArgument in args.Custom)
        {
            builder.Append(string.Format("--{0}={1}", customArgument.Key, customArgument.Value));
        }

        return builder;
    }

    string SuffixFor(UnityBuildTargetType buildTarget)
    {
        switch (buildTarget)
        {
            case UnityBuildTargetType.None:
                return "";
            case UnityBuildTargetType.WindowsPlayer:
            case UnityBuildTargetType.Windows64Player:
                return ".exe";
            case UnityBuildTargetType.OSXPlayer:
            case UnityBuildTargetType.OSX64Player:
            case UnityBuildTargetType.OSXUniversalPlayer:
                return ".app";
            case UnityBuildTargetType.Linux32Player:
            case UnityBuildTargetType.Linux64Player:
            case UnityBuildTargetType.LinuxUniversalPlayer:
                return "";
            default:
                throw new ArgumentOutOfRangeException("buildTarget", buildTarget, null);
        }
    }

    private void Run(UnityEditorToolArguments args, UnityEditorToolSettings settings)
    {
        var argumentBuilder = CustomizeCommandLineArguments(args, new ProcessArgumentBuilder(), environment);
        Run(settings, argumentBuilder);
    }

    private IProcess RunProcess(UnityEditorToolArguments args, UnityEditorToolSettings settings)
    {
        var argumentBuilder = CustomizeCommandLineArguments(args, new ProcessArgumentBuilder(), environment);
        return RunProcess(settings, argumentBuilder);
    }

    private void ForwardLogFileToOutputUntilCancel(FilePath logPath, CancellationToken cancellationToken)
    {
        foreach (var line in ReadLogUntilCancel(logPath, cancellationToken))
            ForwardLogLineToOutput(line);
    }

    private void ForwardLogFileToOutputInOnePass(FilePath logPath)
    {
        var logFile = fileSystem.GetFile(logPath);

        if (!logFile.Exists)
        {
            log.Warning("Unity Editor log file not found: {0}", logPath);
            return;
        }

        foreach (var line in ReadLogSafeInOnePass(logFile))
            ForwardLogLineToOutput(line);
    }

    private void ForwardLogLineToOutput(string line)
    {
        if (IsError(line))
            log.Error(line);
        else if (IsWarning(line))
            log.Warning(line);
        else
            log.Information(line);
    }

    private static bool IsError(string line)
    {
        return IsCSharpCompilerError(line);
    }

    private static bool IsWarning(string line)
    {
        return IsCSharpCompilerWarning(line);
    }

    private static bool IsCSharpCompilerError(string line)
    {
        return line.Contains(": error CS");
    }

    private static bool IsCSharpCompilerWarning(string line)
    {
        return line.Contains(": warning CS");
    }

    void Sleep()
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
    }

    IEnumerable<string> ReadLogUntilCancel(FilePath logPath, CancellationToken cancellationToken)
    {
        while (!(fileSystem.Exist(logPath)) && (!cancellationToken.IsCancellationRequested))
            Sleep();

        if (!fileSystem.Exist(logPath))
        {
            log.Warning("Unity Editor log file not found: {0}", logPath);
            yield break;
        }

        using (var stream = fileSystem.GetFile(logPath).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream || (!cancellationToken.IsCancellationRequested))
                {
                    if (!reader.EndOfStream)
                        yield return reader.ReadLine();
                    else
                        Sleep();
                }
            }
    }

    private static IEnumerable<string> ReadLogSafeInOnePass(IFile file)
    {
        using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                while (!reader.EndOfStream)
                    yield return reader.ReadLine();
    }

    private void ErrorIfRealTimeLogSetButLogFileNotSet(UnityEditorToolSettings settings, UnityEditorToolArguments arguments)
    {
        if (settings.RealTimeLog.GetValueOrDefault() && arguments.LogFile == null)
        {
            log.Error("Cannot forward log in real time because LogFile is not specified.");
        }
    }

    private void WarnIfLogFileNotSet(UnityEditorToolArguments arguments)
    {
        if (arguments.LogFile == null)
        {
            log.Warning("LogFile is not specified by Unity Editor arguments.");
            log.Warning("Please specify it for ability to forward Unity log to console.");
        }
    }
}