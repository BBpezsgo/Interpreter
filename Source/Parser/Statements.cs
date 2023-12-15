using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace LanguageCore.Parser.Statement
{
    using System.Diagnostics;
    using Compiler;
    using Tokenizing;

    struct Stringify
    {
        public const int CozyLength = 30;
    }

    public static class StatementFinder
    {
        public static bool GetAllStatement(Statement? st, Func<Statement, bool>? callback)
        {
            if (st == null) return false;
            if (callback == null) return false;

            if (callback.Invoke(st) == true) return true;

            IEnumerable<Statement> statements = st.GetStatements();
            return GetAllStatement(statements, callback);
        }
        public static bool GetAllStatement(IEnumerable<Statement>? statements, Func<Statement, bool>? callback)
        {
            if (statements is null) return false;
            if (callback == null) return false;

            foreach (Statement statement in statements)
            {
                if (GetAllStatement(statement, callback)) return true;
            }
            return false;
        }
        public static bool GetAllStatement(Statement[]? statements, Func<Statement, bool>? callback)
        {
            if (statements is null) return false;
            if (callback == null) return false;

            for (int i = 0; i < statements.Length; i++)
            {
                if (GetAllStatement(statements[i], callback))
                {
                    return true;
                }
            }
            return false;
        }
        public static void GetAllStatement(ParserResult parserResult, Func<Statement, bool>? callback)
        {
            if (callback == null) return;

            for (int i = 0; i < parserResult.TopLevelStatements.Length; i++)
            {
                if (parserResult.TopLevelStatements[i] == null) continue;
                GetAllStatement(parserResult.TopLevelStatements[i], callback);
            }

            for (int i = 0; i < parserResult.Functions.Length; i++)
            {
                GetAllStatement(parserResult.Functions[i].Block?.Statements, callback);
            }
        }
        public static void GetAllStatement(IEnumerable<FunctionDefinition>? functions, Func<Statement, bool>? callback)
        {
            if (callback == null) return;
            if (functions == null) return;

            foreach (FunctionDefinition item in functions)
            { GetAllStatement(item.Block?.Statements, callback); }
        }
        public static void GetAllStatement(IEnumerable<GeneralFunctionDefinition>? functions, Func<Statement, bool>? callback)
        {
            if (callback == null) return;
            if (functions == null) return;

            foreach (GeneralFunctionDefinition item in functions)
            { GetAllStatement(item.Block?.Statements, callback); }
        }
    }

    public interface IReadableID
    {
        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch);
    }

    public abstract class Statement : IThingWithPosition
    {
        public Token? Semicolon;

        public override string ToString()
            => $"{GetType().Name}{Semicolon}";

        public abstract Position Position { get; }

        public virtual IEnumerable<Statement> GetStatements()
        { yield break; }
    }

    public abstract class AnyAssignment : Statement
    {
        public abstract Assignment ToAssignment();
    }

    public abstract class StatementWithValue : Statement
    {
        public bool SaveValue = true;
    }

    public class Block : Statement
    {
        public readonly Statement[] Statements;

        public readonly Token BracketStart;
        public readonly Token BracketEnd;

        public Block(Token bracketStart, IEnumerable<Statement> statements, Token bracketEnd)
        {
            this.BracketStart = bracketStart;
            this.Statements = statements.ToArray();
            this.BracketEnd = bracketEnd;
        }

        public override Position Position
            => new(BracketStart, BracketEnd);

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

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Statements.Length; i++)
            { yield return Statements[i]; }
        }
    }

    public abstract class StatementWithBlock : Statement
    {
        public readonly Block Block;

        protected StatementWithBlock(Block block)
        {
            Block = block;
        }
    }

    public abstract class StatementWithAnyBlock : Statement
    {
        public readonly Statement Block;

        protected StatementWithAnyBlock(Statement block)
        {
            Block = block;
        }
    }

    public abstract class LinkedIfThing : StatementWithAnyBlock
    {
        public readonly Token Keyword;

        public LinkedIfThing(Token keyword, Statement block) : base(block)
        {
            Keyword = keyword;
        }
    }

    public class LinkedIf : LinkedIfThing
    {
        public readonly StatementWithValue Condition;
        /// <summary>
        /// Can be:
        /// <list type="bullet">
        /// <item><see cref="LinkedIf"/> (else if)</item>
        /// <item><see cref="LinkedElse"/></item>
        /// <item><see langword="null"/></item>
        /// </list>
        /// </summary>
        public LinkedIfThing? NextLink;

        public LinkedIf(Token keyword, StatementWithValue condition, Statement block) : base(keyword, block)
        {
            Condition = condition;
        }

        public override Position Position
            => new(Keyword, Condition, Block);

        public override string ToString()
            => $"{Keyword} ({Condition}) {Block}{(NextLink != null ? " ..." : string.Empty)}{Semicolon}";
    }

    public class LinkedElse : LinkedIfThing
    {
        public LinkedElse(Token keyword, Statement block) : base(keyword, block)
        {

        }

        public override Position Position
            => new(Keyword, Block);
    }

    public class CompileTag : Statement, IDefinition
    {
        public readonly Token HashToken;
        public readonly Token HashName;
        public readonly Literal[] Parameters;

        public string? FilePath { get; set; }

        public CompileTag(Token hashToken, Token hashName, Literal[] parameters)
        {
            HashToken = hashToken;
            HashName = hashName;
            Parameters = parameters;
        }

        public override string ToString()
            => $"{HashToken}{HashName}{(Parameters.Length > 0 ? string.Join<Literal>(' ', Parameters) : string.Empty)}{Semicolon}";

        public override Position Position
            => new Position(HashToken, HashName).Union(Parameters);
    }

    public class LiteralList : StatementWithValue
    {
        public readonly Token BracketLeft;
        public readonly Token BracketRight;
        public readonly StatementWithValue[] Values;

        public int Size => Values.Length;

        public LiteralList(Token bracketLeft, StatementWithValue[] values, Token bracketRight)
        {
            BracketLeft = bracketLeft;
            BracketRight = bracketRight;
            Values = values;
        }

        public override Position Position
            => new(BracketLeft, BracketRight);

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Values.Length; i++)
            { yield return Values[i]; }
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

        public VariableDeclaration(Token[] modifiers, TypeInstance type, Token variableName, StatementWithValue? initialValue)
        {
            Type = type;
            VariableName = variableName;
            InitialValue = initialValue;
            Modifiers = modifiers;
        }

        public override string ToString()
            => $"{string.Join<Token>(' ', Modifiers)} {Type} {VariableName}{((InitialValue != null) ? " = ..." : string.Empty)}{Semicolon}".TrimStart();

        public override Position Position
            => new Position(Type, VariableName, InitialValue).Union(Modifiers);

        public override IEnumerable<Statement> GetStatements()
        {
            if (InitialValue != null) yield return InitialValue;
        }
    }

    public class AnyCall : StatementWithValue, IReadableID
    {
        public readonly StatementWithValue PrevStatement;
        public readonly Token BracketLeft;
        public readonly StatementWithValue[] Parameters;
        public readonly Token BracketRight;

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

        public override Position Position
            => new(PrevStatement, BracketLeft, BracketRight);

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

        public override IEnumerable<Statement> GetStatements()
        {
            if (PrevStatement != null) yield return PrevStatement;
            for (int i = 0; i < Parameters.Length; i++)
            { yield return Parameters[i]; }
        }

        public bool IsFunctionCall => PrevStatement is Identifier;
        public bool IsMethodCall => PrevStatement is Field;
        public bool IsFunctionOrMethodCall => IsFunctionCall || IsMethodCall;

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
            string paramsString = "";
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) paramsString += ", ";
                paramsString += Parameters[i].ToString();
                if (paramsString.Length >= 10 && i - 1 != Parameters.Length)
                {
                    paramsString += ", ...";
                    break;
                }
            }
            string result = "";
            if (PrevStatement != null)
            { result += $"{PrevStatement}."; }
            result += $"{FunctionName}({paramsString})";
            return result;
        }

        public override Position Position
            => new Position(BracketLeft, BracketRight, Identifier).Union(MethodParameters);

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            string result = "";
            if (this.PrevStatement != null)
            {
                result += TypeSearch.Invoke(this.PrevStatement).ToString();
                result += ".";
            }
            result += this.FunctionName;
            result += "(";
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (i > 0) { result += ", "; }
                result += TypeSearch.Invoke(this.Parameters[i]).ToString();
            }
            result += ")";
            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            if (PrevStatement != null) yield return PrevStatement;
            for (int i = 0; i < Parameters.Length; i++)
            { yield return Parameters[i]; }
        }
    }

    public class KeywordCall : StatementWithValue, IReadableID
    {
        public readonly Token Identifier;
        public readonly StatementWithValue[] Parameters;

        public string FunctionName => Identifier.Content;

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

        public override Position Position
            => new Position(Identifier).Union(Parameters);

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            string result = "";
            result += this.Identifier.Content;
            result += "(";
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (i > 0) { result += ", "; }

                result += TypeSearch.Invoke(this.Parameters[i]).Name;
            }
            result += ")";

            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Parameters.Length; i++)
            { yield return Parameters[i]; }
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
                StatementWithValue? left = this.Left;
                StatementWithValue? right = this.Right;

                if (left is null && right is not null)
                { return new StatementWithValue[] { right }; }
                else if (left is not null && right is null)
                { return new StatementWithValue[] { left }; }
                else if (left is not null && right is not null)
                { return new StatementWithValue[] { left, right }; }
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

        public OperatorCall(Token op, StatementWithValue left) : this(op, left, null) { }
        public OperatorCall(Token op, StatementWithValue left, StatementWithValue? right)
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

        public override Position Position
            => new(Operator, Left, Right);

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

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
            if (Right != null) yield return Right;
        }
    }

    public class ShortOperatorCall : AnyAssignment, IReadableID
    {
        public readonly Token Operator;
        public readonly StatementWithValue Left;

        public StatementWithValue[] Parameters => new StatementWithValue[] { this.Left };
        public int ParameterCount => 1;

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

        public override Position Position
            => new(Operator, Left);
        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            string result = this.Operator.Content;
            result += "(";
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (i > 0) { result += ", "; }

                result += TypeSearch.Invoke(this.Parameters[i]).Name;
            }
            result += ")";

            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
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
    }

    public class Assignment : AnyAssignment
    {
        /// <summary>
        /// This should always be "="
        /// </summary>
        public readonly Token Operator;
        public readonly StatementWithValue Left;
        public readonly StatementWithValue Right;

        public Assignment(Token @operator, StatementWithValue left, StatementWithValue right)
        {
            this.Operator = @operator;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString()
            => $"... {Operator} ...{Semicolon}";

        public override Position Position
            => new(Operator, Left, Right);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
            yield return Right;
        }

        public override Assignment ToAssignment() => this;
    }

    public class CompoundAssignment : AnyAssignment
    {
        /// This should always starts with "="
        public readonly Token Operator;
        public readonly StatementWithValue Left;
        public readonly StatementWithValue Right;

        public CompoundAssignment(Token @operator, StatementWithValue left, StatementWithValue right)
        {
            this.Operator = @operator;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString() => $"... {Operator} ...{Semicolon}";

        public override Position Position
            => new(Operator, Left, Right);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
            yield return Right;
        }

        public override Assignment ToAssignment()
        {
            OperatorCall statementToAssign = new(Token.CreateAnonymous(Operator.Content.Replace("=", ""), TokenType.Operator, Operator.Position), Left, Right);
            return new Assignment(Token.CreateAnonymous("=", TokenType.Operator, Operator.Position), Left, statementToAssign);
        }
    }

    public class Literal : StatementWithValue
    {
        public readonly LiteralType Type;
        public readonly string Value;
        /// <summary>
        /// If there is no <c>ValueToken</c>:<br/>
        /// i.e in <c>i++</c> statement
        /// </summary>
        public Position ImaginaryPosition;
        public readonly Token ValueToken;

        public Literal(LiteralType type, string value, Token valueToken)
        {
            Type = type;
            Value = value;
            ValueToken = valueToken;
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
            LiteralType.Integer => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.Float => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.Boolean => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.String => $"\"{Value}\"",
            LiteralType.Char => $"'{Value}'",
            _ => throw new ImpossibleException(),
        };

        public static int GetInt(string value)
        {
            value = value.Trim();
            int @base = 10;
            if (value.StartsWith("0b"))
            {
                value = value[2..];
                @base = 2;
            }
            if (value.StartsWith("0x"))
            {
                value = value[2..];
                @base = 16;
            }
            value = value.Replace("_", "");
            return Convert.ToInt32(value, @base);
        }
        public static float GetFloat(string value)
        {
            value = value.Trim();
            value = value.Replace("_", "");
            value = value.EndsWith('f') ? value[..^1] : value;
            return float.Parse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
        }

        public int GetInt() => Literal.GetInt(Value);
        public float GetFloat() => Literal.GetFloat(Value);

        public override Position Position
            => ValueToken is null ? ImaginaryPosition : new Position(ValueToken);
    }

    public class Identifier : StatementWithValue
    {
        public readonly Token Token;

        public string Content
        {
            [DebuggerStepThrough]
            get => Token.Content;
        }

        public Identifier(Token token) => Token = token;

        public override string ToString() => Token.Content;

        public override Position Position => Token.Position;
    }

    public class AddressGetter : StatementWithValue
    {
        public readonly Token OperatorToken;
        public readonly StatementWithValue PrevStatement;

        public AddressGetter(Token operatorToken, StatementWithValue prevStatement)
        {
            OperatorToken = operatorToken;
            PrevStatement = prevStatement;
        }

        public override string ToString()
            => $"{OperatorToken.Content}{PrevStatement}{Semicolon}";

        public override Position Position
            => new(OperatorToken, PrevStatement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    public class Pointer : StatementWithValue
    {
        public readonly Token OperatorToken;
        public readonly StatementWithValue PrevStatement;

        public Pointer(Token operatorToken, StatementWithValue prevStatement)
        {
            OperatorToken = operatorToken;
            PrevStatement = prevStatement;
        }

        public override string ToString()
            => $"{OperatorToken.Content}{PrevStatement}{Semicolon}";

        public override Position Position
            => new(OperatorToken, PrevStatement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    public class WhileLoop : StatementWithBlock
    {
        public readonly Token Keyword;
        public readonly StatementWithValue Condition;

        public WhileLoop(Token keyword, StatementWithValue condition, Block block)
            : base(block)
        {
            Keyword = keyword;
            Condition = condition;
        }

        public override string ToString()
            => $"{Keyword} ({Condition}) {Block}{Semicolon}";

        public override Position Position
            => new(Keyword, Block);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Condition;
            yield return Block;
        }
    }

    public class ForLoop : StatementWithBlock
    {
        public readonly Token Keyword;
        public readonly VariableDeclaration VariableDeclaration;
        public readonly StatementWithValue Condition;
        public readonly AnyAssignment Expression;

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

        public override Position Position
            => new(Keyword, Block);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return VariableDeclaration;
            yield return Condition;
            yield return Expression;
            yield return Block;
        }
    }

    public class IfContainer : Statement
    {
        public readonly BaseBranch[] Parts;

        public IfContainer(IEnumerable<BaseBranch> parts)
        {
            Parts = parts.ToArray();
        }

        public override Position Position
            => new(Parts);

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                yield return Parts[i];
            }
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
    }

    public abstract class BaseBranch : StatementWithAnyBlock
    {
        public readonly Token Keyword;
        public readonly IfPart Type;

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

        public override Position Position
            => new(Keyword, Block);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Block;
        }
    }

    public class IfBranch : BaseBranch
    {
        public readonly StatementWithValue Condition;

        public IfBranch(Token keyword, StatementWithValue condition, Statement block)
            : base(keyword, IfPart.If, block)
        {
            this.Condition = condition;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Condition;
            yield return Block;
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

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Condition;
            yield return Block;
        }
    }

    public class ElseBranch : BaseBranch
    {
        public ElseBranch(Token keyword, Statement block)
            : base(keyword, IfPart.Else, block)
        { }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Block;
        }
    }

    public class NewInstance : StatementWithValue
    {
        public readonly Token Keyword;
        public readonly TypeInstance TypeName;

        public NewInstance(Token keyword, TypeInstance typeName)
        {
            Keyword = keyword;
            TypeName = typeName;
        }

        public override string ToString()
            => $"{Keyword} {TypeName}{Semicolon}";
        public override Position Position
            => new(Keyword, TypeName);
    }

    public class ConstructorCall : StatementWithValue, IReadableID
    {
        public readonly Token Keyword;
        public readonly TypeInstance TypeName;

        public readonly StatementWithValue[] Parameters = Array.Empty<StatementWithValue>();

        public readonly Token BracketLeft;
        public readonly Token BracketRight;

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
        public override Position Position
            => new Position(Keyword, TypeName, BracketLeft, BracketRight).Union(Parameters);

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            string result = "";
            result += TypeName.ToString();
            result += ".";
            result += this.Keyword.Content;
            result += "(";
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (i > 0) { result += ", "; }

                result += TypeSearch.Invoke(this.Parameters[i]).Name;
            }
            result += ")";

            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Parameters.Length; i++)
            { yield return Parameters[i]; }
        }
    }

    public class IndexCall : StatementWithValue, IReadableID
    {
        public readonly StatementWithValue PrevStatement;

        public readonly StatementWithValue Expression;
        public readonly Token BracketLeft;
        public readonly Token BracketRight;

        public IndexCall(StatementWithValue prevStatement, Token bracketLeft, StatementWithValue indexStatement, Token bracketRight)
        {
            this.PrevStatement = prevStatement;
            this.Expression = indexStatement;
            this.BracketLeft = bracketLeft;
            this.BracketRight = bracketRight;
        }

        public override string ToString()
            => $"{PrevStatement}{BracketLeft}{Expression}{BracketRight}{Semicolon}";

        public override Position Position
            => new(PrevStatement, Expression);

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

        public override IEnumerable<Statement> GetStatements()
        {
            if (PrevStatement != null) yield return PrevStatement;
            yield return Expression;
        }
    }

    public class Field : StatementWithValue
    {
        public readonly Token FieldName;
        public readonly StatementWithValue PrevStatement;

        public Field(StatementWithValue prevStatement, Token fieldName)
        {
            PrevStatement = prevStatement;
            FieldName = fieldName;
        }

        public override string ToString()
            => $"{PrevStatement}.{FieldName}{Semicolon}";

        public override Position Position
            => new(PrevStatement, FieldName);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    public class TypeCast : StatementWithValue
    {
        public readonly StatementWithValue PrevStatement;
        public readonly Token Keyword;
        public readonly TypeInstance Type;

        public TypeCast(StatementWithValue prevStatement, Token keyword, TypeInstance type)
        {
            this.PrevStatement = prevStatement;
            this.Keyword = keyword;
            this.Type = type;
        }

        public override string ToString()
            => $"{PrevStatement} {Keyword} {Type}{Semicolon}";

        public override Position Position
            => new(PrevStatement, Keyword, Type);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    public class ModifiedStatement : StatementWithValue
    {
        public readonly StatementWithValue Statement;
        public readonly Token Modifier;

        public ModifiedStatement(Token modifier, StatementWithValue statement)
        {
            this.Statement = statement;
            this.Modifier = modifier;
        }

        public override string ToString()
            => $"{Modifier} {Statement}{Semicolon}";

        public override Position Position
            => new(Modifier, Statement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Statement;
        }
    }
}
