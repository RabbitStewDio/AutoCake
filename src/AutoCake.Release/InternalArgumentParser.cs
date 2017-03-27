using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Core;
using Cake.Core.Diagnostics;

internal class InternalArgumentParser
{
    readonly ICakeLog _log;

    readonly VerbosityParser _verbosityParser;

    internal InternalArgumentParser(ICakeLog log)
    {
        _log = log;
        _verbosityParser = new VerbosityParser(log);
    }

    public Dictionary<string, string> Parse(IEnumerable<string> args)
    {
        if (args == null)
            throw new ArgumentNullException("args");

        var options = new Dictionary<string, string>();
        var isParsingOptions = false;

        var arguments = args.ToList();

        foreach (var arg in arguments)
        {
            var value = arg.UnQuote();

            if (isParsingOptions)
                if (IsOption(value))
                {
                    if (!ParseOption(value, options))
                        return options;
                }
                else
                {
                    _log.Error("More than one build script specified.");
                    return options;
                }
            else
                try
                {
                    // If they didn't provide a specific build script, search for a default.
                    if (IsOption(arg))
                        if (!ParseOption(value, options))
                            return options;
                }
                finally
                {
                    // Start parsing options.
                    isParsingOptions = true;
                }
        }

        return options;
    }

    static bool IsOption(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return false;

        return arg.StartsWith("--") || arg.StartsWith("-");
    }

    bool ParseOption(string arg, Dictionary<string, string> options)
    {
        string name, value;

        var nameIndex = arg.StartsWith("--") ? 2 : 1;
        var separatorIndex = arg.IndexOfAny(new[] {'='});
        if (separatorIndex < 0)
        {
            name = arg.Substring(nameIndex);
            value = string.Empty;
        }
        else
        {
            name = arg.Substring(nameIndex, separatorIndex - nameIndex);
            value = arg.Substring(separatorIndex + 1);
        }

        return ParseOption(name, value.UnQuote(), options);
    }

    bool ParseOption(string name, string value, Dictionary<string, string> options)
    {
        if (name.Equals("verbosity", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("v", StringComparison.OrdinalIgnoreCase))
        {
            Verbosity verbosity;
            if (!_verbosityParser.TryParse(value, out verbosity))
                value = "normal";
        }

        if (name.Equals("showdescription", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("s", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (name.Equals("dryrun", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("noop", StringComparison.OrdinalIgnoreCase)
            || name.Equals("whatif", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (name.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("?", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (name.Equals("version", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("ver", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (name.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("d", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (name.Equals("mono", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (name.Equals("experimental", StringComparison.OrdinalIgnoreCase))
            value = ParseBooleanValue(value);

        if (options.ContainsKey(name))
        {
            _log.Error("Multiple arguments with the same name ({0}).", name);
            return false;
        }

        options.Add(name, value);
        return true;
    }

    static string ParseBooleanValue(string value)
    {
        value = (value ?? string.Empty).UnQuote();
        if (string.IsNullOrWhiteSpace(value))
            return "true";

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return "true";

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return "false";

        throw new InvalidOperationException("Argument value is not a valid boolean value.");
    }
}