using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

static class Stringify
{
    public const int CozyLength = 30;
}

public readonly struct TokenPair :
    IPositioned
{
    public Token Start { get; }
    public Token End { get; }

    public Position Position => new(Start, End);

    public TokenPair(Token start, Token end)
    {
        Start = start;
        End = end;
    }

    public static TokenPair CreateAnonymous(Position surround, string start, string end) => new(
        Token.CreateAnonymous(start, TokenType.Operator, surround.Before()),
        Token.CreateAnonymous(end, TokenType.Operator, surround.Before())
    );

    public static TokenPair CreateAnonymous(string start, string end) => new(
        Token.CreateAnonymous(start, TokenType.Operator),
        Token.CreateAnonymous(end, TokenType.Operator)
    );
}

public static class StatementExtensions
{
    public static Statement? GetStatementAt(this ParserResult parserResult, SinglePosition position)
        => parserResult.GetStatementsRecursively()
        .LastOrDefault(statement => statement.Position.Range.Contains(position));

    public static Statement? GetStatement(this ParserResult parserResult, Func<Statement, bool> condition)
        => parserResult.GetStatementsRecursively()
        .LastOrDefault(condition);

    public static T? GetStatement<T>(this Statement statement, Func<T, bool> condition)
        => statement.GetStatementsRecursively(true)
        .OfType<T>()
        .LastOrDefault(condition);

    public static bool GetStatementAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out Statement? statement)
        => (statement = GetStatement(parserResult, statement => statement.Position.Range.Contains(position))) is not null;

    public static bool GetStatement<T>(this Statement statement, [NotNullWhen(true)] out T? result, Func<T, bool> condition)
        => (result = GetStatement(statement, condition)) is not null;
}

public abstract class Statement : IPositioned, IInFile, ILocated
{
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public Token? Semicolon { get; internal set; }
    public abstract Position Position { get; }
    public Uri File { get; }

    public Location Location => new(Position, File);

    protected Statement(Uri file)
    {
        Semicolon = null;
        File = file;
    }

    protected Statement(Statement other)
    {
        Semicolon = other.Semicolon;
        File = other.File;
    }

    public override string ToString()
        => $"{GetType().Name}{Semicolon}";

    public abstract IEnumerable<Statement> GetStatementsRecursively(bool includeThis);
}

public abstract class AnyAssignment : Statement
{
    protected AnyAssignment(Uri file) : base(file)
    {

    }

    public abstract Assignment ToAssignment();
}

public abstract class StatementWithValue : Statement
{
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public bool SaveValue { get; internal set; } = true;

    /// <summary>
    /// Set by the compiler
    /// </summary>
    public GeneralType? CompiledType { get; internal set; }
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledValue? PredictedValue { get; internal set; }
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public TokenPair? SurroundingBracelet { get; internal set; }

    protected StatementWithValue(Uri file) : base(file)
    {
        SaveValue = true;
        CompiledType = null;
        PredictedValue = null;
        SurroundingBracelet = null;
    }
}

public abstract class StatementWithBlock : Statement
{
    public Block Block { get; }

    protected StatementWithBlock(Block block, Uri file) : base(file)
    {
        Block = block;
    }
}

public abstract class StatementWithAnyBlock : Statement
{
    public Statement Block { get; }

    protected StatementWithAnyBlock(Statement block, Uri file) : base(file)
    {
        Block = block;
    }
}

public class Block : Statement
{
    public ImmutableArray<Statement> Statements { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(Brackets);

    public Block(IEnumerable<Statement> statements, TokenPair brackets, Uri file) : base(file)
    {
        Statements = statements.ToImmutableArray();
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Brackets.Start);

        if (Statements.Length == 0)
        { result.Append(' '); }
        else if (Statements.Length == 1)
        {
            result.Append(' ');
            result.Append(Statements[0]);
            result.Append(' ');
        }
        else
        { result.Append("..."); }

        result.Append(Brackets.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Statements.Length; i++)
        {
            foreach (Statement statement in Statements[i].GetStatementsRecursively(true))
            { yield return statement; }
        }
    }

