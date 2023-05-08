﻿using System;
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
            else if (st is Statement_Operator @operator)
            {
                if (@operator.Right != null) if (GetAllStatement(@operator.Right, callback)) return true;
                if (@operator.Left != null) return GetAllStatement(@operator.Left, callback);
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
            else if (st is Statement_VariableAddressGetter)
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
        public virtual object TryGetValue() => null;
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
        public List<Statement> Values;
        public int Size => Values.Count;

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}[{string.Join(", ", Values)}]";
        }

        public override Position TotalPosition() => new(BracketLeft, BracketRight);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewVariable : Statement, IDefinition
    {
        public TypeToken Type;
        public Token VariableName;
        /// <summary>
        /// <b>The value is:</b>
        /// <seealso cref="Parser.ExpectExpression"/><br/>
        /// <b>Wich can be:</b>
        /// <list type="bullet">
        /// <item>
        ///  <seealso cref="Statement_FunctionCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_Literal"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_NewInstance"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_MethodCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_StructField"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_Variable"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_Operator"></seealso>
        /// </item>
        /// <item>
        ///  <c>null</c>
        /// </item>
        /// </list>
        /// </summary>
        internal Statement InitialValue;

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
        readonly string[] namespacePath;
        string[] targetNamespacePath;
        public Token Identifier;
        internal string FunctionName => Identifier.text;
        public List<Statement> Parameters = new();
        internal bool IsMethodCall => PrevStatement != null;
        public Statement PrevStatement;
        internal bool TargetNamespacePathPrefixIsReversed;

        internal Statement[] MethodParameters
        {
            get
            {
                if (PrevStatement == null)
                { return Parameters.ToArray(); }
                var newList = new List<Statement>(Parameters.ToArray());
                newList.Insert(0, PrevStatement);
                return newList.ToArray();
            }
        }

        /// <summary> The path in which **the statement is located** </summary>
        /// <returns> "[library].[...].[library]." </returns>
        public string NamespacePathPrefix
        {
            get
            {
                string val = "";
                for (int i = 0; i < namespacePath.Length; i++)
                { val += namespacePath[i].ToString() + "."; }
                return val;
            }
        }
        /// <summary> The path to which **the statement refers** </summary>
        /// <returns> "[library].[...].[library]." </returns>
        public string TargetNamespacePathPrefix
        {
            get
            {
                string val = "";
                for (int i = 0; i < targetNamespacePath.Length; i++)
                { val += targetNamespacePath[i].ToString() + "."; }
                return val;
            }
            set
            {
                if (value.Length == 0) { this.targetNamespacePath = Array.Empty<string>(); }
                if (!value.Contains('.')) { this.targetNamespacePath = new string[1] { value }; }
                this.targetNamespacePath = value.Split(".");
            }
        }

        public Statement_FunctionCall(string[] namespacePath, string[] targetNamespacePath)
        {
            this.namespacePath = namespacePath;
            this.targetNamespacePath = targetNamespacePath;
        }

        public override string ToString()
        {
            return $"{TargetNamespacePathPrefix}{FunctionName}(...)";
        }

        public override string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var arg in this.Parameters)
            {
                parameters.Add(arg.PrettyPrint());
            }
            return $"{" ".Repeat(ident)}{TargetNamespacePathPrefix}{FunctionName}({(string.Join(", ", parameters))})";
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
    public class Statement_Operator : StatementWithReturnValue
    {
        public readonly Token Operator;
        internal readonly Statement Left;
        internal Statement Right;
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

        public Statement_Operator(Token op, Statement left)
        {
            this.Operator = op;
            this.Left = left;
            this.Right = null;
        }
        public Statement_Operator(Token op, Statement left, Statement right)
        {
            this.Operator = op;
            this.Left = left;
            this.Right = right;
        }

        public override string ToString()
        {
            string v;
            if (Left != null)
            {
                if (Right != null)
                {
                    v = $"... {Operator} ...";
                }
                else
                {
                    v = $"... {Operator}";
                }
            }
            else
            {
                v = $"{Operator}";
            }
            if (InsideBracelet)
            {
                return $"({v})";
            }
            else
            {
                return v;
            }
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
                return $"{" ".Repeat(ident)}{v}";
            }
        }

        public override object TryGetValue()
        {
            switch (this.Operator.text)
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
            if (Type.typeName == BuiltinType.STRING)
            {
                return $"{" ".Repeat(ident)}\"{Value}\"";
            }
            else
            {
                return $"{" ".Repeat(ident)}{Value}";
            }
        }

        public override object TryGetValue()
        {
            if (Type.IsList) return null;

            return Type.typeName switch
            {
                BuiltinType.INT => int.Parse(Value),
                BuiltinType.FLOAT => float.Parse(Value),
                BuiltinType.STRING => Value,
                BuiltinType.BOOLEAN => bool.Parse(Value),
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
            return $"{VariableName.text}{((ListIndex != null) ? "[...]" : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{VariableName.text}{((ListIndex != null) ? $"[{ListIndex.PrettyPrint()}]" : "")}";
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
    public class Statement_VariableAddressGetter : StatementWithReturnValue
    {
        public Token OperatorToken;
        public Token VariableName;

        public override string ToString()
        {
            return $"{OperatorToken.text}{VariableName.text}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{OperatorToken.text}{VariableName.text}";
        }

        public Statement_VariableAddressGetter()
        {

        }

        public override Position TotalPosition() => new(OperatorToken, VariableName);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_WhileLoop : StatementParent
    {
        internal Token Keyword;
        internal Statement Condition;

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
        internal Statement Condition;
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
        internal Statement Condition;

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
        internal Statement Condition;

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
        readonly string[] namespacePath;
        readonly string[] targetNamespacePath;

        public Token Keyword;
        public Token TypeName;
        /// <returns> "[library].[...].[library]." </returns>
        public string NamespacePathPrefix
        {
            get
            {
                string val = "";
                for (int i = 0; i < namespacePath.Length; i++)
                {
                    if (val.Length > 0)
                    {
                        val += "." + namespacePath[i].ToString();
                    }
                    else
                    {
                        val = namespacePath[i].ToString();
                    }
                }
                if (val.Length > 0)
                {
                    val += ".";
                }
                return val;
            }
        }
        /// <returns> "[library].[...].[library]." </returns>
        public string TargetNamespacePathPrefix
        {
            get
            {
                string val = "";
                for (int i = 0; i < targetNamespacePath.Length; i++)
                {
                    if (val.Length > 0)
                    {
                        val += "." + targetNamespacePath[i].ToString();
                    }
                    else
                    {
                        val = targetNamespacePath[i].ToString();
                    }
                }
                if (val.Length > 0)
                {
                    val += ".";
                }
                return val;
            }
        }

        public Statement_NewInstance(string[] namespacePath, string[] targetNamespacePath)
        {
            this.namespacePath = namespacePath;
            this.targetNamespacePath = targetNamespacePath;
        }

        public override string ToString() => $"new {TargetNamespacePathPrefix}{TypeName}()";
        public override string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}new {TargetNamespacePathPrefix}{TypeName}()";
        public override Position TotalPosition() => new(TypeName);
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Index : StatementWithReturnValue
    {
        internal readonly Statement Expression;
        internal Statement PrevStatement;

        public Statement_Index(Statement indexStatement)
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
        internal Statement PrevStatement;

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

#if false
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_MethodCall : Statement_FunctionCall
    {
        internal Token variableNameToken;
        internal string VariableName => variableNameToken.text;

        public Statement_MethodCall(string[] namespacePath, string[] targetNamespacePath, bool isMethodCall = true) : base(namespacePath, targetNamespacePath, isMethodCall)
        {
            this.variableNameToken = null;
        }

        public override string ToString()
        {
            return $"{VariableName}.{FunctionName}(...)";
        }

        public override string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var arg in this.parameters)
            {
                parameters.Add(arg.PrettyPrint());
            }
            return $"{" ".Repeat(ident)}{VariableName}.{FunctionName}({string.Join(", ", parameters)})";
        }
    }
#endif
}
