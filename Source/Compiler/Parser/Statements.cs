using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace IngameCoding.BBCode.Parser.Statements
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

            if (st is StatementParent statementParent)
            { if (GetAllStatement(statementParent.Statements, callback)) return true; }

            if (st is Statement_ListValue listValue)
            { return GetAllStatement(listValue.Values, callback); }
            else if (st is Statement_NewVariable newVariable)
            { if (newVariable.InitialValue != null) return GetAllStatement(newVariable.InitialValue, callback); }
            else if (st is Statement_FunctionCall functionCall)
            {
                if (GetAllStatement(functionCall.PrevStatement, callback)) return true;

                return GetAllStatement(functionCall.Parameters, callback);
            }
            else if (st is Statement_ConstructorCall constructorCall)
            {
                return GetAllStatement(constructorCall.Parameters, callback);
            }
            else if (st is Statement_KeywordCall keywordCall)
            {
                return GetAllStatement(keywordCall.Parameters, callback);
            }
            else if (st is Statement_Operator @operator)
            {
                if (@operator.Right != null) if (GetAllStatement(@operator.Right, callback)) return true;
                if (@operator.Left != null) return GetAllStatement(@operator.Left, callback);
            }
            else if (st is Statement_Setter setter)
            {
                if (GetAllStatement(setter.Right, callback)) return true;
                return GetAllStatement(setter.Left, callback);
            }
            else if (st is Statement_Literal)
            { }
            else if (st is Statement_Variable variable)
            { if (variable.ListIndex != null) return GetAllStatement(variable.ListIndex, callback); }
            else if (st is Statement_WhileLoop whileLoop)
            { return GetAllStatement(whileLoop.Condition, callback); }
            else if (st is Statement_ForLoop forLoop)
            {
                if (GetAllStatement(forLoop.Condition, callback)) return true;
                if (GetAllStatement(forLoop.Expression, callback)) return true;
                if (GetAllStatement(forLoop.VariableDeclaration, callback)) return true;
            }
            else if (st is Statement_If @if)
            { return GetAllStatement(@if.Parts.ToArray(), callback); }
            else if (st is Statement_If_If ifIf)
            { return GetAllStatement(ifIf.Condition, callback); }
            else if (st is Statement_If_ElseIf ifElseif)
            { return GetAllStatement(ifElseif.Condition, callback); }
            else if (st is Statement_If_Else)
            { }
            else if (st is Statement_NewInstance)
            { }
            else if (st is Statement_Index)
            { }
            else if (st is Statement_Field field)
            { return GetAllStatement(field.PrevStatement, callback); }
            else if (st is Statement_As @as)
            { return GetAllStatement(@as.PrevStatement, callback); }
            else if (st is Statement_MemoryAddressGetter)
            { }
            else if (st is Statement_MemoryAddressFinder)
            { }
            else
            { throw new NotImplementedException($"{st.GetType().FullName}"); }

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

            for (int i = 0; i < parserResult.Functions.Count; i++)
            {
                if (parserResult.Functions[i].Statements == null) continue;
                GetAllStatement(parserResult.Functions[i].Statements, callback);
            }
        }
        static bool GetAllStatement(List<Statement> statements, Func<Statement, bool> callback)
        {
            if (statements is null) throw new ArgumentNullException(nameof(statements));
            return GetAllStatement(statements.ToArray(), callback);
        }
    }

    public abstract class Statement
    {
        public override string ToString()
        { return this.GetType().Name; }

        public abstract string PrettyPrint(int ident = 0);
        public abstract Position TotalPosition();
    }
    public class Statement_HashInfo : Statement, IDefinition
    {
        public Token HashToken;
        public Token HashName;
        public Statement_Literal[] Parameters;

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

    public abstract class StatementWithReturnValue : Statement
    {
        public bool SaveValue = true;
        public virtual object TryGetValue() => null;
    }

    public abstract class StatementParent : Statement
    {
        public List<Statement> Statements;
        public StatementParent()
        { this.Statements = new(); }

        public Token BracketStart;
        public Token BracketEnd;

        public override Position TotalPosition() => new(BracketStart, BracketEnd);

        public abstract override string PrettyPrint(int ident = 0);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_ListValue : StatementWithReturnValue
    {
        public Token BracketLeft;
        public Token BracketRight;
        public StatementWithReturnValue[] Values;
        public int Size => Values.Length;

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}[{string.Join(", ", Values.ToList())}]";
        }

        public override Position TotalPosition() => new(BracketLeft, BracketRight);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewVariable : Statement, IDefinition
    {
        public TypeToken Type;
        public Token VariableName;
        internal StatementWithReturnValue InitialValue;

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
            Position result = new(Type, VariableName);
            if (InitialValue != null)
            { result.Extend(InitialValue.TotalPosition()); }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_FunctionCall : StatementWithReturnValue
    {
        public Token Identifier;
        internal string FunctionName => Identifier.Content;
        public StatementWithReturnValue[] Parameters = Array.Empty<StatementWithReturnValue>();
        internal bool IsMethodCall => PrevStatement != null;
        public StatementWithReturnValue PrevStatement;

        internal StatementWithReturnValue[] MethodParameters
        {
            get
            {
                if (PrevStatement == null)
                { return Parameters.ToArray(); }
                var newList = new List<StatementWithReturnValue>(Parameters.ToArray());
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
            Position result = new(Identifier);
            foreach (var item in MethodParameters)
            {
                result.Extend(item.TotalPosition());
            }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_KeywordCall : Statement
    {
        public Token Identifier;
        internal string FunctionName => Identifier.Content;
        public StatementWithReturnValue[] Parameters = Array.Empty<StatementWithReturnValue>();

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
            foreach (var item in Parameters)
            { result.Extend(item.TotalPosition()); }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Operator : StatementWithReturnValue
    {
        public readonly Token Operator;
        internal readonly StatementWithReturnValue Left;
        internal StatementWithReturnValue Right;
        internal bool InsideBracelet;

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

        public Statement_Operator(Token op, StatementWithReturnValue left)
        {
            this.Operator = op;
            this.Left = left;
            this.Right = null;
        }
        public Statement_Operator(Token op, StatementWithReturnValue left, StatementWithReturnValue right)
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

        public override Position TotalPosition()
        {
            Position result = new(Operator);
            if (Left != null)
            { result.Extend(Left.TotalPosition()); }
            if (Right != null)
            { result.Extend(Right.TotalPosition()); }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Setter : Statement
    {
        public readonly Token Operator;
        internal readonly StatementWithReturnValue Left;
        internal readonly StatementWithReturnValue Right;

        public Statement_Setter(Token @operator, StatementWithReturnValue left, StatementWithReturnValue right)
        {
            this.Operator = @operator;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString() => $"... {Operator} ...";

        public override string PrettyPrint(int ident = 0) => $"{Left.PrettyPrint()} {Operator} {Right.PrettyPrint()}";

        public override Position TotalPosition()
        {
            Position result = new(Operator);
            if (Left != null)
            { result.Extend(Left.TotalPosition()); }
            if (Right != null)
            { result.Extend(Right.TotalPosition()); }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Literal : StatementWithReturnValue
    {
        public TypeToken Type;
        internal string Value;
        /// <summary>
        /// If there is no <c>ValueToken</c>:<br/>
        /// i.e in <c>i++</c> statement
        /// </summary>
        internal Position ImagineryPosition;
        public Token ValueToken;

        public override string ToString()
        {
            return $"{Value}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{Value}";
        }

        public override object TryGetValue()
        {
            if (Type.IsList) return null;

            return Type.Type switch
            {
                TypeTokenType.INT => int.Parse(Value),
                TypeTokenType.FLOAT => float.Parse(Value),
                TypeTokenType.BOOLEAN => bool.Parse(Value),
                _ => null,
            };
        }

        public override Position TotalPosition() => ValueToken == null ? ImagineryPosition : new Position(ValueToken);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Variable : StatementWithReturnValue
    {
        /// <summary> Used for: Only for lists! This is the value between "[]" </summary>
        public Statement ListIndex;
        public Token VariableName;

        public override string ToString()
        {
            return $"{VariableName.Content}{((ListIndex != null) ? "[...]" : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{VariableName.Content}{((ListIndex != null) ? $"[{ListIndex.PrettyPrint()}]" : "")}";
        }

        public Statement_Variable()
        {
            this.ListIndex = null;
        }

        public override Position TotalPosition()
        {
            Position result = new(VariableName);
            if (ListIndex != null)
            { result.Extend(ListIndex.TotalPosition()); }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_MemoryAddressGetter : StatementWithReturnValue
    {
        public Token OperatorToken;
        internal StatementWithReturnValue PrevStatement;

        public override string ToString()
        {
            return $"{OperatorToken.Content}{PrevStatement}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.Content}{PrevStatement.PrettyPrint(0)}";
        }

        public Statement_MemoryAddressGetter()
        {

        }

        public override Position TotalPosition()
        {
            Position result = PrevStatement.TotalPosition();
            result.Extend(OperatorToken);
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_MemoryAddressFinder : StatementWithReturnValue
    {
        public Token OperatorToken;
        internal StatementWithReturnValue PrevStatement;

        public override string ToString()
        {
            return $"{OperatorToken.Content}{PrevStatement}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.Content}{PrevStatement.PrettyPrint(0)}";
        }

        public Statement_MemoryAddressFinder()
        {

        }

        public override Position TotalPosition()
        {
            Position result = OperatorToken.GetPosition();
            if (PrevStatement != null) result.Extend(PrevStatement);
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_WhileLoop : StatementParent
    {
        internal Token Keyword;
        internal StatementWithReturnValue Condition;

        public override string ToString()
        {
            return $"while (...) {{...}};";
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}while ({Condition.PrettyPrint()}) {{\n";

            foreach (var statement in Statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        public override Position TotalPosition() => new(Keyword, BracketStart, BracketEnd);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_ForLoop : StatementParent
    {
        internal Token Keyword;
        internal Statement_NewVariable VariableDeclaration;
        internal StatementWithReturnValue Condition;
        internal Statement Expression;

        public override string ToString()
        {
            return $"for (...) {{...}};";
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}for ({VariableDeclaration.PrettyPrint()}; {Condition.PrettyPrint()}; {Expression.PrettyPrint()}) {{\n";

            foreach (var statement in Statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        public override Position TotalPosition() => new(Keyword, BracketStart, BracketEnd);
    }
    public class Statement_If : Statement
    {
        public List<Statement_If_Part> Parts = new();

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

        public override Position TotalPosition() => new(Parts[0].BracketStart, Parts[^1].BracketEnd);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public abstract class Statement_If_Part : StatementParent
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

        public override Position TotalPosition() => new(Keyword, BracketStart, BracketEnd);

        public abstract override string PrettyPrint(int ident = 0);
    }
    public class Statement_If_If : Statement_If_Part
    {
        internal StatementWithReturnValue Condition;

        public Statement_If_If()
        { Type = IfPart.If; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}if ({Condition.PrettyPrint()}) {{\n";

            foreach (var statement in Statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }
    }
    public class Statement_If_ElseIf : Statement_If_Part
    {
        internal StatementWithReturnValue Condition;

        public Statement_If_ElseIf()
        { Type = IfPart.ElseIf; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}elseif ({Condition.PrettyPrint()}) {{\n";

            foreach (var statement in Statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }
    }
    public class Statement_If_Else : Statement_If_Part
    {
        public Statement_If_Else()
        { Type = IfPart.Else; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}else {{\n";

            foreach (var statement in Statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewInstance : StatementWithReturnValue
    {
        public Token Keyword;
        public Token TypeName;

        public override string ToString() => $"new {TypeName}";
        public override string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}new {TypeName}";
        public override Position TotalPosition() => new(TypeName);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_ConstructorCall : StatementWithReturnValue
    {
        public StatementWithReturnValue[] Parameters = Array.Empty<StatementWithReturnValue>();
        public Token Keyword;
        public Token TypeName;

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
        public override Position TotalPosition() => new(TypeName);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Index : StatementWithReturnValue
    {
        internal readonly StatementWithReturnValue Expression;
        internal StatementWithReturnValue PrevStatement;

        public Statement_Index(StatementWithReturnValue indexStatement)
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
        {
            Position result = Expression.TotalPosition();
            if (PrevStatement != null)
            { result.Extend(PrevStatement.TotalPosition()); }
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Field : StatementWithReturnValue
    {
        public Token FieldName;
        internal StatementWithReturnValue PrevStatement;

        public override string ToString()
        {
            return $"{PrevStatement}.{FieldName}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{PrevStatement.PrettyPrint()}.{FieldName}";
        }

        public override Position TotalPosition()
        {
            Position result = PrevStatement.TotalPosition();
            result.Extend(new Position(FieldName));
            return result;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_As : StatementWithReturnValue
    {
        internal StatementWithReturnValue PrevStatement;
        internal Token Keyword;
        internal TypeToken Type;

        public Statement_As(StatementWithReturnValue prevStatement, Token keyword, TypeToken type)
        {
            this.PrevStatement = prevStatement;
            this.Keyword = keyword;
            this.Type = type;
        }

        public override string ToString() => $"{PrevStatement} as {Type}";
        public override string PrettyPrint(int ident = 0) => $"{new string(' ', ident)}{PrevStatement} as {Type}";

        public override Position TotalPosition()
        {
            Position result = Keyword.GetPosition();

            if (PrevStatement != null) result.Extend(PrevStatement.TotalPosition());
            if (Type != null) result.Extend(Type);

            return result;
        }
    }
}