    public static Block CreateIfNotBlock(Statement statement)
    {
        if (statement is Block block) return block;
        return new Block(
            new Statement[] { statement },
            TokenPair.CreateAnonymous(statement.Position, "{", "}"),
            statement.File
        );
    }
}

public abstract class LinkedIfThing : StatementWithAnyBlock
{
    public Token KeywordToken { get; }

    public Identifier Keyword => new(KeywordToken, File);

    protected LinkedIfThing(Token keyword, Statement block, Uri file) : base(block, file)
    {
        KeywordToken = keyword;
    }
}

public class LinkedIf : LinkedIfThing
{
    public StatementWithValue Condition { get; }
    public LinkedIfThing? NextLink { get; init; }

    public override Position Position => new(KeywordToken, Condition, Block);

    public LinkedIf(Token keyword, StatementWithValue condition, Statement block, Uri file) : base(keyword, block, file)
    {
        Condition = condition;
    }

    public override string ToString()
        => $"{KeywordToken} ({Condition}) {Block}{(NextLink != null ? " ..." : string.Empty)}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }

        if (NextLink != null)
        {
            foreach (Statement statement in NextLink.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }
}

public class LinkedElse : LinkedIfThing
{
    public override Position Position => new(KeywordToken, Block);

    public LinkedElse(Token keyword, Statement block, Uri file) : base(keyword, block, file)
    { }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class LiteralList : StatementWithValue
{
    public TokenPair Brackets { get; }
    public ImmutableArray<StatementWithValue> Values { get; }

    public override Position Position => new(Brackets);

    public LiteralList(IEnumerable<StatementWithValue> values, TokenPair brackets, Uri file) : base(file)
    {
        Brackets = brackets;
        Values = values.ToImmutableArray();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Values.Length; i++)
        {
            foreach (Statement statement in Values[i].GetStatementsRecursively(true))
            { yield return statement; }
        }
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBracelet?.Start);
        result.Append(Brackets.Start);

        if (Values.Length == 0)
        {
            result.Append(' ');
        }
        else
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= Stringify.CozyLength)
                { result.Append("..."); break; }

                result.Append(Values[i]);
            }
        }
        result.Append(Brackets.End);
        result.Append(SurroundingBracelet?.End);

        if (Semicolon != null) result.Append(Semicolon);

        return result.ToString();
    }
}

public class VariableDeclaration : Statement,
    IHaveType,
    IExportable,
    IIdentifiable<Identifier>,
    IHaveAttributes
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public GeneralType? CompiledType { get; set; }

    public ImmutableArray<AttributeUsage> Attributes { get; }
    public TypeInstance Type { get; }
    public Identifier Identifier { get; }
    public StatementWithValue? InitialValue { get; }
    public ImmutableArray<Token> Modifiers { get; }

    public string? ExternalConstantName => Attributes.TryGetAttribute(AttributeConstants.ExternalIdentifier, out AttributeUsage? attribute) && attribute.TryGetValue(out string? name) ? name : null;

    public override Position Position =>
        new Position(Type, Identifier, InitialValue)
        .Union(Modifiers);
    public bool IsExported => Modifiers.Contains(ProtectionKeywords.Export);

    public VariableDeclaration(VariableDeclaration other) : base(other)
    {
        Attributes = other.Attributes;
        Type = other.Type;
        Identifier = other.Identifier;
        InitialValue = other.InitialValue;
        Modifiers = other.Modifiers;
        CompiledType = other.CompiledType;
    }

    public VariableDeclaration(
        IEnumerable<AttributeUsage> attributes,
        IEnumerable<Token> modifiers,
        TypeInstance type,
        Identifier variableName,
        StatementWithValue? initialValue,
        Uri file) : base(file)
    {
        Attributes = attributes.ToImmutableArray();
        Type = type;
        Identifier = variableName;
        InitialValue = initialValue;
        Modifiers = modifiers.ToImmutableArray();
    }

    public override string ToString()
        => $"{string.Join(' ', Modifiers)} {Type} {Identifier}{((InitialValue != null) ? " = ..." : string.Empty)}{Semicolon}".TrimStart();

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        if (InitialValue != null)
        {
            foreach (Statement statement in InitialValue.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }
}

