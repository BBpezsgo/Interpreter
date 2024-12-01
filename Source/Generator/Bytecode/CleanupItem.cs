using LanguageCore.Compiler;

namespace LanguageCore.BBLang.Generator;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct CleanupItem
{
    public readonly int SizeOnStack;
    public readonly bool ShouldDeallocate;
    public readonly GeneralType? Type;

    public static CleanupItem Null => new(0, false, null);

    public CleanupItem(int size, bool shouldDeallocate, GeneralType? type)
    {
        SizeOnStack = size;
        ShouldDeallocate = shouldDeallocate;
        Type = type;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        if (Type is null && SizeOnStack == 0 && !ShouldDeallocate) return "null";
        return $"({(ShouldDeallocate ? "temp " : string.Empty)}{Type} : {SizeOnStack} bytes)";
    }
    string GetDebuggerDisplay() => ToString();
}
