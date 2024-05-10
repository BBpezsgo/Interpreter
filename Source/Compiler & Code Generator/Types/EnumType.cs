﻿namespace LanguageCore.Compiler;

using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class EnumType : GeneralType,
    IEquatable<EnumType>
{
    public CompiledEnum Enum { get; }

    public override int Size => 1;

    public EnumType(EnumType other)
    {
        Enum = other.Enum;
    }

    public EnumType(CompiledEnum @enum)
    {
        Enum = @enum;
    }

    public override bool Equals(object? other) => Equals(other as EnumType);
    public override bool Equals(GeneralType? other) => Equals(other as EnumType);
    public bool Equals(EnumType? other)
    {
        if (other is null) return false;
        if (!object.ReferenceEquals(Enum, other.Enum)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;

        if (other is not TypeInstanceSimple otherSimple)
        { return false; }

        if (TypeKeywords.BasicTypes.ContainsKey(otherSimple.Identifier.Content))
        { return false; }

        if (Enum.Identifier.Content == otherSimple.Identifier.Content)
        { return true; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Enum);
    public override string ToString() => Enum.Identifier.Content;

    public override TypeInstance ToTypeInstance() => TypeInstanceSimple.CreateAnonymous(Enum.Identifier.Content);
}