public class TypeStatement : StatementWithValue
{
    public Token Keyword { get; }
    public TypeInstance Type { get; }

    public override Position Position => new(Keyword, Type);

    public TypeStatement(Token keyword, TypeInstance type, Uri file) : base(file)
    {
        Keyword = keyword;
        Type = type;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBracelet?.Start);

        result.Append(Type);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);

        return result.ToString();
    }
    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class CompiledTypeStatement : StatementWithValue
{
    public Token Keyword { get; }
    public GeneralType Type { get; }

    public override Position Position => new(Keyword);

    public CompiledTypeStatement(Token keyword, GeneralType type, Uri file) : base(file)
    {
        Keyword = keyword;
        Type = type;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBracelet?.Start);

        result.Append(Type);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);

        return result.ToString();
    }
    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class AnyCall : StatementWithValue, IReadable, IReferenceableTo<CompiledFunctionDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledFunctionDefinition? Reference { get; set; }

    public StatementWithValue PrevStatement { get; }
    public TokenPair Brackets { get; }
    public ImmutableArray<StatementWithValue> Arguments { get; }
    public ImmutableArray<Token> Commas { get; }

    public override Position Position => new(PrevStatement, Brackets);

    public AnyCall(
        StatementWithValue prevStatement,
        IEnumerable<StatementWithValue> parameters,
        IEnumerable<Token> commas,
        TokenPair brackets,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Arguments = parameters.ToImmutableArray();
        Commas = commas.ToImmutableArray();
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBracelet?.Start);

        result.Append(PrevStatement);
        result.Append(Brackets.Start);

        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0)
            { result.Append(", "); }

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Arguments[i]);
        }
        result.Append(Brackets.End);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();
        result.Append("...");
        result.Append('(');
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) { result.Append(", "); }
            result.Append(typeSearch.Invoke(Arguments[i]).ToString());
        }
        result.Append(')');
        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (StatementWithValue argument in Arguments)
        {
            foreach (Statement statement in argument.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }
}

public class FunctionCall : StatementWithValue, IReadable, IReferenceableTo<CompiledFunctionDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledFunctionDefinition? Reference { get; set; }

    public Identifier Identifier { get; }
    public ImmutableArray<StatementWithValue> Arguments { get; }
    public StatementWithValue? PrevStatement { get; }
    public TokenPair Brackets { get; }

    public bool IsMethodCall => PrevStatement != null;
    public ImmutableArray<StatementWithValue> MethodArguments
    {
        get
        {
            if (PrevStatement == null) return Arguments;
            return Arguments.Insert(0, PrevStatement);
        }
    }
    public override Position Position =>
        new Position(Brackets, Identifier)
        .Union(MethodArguments);

    public FunctionCall(
        StatementWithValue? prevStatement,
        Identifier identifier,
        IEnumerable<StatementWithValue> arguments,
        TokenPair brackets,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Identifier = identifier;
        Arguments = arguments.ToImmutableArray();
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBracelet?.Start);

        if (PrevStatement != null)
        {
            result.Append(PrevStatement);
            result.Append('.');
        }
        result.Append(Identifier);
        result.Append(Brackets.Start);
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(Arguments[i]);

            if (result.Length >= 10 && i + 1 != Arguments.Length)
            {
                result.Append(", ...");
                break;
            }
        }
        result.Append(Brackets.End);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();
        if (PrevStatement != null)
        {
            result.Append(typeSearch.Invoke(PrevStatement).ToString());
            result.Append('.');
        }
        result.Append(Identifier.ToString());
        result.Append('(');
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(typeSearch.Invoke(Arguments[i]).ToString());
        }
        result.Append(')');
        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        if (PrevStatement != null)
        {
            foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
            { yield return statement; }
        }

        foreach (StatementWithValue argument in Arguments)
        {
            foreach (Statement statement in argument.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }
}

public class KeywordCall : Statement, IReadable
{
    public Token IdentifierToken { get; }
    public ImmutableArray<StatementWithValue> Arguments { get; }

    public Identifier Identifier => new(IdentifierToken, File);

    public override Position Position =>
        new Position(IdentifierToken)
        .Union(Arguments);

