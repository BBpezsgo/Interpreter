namespace LanguageCore.Parser.Statement;

using Compiler;
using Runtime;
using Tokenizing;

struct Stringify
{
    public const int CozyLength = 30;
}

public interface IReferenceableTo : IReferenceableTo<object>;
public interface IReferenceableTo<T> where T : notnull
{
    public T? Reference { get; set; }
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
}

public static class StatementExtensions
{
    public static T? GetStatementAt<T>(this ParserResult parserResult, int absolutePosition)
        where T : IPositioned
        => StatementExtensions.GetStatement<T>(parserResult, statement => statement.Position.AbsoluteRange.Contains(absolutePosition));

    public static T? GetStatementAt<T>(this ParserResult parserResult, SinglePosition position)
        where T : IPositioned
        => StatementExtensions.GetStatement<T>(parserResult, statement => statement.Position.Range.Contains(position));

    public static Statement? GetStatementAt(this ParserResult parserResult, int absolutePosition)
        => StatementExtensions.GetStatement(parserResult, statement => statement.Position.AbsoluteRange.Contains(absolutePosition));

    public static Statement? GetStatementAt(this ParserResult parserResult, SinglePosition position)
        => StatementExtensions.GetStatement(parserResult, statement => statement.Position.Range.Contains(position));

    public static IEnumerable<T> GetStatements<T>(this ParserResult parserResult)
    {
        foreach (Statement statement in parserResult.GetStatementsRecursively())
        {
            if (statement is T _statement)
            { yield return _statement; }
        }
    }

    public static IEnumerable<T> GetStatements<T>(this ParserResult parserResult, Func<T, bool> condition)
    {
        foreach (Statement statement in parserResult.GetStatementsRecursively())
        {
            if (statement is T _statement && condition.Invoke(_statement))
            { yield return _statement; }
        }
    }

    public static IEnumerable<T> GetStatements<T>(this Statement statement)
    {
        foreach (Statement subStatement in statement.GetStatementsRecursively(true))
        {
            if (subStatement is T _subStatement)
            { yield return _subStatement; }
        }
    }

    public static IEnumerable<T> GetStatements<T>(this Statement statement, Func<T, bool> condition)
    {
        foreach (Statement subStatement in statement.GetStatementsRecursively(true))
        {
            if (subStatement is T _subStatement && condition.Invoke(_subStatement))
            { yield return _subStatement; }
        }
    }

    public static T? GetStatement<T>(this ParserResult parserResult)
    {
        foreach (Statement statement in parserResult.GetStatementsRecursively())
        {
            if (statement is T _statement)
            { return _statement; }
        }
        return default;
    }

    public static Statement? GetStatement(this ParserResult parserResult)
    {
        foreach (Statement statement in parserResult.GetStatementsRecursively())
        { return statement; }
        return default;
    }

    public static T? GetStatement<T>(this ParserResult parserResult, Func<T, bool> condition)
    {
        foreach (Statement statement in parserResult.GetStatementsRecursively())
        {
            if (statement is T statement_ && condition.Invoke(statement_))
            { return statement_; }
        }
        return default;
    }

    public static Statement? GetStatement(this ParserResult parserResult, Func<Statement, bool> condition)
    {
        foreach (Statement statement in parserResult.GetStatementsRecursively())
        {
            if (condition.Invoke(statement))
            { return statement; }
        }
        return default;
    }

    public static bool TryGetStatement<T>(this ParserResult parserResult, [NotNullWhen(true)] out T? result)
        => (result = GetStatement<T>(parserResult)) != null;

    public static bool TryGetStatement(this ParserResult parserResult, [NotNullWhen(true)] out Statement? result)
        => (result = GetStatement(parserResult)) != null;

    public static bool TryGetStatement<T>(this ParserResult parserResult, [NotNullWhen(true)] out T? result, Func<T, bool> condition)
        => (result = GetStatement<T>(parserResult, condition)) != null;

    public static T? GetStatement<T>(this Statement statement)
    {
        foreach (Statement subStatement in statement.GetStatementsRecursively(true))
        {
            if (subStatement is T _subStatement)
            { return _subStatement; }
        }
        return default;
    }

    public static T? GetStatement<T>(this Statement statement, Func<T, bool> condition)
    {
        foreach (Statement subStatement in statement.GetStatementsRecursively(true))
        {
            if (subStatement is T _subStatement && condition.Invoke(_subStatement))
            { return _subStatement; }
        }
        return default;
    }

