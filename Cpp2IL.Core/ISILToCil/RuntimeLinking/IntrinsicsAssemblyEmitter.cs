using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.DotNet.Cil;

namespace Cpp2IL.Core.ISILToCil.RuntimeLinking;

internal sealed class IntrinsicsAssemblyEmitter
{
    private readonly IntrinsicsReferenceStrategy _strategy;

    public IntrinsicsAssemblyEmitter(IntrinsicsReferenceStrategy strategy)
    {
        _strategy = strategy;
    }

    public AssemblyDefinition Emit(AssemblyDefinition corlib)
    {
        var assembly = new AssemblyDefinition(_strategy.RuntimeAssemblyName, _strategy.RuntimeAssemblyVersion);
        var module = new ModuleDefinition(_strategy.RuntimeAssemblyName + ".dll", new AssemblyReference(corlib.Name!, corlib.Version!));
        assembly.Modules.Add(module);

        var intrinsicsType = new TypeDefinition(
            _strategy.RuntimeNamespace,
            _strategy.IntrinsicsTypeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit);
        module.TopLevelTypes.Add(intrinsicsType);

        AddMethods(module, intrinsicsType);
        var methodHandleType = AddIl2CppMethodHandle(module, _strategy.RuntimeNamespace);
        AddIl2CppInvoker(module, _strategy.RuntimeNamespace);
        AddIl2CppDelegate(module, _strategy.RuntimeNamespace);
        AddIl2CppMetadata(module, _strategy.RuntimeNamespace);
        AddIl2CppRGCTXData(module, _strategy.RuntimeNamespace, methodHandleType);
        return assembly;
    }

