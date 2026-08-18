namespace Warden.Services.Theming.Structures;

/// <summary>Warden's default page shape, unchanged. Used when none is configured.</summary>
public sealed class DefaultStructure : IWardenStructure
{
    public string Name => "default";

    public string Label => "Default";

    public string ComponentCss => string.Empty;
}
