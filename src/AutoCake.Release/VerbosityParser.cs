using System;
using System.Collections.Generic;
using System.Globalization;
using Cake.Core.Diagnostics;

internal class VerbosityParser
{
    readonly ICakeLog _log;

    readonly Dictionary<string, Verbosity> _lookup;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VerbosityParser" /> class.
    /// </summary>
    /// <param name="log">The log.</param>
    public VerbosityParser(ICakeLog log)
    {
        _log = log;
        _lookup = new Dictionary<string, Verbosity>(StringComparer.OrdinalIgnoreCase)
        {
            {"q", Verbosity.Quiet},
            {"quiet", Verbosity.Quiet},
            {"m", Verbosity.Minimal},
            {"minimal", Verbosity.Minimal},
            {"n", Verbosity.Normal},
            {"normal", Verbosity.Normal},
            {"v", Verbosity.Verbose},
            {"verbose", Verbosity.Verbose},
            {"d", Verbosity.Diagnostic},
            {"diagnostic", Verbosity.Diagnostic}
        };
    }

    /// <summary>
    ///     Parses the provided string to a <see cref="Verbosity" />.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="verbosity">The verbosity.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    public bool TryParse(string value, out Verbosity verbosity)
    {
        var result = _lookup.TryGetValue(value, out verbosity);
        if (!result)
        {
            const string format = "The value '{0}' is not a valid verbosity.";
            var message = string.Format(CultureInfo.InvariantCulture, format, value);
            _log.Error(message);
        }

        return result;
    }
}