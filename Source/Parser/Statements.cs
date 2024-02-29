namespace LanguageCore.Parser.Statement;

using Compiler;
using Runtime;
using Tokenizing;

struct Stringify
{
    public const int CozyLength = 30;
}

public interface IReferenceableTo : IReferenceableTo<object> { }
public interface IReferenceableTo<T> where T : notnull
{
    public T? Reference { get; set; }
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
    public Token? Semicolon { get; set; }
    public abstract Position Position { get; }

    public Statement()
    {
        Semicolon = null;
    }

    public Statement(Statement other)
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
    public bool SaveValue { get; set; } = true;
    public GeneralType? CompiledType { get; internal set; }
    public DataItem? PredictedValue { get; internal set; }

    public StatementWithValue() : base()
    {
        SaveValue = true;
        CompiledType = null;
    }

    public StatementWithValue(StatementWithValue other) : base(other)
    {
        SaveValue = other.SaveValue;
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
    public Token BracketStart { get; }
    public Token BracketEnd { get; }
    public override Position Position => new(BracketStart, BracketEnd);

    public Block(Token bracketStart, IEnumerable<Statement> statements, Token bracketEnd)
    {
        this.BracketStart = bracketStart;
        this.Statements = statements.ToImmutableArray();
        this.BracketEnd = bracketEnd;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append('{');

        if (Statements.Length == 0)
        { result.Append(' '); }
        else if (Statements.Length == 1)
        {
            result.Append(' ');
            result.Append(Statements[0].ToString());
            result.Append(' ');
        }
        else
        { result.Append("..."); }

        result.Append('}');
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
            Token.CreateAnonymous("{", TokenType.Operator, statement.Position.Before()),
            [statement],
            Token.CreateAnonymous("}", TokenType.Operator, statement.Position.After())
            );
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
    {

    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Block.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class CompileTag : Statement, IInFile
{
    public Token HashToken { get; }
    public Token HashName { get; }
    public ImmutableArray<Literal> Parameters { get; }
    public Uri? FilePath { get; set; }
    public override Position Position =>
        new Position(HashToken, HashName)
        .Union(Parameters);

    public CompileTag(Token hashToken, Token hashName, IEnumerable<Literal> parameters)
    {
        HashToken = hashToken;
        HashName = hashName;
        Parameters = parameters.ToImmutableArray();
    }

    public override string ToString()
        => $"{HashToken}{HashName}{(Parameters.Length > 0 ? string.Join<Literal>(' ', Parameters) : string.Empty)}{Semicolon}";

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
    public Token BracketLeft { get; }
    public Token BracketRight { get; }
    public ImmutableArray<StatementWithValue> Values { get; }
    public override Position Position => new(BracketLeft, BracketRight);

    public LiteralList(Token bracketLeft, IEnumerable<StatementWithValue> values, Token bracketRight)
    {
        BracketLeft = bracketLeft;
        BracketRight = bracketRight;
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
        StringBuilder result = new(3);
        result.Append('[');
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

                result.Append(Values[i].ToString());
            }
        }
        result.Append(']');
        if (Semicolon != null) result.Append(Semicolon.ToString());
        return result.ToString();
    }
}

public class VariableDeclaration : Statement, IInFile, IHaveType
{
    public TypeInstance Type { get; }
    public Token VariableName { get; }
    public StatementWithValue? InitialValue { get; }
    public ImmutableArray<Token> Modifiers { get; }
    public GeneralType? CompiledType { get; set; }
    public Uri? FilePath { get; set; }
    public override Position Position =>
        new Position(Type, VariableName, InitialValue)
        .Union(Modifiers);

    public VariableDeclaration(VariableDeclaration other) : base(other)
    {
        Type = other.Type;
        VariableName = other.VariableName;
        InitialValue = other.InitialValue;
        Modifiers = other.Modifiers;
        FilePath = other.FilePath;
        CompiledType = other.CompiledType;
    }

    public VariableDeclaration(IEnumerable<Token> modifiers, TypeInstance type, Token variableName, StatementWithValue? initialValue)
    {
        Type = type;
        VariableName = variableName;
        InitialValue = initialValue;
        Modifiers = modifiers.ToImmutableArray();
    }

