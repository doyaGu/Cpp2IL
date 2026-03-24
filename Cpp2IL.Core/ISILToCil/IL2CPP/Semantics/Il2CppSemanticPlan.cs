using System;
using System.Collections.Generic;
using System.Linq;

namespace Cpp2IL.Core.ISILToCil.IL2CPP.Semantics;

internal sealed class Il2CppSemanticPlan
{
    public Il2CppSemanticPlan(IEnumerable<Il2CppSemanticPlanNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        // Guardrail: the plan is a semantic boundary for IL2CPP-specific operations,
        // not a thin wrapper over generic CIL or ISIL instructions.
        Nodes = nodes.ToArray();
    }

    public IReadOnlyList<Il2CppSemanticPlanNode> Nodes { get; }
}
