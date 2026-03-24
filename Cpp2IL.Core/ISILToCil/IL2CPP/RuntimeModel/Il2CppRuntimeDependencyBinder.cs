using System;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Cpp2IL.Core.ISILToCil.IL2CPP.Semantics;
using Cpp2IL.Core.ISILToCil.RuntimeLinking;

namespace Cpp2IL.Core.ISILToCil.IL2CPP.RuntimeModel;

internal sealed class Il2CppRuntimeDependencyBinder
{
    private readonly IntrinsicsImporter _importer;

    public Il2CppRuntimeDependencyBinder(ModuleDefinition module, IntrinsicsReferenceStrategy? strategy = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        _importer = new IntrinsicsImporter(module, strategy ?? new IntrinsicsReferenceStrategy());
    }

    public TypeReference GetMethodHandle() => _importer.GetIl2CppMethodHandle();

    public IMethodDescriptor Bind(Il2CppSemanticPlanNode node, TypeSignature? invokeReturnType = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            Il2CppSemanticPlanNode.PlanInitializeMetadata => _importer.GetIl2CppMetadataInitialize(),
            Il2CppSemanticPlanNode.PlanResolveRGCTXData rgctx => rgctx.AccessKind switch
            {
                Il2CppRGCTXDataAccessKind.Data => _importer.GetIl2CppRGCTXDataData(),
                Il2CppRGCTXDataAccessKind.DataNoInit => _importer.GetIl2CppRGCTXDataDataNoInit(),
                Il2CppRGCTXDataAccessKind.Type => _importer.GetIl2CppRGCTXDataType(),
                Il2CppRGCTXDataAccessKind.Method => _importer.GetIl2CppRGCTXDataMethod(),
                Il2CppRGCTXDataAccessKind.Field => _importer.GetIl2CppRGCTXDataField(),
                _ => throw new NotSupportedException($"No runtime helper binding exists for RGCTX access kind {rgctx.AccessKind}.")
            },
            Il2CppSemanticPlanNode.PlanConstructDelegate => _importer.GetIl2CppDelegateConstruct(),
            Il2CppSemanticPlanNode.PlanCallInvoker when IsVoidType(invokeReturnType) => _importer.GetIl2CppInvokerInvokeVoid(),
            Il2CppSemanticPlanNode.PlanCallInvoker when invokeReturnType != null => _importer.GetIl2CppInvokerInvoke(invokeReturnType),
            Il2CppSemanticPlanNode.PlanCallInvoker => throw new InvalidOperationException("PlanCallInvoker requires a propagated return type so the binder can choose between InvokeVoid and Invoke<T>."),
            _ => throw new NotSupportedException($"No runtime helper binding exists for {node.GetType().Name}.")
        };
    }

    private static bool IsVoidType(TypeSignature? typeSignature)
    {
        return typeSignature?.FullName == "System.Void";
    }
}