    public override string ToString()
        => $"{string.Join(' ', Modifiers)} {Type} {VariableName}{((InitialValue != null) ? " = ..." : string.Empty)}{Semicolon}".TrimStart();

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        if (InitialValue != null)
        {
            foreach (Statement substatement in InitialValue.GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class AnyCall : StatementWithValue, IReadable, IReferenceableTo
{
    public StatementWithValue PrevStatement { get; }
    public Token BracketLeft { get; }
    public ImmutableArray<StatementWithValue> Parameters { get; }
    public Token BracketRight { get; }
    public object? Reference { get; set; }
    public override Position Position => new(PrevStatement, BracketLeft, BracketRight);

    public AnyCall(StatementWithValue prevStatement, Token bracketLeft, IEnumerable<StatementWithValue> parameters, Token bracketRight)
    {
        PrevStatement = prevStatement;
        BracketLeft = bracketLeft;
        Parameters = parameters.ToImmutableArray();
        BracketRight = bracketRight;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (PrevStatement != null) result.Append(PrevStatement.ToString());
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0)
            { result.Append(", "); }

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Parameters[i].ToString());
        }
        result.Append(')');
        if (Semicolon != null) result.Append(Semicolon.ToString());
        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new();
        result.Append('(');
        for (int i = 0; i < this.Parameters.Length; i++)
        {
            if (i > 0) { result.Append(", "); }
            result.Append(TypeSearch.Invoke(this.Parameters[i]).ToString());
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
            functionCall = new FunctionCall(null, functionIdentifier.Token, BracketLeft, Parameters, BracketRight)
            {
                Semicolon = Semicolon,
                SaveValue = SaveValue,
            };
            return true;
        }

        if (PrevStatement is Field field)
        {
            functionCall = new FunctionCall(field.PrevStatement, field.FieldName, BracketLeft, Parameters, BracketRight)
            {
                Semicolon = Semicolon,
                SaveValue = SaveValue,
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
    public Token BracketLeft { get; }
    public Token BracketRight { get; }
    public object? Reference { get; set; }
    public string FunctionName => Identifier.Content;
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
        new Position(BracketLeft, BracketRight, Identifier)
        .Union(MethodParameters);

    public FunctionCall(StatementWithValue? prevStatement, Token identifier, Token bracketLeft, IEnumerable<StatementWithValue> parameters, Token bracketRight)
    {
        PrevStatement = prevStatement;
        Identifier = identifier;
        BracketLeft = bracketLeft;
        Parameters = parameters.ToImmutableArray();
        BracketRight = bracketRight;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (PrevStatement != null)
        {
            result.Append(PrevStatement);
            result.Append('.');
        }
        result.Append(FunctionName);
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(Parameters[i].ToString());

            if (result.Length >= 10 && i + 1 != Parameters.Length)
            {
                result.Append(", ...");
                break;
            }
        }
        result.Append(')');
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
        result.Append(FunctionName);
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
    public string FunctionName => Identifier.Content;
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
        StringBuilder result = new(1);
        result.Append(FunctionName);

        if (Parameters.Length > 0)
        {
            result.Append(' ');
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= Stringify.CozyLength)
                { result.Append("..."); break; }

                result.Append(Parameters[i].ToString());
            }
        }

        if (Semicolon != null) result.Append(Semicolon.ToString());

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

public class OperatorCall : StatementWithValue, IReadable, IReferenceableTo<CompiledOperator>
{
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public StatementWithValue? Right { get; set; }
    public bool InsideBracelet { get; set; }
    public CompiledOperator? Reference { get; set; }
    public StatementWithValue[] Parameters
    {
        get
        {
            if (Right is null) return new StatementWithValue[] { Left };
            else return new StatementWithValue[] { Left, Right };
        }
    }
    public int ParameterCount
    {
        get
        {
            if (Right is not null) return 2;
            else return 1;
        }
    }
    public override Position Position => new(Operator, Left, Right);

    public OperatorCall(Token op, StatementWithValue left, StatementWithValue? right = null)
    {
        this.Operator = op;
        this.Left = left;
        this.Right = right;
    }

    public override string ToString()
    {
        StringBuilder result = new(3);

        if (InsideBracelet) result.Append('(');

        if (Left != null)
        {
            if (Left.ToString().Length < Stringify.CozyLength)
            { result.Append(Left.ToString()); }
            else
            { result.Append("..."); }

            result.Append(' ');
            result.Append(Operator.ToString());
            result.Append(' ');

            if (Right != null)
            {
                if (Right.ToString().Length < Stringify.CozyLength)
                { result.Append(Right.ToString()); }
                else
                { result.Append("..."); }
            }
        }
        else
        { result.Append(Operator.ToString()); }

        if (InsideBracelet) result.Append(')');

        if (Semicolon != null) result.Append(Semicolon.ToString());

        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new(this.Operator.Content);
        result.Append('(');
        for (int i = 0; i < this.Parameters.Length; i++)
        {
            if (i > 0) { result.Append(", "); }

            result.Append(TypeSearch.Invoke(this.Parameters[i]).ToString());
        }
        result.Append(')');

        return result.ToString();
    }

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Left.GetStatementsRecursively(true))
        { yield return substatement; }

        if (Right != null)
        {
            foreach (Statement substatement in Right.GetStatementsRecursively(true))
            { yield return substatement; }
        }
    }
}

public class ShortOperatorCall : AnyAssignment, IReadable, IReferenceableTo<CompiledOperator>
{
    public Token Operator { get; }
    public StatementWithValue Left { get; }
    public CompiledOperator? Reference { get; set; }
    public StatementWithValue[] Parameters => new StatementWithValue[] { Left };
    public override Position Position => new(Operator, Left);

