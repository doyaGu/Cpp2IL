using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Cpp2IL.Plugin.Il2CppMetadataJson.Model;
using LibCpp2IL;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Cpp2IL.Plugin.Il2CppMetadataJson;

public class Il2CppMetadataJsonOutputFormat : Cpp2IlOutputFormat
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public override string OutputFormatId => "il2cpp-metadata-json";

    public override string OutputFormatName => "IL2CPP Metadata JSON";

    public override void DoOutput(ApplicationAnalysisContext context, string outputRoot)
    {
        Directory.CreateDirectory(outputRoot);

        var diagnostics = new List<string>();
        PrepareCustomAttributes(context, diagnostics);

        var allTypes = context.AllTypes.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
        var allMethods = allTypes.SelectMany(t => t.Methods).OrderBy(m => m.FullNameWithSignature, StringComparer.Ordinal).ToList();
        var allGenericMethods = context.ConcreteGenericMethodsByRef.Values.OrderBy(m => m.FullNameWithSignature, StringComparer.Ordinal).ToList();
        var addressableMetadata = BuildAddressableMetadata(context, allMethods, diagnostics);

        var root = new Il2CppMetadataJsonRoot
        {
            SchemaVersion = "1.1.0",
            Generator = BuildGeneratorInfo(),
            Application = new ApplicationInfo
            {
                UnityVersion = context.UnityVersion.ToString(),
                MetadataVersion = context.MetadataVersion,
                InstructionSet = context.Binary.InstructionSetId.Name,
                PointerSize = context.Binary.PointerSize,
                BinaryLength = context.Binary.RawLength,
                InBinaryMetadataSize = context.Binary.InBinaryMetadataSize,
                AssemblyCount = context.Assemblies.Count,
                TypeCount = allTypes.Count,
                MethodCount = allMethods.Count,
                GenericMethodCount = allGenericMethods.Count,
                StringLiteralCount = context.Metadata.stringLiterals.Length,
            },
            Assemblies = context.Assemblies
                .OrderBy(a => a.Name, StringComparer.Ordinal)
                .Select(ExportAssembly)
                .ToArray(),
            Types = allTypes.Select(type => ExportType(type, diagnostics)).ToArray(),
            Methods = allMethods.Select(method => ExportMethod(method, diagnostics)).ToArray(),
            ConcreteGenericMethods = allGenericMethods.Select(ExportConcreteGenericMethod).ToArray(),
            StringLiterals = context.Metadata.stringLiterals
                .Select((literal, index) => new StringLiteralInfo
                {
                    Index = index,
                    DataIndex = literal.dataIndex,
                    Length = context.MetadataVersion < 35 ? literal.length : null,
                    Value = context.Metadata.GetStringLiteralFromIndex((uint)index),
                })
                .ToArray(),
            AddressableStringLiterals = addressableMetadata.Strings,
            MetadataGlobals = addressableMetadata.Globals,
            MetadataMethods = addressableMetadata.Methods,
            Pre27GlobalMetadataUsages = BuildPre27GlobalMetadataUsages(context, diagnostics),
            Diagnostics = diagnostics.Distinct(StringComparer.Ordinal).OrderBy(message => message, StringComparer.Ordinal).ToArray(),
        };

        var outputPath = Path.Combine(outputRoot, "il2cpp-metadata.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(root, JsonOptions), Encoding.UTF8);
        Logger.InfoNewline($"Wrote IL2CPP metadata JSON to {outputPath}", nameof(Il2CppMetadataJsonOutputFormat));
    }

    private static GeneratorInfo BuildGeneratorInfo()
    {
        var assembly = typeof(Il2CppMetadataJsonOutputFormat).Assembly.GetName();
        return new GeneratorInfo
        {
            Name = "Cpp2IL IL2CPP Metadata JSON Plugin",
            OutputFormatId = "il2cpp-metadata-json",
            OutputFormatName = "IL2CPP Metadata JSON",
            PluginAssembly = assembly.Name ?? "unknown",
            PluginVersion = assembly.Version?.ToString() ?? "0.0.0.0",
        };
    }

    private static void PrepareCustomAttributes(ApplicationAnalysisContext context, List<string> diagnostics)
    {
        foreach (var owner in EnumerateCustomAttributeOwners(context))
        {
            try
            {
                owner.AnalyzeCustomAttributeData();
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Custom attribute analysis failed for {owner}: {ex.Message}");
            }
        }
    }

    private static IEnumerable<HasCustomAttributes> EnumerateCustomAttributeOwners(ApplicationAnalysisContext context)
    {
        foreach (var assembly in context.Assemblies)
            yield return assembly;

        foreach (var type in context.AllTypes)
        {
            yield return type;

            foreach (var field in type.Fields)
                yield return field;

            foreach (var property in type.Properties)
                yield return property;

            foreach (var @event in type.Events)
                yield return @event;

            foreach (var method in type.Methods)
            {
                yield return method;

                foreach (var parameter in method.Parameters)
                    yield return parameter;
            }
        }
    }

    private static AssemblyInfo ExportAssembly(AssemblyAnalysisContext assembly)
    {
        var definition = assembly.Definition;
        var image = definition?.Image;

        return new AssemblyInfo
        {
            Name = assembly.Name,
            FullName = definition?.AssemblyName.ToString() ?? assembly.Name,
            CleanName = assembly.CleanAssemblyName,
            Token = definition?.Token,
            TokenHex = definition != null ? Hex(definition.Token) : null,
            Version = assembly.Version.ToString(),
            HashAlgorithm = assembly.HashAlgorithm,
            Flags = assembly.Flags,
            Culture = assembly.Culture,
            PublicKeyTokenHex = BytesToHex(assembly.PublicKeyToken),
            PublicKeyHex = BytesToHex(assembly.PublicKey),
            Image = image == null ? null : new ImageInfo
            {
                Name = image.Name,
                Token = image.token,
                TokenHex = Hex(image.token),
                TypeStart = image.firstTypeIndex.Value,
                TypeCount = image.typeCount,
                CustomAttributeStart = image.customAttributeStart,
                CustomAttributeCount = image.customAttributeCount,
                ExportedTypeStart = image.exportedTypeStart.IsNull ? -1 : image.exportedTypeStart.Value,
                ExportedTypeCount = image.exportedTypeCount,
                EntryPointIndex = image.entryPointIndex.IsNull ? -1 : image.entryPointIndex.Value,
            },
            TopLevelTypeCount = assembly.TopLevelTypes.Count(),
            TotalTypeCount = assembly.Types.Count,
            CustomAttributes = ExportCustomAttributes(assembly.CustomAttributes),
        };
    }

    private static TypeInfo ExportType(TypeAnalysisContext type, List<string> diagnostics)
    {
        var definition = type.Definition;
        if (definition == null)
            throw new InvalidOperationException($"Injected type export is not supported in this format: {type.FullName}");

        IReadOnlyList<RgctxEntryInfo> rgctxEntries;
        IReadOnlyList<string> rgctxMethodPointers;
        try
        {
            rgctxEntries = definition.RgctXs.Select(ExportRgctxEntry).ToArray();
            rgctxMethodPointers = definition.RgctxMethodPointers.Select(Hex).ToArray();
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to resolve RGCTX data for {type.FullName}: {ex.Message}");
            rgctxEntries = [];
            rgctxMethodPointers = [];
        }

        TypeSizeInfo sizes;
        try
        {
            var rawSizes = definition.RawSizes;
            sizes = new TypeSizeInfo
            {
                InstanceSize = rawSizes.instance_size,
                NativeSize = rawSizes.native_size,
                StaticFieldsSize = rawSizes.static_fields_size,
                ThreadStaticFieldsSize = rawSizes.thread_static_fields_size,
            };
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to resolve type sizes for {type.FullName}: {ex.Message}");
            sizes = new TypeSizeInfo
            {
                InstanceSize = 0,
                NativeSize = 0,
                StaticFieldsSize = 0,
                ThreadStaticFieldsSize = 0,
            };
        }

        return new TypeInfo
        {
            Name = type.Name,
            Namespace = type.Namespace,
            FullName = type.FullName,
            Token = definition.Token,
            TokenHex = Hex(definition.Token),
            AssemblyName = type.DeclaringAssembly.Name,
            DeclaringTypeName = type.DeclaringType?.FullName,
            BaseTypeName = type.BaseType?.FullName,
            EnumUnderlyingTypeName = definition.IsEnumType ? type.EnumUnderlyingType?.FullName : null,
            Attributes = type.Attributes.ToString(),
            RawFlags = definition.Flags,
            RawBitfield = definition.Bitfield,
            IsInterface = type.IsInterface,
            IsAbstract = type.IsAbstract,
            IsSealed = type.IsSealed,
            IsStatic = type.IsStatic,
            IsEnum = definition.IsEnumType,
            IsValueType = definition.IsValueType,
            HasFinalizer = definition.HasFinalizer,
            HasStaticConstructor = definition.HasCctor,
            IsBlittable = definition.IsBlittable,
            IsImportOrWindowsRuntime = definition.IsImportOrWindowsRuntime,
            IsByRefLike = definition.IsByRefLike,
            HasInlineArray = definition.HasInlineArray,
            PackingSize = definition.PackingSize,
            PackingSizeIsDefault = definition.PackingSizeIsDefault,
            SpecifiedPackingSize = definition.SpecifiedPackingSize,
            ClassSizeIsDefault = definition.ClassSizeIsDefault,
            Sizes = sizes,
            Interfaces = type.InterfaceContexts.Select(i => i.FullName).ToArray(),
            GenericParameters = type.GenericParameters.Select(ExportGenericParameter).ToArray(),
            RgctxEntries = rgctxEntries,
            RgctxMethodPointers = rgctxMethodPointers,
            Fields = type.Fields.Select(field => ExportField(field, diagnostics)).ToArray(),
            Properties = type.Properties.Select(property => ExportProperty(property)).ToArray(),
            Events = type.Events.Select(ExportEvent).ToArray(),
            NestedTypes = type.NestedTypes.Select(n => n.FullName).ToArray(),
            Methods = type.Methods.Select(m => m.FullNameWithSignature).ToArray(),
            CustomAttributes = ExportCustomAttributes(type.CustomAttributes),
        };
    }

    private static FieldInfo ExportField(FieldAnalysisContext field, List<string> diagnostics)
    {
        var token = field.BackingData?.Field.token ?? 0U;
        byte[] staticArrayInitialValue;
        try
        {
            staticArrayInitialValue = field.StaticArrayInitialValue;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to resolve static array initializer for {field}: {ex.Message}");
            staticArrayInitialValue = [];
        }

        object? constantValue;
        try
        {
            constantValue = field.ConstantValue;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to resolve constant value for {field}: {ex.Message}");
            constantValue = null;
        }

        return new FieldInfo
        {
            Name = field.Name,
            DeclaringTypeName = field.DeclaringType.FullName,
            Token = token,
            TokenHex = Hex(token),
            FieldTypeName = field.FieldType.FullName,
            Attributes = field.Attributes.ToString(),
            Offset = field.Offset,
            IsStatic = field.IsStatic,
            IndexInParent = field.BackingData?.IndexInParent ?? -1,
            ConstantValue = NormalizeValue(constantValue),
            ConstantValueDisplay = constantValue?.ToString(),
            StaticArrayInitialValue = staticArrayInitialValue,
            CustomAttributes = ExportCustomAttributes(field.CustomAttributes),
        };
    }

    private static PropertyInfo ExportProperty(PropertyAnalysisContext property)
    {
        return new PropertyInfo
        {
            Name = property.Name,
            DeclaringTypeName = property.DeclaringType.FullName,
            Token = property.Definition?.token ?? 0U,
            TokenHex = Hex(property.Definition?.token ?? 0U),
            PropertyTypeName = property.PropertyType.FullName,
            Attributes = property.Attributes.ToString(),
            IsStatic = property.IsStatic,
            Getter = property.Getter?.FullNameWithSignature,
            Setter = property.Setter?.FullNameWithSignature,
            CustomAttributes = ExportCustomAttributes(property.CustomAttributes),
        };
    }

    private static EventInfo ExportEvent(EventAnalysisContext @event)
    {
        return new EventInfo
        {
            Name = @event.Name,
            DeclaringTypeName = @event.DeclaringType.FullName,
            Token = @event.Definition?.token ?? 0U,
            TokenHex = Hex(@event.Definition?.token ?? 0U),
            EventTypeName = @event.EventType.FullName,
            Attributes = @event.Attributes.ToString(),
            IsStatic = @event.IsStatic,
            Adder = @event.Adder?.FullNameWithSignature,
            Remover = @event.Remover?.FullNameWithSignature,
            Invoker = @event.Invoker?.FullNameWithSignature,
            CustomAttributes = ExportCustomAttributes(@event.CustomAttributes),
        };
    }

    private static MethodInfo ExportMethod(MethodAnalysisContext method, List<string> diagnostics)
    {
        int rawByteCount;
        try
        {
            rawByteCount = method.RawBytes.Length;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to resolve raw bytes for {method.FullName}: {ex.Message}");
            rawByteCount = 0;
        }

        return new MethodInfo
        {
            Name = method.Name,
            FullName = method.FullName,
            FullNameWithSignature = method.FullNameWithSignature,
            DeclaringTypeName = method.DeclaringType?.FullName ?? "<unknown>",
            Token = method.Definition?.token ?? 0U,
            TokenHex = Hex(method.Definition?.token ?? 0U),
            ReturnTypeName = method.ReturnType.FullName,
            Attributes = method.Attributes.ToString(),
            ImplAttributes = method.ImplAttributes.ToString(),
            IsStatic = method.IsStatic,
            IsVirtual = method.IsVirtual,
            IsAbstract = method.IsAbstract,
            IsNewSlot = method.IsNewSlot,
            UnderlyingPointer = Hex(method.UnderlyingPointer),
            Rva = Hex(method.Rva),
            RawByteCount = rawByteCount,
            Slot = method.Definition?.slot ?? -1,
            BaseMethod = method.BaseMethod?.FullNameWithSignature,
            Overrides = method.Overrides.Select(o => o.FullNameWithSignature).ToArray(),
            GenericParameters = method.GenericParameters.Select(ExportGenericParameter).ToArray(),
            Parameters = method.Parameters.Select(ExportParameter).ToArray(),
            CustomAttributes = ExportCustomAttributes(method.CustomAttributes),
        };
    }

    private static ConcreteGenericMethodInfo ExportConcreteGenericMethod(ConcreteGenericMethodAnalysisContext method)
    {
        return new ConcreteGenericMethodInfo
        {
            FullNameWithSignature = method.FullNameWithSignature,
            BaseMethod = method.BaseMethodContext.FullNameWithSignature,
            DeclaringAssembly = method.DeclaringAsm.Name,
            DeclaringTypeName = method.DeclaringType?.FullName ?? "<unknown>",
            UnderlyingPointer = Hex(method.UnderlyingPointer),
            Rva = Hex(method.Rva),
            RawByteCount = method.RawBytes.Length,
            IsPartialInstantiation = method.IsPartialInstantiation,
            TypeGenericParameters = method.TypeGenericParameters.Select(t => t.FullName).ToArray(),
            MethodGenericParameters = method.MethodGenericParameters.Select(t => t.FullName).ToArray(),
            Parameters = method.Parameters.Select(ExportParameter).ToArray(),
            ReturnTypeName = method.ReturnType.FullName,
        };
    }

    private static ParameterInfo ExportParameter(ParameterAnalysisContext parameter)
    {
        var defaultValue = parameter.DefaultValue?.ContainedDefaultValue;
        return new ParameterInfo
        {
            Name = parameter.Name,
            Index = parameter.ParameterIndex,
            Token = parameter.Definition?.token ?? 0U,
            TokenHex = Hex(parameter.Definition?.token ?? 0U),
            ParameterTypeName = parameter.ParameterType.FullName,
            Attributes = parameter.Attributes.ToString(),
            IsRef = parameter.IsRef,
            DefaultValue = NormalizeValue(defaultValue),
            DefaultValueDisplay = defaultValue?.ToString(),
            CustomAttributes = ExportCustomAttributes(parameter.CustomAttributes),
        };
    }

    private static GenericParameterInfo ExportGenericParameter(GenericParameterTypeAnalysisContext parameter)
    {
        return new GenericParameterInfo
        {
            Name = parameter.Name,
            Index = parameter.Index,
            Kind = parameter.Type.ToString(),
            Attributes = parameter.Attributes.ToString(),
            Constraints = parameter.ConstraintTypes.Select(c => c.FullName).ToArray(),
        };
    }

    private static IReadOnlyList<CustomAttributeInfo> ExportCustomAttributes(List<AnalyzedCustomAttribute>? customAttributes)
    {
        if (customAttributes == null || customAttributes.Count == 0)
            return [];

        return customAttributes.Select(attribute => new CustomAttributeInfo
        {
            Constructor = attribute.Constructor.FullNameWithSignature,
            AttributeType = attribute.Constructor.DeclaringType?.FullName ?? attribute.Constructor.Name,
            Display = attribute.ToString(),
            ConstructorParameters = attribute.ConstructorParameters.Select(ExportCustomAttributeValue).ToArray(),
            Fields = attribute.Fields.Select(field => new NamedCustomAttributeValueInfo
            {
                Name = field.Field.Name,
                Value = ExportCustomAttributeValue(field.Value),
            }).ToArray(),
            Properties = attribute.Properties.Select(property => new NamedCustomAttributeValueInfo
            {
                Name = property.Property.Name,
                Value = ExportCustomAttributeValue(property.Value),
            }).ToArray(),
        }).ToArray();
    }

    private static CustomAttributeValueInfo ExportCustomAttributeValue(BaseCustomAttributeParameter parameter)
    {
        return new CustomAttributeValueInfo
        {
            Kind = parameter.Kind.ToString(),
            Index = parameter.Index,
            Display = parameter.ToString() ?? string.Empty,
            TypeName = TryGetCustomAttributeValueType(parameter),
        };
    }

    private static string? TryGetCustomAttributeValueType(BaseCustomAttributeParameter parameter)
    {
        return parameter switch
        {
            BaseCustomAttributeTypeParameter typeParameter => typeParameter.TypeContext?.FullName,
            CustomAttributeEnumParameter enumParameter => enumParameter.EnumTypeContext.FullName,
            CustomAttributePrimitiveParameter primitiveParameter => primitiveParameter.PrimitiveType.ToString(),
            CustomAttributeArrayParameter arrayParameter => arrayParameter.EnumType?.AsClass().FullName ?? arrayParameter.ArrType.ToString(),
            CustomAttributeNullParameter => null,
            _ => null,
        };
    }

    private static RgctxEntryInfo ExportRgctxEntry(Il2CppRGCTXDefinition entry)
    {
        return new RgctxEntryInfo
        {
            Kind = entry.type.ToString(),
            MethodIndex = entry.MethodIndex,
            TypeIndex = entry.TypeIndex,
            TypeName = entry.Type?.ToString(),
            Method = entry.MethodSpec?.ToString(),
        };
    }

    private static AddressableMetadataExport BuildAddressableMetadata(ApplicationAnalysisContext context, IReadOnlyList<MethodAnalysisContext> allMethods,
        List<string> diagnostics)
    {
        var stringsByAddress = new Dictionary<ulong, AddressableStringLiteralInfo>();
        var globalsByAddress = new Dictionary<ulong, MetadataGlobalInfo>();
        var methodsByAddress = new Dictionary<ulong, MetadataMethodInfo>();
        var visitedAddresses = new HashSet<ulong>();

        if (context.MetadataVersion < 27)
        {
            foreach (var usage in EnumeratePre27MetadataUsages())
            {
                AddResolvedMetadataUsage(context, usage, stringsByAddress, globalsByAddress, methodsByAddress, diagnostics);
                visitedAddresses.Add(usage.Offset);
            }
        }

        foreach (var method in allMethods)
        {
            if (method.UnderlyingPointer == 0 || method.IsAbstract)
                continue;

            try
            {
                foreach (var address in EnumerateAbsoluteMetadataAddresses(context, method))
                {
                    if (!visitedAddresses.Add(address))
                        continue;

                    var usage = LibCpp2IlMain.GetAnyGlobalByAddress(address);
                    if (usage == null)
                        continue;

                    AddResolvedMetadataUsage(context, usage, stringsByAddress, globalsByAddress, methodsByAddress, diagnostics);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Failed to recover metadata addresses for {method.FullNameWithSignature}: {ex.Message}");
            }
        }

        return new AddressableMetadataExport(
            stringsByAddress.Values.OrderBy(s => ParseSortableHex(s.Rva)).ThenBy(s => s.Index).ToArray(),
            globalsByAddress.Values.OrderBy(g => ParseSortableHex(g.Rva)).ThenBy(g => g.DisplayName, StringComparer.Ordinal).ToArray(),
            methodsByAddress.Values.OrderBy(m => ParseSortableHex(m.Rva)).ThenBy(m => m.Name, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<ulong> EnumerateAbsoluteMetadataAddresses(ApplicationAnalysisContext context, MethodAnalysisContext method)
    {
        var isil = context.InstructionSet.GetIsilFromMethod(method);
        if (isil.Count == 0)
            yield break;

        foreach (var instruction in isil)
        {
            foreach (var operand in instruction.Operands)
            {
                if (operand.Type != InstructionSetIndependentOperand.OperandType.Memory)
                    continue;

                var memoryOp = (IsilMemoryOperand)operand.Data;
                if (memoryOp.Base != null || memoryOp.Index != null || memoryOp.Scale != 0 || memoryOp.Addend <= 0)
                    continue;

                yield return (ulong)memoryOp.Addend;
            }
        }
    }

    private static IReadOnlyList<MetadataUsageInfo> BuildPre27GlobalMetadataUsages(ApplicationAnalysisContext context, List<string> diagnostics)
    {
        if (context.MetadataVersion >= 27)
            return [];

        return EnumeratePre27MetadataUsages()
            .Select(usage => ExportMetadataUsage(context, usage, diagnostics))
            .Where(info => info != null)
            .Cast<MetadataUsageInfo>()
            .OrderBy(info => ParseSortableHex(info.Rva))
            .ThenBy(info => info.Kind, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<MetadataUsage> EnumeratePre27MetadataUsages()
    {
        var typeRefs = GetMetadataUsageCache("TypeRefs");
        var methodRefs = GetMetadataUsageCache("MethodRefs");
        var fieldRefs = GetMetadataUsageCache("FieldRefs");
        var literals = GetMetadataUsageCache("Literals");

        return [.. typeRefs, .. methodRefs, .. fieldRefs, .. literals];
    }

    private static IEnumerable<MetadataUsage> GetMetadataUsageCache(string fieldName)
    {
        var field = typeof(LibCpp2IlGlobalMapper).GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (field?.GetValue(null) is IEnumerable<MetadataUsage> values)
            return values;

        return [];
    }

    private static MetadataUsageInfo? ExportMetadataUsage(ApplicationAnalysisContext context, MetadataUsage usage, List<string> diagnostics)
    {
        try
        {
            if (!TryGetRva(usage.Offset, out var rva))
                return null;

            var details = DescribeMetadataUsage(context, usage);
            return new MetadataUsageInfo
            {
                Kind = details.Kind,
                Address = Hex(usage.Offset),
                Rva = Hex(rva),
                RawValue = usage.RawValue,
                Display = details.Display,
                DisplayName = details.DisplayName,
                TypeName = details.TypeName,
                Token = details.Token,
                TypeDefIndex = details.TypeDefIndex,
                MethodName = details.MethodName,
                MethodRva = details.MethodRva.HasValue ? Hex(details.MethodRva.Value) : null,
            };
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to export raw metadata usage at {Hex(usage.Offset)}: {ex.Message}");
            return null;
        }
    }

    private static void AddResolvedMetadataUsage(ApplicationAnalysisContext context, MetadataUsage usage,
        IDictionary<ulong, AddressableStringLiteralInfo> stringsByAddress,
        IDictionary<ulong, MetadataGlobalInfo> globalsByAddress,
        IDictionary<ulong, MetadataMethodInfo> methodsByAddress,
        List<string> diagnostics)
    {
        try
        {
            if (!TryGetRva(usage.Offset, out var rva))
                return;

            var details = DescribeMetadataUsage(context, usage);

            if (details.StringValue != null)
            {
                stringsByAddress[usage.Offset] = new AddressableStringLiteralInfo
                {
                    Index = details.StringIndex,
                    Kind = "StringLiteral",
                    Address = Hex(usage.Offset),
                    Rva = Hex(rva),
                    Value = details.StringValue,
                };
            }

            globalsByAddress[usage.Offset] = new MetadataGlobalInfo
            {
                Kind = details.Kind,
                Address = Hex(usage.Offset),
                Rva = Hex(rva),
                DisplayName = details.DisplayName,
                TypeName = details.TypeName,
                Token = details.Token,
                TypeDefIndex = details.TypeDefIndex,
            };

            if (details.MethodName != null && details.MethodRva.HasValue)
            {
                methodsByAddress[usage.Offset] = new MetadataMethodInfo
                {
                    Address = Hex(usage.Offset),
                    Rva = Hex(rva),
                    Name = details.MethodName,
                    MethodRva = Hex(details.MethodRva.Value),
                    Token = details.Token,
                };
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Failed to resolve metadata usage at {Hex(usage.Offset)}: {ex.Message}");
        }
    }

    private static MetadataUsageDetails DescribeMetadataUsage(ApplicationAnalysisContext context, MetadataUsage usage)
    {
        var display = SafeToString(() => usage.Value);

        return usage.Type switch
        {
            MetadataUsageType.TypeInfo => DescribeTypeUsage(context, usage, "TypeInfo", display),
            MetadataUsageType.Type => DescribeTypeUsage(context, usage, "Il2CppType", display),
            MetadataUsageType.FieldInfo => DescribeFieldUsage(context, usage, display),
            MetadataUsageType.MethodDef => DescribeMethodUsage(context, usage, display),
            MetadataUsageType.MethodRef => DescribeGenericMethodUsage(context, usage, display),
            MetadataUsageType.StringLiteral => DescribeStringUsage(usage, display),
            _ => new MetadataUsageDetails(usage.Type.ToString(), display ?? usage.Type.ToString(), display, null, null, -1, null, null, null, -1),
        };
    }

    private static MetadataUsageDetails DescribeTypeUsage(ApplicationAnalysisContext context, MetadataUsage usage, string kind, string? display)
    {
        var reflectionData = usage.AsType();
        var typeName = reflectionData.ToString();
        var baseType = reflectionData.baseType;
        var typeContext = context.ResolveContextForType(baseType);
        return new MetadataUsageDetails(
            kind,
            typeName ?? display ?? kind,
            display ?? typeName,
            typeName,
            baseType == null ? null : Hex(baseType.Token),
            baseType?.TypeIndex.Value ?? -1,
            null,
            null,
            null,
            -1);
    }

    private static MetadataUsageDetails DescribeFieldUsage(ApplicationAnalysisContext context, MetadataUsage usage, string? display)
    {
        var field = usage.AsField();
        var fieldContext = context.ResolveContextForField(field);
        var typeName = fieldContext?.FieldType.FullName ?? field.FieldType?.ToString();
        var displayName = fieldContext == null
            ? $"{field.DeclaringType.FullName}.{field.Name}"
            : $"{fieldContext.DeclaringType.FullName}.{fieldContext.Name}";

        return new MetadataUsageDetails(
            "FieldInfo",
            displayName,
            display ?? displayName,
            typeName,
            Hex(field.token),
            field.DeclaringType.TypeIndex.Value,
            null,
            null,
            null,
            -1);
    }

    private static MetadataUsageDetails DescribeMethodUsage(ApplicationAnalysisContext context, MetadataUsage usage, string? display)
    {
        var method = usage.AsMethod();
        var methodContext = context.ResolveContextForMethod(method);
        var methodName = methodContext?.FullNameWithSignature ?? method.HumanReadableSignature ?? method.GlobalKey ?? method.Name ?? "<method>";
        var methodRva = methodContext?.Rva ?? method.Rva;
        return new MetadataUsageDetails(
            "MethodInfo",
            methodName,
            display ?? methodName,
            method.DeclaringType?.FullName,
            Hex(method.token),
            method.DeclaringType?.TypeIndex.Value ?? -1,
            methodName,
            methodRva,
            null,
            -1);
    }

    private static MetadataUsageDetails DescribeGenericMethodUsage(ApplicationAnalysisContext context, MetadataUsage usage, string? display)
    {
        var methodRef = usage.AsGenericMethodRef();
        var methodContext = context.ResolveContextForMethod(methodRef);
        var baseMethod = methodRef.BaseMethod;
        var methodName = methodContext?.FullNameWithSignature ?? methodRef.ToString();
        var methodRva = methodContext?.Rva ?? baseMethod.Rva;
        return new MetadataUsageDetails(
            "MethodInfo",
            methodName,
            display ?? methodName,
            baseMethod.DeclaringType?.FullName,
            Hex(baseMethod.token),
            baseMethod.DeclaringType?.TypeIndex.Value ?? -1,
            methodName,
            methodRva,
            null,
            -1);
    }

    private static MetadataUsageDetails DescribeStringUsage(MetadataUsage usage, string? display)
    {
        var value = usage.AsLiteral();
        return new MetadataUsageDetails(
            "StringLiteral",
            value,
            display ?? value,
            null,
            null,
            -1,
            null,
            null,
            value,
            (int)usage.RawValue);
    }

    private static bool TryGetRva(ulong address, out ulong rva)
    {
        rva = 0;
        if (address == 0 || LibCpp2IlMain.Binary == null)
            return false;

        try
        {
            rva = LibCpp2IlMain.Binary.GetRva(address);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ulong ParseSortableHex(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : ulong.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string? SafeToString(Func<object?> valueFactory)
    {
        try
        {
            return valueFactory()?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            Enum e => e.ToString(),
            IConvertible convertible => NormalizeConvertible(convertible),
            byte[] bytes => bytes,
            _ => value.ToString(),
        };
    }

    private static object NormalizeConvertible(IConvertible convertible)
    {
        return convertible switch
        {
            string s => s,
            bool b => b,
            char c => c.ToString(),
            sbyte sb => sb,
            byte b => b,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => ui,
            long l => l,
            ulong ul => ul.ToString(CultureInfo.InvariantCulture),
            float f => float.IsFinite(f) ? f : f.ToString(CultureInfo.InvariantCulture),
            double d => double.IsFinite(d) ? d : d.ToString(CultureInfo.InvariantCulture),
            decimal m => m,
            _ => convertible.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static string Hex(ulong value) => $"0x{value:X}";

    private static string Hex(uint value) => $"0x{value:X}";

    private static string? BytesToHex(byte[]? bytes)
    {
        return bytes == null || bytes.Length == 0 ? null : Convert.ToHexString(bytes);
    }

    private sealed record AddressableMetadataExport(
        IReadOnlyList<AddressableStringLiteralInfo> Strings,
        IReadOnlyList<MetadataGlobalInfo> Globals,
        IReadOnlyList<MetadataMethodInfo> Methods);

    private sealed record MetadataUsageDetails(
        string Kind,
        string DisplayName,
        string? Display,
        string? TypeName,
        string? Token,
        int TypeDefIndex,
        string? MethodName,
        ulong? MethodRva,
        string? StringValue,
        int StringIndex);
}
