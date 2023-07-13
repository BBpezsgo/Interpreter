using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProgrammingLanguage.BBCode.Parser.Statement
{
    using Core;

    using System.Linq;

    public static class StatementFinder
    {
        static bool GetAllStatement(Statement st, Func<Statement, bool> callback)
        {
            if (st == null) return false;
            if (callback == null) return false;

            if (callback?.Invoke(st) == true) return true;

            var statements = st.GetStatements();
            return GetAllStatement(statements, callback);
        }
        static bool GetAllStatement(IEnumerable<Statement> statements, Func<Statement, bool> callback)
        {
            if (statements is null) throw new ArgumentNullException(nameof(statements));

            if (callback == null) return false;

            foreach (var statement in statements)
            {
                if (GetAllStatement(statement, callback)) return true;
            }
            return false;
        }
        static bool GetAllStatement(Statement[] statements, Func<Statement, bool> callback)
        {
            if (statements is null) throw new ArgumentNullException(nameof(statements));

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
        public static void GetAllStatement(ParserResult parserResult, Func<Statement, bool> callback)
        {
            if (callback == null) return;

            for (int i = 0; i < parserResult.TopLevelStatements.Length; i++)
            {
                if (parserResult.TopLevelStatements[i] == null) continue;
                GetAllStatement(parserResult.TopLevelStatements[i], callback);
            }

            for (int i = 0; i < parserResult.Functions.Length; i++)
            {
                if (parserResult.Functions[i].Statements == null) continue;
                GetAllStatement(parserResult.Functions[i].Statements, callback);
            }
        }
        public static void GetAllStatement(IEnumerable<FunctionDefinition> functions, Func<Statement, bool> callback)
        {
            if (callback == null) return;
            if (functions == null) return;

            foreach (var item in functions)
            { GetAllStatement(item.Statements, callback); }
        }
        public static void GetAllStatement(IEnumerable<GeneralFunctionDefinition> functions, Func<Statement, bool> callback)
        {
            if (callback == null) return;
            if (functions == null) return;

            foreach (var item in functions)
            { GetAllStatement(item.Statements, callback); }
        }
    }

    public interface IReadableID
    {
        public string ReadableID(Func<StatementWithValue, Compiler.CompiledType> TypeSearch);
    }

    public abstract class Statement : IThingWithPosition
    {
        public override string ToString()
            => this.GetType().Name;

        public abstract string PrettyPrint(int ident = 0);
        public abstract Position TotalPosition();
        public Position GetPosition() => TotalPosition();

        public virtual IEnumerable<Statement> GetStatements()
        { yield break; }
    }

    public abstract class StatementWithValue : Statement
    {
        public bool SaveValue = true;
        public virtual object TryGetValue() => null;
    }

    public class Block : Statement
    {
        public List<Statement> Statements;

        public Token BracketStart;
        public Token BracketEnd;

        public Block()
        { this.Statements = new(); }

        public Block(IEnumerable<Statement> statements)
        { this.Statements = new(statements); }

        public override Position TotalPosition()
            => new(BracketStart, BracketEnd);

        public override string PrettyPrint(int ident = 0)
        {
            string result = "{\n";

            foreach (Statement statement in Statements)
            { result += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n"; }

            result += $"{" ".Repeat(ident)}}}";
            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Statements.Count; i++)
            { yield return Statements[i]; }
        }
    }

    public abstract class StatementWithBlock : Statement
    {
        internal Block Block;
    }

    public class CompileTag : Statement, IDefinition
    {
        public Token HashToken;
        public Token HashName;
        public Literal[] Parameters;

        public string FilePath { get; set; }

        public override string PrettyPrint(int ident = 0)
        {
            return $"# <hasn info>";
        }

        public override Position TotalPosition()
        {
            Position result = new(HashToken, HashName);
            foreach (var item in Parameters)
            {
                result.Extend(item.ValueToken);
            }
            return result;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class LiteralList : StatementWithValue
    {
        public Token BracketLeft;
        public Token BracketRight;
        public StatementWithValue[] Values;
        public int Size => Values.Length;

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}[{string.Join(", ", Values.ToList())}]";
        }

        public override Position TotalPosition()
            => new(BracketLeft, BracketRight);

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Values.Length; i++)
            { yield return Values[i]; }
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class VariableDeclaretion : Statement, IDefinition
    {
        public TypeInstance Type;
        public Token VariableName;
        internal StatementWithValue InitialValue;

        public string FilePath { get; set; }

        public override string ToString()
        {
            return $"{Type} {VariableName}{((InitialValue != null) ? " = ..." : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{Type} {VariableName}{((InitialValue != null) ? $" = {InitialValue.PrettyPrint()}" : "")}";
        }

        public override Position TotalPosition()
        {
            Position result = new(Type, VariableName, InitialValue);
            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return InitialValue;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class FunctionCall : StatementWithValue, IReadableID
    {
        public Token Identifier;
        internal string FunctionName => Identifier.Content;
        public StatementWithValue[] Parameters = Array.Empty<StatementWithValue>();
        internal bool IsMethodCall => PrevStatement != null;
        public StatementWithValue PrevStatement;

        public Token BracketLeft;
        public Token BracketRight;

        internal StatementWithValue[] MethodParameters
        {
            get
            {
                if (PrevStatement == null)
                { return Parameters.ToArray(); }
                var newList = new List<StatementWithValue>(Parameters.ToArray());
                newList.Insert(0, PrevStatement);
                return newList.ToArray();
            }
        }

        public override string ToString()
        {
            string result = "";
            result += FunctionName;
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
            return $"{" ".Repeat(ident)}{FunctionName}({(string.Join(", ", parameters))})";
        }

        public override Position TotalPosition()
        {
            Position result = new(Identifier, BracketLeft, BracketRight);
            result.Extend(MethodParameters);
            return result;
        }

        public string ReadableID(Func<StatementWithValue, Compiler.CompiledType> TypeSearch)
        {
            string result = "";
            if (this.PrevStatement != null)
            {
                result += TypeSearch.Invoke(this.PrevStatement);
                result += ".";
            }
            result += this.FunctionName;
            result += "(";
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (i > 0) { result += ", "; }
                result += TypeSearch.Invoke(this.Parameters[i]);
            }
            result += ")";
            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
            for (int i = 0; i < Parameters.Length; i++)
            { yield return Parameters[i]; }
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class KeywordCall : StatementWithValue, IReadableID
    {
        public Token Identifier;
        internal string FunctionName => Identifier.Content;
        public StatementWithValue[] Parameters = Array.Empty<StatementWithValue>();

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

        public override Position TotalPosition()
        {
            Position result = new(Identifier);
            result.Extend(Parameters);
            return result;
        }

        public string ReadableID(Func<StatementWithValue, Compiler.CompiledType> TypeSearch)
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
        internal readonly StatementWithValue Left;
        internal StatementWithValue Right;
        internal bool InsideBracelet;

        internal StatementWithValue[] Parameters
        {
            get
            {
                StatementWithValue left = this.Left;
                StatementWithValue right = this.Right;

                if (left is null && right is not null)
                { return new StatementWithValue[] { right }; }
                else if (left is not null && right is null)
                { return new StatementWithValue[] { left }; }
                else if (left is not null && right is not null)
                { return new StatementWithValue[] { left, right }; }
                else
                { throw new Errors.InternalException(); }
            }
        }
        internal int ParameterCount
        {
            get
            {
                int i = 0;
                if (Left != null) i++;
                if (Right != null) i++;
                return i;
            }
        }

        public OperatorCall(Token op, StatementWithValue left)
        {
            this.Operator = op;
            this.Left = left;
            this.Right = null;
        }
        public OperatorCall(Token op, StatementWithValue left, StatementWithValue right)
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

        public override object TryGetValue()
        {
            switch (this.Operator.Content)
            {
                case "+":
                    {
                        if (Left == null) return null;
                        if (Right == null) return null;

                        var leftVal = Left.TryGetValue();
                        if (leftVal == null) return null;

                        var rightVal = Right.TryGetValue();
                        if (rightVal == null) return null;

                        if (leftVal is int leftInt && rightVal is int rightInt)
                        { return leftInt + rightInt; }

                        if (leftVal is float leftFloat && rightVal is float rightFloat)
                        { return leftFloat + rightFloat; }

                        if (leftVal is string leftStr && rightVal is string rightStr)
                        { return leftStr + rightStr; }
                    }

                    return null;
                case "-":
                    {
                        if (Left == null) return null;
                        if (Right == null) return null;

                        var leftVal = Left.TryGetValue();
                        if (leftVal == null) return null;

                        var rightVal = Right.TryGetValue();
                        if (rightVal == null) return null;

                        if (leftVal is int leftInt && rightVal is int rightInt)
                        { return leftInt - rightInt; }

                        if (leftVal is float leftFloat && rightVal is float rightFloat)
                        { return leftFloat - rightFloat; }
                    }

                    return null;
                case "*":
                    {
                        if (Left == null) return null;
                        if (Right == null) return null;

                        var leftVal = Left.TryGetValue();
                        if (leftVal == null) return null;

                        var rightVal = Right.TryGetValue();
                        if (rightVal == null) return null;

                        if (leftVal is int leftInt && rightVal is int rightInt)
                        { return leftInt * rightInt; }

                        if (leftVal is float leftFloat && rightVal is float rightFloat)
                        { return leftFloat * rightFloat; }
                    }

                    return null;
                case "/":
                    {
                        if (Left == null) return null;
                        if (Right == null) return null;

                        var leftVal = Left.TryGetValue();
                        if (leftVal == null) return null;

                        var rightVal = Right.TryGetValue();
                        if (rightVal == null) return null;

                        if (leftVal is int leftInt && rightVal is int rightInt)
                        { return leftInt / rightInt; }

                        if (leftVal is float leftFloat && rightVal is float rightFloat)
                        { return leftFloat / rightFloat; }
                    }

                    return null;
                default:
                    return null;
            }
        }

        public override Position TotalPosition() => new(Operator, Left, Right);
        public string ReadableID(Func<StatementWithValue, Compiler.CompiledType> TypeSearch)
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
            yield return Right;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Assignment : Statement
    {
        public readonly Token Operator;
        internal readonly StatementWithValue Left;
        internal readonly StatementWithValue Right;

        public Assignment(Token @operator, StatementWithValue left, StatementWithValue right)
        {
            this.Operator = @operator;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString() => $"... {Operator} ...";

        public override string PrettyPrint(int ident = 0) => $"{Left.PrettyPrint()} {Operator} {Right.PrettyPrint()}";

        public override Position TotalPosition() => new(Operator, Left, Right);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Left;
            yield return Right;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Literal : StatementWithValue
    {
        public LiteralType Type;
        internal string Value;
        /// <summary>
        /// If there is no <c>ValueToken</c>:<br/>
        /// i.e in <c>i++</c> statement
        /// </summary>
        internal Position ImagineryPosition;
        public Token ValueToken;

        public override string ToString() => Type switch
        {
            LiteralType.INT => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.FLOAT => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.BOOLEAN => Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.STRING => $"\"{Value}\"",
            LiteralType.CHAR => $"'{Value}'",
            _ => null,
        };

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{Value}";
        }

        public override object TryGetValue()
            => Type switch
            {
                LiteralType.INT => int.Parse(Value),
                LiteralType.FLOAT => float.Parse(Value),
                LiteralType.BOOLEAN => bool.Parse(Value),
                _ => null,
            };

        public override Position TotalPosition() => ValueToken == null ? ImagineryPosition : new Position(ValueToken);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Identifier : StatementWithValue
    {
        public Token VariableName;

        public override string ToString()
            => $"{VariableName.Content}";

        public override string PrettyPrint(int ident = 0)
            => $"{" ".Repeat(ident)}{VariableName.Content}";

        public Identifier()
        {

        }

        public override Position TotalPosition() => new Position(VariableName);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class AddressGetter : StatementWithValue
    {
        public Token OperatorToken;
        internal StatementWithValue PrevStatement;

        public override string ToString()
        {
            return $"{OperatorToken.Content}{PrevStatement}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.Content}{PrevStatement.PrettyPrint(0)}";
        }

        public AddressGetter()
        {

        }

        public override Position TotalPosition()
            => new(OperatorToken, PrevStatement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Pointer : StatementWithValue
    {
        public Token OperatorToken;
        internal StatementWithValue PrevStatement;

        public override string ToString()
        {
            return $"{OperatorToken.Content}{PrevStatement}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.Content}{PrevStatement.PrettyPrint(0)}";
        }

        public Pointer()
        {

        }

        public override Position TotalPosition()
            => new(OperatorToken, PrevStatement);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class WhileLoop : StatementWithBlock
    {
        internal Token Keyword;
        internal StatementWithValue Condition;

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

        public override Position TotalPosition()
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
        internal Token Keyword;
        internal VariableDeclaretion VariableDeclaration;
        internal StatementWithValue Condition;
        internal Statement Expression;

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

        public override Position TotalPosition()
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
        public List<BaseBranch> Parts = new();

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

        public override Position TotalPosition()
            => new(Parts);

        public override IEnumerable<Statement> GetStatements()
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                yield return Parts[i];
            }
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public abstract class BaseBranch : StatementWithBlock
    {
        internal Token Keyword;
        public IfPart Type;

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

        public override Position TotalPosition()
            => new(Keyword, Block);

        public abstract override string PrettyPrint(int ident = 0);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return Block;
        }
    }
    public class IfBranch : BaseBranch
    {
        internal StatementWithValue Condition;

        public IfBranch()
        { Type = IfPart.If; }

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
        internal StatementWithValue Condition;

        public ElseIfBranch()
        { Type = IfPart.ElseIf; }

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
        public ElseBranch()
        { Type = IfPart.Else; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}else";
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
        public Token Keyword;
        public Token TypeName;

        public override string ToString() => $"new {TypeName}";
        public override string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}new {TypeName}";
        public override Position TotalPosition()
            => new(Keyword, TypeName);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class ConstructorCall : StatementWithValue, IReadableID
    {
        public StatementWithValue[] Parameters = Array.Empty<StatementWithValue>();
        public Token Keyword;
        public Token TypeName;

        public Token BracketLeft;
        public Token BracketRight;

        public override string ToString()
        {
            string result = "new ";
            result += TypeName.Content;
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
        public override Position TotalPosition()
        {
            Position result = new Position(Keyword, TypeName, BracketLeft, BracketRight);
            result.Extend(Parameters);
            return result;
        }

        public string ReadableID(Func<StatementWithValue, Compiler.CompiledType> TypeSearch)
        {
            string result = "";
            result += TypeName.Content;
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
        internal readonly StatementWithValue Expression;
        internal StatementWithValue PrevStatement;

        public IndexCall(StatementWithValue indexStatement)
        {
            this.Expression = indexStatement;
        }

        public override string ToString()
        {
            return $"[{Expression}]";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"[{Expression.PrettyPrint()}]";
        }

        public override Position TotalPosition()
            => new(PrevStatement, Expression);

        public string ReadableID(Func<StatementWithValue, Compiler.CompiledType> TypeSearch)
        {
            string result = TypeSearch.Invoke(this.PrevStatement).Name;
            result += "[";
            result += TypeSearch.Invoke(this.Expression).Name;
            result += "]";

            return result;
        }

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
            yield return Expression;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Field : StatementWithValue
    {
        public Token FieldName;
        internal StatementWithValue PrevStatement;

        public override string ToString()
        {
            return $"{PrevStatement}.{FieldName}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{PrevStatement.PrettyPrint()}.{FieldName}";
        }

        public override Position TotalPosition()
            => new(PrevStatement, FieldName);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class TypeCast : StatementWithValue
    {
        internal StatementWithValue PrevStatement;
        internal Token Keyword;
        internal TypeInstance Type;

        public TypeCast(StatementWithValue prevStatement, Token keyword, TypeInstance type)
        {
            this.PrevStatement = prevStatement;
            this.Keyword = keyword;
            this.Type = type;
        }

        public override string ToString() => $"{PrevStatement} as {Type}";
        public override string PrettyPrint(int ident = 0) => $"{new string(' ', ident)}{PrevStatement} as {Type}";

        public override Position TotalPosition()
            => new(PrevStatement, Keyword, Type);

        public override IEnumerable<Statement> GetStatements()
        {
            yield return PrevStatement;
        }
    }
}
