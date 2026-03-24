using System;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Cpp2IL.Core.ISILToCil.IL2CPP.RuntimeModel;
using Cpp2IL.Core.ISILToCil.IL2CPP.Semantics;
using Cpp2IL.Core.ISILToCil.RuntimeLinking;

namespace Cpp2IL.Core.Tests;

public class Il2CppRuntimeShimTests
{
    [Test]
    public void RuntimeAssemblyEmitsPhase1Il2CppShimSurface()
    {
        var corlib = new AssemblyDefinition("mscorlib", new Version(4, 0, 0, 0));
        var runtimeAssembly = new IntrinsicsReferenceStrategy().EmitRuntimeAssembly(corlib);
        var module = runtimeAssembly.ManifestModule!;

        Assert.Multiple(() =>
        {
            var methodHandleType = module.TopLevelTypes.Single(t => t.Namespace == "Cpp2IL.Runtime" && t.Name == "Il2CppMethodHandle");
            Assert.That(methodHandleType.Methods.Count, Is.EqualTo(0));

            var invokerType = module.TopLevelTypes.Single(t => t.Namespace == "Cpp2IL.Runtime" && t.Name == "Il2CppInvoker");
            var invokeVoid = invokerType.Methods.Single(method => method.Name == "InvokeVoid");
            Assert.That(invokeVoid.Signature.ReturnType.FullName, Is.EqualTo("System.Void"));
            Assert.That(invokeVoid.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Object[]" }));

            var invokeGeneric = invokerType.Methods.Single(method => method.Name == "Invoke" && method.GenericParameters.Count == 1);
            Assert.That(invokeGeneric.Signature.ReturnType is GenericParameterSignature, Is.True);
            Assert.That(invokeGeneric.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Object[]" }));
            Assert.That(invokeGeneric.GenericParameters.Count, Is.EqualTo(1));

            var delegateType = module.TopLevelTypes.Single(t => t.Namespace == "Cpp2IL.Runtime" && t.Name == "Il2CppDelegate");
            var construct = delegateType.Methods.Single(method => method.Name == "Construct");
            Assert.That(construct.Signature.ReturnType.FullName, Is.EqualTo("System.Object"));
            Assert.That(construct.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Object" }));

            var metadataType = module.TopLevelTypes.Single(t => t.Namespace == "Cpp2IL.Runtime" && t.Name == "Il2CppMetadata");
            var initialize = metadataType.Methods.Single(method => method.Name == "Initialize");
            Assert.That(initialize.Signature.ReturnType.FullName, Is.EqualTo("System.Void"));
            Assert.That(initialize.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr" }));

            var rgctxType = module.TopLevelTypes.Single(t => t.Namespace == "Cpp2IL.Runtime" && t.Name == "Il2CppRGCTXData");
            Assert.That(rgctxType.Methods.Select(method => method.Name), Is.EqualTo(new[]
            {
                "GetData",
                "GetDataNoInit",
                "GetType",
                "GetMethod",
                "GetField",
                "IsInitialized",
                "InitializeMethod",
            }));

            var getData = rgctxType.Methods.Single(method => method.Name == "GetData");
            Assert.That(getData.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(getData.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            var getDataNoInit = rgctxType.Methods.Single(method => method.Name == "GetDataNoInit");
            Assert.That(getDataNoInit.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(getDataNoInit.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            var getType = rgctxType.Methods.Single(method => method.Name == "GetType");
            Assert.That(getType.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(getType.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            var getMethod = rgctxType.Methods.Single(method => method.Name == "GetMethod");
            Assert.That(getMethod.Signature.ReturnType.FullName, Is.EqualTo("Cpp2IL.Runtime.Il2CppMethodHandle"));
            Assert.That(getMethod.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            var getField = rgctxType.Methods.Single(method => method.Name == "GetField");
            Assert.That(getField.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(getField.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            var isInitialized = rgctxType.Methods.Single(method => method.Name == "IsInitialized");
            Assert.That(isInitialized.Signature.ReturnType.FullName, Is.EqualTo("System.Boolean"));
            Assert.That(isInitialized.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "Cpp2IL.Runtime.Il2CppMethodHandle" }));

            var initializeMethod = rgctxType.Methods.Single(method => method.Name == "InitializeMethod");
            Assert.That(initializeMethod.Signature.ReturnType.FullName, Is.EqualTo("System.Void"));
            Assert.That(initializeMethod.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "Cpp2IL.Runtime.Il2CppMethodHandle" }));
        });
    }

    [Test]
    public void RuntimeDependencyBinderMapsSemanticNeedsToImportedReferences()
    {
        var module = new ModuleDefinition("ShimHarness.dll", new AssemblyReference("mscorlib", new Version(4, 0, 0, 0)));
        var strategy = new IntrinsicsReferenceStrategy();
        var binder = new Il2CppRuntimeDependencyBinder(module, strategy);
        var importer = new IntrinsicsImporter(module, strategy);

        var methodHandle = importer.GetIl2CppMethodHandle();
        var rgctx = importer.GetIl2CppRGCTXData();
        var resolveData = binder.Bind(new Il2CppSemanticPlanNode.PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind.Data, "rgctx.data"));
        var resolveDataNoInit = binder.Bind(new Il2CppSemanticPlanNode.PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind.DataNoInit, "rgctx.data.no_init"));
        var resolveType = binder.Bind(new Il2CppSemanticPlanNode.PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind.Type, "rgctx.type"));
        var resolveMethod = binder.Bind(new Il2CppSemanticPlanNode.PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind.Method, "rgctx.method"));
        var resolveField = binder.Bind(new Il2CppSemanticPlanNode.PlanResolveRGCTXData(Il2CppRGCTXDataAccessKind.Field, "rgctx.field"));
        var rgctxIsInitialized = importer.GetIl2CppRGCTXDataIsInitialized();
        var rgctxInitializeMethod = importer.GetIl2CppRGCTXDataInitializeMethod();
        var initialize = binder.Bind(new Il2CppSemanticPlanNode.PlanInitializeMetadata("metadata"));
        var construct = binder.Bind(new Il2CppSemanticPlanNode.PlanConstructDelegate("delegate"));
        var invokeVoid = binder.Bind(new Il2CppSemanticPlanNode.PlanCallInvoker("invoke"), module.CorLibTypeFactory.Void);
        var invokeInt32 = binder.Bind(new Il2CppSemanticPlanNode.PlanCallInvoker("invoke"), module.CorLibTypeFactory.Int32);
        Assert.Throws<InvalidOperationException>(() => binder.Bind(new Il2CppSemanticPlanNode.PlanCallInvoker("invoke")));

        Assert.Multiple(() =>
        {
            Assert.That(module.AssemblyReferences.Any(reference => reference.Name == "Cpp2IL.Runtime"), Is.True);
            Assert.That(methodHandle.FullName, Is.EqualTo("Cpp2IL.Runtime.Il2CppMethodHandle"));
            Assert.That(rgctx.FullName, Is.EqualTo("Cpp2IL.Runtime.Il2CppRGCTXData"));

            Assert.That(initialize.Name, Is.EqualTo("Initialize"));
            Assert.That(initialize.Signature.ReturnType.FullName, Is.EqualTo("System.Void"));
            Assert.That(initialize.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr" }));

            Assert.That(resolveData.Name, Is.EqualTo("GetData"));
            Assert.That(resolveData.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(resolveData.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            Assert.That(resolveDataNoInit.Name, Is.EqualTo("GetDataNoInit"));
            Assert.That(resolveDataNoInit.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(resolveDataNoInit.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            Assert.That(resolveType.Name, Is.EqualTo("GetType"));
            Assert.That(resolveType.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(resolveType.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            Assert.That(resolveMethod.Name, Is.EqualTo("GetMethod"));
            Assert.That(resolveMethod.Signature.ReturnType.FullName, Is.EqualTo("Cpp2IL.Runtime.Il2CppMethodHandle"));
            Assert.That(resolveMethod.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            Assert.That(resolveField.Name, Is.EqualTo("GetField"));
            Assert.That(resolveField.Signature.ReturnType.FullName, Is.EqualTo("System.IntPtr"));
            Assert.That(resolveField.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Int32" }));

            Assert.That(rgctxIsInitialized.Name, Is.EqualTo("IsInitialized"));
            Assert.That(rgctxIsInitialized.Signature.ReturnType.FullName, Is.EqualTo("System.Boolean"));
            Assert.That(rgctxIsInitialized.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "Cpp2IL.Runtime.Il2CppMethodHandle" }));

            Assert.That(rgctxInitializeMethod.Name, Is.EqualTo("InitializeMethod"));
            Assert.That(rgctxInitializeMethod.Signature.ReturnType.FullName, Is.EqualTo("System.Void"));
            Assert.That(rgctxInitializeMethod.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "Cpp2IL.Runtime.Il2CppMethodHandle" }));

            Assert.That(construct.Name, Is.EqualTo("Construct"));
            Assert.That(construct.Signature.ReturnType.FullName, Is.EqualTo("System.Object"));
            Assert.That(construct.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Object" }));

            Assert.That(invokeVoid.Name, Is.EqualTo("InvokeVoid"));
            Assert.That(invokeVoid.Signature.ReturnType.FullName, Is.EqualTo("System.Void"));
            Assert.That(invokeVoid.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Object[]" }));

            Assert.That(invokeInt32.Name, Is.EqualTo("Invoke"));
            Assert.That(invokeInt32, Is.InstanceOf<MethodSpecification>());
            Assert.That(((MethodSpecification)invokeInt32).Signature.TypeArguments.Count, Is.EqualTo(1));
            Assert.That(((MethodSpecification)invokeInt32).Signature.TypeArguments.Single().FullName, Is.EqualTo("System.Int32"));
            Assert.That(invokeInt32.Signature.ReturnType.FullName, Is.EqualTo("!!0"));
            Assert.That(invokeInt32.Signature.ParameterTypes.Select(type => type.FullName), Is.EqualTo(new[] { "System.IntPtr", "System.Object[]" }));
        });
    }
}
