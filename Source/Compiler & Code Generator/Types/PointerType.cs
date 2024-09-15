using LanguageCore.Runtime;
using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class PointerType : GeneralType,
    IEquatable<PointerType>
{
    public GeneralType To { get; }

    public PointerType(PointerType other)
    {
        To = other.To;
    }

    public PointerType(GeneralType to)
    {
        To = to;
    }

    public override int GetSize(IRuntimeInfoProvider runtime) => runtime.PointerSize;
    public override BitWidth GetBitWidth(IRuntimeInfoProvider runtime) => (BitWidth)runtime.PointerSize;

    public override bool Equals(object? other) => Equals(other as PointerType);
    public override bool Equals(GeneralType? other) => Equals(other as PointerType);
    public bool Equals(PointerType? other)
    {
        if (other is null) return false;
        if (!To.Equals(other.To)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstancePointer otherPointer) return false;
        if (!To.Equals(otherPointer.To)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(To);
    public override string ToString() => $"{To}*";
}