    public static bool TryGetStatement<T>(this Statement statement, [NotNullWhen(true)] out T? result)
        => (result = GetStatement<T>(statement)) != null;

    public static bool TryGetStatement<T>(this Statement statement, [NotNullWhen(true)] out T? result, Func<T, bool> condition)
        => (result = GetStatement<T>(statement, condition)) != null;
}

public abstract class Statement : IPositioned
{
    public Token? Semicolon { get; internal set; }
    public abstract Position Position { get; }

    protected Statement()
    {
        Semicolon = null;
    }

    protected Statement(Statement other)
    {
        Semicolon = other.Semicolon;
    }

    public override string ToString()
        => $"{GetType().Name}{Semicolon}";

    public abstract IEnumerable<Statement> GetStatementsRecursively(bool includeThis);
}

public abstract class AnyAssignment : Statement
{
    public abstract Assignment ToAssignment();
}

public abstract class StatementWithValue : Statement
{
    public bool SaveValue { get; internal set; } = true;
    public GeneralType? CompiledType { get; internal set; }
    public DataItem? PredictedValue { get; internal set; }
    public TokenPair? SurroundingBracelet { get; internal set; }

    protected StatementWithValue()
    {
        SaveValue = true;
        CompiledType = null;
    }
}

public abstract class StatementWithBlock : Statement
{
    public Block Block { get; }

    protected StatementWithBlock(Block block) => Block = block;
}

public abstract class StatementWithAnyBlock : Statement
{
    public Statement Block { get; }

    protected StatementWithAnyBlock(Statement block) => Block = block;
}

public class Block : Statement
{
    public ImmutableArray<Statement> Statements { get; }
    public TokenPair Brackets { get; }
    public override Position Position => new(Brackets);

    public Block(IEnumerable<Statement> statements, TokenPair brackets)
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
            foreach (Statement substatement in Statements[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }

    public static Block CreateIfNotBlock(Statement statement)
    {
        if (statement is Block block) return block;
        return new Block(
            [statement],
            TokenPair.CreateAnonymous(statement.Position, "{", "}"));
    }
}

public abstract class LinkedIfThing : StatementWithAnyBlock
{
    public Token Keyword { get; }

    protected LinkedIfThing(Token keyword, Statement block) : base(block)
    {
        Keyword = keyword;
    }
}

public class LinkedIf : LinkedIfThing
{
    public StatementWithValue Condition { get; }
    public LinkedIfThing? NextLink { get; init; }
    public override Position Position => new(Keyword, Condition, Block);

    public LinkedIf(Token keyword, StatementWithValue condition, Statement block) : base(keyword, block)
    {
        Condition = condition;
    }

    public override string ToString()
        => $"{Keyword} ({Condition}) {Block}{(NextLink != null ? " ..." : string.Empty)}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Condition.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }

