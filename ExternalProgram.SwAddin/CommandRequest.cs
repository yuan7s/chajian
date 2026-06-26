using System.Collections.Generic;

namespace ExternalProgram.SwAddin;

public sealed class CommandRequest
{
    public string Command { get; set; }
    public Dictionary<string, object> Args { get; set; }
}
