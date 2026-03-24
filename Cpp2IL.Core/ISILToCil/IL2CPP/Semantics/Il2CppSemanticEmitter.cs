using System;
using System.Collections.Generic;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.ISILToCil.IL2CPP.Patterns;
using Cpp2IL.Core.ISILToCil.IL2CPP.RuntimeModel;
using Cpp2IL.Core.ISILToCil.Materialization;
using Cpp2IL.Core.ISILToCil.Resolution;
using Cpp2IL.Core.ISILToCil.RuntimeLinking;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils.AsmResolver;

namespace Cpp2IL.Core.ISILToCil.IL2CPP.Semantics;

internal sealed class Il2CppSemanticEmitter
{
    private readonly ModuleDefinition _module;
    private readonly CilInstructionCollection _cil;
    private readonly Il2CppRuntimeDependencyBinder _runtimeDependencyBinder;
    private readonly MethodDefinition? _method;
    private readonly MethodAnalysisContext? _context;
    private readonly OperandMaterializer? _materializer;

    public Il2CppSemanticEmitter(
        ModuleDefinition module,
        CilInstructionCollection cil,
        IntrinsicsReferenceStrategy? strategy = null)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _cil = cil ?? throw new ArgumentNullException(nameof(cil));
        _runtimeDependencyBinder = new Il2CppRuntimeDependencyBinder(module, strategy);
    }

    public Il2CppSemanticEmitter(
        MethodDefinition method,
        MethodAnalysisContext context,
        CilInstructionCollection cil,
        OperandMaterializer? materializer = null,
        IntrinsicsReferenceStrategy? strategy = null)
        : this(method?.DeclaringModule ?? throw new ArgumentNullException(nameof(method)), cil, strategy)
    {
        _method = method;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        var resolver = new MethodResolverServices(context, _module);
        _materializer = materializer ?? new OperandMaterializer(method, context, cil, resolver);
    }

    public Il2CppSemanticPlan CreatePlan(Il2CppPatternMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        return match.Kind switch
        {
            Il2CppPatternKind.MetadataInitialization when match.IsMediumConfidenceOrBetter && !string.IsNullOrEmpty(match.MetadataToken) =>
                new Il2CppSemanticPlan(
                [
                    new Il2CppSemanticPlanNode.PlanInitializeMetadata(match.MetadataToken),
                ]),
            Il2CppPatternKind.RgctxAccess when match.IsMediumConfidenceOrBetter &&
                                               match.RgctxAccessKind is Il2CppRGCTXDataAccessKind accessKind &&
                                               !string.IsNullOrEmpty(match.RgctxToken) =>
                new Il2CppSemanticPlan(
                [
                    new Il2CppSemanticPlanNode.PlanResolveRGCTXData(accessKind, match.RgctxToken),
                ]),
            Il2CppPatternKind.InvokerCall when match.IsMediumConfidenceOrBetter && !string.IsNullOrEmpty(match.InvokerToken) =>
                new Il2CppSemanticPlan(
                [
                    new Il2CppSemanticPlanNode.PlanCallInvoker(match.InvokerToken),
                ]),
            _ => throw new NotSupportedException($"Only medium/high-confidence IL2CPP matches with a semantic-plan mapping can be converted. Unsupported kind: {match.Kind}.")
        };
    }

    public bool TryEmit(Il2CppPatternMatch match)
    {
        var plan = CreatePlan(match);
        foreach (var node in plan.Nodes)
        {
            switch (node)
            {
                case Il2CppSemanticPlanNode.PlanInitializeMetadata when match.MetadataPointer is ulong metadataPointer:
                    EmitLoadNativeInt(metadataPointer);
                    _cil.Add(CilOpCodes.Call, _runtimeDependencyBinder.Bind(node));
                    break;
                case Il2CppSemanticPlanNode.PlanCallInvoker invokerNode:
                    if (!TryEmitInvokerCall(invokerNode, match))
                        return false;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private bool TryEmitInvokerCall(Il2CppSemanticPlanNode.PlanCallInvoker node, Il2CppPatternMatch match)
    {
        if (_context == null || _method == null || _materializer == null || match.InvokerMethodInfoPointer is not ulong methodInfoPointer)
            return false;

        var returnType = _context.IsVoid
            ? _module.CorLibTypeFactory.Void
            : _context.ReturnType?.ToTypeSignature(_module) ?? _module.CorLibTypeFactory.Object;

        EmitLoadNativeInt(methodInfoPointer);
        if (!EmitObjectArray(match.InvokerArguments ?? Array.Empty<InstructionSetIndependentOperand>()))
            return false;

        _cil.Add(CilOpCodes.Call, _runtimeDependencyBinder.Bind(node, returnType));
        if (_context.IsVoid)
            return true;

        return _materializer.TryCaptureCallResult(returnType);
    }

    private bool EmitObjectArray(IReadOnlyList<InstructionSetIndependentOperand> operands)
    {
        _cil.Add(CilOpCodes.Ldc_I4, operands.Count);
        _cil.Add(CilOpCodes.Newarr, _module.CorLibTypeFactory.Object.ToTypeDefOrRef());

        for (var i = 0; i < operands.Count; i++)
        {
            _cil.Add(CilOpCodes.Dup);
            _cil.Add(CilOpCodes.Ldc_I4, i);
            if (_materializer == null || !_materializer.TryLoad(operands[i]))
                return false;

            EmitBoxIfNeeded(_materializer.TryInferType(operands[i]));
            _cil.Add(CilOpCodes.Stelem_Ref);
        }

        return true;
    }

    private void EmitBoxIfNeeded(TypeSignature? typeSignature)
    {
        if (typeSignature?.IsValueType == true)
            _cil.Add(CilOpCodes.Box, typeSignature.ToTypeDefOrRef());
    }

    private void EmitLoadNativeInt(ulong value)
    {
        if (value <= int.MaxValue)
        {
            _cil.Add(CilOpCodes.Ldc_I4, (int)value);
        }
        else
        {
            _cil.Add(CilOpCodes.Ldc_I8, unchecked((long)value));
        }

        _cil.Add(CilOpCodes.Conv_I);
    }
}