        if (NextLink != null)
        {
            foreach (Statement substatement in NextLink.GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class LinkedElse : LinkedIfThing
{
    public override Position Position => new(Keyword, Block);

    public LinkedElse(Token keyword, Statement block) : base(keyword, block)
    { }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class CompileTag : Statement, IInFile
{
    public Token Operator { get; }
    public Token Identifier { get; }
    public ImmutableArray<Literal> Parameters { get; }
    public Uri? FilePath { get; set; }
    public override Position Position =>
        new Position(Operator, Identifier)
        .Union(Parameters);

    public CompileTag(Token hashToken, Token hashName, IEnumerable<Literal> parameters)
    {
        Operator = hashToken;
        Identifier = hashName;
        Parameters = parameters.ToImmutableArray();
    }

    public override string ToString()
        => $"{Operator}{Identifier}{(Parameters.Length > 0 ? string.Join<Literal>(' ', Parameters) : string.Empty)}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Parameters.Length; i++)
        {
            foreach (Statement substatement in Parameters[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class LiteralList : StatementWithValue
{
    public TokenPair Brackets { get; }
    public ImmutableArray<StatementWithValue> Values { get; }
    public override Position Position => new(Brackets);

    public LiteralList(IEnumerable<StatementWithValue> values, TokenPair brackets)
    {
        Brackets = brackets;
        Values = values.ToImmutableArray();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Values.Length; i++)
        {
            foreach (Statement substatement in Values[i].GetStatementsRecursively(true))
            { yield return substatement; }
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

public class VariableDeclaration : Statement, IInFile, IHaveType
{
    public TypeInstance Type { get; }
    public Token Identifier { get; }
    public StatementWithValue? InitialValue { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public GeneralType? CompiledType { get; set; }
    public Uri? FilePath { get; set; }
    public override Position Position =>
        new Position(Type, Identifier, InitialValue)
        .Union(Modifiers);

    public VariableDeclaration(VariableDeclaration other) : base(other)
    {
        Type = other.Type;
        Identifier = other.Identifier;
        InitialValue = other.InitialValue;
        Modifiers = other.Modifiers;
        FilePath = other.FilePath;
        CompiledType = other.CompiledType;
    }

    public VariableDeclaration(IEnumerable<Token> modifiers, TypeInstance type, Token variableName, StatementWithValue? initialValue)
    {
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
            foreach (Statement substatement in InitialValue.GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }

    public static VariableDeclaration CreateAnonymous(GeneralType type, string name, StatementWithValue? initialValue = null)
        => CreateAnonymous(type.ToTypeInstance(), name, initialValue);

    public static VariableDeclaration CreateAnonymous(TypeInstance type, string name, StatementWithValue? initialValue = null) => new(
        Enumerable.Empty<Token>(),
        type,
        Token.CreateAnonymous(name),
        initialValue);
}

public class TypeStatement : StatementWithValue
{
    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public override Position Position => new(Keyword, Type);

    public TypeStatement(Token keyword, TypeInstance type)
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

public class AnyCall : StatementWithValue, IReadable, IReferenceableTo
{
    public StatementWithValue PrevStatement { get; }
    public TokenPair Brackets { get; }
    public ImmutableArray<StatementWithValue> Parameters { get; }
    public object? Reference { get; set; }
    public override Position Position => new(PrevStatement, Brackets);

    public AnyCall(StatementWithValue prevStatement, IEnumerable<StatementWithValue> parameters, TokenPair brackets)
    {
        PrevStatement = prevStatement;
        Parameters = parameters.ToImmutableArray();
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBracelet?.Start);

        result.Append(PrevStatement);
        result.Append(Brackets.Start);

        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0)
            { result.Append(", "); }

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Parameters[i]);
        }
        result.Append(Brackets.End);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new();
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) { result.Append(", "); }
            result.Append(TypeSearch.Invoke(Parameters[i]).ToString());
        }
        result.Append(')');
        return result.ToString();
    }

    public bool ToFunctionCall([NotNullWhen(true)] out FunctionCall? functionCall)
    {
        functionCall = null;

        if (PrevStatement is null)
        { return false; }

        if (PrevStatement is Identifier functionIdentifier)
        {
            functionCall = new FunctionCall(null, functionIdentifier.Token, Parameters, Brackets)
            {
                Semicolon = Semicolon,
                SaveValue = SaveValue,
                SurroundingBracelet = SurroundingBracelet,
                CompiledType = CompiledType,
                PredictedValue = PredictedValue,
                Reference = Reference,
            };
            return true;
        }

        if (PrevStatement is Field field)
        {
            functionCall = new FunctionCall(field.PrevStatement, field.Identifier, Parameters, Brackets)
            {
                Semicolon = Semicolon,
                SaveValue = SaveValue,
                SurroundingBracelet = SurroundingBracelet,
                CompiledType = CompiledType,
                PredictedValue = PredictedValue,
                Reference = Reference,
            };
            return true;
        }

        return false;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }

        for (int i = 0; i < Parameters.Length; i++)
        {
            foreach (Statement substatement in Parameters[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class FunctionCall : StatementWithValue, IReadable, IReferenceableTo
{
    public Token Identifier { get; }
    public ImmutableArray<StatementWithValue> Parameters { get; }
    public StatementWithValue? PrevStatement { get; }
    public TokenPair Brackets { get; }
    public object? Reference { get; set; }
    public bool IsMethodCall => PrevStatement != null;
    public StatementWithValue[] MethodParameters
    {
        get
        {
            if (PrevStatement == null)
            { return Parameters.ToArray(); }
            List<StatementWithValue> newList = new(Parameters);
            newList.Insert(0, PrevStatement);
            return newList.ToArray();
        }
    }
    public override Position Position =>
        new Position(Brackets, Identifier)
        .Union(MethodParameters);

    public FunctionCall(StatementWithValue? prevStatement, Token identifier, IEnumerable<StatementWithValue> parameters, TokenPair brackets)
    {
        PrevStatement = prevStatement;
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
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
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(Parameters[i]);

            if (result.Length >= 10 && i + 1 != Parameters.Length)
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

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new();
        if (PrevStatement != null)
        {
            result.Append(TypeSearch.Invoke(PrevStatement).ToString());
            result.Append('.');
        }
        result.Append(Identifier.ToString());
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(TypeSearch.Invoke(Parameters[i]).ToString());
        }
        result.Append(')');
        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        if (PrevStatement != null)
        {
            foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
            { yield return substatement; }
        }

        for (int i = 0; i < Parameters.Length; i++)
        {
            foreach (Statement substatement in Parameters[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class KeywordCall : StatementWithValue, IReadable
{
    public Token Identifier { get; }
    public ImmutableArray<StatementWithValue> Parameters { get; }
    public override Position Position =>
        new Position(Identifier)
        .Union(Parameters);

    public KeywordCall(Token identifier, IEnumerable<StatementWithValue> parameters)
    {
        Identifier = identifier;
        Parameters = parameters.ToImmutableArray();
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SurroundingBracelet?.Start);

        result.Append(Identifier);

        if (Parameters.Length > 0)
        {
            result.Append(' ');
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= Stringify.CozyLength)
                { result.Append("..."); break; }

                result.Append(Parameters[i]);
            }
        }

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new();
        result.Append(Identifier.Content);
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(TypeSearch.Invoke(Parameters[i]));
        }
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Parameters.Length; i++)
        {
            foreach (Statement substatement in Parameters[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class BinaryOperatorCall : StatementWithValue, IReadable, IReferenceableTo<CompiledOperator>
{
    public const int ParameterCount = 2;

    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public StatementWithValue Right { get; set; }
    public CompiledOperator? Reference { get; set; }
    public override Position Position => new(Operator, Left, Right);
    public StatementWithValue[] Parameters => new StatementWithValue[] { Left, Right };

    public BinaryOperatorCall(Token op, StatementWithValue left, StatementWithValue right)
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
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) { result.Append(", "); }
            result.Append(typeSearch.Invoke(Parameters[i]).ToString());
        }
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Left.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Right.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class UnaryOperatorCall : StatementWithValue, IReadable, IReferenceableTo<CompiledOperator>
{
    public const int ParameterCount = 1;

    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public CompiledOperator? Reference { get; set; }
    public override Position Position => new(Operator, Left);
    public StatementWithValue[] Parameters => new StatementWithValue[] { Left };

    public UnaryOperatorCall(Token op, StatementWithValue left)
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
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) { result.Append(", "); }
            result.Append(typeSearch.Invoke(Parameters[i]).ToString());
        }
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Left.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

/// <summary>
/// Increment and decrement operator
/// </summary>
public class ShortOperatorCall : AnyAssignment, IReadable, IReferenceableTo<CompiledOperator>
{
    /// <summary>
    /// This should be "++" or "--"
    /// </summary>
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public CompiledOperator? Reference { get; set; }
    public StatementWithValue[] Parameters => new StatementWithValue[] { Left };
    public override Position Position => new(Operator, Left);

    public ShortOperatorCall(Token op, StatementWithValue left)
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
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(typeSearch.Invoke(Parameters[i]));
        }
        result.Append(')');

        return result.ToString();
    }

    /// <exception cref="NotImplementedException"/>
    public override Assignment ToAssignment()
    {
        BinaryOperatorCall operatorCall = GetOperatorCall();
        Token assignmentToken = Token.CreateAnonymous("=", TokenType.Operator, Operator.Position);
        return new Assignment(assignmentToken, Left, operatorCall);
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Left.GetStatementsRecursively(true))
        { yield return substatement; }
    }

    /// <exception cref="NotImplementedException"/>
    public BinaryOperatorCall GetOperatorCall()
    {
        switch (Operator.Content)
        {
            case "++":
            {
                Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position);
                return new BinaryOperatorCall(Token.CreateAnonymous("+", TokenType.Operator, Operator.Position), Left, one);
            }

            case "--":
            {
                Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position);
                return new BinaryOperatorCall(Token.CreateAnonymous("-", TokenType.Operator, Operator.Position), Left, one);
            }

            default: throw new NotImplementedException();
        }
    }
}

public class Assignment : AnyAssignment, IReferenceableTo<CompiledOperator>
{
    /// <summary>
    /// This should always be <c>"="</c>
    /// </summary>
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public StatementWithValue Right { get; }
    public CompiledOperator? Reference { get; set; }
    public override Position Position => new(Operator, Left, Right);

    public Assignment(Token @operator, StatementWithValue left, StatementWithValue right)
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

        foreach (Statement substatement in Left.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Right.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class CompoundAssignment : AnyAssignment, IReferenceableTo<CompiledOperator>
{
    /// <summary>
    /// This should always starts with <c>"="</c>
    /// </summary>
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public StatementWithValue Right { get; }
    public override Position Position => new(Operator, Left, Right);
    public CompiledOperator? Reference { get; set; }

    public CompoundAssignment(Token @operator, StatementWithValue left, StatementWithValue right)
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
        return new Assignment(Token.CreateAnonymous("=", TokenType.Operator, Operator.Position), Left, statementToAssign);
    }

    public BinaryOperatorCall GetOperatorCall() => new(
        Token.CreateAnonymous(Operator.Content.Replace("=", string.Empty, StringComparison.Ordinal), TokenType.Operator, Operator.Position),
        Left,
        Right);

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Left.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Right.GetStatementsRecursively(true))
        { yield return substatement; }
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

    public Literal(LiteralType type, string value, Token valueToken)
    {
        Type = type;
        Value = value;
        ValueToken = valueToken;
    }

    public Literal(LiteralType type, Token value)
    {
        Type = type;
        Value = value.Content;
        ValueToken = value;
    }

    /// <exception cref="NotImplementedException"/>
    Literal(DataItem value, Token token)
    {
        Type = value.Type switch
        {
            RuntimeType.Byte => LiteralType.Integer,
            RuntimeType.Integer => LiteralType.Integer,
            RuntimeType.Single => LiteralType.Float,
            RuntimeType.Char => LiteralType.Char,
            _ => throw new NotImplementedException(),
        };
        Value = value.ToString();
        ValueToken = token;
    }

    public static Literal CreateAnonymous(LiteralType type, string value, IPositioned position)
        => Literal.CreateAnonymous(type, value, position.Position);

    public static Literal CreateAnonymous(LiteralType type, string value, Position position)
    {
        TokenType tokenType = type switch
        {
            LiteralType.Integer => TokenType.LiteralNumber,
            LiteralType.Float => TokenType.LiteralFloat,
            LiteralType.String => TokenType.LiteralString,
            LiteralType.Char => TokenType.LiteralCharacter,
            _ => TokenType.Identifier,
        };
        return new Literal(type, value, Token.CreateAnonymous(value, tokenType))
        {
            ImaginaryPosition = position,
        };
    }

    /// <exception cref="NotImplementedException"/>
    public static Literal CreateAnonymous(DataItem value, IPositioned position)
        => Literal.CreateAnonymous(value, position.Position);

    /// <exception cref="NotImplementedException"/>
    public static Literal CreateAnonymous(DataItem value, Position position)
    {
        TokenType tokenType = value.Type switch
        {
            RuntimeType.Byte => TokenType.LiteralNumber,
            RuntimeType.Integer => TokenType.LiteralNumber,
            RuntimeType.Single => TokenType.LiteralFloat,
            RuntimeType.Char => TokenType.LiteralCharacter,
            _ => TokenType.Identifier,
        };
        return new Literal(value, Token.CreateAnonymous(value.ToString(), tokenType))
        {
            ImaginaryPosition = position,
        };
    }

    /// <exception cref="NotImplementedException"/>
    public static Literal[] CreateAnonymous(DataItem[] values, IPositioned[] positions)
    {
        Literal[] result = new Literal[values.Length];
        for (int i = 0; i < values.Length; i++)
        { result[i] = Literal.CreateAnonymous(values[i], positions[i]); }
        return result;
    }

    /// <exception cref="NotImplementedException"/>
    public static Literal[] CreateAnonymous(DataItem[] values, Position[] positions)
    {
        Literal[] result = new Literal[values.Length];
        for (int i = 0; i < values.Length; i++)
        { result[i] = Literal.CreateAnonymous(values[i], positions[i]); }
        return result;
    }

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

    public static int GetInt(string value)
    {
        value = value.Trim();
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
    public static float GetFloat(string value)
    {
        value = value.Trim();
        value = value.Replace("_", string.Empty, StringComparison.Ordinal);
        value = value.EndsWith('f') ? value[..^1] : value;
        return float.Parse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }
    public static bool GetBoolean(string value)
    {
        value = value.Trim();
        return value == "true";
    }

    public int GetInt() => Literal.GetInt(Value);
    public float GetFloat() => Literal.GetFloat(Value);
    public bool GetBoolean() => Literal.GetBoolean(Value);

    public bool TryConvert<T>([NotNullWhen(true)] out T? value)
    {
        if (!Utils.TryConvertType(typeof(T), out LiteralType type))
        {
            value = default;
            return false;
        }

        if (type != Type)
        {
            value = default;
            return false;
        }

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
    public Token Token { get; }
    public object? Reference { get; set; }
    public string Content => Token.Content;
    public override Position Position => Token.Position;

    public Identifier(Token token) => Token = token;

    public override string ToString() => Token.Content;

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

    public AddressGetter(Token operatorToken, StatementWithValue prevStatement)
    {
        Operator = operatorToken;
        PrevStatement = prevStatement;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Operator}{PrevStatement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class Pointer : StatementWithValue
{
    public Token Operator { get; }
    public StatementWithValue PrevStatement { get; }
    public override Position Position => new(Operator, PrevStatement);

    public Pointer(Token operatorToken, StatementWithValue prevStatement)
    {
        Operator = operatorToken;
        PrevStatement = prevStatement;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Operator}{PrevStatement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class WhileLoop : StatementWithBlock
{
    public Token Keyword { get; }
    public StatementWithValue Condition { get; }
    public override Position Position => new(Keyword, Block);

    public WhileLoop(Token keyword, StatementWithValue condition, Block block)
        : base(block)
    {
        Keyword = keyword;
        Condition = condition;
    }

    public override string ToString()
        => $"{Keyword} ({Condition}) {Block}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Condition.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class ForLoop : StatementWithBlock
{
    public Token Keyword { get; }
    public VariableDeclaration VariableDeclaration { get; }
    public StatementWithValue Condition { get; }
    public AnyAssignment Expression { get; }
    public override Position Position => new(Keyword, Block);

    public ForLoop(Token keyword, VariableDeclaration variableDeclaration, StatementWithValue condition, AnyAssignment expression, Block block)
        : base(block)
    {
        Keyword = keyword;
        VariableDeclaration = variableDeclaration;
        Condition = condition;
        Expression = expression;
    }

    public override string ToString()
        => $"{Keyword} (...) {Block}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in VariableDeclaration.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Condition.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Expression.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class IfContainer : Statement
{
    public ImmutableArray<BaseBranch> Parts { get; }
    public override Position Position => new(Parts);

    public IfContainer(IEnumerable<BaseBranch> parts)
    {
        Parts = parts.ToImmutableArray();
    }

    /// <exception cref="NotImplementedException"/>
    LinkedIfThing? ToLinks(int i)
    {
        if (i >= Parts.Length)
        { return null; }

        if (Parts[i] is ElseIfBranch elseIfBranch)
        {
            return new LinkedIf(elseIfBranch.Keyword, elseIfBranch.Condition, elseIfBranch.Block)
            {
                NextLink = ToLinks(i + 1),
            };
        }

        if (Parts[i] is ElseBranch elseBranch)
        {
            return new LinkedElse(elseBranch.Keyword, elseBranch.Block);
        }

        throw new NotImplementedException();
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="NotImplementedException"/>
    public LinkedIf ToLinks()
    {
        if (Parts.Length == 0) throw new InternalException();
        if (Parts[0] is not IfBranch ifBranch) throw new InternalException();
        return new LinkedIf(ifBranch.Keyword, ifBranch.Condition, ifBranch.Block)
        {
            NextLink = ToLinks(1),
        };
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Parts.Length; i++)
        {
            foreach (Statement substatement in Parts[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }

    public override string ToString()
    {
        if (Parts.Length == 0) return "null";
        return Parts[0].ToString();
    }
}

public abstract class BaseBranch : StatementWithAnyBlock
{
    public Token Keyword { get; }
    public IfPart Type { get; }
    public override Position Position => new(Keyword, Block);

    protected BaseBranch(Token keyword, IfPart type, Statement block)
        : base(block)
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

    public IfBranch(Token keyword, StatementWithValue condition, Statement block)
        : base(keyword, IfPart.If, block)
    {
        Condition = condition;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Condition.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class ElseIfBranch : BaseBranch
{
    public StatementWithValue Condition { get; }

    public ElseIfBranch(Token keyword, StatementWithValue condition, Statement block)
        : base(keyword, IfPart.ElseIf, block)
    {
        Condition = condition;
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Condition.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class ElseBranch : BaseBranch
{
    public ElseBranch(Token keyword, Statement block)
        : base(keyword, IfPart.Else, block)
    { }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class NewInstance : StatementWithValue, IHaveType
{
    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public override Position Position => new(Keyword, Type);

    public NewInstance(Token keyword, TypeInstance typeName)
    {
        Keyword = keyword;
        Type = typeName;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Keyword} {Type}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class ConstructorCall : StatementWithValue, IReadable, IReferenceableTo<ConstructorDefinition>, IHaveType
{
    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public ImmutableArray<StatementWithValue> Parameters { get; }
    public TokenPair Brackets { get; }
    public ConstructorDefinition? Reference { get; set; }
    public override Position Position =>
        new Position(Keyword, Type, Brackets)
        .Union(Parameters);

    public ConstructorCall(Token keyword, TypeInstance typeName, IEnumerable<StatementWithValue> parameters, TokenPair brackets)
    {
        Keyword = keyword;
        Type = typeName;
        Parameters = parameters.ToImmutableArray();
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

        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Parameters[i]);
        }

        result.Append(Brackets.End);

        result.Append(SurroundingBracelet?.End);
        result.Append(Semicolon);
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new();
        result.Append(Type.ToString());
        result.Append('.');
        result.Append(Keyword.Content);
        result.Append(Brackets.Start);
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(TypeSearch.Invoke(Parameters[i]));
        }
        result.Append(Brackets.End);

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        for (int i = 0; i < Parameters.Length; i++)
        {
            foreach (Statement substatement in Parameters[i].GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }

    public NewInstance ToInstantiation() => new(Keyword, Type)
    {
        CompiledType = CompiledType,
        SaveValue = true,
        Semicolon = Semicolon,
    };
}

public class IndexCall : StatementWithValue, IReadable, IReferenceableTo<GeneralFunctionDefinition>
{
    public StatementWithValue PrevStatement { get; }
    public StatementWithValue Index { get; }
    public TokenPair Brackets { get; }
    public override Position Position => new(PrevStatement, Index);
    public GeneralFunctionDefinition? Reference { get; set; }

    public IndexCall(StatementWithValue prevStatement, StatementWithValue indexStatement, TokenPair brackets)
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

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }

        foreach (Statement substatement in Index.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class Field : StatementWithValue, IReferenceableTo<FieldDefinition>
{
    public Token Identifier { get; }
    public StatementWithValue PrevStatement { get; }
    public FieldDefinition? Reference { get; set; }
    public override Position Position => new(PrevStatement, Identifier);

    public Field(StatementWithValue prevStatement, Token fieldName)
    {
        PrevStatement = prevStatement;
        Identifier = fieldName;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{PrevStatement}.{Identifier}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class TypeCast : StatementWithValue, IHaveType
{
    public StatementWithValue PrevStatement { get; }
    public Token Keyword { get; }
    public TypeInstance Type { get; }
    public override Position Position => new(PrevStatement, Keyword, Type);

    public TypeCast(StatementWithValue prevStatement, Token keyword, TypeInstance type)
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

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class ModifiedStatement : StatementWithValue
{
    public StatementWithValue Statement { get; }
    public Token Modifier { get; }
    public override Position Position
        => new(Modifier, Statement);

    public ModifiedStatement(Token modifier, StatementWithValue statement)
    {
        Statement = statement;
        Modifier = modifier;
    }

    public override string ToString()
        => $"{SurroundingBracelet?.Start}{Modifier} {Statement}{SurroundingBracelet?.End}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Statement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}
