using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.Tools.Cake;
using Cake.Core;

public static class CakeRunnerAlias
{
    static string GetCommandLine()
    {
        var envType = typeof(Environment);
        var propertyInfo = envType.GetProperty("CommandLine");
        if (propertyInfo != null)
            return (string) propertyInfo.GetMethod.Invoke(null, new object[0]);

        return string.Join(" ", Environment.GetCommandLineArgs());
    }

    public static void RunCake(ICakeContext context, string script = null, CakeSettings settings = null)
    {
        var rawArgs = QuoteAwareStringSplitter.Split(GetCommandLine()).Skip(1) // Skip executable.
            .ToArray();

        settings = settings ?? new CakeSettings();

        var ar = new InternalArgumentParser(context.Log);
        var baseOptions = ar.Parse(rawArgs);
        var mergedOptions = new Dictionary<string, string>();
        foreach (var optionsArgument in baseOptions)
        {
            if (optionsArgument.Key == "target" || string.IsNullOrEmpty(optionsArgument.Value))
                continue;

            mergedOptions[optionsArgument.Key] = optionsArgument.Value;
        }

        if (settings.Arguments != null)
            foreach (var optionsArgument in settings.Arguments)
            {
                if (string.IsNullOrEmpty(optionsArgument.Value))
                {
                    mergedOptions.Remove(optionsArgument.Key);
                    continue;
                }

                mergedOptions[optionsArgument.Key] = optionsArgument.Value;
            }

        mergedOptions.Remove("script");
        settings.Arguments = mergedOptions;
        script = script ?? "./build.cake";

        var cakeRunner = new FixedCakeRunner(context.FileSystem, context.Environment, context.Globber,
            context.ProcessRunner, context.Tools, context.Log);
        cakeRunner.ExecuteScript(script, settings);
    }
}