using System.Collections.Generic;

namespace 外部程序.SwAddin;

public sealed class CommandRequest
{
    public string Command { get; set; }
    public Dictionary<string, object> Args { get; set; }
}
