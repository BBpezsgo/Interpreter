using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace IngameCoding.BBCode
{
    public enum TokenSubtype
    {
        None,
        MethodName,
        Keyword,
        Type,
        VariableName,
        /// <summary>
        /// while, for, if, etc.
        /// </summary>
        Statement,
        Library,
        Struct,
    }

    public enum BuiltinType
    {
        AUTO,
        INT,
        FLOAT,
        VOID,
        STRING,
        BOOLEAN,
        STRUCT,
        RUNTIME,
        ANY,
    }

    [System.Serializable]
    public class Type
    {
        public string name;
        public BuiltinType type;
        /// <summary>Struct only!</summary>
        public List<Type> types;
        public bool isList;

        public int startOffset;
        public int endOffset;
        public int startOffsetTotal;
        public int endOffsetTotal;
        public int lineNumber;

        public Position Position
        {
            get
            {
                return new Position(lineNumber, startOffset, new Vector2Int(startOffsetTotal, endOffsetTotal));
            }
        }

        public override string ToString()
        {
            return name + (isList ? "[]" : "");
        }

        public Type(string name, BuiltinType type)
        {
            this.name = name;
            this.type = type;
        }

        public void AddTokenProperties(Token token)
        {
            startOffset = token.startOffset;
            endOffset = token.endOffset;
            startOffsetTotal = token.startOffsetTotal;
            endOffsetTotal = token.endOffsetTotal;
            lineNumber = token.lineNumber;
        }

        public Type Clone()
        {
            Type newType = new(name, type)
            {
                types = types,
                startOffset = startOffset,
                endOffset = endOffset,
                startOffsetTotal = startOffsetTotal,
                endOffsetTotal = endOffsetTotal,
                lineNumber = lineNumber,
                isList = isList
            };
            return newType;
        }
    }

    [System.Serializable]
    public class ParameterDefinition
    {
        public string name;
        public Type type;

        internal bool withRefKeyword;
        internal bool withThisKeyword;

        public override string ToString()
        {
            return $"{(this.withRefKeyword ? "ref " : "")}{type} {name}";
        }

        internal string PrettyPrint(int ident = 0)
        {
            return $"{" ".Repeat(ident)}{(this.withRefKeyword ? "ref " : "")}{type} {name}";
        }
    }

    public class FunctionDefinition
    {
        public class Attribute
        {
            public string Name;
            public object[] Parameters;
        }

        public Attribute[] attributes;
        public readonly string Name;
        public string FullName
        {
            get
            {
                return NamespacePathPrefix + Name;
            }
        }
        public List<ParameterDefinition> parameters;
        public List<Statement> statements;
        public Type type;
        public readonly string[] namespacePath;
        string NamespacePathPrefix
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

        public FunctionDefinition(string[] namespacePath, string name)
        {
            parameters = new List<ParameterDefinition>();
            statements = new List<Statement>();
            attributes = new Attribute[] { };
            this.namespacePath = namespacePath;
            this.Name = name;
        }

        public FunctionDefinition(List<string> namespacePath, string name)
        {
            parameters = new List<ParameterDefinition>();
            statements = new List<Statement>();
            attributes = new Attribute[0];
            this.namespacePath = namespacePath.ToArray();
            this.Name = name;
        }

        public override string ToString()
        {
            return $"{this.type.name} {this.FullName}" + (this.parameters.Count > 0 ? "(...)" : "()") + " " + (this.statements.Count > 0 ? "{...}" : "{}");
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var parameter in this.parameters)
            {
                parameters.Add(parameter.PrettyPrint((ident == 0) ? 2 : ident));
            }

            List<string> statements = new();
            foreach (var statement in this.statements)
            {
                statements.Add($"{" ".Repeat(ident)}" + statement.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            return $"{" ".Repeat(ident)}{this.type.name} {this.FullName}" + ($"({string.Join(", ", parameters)})") + " " + (this.statements.Count > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
        }
    }

    public class StructDefinition
    {
        public FunctionDefinition.Attribute[] attributes;
        readonly string name;
        public string FullName
        {
            get
            {
                return NamespacePathPrefix + name;
            }
        }
        public List<ParameterDefinition> parameters;
        public List<Statement> statements;
        public Type type;
        public readonly string[] namespacePath;
        string NamespacePathPrefix
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

        public List<ParameterDefinition> fields;
        public Dictionary<string, FunctionDefinition> methods;
        public string NamespacePath
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
                return val;
            }
        }

        public StructDefinition(string[] namespacePath, string name)
        {
            this.name = name;
            fields = new List<ParameterDefinition>();
            methods = new Dictionary<string, FunctionDefinition>();
            attributes = new FunctionDefinition.Attribute[] { };
            this.namespacePath = namespacePath;
        }
        public StructDefinition(List<string> namespacePath, string name)
        {
            this.name = name;
            fields = new List<ParameterDefinition>();
            methods = new Dictionary<string, FunctionDefinition>();
            attributes = new FunctionDefinition.Attribute[] { };
            this.namespacePath = namespacePath.ToArray();
        }

        public override string ToString()
        {
            return $"struct {this.name} " + "{...}";
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> fields = new();
            foreach (var field in this.fields)
            {
                fields.Add($"{" ".Repeat(ident)}" + field.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            List<string> methods = new();
            foreach (var method in this.methods)
            {
                methods.Add($"{" ".Repeat(ident)}" + method.Value.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            return $"{" ".Repeat(ident)}struct {this.name} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
        }
    }

    #region Statements

    public abstract class Statement
    {
        public struct Position
        {
            public int Line;
            public Vector2Int AbsolutePosition;

            public void Extend(Vector2Int absolutePosition)
            {
                this.AbsolutePosition = new Vector2Int(
                    Math.Min(this.AbsolutePosition.x, absolutePosition.x),
                    Math.Max(this.AbsolutePosition.y, absolutePosition.y)
                    );
            }
            public void Extend(int absoluteStart, int absoluteEnd) => Extend(new Vector2Int(absoluteStart, absoluteEnd));

            public static implicit operator IngameCoding.Position(Position position)
            {
                return new IngameCoding.Position(position.Line, 0, position.AbsolutePosition);
            }
        }

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        public Position position;

        public override string ToString()
        { return this.GetType().Name; }

        public abstract string PrettyPrint(int ident = 0);
    }

    public abstract class StatementParent : Statement
    {
        public List<Statement> statements;
        public StatementParent()
        { this.statements = new(); }

        public abstract override string PrettyPrint(int ident = 0);
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewVariable : Statement
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
    public class Statement_FunctionCall : Statement
    {
        string[] namespacePath;
        readonly string[] targetNamespacePath;
        internal Token functionNameT;
        internal string FunctionName => functionNameT.text;
        internal List<Statement> parameters = new();
        internal bool IsMethodCall;
        internal Statement? PrevStatement;

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
                if (value.Length == 0) { this.namespacePath = new string[0]; }
                if (!value.Contains(".")) { this.namespacePath = new string[1] { value }; }
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
    public class Statement_Operator : Statement
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
    public class Statement_Literal : Statement
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
    public class Statement_Variable : Statement
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
            if (x.EndsWith("\n"))
            {
                x = x[..^1];
            }
            return x;
        }
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_StructField : Statement
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
    }
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Statement_NewStruct : Statement
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
    public class Statement_Index : Statement
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
    public class Statement_Field : Statement
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
    public class Statement_MethodCall : Statement_FunctionCall
    {
        internal Token variableNameToken;
        internal string VariableName => variableNameToken.text;

        public Statement_MethodCall(string[] namespacePath, string[] targetNamespacePath, bool isMethodCall = true) : base(namespacePath, targetNamespacePath, isMethodCall)
        { }

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

    #endregion

    public class Parser
    {
        int currentTokenIndex;
        readonly List<Token> tokens = new();

        Token CurrentToken
        {
            get { return (currentTokenIndex < tokens.Count) ? tokens[currentTokenIndex] : null; }
        }

        readonly Dictionary<string, Type> types = new();
        readonly Dictionary<string, int> operators = new();
        bool enableThisKeyword = false;
        readonly List<string> CurrentNamespace = new();
        readonly List<string> VariableNames = new();
        List<string> GlobalVariableNames
        {
            get
            {
                List<string> returnValue = new();
                foreach (var variable in GlobalVariables)
                {
                    returnValue.Add(variable.variableName);
                }
                return returnValue;
            }
        }

        List<Warning> Warnings;

        // === Result ===
        public Dictionary<string, FunctionDefinition> Functions = new();
        public Dictionary<string, StructDefinition> Structs = new();
        public List<Statement_NewVariable> GlobalVariables = new();
        public List<string> Usings = new();
        // === ===

        public Parser()
        {
            types.Add("int", new Type("int", BuiltinType.INT));
            types.Add("string", new Type("string", BuiltinType.STRING));
            types.Add("void", new Type("void", BuiltinType.VOID));
            types.Add("float", new Type("float", BuiltinType.FLOAT));
            types.Add("bool", new Type("bool", BuiltinType.BOOLEAN));

            operators.Add("|", 4);
            operators.Add("&", 4);
            operators.Add("^", 4);

            operators.Add("!=", 5);
            operators.Add(">=", 5);
            operators.Add("<=", 5);
            operators.Add("==", 5);

            operators.Add("=", 10);

            operators.Add("+=", 11);
            operators.Add("-=", 11);

            operators.Add("*=", 12);
            operators.Add("/=", 12);
            operators.Add("%=", 12);

            operators.Add("<", 20);
            operators.Add(">", 20);

            operators.Add("+", 30);
            operators.Add("-", 30);

            operators.Add("*", 31);
            operators.Add("/", 31);
            operators.Add("%", 31);

            operators.Add("++", 40);
            operators.Add("--", 40);
        }

        public void Parse(Token[] _tokens, List<Warning> warnings)
        {
            Warnings = warnings;
            tokens.Clear();
            tokens.AddRange(_tokens);

            currentTokenIndex = 0;

            ParseCodeHeader();

            int endlessSafe = 0;
            while (CurrentToken != null)
            {
                ParseCodeBlock();

                endlessSafe++;
                if (endlessSafe > 500) { throw new EndlessLoopException(); }
            }
        }

        public string PrettyPrint()
        {
            var x = "";

            foreach (var @using in Usings)
            {
                x += "using " + @using + ";\n";
            }

            foreach (var globalVariable in GlobalVariables)
            {
                x += globalVariable.PrettyPrint() + ";\n";
            }

            foreach (var @struct in Structs)
            {
                x += @struct.Value.PrettyPrint() + "\n";
            }

            foreach (var function in Functions)
            {
                x += function.Value.PrettyPrint() + "\n";
            }

            return x;
        }

        #region Parse top level

        bool ExpectUsing(out string @namespace)
        {
            @namespace = string.Empty;

            Token usingT = ExpectIdentifier("using");
            if (usingT == null)
            { return false; }
            usingT.subtype = TokenSubtype.Keyword;

            List<Token> tokens = new();
            int endlessSafe = 50;
            while (ExpectIdentifier(out Token pathPartT) != null)
            {
                pathPartT.subtype = TokenSubtype.Library;
                tokens.Add(pathPartT);
                @namespace += pathPartT.text;
                if (ExpectOperator(";") != null)
                {
                    break;
                }
                else if (ExpectOperator(".") == null)
                {
                    throw new SyntaxException($"Unexpected token '{CurrentToken.text}'", pathPartT);
                }

                @namespace += ".";

                endlessSafe--;
                if (endlessSafe <= 0)
                { throw new EndlessLoopException(); }
            }

            if (@namespace == string.Empty)
            { throw new SyntaxException("Expected library name after 'using'", usingT); }


            return true;
        }

        void ParseCodeHeader()
        {
            while (ExpectUsing(out var usingNamespace))
            {
                Usings.Add(usingNamespace);
            }
        }

        void ParseCodeBlock()
        {
            if (ExpectNamespaceDefinition()) { }
            else if (ExpectStructDefinition()) { }
            else if (ExpectFunctionDefinition()) { }
            else if (ExpectGlobalVariable()) { }
            else
            { throw new SyntaxException("Expected global variable or namespace/struct/function definition", CurrentToken); }
        }

        bool ExpectGlobalVariable()
        {
            var possibleVariable = ExpectVariableDeclaration(false);
            if (possibleVariable != null)
            {
                GlobalVariables.Add(possibleVariable);

                if (ExpectOperator(";") == null)
                { throw new SyntaxException("Expected ';' at end of statement", new Position(CurrentToken.lineNumber)); }

                return true;
            }

            return false;
        }

        bool ExpectFunctionDefinition()
        {
            int parseStart = currentTokenIndex;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
                {
                    if (attribute.Name == attr.Name)
                    {
                        alreadyHave = true;
                        break;
                    }
                }
                if (!alreadyHave)
                {
                    attributes.Add(attr);
                }
                else
                { throw new ParserException("Attribute '" + attr + "' already applied to the function"); }
            }

            Type possibleType = ExceptType(false);
            if (possibleType == null)
            { currentTokenIndex = parseStart; return false; }

            Token possibleNameT = ExpectIdentifier();
            if (possibleNameT == null)
            { currentTokenIndex = parseStart; return false; }

            if (ExpectOperator("(") == null)
            { currentTokenIndex = parseStart; return false; }

            FunctionDefinition function = new(CurrentNamespace, possibleNameT.text)
            {
                type = possibleType,
                attributes = attributes.ToArray()
            };

            possibleNameT.subtype = TokenSubtype.MethodName;

            var expectParameter = false;
            while (ExpectOperator(")") == null || expectParameter)
            {
                Token thisKeywordT = ExpectIdentifier("this");
                if (thisKeywordT != null)
                {
                    thisKeywordT.subtype = TokenSubtype.Keyword;
                    if (function.parameters.Count > 0)
                    { throw new ParserException("Keyword 'this' is only valid at the first parameter", thisKeywordT); }
                }

                Token referenceKeywordT = ExpectIdentifier("ref");
                if (referenceKeywordT != null) referenceKeywordT.subtype = TokenSubtype.Keyword;

                Type possibleParameterType = ExceptType(false, true);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                Token possibleParameterNameT = ExpectIdentifier();
                if (possibleParameterNameT == null)
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.subtype = TokenSubtype.VariableName;

                ParameterDefinition parameterDefinition = new()
                {
                    type = possibleParameterType,
                    name = possibleParameterNameT.text,
                    withRefKeyword = referenceKeywordT != null,
                    withThisKeyword = thisKeywordT != null,
                };
                function.parameters.Add(parameterDefinition);

                if (ExpectOperator(")") != null)
                { break; }

                if (ExpectOperator(",") == null)
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            List<Statement> statements = new();

            if (ExpectOperator(";") == null)
            {
                statements = ParseFunctionBody();
            }

            function.statements = statements;

            Functions.Add(function.FullName, function);

            return true;
        }

        bool ExpectNamespaceDefinition()
        {
            if (ExpectIdentifier("namespace", out var possibleNamespaceIdentifier) == null)
            { return false; }

            possibleNamespaceIdentifier.subtype = TokenSubtype.Keyword;

            Token possibleName = ExpectIdentifier();
            if (possibleName != null)
            {
                Token possibleOperator = ExpectOperator("{");
                if (possibleOperator != null)
                {
                    possibleName.subtype = TokenSubtype.Library;
                    CurrentNamespace.Add(possibleName.text);
                    int endlessSafe = 0;
                    while (ExpectOperator("}") == null)
                    {
                        ParseCodeBlock();
                        endlessSafe++;
                        if (endlessSafe >= 100)
                        {
                            throw new EndlessLoopException();
                        }
                    }
                    CurrentNamespace.RemoveAt(CurrentNamespace.Count - 1);

                    return true;
                }
                { throw new SyntaxException("Expected { after namespace name", possibleName); }
            }
            else
            { throw new SyntaxException("Expected namespace name", possibleNamespaceIdentifier); }
        }

        bool ExpectStructDefinition()
        {
            int startTokenIndex = currentTokenIndex;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
                {
                    if (attribute.Name == attr.Name)
                    {
                        alreadyHave = true;
                        break;
                    }
                }
                if (!alreadyHave)
                {
                    attributes.Add(attr);
                }
                else
                { throw new ParserException("Attribute '" + attr + "' already applied to the struct"); }
            }

            Token possibleType = ExpectIdentifier("struct");
            if (possibleType == null)
            { currentTokenIndex = startTokenIndex; return false; }

            Token possibleStructName = ExpectIdentifier();
            if (possibleStructName == null)
            { throw new ParserException("Expected struct identifier after keyword 'struct'"); }

            if (ExpectOperator("{") == null)
            { throw new ParserException("Expected '{' after struct identifier"); }

            possibleStructName.subtype = TokenSubtype.Struct;
            possibleType.subtype = TokenSubtype.Keyword;

            StructDefinition structDefinition = new(CurrentNamespace, possibleStructName.text)
            {
                attributes = attributes.ToArray()
            };

            int endlessSafe = 0;
            while (ExpectOperator("}") == null)
            {
                ParameterDefinition field = ExpectField();
                if (field != null)
                {
                    structDefinition.fields.Add(field);
                }
                else
                {
                    enableThisKeyword = true;
                    FunctionDefinition method = ExpectMethodDefinition();
                    enableThisKeyword = false;
                    if (method != null)
                    {
                        structDefinition.methods.Add(method.FullName, method);
                    }
                }
                if (ExpectOperator(";") == null)
                    throw new SyntaxException("Expected ';' at end of statement", new Position(CurrentToken.lineNumber));

                endlessSafe++;
                if (endlessSafe > 50)
                {
                    throw new EndlessLoopException();
                }
            }

            Structs.Add(structDefinition.FullName, structDefinition);

            return true;
        }

        Statement_Variable ExpectReference()
        {
            if (ExpectIdentifier("ref", out var refKeyword) == null)
            { return null; }

            refKeyword.subtype = TokenSubtype.Keyword;

            if (ExpectIdentifier(out Token variableName) == null)
            { throw new SyntaxException("Expected variable name after 'ref' keyword", refKeyword); }

            if (variableName.text == "this")
            { throw new SyntaxException("Expected variable name after 'ref' keyword", refKeyword); }

            if (ExpectOperator("(") != null)
            { throw new SyntaxException("Expected variable name after 'ref' keyword", refKeyword); }

            if (ExpectOperator(".") != null)
            { throw new SyntaxException("Expected variable name after 'ref' keyword", refKeyword); }

            if (ExpectOperator("[") != null)
            { throw new SyntaxException("Expected variable name after 'ref' keyword", refKeyword); }

            Statement_Variable variableNameStatement = new()
            {
                variableName = variableName.text,
                reference = true,
            };

            variableNameStatement.position.Line = variableName.lineNumber;
            variableNameStatement.position.Extend(variableName.Position.AbsolutePosition);

            variableName.subtype = TokenSubtype.VariableName;

            return variableNameStatement;
        }

        #endregion

        #region Parse low level

        bool ExpectLiteral(out Statement_Literal statement)
        {
            int savedToken = currentTokenIndex;

            if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_FLOAT)
            {
                Statement_Literal literal = new()
                {
                    value = CurrentToken.text,
                    type = new Type("float", BuiltinType.FLOAT)
                };

                literal.position.Line = CurrentToken.lineNumber;
                literal.position.Extend(CurrentToken.Position.AbsolutePosition);

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_NUMBER)
            {
                Statement_Literal literal = new()
                {
                    value = CurrentToken.text,
                    type = new Type("int", BuiltinType.INT)
                };

                literal.position.Line = CurrentToken.lineNumber;
                literal.position.Extend(CurrentToken.Position.AbsolutePosition);

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_STRING)
            {
                Statement_Literal literal = new()
                {
                    value = CurrentToken.text,
                    type = new Type("string", BuiltinType.STRING)
                };

                literal.position.Line = CurrentToken.lineNumber;
                literal.position.Extend(CurrentToken.startOffsetTotal - 1, CurrentToken.endOffsetTotal + 1);

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("true", out var tTrue) != null)
            {
                Statement_Literal literal = new()
                {
                    value = "true",
                    type = new Type("bool", BuiltinType.BOOLEAN)
                };

                tTrue.subtype = TokenSubtype.Keyword;

                literal.position.Line = tTrue.lineNumber;
                literal.position.Extend(tTrue.Position.AbsolutePosition);

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("false", out var tFalse) != null)
            {
                Statement_Literal literal = new()
                {
                    value = "false",
                    type = new Type("bool", BuiltinType.BOOLEAN)
                };

                tFalse.subtype = TokenSubtype.Keyword;

                literal.position.Line = tFalse.lineNumber;
                literal.position.Extend(tFalse.Position.AbsolutePosition);

                statement = literal;
                return true;
            }

            currentTokenIndex = savedToken;

            statement = null;
            return false;
        }

        bool ExpectIndex(out Statement_Index statement)
        {
            if (ExpectOperator("[", out var token0) != null)
            {
                var st = ExpectOneValue();
                if (ExpectOperator("]") != null)
                {
                    statement = new Statement_Index(st);
                    return true;
                }
                else
                {
                    throw new SyntaxException("Unbalanced [", token0);
                }
            }
            statement = null;
            return false;
        }

        /// <returns>
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
        /// </list>
        /// </returns>
        Statement ExpectOneValue()
        {
            int savedToken = currentTokenIndex;

            Statement returnStatement = null;

            if (ExpectLiteral(out var literal))
            {
                returnStatement = literal;
            }
            else if (ExpectOperator("(", out var braceletT) != null)
            {
                var expression = ExpectExpression();
                if (expression == null)
                { throw new SyntaxException("Expected expression after '('", braceletT); }

                if (expression is Statement_Operator operation)
                { operation.InsideBracelet = true; }

                if (ExpectOperator(")") == null)
                { throw new SyntaxException("Unbalanced '('", braceletT); }

                returnStatement = expression;
            }
            else if (ExpectIdentifier("new", out Token newIdentifier) != null)
            {
                newIdentifier.subtype = TokenSubtype.Keyword;

                Token structName = ExpectIdentifier();
                if (structName == null)
                { throw new SyntaxException("Expected struct constructor after keyword 'new'", newIdentifier); }

                structName.subtype = TokenSubtype.Struct;

                List<string> targetLibraryPath = new();
                List<Token> targetLibraryPathTokens = new();

                while (ExpectOperator(".", out Token dotToken) != null)
                {
                    Token libraryToken = ExpectIdentifier();
                    if (libraryToken == null)
                    {
                        throw new SyntaxException("Expected namespace or class identifier", dotToken);
                    }
                    else
                    {
                        targetLibraryPath.Add(structName.text);
                        targetLibraryPathTokens.Add(structName);
                        structName = libraryToken;
                    }
                }

                foreach (var token in targetLibraryPathTokens)
                { token.subtype = TokenSubtype.Library; }

                if (structName == null)
                { throw new SyntaxException("Expected struct constructor after keyword 'new'", newIdentifier); }

                structName.subtype = TokenSubtype.Struct;

                Statement_NewStruct newStructStatement = new(CurrentNamespace.ToArray(), targetLibraryPath.ToArray())
                {
                    structName = structName.text
                };

                newStructStatement.position.Line = structName.lineNumber;
                newStructStatement.position.Extend(structName.Position.AbsolutePosition);

                returnStatement = newStructStatement;
            }
            else if (ExpectIdentifier(out Token variableName) != null)
            {
                if (variableName.text == "this" && !enableThisKeyword)
                { throw new ParserException("The keyword 'this' does not avaiable in the current context", variableName); }

                if (ExpectOperator("(") != null)
                {
                    currentTokenIndex = savedToken;
                    returnStatement = ExpectFunctionCall();
                }
                else
                {
                    Statement_Variable variableNameStatement = new()
                    {
                        variableName = variableName.text
                    };

                    variableNameStatement.position.Line = variableName.lineNumber;
                    variableNameStatement.position.Extend(variableName.Position.AbsolutePosition);

                    if (variableName.text == "this")
                    { variableName.subtype = TokenSubtype.Keyword; }
                    else
                    { variableName.subtype = TokenSubtype.VariableName; }

                    returnStatement = variableNameStatement;
                }
            }

            while (true)
            {
                if (ExpectOperator(".", out var tokenDot) != null)
                {
                    if (ExpectMethodCall(false, out var methodCall))
                    {
                        methodCall.PrevStatement = returnStatement;
                        returnStatement = methodCall;
                    }
                    else
                    {
                        var fieldName = ExpectIdentifier();
                        if (fieldName == null)
                        { throw new SyntaxException("Expected field or method", tokenDot); }

                        var fieldStatement = new Statement_Field()
                        {
                            FieldName = fieldName.text,
                            position = new Statement.Position()
                            {
                                AbsolutePosition = fieldName.Position.AbsolutePosition,
                                Line = fieldName.Position.Line
                            },
                            PrevStatement = returnStatement
                        };
                        returnStatement = fieldStatement;
                    }

                    continue;
                }

                if (ExpectIndex(out var statementIndex))
                {
                    statementIndex.PrevStatement = returnStatement;
                    returnStatement = statementIndex;

                    continue;
                }

                break;
            }

            return returnStatement;
        }

        List<Statement> ParseFunctionBody()
        {
            if (ExpectOperator("{") == null)
            { return null; }

            List<Statement> statements = new();

            int endlessSafe = 0;
            VariableNames.Clear();
            while (ExpectOperator("}") == null)
            {
                Statement statement = ExpectStatement();
                if (statement == null)
                {
                    if (CurrentToken != null)
                    {
                        throw new SyntaxException("Unknown statement", CurrentToken);
                    }
                    else
                    {
                        throw new SyntaxException("Unknown statement");
                    }
                }
                else if (statement is Statement_Literal)
                {
                    throw new SyntaxException("Unexpected kind of statement", statement.position);
                }
                else if (statement is Statement_Variable)
                {
                    throw new SyntaxException("Unexpected kind of statement", statement.position);
                }
                else if (statement is Statement_NewStruct)
                {
                    throw new SyntaxException("Unexpected kind of statement", statement.position);
                }
                else
                {
                    statements.Add(statement);

                    if (ExpectOperator(";") == null)
                    { throw new SyntaxException("Expected ';' at end of statement", statement.position); }
                }

                endlessSafe++;
                if (endlessSafe > 500) throw new EndlessLoopException();
            }
            VariableNames.Clear();

            return statements;
        }

        Statement_NewVariable ExpectVariableDeclaration(bool enableRefKeyword)
        {
            int startTokenIndex = currentTokenIndex;
            Type possibleType = ExceptType(out var structNotFoundWarning);
            if (possibleType == null)
            { currentTokenIndex = startTokenIndex; return null; }

            bool IsRef = false;
            if (ExpectIdentifier("ref") != null)
            {
                if (enableRefKeyword)
                {
                    throw new System.NotImplementedException();
                    // IsRef = true;
                }
                else
                { throw new SyntaxException("Keyword 'ref' is not valid in the current context"); }
            }

            Token possibleVariableName = ExpectIdentifier();
            if (possibleVariableName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            possibleVariableName.subtype = TokenSubtype.VariableName;

            Statement_NewVariable statement = new()
            {
                variableName = possibleVariableName.text,
                type = possibleType,
                IsRef = IsRef
            };
            VariableNames.Add(possibleVariableName.text);

            if (structNotFoundWarning != null)
            { Warnings.Add(structNotFoundWarning); }

            statement.position.Line = possibleType.lineNumber;
            statement.position.Extend(possibleType.startOffsetTotal, possibleVariableName.endOffsetTotal);

            if (ExpectOperator("=", out var eqT) != null)
            {
                if (possibleType.isList)
                { throw new SyntaxException("Initial value for list is not supported", eqT); }
                statement.initialValue = ExpectExpression() ?? throw new SyntaxException("Expected initial value after '=' in variable declaration", eqT);
            }
            else
            {
                if (IsRef)
                { throw new SyntaxException("Initial value for reference variable declaration is requied"); }
                if (possibleType.type == BuiltinType.AUTO)
                { throw new SyntaxException("Initial value for 'var' variable declaration is requied", possibleType.Position); }
            }

            return statement;
        }

        Statement_ForLoop ExpectForStatement()
        {
            if (ExpectIdentifier("for", out Token tokenFor) == null)
            { return null; }

            tokenFor.subtype = TokenSubtype.Statement;

            if (ExpectOperator("(", out Token tokenZarojel) == null)
            { throw new SyntaxException("Expected '(' after \"for\" statement", tokenFor); }

            var variableDeclaration = ExpectVariableDeclaration(false);
            if (variableDeclaration == null)
            { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenZarojel); }

            if (ExpectOperator(";") == null)
            { throw new SyntaxException("Expected ';' after \"for\" variable declaration", variableDeclaration.position); }

            Statement condition = ExpectExpression();
            if (condition == null)
            { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenZarojel.Position); }

            if (ExpectOperator(";") == null)
            { throw new SyntaxException("Expected ';' after \"for\" condition", variableDeclaration.position); }

            Statement expression = ExpectExpression();
            if (expression == null)
            { throw new SyntaxException("Expected expression after \"for\" condition", tokenZarojel.Position); }

            if (ExpectOperator(")", out Token tokenZarojel2) == null)
            { throw new SyntaxException("Expected ')' after \"for\" condition", condition.position); }

            if (ExpectOperator("{") == null)
            { throw new SyntaxException("Expected '{' after \"for\" condition", tokenZarojel2); }

            Statement_ForLoop forStatement = new()
            {
                name = tokenFor.text,
                variableDeclaration = variableDeclaration,
                condition = condition,
                expression = expression,
            };

            forStatement.position.Line = tokenFor.lineNumber;
            forStatement.position.Extend(tokenFor.Position.AbsolutePosition);
            forStatement.position.Extend(tokenZarojel2.Position.AbsolutePosition);

            int endlessSafe = 0;
            while (CurrentToken != null && CurrentToken != null && ExpectOperator("}") == null)
            {
                var statement = ExpectStatement();
                if (statement == null) break;
                if (ExpectOperator(";") == null)
                { throw new SyntaxException("Expected ';' at end of statement", statement.position); }

                forStatement.statements.Add(statement);

                endlessSafe++;
                if (endlessSafe > 500)
                {
                    throw new EndlessLoopException();
                }
            }

            return forStatement;
        }

        Statement_WhileLoop ExpectWhileStatement()
        {
            if (ExpectIdentifier("while", out Token tokenWhile) == null)
            { return null; }

            tokenWhile.subtype = TokenSubtype.Statement;

            if (ExpectOperator("(", out Token tokenZarojel) == null)
            { throw new SyntaxException("Expected '(' after \"while\" statement", tokenWhile); }

            Statement condition = ExpectExpression();
            if (condition == null)
            { throw new SyntaxException("Expected condition after \"while\" statement", tokenZarojel.Position); }

            if (ExpectOperator(")", out Token tokenZarojel2) == null)
            { throw new SyntaxException("Expected ')' after \"while\" condition", condition.position); }

            if (ExpectOperator("{", out Token tokenZarojel3) == null)
            { throw new SyntaxException("Expected '{' after \"while\" condition", tokenZarojel2); }

            Statement_WhileLoop whileStatement = new()
            {
                name = tokenWhile.text,
                condition = condition,
            };

            whileStatement.position.Line = tokenWhile.lineNumber;
            whileStatement.position.Extend(tokenWhile.Position.AbsolutePosition);
            whileStatement.position.Extend(tokenZarojel3.Position.AbsolutePosition);

            int endlessSafe = 0;
            while (CurrentToken != null && CurrentToken != null && ExpectOperator("}") == null)
            {
                var statement = ExpectStatement();
                if (ExpectOperator(";") == null)
                { throw new SyntaxException("Expected ';' at end of statement", statement.position); }
                if (statement == null) break;

                whileStatement.statements.Add(statement);

                endlessSafe++;
                if (endlessSafe > 500)
                { throw new EndlessLoopException(); }
            }

            return whileStatement;
        }

        Statement_If ExpectIfStatement()
        {
            Statement_If_Part ifStatement = ExpectIfSegmentStatement();
            if (ifStatement == null) return null;

            Statement_If statement = new();
            statement.parts.Add(ifStatement);

            statement.position.Line = ifStatement.position.Line;
            statement.position.Extend(ifStatement.position.AbsolutePosition);

            int endlessSafe = 0;
            while (true)
            {
                Statement_If_Part elseifStatement = ExpectIfSegmentStatement("elseif", Statement_If_Part.IfPart.If_ElseIfStatement);
                if (elseifStatement == null) break;
                statement.parts.Add(elseifStatement);
                statement.position.Extend(elseifStatement.position.AbsolutePosition);

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            Statement_If_Part elseStatement = ExpectIfSegmentStatement("else", Statement_If_Part.IfPart.If_ElseStatement, false);
            if (elseStatement != null)
            {
                statement.parts.Add(elseStatement);
                statement.position.Extend(elseStatement.position.AbsolutePosition);
            }

            return statement;
        }

        Statement_If_Part ExpectIfSegmentStatement(string ifSegmentName = "if", Statement_If_Part.IfPart ifSegmentType = Statement_If_Part.IfPart.If_IfStatement, bool needParameters = true)
        {
            if (ExpectIdentifier(ifSegmentName, out Token tokenIf) == null)
            { return null; }

            tokenIf.subtype = TokenSubtype.Statement;

            Statement condition = null;
            if (needParameters)
            {
                if (ExpectOperator("(", out Token tokenZarojel) == null)
                { throw new SyntaxException("Expected '(' after \"" + ifSegmentName + "\" statement", tokenIf); }
                condition = ExpectExpression();
                if (condition == null)
                { throw new SyntaxException("Expected condition after \"" + ifSegmentName + "\" statement", tokenZarojel.Position); }

                if (ExpectOperator(")", out _) == null)
                { throw new SyntaxException("Expected ')' after \"" + ifSegmentName + "\" condition", condition.position); }
            }
            if (ExpectOperator("{", out Token tokenZarojel3) == null)
            { throw new SyntaxException("Expected '{' after \"" + ifSegmentName + "\" condition", tokenIf); }

            Statement_If_Part ifStatement = null;

            switch (ifSegmentType)
            {
                case Statement_If_Part.IfPart.If_IfStatement:
                    ifStatement = new Statement_If_If()
                    {
                        name = tokenIf.text,
                        condition = condition,
                    };
                    break;
                case Statement_If_Part.IfPart.If_ElseIfStatement:
                    ifStatement = new Statement_If_ElseIf()
                    {
                        name = tokenIf.text,
                        condition = condition,
                    };
                    break;
                case Statement_If_Part.IfPart.If_ElseStatement:
                    ifStatement = new Statement_If_Else()
                    {
                        name = tokenIf.text
                    };
                    break;
            }

            if (ifStatement == null)
            { throw new InternalException(); }

            ifStatement.position.Line = tokenIf.lineNumber;
            ifStatement.position.Extend(tokenIf.Position.AbsolutePosition);
            ifStatement.position.Extend(tokenZarojel3.Position.AbsolutePosition);

            int endlessSafe = 0;
            while (CurrentToken != null && ExpectOperator("}") == null)
            {
                var statement = ExpectStatement();
                if (ExpectOperator(";") == null)
                {
                    if (statement == null)
                    { throw new SyntaxException("Expected a statement", CurrentToken); }
                    else
                    { throw new SyntaxException("Expected ';' at end of statement", statement.position); }
                }

                ifStatement.statements.Add(statement);

                endlessSafe++;
                if (endlessSafe > 500)
                { throw new EndlessLoopException(); }
            }

            return ifStatement;
        }

        /// <returns>
        /// <list type="bullet">
        /// <item>
        ///  <seealso cref="Statement_WhileLoop"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_ForLoop"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_FunctionCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_If"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Statement_NewVariable"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="ExpectExpression"></seealso>
        /// </item>
        /// </list>
        /// </returns>
        Statement ExpectStatement()
        {
            Statement statement = ExpectWhileStatement();
            statement ??= ExpectForStatement();
            statement ??= ExpectKeywordCall("return", true);
            statement ??= ExpectKeywordCall("break");
            statement ??= ExpectIfStatement();
            statement ??= ExpectVariableDeclaration(true);
            statement ??= ExpectExpression();
            return statement;
        }

        bool ExpectMethodCall(bool expectDot, out Statement_FunctionCall methodCall)
        {
            int startTokenIndex = currentTokenIndex;

            methodCall = null;

            if (expectDot)
            {
                if (ExpectOperator(".") == null)
                { currentTokenIndex = startTokenIndex; return false; }
            }

            if (ExpectIdentifier(out var possibleFunctionName) == null)
            { currentTokenIndex = startTokenIndex; return false; }

            if (ExpectOperator("(") == null)
            { currentTokenIndex = startTokenIndex; return false; }

            possibleFunctionName.subtype = TokenSubtype.MethodName;

            methodCall = new(CurrentNamespace.ToArray(), new string[0], true)
            {
                functionNameT = possibleFunctionName,
            };

            methodCall.position.Line = possibleFunctionName.lineNumber;
            methodCall.position.Extend(possibleFunctionName.Position.AbsolutePosition);

            bool expectParameter = false;

            int endlessSafe = 0;
            while (ExpectOperator(")") == null || expectParameter)
            {
                Statement parameter = ExpectReference() ?? ExpectExpression();
                if (parameter == null)
                { throw new SyntaxException("Expected expression as parameter", methodCall.position); }

                methodCall.position.Extend(parameter.position.AbsolutePosition);

                methodCall.parameters.Add(parameter);

                if (ExpectOperator(")", out Token outToken0) != null)
                {
                    methodCall.position.Extend(outToken0.Position.AbsolutePosition);
                    break;
                }

                if (ExpectOperator(",", out Token operatorVesszo) == null)
                { throw new SyntaxException("Expected ',' to separate parameters", parameter.position); }
                else
                { expectParameter = true; }

                methodCall.position.Extend(operatorVesszo.Position.AbsolutePosition);

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            if (possibleFunctionName.text == "type")
            {
                possibleFunctionName.subtype = TokenSubtype.Keyword;
            }

            return true;
        }

        /// <returns>
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
        /// </returns>
        /// <exception cref="SyntaxException"></exception>
        Statement ExpectExpression()
        {
            if (ExpectOperator("!", out var tNotOperator) != null)
            {
                Statement statement = ExpectOneValue();
                if (statement == null)
                { throw new SyntaxException("Expected value or expression after '!' operator", tNotOperator); }

                return new Statement_Operator("!", statement);
            }

            // Possible a variable
            Statement leftStatement = ExpectOneValue();
            if (leftStatement == null) return null;

            if (ExpectOperator(new string[] {
                    "+=", "-=", "*=", "/=", "%=",
                    "&=", "|=", "^=",
                }, out var o0) != null)
            {
                var valueToAssign = ExpectOneValue();
                if (valueToAssign == null)
                { throw new SyntaxException("Expected expression", o0); }

                Statement_Operator statementToAssign = new(o0.text.Replace("=", ""), leftStatement, valueToAssign);

                Statement_Operator operatorCall = new("=", leftStatement, statementToAssign);

                return operatorCall;
            }
            else if (ExpectOperator("++") != null)
            {
                Statement_Literal literalOne = new()
                {
                    value = "1",
                    type = types["int"],
                };

                Statement_Operator statementToAssign = new("+", leftStatement, literalOne);

                Statement_Operator operatorCall = new("=", leftStatement, statementToAssign);

                return operatorCall;
            }
            else if (ExpectOperator("--") != null)
            {
                Statement_Literal literalOne = new()
                {
                    value = "1",
                    type = types["int"],
                };

                Statement_Operator statementToAssign = new("-", leftStatement, literalOne);

                Statement_Operator operatorCall = new("=", leftStatement, statementToAssign);

                return operatorCall;
            }

            while (true)
            {
                Token op = ExpectOperator(new string[]
                {
                    "+", "-", "*", "/", "%",
                    "=",
                    "<", ">", ">=", "<=", "!=", "==", "&", "|", "^"
                });
                if (op == null) break;

                int rhsPrecedence = OperatorPrecedence(op.text);
                if (rhsPrecedence == 0)
                {
                    currentTokenIndex--;
                    return leftStatement;
                }

                Statement rightStatement = ExpectOneValue();
                if (rightStatement == null)
                {
                    currentTokenIndex--;
                    return leftStatement;
                }

                Statement rightmostStatement = FindRightmostStatement(leftStatement, rhsPrecedence);
                if (rightmostStatement != null)
                {
                    if (rightmostStatement is Statement_Operator rightmostOperator)
                    {
                        Statement_Operator operatorCall = new(op.text, rightmostOperator.Right, rightStatement);
                        rightmostOperator.Right = operatorCall;
                    }
                    else
                    {
                        Statement_Operator operatorCall = new(op.text, leftStatement, rightStatement);
                        leftStatement = operatorCall;
                    }
                }
                else
                {
                    Statement_Operator operatorCall = new(op.text, leftStatement, rightStatement);
                    leftStatement = operatorCall;
                }
            }

            return leftStatement;
        }

        Statement_Operator FindRightmostStatement(Statement statement, int rhsPrecedence)
        {
            if (statement is not Statement_Operator lhs) return null;
            if (OperatorPrecedence(lhs.Operator) >= rhsPrecedence) return null;
            if (lhs.InsideBracelet) return null;

            var rhs = FindRightmostStatement(lhs.Right, rhsPrecedence);

            if (rhs == null) return lhs;
            return rhs;
        }

        int OperatorPrecedence(string str)
        {
            if (operators.TryGetValue(str, out int precedence))
            { return precedence; }
            else
            { return 0; }
        }

        Statement_FunctionCall ExpectFunctionCall()
        {
            int startTokenIndex = currentTokenIndex;

            Token possibleFunctionName = ExpectIdentifier();
            if (possibleFunctionName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            List<string> targetLibraryPath = new();
            List<Token> targetLibraryPathTokens = new();

            while (ExpectOperator(".", out Token dotToken) != null)
            {
                Token libraryToken = ExpectIdentifier();
                if (libraryToken == null)
                {
                    throw new SyntaxException("Expected namespace, class, method, or field identifier", dotToken);
                }
                else
                {
                    targetLibraryPath.Add(possibleFunctionName.text);
                    targetLibraryPathTokens.Add(possibleFunctionName);
                    possibleFunctionName = libraryToken;
                }
            }

            if (possibleFunctionName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (ExpectOperator("(") == null)
            { currentTokenIndex = startTokenIndex; return null; }

            foreach (var token in targetLibraryPathTokens)
            { token.subtype = TokenSubtype.Library; }

            possibleFunctionName.subtype = TokenSubtype.MethodName;

            Statement_FunctionCall functionCall = new(CurrentNamespace.ToArray(), targetLibraryPath.ToArray())
            {
                functionNameT = possibleFunctionName,
            };

            functionCall.position.Line = possibleFunctionName.lineNumber;
            functionCall.position.Extend(possibleFunctionName.Position.AbsolutePosition);

            bool expectParameter = false;

            int endlessSafe = 0;
            while (ExpectOperator(")") == null || expectParameter)
            {
                Statement parameter = ExpectReference() ?? ExpectExpression();
                if (parameter == null)
                { throw new SyntaxException("Expected expression as parameter", functionCall.position); }

                functionCall.position.Extend(parameter.position.AbsolutePosition);

                functionCall.parameters.Add(parameter);

                if (ExpectOperator(")", out Token outToken0) != null)
                {
                    functionCall.position.Extend(outToken0.Position.AbsolutePosition);
                    break;
                }

                if (ExpectOperator(",", out Token operatorVesszo) == null)
                { throw new SyntaxException("Expected ',' to separate parameters", parameter.position); }
                else
                { expectParameter = true; }

                functionCall.position.Extend(operatorVesszo.Position.AbsolutePosition);

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            if (possibleFunctionName.text == "type" && targetLibraryPath.Count == 0)
            {
                possibleFunctionName.subtype = TokenSubtype.Keyword;
            }

            return functionCall;
        }

        Statement_MethodCall ExpectMethodCall()
        {
            int startTokenIndex = currentTokenIndex;

            Token possibleMethodName = ExpectIdentifier();
            if (possibleMethodName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (possibleMethodName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            possibleMethodName.subtype = TokenSubtype.MethodName;

            if (ExpectOperator("(") == null)
            { currentTokenIndex = startTokenIndex; return null; }

            Statement_MethodCall methodCall = new(CurrentNamespace.ToArray(), new string[0])
            {
                functionNameT = possibleMethodName
            };

            methodCall.position.Line = possibleMethodName.lineNumber;
            methodCall.position.Extend(possibleMethodName.Position.AbsolutePosition);

            bool expectParameter = false;

            int endlessSafe = 0;
            while (ExpectOperator(")") == null || expectParameter)
            {
                Statement parameter = ExpectExpression();
                if (parameter == null)
                {
                    throw new SyntaxException("Expected expression as parameter", methodCall.position);
                }


                methodCall.position.Extend(parameter.position.AbsolutePosition);

                methodCall.parameters.Add(parameter);

                if (ExpectOperator(")", out Token outToken0) != null)
                {
                    methodCall.position.Extend(outToken0.Position.AbsolutePosition);
                    break;
                }

                if (ExpectOperator(",", out Token operatorVesszo) == null)
                {
                    throw new SyntaxException("Expected ',' to separate parameters", parameter.position);
                }
                else
                { expectParameter = true; }

                methodCall.position.Extend(operatorVesszo.Position.AbsolutePosition);

                endlessSafe++;
                if (endlessSafe > 100) { throw new EndlessLoopException(); }
            }

            return methodCall;
        }

        /// <summary> return, break, continue, etc. </summary>
        Statement_FunctionCall ExpectKeywordCall(string name, bool haveParameters = false)
        {
            int startTokenIndex = currentTokenIndex;

            Token possibleFunctionName = ExpectIdentifier();
            if (possibleFunctionName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (possibleFunctionName.text != name)
            { currentTokenIndex = startTokenIndex; return null; }

            possibleFunctionName.subtype = TokenSubtype.Statement;

            Statement_FunctionCall functionCall = new(new string[0], new string[0])
            {
                functionNameT = possibleFunctionName
            };

            functionCall.position.Line = possibleFunctionName.lineNumber;
            functionCall.position.Extend(possibleFunctionName.Position.AbsolutePosition);

            if (haveParameters)
            {
                Statement parameter = ExpectExpression();
                if (parameter == null)
                    throw new SyntaxException("Expected expression as parameter", functionCall.position);

                functionCall.position.Extend(parameter.position.AbsolutePosition);

                functionCall.parameters.Add(parameter);
            }
            return functionCall;
        }

        #endregion

        bool ExpectAttribute(out FunctionDefinition.Attribute attribute)
        {
            int parseStart = currentTokenIndex;
            attribute = new();

            if (ExpectOperator("[") == null)
            { currentTokenIndex = parseStart; return false; }

            Token attributeT = ExpectIdentifier();
            if (attributeT == null)
            { currentTokenIndex = parseStart; return false; }

            attributeT.subtype = TokenSubtype.Type;

            if (ExpectOperator("(", out var t3) != null)
            {
                List<object> parameters = new();
                int endlessSafe = 50;
                while (ExpectOperator(")") == null)
                {
                    ExpectOneLiteral(out object param);
                    if (param == null)
                    { throw new SyntaxException("Expected parameter", t3); }
                    ExpectOperator(",");

                    parameters.Add(param);

                    endlessSafe--;
                    if (endlessSafe <= 0)
                    { throw new EndlessLoopException(); }
                }
                attribute.Parameters = parameters.ToArray();
            }

            if (ExpectOperator("]") == null)
            { throw new SyntaxException("Expected ] after attribute parameter list"); }

            attribute.Name = attributeT.text;
            return true;
        }

        FunctionDefinition ExpectMethodDefinition()
        {
            int parseStart = currentTokenIndex;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
                {
                    if (attribute.Name == attr.Name)
                    {
                        alreadyHave = true;
                        break;
                    }
                }
                if (!alreadyHave)
                {
                    attributes.Add(attr);
                }
                else
                { throw new ParserException("Attribute '" + attr + "' already applied to the method"); }
            }

            Type possibleType = ExceptType(false);
            if (possibleType == null)
            { currentTokenIndex = parseStart; return null; }

            Token possibleName = ExpectIdentifier();
            if (possibleName == null)
            { currentTokenIndex = parseStart; return null; }

            Token possibleOperator = ExpectOperator("(");
            if (possibleOperator == null)
            { currentTokenIndex = parseStart; return null; }

            FunctionDefinition methodDefinition = new(new string[0], possibleName.text)
            {
                type = possibleType,
                attributes = attributes.ToArray()
            };

            possibleName.subtype = TokenSubtype.MethodName;

            var expectParameter = false;
            while (ExpectOperator(")") == null || expectParameter)
            {
                Type possibleParameterType = ExceptType(false, true);
                if (possibleParameterType == null)
                    throw new SyntaxException("Expected parameter type", possibleOperator);

                Token possibleParameterName = ExpectIdentifier();
                if (possibleParameterName == null)
                    throw new SyntaxException("Expected a parameter name", possibleParameterType.Position);

                possibleParameterName.subtype = TokenSubtype.VariableName;

                ParameterDefinition parameterDefinition = new()
                {
                    type = possibleParameterType,
                    name = possibleParameterName.text
                };
                methodDefinition.parameters.Add(parameterDefinition);

                if (ExpectOperator(")") != null)
                { break; }

                if (ExpectOperator(",") == null)
                { throw new SyntaxException("Expected ',' or ')'", possibleParameterName); }
                else
                { expectParameter = true; }
            }

            List<Statement> statements = new();
            if (ExpectOperator(";") == null)
            {
                statements = ParseFunctionBody();
            }
            else
            {
                currentTokenIndex--;
            }

            if (statements == null)
            {
                currentTokenIndex = parseStart;
                return null;
            }
            methodDefinition.statements = statements;

            return methodDefinition;
        }

        ParameterDefinition ExpectField()
        {
            int startTokenIndex = currentTokenIndex;
            Type possibleType = ExceptType();
            if (possibleType == null)
            { currentTokenIndex = startTokenIndex; return null; }

            Token possibleVariableName = ExpectIdentifier();
            if (possibleVariableName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (ExpectOperator("(") != null)
            { currentTokenIndex = startTokenIndex; return null; }

            possibleVariableName.subtype = TokenSubtype.None;

            ParameterDefinition field = new()
            {
                name = possibleVariableName.text,
                type = possibleType,
            };

            return field;
        }

        void ExpectOneLiteral(out object value)
        {
            value = null;

            if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_FLOAT)
            {
                value = float.Parse(CurrentToken.text);

                currentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_NUMBER)
            {
                value = int.Parse(CurrentToken.text);

                currentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_STRING)
            {
                value = CurrentToken.text;

                currentTokenIndex++;
            }
        }

        #region Basic parsing

        Token ExpectIdentifier(string name)
        {
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.IDENTIFIER) return null;
            if (name.Length > 0 && CurrentToken.text != name) return null;

            Token returnToken = CurrentToken;
            currentTokenIndex++;

            return returnToken;
        }
        Token ExpectIdentifier() { return ExpectIdentifier(""); }
        Token ExpectIdentifier(out Token result)
        {
            result = null;
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.IDENTIFIER) return null;
            if ("".Length > 0 && CurrentToken.text != "") return null;

            Token returnToken = CurrentToken;
            result = returnToken;
            currentTokenIndex++;

            return returnToken;
        }
        Token ExpectIdentifier(string name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.IDENTIFIER) return null;
            if (name.Length > 0 && CurrentToken.text != name) return null;

            Token returnToken = CurrentToken;
            result = returnToken;
            currentTokenIndex++;

            return returnToken;
        }

        Token ExpectOperator(string name)
        {
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.OPERATOR) return null;
            if (name.Length > 0 && CurrentToken.text != name) return null;

            Token returnToken = CurrentToken;
            currentTokenIndex++;

            return returnToken;
        }
        Token ExpectOperator(string[] name)
        {
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.OPERATOR) return null;
            if (name.Contains(CurrentToken.text) == false) return null;

            Token returnToken = CurrentToken;
            currentTokenIndex++;

            return returnToken;
        }
        Token ExpectOperator(string[] name, out Token outToken)
        {
            outToken = null;
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.OPERATOR) return null;
            if (name.Contains(CurrentToken.text) == false) return null;

            Token returnToken = CurrentToken;
            currentTokenIndex++;
            outToken = returnToken;
            return returnToken;
        }
        Token ExpectOperator(string name, out Token outToken)
        {
            outToken = null;
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.OPERATOR) return null;
            if (name.Length > 0 && CurrentToken.text != name) return null;

            Token returnToken = CurrentToken;
            outToken = returnToken;
            currentTokenIndex++;

            return returnToken;
        }

        Type ExceptType(out Warning warning, bool allowVarKeyword = true, bool allowAnyKeyword = false)
        {
            warning = null;
            Token possibleType = ExpectIdentifier();
            if (possibleType == null) return null;

            possibleType.subtype = TokenSubtype.Keyword;

            bool typeFound = types.TryGetValue(possibleType.text, out Type foundtype);

            Type newType = null;

            if (typeFound == false)
            {
                if (newType == null && possibleType.text == "any")
                {
                    if (allowAnyKeyword)
                    { newType = new Type("any", BuiltinType.ANY); }
                    else
                    {
                        throw new ParserException($"Type '{possibleType.text}' is not valid in the current context");
                    }
                }

                if (newType == null && possibleType.text == "var")
                {
                    if (allowVarKeyword)
                    { newType = new Type("var", BuiltinType.AUTO); }
                    else
                    {
                        throw new ParserException($"Type '{possibleType.text}' is not valid in the current context");
                    }
                }

                if (newType == null)
                {
                    if (TryGetStruct(possibleType.text, out var s))
                    {
                        newType = new Type(s.FullName, BuiltinType.STRUCT);
                        possibleType.subtype = TokenSubtype.Struct;
                    }
                    else
                    {
                        newType = new Type(possibleType.text, BuiltinType.STRUCT);
                        possibleType.subtype = TokenSubtype.None;
                        warning = new Warning()
                        {
                            Message = $"Struct '{possibleType.text}' not found",
                            Position = possibleType.Position
                        };
                    }
                }

                if (newType == null)
                { return null; }
            }
            else
            { newType = foundtype.Clone(); }

            newType.AddTokenProperties(possibleType);

            if (ExpectOperator("[") != null)
            {
                if (ExpectOperator("]") != null)
                { newType.isList = true; }
                else
                { throw new SyntaxException("Unbalanced '['"); }
            }
            return newType;
        }

        Type ExceptType(bool allowVarKeyword = true, bool allowAnyKeyword = false) => ExceptType(out Warning _, allowVarKeyword, allowAnyKeyword);

        bool TryGetStruct(string structName, out StructDefinition @struct)
        {
            if (Structs.TryGetValue(structName, out @struct))
            {
                return true;
            }
            return false;
        }

        #endregion
    }
}
