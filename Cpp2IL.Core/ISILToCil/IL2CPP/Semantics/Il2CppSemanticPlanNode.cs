using System;

namespace Cpp2IL.Core.ISILToCil.IL2CPP.Semantics;

internal abstract class Il2CppSemanticPlanNode
{
    protected Il2CppSemanticPlanNode()
    {
    }

    // Guardrail: each node captures an IL2CPP semantic operation, not a generic
    // one-to-one wrapper around a single CIL instruction.
    internal sealed class PlanInitializeMetadata : Il2CppSemanticPlanNode
    {
        public PlanInitializeMetadata(string metadataToken)
        {
            MetadataToken = metadataToken ?? throw new ArgumentNullException(nameof(metadataToken));
        }

        public string MetadataToken { get; }
    }

    internal sealed class PlanResolveRGCTXData : Il2CppSemanticPlanNode
    {
        public PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind accessKind, string rgctxToken)
        {
            AccessKind = accessKind;
            RgctxToken = rgctxToken ?? throw new ArgumentNullException(nameof(rgctxToken));
        }

        public Il2CppRGCTXDataAccessKind AccessKind { get; }
        public string RgctxToken { get; }
    }

    internal sealed class PlanConstructDelegate : Il2CppSemanticPlanNode
    {
        public PlanConstructDelegate(string delegateToken)
        {
            DelegateToken = delegateToken ?? throw new ArgumentNullException(nameof(delegateToken));
        }

        public string DelegateToken { get; }
    }

    internal sealed class PlanCallInvoker : Il2CppSemanticPlanNode
    {
        public PlanCallInvoker(string invokerToken)
        {
            InvokerToken = invokerToken ?? throw new ArgumentNullException(nameof(invokerToken));
        }

        public string InvokerToken { get; }
    }

    internal sealed class PlanReturn : Il2CppSemanticPlanNode
    {
        public PlanReturn()
        {
        }
    }
}