    public KeywordCall(
        Token identifier,
        IEnumerable<StatementWithValue> arguments,
        Uri file) : base(file)
    {
        IdentifierToken = identifier;
        Arguments = arguments.ToImmutableArray();
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(IdentifierToken);

        if (Arguments.Length > 0)
        {
            result.Append(' ');
            for (int i = 0; i < Arguments.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= Stringify.CozyLength)
                { result.Append("..."); break; }

                result.Append(Arguments[i]);
            }
        }

        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();
        result.Append(IdentifierToken.Content);
        result.Append('(');
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(typeSearch.Invoke(Arguments[i]));
        }
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (StatementWithValue argument in Arguments)
        {
            foreach (Statement statement in argument.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }
}

public class BinaryOperatorCall : StatementWithValue, IReadable, IReferenceableTo<CompiledOperatorDefinition>
{
    public const int ParameterCount = 2;

    #region Operators

    public const string BitshiftLeft = "<<";
    public const string BitshiftRight = ">>";
    public const string Addition = "+";
    public const string Subtraction = "-";
    public const string Multiplication = "*";
    public const string Division = "/";
    public const string Modulo = "%";
    public const string BitwiseAND = "&";
    public const string BitwiseOR = "|";
    public const string BitwiseXOR = "^";
    public const string CompLT = "<";
    public const string CompGT = ">";
    public const string CompGEQ = ">=";
    public const string CompLEQ = "<=";
    public const string CompNEQ = "!=";
    public const string CompEQ = "==";
    public const string LogicalAND = "&&";
    public const string LogicalOR = "||";

    #endregion

    public Token Operator { get; }
    public StatementWithValue Left { get; }
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public StatementWithValue Right { get; set; }
    public CompiledOperatorDefinition? Reference { get; set; }

    public override Position Position => new(Operator, Left, Right);
    public ImmutableArray<StatementWithValue> Arguments => ImmutableArray.Create(Left, Right);

    public BinaryOperatorCall(
        Token op,
        StatementWithValue left,
        StatementWithValue right,
        Uri file) : base(file)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBracelet?.Start);

        if (Left.ToString().Length < Stringify.CozyLength)
        { result.Append(Left); }
        else
        { result.Append("..."); }

        result.Append(' ');
        result.Append(Operator);
        result.Append(' ');

        if (Right.ToString().Length < Stringify.CozyLength)
        { result.Append(Right); }
        else
        { result.Append("..."); }

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();

        result.Append(Operator.Content);
        result.Append('(');
        result.Append(typeSearch.Invoke(Left));
        result.Append(", ");
        result.Append(typeSearch.Invoke(Right));
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Left.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Right.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class UnaryOperatorCall : StatementWithValue, IReadable, IReferenceableTo<CompiledOperatorDefinition>
{
    public const int ParameterCount = 1;

    #region Operators

    public const string LogicalNOT = "!";
    public const string BinaryNOT = "~";
    public const string UnaryPlus = "+";
    public const string UnaryMinus = "-";

    #endregion

    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledOperatorDefinition? Reference { get; set; }

    public Token Operator { get; }
    public StatementWithValue Left { get; }

    public override Position Position => new(Operator, Left);
    public ImmutableArray<StatementWithValue> Arguments => ImmutableArray.Create(Left);

    public UnaryOperatorCall(
        Token op,
        StatementWithValue left,
        Uri file) : base(file)
    {
        Operator = op;
        Left = left;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBracelet?.Start);

        if (Left.ToString().Length < Stringify.CozyLength)
        { result.Append(Left); }
        else
        { result.Append("..."); }

        result.Append(' ');

        result.Append(Operator);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();

        result.Append(Operator.Content);
        result.Append('(');
        result.Append(typeSearch.Invoke(Left));
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Left.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

/// <summary>
/// Increment and decrement operator
/// </summary>
public class ShortOperatorCall : AnyAssignment, IReadable, IReferenceableTo<CompiledOperatorDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledOperatorDefinition? Reference { get; set; }

    /// <summary>
    /// This should be "++" or "--"
    /// </summary>
    public Token Operator { get; }
    public StatementWithValue Left { get; }

    public ImmutableArray<StatementWithValue> Arguments => ImmutableArray.Create(Left);
    public override Position Position => new(Operator, Left);

    public ShortOperatorCall(
        Token op,
        StatementWithValue left,
        Uri file) : base(file)
    {
        Operator = op;
        Left = left;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (Left != null)
        {
            if (Left.ToString().Length <= Stringify.CozyLength)
            { result.Append(Left); }
            else
            { result.Append("..."); }

            result.Append(' ');
            result.Append(Operator);
        }
        else
        { result.Append(Operator); }

        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();

        result.Append(Operator.Content);
        result.Append('(');
        result.Append(typeSearch.Invoke(Left));
        result.Append(')');

        return result.ToString();
    }

    public override Assignment ToAssignment()
    {
        BinaryOperatorCall operatorCall = GetOperatorCall();
        Token assignmentToken = Token.CreateAnonymous("=", TokenType.Operator, Operator.Position);
        return new Assignment(assignmentToken, Left, operatorCall, File);
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Left.GetStatementsRecursively(true))
        { yield return statement; }
    }

    public BinaryOperatorCall GetOperatorCall()
    {
        switch (Operator.Content)
        {
            case "++":
            {
                Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position, File);
                return new BinaryOperatorCall(Token.CreateAnonymous("+", TokenType.Operator, Operator.Position), Left, one, File);
            }

            case "--":
            {
                Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position, File);
                return new BinaryOperatorCall(Token.CreateAnonymous("-", TokenType.Operator, Operator.Position), Left, one, File);
            }

            default: throw new NotImplementedException();
        }
    }
}

