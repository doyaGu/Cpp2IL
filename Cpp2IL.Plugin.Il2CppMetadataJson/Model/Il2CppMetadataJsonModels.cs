namespace Cpp2IL.Plugin.Il2CppMetadataJson.Model;

public sealed class Il2CppMetadataJsonRoot
{
    public required string SchemaVersion { get; init; }
    public required GeneratorInfo Generator { get; init; }
    public required ApplicationInfo Application { get; init; }
    public required IReadOnlyList<AssemblyInfo> Assemblies { get; init; }
    public required IReadOnlyList<TypeInfo> Types { get; init; }
    public required IReadOnlyList<MethodInfo> Methods { get; init; }
    public required IReadOnlyList<ConcreteGenericMethodInfo> ConcreteGenericMethods { get; init; }
    public required IReadOnlyList<StringLiteralInfo> StringLiterals { get; init; }
    public required IReadOnlyList<AddressableStringLiteralInfo> AddressableStringLiterals { get; init; }
    public required IReadOnlyList<MetadataGlobalInfo> MetadataGlobals { get; init; }
    public required IReadOnlyList<MetadataMethodInfo> MetadataMethods { get; init; }
    public required IReadOnlyList<MetadataUsageInfo> Pre27GlobalMetadataUsages { get; init; }
    public required IReadOnlyList<string> Diagnostics { get; init; }
}

public sealed class GeneratorInfo
{
    public required string Name { get; init; }
    public required string OutputFormatId { get; init; }
    public required string OutputFormatName { get; init; }
    public required string PluginAssembly { get; init; }
    public required string PluginVersion { get; init; }
}

public sealed class ApplicationInfo
{
    public required string UnityVersion { get; init; }
    public required double MetadataVersion { get; init; }
    public required string InstructionSet { get; init; }
    public required ulong PointerSize { get; init; }
    public required long BinaryLength { get; init; }
    public required int InBinaryMetadataSize { get; init; }
    public required int AssemblyCount { get; init; }
    public required int TypeCount { get; init; }
    public required int MethodCount { get; init; }
    public required int GenericMethodCount { get; init; }
    public required int StringLiteralCount { get; init; }
}