    public ShortOperatorCall(Token op, StatementWithValue left)
    {
        this.Operator = op;
        this.Left = left;
    }

    public override string ToString()
    {
        StringBuilder result = new();
        if (Left != null)
        {
            if (Left.ToString().Length <= Stringify.CozyLength)
            { result.Append(Left.ToString()); }
            else
            { result.Append("..."); }

            result.Append(' ');
            result.Append(Operator.ToString());
        }
        else
        { result.Append(Operator.ToString()); }

        if (Semicolon != null) result.Append(Semicolon.ToString());
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
        OperatorCall operatorCall = GetOperatorCall();
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
    public OperatorCall GetOperatorCall()
    {
        switch (Operator.Content)
        {
            case "++":
            {
                Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position);
                return new OperatorCall(Token.CreateAnonymous("+", TokenType.Operator, Operator.Position), Left, one);
            }

            case "--":
            {
                Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position);
                return new OperatorCall(Token.CreateAnonymous("-", TokenType.Operator, Operator.Position), Left, one);
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
        this.Operator = @operator;
        this.Left = left;
        this.Right = right;
    }

    public override string ToString()
        => $"... {Operator} ...{Semicolon}";

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
        this.Operator = @operator;
        this.Left = left;
        this.Right = right;
    }

    public override string ToString() => $"... {Operator} ...{Semicolon}";

    public override Assignment ToAssignment()
    {
        OperatorCall statementToAssign = GetOperatorCall();
        return new Assignment(Token.CreateAnonymous("=", TokenType.Operator, Operator.Position), Left, statementToAssign);
    }

    public OperatorCall GetOperatorCall() => new(
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
            RuntimeType.UInt8 => LiteralType.Integer,
            RuntimeType.SInt32 => LiteralType.Integer,
            RuntimeType.Single => LiteralType.Float,
            RuntimeType.UInt16 => LiteralType.Char,
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
            RuntimeType.UInt8 => TokenType.LiteralNumber,
            RuntimeType.SInt32 => TokenType.LiteralNumber,
            RuntimeType.Single => TokenType.LiteralFloat,
            RuntimeType.UInt16 => TokenType.LiteralCharacter,
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

    public override string ToString() => Type switch
    {
        LiteralType.String => $"\"{Value.Escape()}\"",
        LiteralType.Char => $"'{Value.Escape()}'",
        _ => Value,
    };

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
    public Token OperatorToken { get; }
    public StatementWithValue PrevStatement { get; }
    public override Position Position => new(OperatorToken, PrevStatement);

    public AddressGetter(Token operatorToken, StatementWithValue prevStatement)
    {
        OperatorToken = operatorToken;
        PrevStatement = prevStatement;
    }

    public override string ToString()
        => $"{OperatorToken.Content}{PrevStatement}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in PrevStatement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}

public class Pointer : StatementWithValue
{
    public Token OperatorToken { get; }
    public StatementWithValue PrevStatement { get; }
    public override Position Position => new(OperatorToken, PrevStatement);

    public Pointer(Token operatorToken, StatementWithValue prevStatement)
    {
        OperatorToken = operatorToken;
        PrevStatement = prevStatement;
    }

    public override string ToString()
        => $"{OperatorToken.Content}{PrevStatement}{Semicolon}";

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
        this.Condition = condition;
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
        this.Condition = condition;
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
    public TypeInstance TypeName { get; }
    public override Position Position => new(Keyword, TypeName);
    TypeInstance IHaveType.Type => TypeName;

    public NewInstance(Token keyword, TypeInstance typeName)
    {
        Keyword = keyword;
        TypeName = typeName;
    }

    public override string ToString()
        => $"{Keyword} {TypeName}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;
    }
}

public class ConstructorCall : StatementWithValue, IReadable, IReferenceableTo<ConstructorDefinition>, IHaveType
{
    public Token Keyword { get; }
    public TypeInstance TypeName { get; }
    TypeInstance IHaveType.Type => TypeName;
    public ImmutableArray<StatementWithValue> Parameters { get; }
    public Token BracketLeft { get; }
    public Token BracketRight { get; }
    public ConstructorDefinition? Reference { get; set; }
    public override Position Position =>
        new Position(Keyword, TypeName, BracketLeft, BracketRight)
        .Union(Parameters);

