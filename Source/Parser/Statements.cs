using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LanguageCore.Parser.Statement
{
    using LanguageCore.BBCode.Compiler;
    using LanguageCore.Tokenizing;

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
            => this.GetType().Name;

        public abstract string PrettyPrint(int ident = 0);
        public abstract Position GetPosition();

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
        public readonly List<Statement> Statements;

        public readonly Token BracketStart;
        public readonly Token BracketEnd;

        public Block(Token bracketStart, IEnumerable<Statement> statements, Token bracketEnd)
        {
            this.BracketStart = bracketStart;
            this.Statements = new(statements);
            this.BracketEnd = bracketEnd;
        }

        public override Position GetPosition()
            => new(BracketStart, BracketEnd);

        public override string PrettyPrint(int ident = 0)
        {
            string result = "{\n";

            foreach (Statement statement in Statements)
            { result += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n"; }

            result += $"{" ".Repeat(ident)}}}";
            return result;
        }

        public override string ToString()
        {
            StringBuilder result = new(3);
            result.Append('{');
            if (Statements.Count > 0) result.Append("...");
            else result.Append(' ');
            result.Append('}');
            return result.ToString();
        }

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Statements.Count; i++)
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

    public abstract class LinkedIfThing : StatementWithBlock
    {
        public readonly Token Keyword;

        public LinkedIfThing(Token keyword, Block block) : base(block)
        {
            Keyword = keyword;
        }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
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

        public LinkedIf(Token keyword, StatementWithValue condition, Block block) : base(keyword, block)
        {
            Condition = condition;
        }

        public override Position GetPosition()
            => new(Keyword, Condition, Block);

        public override string PrettyPrint(int ident = 0)
        { throw new NotImplementedException(); }

        public override string ToString() => $"{Keyword} ({Condition}) {{ ... }} {(NextLink != null ? "..." : ";")}";
        string GetDebuggerDisplay() => ToString();
    }

    public class LinkedElse : LinkedIfThing
    {
        public LinkedElse(Token keyword, Block block) : base(keyword, block)
        {

        }

        public override Position GetPosition()
            => new(Keyword, Block);

        public override string PrettyPrint(int ident = 0)
        { throw new NotImplementedException(); }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"{HashToken}{HashName}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{HashToken}{HashName} ...";
        }

        public override Position GetPosition()
            => new Position(HashToken, HashName).Extend(Parameters);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}[{string.Join(", ", Values.ToList())}]";
        }

        public override Position GetPosition()
            => new(BracketLeft, BracketRight);

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Values.Length; i++)
            { yield return Values[i]; }
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"{string.Join<Token>(' ', Modifiers)} {Type} {VariableName}{((InitialValue != null) ? " = ..." : "")}".Trim();
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{string.Join<Token>(' ', Modifiers)} {Type} {VariableName}{((InitialValue != null) ? $" = {InitialValue.PrettyPrint()}" : "")}";
        }

        public override Position GetPosition()
            => new(Type, VariableName, InitialValue);

        public override IEnumerable<Statement> GetStatements()
        {
            if (InitialValue != null) yield return InitialValue;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class FunctionCall : StatementWithValue, IReadableID
    {
        public readonly Token Identifier;
        public readonly StatementWithValue[] Parameters;
        public readonly StatementWithValue? PrevStatement;

        public readonly Token BracketLeft;
        public readonly Token BracketRight;

        internal string FunctionName => Identifier.Content;
        internal bool IsMethodCall => PrevStatement != null;
        internal StatementWithValue[] MethodParameters
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

        public override string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var arg in this.Parameters)
            {
                parameters.Add(arg.PrettyPrint());
            }
            return $"{" ".Repeat(ident)}{FunctionName}({(string.Join(", ", parameters))})";
        }

        public override Position GetPosition()
            => new Position(BracketLeft, BracketRight, Identifier).Extend(MethodParameters);

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

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
            string result = "";
            result += FunctionName;
            result += " ";

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
            result += paramsString;

            return result;
        }

        public override string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var arg in this.Parameters)
            {
                parameters.Add(arg.PrettyPrint());
            }
            return $"{" ".Repeat(ident)}{FunctionName} {string.Join(" ", parameters)}";
        }

        public override Position GetPosition()
            => new Position(Identifier).Extend(Parameters);

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

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
            string result = "";
            if (Left != null)
            {
                if (Left.ToString().Length <= 50)
                { result += Left.ToString(); }
                else
                { result += "..."; }

                result += $" {Operator}";

                if (Right != null)
                {
                    if (Right.ToString().Length <= 50)
                    { result += " " + Right.ToString(); }
                    else
                    { result += " ..."; }
                }
            }
            else
            { result = $"{Operator}"; }

            if (InsideBracelet)
            { return $"({result})"; }
            else
            { return result; }
        }

        public override string PrettyPrint(int ident = 0)
        {
            string v;
            if (Left != null)
            {
                if (Right != null)
                {
                    v = $"{Left.PrettyPrint()} {Operator} {Right.PrettyPrint()}";
                }
                else
                {
                    v = $"{Left.PrettyPrint()} {Operator}";
                }
            }
            else
            {
                v = $"{Operator}";
            }
            if (InsideBracelet)
            {
                return $"{" ".Repeat(ident)}({v})";
            }
            else
            {
                return $"{" ".Repeat(ident)}({v})";
            }
        }

        public override Position GetPosition()
            => new(Operator, Left, Right);
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
            if (Right != null) yield return Right;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
            string result = "";
            if (Left != null)
            {
                if (Left.ToString().Length <= 50)
                { result += Left.ToString(); }
                else
                { result += "..."; }

                result += $" {Operator}";
            }
            else
            { result = $"{Operator}"; }

            return result;
        }

        public override string PrettyPrint(int ident = 0)
        {
            string v;
            if (Left != null)
            {
                v = $"{Left.PrettyPrint()} {Operator}";
            }
            else
            {
                v = $"{Operator}";
            }

            return $"{" ".Repeat(ident)}({v})";
        }

        public override Position GetPosition()
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
                        Literal one = Literal.CreateAnonymous(LiteralType.INT, "1", Operator.GetPosition());
                        one.SaveValue = true;

                        OperatorCall operatorCall = new(Token.CreateAnonymous("+", TokenType.OPERATOR), Left, one);

                        Token assignmentToken = new(TokenType.OPERATOR, "=", true)
                        {
                            AbsolutePosition = Operator.AbsolutePosition,
                            Position = Operator.Position,
                        };

                        return new Assignment(assignmentToken, Left, operatorCall);
                    }

                case "--":
                    {
                        Literal one = Literal.CreateAnonymous(LiteralType.INT, "1", Operator.GetPosition());
                        one.SaveValue = true;

                        OperatorCall operatorCall = new(Token.CreateAnonymous("-", TokenType.OPERATOR), Left, one);

                        Token assignmentToken = new(TokenType.OPERATOR, "=", true)
                        {
                            AbsolutePosition = Operator.AbsolutePosition,
                            Position = Operator.Position,
                        };

                        return new Assignment(assignmentToken, Left, operatorCall);
                    }

                default: throw new NotImplementedException();
            }
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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

        public override string ToString() => $"... {Operator} ...";

        public override string PrettyPrint(int ident = 0) => $"{Left.PrettyPrint()} {Operator} {Right.PrettyPrint()}";

        public override Position GetPosition()
            => new(Operator, Left, Right);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
            yield return Right;
        }

        public override Assignment ToAssignment() => this;
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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

        public override string ToString() => $"... {Operator} ...";

        public override string PrettyPrint(int ident = 0) => $"{Left.PrettyPrint()} {Operator} {Right.PrettyPrint()}";

        public override Position GetPosition()
            => new(Operator, Left, Right);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
            yield return Right;
        }

        public override Assignment ToAssignment()
        {
            OperatorCall statementToAssign = new(new Token(TokenType.OPERATOR, Operator.Content.Replace("=", ""), true)
            {
                AbsolutePosition = Operator.AbsolutePosition,
                Position = Operator.Position,
            }, Left, Right);

            return new Assignment(new Token(TokenType.OPERATOR, "=", true)
            {
                AbsolutePosition = Operator.AbsolutePosition,
                Position = Operator.Position,
            }, Left, statementToAssign);
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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

        public static Literal CreateAnonymous(LiteralType type, string value, Position position)
        {
            TokenType tokenType = type switch
            {
                LiteralType.INT => TokenType.LITERAL_NUMBER,
                LiteralType.FLOAT => TokenType.LITERAL_FLOAT,
                LiteralType.BOOLEAN => TokenType.IDENTIFIER,
                LiteralType.STRING => TokenType.LITERAL_STRING,
                LiteralType.CHAR => TokenType.LITERAL_CHAR,
                _ => TokenType.IDENTIFIER,
            };
            return new Literal(type, value, Token.CreateAnonymous(value, tokenType))
            {
                ImaginaryPosition = position,
            };
        }

        public override string ToString() => Type switch
        {
            LiteralType.INT => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.FLOAT => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.BOOLEAN => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.STRING => $"\"{Value}\"",
            LiteralType.CHAR => $"'{Value}'",
            _ => throw new ImpossibleException(),
        };

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{Value}";
        }

        public object? TryGetValue()
            => Type switch
            {
                LiteralType.INT => int.Parse(Value),
                LiteralType.FLOAT => float.Parse(Value),
                LiteralType.BOOLEAN => bool.Parse(Value),
                _ => null,
            };

        public override Position GetPosition()
            => ValueToken == null
                ? ImaginaryPosition
                : new Position(ValueToken);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Identifier : StatementWithValue
    {
        public readonly Token Name;
        public string Content => Name.Content;

        public Identifier(Token identifier)
        {
            Name = identifier;
        }

        public override string ToString() => Name.Content;

        public override string PrettyPrint(int ident = 0)
            => $"{" ".Repeat(ident)}{Name.Content}";

        public override Position GetPosition()
            => new(Name);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"{OperatorToken.Content}{PrevStatement}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.Content}{PrevStatement.PrettyPrint(0)}";
        }

        public override Position GetPosition()
            => new(OperatorToken, PrevStatement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"{OperatorToken.Content}{PrevStatement}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.Content}{PrevStatement.PrettyPrint(0)}";
        }

        public override Position GetPosition()
            => new(OperatorToken, PrevStatement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"while (...) {{...}};";
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}while ({Condition.PrettyPrint()})";
            x += Block.PrettyPrint();
            return x;
        }

        public override Position GetPosition()
            => new(Keyword, Block);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Condition;
            yield return Block;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"for (...) {{...}};";
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}for ({VariableDeclaration.PrettyPrint()}; {Condition.PrettyPrint()}; {Expression.PrettyPrint()})";
            x += Block.PrettyPrint();
            return x;
        }

        public override Position GetPosition()
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

        public override string PrettyPrint(int ident = 0)
        {
            var x = "";
            foreach (var part in Parts)
            {
                x += $"{part.PrettyPrint(ident)}\n";
            }
            if (x.EndsWith("\n", StringComparison.InvariantCulture))
            {
                x = x[..^1];
            }
            return x;
        }

        public override Position GetPosition()
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

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public abstract class BaseBranch : StatementWithBlock
    {
        public readonly Token Keyword;
        public readonly IfPart Type;

        protected BaseBranch(Token keyword, IfPart type, Block block)
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
        {
            return $"{Keyword} {((Type != IfPart.Else) ? "(...)" : "")} {{...}}";
        }

        public override Position GetPosition()
            => new(Keyword, Block);

        public abstract override string PrettyPrint(int ident = 0);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Block;
        }
    }

    public class IfBranch : BaseBranch
    {
        public readonly StatementWithValue Condition;

        public IfBranch(Token keyword, StatementWithValue condition, Block block)
            : base(keyword, IfPart.If, block)
        {
            this.Condition = condition;
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}if ({Condition.PrettyPrint()})";
            x += Block.PrettyPrint();
            return x;
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

        public ElseIfBranch(Token keyword, StatementWithValue condition, Block block)
            : base(keyword, IfPart.ElseIf, block)
        {
            this.Condition = condition;
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}elseif ({Condition.PrettyPrint()})";
            x += Block.PrettyPrint();
            return x;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Condition;
            yield return Block;
        }
    }

    public class ElseBranch : BaseBranch
    {
        public ElseBranch(Token keyword, Block block)
            : base(keyword, IfPart.Else, block)
        { }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}{Keyword}";
            x += Block.PrettyPrint();
            return x;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Block;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class NewInstance : StatementWithValue
    {
        public readonly Token Keyword;
        public readonly TypeInstance TypeName;

        public NewInstance(Token keyword, TypeInstance typeName)
        {
            Keyword = keyword;
            TypeName = typeName;
        }

        public override string ToString() => $"new {TypeName}";
        public override string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}new {TypeName}";
        public override Position GetPosition()
            => new(Keyword, TypeName);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
            string result = "new ";
            result += TypeName.ToString();
            result += "(";

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
            result += paramsString;

            result += ")";
            return result;
        }
        public override string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var arg in this.Parameters)
            {
                parameters.Add(arg.PrettyPrint());
            }

            return $"{" ".Repeat(ident)}new {TypeName}({(string.Join(", ", parameters))})";
        }
        public override Position GetPosition()
            => new Position(Keyword, TypeName, BracketLeft, BracketRight).Extend(Parameters);

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

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class IndexCall : StatementWithValue, IReadableID
    {
        public StatementWithValue? PrevStatement;

        public readonly StatementWithValue Expression;
        public readonly Token BracketLeft;
        public readonly Token BracketRight;

        public IndexCall(Token bracketLeft, StatementWithValue indexStatement, Token bracketRight)
        {
            this.Expression = indexStatement;
            this.BracketLeft = bracketLeft;
            this.BracketRight = bracketRight;
        }

        public override string ToString()
        {
            return $"{PrevStatement}{BracketLeft}{Expression}{BracketRight}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{PrevStatement}[{Expression.PrettyPrint()}]";
        }

        public override Position GetPosition()
            => new(PrevStatement, Expression);

        public string ReadableID(Func<StatementWithValue, CompiledType> TypeSearch)
        {
            string result = string.Empty;
            if (PrevStatement != null) result += TypeSearch.Invoke(this.PrevStatement).Name;
            else result += "null";
            result += "[";
            result += TypeSearch.Invoke(this.Expression).Name;
            result += "]";

            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            if (PrevStatement != null) yield return PrevStatement;
            yield return Expression;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
        {
            return $"{PrevStatement}.{FieldName}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{PrevStatement.PrettyPrint()}.{FieldName}";
        }

        public override Position GetPosition()
            => new(PrevStatement, FieldName);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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

        public override string ToString() => $"{PrevStatement} as {Type}";
        public override string PrettyPrint(int ident = 0) => $"{new string(' ', ident)}{PrevStatement} as {Type}";

        public override Position GetPosition()
            => new(PrevStatement, Keyword, Type);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class ModifiedStatement : StatementWithValue
    {
        public readonly StatementWithValue Statement;
        public readonly Token Modifier;

        public ModifiedStatement(Token modifier, StatementWithValue statement)
        {
            this.Statement = statement;
            this.Modifier = modifier;
        }

        public override string ToString() => $"{Modifier} {Statement}";
        public override string PrettyPrint(int ident = 0) => $"{new string(' ', ident)}{Modifier} {Statement}";

        public override Position GetPosition()
            => new(Modifier, Statement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Statement;
        }
    }
}
