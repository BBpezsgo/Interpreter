using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace IngameCoding.BBCode.Parser.Statements
{
    using Core;

    using System.Security.Cryptography;

    public static class StatementFinder
    {
        static bool GetAllStatement(Statement st, Func<Statement, bool> callback)
        {
            if (st == null) return false;
            if (callback == null) return false;

            if (callback?.Invoke(st) == true) return true;

            if (st is StatementParent statementParent)
            { if (GetAllStatement(statementParent.statements, callback)) return true; }

            if (st is Statement_ListValue listValue)
            { return GetAllStatement(listValue.Values, callback); }
            else if (st is Statement_NewVariable newVariable)
            { if (newVariable.initialValue != null) return GetAllStatement(newVariable.initialValue, callback); }
            else if (st is Statement_FunctionCall functionCall)
            {
                if (GetAllStatement(functionCall.PrevStatement, callback)) return true;

                return GetAllStatement(functionCall.parameters, callback);
            }
            else if (st is Statement_Operator @operator)
            {
                if (@operator.Right != null) if (GetAllStatement(@operator.Right, callback)) return true;
                if (@operator.Left != null) return GetAllStatement(@operator.Left, callback);
            }
            else if (st is Statement_Literal literal)
            { }
            else if (st is Statement_Variable variable)
            { if (variable.listIndex != null) return GetAllStatement(variable.listIndex, callback); }
            else if (st is Statement_WhileLoop whileLoop)
            { return GetAllStatement(whileLoop.condition, callback); }
            else if (st is Statement_ForLoop forLoop)
            {
                if (GetAllStatement(forLoop.condition, callback)) return true;
                if (GetAllStatement(forLoop.expression, callback)) return true;
                if (GetAllStatement(forLoop.variableDeclaration, callback)) return true;
            }
            else if (st is Statement_If @if)
            { return GetAllStatement(@if.parts.ToArray(), callback); }
            else if (st is Statement_If_If ifIf)
            { return GetAllStatement(ifIf.condition, callback); }
            else if (st is Statement_If_ElseIf ifElseif)
            { return GetAllStatement(ifElseif.condition, callback); }
            else if (st is Statement_If_Else)
            { }
            else if (st is Statement_NewStruct newStruct)
            { }
            else if (st is Statement_Index indexStatement)
            { }
            else if (st is Statement_Field field)
            { return GetAllStatement(field.PrevStatement, callback); }
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
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Position position;

        public override string ToString()
        { return this.GetType().Name; }

        public abstract string PrettyPrint(int ident = 0);

        public virtual object TryGetValue()
        { return null; }

        public abstract bool TryGetTotalPosition(out Position result);

        internal abstract void SetPosition();
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

        public override bool TryGetTotalPosition(out Position result)
        {
            result = new(HashToken, HashName);
            foreach (var item in Parameters)
            {
                result.Extend(item.ValueToken);
            }
            return true;
        }

        internal override void SetPosition()
        {

        }
    }

    public abstract class StatementWithReturnValue : Statement
    {
        public bool SaveValue = true;
    }

    public abstract class StatementParent : Statement
    {
        public List<Statement> statements;
        public StatementParent()
        { this.statements = new(); }

        public Token BracketStart;
        public Token BracketEnd;

        public override bool TryGetTotalPosition(out Position result)
        { throw new NotImplementedException(); }

        public abstract override string PrettyPrint(int ident = 0);
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_ListValue : StatementWithReturnValue
    {
        public List<Statement> Values;
        public int Size => Values.Count;

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}[{string.Join(", ", Values)}]";
        }

        public override bool TryGetTotalPosition(out Position result)
        {
            if (Values.Count == 0) { result = position; return false; }
            bool clean = Values[0].TryGetTotalPosition(out result);
            for (int i = 1; i < Values.Count; i++)
            {
                clean = Values[i].TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            return clean;
        }

        internal override void SetPosition()
        {
            Values[0].SetPosition();
            Position position = Values[0].position;
            foreach (var item in Values)
            {
                item.SetPosition();
                position.Extend(item);
            }
            this.position = position;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewVariable : Statement, IDefinition
    {
        public TypeToken type;
        public Token variableName;
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
        ///  <seealso cref="Statement_NewStruct"></seealso>
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
        internal Statement initialValue;
        internal bool IsRef;

        public string FilePath { get; set; }

        public override string ToString()
        {
            return $"{type.text}{(type.isList ? "[]" : "")}{(IsRef ? " ref" : "")} {variableName}{((initialValue != null) ? " = ..." : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{type.text}{(type.isList ? "[]" : "")}{(IsRef ? " ref" : "")} {variableName}{((initialValue != null) ? $" = {initialValue.PrettyPrint()}" : "")}";
        }

        internal override void SetPosition()
        {
            position = type.GetPosition();
            if (initialValue != null)
            {
                initialValue.SetPosition();
                position.Extend(initialValue);
            }
        }

        public override bool TryGetTotalPosition(out Position result)
        {
            bool clean = true;
            result = new(type, variableName);
            if (initialValue != null)
            {
                clean = initialValue.TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            return clean;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_FunctionCall : StatementWithReturnValue
    {
        string[] namespacePath;
        readonly string[] targetNamespacePath;
        public Token functionNameT;
        internal string FunctionName => functionNameT.text;
        public List<Statement> parameters = new();
        internal bool IsMethodCall;
        public Statement PrevStatement;

        internal Statement[] MethodParameters
        {
            get
            {
                if (PrevStatement == null)
                { return parameters.ToArray(); }
                var newList = new List<Statement>(parameters.ToArray());
                newList.Insert(0, PrevStatement);
                return newList.ToArray();
            }
        }

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
            set
            {
                if (value.Length == 0) { this.namespacePath = Array.Empty<string>(); }
                if (!value.Contains('.')) { this.namespacePath = new string[1] { value }; }
                this.namespacePath = value.Split(".");
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

        public Statement_FunctionCall(string[] namespacePath, string[] targetNamespacePath, bool isMethodCall = false)
        {
            this.namespacePath = namespacePath;
            this.targetNamespacePath = targetNamespacePath;
            this.IsMethodCall = isMethodCall;
        }

        public override string ToString()
        {
            return $"{TargetNamespacePathPrefix}{FunctionName}(...)";
        }

        public override string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var arg in this.parameters)
            {
                parameters.Add(arg.PrettyPrint());
            }
            return $"{" ".Repeat(ident)}{TargetNamespacePathPrefix}{FunctionName}({(string.Join(", ", parameters))})";
        }

        internal override void SetPosition()
        {
            position = functionNameT.GetPosition();
            foreach (var item in parameters)
            {
                item.SetPosition();
                position.Extend(item);
            }
        }

        public override bool TryGetTotalPosition(out Position result)
        {
            bool clean = true;
            result = new(functionNameT);
            foreach (var item in MethodParameters)
            {
                clean = item.TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            return clean;
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

        internal override void SetPosition() { }

        public override bool TryGetTotalPosition(out Position result)
        {
            bool clean = true;
            result = new(Operator);
            if (Left != null)
            {
                clean = Left.TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            if (Right != null)
            {
                clean = Right.TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            return clean;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Literal : StatementWithReturnValue
    {
        public TypeToken type;
        internal string value;
        public Token ValueToken;

        public override string ToString()
        {
            return $"{value}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            if (type.typeName == BuiltinType.STRING)
            {
                return $"{" ".Repeat(ident)}\"{value}\"";
            }
            else
            {
                return $"{" ".Repeat(ident)}{value}";
            }
        }

        public override object TryGetValue()
        {
            if (type.isList) return null;

            return type.typeName switch
            {
                BuiltinType.INT => int.Parse(value),
                BuiltinType.FLOAT => float.Parse(value),
                BuiltinType.STRING => value,
                BuiltinType.BOOLEAN => bool.Parse(value),
                _ => null,
            };
        }

        internal override void SetPosition()
        {

        }

        public override bool TryGetTotalPosition(out Position result)
        {
            result = new Position(ValueToken);
            return true;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Variable : StatementWithReturnValue
    {
        /// <summary> Used for: Only for lists! This is the value between "[]" </summary>
        public Statement listIndex;
        public Token variableName;
        internal bool reference;

        public override string ToString()
        {
            return $"{(reference ? "ref " : "")}{variableName.text}{((listIndex != null) ? "[...]" : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{(reference ? "ref " : "")}{variableName.text}{((listIndex != null) ? $"[{listIndex.PrettyPrint()}]" : "")}";
        }

        public Statement_Variable()
        {
            this.listIndex = null;
        }

        internal override void SetPosition()
        {

        }

        public override bool TryGetTotalPosition(out Position result)
        {
            bool clean = true;
            result = new(variableName);
            if (listIndex != null)
            {
                clean = listIndex.TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            return clean;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_WhileLoop : StatementParent
    {
        internal string name;
        internal Statement condition;

        public override string ToString()
        {
            return $"while (...) {{...}};";
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}while ({condition.PrettyPrint()}) {{\n";

            foreach (var statement in statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        internal override void SetPosition()
        {

        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_ForLoop : StatementParent
    {
        internal string name;
        internal Statement_NewVariable variableDeclaration;
        internal Statement condition;
        internal Statement expression;

        public override string ToString()
        {
            return $"for (...) {{...}};";
        }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}for ({variableDeclaration.PrettyPrint()}; {condition.PrettyPrint()}; {expression.PrettyPrint()}) {{\n";

            foreach (var statement in statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        internal override void SetPosition()
        {

        }
    }
    public class Statement_If : Statement
    {
        public List<Statement_If_Part> parts = new();

        public override string PrettyPrint(int ident = 0)
        {
            var x = "";
            foreach (var part in parts)
            {
                x += $"{part.PrettyPrint(ident)}\n";
            }
            if (x.EndsWith("\n", StringComparison.InvariantCulture))
            {
                x = x[..^1];
            }
            return x;
        }

        public override bool TryGetTotalPosition(out Position result)
        { throw new NotImplementedException(); }

        internal override void SetPosition()
        {

        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public abstract class Statement_If_Part : StatementParent
    {
        public IfPart partType;
        internal string name;

        public enum IfPart
        {
            If_IfStatement,
            If_ElseStatement,
            If_ElseIfStatement
        }

        public override string ToString()
        {
            return $"{name} {((partType != IfPart.If_ElseStatement) ? "(...)" : "")} {{...}}";
        }

        public override bool TryGetTotalPosition(out Position result)
        { throw new NotImplementedException(); }

        public abstract override string PrettyPrint(int ident = 0);
    }
    public class Statement_If_If : Statement_If_Part
    {
        internal Statement condition;

        public Statement_If_If()
        { partType = IfPart.If_IfStatement; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}if ({condition.PrettyPrint()}) {{\n";

            foreach (var statement in statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        internal override void SetPosition()
        {

        }
    }
    public class Statement_If_ElseIf : Statement_If_Part
    {
        internal Statement condition;

        public Statement_If_ElseIf()
        { partType = IfPart.If_ElseIfStatement; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}elseif ({condition.PrettyPrint()}) {{\n";

            foreach (var statement in statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        internal override void SetPosition()
        {

        }
    }
    public class Statement_If_Else : Statement_If_Part
    {
        public Statement_If_Else()
        { partType = IfPart.If_ElseStatement; }

        public override string PrettyPrint(int ident = 0)
        {
            var x = $"{" ".Repeat(ident)}else {{\n";

            foreach (var statement in statements)
            {
                x += $"{" ".Repeat(ident)}{statement.PrettyPrint(ident)};\n";
            }

            x += $"{" ".Repeat(ident)}}}";
            return x;
        }

        internal override void SetPosition()
        {

        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewStruct : StatementWithReturnValue
    {
        readonly string[] namespacePath;
        readonly string[] targetNamespacePath;
        public Token structName;

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

        public Statement_NewStruct(string[] namespacePath, string[] targetNamespacePath)
        {
            this.namespacePath = namespacePath;
            this.targetNamespacePath = targetNamespacePath;
        }

        public override string ToString()
        {
            return $"new {TargetNamespacePathPrefix}{structName}()";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}new {TargetNamespacePathPrefix}{structName}()";
        }

        internal override void SetPosition()
        {

        }

        public override bool TryGetTotalPosition(out Position result)
        {
            result = new Position(structName);
            return true;
        }
    }
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_Index : StatementWithReturnValue
    {
        internal readonly Statement indexStatement;
        internal Statement PrevStatement;

        public Statement_Index(Statement indexStatement)
        {
            this.indexStatement = indexStatement;
        }

        public override string ToString()
        {
            return $"[{indexStatement}]";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"[{indexStatement.PrettyPrint()}]";
        }

        internal override void SetPosition()
        {
            indexStatement.SetPosition();
            this.position = indexStatement.position;
        }

        public override bool TryGetTotalPosition(out Position result)
        {
            bool clean = indexStatement.TryGetTotalPosition(out result);
            if (PrevStatement != null)
            {
                clean = PrevStatement.TryGetTotalPosition(out var p) && clean;
                result.Extend(p);
            }
            return clean;
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

        internal override void SetPosition()
        {

        }

        public override bool TryGetTotalPosition(out Position result)
        {
            bool clean = PrevStatement.TryGetTotalPosition(out result);
            result.Extend(new Position(FieldName));
            return clean;
        }
    }


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
}
