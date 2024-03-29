namespace LanguageCore.Compiler;

using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class GenericType : GeneralType,
    IEquatable<GenericType>
{
    public string Identifier { get; }

    public override int Size
    {
        [DoesNotReturn]
        get => throw new InternalException($"Can not get the size of a generic type");
    }

    public GenericType(GenericType other)
    {
        Identifier = other.Identifier;
    }

    public GenericType(string identifier)
    {
        Identifier = identifier;
    }

    public override bool Equals(object? other) => Equals(other as GenericType);
    public override bool Equals(GeneralType? other) => Equals(other as GenericType);
    public bool Equals(GenericType? other)
    {
        if (other is null) return false;
        if (!Identifier.Equals(other.Identifier)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;

        if (other is not TypeInstanceSimple otherSimple)
        { return false; }

        if (TypeKeywords.BasicTypes.ContainsKey(otherSimple.Identifier.Content))
        { return false; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Identifier);
    public override string ToString() => Identifier;

    public override TypeInstance ToTypeInstance() => TypeInstanceSimple.CreateAnonymous(Identifier);
}
