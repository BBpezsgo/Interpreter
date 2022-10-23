using IngameCoding.Core;

namespace IngameCoding.BBCode.Parser.Statements
{
    abstract class Statement
    {
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        public Position position;

        public override string ToString()
        { return this.GetType().Name; }

        public abstract string PrettyPrint(int ident = 0);
    }

    abstract class StatementParent : Statement
    {
        public List<Statement> statements;
        public StatementParent()
        { this.statements = new(); }

        public abstract override string PrettyPrint(int ident = 0);
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_NewVariable : Statement
    {
        internal Type type;
        internal string variableName;
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
        /// </list>
        /// </summary>
        internal Statement initialValue;
        internal bool IsRef;

        public override string ToString()
        {
            return $"{type.name}{(IsRef ? " ref" : "")} {variableName}{((initialValue != null) ? " = ..." : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{type.name}{(IsRef ? " ref" : "")} {variableName}{((initialValue != null) ? $" = {initialValue.PrettyPrint()}" : "")}";
        }
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_FunctionCall : Statement
    {
        string[] namespacePath;
        readonly string[] targetNamespacePath;
        internal Token functionNameT;
        internal string FunctionName => functionNameT.text;
        internal List<Statement> parameters = new();
        internal bool IsMethodCall;
        internal Statement PrevStatement;

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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_Operator : Statement
    {
        internal readonly string Operator;
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

        public Statement_Operator(string op, Statement left)
        {
            this.Operator = op;
            this.Left = left;
            this.Right = null;
        }
        public Statement_Operator(string op, Statement left, Statement right)
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_Literal : Statement
    {
        internal Type type;
        internal string value;

        public override string ToString()
        {
            return $"{value}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            if (type.type == BuiltinType.STRING)
            {
                return $"{" ".Repeat(ident)}\"{value}\"";
            }
            else
            {
                return $"{" ".Repeat(ident)}{value}";
            }
        }
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_Variable : Statement
    {
        /// <summary> Used for: Only for lists! This is the value between "[]" </summary>
        public Statement listIndex;
        internal string variableName;
        internal bool reference;

        public override string ToString()
        {
            return $"{(reference ? "ref " : "")}{variableName}{((listIndex != null) ? "[...]" : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{(reference ? "ref " : "")}{variableName}{((listIndex != null) ? $"[{listIndex.PrettyPrint()}]" : "")}";
        }

        public Statement_Variable()
        {
            this.listIndex = null;
        }
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_WhileLoop : StatementParent
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_ForLoop : StatementParent
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
    }
    class Statement_If : Statement
    {
        public List<Statement_If_Part> parts = new();

        public override string PrettyPrint(int ident = 0)
        {
            var x = "";
            foreach (var part in parts)
            {
                x += $"{part.PrettyPrint(ident)}\n";
            }
            if (x.EndsWith("\n"))
            {
                x = x[..^1];
            }
            return x;
        }
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    abstract class Statement_If_Part : StatementParent
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

        public abstract override string PrettyPrint(int ident = 0);
    }
    class Statement_If_If : Statement_If_Part
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
    }
    class Statement_If_ElseIf : Statement_If_Part
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
    }
    class Statement_If_Else : Statement_If_Part
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_StructField : Statement
    {
        /// <summary> Used for: Only for lists! This is the value between "[]" </summary>
        public Statement listIndex;
        internal string variableName;
        internal string fieldName;

        public override string ToString()
        {
            return $"{variableName}.{fieldName}{((listIndex != null) ? "[...]" : "")}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{variableName}.{fieldName}{((listIndex != null) ? $"[{listIndex.PrettyPrint()}]" : "")}";
        }

        public Statement_StructField()
        {
            this.listIndex = null;
            this.variableName = null;
            this.fieldName = null;
        }
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_NewStruct : Statement
    {
        readonly string[] namespacePath;
        readonly string[] targetNamespacePath;
        internal string structName;

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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_Index : Statement
    {
        readonly internal Statement indexStatement;
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_Field : Statement
    {
        internal string FieldName;
        internal Statement PrevStatement;

        public override string ToString()
        {
            return $"{PrevStatement}.{FieldName}";
        }

        public override string PrettyPrint(int ident = 0)
        {
            return $"{PrevStatement.PrettyPrint()}.{FieldName}";
        }
    }


    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    class Statement_MethodCall : Statement_FunctionCall
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
