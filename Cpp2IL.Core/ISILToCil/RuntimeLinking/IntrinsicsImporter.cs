using System;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Cpp2IL.Core.ISILToCil.RuntimeLinking;

internal sealed class IntrinsicsImporter
{
    private readonly ModuleDefinition _module;
    private readonly IntrinsicsReferenceStrategy _strategy;
    private TypeReference? _intrinsicsType;

    public IntrinsicsImporter(ModuleDefinition module, IntrinsicsReferenceStrategy strategy)
    {
        _module = module;
        _strategy = strategy;
    }

    public IMethodDescriptor GetCallNative() => CreateMethodReference(
        "CallNative",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, CreateObjectArrayType()]));

    public IMethodDescriptor GetCallIndirect() => CreateMethodReference(
        "CallIndirect",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, CreateObjectArrayType()]));

    public IMethodDescriptor GetLdMem(TypeSignature valueType)
    {
        var genericParameter = new GenericParameterSignature(_module, GenericParameterType.Method, 0);
        return CreateMethodReference(
            "LdMem",
            MethodSignature.CreateStatic(genericParameter, 1, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int64]))
            .MakeGenericInstanceMethod([valueType]);
    }

    public IMethodDescriptor GetLdMemRef() => CreateMethodReference(
        "LdMemRef",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int64]));

    public IMethodDescriptor GetStMem(TypeSignature valueType)
    {
        var genericParameter = new GenericParameterSignature(_module, GenericParameterType.Method, 0);
        return CreateMethodReference(
            "StMem",
            MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 1, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int64, genericParameter]))
            .MakeGenericInstanceMethod([valueType]);
    }

    public IMethodDescriptor GetStMemRef() => CreateMethodReference(
        "StMemRef",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int64, _module.CorLibTypeFactory.Object]));

    public IMethodDescriptor GetLoadStackSlot(TypeSignature valueType)
    {
        var genericParameter = new GenericParameterSignature(_module, GenericParameterType.Method, 0);
        return CreateMethodReference(
            "LoadStackSlot",
            MethodSignature.CreateStatic(genericParameter, 1, [_module.CorLibTypeFactory.Int32]))
            .MakeGenericInstanceMethod([valueType]);
    }

    public IMethodDescriptor GetStoreStackSlot(TypeSignature valueType)
    {
        var genericParameter = new GenericParameterSignature(_module, GenericParameterType.Method, 0);
        return CreateMethodReference(
            "StoreStackSlot",
            MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 1, [_module.CorLibTypeFactory.Int32, genericParameter]))
            .MakeGenericInstanceMethod([valueType]);
    }

    public IMethodDescriptor GetNewObj() => CreateMethodReference(
        "NewObj",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, CreateObjectArrayType()]));

    public IMethodDescriptor GetBox() => CreateMethodReference(
        "Box",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Object]));

    public IMethodDescriptor GetUnboxAny(TypeSignature valueType)
    {
        var genericParameter = new GenericParameterSignature(_module, GenericParameterType.Method, 0);
        return CreateMethodReference(
            "UnboxAny",
            MethodSignature.CreateStatic(genericParameter, 1, [_module.CorLibTypeFactory.Object]))
            .MakeGenericInstanceMethod([valueType]);
    }

    public IMethodDescriptor GetUnboxAnyWithTypeHandle() => CreateMethodReference(
        "UnboxAny",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Object]));

    public IMethodDescriptor GetLdStrHandle() => CreateMethodReference(
        "LdStr",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.String, 0, [_module.CorLibTypeFactory.IntPtr]));

    public IMethodDescriptor GetLdStrToken() => CreateMethodReference(
        "LdStr",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.String, 0, [_module.CorLibTypeFactory.Int32]));

    public IMethodDescriptor GetThrowNotImplemented() => CreateMethodReference(
        "ThrowNotImplemented",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 0, [_module.CorLibTypeFactory.String]));

    public IMethodDescriptor GetThrowInvalid() => CreateMethodReference(
        "ThrowInvalid",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 0, [_module.CorLibTypeFactory.String]));

    public TypeReference GetIl2CppMethodHandle() => CreateTypeReference("Il2CppMethodHandle");
    public TypeReference GetIl2CppRGCTXData() => CreateTypeReference("Il2CppRGCTXData");

    public IMethodDescriptor GetIl2CppInvokerInvokeVoid() => CreateMethodReference(
        "Il2CppInvoker",
        "InvokeVoid",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 0, [_module.CorLibTypeFactory.IntPtr, CreateObjectArrayType()]));

    public IMethodDescriptor GetIl2CppInvokerInvoke(TypeSignature returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);

        var genericParameter = new GenericParameterSignature(_module, GenericParameterType.Method, 0);
        return CreateMethodReference(
            "Il2CppInvoker",
            "Invoke",
            MethodSignature.CreateStatic(genericParameter, 1, [_module.CorLibTypeFactory.IntPtr, CreateObjectArrayType()]))
            .MakeGenericInstanceMethod([returnType]);
    }

    public IMethodDescriptor GetIl2CppDelegateConstruct() => CreateMethodReference(
        "Il2CppDelegate",
        "Construct",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Object, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Object]));

    public IMethodDescriptor GetIl2CppMetadataInitialize() => CreateMethodReference(
        "Il2CppMetadata",
        "Initialize",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 0, [_module.CorLibTypeFactory.IntPtr]));

    public IMethodDescriptor GetIl2CppRGCTXDataData() => CreateMethodReference(
        "Il2CppRGCTXData",
        "GetData",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.IntPtr, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int32]));

    public IMethodDescriptor GetIl2CppRGCTXDataDataNoInit() => CreateMethodReference(
        "Il2CppRGCTXData",
        "GetDataNoInit",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.IntPtr, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int32]));

    public IMethodDescriptor GetIl2CppRGCTXDataType() => CreateMethodReference(
        "Il2CppRGCTXData",
        "GetType",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.IntPtr, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int32]));

    public IMethodDescriptor GetIl2CppRGCTXDataMethod() => CreateMethodReference(
        "Il2CppRGCTXData",
        "GetMethod",
        MethodSignature.CreateStatic(GetIl2CppMethodHandle().ToTypeSignature(false), 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int32]));

    public IMethodDescriptor GetIl2CppRGCTXDataField() => CreateMethodReference(
        "Il2CppRGCTXData",
        "GetField",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.IntPtr, 0, [_module.CorLibTypeFactory.IntPtr, _module.CorLibTypeFactory.Int32]));

    public IMethodDescriptor GetIl2CppRGCTXDataIsInitialized() => CreateMethodReference(
        "Il2CppRGCTXData",
        "IsInitialized",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Boolean, 0, [GetIl2CppMethodHandle().ToTypeSignature(false)]));

    public IMethodDescriptor GetIl2CppRGCTXDataInitializeMethod() => CreateMethodReference(
        "Il2CppRGCTXData",
        "InitializeMethod",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Void, 0, [GetIl2CppMethodHandle().ToTypeSignature(false)]));

    public IMethodDescriptor GetOpAdd() => GetLongBinaryOperator("Op_Add");
    public IMethodDescriptor GetOpSub() => GetLongBinaryOperator("Op_Sub");
    public IMethodDescriptor GetOpMul() => GetLongBinaryOperator("Op_Mul");
    public IMethodDescriptor GetOpDiv() => GetLongBinaryOperator("Op_Div");
    public IMethodDescriptor GetOpAnd() => GetLongBinaryOperator("Op_And");
    public IMethodDescriptor GetOpOr() => GetLongBinaryOperator("Op_Or");
    public IMethodDescriptor GetOpXor() => GetLongBinaryOperator("Op_Xor");
    public IMethodDescriptor GetOpShl() => CreateMethodReference(
        "Op_Shl",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Int64, 0, [_module.CorLibTypeFactory.Int64, _module.CorLibTypeFactory.Int32]));
    public IMethodDescriptor GetOpShr() => CreateMethodReference(
        "Op_Shr",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Int64, 0, [_module.CorLibTypeFactory.Int64, _module.CorLibTypeFactory.Int32]));
    public IMethodDescriptor GetOpNeg() => CreateMethodReference(
        "Op_Neg",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Int64, 0, [_module.CorLibTypeFactory.Int64]));
    public IMethodDescriptor GetOpNot() => CreateMethodReference(
        "Op_Not",
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Int64, 0, [_module.CorLibTypeFactory.Int64]));

    private SzArrayTypeSignature CreateObjectArrayType() => _module.CorLibTypeFactory.Object.MakeSzArrayType();

    private IMethodDescriptor GetLongBinaryOperator(string name) => CreateMethodReference(
        name,
        MethodSignature.CreateStatic(_module.CorLibTypeFactory.Int64, 0, [_module.CorLibTypeFactory.Int64, _module.CorLibTypeFactory.Int64]));

    private TypeReference GetIntrinsicsTypeReference()
    {
        if (_intrinsicsType != null)
            return _intrinsicsType;

        var assemblyReference = _strategy.EnsureRuntimeAssemblyReference(_module);
        _intrinsicsType = assemblyReference.CreateTypeReference(_strategy.RuntimeNamespace, _strategy.IntrinsicsTypeName);
        return _intrinsicsType;
    }

    private TypeReference CreateTypeReference(string name)
    {
        return GetRuntimeAssemblyReference().CreateTypeReference(_strategy.RuntimeNamespace, name);
    }

    private AssemblyReference GetRuntimeAssemblyReference() => _strategy.EnsureRuntimeAssemblyReference(_module);

    private MemberReference CreateMethodReference(string name, MethodSignature signature)
    {
        return GetIntrinsicsTypeReference().CreateMemberReference(name, signature);
    }

    private MemberReference CreateMethodReference(string typeName, string name, MethodSignature signature)
    {
        return CreateRuntimeTypeReference(typeName).CreateMemberReference(name, signature);
    }

    private TypeReference CreateRuntimeTypeReference(string typeName) => GetRuntimeAssemblyReference().CreateTypeReference(_strategy.RuntimeNamespace, typeName);
}
