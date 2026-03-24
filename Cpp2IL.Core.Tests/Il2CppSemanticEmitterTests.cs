using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.ISILToCil.IL2CPP.Patterns;
using Cpp2IL.Core.ISILToCil.IL2CPP.Semantics;
using Cpp2IL.Core.OutputFormats;

namespace Cpp2IL.Core.Tests;

public class Il2CppSemanticEmitterTests
{
    [Test]
    public void SemanticPlanUsesDedicatedIl2CppOperationNodes()
    {
        // Guardrail: semantic-plan nodes model IL2CPP operations, not generic one-to-one CIL wrappers.
        var sourceNodes = new List<Il2CppSemanticPlanNode>
        {
            new Il2CppSemanticPlanNode.PlanInitializeMetadata("RGCTXData"),
            new Il2CppSemanticPlanNode.PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind.Method, "rgctx.method[3]"),
            new Il2CppSemanticPlanNode.PlanConstructDelegate("delegate.ctor"),
            new Il2CppSemanticPlanNode.PlanCallInvoker("invoker.call"),
            new Il2CppSemanticPlanNode.PlanReturn(),
        };

        var plan = new Il2CppSemanticPlan(sourceNodes);
        sourceNodes.Add(new Il2CppSemanticPlanNode.PlanReturn());

