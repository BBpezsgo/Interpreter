namespace LanguageCore.Compiler;

public readonly struct CleanupItem
{
    public readonly bool ShouldDeallocate;
    public readonly GeneralType? Type;

    public static CleanupItem Null => new(false, null);

    public CleanupItem(bool shouldDeallocate, GeneralType? type)
    {
        ShouldDeallocate = shouldDeallocate;
        Type = type;
    }
}
