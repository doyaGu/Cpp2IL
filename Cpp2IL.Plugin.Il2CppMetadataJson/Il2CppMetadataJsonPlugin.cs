using Cpp2IL.Core.Api;
using Cpp2IL.Core.Attributes;

[assembly: RegisterCpp2IlPlugin(typeof(Cpp2IL.Plugin.Il2CppMetadataJson.Il2CppMetadataJsonPlugin))]

namespace Cpp2IL.Plugin.Il2CppMetadataJson;

public class Il2CppMetadataJsonPlugin : Cpp2IlPlugin
{
    public override string Name => "IL2CPP Metadata JSON Plugin";

    public override string Description => "Adds a public JSON output format for comprehensive IL2CPP metadata export";

    public override void OnLoad()
    {
        OutputFormatRegistry.Register<Il2CppMetadataJsonOutputFormat>();
        Logger.VerboseNewline("IL2CPP Metadata JSON Plugin loaded and output format registered.");
    }
}