        Assert.Multiple(() =>
        {
            Assert.That(plan.Nodes, Has.Count.EqualTo(5));
            Assert.That(plan.Nodes[0], Is.TypeOf<Il2CppSemanticPlanNode.PlanInitializeMetadata>());
            Assert.That(((Il2CppSemanticPlanNode.PlanInitializeMetadata)plan.Nodes[0]).MetadataToken, Is.EqualTo("RGCTXData"));
            Assert.That(plan.Nodes[1], Is.TypeOf<Il2CppSemanticPlanNode.PlanResolveRGCTXData>());
            Assert.That(((Il2CppSemanticPlanNode.PlanResolveRGCTXData)plan.Nodes[1]).AccessKind, Is.EqualTo(Il2CppRGCTXDataAccessKind.Method));
            Assert.That(((Il2CppSemanticPlanNode.PlanResolveRGCTXData)plan.Nodes[1]).RgctxToken, Is.EqualTo("rgctx.method[3]"));
            Assert.That(plan.Nodes[2], Is.TypeOf<Il2CppSemanticPlanNode.PlanConstructDelegate>());
            Assert.That(((Il2CppSemanticPlanNode.PlanConstructDelegate)plan.Nodes[2]).DelegateToken, Is.EqualTo("delegate.ctor"));
            Assert.That(plan.Nodes[3], Is.TypeOf<Il2CppSemanticPlanNode.PlanCallInvoker>());
            Assert.That(((Il2CppSemanticPlanNode.PlanCallInvoker)plan.Nodes[3]).InvokerToken, Is.EqualTo("invoker.call"));
            Assert.That(plan.Nodes[4], Is.TypeOf<Il2CppSemanticPlanNode.PlanReturn>());
        });
    }

    [Test]
    public void SemanticEmitterBuildsRGCTXDataPlanForExplicitRgctxMatch()
    {
        var appContext = TestGameLoader.LoadSimple2019Game();
        var assembly = appContext.Assemblies.First();
        var type = assembly.InjectType("Cpp2ILInjected", "Il2CppRGCTXDataEmitterHarness", appContext.SystemTypes.SystemObjectType, TypeAttributes.Public | TypeAttributes.Class);
        var methodContext = type.InjectMethodContext("Harness", appContext.SystemTypes.SystemVoidType, MethodAttributes.Public | MethodAttributes.Static);

        _ = new AsmResolverDllOutputFormatEmpty().BuildAssemblies(appContext);

        var methodDefinition = methodContext.GetExtraData<MethodDefinition>("AsmResolverMethod")!;
        methodDefinition.CilMethodBody = new();
        var patternContext = new Il2CppPatternContext(
            methodContext.AppContext,
            methodContext,
            [],
            methodContext.DeclaringType!,
            methodContext.ReturnType,
            methodContext.Parameters,
            null,
            false);
        var match = new Il2CppPatternMatch(
            Il2CppPatternKind.RgctxAccess,
            Il2CppPatternProfile.Unity2019Like,
            patternContext,
            Il2CppPatternConfidence.Medium,
            RgctxAccessKind: Il2CppRGCTXDataAccessKind.Method,
            RgctxPointer: 0x4400,
            RgctxIndex: 3,
            RgctxToken: "rgctx.method[3]");
        var emitter = new Il2CppSemanticEmitter(methodDefinition.DeclaringModule!, methodDefinition.CilMethodBody.Instructions);

        var plan = emitter.CreatePlan(match);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Nodes.Select(node => node.GetType().Name), Is.EqualTo(new[] { "PlanResolveRGCTXData" }));
            Assert.That(((Il2CppSemanticPlanNode.PlanResolveRGCTXData)plan.Nodes[0]).AccessKind, Is.EqualTo(Il2CppRGCTXDataAccessKind.Method));
            Assert.That(((Il2CppSemanticPlanNode.PlanResolveRGCTXData)plan.Nodes[0]).RgctxToken, Is.EqualTo("rgctx.method[3]"));
        });
    }

    [Test]
    public void SemanticEmitterBuildsMetadataPlanAndBindsRuntimeInitializeHelper()
    {
        var appContext = TestGameLoader.LoadSimple2019Game();
        var assembly = appContext.Assemblies.First();
        var type = assembly.InjectType("Cpp2ILInjected", "Il2CppSemanticEmitterHarness", appContext.SystemTypes.SystemObjectType, TypeAttributes.Public | TypeAttributes.Class);
        var methodContext = type.InjectMethodContext("Harness", appContext.SystemTypes.SystemVoidType, MethodAttributes.Public | MethodAttributes.Static);

        _ = new AsmResolverDllOutputFormatEmpty().BuildAssemblies(appContext);

        var methodDefinition = methodContext.GetExtraData<MethodDefinition>("AsmResolverMethod")!;
        methodDefinition.CilMethodBody = new();

        var metadataLoad = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Move,
            0,
            IsilFlowControl.Continue,
            InstructionSetIndependentOperand.MakeRegister("rcx"),
            InstructionSetIndependentOperand.MakeImmediate(0x1234L));
        var metadataInit = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Call,
            1,
            IsilFlowControl.MethodCall,
            InstructionSetIndependentOperand.MakeImmediate("il2cpp_codegen_initialize_runtime_metadata"),
            InstructionSetIndependentOperand.MakeRegister("rcx"));
        var ret = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Return,
            2,
            IsilFlowControl.MethodReturn);

        var match = Il2CppPatternMatch.TryRecognize(new Il2CppPatternContext(
            methodContext.AppContext,
            methodContext,
            [metadataLoad, metadataInit, ret],
            methodContext.DeclaringType!,
            methodContext.ReturnType,
            methodContext.Parameters,
            null,
            false));
        var emitter = new Il2CppSemanticEmitter(methodDefinition.DeclaringModule!, methodDefinition.CilMethodBody.Instructions);

        var plan = emitter.CreatePlan(match);
        var emitted = emitter.TryEmit(match);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Nodes.Select(node => node.GetType().Name), Is.EqualTo(new[] { "PlanInitializeMetadata" }));
            Assert.That(((Il2CppSemanticPlanNode.PlanInitializeMetadata)plan.Nodes[0]).MetadataToken, Is.EqualTo("0x1234"));
            Assert.That(emitted, Is.True);
            Assert.That(methodDefinition.CilMethodBody.Instructions.Any(i => i.OpCode == CilOpCodes.Call && ((IMethodDescriptor)i.Operand!).Name == "Initialize"), Is.True);
            Assert.That(methodDefinition.CilMethodBody.Instructions.Any(i => i.OpCode == CilOpCodes.Ret), Is.False);
        });
    }

    [Test]
    public void SemanticEmitterBuildsInvokerPlanAndBindsRuntimeInvokerHelper()
    {
        var appContext = TestGameLoader.LoadSimple2019Game();
        var assembly = appContext.Assemblies.First();
        var type = assembly.InjectType("Cpp2ILInjected", "Il2CppInvokerEmitterHarness", appContext.SystemTypes.SystemObjectType, TypeAttributes.Public | TypeAttributes.Class);
        var methodContext = type.InjectMethodContext(
            "Harness",
            appContext.SystemTypes.SystemInt32Type,
            MethodAttributes.Public | MethodAttributes.Static,
            appContext.SystemTypes.SystemObjectType);

        _ = new AsmResolverDllOutputFormatEmpty().BuildAssemblies(appContext);

        var methodDefinition = methodContext.GetExtraData<MethodDefinition>("AsmResolverMethod")!;
        methodDefinition.CilMethodBody = new();

        var loadInvokerThunk = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Move,
            0,
            IsilFlowControl.Continue,
            InstructionSetIndependentOperand.MakeRegister("r11"),
            InstructionSetIndependentOperand.MakeImmediate(0x401100L));
        var loadMethodPointer = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Move,
            1,
            IsilFlowControl.Continue,
            InstructionSetIndependentOperand.MakeRegister("rdx"),
            InstructionSetIndependentOperand.MakeImmediate(0x402000L));
        var loadMethodInfo = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Move,
            2,
            IsilFlowControl.Continue,
            InstructionSetIndependentOperand.MakeRegister("r8"),
            InstructionSetIndependentOperand.MakeImmediate(0x403000L));
        var invokerCall = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Call,
            3,
            IsilFlowControl.MethodCall,
            InstructionSetIndependentOperand.MakeRegister("r11"),
            InstructionSetIndependentOperand.MakeRegister("rdx"),
            InstructionSetIndependentOperand.MakeRegister("r8"),
            InstructionSetIndependentOperand.MakeImmediate(0L),
            InstructionSetIndependentOperand.MakeRegister("rcx"));
        var ret = new InstructionSetIndependentInstruction(
            InstructionSetIndependentOpCode.Return,
            4,
            IsilFlowControl.MethodReturn,
            InstructionSetIndependentOperand.MakeRegister("rax"));

        var match = Il2CppPatternMatch.TryRecognize(new Il2CppPatternContext(
            methodContext.AppContext,
            methodContext,
            [loadInvokerThunk, loadMethodPointer, loadMethodInfo, invokerCall, ret],
            methodContext.DeclaringType!,
            methodContext.ReturnType,
            methodContext.Parameters,
            null,
            false));
        var emitter = new Il2CppSemanticEmitter(methodDefinition, methodContext, methodDefinition.CilMethodBody.Instructions);

        var plan = emitter.CreatePlan(match);
        var emitted = emitter.TryEmit(match);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Nodes.Select(node => node.GetType().Name), Is.EqualTo(new[] { "PlanCallInvoker" }));
            Assert.That(((Il2CppSemanticPlanNode.PlanCallInvoker)plan.Nodes[0]).InvokerToken, Is.EqualTo("0x403000"));
            Assert.That(emitted, Is.True);
            Assert.That(methodDefinition.CilMethodBody.Instructions.Any(i => i.OpCode == CilOpCodes.Call && ((IMethodDescriptor)i.Operand!).Name == "Invoke"), Is.True);
            Assert.That(methodDefinition.CilMethodBody.Instructions.Any(i => i.OpCode == CilOpCodes.Newarr), Is.True);
        });
    }
}