    public ConstructorCall(Token keyword, TypeInstance typeName, Token bracketLeft, IEnumerable<StatementWithValue> parameters, Token bracketRight)
    {
        Keyword = keyword;
        TypeName = typeName;
        BracketLeft = bracketLeft;
        Parameters = parameters.ToImmutableArray();
        BracketRight = bracketRight;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Keyword.ToString());

        result.Append(' ');

        result.Append(TypeName.ToString());
        result.Append(BracketLeft.ToString());

        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            if (result.Length >= Stringify.CozyLength)
            { result.Append("..."); break; }

            result.Append(Parameters[i].ToString());
        }

        result.Append(BracketRight.ToString());

        if (Semicolon != null) result.Append(Semicolon.ToString());

        return result.ToString();
    }

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new();
        result.Append(TypeName.ToString());
        result.Append('.');
        result.Append(Keyword.Content);
        result.Append(BracketLeft.ToString());
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");

            result.Append(TypeSearch.Invoke(Parameters[i]));
        }
        result.Append(BracketRight.ToString());

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

    public NewInstance ToInstantiation() => new(
        Keyword,
        TypeName)
    {
        CompiledType = CompiledType,
        SaveValue = true,
        Semicolon = null,
    };
}

public class IndexCall : StatementWithValue, IReadable, IReferenceableTo<GeneralFunctionDefinition>
{
    public StatementWithValue PrevStatement { get; }
    public StatementWithValue Index { get; }
    public Token BracketLeft { get; }
    public Token BracketRight { get; }
    public override Position Position => new(PrevStatement, Index);
    public GeneralFunctionDefinition? Reference { get; set; }

    public IndexCall(StatementWithValue prevStatement, Token bracketLeft, StatementWithValue indexStatement, Token bracketRight)
    {
        this.PrevStatement = prevStatement;
        this.Index = indexStatement;
        this.BracketLeft = bracketLeft;
        this.BracketRight = bracketRight;
    }

    public override string ToString()
        => $"{PrevStatement}{BracketLeft}{Index}{BracketRight}{Semicolon}";

    public string ToReadable(Func<StatementWithValue, GeneralType> TypeSearch)
    {
        StringBuilder result = new(2);

        if (PrevStatement != null)
        { result.Append(TypeSearch.Invoke(this.PrevStatement).ToString()); }
        else
        { result.Append('?'); }

        result.Append(BracketLeft.ToString());
        result.Append(TypeSearch.Invoke(this.Index).ToString());
        result.Append(BracketRight.ToString());

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
    public Token FieldName { get; }
    public StatementWithValue PrevStatement { get; }
    public FieldDefinition? Reference { get; set; }
    public override Position Position => new(PrevStatement, FieldName);

    public Field(StatementWithValue prevStatement, Token fieldName)
    {
        PrevStatement = prevStatement;
        FieldName = fieldName;
    }

    public override string ToString()
        => $"{PrevStatement}.{FieldName}{Semicolon}";

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
        this.PrevStatement = prevStatement;
        this.Keyword = keyword;
        this.Type = type;
    }

    public override string ToString()
        => $"{PrevStatement} {Keyword} {Type}{Semicolon}";

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
        this.Statement = statement;
        this.Modifier = modifier;
    }

    public override string ToString()
        => $"{Modifier} {Statement}{Semicolon}";

    public override IEnumerable<Statement> GetStatementsRecursively(bool includeThis)
    {
        if (includeThis) yield return this;

        foreach (Statement substatement in Statement.GetStatementsRecursively(true))
        { yield return substatement; }
    }
}
