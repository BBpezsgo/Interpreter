using LanguageCore.Runtime;
using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class AliasType : GeneralType,
    IEquatable<AliasType>
{
    public GeneralType Value { get; }
    public CompiledAlias Definition { get; }

    public override GeneralType FinalValue => Value is AliasType aliasType ? aliasType.FinalValue : Value;
    public string Identifier => Definition.Identifier.Content;

    public AliasType(GeneralType value, CompiledAlias definition)
    {
        Value = value;
        Definition = definition;
    }

    public override bool GetSize(IRuntimeInfoProvider runtime, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
        => Value.GetSize(runtime, out size, out error);

    public override bool GetBitWidth(IRuntimeInfoProvider runtime, out BitWidth bitWidth, [NotNullWhen(false)] out PossibleDiagnostic? error)
        => Value.GetBitWidth(runtime, out bitWidth, out error);

    public override bool Equals(object? other) => Equals(other as AliasType);
    public override bool Equals(GeneralType? other) => Equals(other as AliasType);
    public bool Equals(AliasType? other)
    {
        if (other is null) return false;
        if (!other.Identifier.Equals(Identifier)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstanceSimple otherSimple) return false;
        if (!Identifier.Equals(otherSimple.Identifier.Content)) return false;
        return true;
    }
    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Identifier;
}
