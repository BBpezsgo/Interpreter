namespace LanguageCore.Parser;

using Compiler;
using Statement;
using Tokenizing;

public class AttributeUsage :
    IPositioned,
    IIdentifiable<Token>
{
    public Token Identifier { get; }
    public ImmutableArray<Literal> Parameters { get; }

    public Position Position =>
        new Position(Parameters)
        .Union(Identifier);

    public AttributeUsage(Token identifier, IEnumerable<Literal> parameters)
    {
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
    }

    public bool TryGetValue<T>(int index, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (Parameters.Length <= index) return false;

        LiteralType literalType;
        Type type = typeof(T);
        if (type == typeof(int))
        { literalType = LiteralType.Integer; }
        else if (type == typeof(float))
        { literalType = LiteralType.Float; }
        else if (type == typeof(char))
        { literalType = LiteralType.Char; }
        else if (type == typeof(string))
        { literalType = LiteralType.String; }
        else
        { throw new NotImplementedException($"Unknown attribute type requested: \"{type.FullName}\""); }

        value = literalType switch
        {
            LiteralType.Integer => (T)(object)Parameters[index].GetInt(),
            LiteralType.Float => (T)(object)Parameters[index].GetFloat(),
            LiteralType.String => (T)(object)Parameters[index].Value,
            LiteralType.Char => (T)(object)Parameters[index].Value[0],
            _ => throw new UnreachableException(),
        };
        return true;
    }

    public bool TryGetValue(int index, out string value)
    {
        value = string.Empty;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].Type == LiteralType.String)
        {
            value = Parameters[index].Value;
        }
        return true;
    }

    public bool TryGetValue(int index, out int value)
    {
        value = 0;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].Type == LiteralType.Integer)
        {
            value = Parameters[index].GetInt();
        }
        return true;
    }

    public bool TryGetValue(int index, out float value)
    {
        value = 0;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].Type == LiteralType.Float)
        {
            value = Parameters[index].GetFloat();
        }
        return true;
    }

    public bool TryGetValue(int index, out char value)
    {
        value = default;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].Type == LiteralType.Char)
        {
            value = Parameters[index].Value[0];
        }
        return true;
    }
}
