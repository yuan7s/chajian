using System;
using System.Collections.Generic;

namespace ExternalProgram.SwAddin;

internal sealed class CommandFailureException : InvalidOperationException
{
    public CommandFailureException(string code, Dictionary<string, object> details = null)
        : base(code)
    {
        Code = code;
        Details = details ?? new Dictionary<string, object>();
    }

    public string Code { get; }

    public Dictionary<string, object> Details { get; }

    public static CommandFailureException Create(string code, params object[] details)
    {
        return new CommandFailureException(code, BuildDetails(details));
    }

    private static Dictionary<string, object> BuildDetails(object[] details)
    {
        var result = new Dictionary<string, object>();
        if (details == null) return result;

        for (var i = 0; i + 1 < details.Length; i += 2)
        {
            var key = details[i]?.ToString();
            if (string.IsNullOrWhiteSpace(key)) continue;
            result[key] = details[i + 1];
        }

        return result;
    }
}
