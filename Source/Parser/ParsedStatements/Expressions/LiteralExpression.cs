using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class LiteralExpression : Expression
{
    public LiteralType Type { get; }
    public string Value { get; }
    public Position ImaginaryPosition { get; init; }
    public Token ValueToken { get; }

    public override Position Position
        => ValueToken is null ? ImaginaryPosition : new Position(ValueToken);

    public LiteralExpression(
        LiteralType type,
        string value,
        Token valueToken,
        Uri file) : base(file)
    {
        Type = type;
        Value = value;
        ValueToken = valueToken;
    }

    public LiteralExpression(
        LiteralType type,
        Token value,
        Uri file) : base(file)
    {
        Type = type;
        Value = value.Content;
        ValueToken = value;
    }

    LiteralExpression(
        CompiledValue value,
        Token token,
        Uri file) : base(file)
    {
        Type = value.Type switch
        {
            RuntimeType.U8 => LiteralType.Integer,
            RuntimeType.I8 => LiteralType.Integer,
            RuntimeType.U16 => LiteralType.Integer,
            RuntimeType.I16 => LiteralType.Integer,
            RuntimeType.U32 => LiteralType.Integer,
            RuntimeType.I32 => LiteralType.Integer,
            RuntimeType.F32 => LiteralType.Float,
            _ => throw new NotImplementedException(),
        };
        Value = value.ToString();
        ValueToken = token;
    }

    public static LiteralExpression CreateAnonymous(LiteralType type, string value, IPositioned position, Uri file)
        => CreateAnonymous(type, value, position.Position, file);

    public static LiteralExpression CreateAnonymous(LiteralType type, string value, Position position, Uri file)
    {
        TokenType tokenType = type switch
        {
            LiteralType.Integer => TokenType.LiteralNumber,
            LiteralType.Float => TokenType.LiteralFloat,
            LiteralType.String => TokenType.LiteralString,
            LiteralType.Char => TokenType.LiteralNumber,
            _ => TokenType.Identifier,
        };
        return new LiteralExpression(type, value, Token.CreateAnonymous(value, tokenType), file)
        {
            ImaginaryPosition = position,
        };
    }

    public static LiteralExpression CreateAnonymous(CompiledValue value, IPositioned position, Uri file)
        => CreateAnonymous(value, position.Position, file);

    public static LiteralExpression CreateAnonymous(CompiledValue value, ILocated location)
        => CreateAnonymous(value, location.Location.Position, location.Location.File);

    public static LiteralExpression CreateAnonymous(CompiledValue value, Position position, Uri file)
    {
        TokenType tokenType = value.Type switch
        {
            RuntimeType.U8 => TokenType.LiteralNumber,
            RuntimeType.I8 => TokenType.LiteralNumber,
            RuntimeType.U16 => TokenType.LiteralNumber,
            RuntimeType.I16 => TokenType.LiteralNumber,
            RuntimeType.U32 => TokenType.LiteralNumber,
            RuntimeType.I32 => TokenType.LiteralNumber,
            RuntimeType.F32 => TokenType.LiteralFloat,
            _ => TokenType.Identifier,
        };
        return new LiteralExpression(value, Token.CreateAnonymous(value.ToString(), tokenType), file)
        {
            ImaginaryPosition = position,
        };
    }

    public static IEnumerable<LiteralExpression> CreateAnonymous(IEnumerable<CompiledValue> values, IEnumerable<ILocated> positions)
        =>
        values
        .Zip(positions, (a, b) => (First: a, Second: b))
        .Select(item => CreateAnonymous(item.First, item.Second.Location.Position, item.Second.Location.File));

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBrackets?.Start);

        result.Append(Type switch
        {
            LiteralType.String => $"\"{Value.Escape()}\"",
            LiteralType.Char => $"'{Value.Escape()}'",
            _ => Value,
        });

        result.Append(SurroundingBrackets?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public int GetInt()
    {
        string value = Value;
        int @base = 10;
        if (value.StartsWith("0b", StringComparison.Ordinal))
        {
            value = value[2..];
            @base = 2;
        }
        if (value.StartsWith("0x", StringComparison.Ordinal))
        {
            value = value[2..];
            @base = 16;
        }
        value = value.Replace("_", string.Empty, StringComparison.Ordinal);
        return Convert.ToInt32(value, @base);
    }

    public float GetFloat()
    {
        string value = Value;
        value = value.Replace("_", string.Empty, StringComparison.Ordinal);
        value = value.EndsWith('f') ? value[..^1] : value;
        return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public override IEnumerable<Statement> GetStatementsRecursively(StatementWalkFlags flags)
    {
        if (flags.HasFlag(StatementWalkFlags.IncludeThis)) yield return this;
    }
}
