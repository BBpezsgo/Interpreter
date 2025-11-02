namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SourceCodeLocation
{
    public MutableRange<int> Instructions;
    public Location Location;

    public readonly bool Contains(int instruction) =>
        Instructions.Start <= instruction &&
        Instructions.End > instruction;

    public override readonly string ToString() => $"({Instructions} -> {Location.Position.ToStringRange()})";
    readonly string GetDebuggerDisplay() => ToString();
}