public class Assignment : AnyAssignment
{
    /// <summary>
    /// This should always be <c>"="</c>
    /// </summary>
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public StatementWithValue Right { get; }

    public override Position Position => new(Operator, Left, Right);

    public Assignment(
        Token @operator,
        StatementWithValue left,
        StatementWithValue right,
        Uri file) : base(file)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (result.Length + Left.ToString().Length > Stringify.CozyLength)
        {
            result.Append($"... {Operator} ...");
        }
        else
        {
            result.Append(Left);
            result.Append(' ');
            result.Append(Operator);
            result.Append(' ');
            if (result.Length + Right.ToString().Length > Stringify.CozyLength)
            { result.Append("..."); }
            else
            { result.Append(Right); }
        }

        result.Append(Semicolon);
        return result.ToString();
    }
    public override Assignment ToAssignment() => this;

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Left.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Right.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class CompoundAssignment : AnyAssignment, IReferenceableTo<CompiledOperatorDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledOperatorDefinition? Reference { get; set; }

    /// <summary>
    /// This should always starts with <c>"="</c>
    /// </summary>
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public StatementWithValue Right { get; }

    public override Position Position => new(Operator, Left, Right);

    public CompoundAssignment(
        Token @operator,
        StatementWithValue left,
        StatementWithValue right,
        Uri file) : base(file)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (result.Length + Left.ToString().Length > Stringify.CozyLength)
        {
            result.Append($"... {Operator} ...");
        }
        else
        {
            result.Append(Left);
            result.Append(' ');
            result.Append(Operator);
            result.Append(' ');
            if (result.Length + Right.ToString().Length > Stringify.CozyLength)
            { result.Append("..."); }
            else
            { result.Append(Right); }
        }

        result.Append(Semicolon);
        return result.ToString();
    }

    public override Assignment ToAssignment()
    {
        BinaryOperatorCall statementToAssign = GetOperatorCall();
        return new Assignment(Token.CreateAnonymous("=", TokenType.Operator, Operator.Position), Left, statementToAssign, File);
    }

    public BinaryOperatorCall GetOperatorCall() => new(
        Token.CreateAnonymous(Operator.Content.Replace("=", string.Empty, StringComparison.Ordinal), TokenType.Operator, Operator.Position),
        Left,
        Right,
        File);

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Left.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Right.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class Literal : StatementWithValue
{
    public LiteralType Type { get; }
    public string Value { get; }
    public Position ImaginaryPosition { get; init; }
    public Token ValueToken { get; }

    public override Position Position
        => ValueToken is null ? ImaginaryPosition : new Position(ValueToken);

    public Literal(
        LiteralType type,
        string value,
        Token valueToken,
        Uri file) : base(file)
    {
        Type = type;
        Value = value;
        ValueToken = valueToken;
    }

    public Literal(
        LiteralType type,
        Token value,
        Uri file) : base(file)
    {
        Type = type;
        Value = value.Content;
        ValueToken = value;
    }

    Literal(
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

    public static Literal CreateAnonymous(LiteralType type, string value, IPositioned position, Uri file)
        => Literal.CreateAnonymous(type, value, position.Position, file);

    public static Literal CreateAnonymous(LiteralType type, string value, Position position, Uri file)
    {
        TokenType tokenType = type switch
        {
            LiteralType.Integer => TokenType.LiteralNumber,
            LiteralType.Float => TokenType.LiteralFloat,
            LiteralType.String => TokenType.LiteralString,
            LiteralType.Char => TokenType.LiteralNumber,
            _ => TokenType.Identifier,
        };
        return new Literal(type, value, Token.CreateAnonymous(value, tokenType), file)
        {
            ImaginaryPosition = position,
        };
    }

    public static Literal CreateAnonymous(CompiledValue value, IPositioned position, Uri file)
        => Literal.CreateAnonymous(value, position.Position, file);

    public static Literal CreateAnonymous(CompiledValue value, Position position, Uri file)
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
        return new Literal(value, Token.CreateAnonymous(value.ToString(), tokenType), file)
        {
            ImaginaryPosition = position,
        };
    }

    public static IEnumerable<Literal> CreateAnonymous(IEnumerable<CompiledValue> values, IEnumerable<ILocated> positions)
        =>
        values
        .Zip(positions, (a, b) => (First: a, Second: b))
        .Select(item => Literal.CreateAnonymous(item.First, item.Second.Location.Position, item.Second.Location.File));

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBracelet?.Start);

        result.Append(Type switch
        {
            LiteralType.String => $"\"{Value.Escape()}\"",
            LiteralType.Char => $"'{Value.Escape()}'",
            _ => Value,
        });

        result.Append(SurroundingBracelet?.End);
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

    public bool TryConvert<T>([NotNullWhen(true)] out T? value)
    {
        value = default;

        LiteralType type;

        if (typeof(T) == typeof(int))
        { type = LiteralType.Integer; }
        else if (typeof(T) == typeof(float))
        { type = LiteralType.Float; }
        else if (typeof(T) == typeof(string))
        { type = LiteralType.String; }
        else
        { return false; }

        if (type != Type)
        { return false; }

        value = type switch
        {
            LiteralType.Integer => (T)(object)GetInt(),
            LiteralType.Float => (T)(object)GetFloat(),
            LiteralType.String => (T)(object)Value,
            LiteralType.Char => (T)(object)Value[0],
            _ => throw new UnreachableException(),
        };
        return true;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class Identifier : StatementWithValue, IReferenceableTo
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public object? Reference { get; set; }

    public Token Token { get; }

    public TokenAnalyzedType AnalyzedType
    {
        get => Token.AnalyzedType;
        set => Token.AnalyzedType = value;
    }
    public string Content => Token.Content;
    public override Position Position => Token.Position;

    public Identifier(
        Token token,
        Uri file) : base(file)
    {
        Token = token;
    }

    public override string ToString() => Token.Content;

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class InstructionLabel : Statement
{
    public Identifier Identifier { get; }
    public Token Colon { get; }

    public override Position Position => new(
        Identifier,
        Colon
    );

    public InstructionLabel(
        Identifier identifier,
        Token colon,
        Uri file) : base(file)
    {
        Identifier = identifier;
        Colon = colon;
    }

    public InstructionLabel(InstructionLabel other) : base(other.File)
    {
        Identifier = other.Identifier;
        Colon = other.Colon;
    }

    public override string ToString() => $"{Identifier}{Colon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class AddressGetter : StatementWithValue
{
    public Token Operator { get; }
    public StatementWithValue PrevStatement { get; }

    public override Position Position => new(Operator, PrevStatement);

    public AddressGetter(
        Token operatorToken,
        StatementWithValue prevStatement,
        Uri file) : base(file)
    {
        Operator = operatorToken;
        PrevStatement = prevStatement;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Operator}{PrevStatement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class Pointer : StatementWithValue
{
    public Token Operator { get; }
    public StatementWithValue PrevStatement { get; }

    public override Position Position => new(Operator, PrevStatement);

    public Pointer(
        Token operatorToken,
        StatementWithValue prevStatement,
        Uri file) : base(file)
    {
        Operator = operatorToken;
        PrevStatement = prevStatement;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Operator}{PrevStatement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class WhileLoop : StatementWithAnyBlock
{
    public Token KeywordToken { get; }
    public StatementWithValue Condition { get; }

    public Identifier Keyword => new(KeywordToken, File);
    public override Position Position => new(KeywordToken, Block);

    public WhileLoop(
        Token keyword,
        StatementWithValue condition,
        Statement block,
        Uri file) : base(block, file)
    {
        KeywordToken = keyword;
        Condition = condition;
    }

    public override string ToString()
        => $"{KeywordToken} ({Condition}) {Block}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class ForLoop : StatementWithBlock
{
    public Token KeywordToken { get; }
    public VariableDeclaration VariableDeclaration { get; }
    public StatementWithValue Condition { get; }
    public AnyAssignment Expression { get; }

    public Identifier Identifier => new(KeywordToken, File);
    public override Position Position => new(KeywordToken, Block);

    public ForLoop(
        Token keyword,
        VariableDeclaration variableDeclaration,
        StatementWithValue condition,
        AnyAssignment expression,
        Block block,
        Uri file)
        : base(block, file)
    {
        KeywordToken = keyword;
        VariableDeclaration = variableDeclaration;
        Condition = condition;
        Expression = expression;
    }

    public override string ToString()
        => $"{KeywordToken} (...) {Block}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in VariableDeclaration.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Condition.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Expression.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class IfContainer : Statement
{
    public ImmutableArray<BaseBranch> Branches { get; }

    public override Position Position => new(Branches);

    public IfContainer(
        IEnumerable<BaseBranch> parts,
        Uri file) : base(file)
    {
        Branches = parts.ToImmutableArray();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (BaseBranch branch in Branches)
        {
            foreach (Statement statement in branch.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }

    public override string ToString()
    {
        if (Branches.Length == 0) return "null";
        return Branches[0].ToString();
    }
}

public abstract class BaseBranch : StatementWithAnyBlock
{
    public Token Keyword { get; }
    public IfPart Type { get; }

    public override Position Position => new(Keyword, Block);

    protected BaseBranch(
        Token keyword,
        IfPart type,
        Statement block,
        Uri file)
        : base(block, file)
    {
        Keyword = keyword;
        Type = type;
    }

    public enum IfPart
    {
        If,
        Else,
        ElseIf,
    }

    public override string ToString()
        => $"{Keyword}{((Type != IfPart.Else) ? " (...)" : "")} {Block}{Semicolon}";
}

public class IfBranch : BaseBranch
{
    public StatementWithValue Condition { get; }

    public IfBranch(
        Token keyword,
        StatementWithValue condition,
        Statement block,
        Uri file)
        : base(keyword, IfPart.If, block, file)
    {
        Condition = condition;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class ElseIfBranch : BaseBranch
{
    public StatementWithValue Condition { get; }

    public ElseIfBranch(
        Token keyword,
        StatementWithValue condition,
        Statement block,
        Uri file)
        : base(keyword, IfPart.ElseIf, block, file)
    {
        Condition = condition;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Condition.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class ElseBranch : BaseBranch
{
    public ElseBranch(
        Token keyword,
        Statement block,
        Uri file)
        : base(keyword, IfPart.Else, block, file)
    { }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Block.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class NewInstance : StatementWithValue, IHaveType, IInFile
{
    public Token KeywordToken { get; }
    public TypeInstance Type { get; }

    public Identifier Keyword => new(KeywordToken, File);
    public override Position Position => new(KeywordToken, Type);

    public NewInstance(
        Token keyword,
        TypeInstance typeName,
        Uri file) : base(file)
    {
        KeywordToken = keyword;
        Type = typeName;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{KeywordToken} {Type}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class ConstructorCall : StatementWithValue, IReadable, IReferenceableTo<CompiledConstructorDefinition>, IHaveType
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledConstructorDefinition? Reference { get; set; }

    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public ImmutableArray<StatementWithValue> Arguments { get; }
    public TokenPair Brackets { get; }

    public override Position Position =>
        new Position(Keyword, Type, Brackets)
        .Union(Arguments);

    public ConstructorCall(
        Token keyword,
        TypeInstance typeName,
        IEnumerable<StatementWithValue> arguments,
        TokenPair brackets,
        Uri file) : base(file)
    {
        Keyword = keyword;
        Type = typeName;
        Arguments = arguments.ToImmutableArray();
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBracelet?.Start);

        result.Append(Keyword);
        result.Append(' ');
        result.Append(Type);
        result.Append(Brackets.Start);

        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Arguments[i]);
        }

        result.Append(Brackets.End);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();
        result.Append(Type.ToString());
        result.Append('.');
        result.Append(Keyword.Content);
        result.Append(Brackets.Start);
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(typeSearch.Invoke(Arguments[i]));
        }
        result.Append(Brackets.End);

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (StatementWithValue argument in Arguments)
        {
            foreach (Statement statement in argument.GetStatementsRecursively(true))
            { yield return statement; }
        }
    }
}

public class IndexCall : StatementWithValue, IReadable, IReferenceableTo<CompiledGeneralFunctionDefinition>
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledGeneralFunctionDefinition? Reference { get; set; }

    public StatementWithValue PrevStatement { get; }
    public StatementWithValue Index { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(PrevStatement, Index);

    public IndexCall(
        StatementWithValue prevStatement,
        StatementWithValue indexStatement,
        TokenPair brackets,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Index = indexStatement;
        Brackets = brackets;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{PrevStatement}{Brackets.Start}{Index}{Brackets.End}{SurroundingBracelet?.End}{Semicolon}";

    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch)
    {
        StringBuilder result = new();

        if (PrevStatement != null)
        { result.Append(typeSearch.Invoke(PrevStatement)); }
        else
        { result.Append('?'); }

        result.Append(Brackets.Start);
        result.Append(typeSearch.Invoke(Index));
        result.Append(Brackets.End);

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }

        foreach (Statement statement in Index.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class Field : StatementWithValue, IReferenceableTo
{
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public object? Reference { get; set; }

    public Identifier Identifier { get; }
    public StatementWithValue PrevStatement { get; }

    public override Position Position => new(PrevStatement, Identifier);

    public Field(
        StatementWithValue prevStatement,
        Identifier identifier,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Identifier = identifier;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{PrevStatement}.{Identifier}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class BasicTypeCast : StatementWithValue, IHaveType
{
    public StatementWithValue PrevStatement { get; }
    public Token Keyword { get; }
    public TypeInstance Type { get; }

    public override Position Position => new(PrevStatement, Keyword, Type);

    public BasicTypeCast(
        StatementWithValue prevStatement,
        Token keyword,
        TypeInstance type,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Keyword = keyword;
        Type = type;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{PrevStatement} {Keyword} {Type}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class ManagedTypeCast : StatementWithValue, IHaveType
{
    public StatementWithValue PrevStatement { get; }
    public TypeInstance Type { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(PrevStatement, Type);

    public ManagedTypeCast(
        StatementWithValue prevStatement,
        TypeInstance type,
        TokenPair brackets,
        Uri file) : base(file)
    {
        PrevStatement = prevStatement;
        Type = type;
        Brackets = brackets;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}({Type}){PrevStatement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in PrevStatement.GetStatementsRecursively(true))
        { yield return statement; }
    }
}

public class ModifiedStatement : StatementWithValue
{
    public StatementWithValue Statement { get; }
    public Token Modifier { get; }

    public override Position Position
        => new(Modifier, Statement);

    public ModifiedStatement(
        Token modifier,
        StatementWithValue statement,
        Uri file) : base(file)
    {
        Statement = statement;
        Modifier = modifier;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Modifier} {Statement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement statement in Statement.GetStatementsRecursively(true))
        { yield return statement; }
    }
}
