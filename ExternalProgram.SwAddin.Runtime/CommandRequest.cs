using System.Collections.Generic;

namespace ExternalProgram.SwAddin;

public sealed class CommandRequest
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string Command { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public Dictionary<string, object> Args { get; set; }
}
