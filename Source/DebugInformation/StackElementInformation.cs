using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public struct StackElementInformation
{
    public StackElementKind Kind;
    public GeneralType Type;
    public string Identifier;

    public int Address;
    public bool BasePointerRelative;
    public int Size;

    public readonly int AbsoluteAddress(int basePointer, int absoluteOffset)
    {
        if (BasePointerRelative) return Address + basePointer;
        else return Address + absoluteOffset;
    }

    public readonly Range<int> GetRange(int basePointer, int absoluteOffset)
    {
        int itemStart = AbsoluteAddress(basePointer, absoluteOffset);
        int itemEnd = itemStart + Size;
        return new Range<int>(itemStart, itemEnd);
    }
}
