using System;
using System.Globalization;
using System.Linq;
using Cake.Common.Tools.DotNetCore;
using Cake.Common.Tools.DotNetCore.Build;
using Cake.Common.Tools.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;

public class DotNetGeneralTool : DotNetCoreTool<DotNetCoreBuildSettings>
{
    readonly ICakeEnvironment environment;

    public DotNetGeneralTool(IFileSystem fileSystem,
        ICakeEnvironment environment,
        IProcessRunner processRunner,
        IToolLocator tools) : base(fileSystem, environment, processRunner, tools)
    {
        this.environment = environment;
    }

    public bool DotNetExists(DotNetCoreBuildSettings settings)
    {
        if (settings == null)
            settings = new DotNetCoreBuildSettings();
        try
        {
            var args = CreateArgumentBuilder(settings);
            args.Append("--version");
            Run(settings, args, new ProcessSettings
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }, null);
            return true;
        }
        catch (Exception e)
        {
            XBuildHelper.Context.Log.Debug("Failed to run dotnet command, so let's assume it does not exist.", e);
            return false;
        }
    }

    public void DotNetMSBuild(FilePath solution, MSBuildSettings settings)
    {
        if (settings == null)
            settings = new MSBuildSettings();

        var toolSettings = new DotNetCoreBuildSettings();
        XBuildHelper.ApplyToolSettings(toolSettings, settings);
        toolSettings.ArgumentCustomization = settings.ArgumentCustomization;
        Run(toolSettings, GetMSBuildArguments(solution, settings, environment));
    }

    static string GetVerbosityName(Verbosity verbosity)
    {
        switch (verbosity)
        {
            case Verbosity.Quiet:
                return "quiet";
            case Verbosity.Minimal:
                return "minimal";
            case Verbosity.Normal:
                return "normal";
            case Verbosity.Verbose:
                return "detailed";
            case Verbosity.Diagnostic:
                return "diagnostic";
        }
        throw new CakeException("Encountered unknown MSBuild build log verbosity.");
    }

    static ProcessArgumentBuilder GetMSBuildArguments(FilePath solution, MSBuildSettings settings, ICakeEnvironment env)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("msbuild");

        // Set the maximum number of processors?
        if (settings.MaxCpuCount != null)
            builder.Append(settings.MaxCpuCount > 0 ? string.Concat("/m:", settings.MaxCpuCount) : "/m");

        // Set the detailed summary flag.
        if (settings.DetailedSummary.GetValueOrDefault())
            builder.Append("/ds");

        // Set the no console logger flag.
        if (settings.NoConsoleLogger.GetValueOrDefault())
            builder.Append("/noconlog");

        // Set the verbosity.
        builder.Append(string.Format(CultureInfo.InvariantCulture, "/v:{0}", GetVerbosityName(settings.Verbosity)));

        if (settings.NodeReuse != null)
            builder.Append(string.Concat("/nr:", settings.NodeReuse.Value ? "true" : "false"));

        // Got a specific configuration in mind?
        if (!string.IsNullOrWhiteSpace(settings.Configuration))
            builder.AppendSwitchQuoted("/p:Configuration", "=", settings.Configuration);

        // Build for a specific platform?
        if (settings.PlatformTarget.HasValue)
        {
            var platform = settings.PlatformTarget.Value;
            var isSolution = string.Equals(solution.GetExtension(), ".sln", StringComparison.OrdinalIgnoreCase);
            builder.Append(string.Concat("/p:Platform=", XBuildHelper.GetPlatformName(platform, isSolution)));
        }

        // Got any properties?
        if (settings.Properties.Count > 0)
            foreach (var property in settings.Properties)
            {
                var propertyKey = property.Key;
                foreach (var propertyValue in property.Value)
                {
                    var arg = string.Concat("/p:", propertyKey, "=", propertyValue);
                    builder.Append(arg);
                }
            }

        // Got any targets? 
        if (settings.Targets.Count > 0)
        {
            var targets = string.Join(";", settings.Targets);
            builder.Append(string.Concat("/target:", targets));
        }

        if (settings.Loggers.Count > 0)
            foreach (var logger in settings.Loggers)
            {
                var argument = GetLoggerArgument(logger);
                builder.Append(argument);
            }

        // Got any file loggers?
        if (settings.FileLoggers.Count > 0)
        {
            var arguments = settings.FileLoggers.Select((logger, index) => { return GetLoggerArgument(index, logger, env); });

            foreach (var argument in arguments)
                builder.Append(argument);
        }

        builder.AppendQuoted(solution.FullPath);
        return builder;
    }

    static string GetLoggerArgument(int index, MSBuildFileLogger logger, ICakeEnvironment env)
    {
        if (index >= 10)
            throw new InvalidOperationException("Too Many FileLoggers");

        var counter = index == 0 ? string.Empty : index.ToString();
        var argument = string.Format("/fl{0}", counter);

        var parameters = logger.GetParameters(env);
        if (!string.IsNullOrWhiteSpace(parameters))
            argument = string.Format("{0} /flp{1}:{2}", argument, counter, parameters);
        return argument;
    }

    static string GetLoggerArgument(MSBuildLogger logger)
    {
        var argument = "/logger:";
        if (!string.IsNullOrWhiteSpace(logger.Class))
            argument += string.Format("{0},{1}", logger.Class, logger.Assembly);
        else
            argument += logger.Assembly;
        if (!string.IsNullOrWhiteSpace(logger.Parameters))
            argument += string.Concat(";", logger.Parameters);
        return argument;
    }
}