    private static void AddMethods(ModuleDefinition module, TypeDefinition intrinsicsType)
    {
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "CallNative",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object.MakeSzArrayType()])));
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "CallIndirect",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object.MakeSzArrayType()])));
        intrinsicsType.Methods.Add(CreateGenericDefaultMethod(
            module,
            "LdMem",
            returnTypeFactory: gp => gp,
            parameterTypesFactory: gp => [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int64]));
        intrinsicsType.Methods.Add(CreateDefaultValueMethod(
            module,
            "LdMemRef",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int64])));
        intrinsicsType.Methods.Add(CreateGenericVoidMethod(
            module,
            "StMem",
            parameterTypesFactory: gp => [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int64, gp]));
        intrinsicsType.Methods.Add(CreateRetMethod(
            module,
            "StMemRef",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Void, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int64, module.CorLibTypeFactory.Object])));
        intrinsicsType.Methods.Add(CreateGenericDefaultMethod(
            module,
            "LoadStackSlot",
            returnTypeFactory: gp => gp,
            parameterTypesFactory: gp => [module.CorLibTypeFactory.Int32]));
        intrinsicsType.Methods.Add(CreateGenericVoidMethod(
            module,
            "StoreStackSlot",
            parameterTypesFactory: gp => [module.CorLibTypeFactory.Int32, gp]));
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "NewObj",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object.MakeSzArrayType()])));
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "Box",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object])));
        intrinsicsType.Methods.Add(CreateGenericDefaultMethod(
            module,
            "UnboxAny",
            returnTypeFactory: gp => gp,
            parameterTypesFactory: gp => [module.CorLibTypeFactory.Object]));
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "UnboxAny",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object])));
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "LdStr",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.String, 0, [module.CorLibTypeFactory.IntPtr])));
        intrinsicsType.Methods.Add(CreateThrowingMethod(
            module,
            "LdStr",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.String, 0, [module.CorLibTypeFactory.Int32])));
        intrinsicsType.Methods.Add(CreateExceptionThrowMethod(
            module,
            "ThrowNotImplemented",
            "System",
            "NotImplementedException"));
        intrinsicsType.Methods.Add(CreateExceptionThrowMethod(
            module,
            "ThrowInvalid",
            "System",
            "InvalidOperationException"));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_Add", CilOpCodes.Add));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_Sub", CilOpCodes.Sub));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_Mul", CilOpCodes.Mul));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_Div", CilOpCodes.Div));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_And", CilOpCodes.And));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_Or", CilOpCodes.Or));
        intrinsicsType.Methods.Add(CreateLongBinaryOperator(module, "Op_Xor", CilOpCodes.Xor));
        intrinsicsType.Methods.Add(CreateShiftOperator(module, "Op_Shl", CilOpCodes.Shl));
        intrinsicsType.Methods.Add(CreateShiftOperator(module, "Op_Shr", CilOpCodes.Shr));
        intrinsicsType.Methods.Add(CreateLongUnaryOperator(module, "Op_Neg", CilOpCodes.Neg));
        intrinsicsType.Methods.Add(CreateLongUnaryOperator(module, "Op_Not", CilOpCodes.Not));
    }

    private static TypeDefinition AddIl2CppMethodHandle(ModuleDefinition module, string ns)
    {
        var methodHandleType = CreateEmptyType(module, ns, "Il2CppMethodHandle");
        module.TopLevelTypes.Add(methodHandleType);
        return methodHandleType;
    }

    private static void AddIl2CppInvoker(ModuleDefinition module, string ns)
    {
        var invokerType = CreateStaticHelperType(module, ns, "Il2CppInvoker");
        invokerType.Methods.Add(CreateRetMethod(
            module,
            "InvokeVoid",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Void, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object.MakeSzArrayType()])));
        invokerType.Methods.Add(CreateGenericDefaultMethod(
            module,
            "Invoke",
            returnTypeFactory: gp => gp,
            parameterTypesFactory: gp => [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object.MakeSzArrayType()]));
        module.TopLevelTypes.Add(invokerType);
    }

    private static void AddIl2CppDelegate(ModuleDefinition module, string ns)
    {
        var delegateType = CreateStaticHelperType(module, ns, "Il2CppDelegate");
        delegateType.Methods.Add(CreateDefaultValueMethod(
            module,
            "Construct",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Object, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Object])));
        module.TopLevelTypes.Add(delegateType);
    }

    private static void AddIl2CppMetadata(ModuleDefinition module, string ns)
    {
        var metadataType = CreateStaticHelperType(module, ns, "Il2CppMetadata");
        metadataType.Methods.Add(CreateRetMethod(
            module,
            "Initialize",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Void, 0, [module.CorLibTypeFactory.IntPtr])));
        module.TopLevelTypes.Add(metadataType);
    }

    private static void AddIl2CppRGCTXData(ModuleDefinition module, string ns, TypeDefinition methodHandleType)
    {
        var rgctxType = CreateStaticHelperType(module, ns, "Il2CppRGCTXData");
        rgctxType.Methods.Add(CreateDefaultValueMethod(
            module,
            "GetData",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.IntPtr, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int32])));
        rgctxType.Methods.Add(CreateDefaultValueMethod(
            module,
            "GetDataNoInit",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.IntPtr, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int32])));
        rgctxType.Methods.Add(CreateDefaultValueMethod(
            module,
            "GetType",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.IntPtr, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int32])));
        rgctxType.Methods.Add(CreateDefaultValueMethod(
            module,
            "GetMethod",
            MethodSignature.CreateStatic(methodHandleType.ToTypeSignature(), 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int32])));
        rgctxType.Methods.Add(CreateDefaultValueMethod(
            module,
            "GetField",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.IntPtr, 0, [module.CorLibTypeFactory.IntPtr, module.CorLibTypeFactory.Int32])));
        rgctxType.Methods.Add(CreateDefaultValueMethod(
            module,
            "IsInitialized",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Boolean, 0, [methodHandleType.ToTypeSignature()])));
        rgctxType.Methods.Add(CreateRetMethod(
            module,
            "InitializeMethod",
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Void, 0, [methodHandleType.ToTypeSignature()])));
        module.TopLevelTypes.Add(rgctxType);
    }

    private static MethodDefinition CreateThrowingMethod(ModuleDefinition module, string name, MethodSignature signature)
    {
        var method = CreateMethodDefinition(name, signature);
        method.CilMethodBody = new();
        var exceptionType = module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "NotImplementedException");
        var ctor = exceptionType.CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void));
        method.CilMethodBody.Instructions.Add(CilOpCodes.Newobj, ctor);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Throw);
        return method;
    }

    private static MethodDefinition CreateDefaultValueMethod(ModuleDefinition module, string name, MethodSignature signature)
    {
        var method = CreateMethodDefinition(name, signature);
        method.CilMethodBody = new();
        EmitDefaultValue(method, module, signature.ReturnType);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateGenericDefaultMethod(
        ModuleDefinition module,
        string name,
        System.Func<GenericParameterSignature, TypeSignature> returnTypeFactory,
        System.Func<GenericParameterSignature, TypeSignature[]> parameterTypesFactory)
    {
        var genericParameter = new GenericParameterSignature(module, GenericParameterType.Method, 0);
        var signature = MethodSignature.CreateStatic(returnTypeFactory(genericParameter), 1, parameterTypesFactory(genericParameter));
        var method = CreateMethodDefinition(name, signature);
        method.GenericParameters.Add(new GenericParameter("T"));
        method.CilMethodBody = new();
        EmitDefaultValue(method, module, returnTypeFactory(genericParameter));
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateGenericVoidMethod(
        ModuleDefinition module,
        string name,
        System.Func<GenericParameterSignature, TypeSignature[]> parameterTypesFactory)
    {
        var genericParameter = new GenericParameterSignature(module, GenericParameterType.Method, 0);
        var signature = MethodSignature.CreateStatic(module.CorLibTypeFactory.Void, 1, parameterTypesFactory(genericParameter));
        var method = CreateMethodDefinition(name, signature);
        method.GenericParameters.Add(new GenericParameter("T"));
        method.CilMethodBody = new();
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateRetMethod(ModuleDefinition module, string name, MethodSignature signature)
    {
        var method = CreateMethodDefinition(name, signature);
        method.CilMethodBody = new();
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateExceptionThrowMethod(ModuleDefinition module, string name, string ns, string typeName)
    {
        var method = CreateMethodDefinition(name, MethodSignature.CreateStatic(module.CorLibTypeFactory.Void, 0, [module.CorLibTypeFactory.String]));
        method.CilMethodBody = new();
        var exceptionType = module.CorLibTypeFactory.CorLibScope.CreateTypeReference(ns, typeName);
        var ctor = exceptionType.CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void));
        method.CilMethodBody.Instructions.Add(CilOpCodes.Newobj, ctor);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Throw);
        return method;
    }

    private static MethodDefinition CreateLongBinaryOperator(ModuleDefinition module, string name, CilOpCode opCode)
    {
        var method = CreateMethodDefinition(name, MethodSignature.CreateStatic(module.CorLibTypeFactory.Int64, 0, [module.CorLibTypeFactory.Int64, module.CorLibTypeFactory.Int64]));
        method.CilMethodBody = new();
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_0);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_1);
        method.CilMethodBody.Instructions.Add(opCode);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateShiftOperator(ModuleDefinition module, string name, CilOpCode opCode)
    {
        var method = CreateMethodDefinition(name, MethodSignature.CreateStatic(module.CorLibTypeFactory.Int64, 0, [module.CorLibTypeFactory.Int64, module.CorLibTypeFactory.Int32]));
        method.CilMethodBody = new();
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_0);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_1);
        method.CilMethodBody.Instructions.Add(opCode);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateLongUnaryOperator(ModuleDefinition module, string name, CilOpCode opCode)
    {
        var method = CreateMethodDefinition(name, MethodSignature.CreateStatic(module.CorLibTypeFactory.Int64, 0, [module.CorLibTypeFactory.Int64]));
        method.CilMethodBody = new();
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ldarg_0);
        method.CilMethodBody.Instructions.Add(opCode);
        method.CilMethodBody.Instructions.Add(CilOpCodes.Ret);
        return method;
    }

    private static MethodDefinition CreateMethodDefinition(string name, MethodSignature signature)
    {
        return new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, signature);
    }

    private static TypeDefinition CreateStaticHelperType(ModuleDefinition module, string ns, string name)
    {
        return new TypeDefinition(
            ns,
            name,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit);
    }

    private static TypeDefinition CreateEmptyType(ModuleDefinition module, string ns, string name)
    {
        return new TypeDefinition(
            ns,
            name,
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit);
    }

    private static void EmitDefaultValue(MethodDefinition method, ModuleDefinition module, TypeSignature returnType)
    {
        var instructions = method.CilMethodBody!.Instructions;

        if (returnType.FullName is "System.Void")
            return;

        if (returnType.FullName is "System.Int32" or "System.UInt32" or "System.Int16" or "System.UInt16" or "System.Byte" or "System.SByte" or "System.Boolean")
        {
            instructions.Add(CilOpCodes.Ldc_I4_0);
            return;
        }

        if (returnType.FullName is "System.Int64" or "System.UInt64")
        {
            instructions.Add(CilOpCodes.Ldc_I8, 0L);
            return;
        }

        if (returnType.FullName is "System.Single")
        {
            instructions.Add(CilOpCodes.Ldc_R4, 0.0f);
            return;
        }

        if (returnType.FullName is "System.Double")
        {
            instructions.Add(CilOpCodes.Ldc_R8, 0.0d);
            return;
        }

        if (returnType.FullName is "System.IntPtr" or "System.UIntPtr")
        {
            instructions.Add(CilOpCodes.Ldc_I4_0);
            instructions.Add(CilOpCodes.Conv_I);
            return;
        }

        if (returnType is GenericParameterSignature or { IsValueType: true })
        {
            var local = new CilLocalVariable(returnType);
            method.CilMethodBody!.LocalVariables.Add(local);
            instructions.Add(CilOpCodes.Ldloca_S, local);
            instructions.Add(CilOpCodes.Initobj, returnType.ToTypeDefOrRef());
            instructions.Add(CilOpCodes.Ldloc, local);
            return;
        }

        instructions.Add(CilOpCodes.Ldnull);
    }
}
