using LanguageCore.Compiler;
using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class AttributeUsage :
    IPositioned,
    IIdentifiable<Token>,
    IInFile,
    ILocated
{
    public Token Identifier { get; }
    public ImmutableArray<Literal> Parameters { get; }
    public Uri File { get; }

    public Position Position =>
        new Position(Parameters.As<IPositioned>().Or(Identifier))
        .Union(Identifier);

    public Location Location => new(Position, File);

    public AttributeUsage(Token identifier, IEnumerable<Literal> parameters, Uri file)
    {
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
        File = file;
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

    public bool TryGetValue<T0>([NotNullWhen(true)] out T0? param0)
    {
        param0 = default;
        if (Parameters.Length != 1) return false;
        return TryGetValue(0, out param0);
    }

    public bool TryGetValue<T0, T1>(
        [NotNullWhen(true)] out T0? param0,
        [NotNullWhen(true)] out T1? param1)
    {
        param0 = default;
        param1 = default;
        if (Parameters.Length != 2) return false;
        return
            TryGetValue(0, out param0) &&
            TryGetValue(1, out param1);
    }

    public bool TryGetValue<T0, T1, T2>(
        [NotNullWhen(true)] out T0? param0,
        [NotNullWhen(true)] out T1? param1,
        [NotNullWhen(true)] out T2? param2)
    {
        param0 = default;
        param1 = default;
        param2 = default;
        if (Parameters.Length != 3) return false;
        return
            TryGetValue(0, out param0) &&
            TryGetValue(1, out param1) &&
            TryGetValue(2, out param2);
    }
}
