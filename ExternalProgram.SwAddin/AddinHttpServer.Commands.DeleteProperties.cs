using System;
using System.Collections.Generic;
using SldWorks;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object DeleteCustomProperties()
    {
        var model = GetActiveModel();
        var stats = DeletePropertiesRecursive(model, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new { done = true, documents = stats.Documents, deleted = stats.Deleted };
    }

    private object DeleteConfigurationProperties()
    {
        var model = GetActiveModel();
        var stats = DeletePropertiesRecursive(model, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new { done = true, documents = stats.Documents, deleted = stats.Deleted };
    }
}
