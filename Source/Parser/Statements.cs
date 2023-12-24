﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace LanguageCore.Parser.Statement
{
    using Compiler;
    using Tokenizing;

    struct Stringify
    {
        public const int CozyLength = 30;
    }

    public static class StatementExtensions
    {
        public static T? GetStatementAt<T>(this ParserResult parserResult, int absolutePosition)
            where T : IThingWithPosition
            => StatementExtensions.GetStatement<T>(parserResult, statement => statement.Position.AbsoluteRange.Contains(absolutePosition));

        public static T? GetStatementAt<T>(this ParserResult parserResult, SinglePosition position)
            where T : IThingWithPosition
            => StatementExtensions.GetStatement<T>(parserResult, statement => statement.Position.Range.Contains(position));

        public static Statement? GetStatementAt(this ParserResult parserResult, int absolutePosition)
            => StatementExtensions.GetStatement(parserResult, statement => statement.Position.AbsoluteRange.Contains(absolutePosition));

        public static Statement? GetStatementAt(this ParserResult parserResult, SinglePosition position)
            => StatementExtensions.GetStatement(parserResult, statement => statement.Position.Range.Contains(position));

        public static IEnumerable<T> GetStatements<T>(this ParserResult parserResult)
        {
            foreach (Statement statement in parserResult)
            {
                if (statement is T _statement)
                { yield return _statement; }
            }
        }

        public static IEnumerable<T> GetStatements<T>(this ParserResult parserResult, Func<T, bool> condition)
        {
            foreach (Statement statement in parserResult)
            {
                if (statement is T _statement && condition.Invoke(_statement))
                { yield return _statement; }
            }
        }

        public static IEnumerable<T> GetStatements<T>(this Statement statement)
        {
            foreach (Statement subStatement in statement)
            {
                if (subStatement is T _subStatement)
                { yield return _subStatement; }
            }
        }

        public static IEnumerable<T> GetStatements<T>(this Statement statement, Func<T, bool> condition)
        {
            foreach (Statement subStatement in statement)
            {
                if (subStatement is T _subStatement && condition.Invoke(_subStatement))
                { yield return _subStatement; }
            }
        }

        public static T? GetStatement<T>(this ParserResult parserResult)
        {
            foreach (Statement statement in parserResult)
            {
                if (statement is T _statement)
                { return _statement; }
            }
            return default;
        }

        public static Statement? GetStatement(this ParserResult parserResult)
        {
            foreach (Statement statement in parserResult)
            { return statement; }
            return default;
        }

        public static T? GetStatement<T>(this ParserResult parserResult, Func<T, bool> condition)
        {
            foreach (Statement statement in parserResult)
            {
                if (statement is T statement_ && condition.Invoke(statement_))
                { return statement_; }
            }
            return default;
        }

        public static Statement? GetStatement(this ParserResult parserResult, Func<Statement, bool> condition)
        {
            foreach (Statement statement in parserResult)
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
            foreach (Statement subStatement in statement)
            {
                if (subStatement is T _subStatement)
                { return _subStatement; }
            }
            return default;
        }

        public static T? GetStatement<T>(this Statement statement, Func<T, bool> condition)
        {
            foreach (Statement subStatement in statement)
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

    public interface IReadableID
    {
        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch);
    }

    public abstract class Statement : IThingWithPosition, IEnumerable<Statement>
    {
        public Token? Semicolon;

        public override string ToString()
            => $"{GetType().Name}{Semicolon}";

        public abstract Position Position { get; }

        public abstract IEnumerator<Statement> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public abstract class AnyAssignment : Statement
    {
        public abstract Assignment ToAssignment();
    }

    public abstract class StatementWithValue : Statement
    {
        public bool SaveValue = true;
    }

    public abstract class StatementWithBlock : Statement
    {
        public readonly Block Block;

        protected StatementWithBlock(Block block) => Block = block;
    }

    public abstract class StatementWithAnyBlock : Statement
    {
        public readonly Statement Block;

        protected StatementWithAnyBlock(Statement block) => Block = block;
    }

    public class Block : Statement
    {
        public readonly Statement[] Statements;

        public readonly Token BracketStart;
        public readonly Token BracketEnd;

        public override Position Position
            => new(BracketStart, BracketEnd);

        public Block(Token bracketStart, IEnumerable<Statement> statements, Token bracketEnd)
        {
            this.BracketStart = bracketStart;
            this.Statements = statements.ToArray();
            this.BracketEnd = bracketEnd;
        }

        public override string ToString()
        {
            StringBuilder result = new(3);
            result.Append('{');

            if (Statements.Length > 0)
            { result.Append("..."); }
            else
            { result.Append(' '); }

            result.Append('}');
            return result.ToString();
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            foreach (Statement statement in Statements)
            {
                yield return statement;
                foreach (Statement substatement in statement)
                { yield return substatement; }
            }
        }
    }

    public abstract class LinkedIfThing : StatementWithAnyBlock
    {
        public readonly Token Keyword;

        protected LinkedIfThing(Token keyword, Statement block) : base(block)
        {
            Keyword = keyword;
        }
    }

    public class LinkedIf : LinkedIfThing
    {
        public readonly StatementWithValue Condition;
        public LinkedIfThing? NextLink;

        public override Position Position
            => new(Keyword, Condition, Block);

        public LinkedIf(Token keyword, StatementWithValue condition, Statement block) : base(keyword, block)
        {
            Condition = condition;
        }

        public override string ToString()
            => $"{Keyword} ({Condition}) {Block}{(NextLink != null ? " ..." : string.Empty)}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Condition;
            foreach (Statement substatement in Condition)
            { yield return substatement; }

            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }

            if (NextLink != null)
            {
                yield return NextLink;
                foreach (Statement substatement in NextLink)
                { yield return substatement; }
            }
        }
    }

    public class LinkedElse : LinkedIfThing
    {
        public override Position Position
            => new(Keyword, Block);

        public LinkedElse(Token keyword, Statement block) : base(keyword, block)
        {

        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }
        }
    }

    public class CompileTag : Statement, IDefinition
    {
        public readonly Token HashToken;
        public readonly Token HashName;
        public readonly Literal[] Parameters;

        public string? FilePath { get; set; }

        public override Position Position
            => new Position(HashToken, HashName).Union(Parameters);

        public CompileTag(Token hashToken, Token hashName, Literal[] parameters)
        {
            HashToken = hashToken;
            HashName = hashName;
            Parameters = parameters;
        }

        public override string ToString()
            => $"{HashToken}{HashName}{(Parameters.Length > 0 ? string.Join<Literal>(' ', Parameters) : string.Empty)}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            foreach (Literal parameter in Parameters)
            {
                yield return parameter;
                foreach (Statement substatement in parameter)
                { yield return substatement; }
            }
        }
    }

    public class LiteralList : StatementWithValue
    {
        public readonly Token BracketLeft;
        public readonly Token BracketRight;
        public readonly StatementWithValue[] Values;

        public override Position Position
            => new(BracketLeft, BracketRight);

        public LiteralList(Token bracketLeft, StatementWithValue[] values, Token bracketRight)
        {
            BracketLeft = bracketLeft;
            BracketRight = bracketRight;
            Values = values;
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            foreach (StatementWithValue value in Values)
            {
                yield return value;
                foreach (Statement substatement in value)
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

    public class VariableDeclaration : Statement, IDefinition
    {
        public readonly TypeInstance Type;
        public readonly Token VariableName;
        public readonly StatementWithValue? InitialValue;
        public readonly Token[] Modifiers;

        public string? FilePath { get; set; }

        public override Position Position
            => new Position(Type, VariableName, InitialValue).Union(Modifiers);

        public VariableDeclaration(Token[] modifiers, TypeInstance type, Token variableName, StatementWithValue? initialValue)
        {
            Type = type;
            VariableName = variableName;
            InitialValue = initialValue;
            Modifiers = modifiers;
        }

        public override string ToString()
            => $"{string.Join<Token>(' ', Modifiers)} {Type} {VariableName}{((InitialValue != null) ? " = ..." : string.Empty)}{Semicolon}".TrimStart();

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (InitialValue != null)
            {
                yield return InitialValue;
                foreach (Statement substatement in InitialValue)
                { yield return substatement; }
            }
        }
    }

    public class AnyCall : StatementWithValue, IReadableID
    {
        public readonly StatementWithValue PrevStatement;
        public readonly Token BracketLeft;
        public readonly StatementWithValue[] Parameters;
        public readonly Token BracketRight;

        public override Position Position
            => new(PrevStatement, BracketLeft, BracketRight);

        public bool IsFunctionCall => PrevStatement is Identifier;
        public bool IsMethodCall => PrevStatement is Field;
        public bool IsFunctionOrMethodCall => IsFunctionCall || IsMethodCall;

        public AnyCall(StatementWithValue prevStatement, Token bracketLeft, IEnumerable<StatementWithValue> parameters, Token bracketRight)
        {
            PrevStatement = prevStatement;
            BracketLeft = bracketLeft;
            Parameters = parameters.ToArray();
            BracketRight = bracketRight;
        }

        public override string ToString()
        {
            StringBuilder result = new(2);
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

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            StringBuilder result = new(2);
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

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (PrevStatement != null)
            {
                yield return PrevStatement;
                foreach (Statement substatement in PrevStatement)
                { yield return substatement; }
            }

            foreach (StatementWithValue parameter in Parameters)
            {
                yield return parameter;
                foreach (Statement substatement in parameter)
                { yield return substatement; }
            }
        }
    }

    public class FunctionCall : StatementWithValue, IReadableID
    {
        public readonly Token Identifier;
        public readonly StatementWithValue[] Parameters;
        public readonly StatementWithValue? PrevStatement;

        public readonly Token BracketLeft;
        public readonly Token BracketRight;

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

        public override Position Position
            => new Position(BracketLeft, BracketRight, Identifier).Union(MethodParameters);

        public FunctionCall(StatementWithValue? prevStatement, Token identifier, Token bracketLeft, IEnumerable<StatementWithValue> parameters, Token bracketRight)
        {
            PrevStatement = prevStatement;
            Identifier = identifier;
            BracketLeft = bracketLeft;
            Parameters = parameters.ToArray();
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

                if (result.Length >= 10 && i - 1 != Parameters.Length)
                {
                    result.Append(", ...");
                    break;
                }
            }
            result.Append(')');
            return result.ToString();
        }

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
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

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (PrevStatement != null)
            {
                yield return PrevStatement;
                foreach (Statement substatement in PrevStatement)
                { yield return substatement; }
            }

            foreach (StatementWithValue parameter in Parameters)
            {
                yield return parameter;
                foreach (Statement substatement in parameter)
                { yield return substatement; }
            }
        }
    }

    public class KeywordCall : StatementWithValue, IReadableID
    {
        public readonly Token Identifier;
        public readonly StatementWithValue[] Parameters;

        public string FunctionName => Identifier.Content;

        public override Position Position
            => new Position(Identifier).Union(Parameters);

        public KeywordCall(Token identifier, IEnumerable<StatementWithValue> parameters)
        {
            Identifier = identifier;
            Parameters = parameters.ToArray();
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

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            StringBuilder result = new();
            result.Append(Identifier.Content);
            result.Append('(');
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) result.Append(", ");

                result.Append(TypeSearch.Invoke(Parameters[i]).Name);
            }
            result.Append(')');

            return result.ToString();
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            foreach (StatementWithValue parameter in Parameters)
            {
                yield return parameter;
                foreach (Statement substatement in parameter)
                { yield return substatement; }
            }
        }
    }

    public class OperatorCall : StatementWithValue, IReadableID
    {
        public readonly Token Operator;
        public readonly StatementWithValue Left;
        public StatementWithValue? Right;
        public bool InsideBracelet;

        public StatementWithValue[] Parameters
        {
            get
            {
                if (Left is null && Right is not null)
                { return new StatementWithValue[] { Right }; }
                else if (Left is not null && Right is null)
                { return new StatementWithValue[] { Left }; }
                else if (Left is not null && Right is not null)
                { return new StatementWithValue[] { Left, Right }; }
                else
                { throw new InternalException($"{nameof(Left)} and {nameof(Right)} are both null"); }
            }
        }
        public int ParameterCount
        {
            get
            {
                int i = 0;
                if (Left != null) i++;
                if (Right != null) i++;
                return i;
            }
        }

        public override Position Position
            => new(Operator, Left, Right);

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

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
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

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (Left != null)
            {
                yield return Left;
                foreach (Statement substatement in Left)
                { yield return substatement; }
            }

            if (Right != null)
            {
                yield return Right;
                foreach (Statement substatement in Right)
                { yield return substatement; }
            }
        }
    }

    public class ShortOperatorCall : AnyAssignment, IReadableID
    {
        public readonly Token Operator;
        public readonly StatementWithValue Left;

        public StatementWithValue[] Parameters => new StatementWithValue[] { this.Left };

        public override Position Position
            => new(Operator, Left);

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

        public string ReadableID(Func<StatementWithValue, CompiledType> typeSearch)
        {
            StringBuilder result = new();

            result.Append(Operator.Content);

            result.Append('(');
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(typeSearch.Invoke(Parameters[i]).Name);
            }
            result.Append(')');

            return result.ToString();
        }

        public override Assignment ToAssignment()
        {
            switch (Operator.Content)
            {
                case "++":
                {
                    Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position);
                    one.SaveValue = true;

                    OperatorCall operatorCall = new(Token.CreateAnonymous("+", TokenType.Operator, Operator.Position), Left, one);

                    Token assignmentToken = Token.CreateAnonymous("=", TokenType.Operator, Operator.Position);

                    return new Assignment(assignmentToken, Left, operatorCall);
                }

                case "--":
                {
                    Literal one = Literal.CreateAnonymous(LiteralType.Integer, "1", Operator.Position);
                    one.SaveValue = true;

                    OperatorCall operatorCall = new(Token.CreateAnonymous("-", TokenType.Operator, Operator.Position), Left, one);

                    Token assignmentToken = Token.CreateAnonymous("=", TokenType.Operator, Operator.Position);

                    return new Assignment(assignmentToken, Left, operatorCall);
                }

                default: throw new NotImplementedException();
            }
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (Left != null)
            {
                yield return Left;
                foreach (Statement substatement in Left)
                { yield return substatement; }
            }
        }
    }

    public class Assignment : AnyAssignment
    {
        /// <summary>
        /// This should always be "="
        /// </summary>
        public readonly Token Operator;
        public readonly StatementWithValue Left;
        public readonly StatementWithValue Right;

        public override Position Position
            => new(Operator, Left, Right);

        public Assignment(Token @operator, StatementWithValue left, StatementWithValue right)
        {
            this.Operator = @operator;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString()
            => $"... {Operator} ...{Semicolon}";

        public override Assignment ToAssignment() => this;

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (Left != null)
            {
                yield return Left;
                foreach (Statement substatement in Left)
                { yield return substatement; }
            }

            if (Right != null)
            {
                yield return Right;
                foreach (Statement substatement in Right)
                { yield return substatement; }
            }
        }
    }

    public class CompoundAssignment : AnyAssignment
    {
        /// This should always starts with "="
        public readonly Token Operator;
        public readonly StatementWithValue Left;
        public readonly StatementWithValue Right;

        public override Position Position
            => new(Operator, Left, Right);

        public CompoundAssignment(Token @operator, StatementWithValue left, StatementWithValue right)
        {
            this.Operator = @operator;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString() => $"... {Operator} ...{Semicolon}";

        public override Assignment ToAssignment()
        {
            OperatorCall statementToAssign = new(Token.CreateAnonymous(Operator.Content.Replace("=", string.Empty, StringComparison.Ordinal), TokenType.Operator, Operator.Position), Left, Right);
            return new Assignment(Token.CreateAnonymous("=", TokenType.Operator, Operator.Position), Left, statementToAssign);
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (Left != null)
            {
                yield return Left;
                foreach (Statement substatement in Left)
                { yield return substatement; }
            }

            if (Right != null)
            {
                yield return Right;
                foreach (Statement substatement in Right)
                { yield return substatement; }
            }
        }
    }

    public class Literal : StatementWithValue
    {
        public readonly LiteralType Type;
        public readonly string Value;
        public Position ImaginaryPosition;
        public readonly Token ValueToken;

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

        public static Literal CreateAnonymous(LiteralType type, string value, IThingWithPosition position)
            => Literal.CreateAnonymous(type, value, position.Position);
        public static Literal CreateAnonymous(LiteralType type, string value, Position position)
        {
            TokenType tokenType = type switch
            {
                LiteralType.Integer => TokenType.LiteralNumber,
                LiteralType.Float => TokenType.LiteralFloat,
                LiteralType.Boolean => TokenType.Identifier,
                LiteralType.String => TokenType.LiteralString,
                LiteralType.Char => TokenType.LiteralCharacter,
                _ => TokenType.Identifier,
            };
            return new Literal(type, value, Token.CreateAnonymous(value, tokenType))
            {
                ImaginaryPosition = position,
            };
        }

        public override string ToString() => Type switch
        {
            LiteralType.String => $"\"{Value}\"",
            LiteralType.Char => $"'{Value}'",
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

        public override IEnumerator<Statement> GetEnumerator()
        { yield break; }
    }

    public class Identifier : StatementWithValue
    {
        public readonly Token Token;

        public string Content
        {
            [DebuggerStepThrough]
            get => Token.Content;
        }

        public override Position Position => Token.Position;

        public Identifier(Token token) => Token = token;

        public override string ToString() => Token.Content;

        public override IEnumerator<Statement> GetEnumerator()
        { yield break; }
    }

    public class AddressGetter : StatementWithValue
    {
        public readonly Token OperatorToken;
        public readonly StatementWithValue PrevStatement;

        public override Position Position
            => new(OperatorToken, PrevStatement);

        public AddressGetter(Token operatorToken, StatementWithValue prevStatement)
        {
            OperatorToken = operatorToken;
            PrevStatement = prevStatement;
        }

        public override string ToString()
            => $"{OperatorToken.Content}{PrevStatement}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (PrevStatement != null)
            {
                yield return PrevStatement;
                foreach (Statement substatement in PrevStatement)
                { yield return substatement; }
            }
        }
    }

    public class Pointer : StatementWithValue
    {
        public readonly Token OperatorToken;
        public readonly StatementWithValue PrevStatement;

        public override Position Position
            => new(OperatorToken, PrevStatement);

        public Pointer(Token operatorToken, StatementWithValue prevStatement)
        {
            OperatorToken = operatorToken;
            PrevStatement = prevStatement;
        }

        public override string ToString()
            => $"{OperatorToken.Content}{PrevStatement}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            if (PrevStatement != null)
            {
                yield return PrevStatement;
                foreach (Statement substatement in PrevStatement)
                { yield return substatement; }
            }
        }
    }

    public class WhileLoop : StatementWithBlock
    {
        public readonly Token Keyword;
        public readonly StatementWithValue Condition;

        public override Position Position
            => new(Keyword, Block);

        public WhileLoop(Token keyword, StatementWithValue condition, Block block)
            : base(block)
        {
            Keyword = keyword;
            Condition = condition;
        }

        public override string ToString()
            => $"{Keyword} ({Condition}) {Block}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Condition;
            foreach (Statement substatement in Condition)
            { yield return substatement; }

            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }
        }
    }

    public class ForLoop : StatementWithBlock
    {
        public readonly Token Keyword;
        public readonly VariableDeclaration VariableDeclaration;
        public readonly StatementWithValue Condition;
        public readonly AnyAssignment Expression;

        public override Position Position
            => new(Keyword, Block);

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

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return VariableDeclaration;
            foreach (Statement substatement in VariableDeclaration)
            { yield return substatement; }

            yield return Condition;
            foreach (Statement substatement in Condition)
            { yield return substatement; }

            yield return Expression;
            foreach (Statement substatement in Expression)
            { yield return substatement; }

            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }
        }
    }

    public class IfContainer : Statement
    {
        public readonly BaseBranch[] Parts;

        public override Position Position
            => new(Parts);

        public IfContainer(IEnumerable<BaseBranch> parts)
        {
            Parts = parts.ToArray();
        }

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

        public LinkedIf ToLinks()
        {
            if (Parts.Length == 0) throw new InternalException();
            if (Parts[0] is not IfBranch ifBranch) throw new InternalException();
            return new LinkedIf(ifBranch.Keyword, ifBranch.Condition, ifBranch.Block)
            {
                NextLink = ToLinks(1),
            };
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            foreach (BaseBranch part in Parts)
            {
                yield return part;
                foreach (Statement substatement in part)
                { yield return substatement; }
            }
        }
    }

    public abstract class BaseBranch : StatementWithAnyBlock
    {
        public readonly Token Keyword;
        public readonly IfPart Type;

        public override Position Position
            => new(Keyword, Block);

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
        public readonly StatementWithValue Condition;

        public IfBranch(Token keyword, StatementWithValue condition, Statement block)
            : base(keyword, IfPart.If, block)
        {
            this.Condition = condition;
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Condition;
            foreach (Statement substatement in Condition)
            { yield return substatement; }

            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }
        }
    }

    public class ElseIfBranch : BaseBranch
    {
        public readonly StatementWithValue Condition;

        public ElseIfBranch(Token keyword, StatementWithValue condition, Statement block)
            : base(keyword, IfPart.ElseIf, block)
        {
            this.Condition = condition;
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Condition;
            foreach (Statement substatement in Condition)
            { yield return substatement; }

            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }
        }
    }

    public class ElseBranch : BaseBranch
    {
        public ElseBranch(Token keyword, Statement block)
            : base(keyword, IfPart.Else, block)
        { }

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Block;
            foreach (Statement substatement in Block)
            { yield return substatement; }
        }
    }

    public class NewInstance : StatementWithValue
    {
        public readonly Token Keyword;
        public readonly TypeInstance TypeName;

        public override Position Position
            => new(Keyword, TypeName);

        public NewInstance(Token keyword, TypeInstance typeName)
        {
            Keyword = keyword;
            TypeName = typeName;
        }

        public override string ToString()
            => $"{Keyword} {TypeName}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        { yield break; }
    }

    public class ConstructorCall : StatementWithValue, IReadableID
    {
        public readonly Token Keyword;
        public readonly TypeInstance TypeName;

        public readonly StatementWithValue[] Parameters = Array.Empty<StatementWithValue>();

        public readonly Token BracketLeft;
        public readonly Token BracketRight;

        public override Position Position
            => new Position(Keyword, TypeName, BracketLeft, BracketRight).Union(Parameters);

        public ConstructorCall(Token keyword, TypeInstance typeName, Token bracketLeft, IEnumerable<StatementWithValue> parameters, Token bracketRight)
        {
            Keyword = keyword;
            TypeName = typeName;
            BracketLeft = bracketLeft;
            Parameters = parameters.ToArray();
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

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            StringBuilder result = new();
            result.Append(TypeName.ToString());
            result.Append('.');
            result.Append(Keyword.Content);
            result.Append(BracketLeft.ToString());
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) result.Append(", ");

                result.Append(TypeSearch.Invoke(Parameters[i]).Name);
            }
            result.Append(BracketRight.ToString());

            return result.ToString();
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            foreach (StatementWithValue parameter in Parameters)
            {
                yield return parameter;
                foreach (Statement substatement in parameter)
                { yield return substatement; }
            }
        }
    }

    public class IndexCall : StatementWithValue, IReadableID
    {
        public readonly StatementWithValue PrevStatement;

        public readonly StatementWithValue Expression;
        public readonly Token BracketLeft;
        public readonly Token BracketRight;

        public override Position Position
            => new(PrevStatement, Expression);

        public IndexCall(StatementWithValue prevStatement, Token bracketLeft, StatementWithValue indexStatement, Token bracketRight)
        {
            this.PrevStatement = prevStatement;
            this.Expression = indexStatement;
            this.BracketLeft = bracketLeft;
            this.BracketRight = bracketRight;
        }

        public override string ToString()
            => $"{PrevStatement}{BracketLeft}{Expression}{BracketRight}{Semicolon}";

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            StringBuilder result = new(2);

            if (PrevStatement != null)
            { result.Append(TypeSearch.Invoke(this.PrevStatement).ToString()); }
            else
            { result.Append('?'); }

            result.Append(BracketLeft.ToString());
            result.Append(TypeSearch.Invoke(this.Expression).ToString());
            result.Append(BracketRight.ToString());

            return result.ToString();
        }

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return PrevStatement;
            foreach (Statement substatement in PrevStatement)
            { yield return substatement; }

            yield return Expression;
            foreach (Statement substatement in Expression)
            { yield return substatement; }
        }
    }

    public class Field : StatementWithValue
    {
        public readonly Token FieldName;
        public readonly StatementWithValue PrevStatement;

        public override Position Position
            => new(PrevStatement, FieldName);

        public Field(StatementWithValue prevStatement, Token fieldName)
        {
            PrevStatement = prevStatement;
            FieldName = fieldName;
        }

        public override string ToString()
            => $"{PrevStatement}.{FieldName}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return PrevStatement;
            foreach (Statement substatement in PrevStatement)
            { yield return substatement; }
        }
    }

    public class TypeCast : StatementWithValue
    {
        public readonly StatementWithValue PrevStatement;
        public readonly Token Keyword;
        public readonly TypeInstance Type;

        public override Position Position
            => new(PrevStatement, Keyword, Type);

        public TypeCast(StatementWithValue prevStatement, Token keyword, TypeInstance type)
        {
            this.PrevStatement = prevStatement;
            this.Keyword = keyword;
            this.Type = type;
        }

        public override string ToString()
            => $"{PrevStatement} {Keyword} {Type}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return PrevStatement;
            foreach (Statement substatement in PrevStatement)
            { yield return substatement; }
        }
    }

    public class ModifiedStatement : StatementWithValue
    {
        public readonly StatementWithValue Statement;
        public readonly Token Modifier;

        public override Position Position
            => new(Modifier, Statement);

        public ModifiedStatement(Token modifier, StatementWithValue statement)
        {
            this.Statement = statement;
            this.Modifier = modifier;
        }

        public override string ToString()
            => $"{Modifier} {Statement}{Semicolon}";

        public override IEnumerator<Statement> GetEnumerator()
        {
            yield return Statement;
            foreach (Statement substatement in Statement)
            { yield return substatement; }
        }
    }
}