public sealed class AssemblyInfo
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string CleanName { get; init; }
    public required uint? Token { get; init; }
    public required string? TokenHex { get; init; }
    public required string Version { get; init; }
    public required uint HashAlgorithm { get; init; }
    public required uint Flags { get; init; }
    public required string? Culture { get; init; }
    public required string? PublicKeyTokenHex { get; init; }
    public required string? PublicKeyHex { get; init; }
    public required ImageInfo? Image { get; init; }
    public required int TopLevelTypeCount { get; init; }
    public required int TotalTypeCount { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class ImageInfo
{
    public required string? Name { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required int TypeStart { get; init; }
    public required uint TypeCount { get; init; }
    public required int CustomAttributeStart { get; init; }
    public required uint CustomAttributeCount { get; init; }
    public required int ExportedTypeStart { get; init; }
    public required uint ExportedTypeCount { get; init; }
    public required int EntryPointIndex { get; init; }
}

public sealed class TypeInfo
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string FullName { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required string AssemblyName { get; init; }
    public required string? DeclaringTypeName { get; init; }
    public required string? BaseTypeName { get; init; }
    public required string? EnumUnderlyingTypeName { get; init; }
    public required string Attributes { get; init; }
    public required uint RawFlags { get; init; }
    public required uint RawBitfield { get; init; }
    public required bool IsInterface { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsSealed { get; init; }
    public required bool IsStatic { get; init; }
    public required bool IsEnum { get; init; }
    public required bool IsValueType { get; init; }
    public required bool HasFinalizer { get; init; }
    public required bool HasStaticConstructor { get; init; }
    public required bool IsBlittable { get; init; }
    public required bool IsImportOrWindowsRuntime { get; init; }
    public required bool IsByRefLike { get; init; }
    public required bool HasInlineArray { get; init; }
    public required uint PackingSize { get; init; }
    public required bool PackingSizeIsDefault { get; init; }
    public required uint SpecifiedPackingSize { get; init; }
    public required bool ClassSizeIsDefault { get; init; }
    public required TypeSizeInfo Sizes { get; init; }
    public required IReadOnlyList<string> Interfaces { get; init; }
    public required IReadOnlyList<GenericParameterInfo> GenericParameters { get; init; }
    public required IReadOnlyList<RgctxEntryInfo> RgctxEntries { get; init; }
    public required IReadOnlyList<string> RgctxMethodPointers { get; init; }
    public required IReadOnlyList<FieldInfo> Fields { get; init; }
    public required IReadOnlyList<PropertyInfo> Properties { get; init; }
    public required IReadOnlyList<EventInfo> Events { get; init; }
    public required IReadOnlyList<string> NestedTypes { get; init; }
    public required IReadOnlyList<string> Methods { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class TypeSizeInfo
{
    public required uint InstanceSize { get; init; }
    public required int NativeSize { get; init; }
    public required uint StaticFieldsSize { get; init; }
    public required uint ThreadStaticFieldsSize { get; init; }
}

public sealed class FieldInfo
{
    public required string Name { get; init; }
    public required string DeclaringTypeName { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required string FieldTypeName { get; init; }
    public required string Attributes { get; init; }
    public required int Offset { get; init; }
    public required bool IsStatic { get; init; }
    public required int IndexInParent { get; init; }
    public required object? ConstantValue { get; init; }
    public required string? ConstantValueDisplay { get; init; }
    public required byte[] StaticArrayInitialValue { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class PropertyInfo
{
    public required string Name { get; init; }
    public required string DeclaringTypeName { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required string PropertyTypeName { get; init; }
    public required string Attributes { get; init; }
    public required bool IsStatic { get; init; }
    public required string? Getter { get; init; }
    public required string? Setter { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class EventInfo
{
    public required string Name { get; init; }
    public required string DeclaringTypeName { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required string EventTypeName { get; init; }
    public required string Attributes { get; init; }
    public required bool IsStatic { get; init; }
    public required string? Adder { get; init; }
    public required string? Remover { get; init; }
    public required string? Invoker { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class MethodInfo
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string FullNameWithSignature { get; init; }
    public required string DeclaringTypeName { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required string ReturnTypeName { get; init; }
    public required string Attributes { get; init; }
    public required string ImplAttributes { get; init; }
    public required bool IsStatic { get; init; }
    public required bool IsVirtual { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsNewSlot { get; init; }
    public required string UnderlyingPointer { get; init; }
    public required string Rva { get; init; }
    public required int RawByteCount { get; init; }
    public required int Slot { get; init; }
    public required string? BaseMethod { get; init; }
    public required IReadOnlyList<string> Overrides { get; init; }
    public required IReadOnlyList<GenericParameterInfo> GenericParameters { get; init; }
    public required IReadOnlyList<ParameterInfo> Parameters { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class ConcreteGenericMethodInfo
{
    public required string FullNameWithSignature { get; init; }
    public required string BaseMethod { get; init; }
    public required string DeclaringAssembly { get; init; }
    public required string DeclaringTypeName { get; init; }
    public required string UnderlyingPointer { get; init; }
    public required string Rva { get; init; }
    public required int RawByteCount { get; init; }
    public required bool IsPartialInstantiation { get; init; }
    public required IReadOnlyList<string> TypeGenericParameters { get; init; }
    public required IReadOnlyList<string> MethodGenericParameters { get; init; }
    public required IReadOnlyList<ParameterInfo> Parameters { get; init; }
    public required string ReturnTypeName { get; init; }
}

public sealed class ParameterInfo
{
    public required string Name { get; init; }
    public required int Index { get; init; }
    public required uint Token { get; init; }
    public required string TokenHex { get; init; }
    public required string ParameterTypeName { get; init; }
    public required string Attributes { get; init; }
    public required bool IsRef { get; init; }
    public required object? DefaultValue { get; init; }
    public required string? DefaultValueDisplay { get; init; }
    public required IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; init; }
}

public sealed class GenericParameterInfo
{
    public required string Name { get; init; }
    public required int Index { get; init; }
    public required string Kind { get; init; }
    public required string Attributes { get; init; }
    public required IReadOnlyList<string> Constraints { get; init; }
}

public sealed class CustomAttributeInfo
{
    public required string Constructor { get; init; }
    public required string AttributeType { get; init; }
    public required string Display { get; init; }
    public required IReadOnlyList<CustomAttributeValueInfo> ConstructorParameters { get; init; }
    public required IReadOnlyList<NamedCustomAttributeValueInfo> Fields { get; init; }
    public required IReadOnlyList<NamedCustomAttributeValueInfo> Properties { get; init; }
}

public sealed class CustomAttributeValueInfo
{
    public required string Kind { get; init; }
    public required int Index { get; init; }
    public required string Display { get; init; }
    public required string? TypeName { get; init; }
}

public sealed class NamedCustomAttributeValueInfo
{
    public required string Name { get; init; }
    public required CustomAttributeValueInfo Value { get; init; }
}

public sealed class StringLiteralInfo
{
    public required int Index { get; init; }
    public required int DataIndex { get; init; }
    public required uint? Length { get; init; }
    public required string Value { get; init; }
}

public sealed class AddressableStringLiteralInfo
{
    public required int Index { get; init; }
    public required string Kind { get; init; }
    public required string Address { get; init; }
    public required string Rva { get; init; }
    public required string Value { get; init; }
}

public sealed class MetadataGlobalInfo
{
    public required string Kind { get; init; }
    public required string Address { get; init; }
    public required string Rva { get; init; }
    public required string DisplayName { get; init; }
    public required string? TypeName { get; init; }
    public required string? Token { get; init; }
    public required int TypeDefIndex { get; init; }
}

public sealed class MetadataMethodInfo
{
    public required string Address { get; init; }
    public required string Rva { get; init; }
    public required string Name { get; init; }
    public required string MethodRva { get; init; }
    public required string? Token { get; init; }
}

public sealed class MetadataUsageInfo
{
    public required string Kind { get; init; }
    public required string Address { get; init; }
    public required string Rva { get; init; }
    public required uint RawValue { get; init; }
    public required string? Display { get; init; }
    public required string? DisplayName { get; init; }
    public required string? TypeName { get; init; }
    public required string? Token { get; init; }
    public required int TypeDefIndex { get; init; }
    public required string? MethodName { get; init; }
    public required string? MethodRva { get; init; }
}

public sealed class RgctxEntryInfo
{
    public required string Kind { get; init; }
    public required int MethodIndex { get; init; }
    public required int TypeIndex { get; init; }
    public required string? TypeName { get; init; }
    public required string? Method { get; init; }
}
