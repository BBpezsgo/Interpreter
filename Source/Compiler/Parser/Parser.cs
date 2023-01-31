using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

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
        BuiltinType,
        Hash,
        HashParameter,
        Class,
    }

    public enum BuiltinType
    {
        AUTO,
        BYTE,
        INT,
        FLOAT,
        VOID,
        STRING,
        BOOLEAN,
        STRUCT,
        LISTOF,

        ANY,
    }

    [Serializable]
    public class TypeToken : Token
    {
        public BuiltinType typeName;
        public TypeToken ListOf;
        public bool IsList => ListOf != null;

        public TypeToken(string name, BuiltinType type, Token @base) : base()
        {
            this.typeName = type;

            base.text = name;

            if (@base == null)
            {
                base.type = TokenType.IDENTIFIER;
            }
            else
            {
                base.AbsolutePosition = @base.AbsolutePosition;
                base.Position = @base.Position;
                base.Analysis = @base.Analysis;
                base.type = @base.type;
            }
        }

        public TypeToken(string name, TypeToken listOf, Token @base) : base()
        {
            this.typeName = BuiltinType.LISTOF;
            this.ListOf = listOf;

            base.text = name;

            if (@base == null)
            {
                base.type = TokenType.IDENTIFIER;
            }
            else
            {
                base.AbsolutePosition = @base.AbsolutePosition;
                base.Position = @base.Position;
                base.Analysis = @base.Analysis;
                base.type = @base.type;
            }
        }

        public new TypeToken Clone() => new(this.text, this.typeName, this)
        {
            type = this.type,

            AbsolutePosition = new Range<int>(this.AbsolutePosition.Start, this.AbsolutePosition.End),
            Position = new Range<SinglePosition>()
            {
                Start = new SinglePosition(Position.Start.Line, Position.Start.Character),
                End = new SinglePosition(Position.End.Line, Position.End.Character),
            },

            ListOf = this.ListOf,
            Analysis = new TokenAnalysis()
            {
                Subtype = Analysis.Subtype,
            },
        };

        public static TypeToken CreateAnonymous(string name, BuiltinType type) => new(name, type, null);
        public static TypeToken CreateAnonymous(string name, TypeToken listOf) => new(name, listOf, null);

        public override string ToString()
        {
            if (IsList) return ListOf.ToString() + "[]";
            return text;
        }
        public new string ToFullString()
        {
            return $"TypeToken {{ {this.ToString()} {base.ToFullString()} }}";
        }
    }

    namespace Parser
    {
        using Statements;

        public struct ParserSettings
        {
            public bool PrintInfo;

            public static ParserSettings Default => new()
            {
                PrintInfo = false,
            };
        }

        public interface IDefinition
        {
            public string FilePath { get; set; }
        }

        [Serializable]
        public class ParameterDefinition
        {
            public Token name;
            public TypeToken type;

            public bool withThisKeyword;

            public override string ToString()
            {
                return $"{type} {name}";
            }

            internal string PrettyPrint(int ident = 0)
            {
                return $"{" ".Repeat(ident)}{type} {name}";
            }
        }

        public class Exportable
        {
            public Token ExportKeyword;
            public bool IsExport => ExportKeyword != null;
        }

        public class FunctionDefinition : Exportable, IDefinition
        {
            public class Attribute
            {
                public Token Name;
                public object[] Parameters;
            }

            public Token BracketStart;
            public Token BracketEnd;

            public Attribute[] Attributes;
            public readonly Token Name;
            /// <summary>
            /// <c>[Namespace].[...].Name</c>
            /// </summary>
            public string FullName => NamespacePathString + Name.text;
            public List<ParameterDefinition> Parameters;
            public List<Statement> Statements;
            public TypeToken Type;
            public readonly string[] NamespacePath;
            /// <summary>
            /// <c>[Namespace].[...].</c>
            /// </summary>
            string NamespacePathString
            {
                get
                {
                    string val = "";
                    for (int i = 0; i < NamespacePath.Length; i++)
                    {
                        if (val.Length > 0)
                        {
                            val += "." + NamespacePath[i].ToString();
                        }
                        else
                        {
                            val = NamespacePath[i].ToString();
                        }
                    }
                    if (val.Length > 0)
                    {
                        val += ".";
                    }
                    return val;
                }
            }

            public string FilePath { get; set; }

            public FunctionDefinition(string[] namespacePath, Token name)
            {
                Parameters = new List<ParameterDefinition>();
                Statements = new List<Statement>();
                Attributes = Array.Empty<Attribute>();
                this.NamespacePath = namespacePath;
                this.Name = name;
            }

            public FunctionDefinition(List<string> namespacePath, Token name)
            {
                Parameters = new List<ParameterDefinition>();
                Statements = new List<Statement>();
                Attributes = Array.Empty<Attribute>();
                this.NamespacePath = namespacePath.ToArray();
                this.Name = name;
            }

            public override string ToString()
            {
                return $"{(IsExport ? "export " : "")}{this.Type.text} {this.FullName}" + (this.Parameters.Count > 0 ? "(...)" : "()") + " " + (this.Statements.Count > 0 ? "{...}" : "{}");
            }

            public string PrettyPrint(int ident = 0)
            {
                List<string> parameters = new();
                foreach (var parameter in this.Parameters)
                {
                    parameters.Add(parameter.PrettyPrint((ident == 0) ? 2 : ident));
                }

                List<string> statements = new();
                foreach (var statement in this.Statements)
                {
                    statements.Add($"{" ".Repeat(ident)}" + statement.PrettyPrint((ident == 0) ? 2 : ident) + ";");
                }

                return $"{" ".Repeat(ident)}{this.Type.text} {this.FullName}" + ($"({string.Join(", ", parameters)})") + " " + (this.Statements.Count > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
            }

            public string ReadableID()
            {
                string result = this.FullName;
                result += "(";
                for (int j = 0; j < this.Parameters.Count; j++)
                {
                    if (j > 0) { result += ", "; }
                    result += this.Parameters[j].type.ToString();
                }
                result += ")";
                return result;
            }

            public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
        }

        public class ClassDefinition : Exportable, IDefinition
        {
            public readonly FunctionDefinition.Attribute[] Attributes;
            public readonly Token Name;
            /// <summary><c>[Namespace].[...].Name</c></summary>
            public string FullName => NamespacePathString + Name.text;
            public Token BracketStart;
            public Token BracketEnd;
            public List<Statement> Statements;
            public string FilePath { get; set; }
            public readonly string[] NamespacePath;
            /// <summary><c>[Namespace].[...].</c></summary>
            string NamespacePathString
            {
                get
                {
                    string result = "";
                    for (int i = 0; i < NamespacePath.Length; i++)
                    {
                        if (result.Length > 0)
                        {
                            result += "." + NamespacePath[i].ToString();
                        }
                        else
                        {
                            result = NamespacePath[i].ToString();
                        }
                    }
                    if (result.Length > 0)
                    {
                        result += ".";
                    }
                    return result;
                }
            }
            public readonly ParameterDefinition[] Fields;
            public IReadOnlyDictionary<string, FunctionDefinition> Methods => methods;
            readonly Dictionary<string, FunctionDefinition> methods;

            public ClassDefinition(IEnumerable<string> namespacePath, Token name, IEnumerable<FunctionDefinition.Attribute> attributes, IEnumerable<ParameterDefinition> fields, IEnumerable<KeyValuePair<string, FunctionDefinition>> methods)
            {
                this.Name = name;
                this.Fields = fields.ToArray();
                this.methods = new Dictionary<string, FunctionDefinition>(methods);
                this.Attributes = attributes.ToArray();
                this.NamespacePath = namespacePath.ToArray();
                this.Statements = new List<Statement>();
            }

            public override string ToString()
            {
                return $"class {this.Name.text} " + "{...}";
            }

            public string PrettyPrint(int ident = 0)
            {
                List<string> fields = new();
                foreach (var field in this.Fields)
                {
                    fields.Add($"{" ".Repeat(ident)}" + field.PrettyPrint((ident == 0) ? 2 : ident) + ";");
                }

                List<string> methods = new();
                foreach (var method in this.methods)
                {
                    methods.Add($"{" ".Repeat(ident)}" + method.Value.PrettyPrint((ident == 0) ? 2 : ident) + ";");
                }

                return $"{" ".Repeat(ident)}class {this.Name.text} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
            }

            public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
        }

        public class StructDefinition : Exportable, IDefinition
        {
            public readonly FunctionDefinition.Attribute[] Attributes;
            public readonly Token Name;
            /// <summary><c>[Namespace].[...].Name</c></summary>
            public string FullName => NamespacePathString + Name.text;
            public Token BracketStart;
            public Token BracketEnd;
            public List<Statement> Statements;
            public string FilePath { get; set; }
            public readonly string[] NamespacePath;
            /// <summary><c>[Namespace].[...].</c></summary>
            string NamespacePathString
            {
                get
                {
                    string result = "";
                    for (int i = 0; i < NamespacePath.Length; i++)
                    {
                        if (result.Length > 0)
                        {
                            result += "." + NamespacePath[i].ToString();
                        }
                        else
                        {
                            result = NamespacePath[i].ToString();
                        }
                    }
                    if (result.Length > 0)
                    {
                        result += ".";
                    }
                    return result;
                }
            }
            public readonly ParameterDefinition[] Fields;
            public IReadOnlyDictionary<string, FunctionDefinition> Methods => methods;
            readonly Dictionary<string, FunctionDefinition> methods;

            public StructDefinition(IEnumerable<string> namespacePath, Token name, IEnumerable<FunctionDefinition.Attribute> attributes, IEnumerable<ParameterDefinition> fields, IEnumerable<KeyValuePair<string, FunctionDefinition>> methods)
            {
                this.Name = name;
                this.Fields = fields.ToArray();
                this.methods = new Dictionary<string, FunctionDefinition>(methods);
                this.Attributes = attributes.ToArray();
                this.NamespacePath = namespacePath.ToArray();
                this.Statements = new List<Statement>();
            }

            public override string ToString()
            {
                return $"struct {this.Name.text} " + "{...}";
            }

            public string PrettyPrint(int ident = 0)
            {
                List<string> fields = new();
                foreach (var field in this.Fields)
                {
                    fields.Add($"{" ".Repeat(ident)}" + field.PrettyPrint((ident == 0) ? 2 : ident) + ";");
                }

                List<string> methods = new();
                foreach (var method in this.methods)
                {
                    methods.Add($"{" ".Repeat(ident)}" + method.Value.PrettyPrint((ident == 0) ? 2 : ident) + ";");
                }

                return $"{" ".Repeat(ident)}struct {this.Name.text} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
            }

            public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
        }

        public class NamespaceDefinition : IDefinition
        {
            public Token Name;
            public Token Keyword;
            public Token BracketStart;
            public Token BracketEnd;
            public string FilePath { get; set; }

            public override string ToString()
            {
                return $"namespace {this.Name.text} " + "{ ... }";
            }
        }

        public struct UsingAnalysis
        {
            public string Path;
            public bool Found;
            public double ParseTime;
        }

        public struct ParserResult
        {
            public readonly List<FunctionDefinition> Functions;
            public readonly Dictionary<string, StructDefinition> Structs;
            public readonly Dictionary<string, ClassDefinition> Classes;
            public readonly List<Statement_NewVariable> GlobalVariables;
            public readonly List<UsingDefinition> Usings;
            public readonly Statement_HashInfo[] Hashes;
            public readonly List<UsingAnalysis> UsingsAnalytics;
            /// <summary>
            /// Only used for diagnostics!
            /// </summary>
            public readonly List<NamespaceDefinition> Namespaces;

            public ParserResult(List<FunctionDefinition> functions, List<Statement_NewVariable> globalVariables, Dictionary<string, StructDefinition> structs, List<UsingDefinition> usings, List<NamespaceDefinition> namespaces, List<Statement_HashInfo> hashes, Dictionary<string, ClassDefinition> classes)
            {
                Functions = functions;
                GlobalVariables = globalVariables;
                Structs = structs;
                Usings = usings;
                UsingsAnalytics = new();
                Namespaces = namespaces;
                Hashes = hashes.ToArray();
                Classes = classes;
            }

            /// <summary>Converts the parsed AST into text</summary>
            public string PrettyPrint()
            {
                var x = "";

                foreach (var @using in Usings)
                {
                    x += "using " + @using.PathString + ";\n";
                }

                foreach (var globalVariable in GlobalVariables)
                {
                    x += globalVariable.PrettyPrint() + ";\n";
                }

                foreach (var @struct in Structs)
                {
                    x += @struct.Value.PrettyPrint() + "\n";
                }

                foreach (var @class in Classes)
                {
                    x += @class.Value.PrettyPrint() + "\n";
                }

                foreach (var function in Functions)
                {
                    x += function.PrettyPrint() + "\n";
                }

                return x;
            }

            public void WriteToConsole(string title = "PARSER INFO")
            {
                Console.WriteLine($"\n\r === {title} ===\n\r");
                // int indent = 0;

                /*
                void Comment(string comment)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"{"  ".Repeat(indent)}{comment}");
                    Console.ResetColor();
                }
                */
                void Attribute(FunctionDefinition.Attribute attribute)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("[");
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(attribute.Name);
                    if (attribute.Parameters != null && attribute.Parameters.Length > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write("(");
                        for (int i = 0; i < attribute.Parameters.Length; i++)
                        {
                            if (i > 0)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.Write($", ");
                            }
                            Console.ForegroundColor = ConsoleColor.White;
                            Value(attribute.Parameters[i]);
                        }
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(")");
                    }
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("]");
                    Console.Write("\n\r");
                    Console.ResetColor();
                }
                void Value(object v)
                {
                    if (v is int || v is float)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.Write(v);
                    }
                    else if (v is bool)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(v);
                    }
                    else if (v is string)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write($"\"{v}\"");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(v);
                    }
                }

                Console.WriteLine("");

                foreach (var item in Usings)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("using ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"{item}");
                    Console.ResetColor();
                }

                Console.WriteLine("");

                foreach (var item in this.GlobalVariables)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{item.Type} ");

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{item.VariableName}");

                    Console.Write("\n\r");

                    Console.ResetColor();
                }

                Console.WriteLine("");

                foreach (var item in this.Structs)
                {
                    foreach (var attr in item.Value.Attributes)
                    { Attribute(attr); }

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("struct ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{item.Key} ");
                    Console.Write("\n\r");

                    foreach (var field in item.Value.Fields)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write($"  {field.type} ");

                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{field.name}");

                        Console.Write("\n\r");
                    }

                    Console.Write("\n\r");
                    Console.ResetColor();
                }

                foreach (var item in this.Classes)
                {
                    foreach (var attr in item.Value.Attributes)
                    { Attribute(attr); }

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("class ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{item.Key} ");
                    Console.Write("\n\r");

                    foreach (var field in item.Value.Fields)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write($"  {field.type} ");

                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{field.name}");

                        Console.Write("\n\r");
                    }

                    Console.Write("\n\r");
                    Console.ResetColor();
                }

                Console.WriteLine("");

                foreach (var item in this.Functions)
                {
                    foreach (var attr in item.Attributes)
                    { Attribute(attr); }

                    Console.ForegroundColor = ConsoleColor.Blue;
                    if (item.Type.typeName == BuiltinType.STRUCT)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    Console.Write($"{item.Type} ");

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"{item.FullName}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("(");
                    for (int i = 0; i < item.Parameters.Count; i++)
                    {
                        if (i > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write($", ");
                        }

                        ParameterDefinition param = item.Parameters[i];
                        Console.ForegroundColor = ConsoleColor.Blue;
                        if (param.withThisKeyword)
                        { Console.Write("this "); }
                        if (param.type.typeName == BuiltinType.STRUCT)
                        { Console.ForegroundColor = ConsoleColor.Green; }
                        Console.Write($"{param.type} ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{param.name}");
                    }
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(")");

                    if (item.Statements.Count > 0)
                    {
                        int statementCount = 0;
                        void AddStatement(Statement st)
                        {
                            statementCount++;
                            if (st is Statement_ForLoop st0)
                            {
                                AddStatement(st0.Condition);
                                AddStatement(st0.VariableDeclaration);
                                AddStatement(st0.Expression);
                            }
                            else if (st is Statement_WhileLoop st1)
                            {
                                AddStatement(st1.Condition);
                            }
                            else if (st is Statement_FunctionCall st2)
                            {
                                foreach (var st3 in st2.Parameters)
                                {
                                    AddStatement(st3);
                                }
                            }
                            else if (st is Statement_If_If st3)
                            {
                                AddStatement(st3.Condition);
                            }
                            else if (st is Statement_If_ElseIf st4)
                            {
                                AddStatement(st4.Condition);
                            }
                            else if (st is Statement_Index st5)
                            {
                                AddStatement(st5.Expression);
                                statementCount++;
                            }
                            else if (st is Statement_NewVariable st6)
                            {
                                if (st6.InitialValue != null) AddStatement(st6.InitialValue);
                            }
                            else if (st is Statement_Operator st7)
                            {
                                if (st7.Left != null) AddStatement(st7.Left);
                                if (st7.Right != null) AddStatement(st7.Right);
                            }

                            if (st is StatementParent st8)
                            {
                                foreach (var item in st8.Statements)
                                {
                                    AddStatement(st);
                                }
                            }
                        }
                        foreach (var st in item.Statements)
                        {
                            AddStatement(st);
                        }

                        Console.Write($"\n\r{{ {statementCount} statements }}\n\r");
                    }
                    else
                    {
                        Console.Write("\n\r");
                    }

                    Console.Write("\n\r");
                    Console.ResetColor();
                }

                Console.WriteLine("\n\r === ===\n\r");
            }

            public void SetFile(string path)
            {
                if (string.IsNullOrEmpty(path))
                { throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path)); }

                for (int i = 0; i < this.Functions.Count; i++)
                { this.Functions[i].FilePath = path; }
                for (int j = 0; j < this.Namespaces.Count; j++)
                { this.Namespaces[j].FilePath = path; }
                for (int i = 0; i < this.GlobalVariables.Count; i++)
                { this.GlobalVariables[i].FilePath = path; }
                for (int i = 0; i < this.Structs.Count; i++)
                { this.Structs.ElementAt(i).Value.FilePath = path; }
                for (int i = 0; i < this.Hashes.Length; i++)
                { this.Hashes[i].FilePath = path; }
                StatementFinder.GetAllStatement(this, statement =>
                {
                    if (statement is not IDefinition def) return false;
                    def.FilePath = path;
                    return false;
                });
            }

            public void CheckFilePaths(System.Action<string> NotSetCallback)
            {
                for (int i = 0; i < this.Functions.Count; i++)
                {
                    if (string.IsNullOrEmpty(this.Functions[i].FilePath))
                    { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {this.Functions[i]} is null"); }
                    else
                    { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {this.Functions[i]} : {this.Functions[i].FilePath}"); }
                }
                for (int i = 0; i < this.Namespaces.Count; i++)
                {
                    if (string.IsNullOrEmpty(this.Namespaces[i].FilePath))
                    { NotSetCallback?.Invoke($"Namespace.FilePath {this.Namespaces[i]} is null"); }
                    else
                    { NotSetCallback?.Invoke($"Namespace.FilePath {this.Namespaces[i]} : {this.Namespaces[i].FilePath}"); }
                }
                for (int i = 0; i < this.GlobalVariables.Count; i++)
                {
                    if (string.IsNullOrEmpty(this.GlobalVariables[i].FilePath))
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {this.GlobalVariables[i]} is null"); }
                    else
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {this.GlobalVariables[i]} : {this.GlobalVariables[i].FilePath}"); }
                }
                for (int i = 0; i < this.Structs.Count; i++)
                {
                    if (string.IsNullOrEmpty(this.Structs.ElementAt(i).Value.FilePath))
                    { NotSetCallback?.Invoke($"StructDefinition.FilePath {this.Structs.ElementAt(i).Value} is null"); }
                    else
                    { NotSetCallback?.Invoke($"StructDefinition.FilePath {this.Structs.ElementAt(i).Value} : {this.Structs.ElementAt(i).Value.FilePath}"); }
                }
                for (int i = 0; i < this.Hashes.Length; i++)
                {
                    if (string.IsNullOrEmpty(this.Hashes[i].FilePath))
                    { NotSetCallback?.Invoke($"Hash.FilePath {this.Hashes[i]} is null"); }
                    else
                    { NotSetCallback?.Invoke($"Hash.FilePath {this.Hashes[i]} : {this.Hashes[i].FilePath}"); }
                }
                StatementFinder.GetAllStatement(this, statement =>
                {
                    if (statement is not IDefinition def) return false;
                    if (string.IsNullOrEmpty(def.FilePath))
                    { NotSetCallback?.Invoke($"IDefinition.FilePath {def} is null"); }
                    else
                    { NotSetCallback?.Invoke($"IDefinition.FilePath {def} : {def.FilePath}"); }
                    return false;
                });
            }
        }

        public struct ParserResultHeader
        {
            public readonly List<UsingDefinition> Usings;
            public readonly Statement_HashInfo[] Hashes;
            public readonly List<UsingAnalysis> UsingsAnalytics;

            public ParserResultHeader(List<UsingDefinition> usings, List<Statement_HashInfo> hashes)
            {
                Usings = usings;
                UsingsAnalytics = new();
                Hashes = hashes.ToArray();
            }
        }

        public class UsingDefinition
        {
            public Token[] Path;
            public Token Keyword;

            public string PathString
            {
                get
                {
                    string result = "";
                    for (int i = 0; i < Path.Length; i++)
                    {
                        if (i > 0) result += ".";
                        result += Path[i].text;
                    }
                    return result;
                }
            }
            public bool IsUrl => Path.Length == 1 && Uri.TryCreate(Path[0].text, UriKind.Absolute, out var uri) && uri.Scheme != "file:";
        }

        /// <summary>
        /// The parser for the BBCode language
        /// </summary>
        public class Parser
        {
            int currentTokenIndex;
            readonly List<Token> tokens = new();
            public Token[] Tokens => tokens.ToArray();

            Token CurrentToken
            {
                get
                {
                    if (currentTokenIndex < tokens.Count)
                    {
                        tokens[currentTokenIndex].Analysis.ParserReached = true;
                        return tokens[currentTokenIndex];
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            static readonly Dictionary<string, TypeToken> types = new()
            {
                { "int", TypeToken.CreateAnonymous("int", BuiltinType.INT) },
                { "string", TypeToken.CreateAnonymous("string", BuiltinType.STRING) },
                { "void", TypeToken.CreateAnonymous("void", BuiltinType.VOID) },
                { "float", TypeToken.CreateAnonymous("float", BuiltinType.FLOAT) },
                { "bool", TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN) },
                { "byte", TypeToken.CreateAnonymous("byte", BuiltinType.BYTE) }
            };
            readonly Dictionary<string, int> operators = new();
            readonly List<string> CurrentNamespace = new();

            List<Warning> Warnings;
            public readonly List<Error> Errors = new();

            // === Result ===
            readonly List<FunctionDefinition> Functions = new();
            readonly Dictionary<string, StructDefinition> Structs = new();
            readonly Dictionary<string, ClassDefinition> Classes = new();
            readonly List<Statement_NewVariable> GlobalVariables = new();
            readonly List<UsingDefinition> Usings = new();
            readonly List<NamespaceDefinition> Namespaces = new();
            readonly List<Statement_HashInfo> Hashes = new();
            // === ===

            public Parser()
            {
                operators.Add("|", 4);
                operators.Add("&", 4);
                operators.Add("^", 4);
                operators.Add("<<", 4);
                operators.Add(">>", 4);

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

            /// <summary>
            /// Parses tokens into AST
            /// </summary>
            /// <param name="_tokens">
            /// The list of tokens<br/>
            /// This should be generated using <see cref="IngameCoding.BBCode.Tokenizer"/>
            /// </param>
            /// <param name="warnings">
            /// A list that the parser can fill with warnings
            /// </param>
            /// <exception cref="EndlessLoopException"/>
            /// <exception cref="SyntaxException"/>
            /// <exception cref="Exception"/>
            /// <returns>
            /// The generated AST
            /// </returns>
            public ParserResult Parse(Token[] _tokens, List<Warning> warnings)
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

                return new ParserResult(this.Functions, this.GlobalVariables, this.Structs, this.Usings, this.Namespaces, this.Hashes, this.Classes);
            }

            public ParserResultHeader ParseCodeHeader(Token[] _tokens, List<Warning> warnings)
            {
                Warnings = warnings;
                tokens.Clear();
                tokens.AddRange(_tokens);

                currentTokenIndex = 0;

                ParseCodeHeader();

                return new ParserResultHeader(this.Usings, this.Hashes);
            }

            /// <summary>
            /// This will call the <see cref="Tokenizer.Parse"/> and the <see cref="Parser.Parse"/>
            /// </summary>
            /// <param name="code">
            /// The source code
            /// </param>
            /// <param name="warnings">
            /// A list that the tokenizer and the parser can fill with warnings
            /// </param>
            /// <param name="printCallback">
            /// Optional: Print callback
            /// </param>
            /// <exception cref="EndlessLoopException"/>
            /// <exception cref="SyntaxException"/>
            /// <exception cref="Exception"/>
            /// <exception cref="InternalException"/>
            /// <exception cref="NotImplementedException"/>
            /// <exception cref="System.Exception"/>
            /// <returns>
            /// The AST
            /// </returns>
            public static ParserResult Parse(string code, List<Warning> warnings, Action<string, Output.LogType> printCallback = null)
            {
                var tokenizer = new Tokenizer(TokenizerSettings.Default);
                var (tokens, _) = tokenizer.Parse(code);

                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing ...", Output.LogType.Debug); }

                Parser parser = new();
                var result = parser.Parse(tokens, warnings);

                if (parser.Errors.Count > 0)
                {
                    throw new Exception("Failed to parse", parser.Errors[0].ToException());
                }

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", Output.LogType.Debug); }

                return result;
            }

            #region Parse top level

            bool ExpectHash(out Statement_HashInfo hashStatement)
            {
                hashStatement = null;

                if (!ExpectOperator("#", out var hashT))
                { return false; }

                hashT.Analysis.Subtype = TokenSubtype.Hash;

                if (!ExpectIdentifier(out var hashName))
                { throw new SyntaxException($"Expected identifier after '#' , got {CurrentToken.type.ToString().ToLower()} \"{CurrentToken.text}\"", hashT); }

                hashName.Analysis.Subtype = TokenSubtype.Hash;

                List<Statement_Literal> parameters = new();
                int endlessSafe = 50;
                while (!ExpectOperator(";"))
                {
                    if (!ExpectLiteral(out var parameter))
                    { throw new SyntaxException($"Expected hash literal parameter or ';' , got {CurrentToken.type.ToString().ToLower()} \"{CurrentToken.text}\"", CurrentToken); }

                    parameter.ValueToken.Analysis.Subtype = TokenSubtype.HashParameter;
                    parameters.Add(parameter);

                    if (ExpectOperator(";"))
                    { break; }

                    endlessSafe--;
                    if (endlessSafe <= 0)
                    { throw new EndlessLoopException(); }
                }

                hashStatement = new Statement_HashInfo
                {
                    HashToken = hashT,
                    HashName = hashName,
                    Parameters = parameters.ToArray()
                };

                return true;
            }

            bool ExpectUsing(out UsingDefinition usingDefinition)
            {
                usingDefinition = null;
                if (!ExpectIdentifier("using", out var keyword))
                { return false; }

                keyword.Analysis.Subtype = TokenSubtype.Keyword;

                List<Token> tokens = new();
                if (CurrentToken.type == TokenType.LITERAL_STRING)
                {
                    tokens.Add(CurrentToken);
                    currentTokenIndex++;
                }
                else
                {
                    int endlessSafe = 50;
                    while (ExpectIdentifier(out Token pathIdentifier))
                    {
                        pathIdentifier.Analysis.Subtype = TokenSubtype.Library;
                        tokens.Add(pathIdentifier);

                        ExpectOperator(".");

                        endlessSafe--;
                        if (endlessSafe <= 0)
                        { throw new EndlessLoopException(); }
                    }
                }

                if (tokens.Count == 0)
                {
                    if (!ExpectOperator(";"))
                    {
                        throw new SyntaxException("Expected library name after 'using'", keyword);
                    }
                    else
                    {
                        Errors.Add(new Error("Expected library name after 'using'", keyword));
                        return true;
                    }
                }

                if (!ExpectOperator(";"))
                { throw new SyntaxException("Expected ';' at end of statement (after 'using')", keyword); }

                usingDefinition = new UsingDefinition
                {
                    Path = tokens.ToArray(),
                    Keyword = keyword,
                };

                return true;
            }

            void ParseCodeHeader()
            {
                while (ExpectHash(out var hash))
                { Hashes.Add(hash); }
                while (ExpectUsing(out var usingNamespace))
                { Usings.Add(usingNamespace); }
            }

            void ParseCodeBlock()
            {
                if (ExpectNamespaceDefinition()) { }
                else if (ExpectStructDefinition()) { }
                else if (ExpectClassDefinition()) { }
                else if (ExpectFunctionDefinition()) { }
                else if (ExpectGlobalVariable()) { }
                else
                { throw new SyntaxException($"Expected global variable or namespace/type/function definition. Got a token {CurrentToken}", CurrentToken); }
            }

            bool ExpectGlobalVariable()
            {
                var possibleVariable = ExpectVariableDeclaration();
                if (possibleVariable != null)
                {
                    GlobalVariables.Add(possibleVariable);

                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error("Expected ';' at end of statement (after global variable definition)", CurrentToken)); }

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
                    { Errors.Add(new Error("Attribute '" + attr + "' already applied to the function", attr.Name)); }
                }

                ExpectIdentifier("export", out Token ExportKeyword);

                TypeToken possibleType = ExceptTypeToken(false);
                if (possibleType == null)
                { currentTokenIndex = parseStart; return false; }

                if (!ExpectIdentifier(out Token possibleNameT))
                { currentTokenIndex = parseStart; return false; }

                if (!ExpectOperator("("))
                { currentTokenIndex = parseStart; return false; }

                FunctionDefinition function = new(CurrentNamespace, possibleNameT)
                {
                    Type = possibleType,
                    Attributes = attributes.ToArray(),
                    ExportKeyword = ExportKeyword,
                };

                possibleNameT.Analysis.Subtype = TokenSubtype.MethodName;

                var expectParameter = false;
                while (!ExpectOperator(")") || expectParameter)
                {
                    if (ExpectIdentifier("this", out Token thisKeywordT))
                    {
                        thisKeywordT.Analysis.Subtype = TokenSubtype.Keyword;
                        if (function.Parameters.Count > 0)
                        { Errors.Add(new Error("Keyword 'this' is only valid at the first parameter", thisKeywordT)); }
                    }

                    TypeToken possibleParameterType = ExceptTypeToken(false, true);
                    if (possibleParameterType == null)
                    { throw new SyntaxException("Expected parameter type", CurrentToken); }

                    if (!ExpectIdentifier(out Token possibleParameterNameT))
                    { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                    possibleParameterNameT.Analysis.Subtype = TokenSubtype.VariableName;

                    ParameterDefinition parameterDefinition = new()
                    {
                        type = possibleParameterType,
                        name = possibleParameterNameT,
                        withThisKeyword = thisKeywordT != null,
                    };
                    function.Parameters.Add(parameterDefinition);

                    if (ExpectOperator(")"))
                    { break; }

                    if (!ExpectOperator(","))
                    { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                    else
                    { expectParameter = true; }
                }

                List<Statement> statements = new();

                if (!ExpectOperator(";"))
                {
                    statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
                    function.BracketStart = braceletStart;
                    function.BracketEnd = braceletEnd;
                }

                function.Statements = statements;

                Functions.Add(function);

                return true;
            }

            bool ExpectNamespaceDefinition()
            {
                if (!ExpectIdentifier("namespace", out var possibleNamespaceIdentifier))
                { return false; }

                possibleNamespaceIdentifier.Analysis.Subtype = TokenSubtype.Keyword;

                if (ExpectIdentifier(out Token possibleName))
                {
                    if (ExpectOperator("{", out var braceletStart))
                    {
                        possibleName.Analysis.Subtype = TokenSubtype.Library;
                        CurrentNamespace.Add(possibleName.text);
                        int endlessSafe = 0;
                        Token braceletEnd;
                        while (!ExpectOperator("}", out braceletEnd))
                        {
                            ParseCodeBlock();
                            endlessSafe++;
                            if (endlessSafe >= 100)
                            {
                                throw new EndlessLoopException();
                            }
                        }
                        CurrentNamespace.RemoveAt(CurrentNamespace.Count - 1);
                        Namespaces.Add(new NamespaceDefinition()
                        {
                            Name = possibleName,
                            Keyword = possibleNamespaceIdentifier,
                            BracketStart = braceletStart,
                            BracketEnd = braceletEnd,
                        });
                        return true;
                    }
                    { throw new SyntaxException("Expected { after namespace name", possibleName); }
                }
                else
                { throw new SyntaxException("Expected namespace name", possibleNamespaceIdentifier); }
            }

            bool ExpectClassDefinition()
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
                    { Errors.Add(new Error("Attribute '" + attr + "' already applied to the class", attr.Name)); }
                }

                ExpectIdentifier("export", out Token ExportKeyword);

                if (!ExpectIdentifier("class", out Token keyword))
                { currentTokenIndex = startTokenIndex; return false; }

                if (!ExpectIdentifier(out Token possibleClassName))
                { throw new SyntaxException("Expected class identifier after keyword 'class'", keyword); }

                if (!ExpectOperator("{", out var braceletStart))
                { throw new SyntaxException("Expected '{' after class identifier", possibleClassName); }

                possibleClassName.Analysis.Subtype = TokenSubtype.Class;
                keyword.Analysis.Subtype = TokenSubtype.Keyword;

                List<ParameterDefinition> fields = new();
                Dictionary<string, FunctionDefinition> methods = new();

                int endlessSafe = 0;
                Token braceletEnd;
                while (!ExpectOperator("}", out braceletEnd))
                {
                    ParameterDefinition field = ExpectField();
                    if (field == null)
                    { throw new SyntaxException($"Expected field definition", CurrentToken); }

                    fields.Add(field);
                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", new Position(CurrentToken.Position.Start.Line))); }

                    endlessSafe++;
                    if (endlessSafe > 50)
                    {
                        throw new EndlessLoopException();
                    }
                }

                ClassDefinition classDefinition = new(CurrentNamespace, possibleClassName, attributes, fields, methods)
                {
                    BracketStart = braceletStart,
                    BracketEnd = braceletEnd,
                    ExportKeyword = ExportKeyword,
                };

                Classes.Add(classDefinition.FullName, classDefinition);

                Warnings.Add(new Warning($"Class is experimental feature!", keyword));

                return true;
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
                    { Errors.Add(new Error("Attribute '" + attr + "' already applied to the struct", attr.Name)); }
                }

                ExpectIdentifier("export", out Token ExportKeyword);

                if (!ExpectIdentifier("struct", out Token keyword))
                { currentTokenIndex = startTokenIndex; return false; }

                if (!ExpectIdentifier(out Token possibleStructName))
                { throw new SyntaxException("Expected struct identifier after keyword 'struct'", keyword); }

                if (!ExpectOperator("{", out var braceletStart))
                { throw new SyntaxException("Expected '{' after struct identifier", possibleStructName); }

                possibleStructName.Analysis.Subtype = TokenSubtype.Struct;
                keyword.Analysis.Subtype = TokenSubtype.Keyword;

                List<ParameterDefinition> fields = new();
                Dictionary<string, FunctionDefinition> methods = new();

                int endlessSafe = 0;
                Token braceletEnd;
                while (!ExpectOperator("}", out braceletEnd))
                {
                    ParameterDefinition field = ExpectField();
                    if (field == null)
                    { throw new SyntaxException($"Expected field definition", CurrentToken); }

                    fields.Add(field);
                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", new Position(CurrentToken.Position.Start.Line))); }

                    endlessSafe++;
                    if (endlessSafe > 50)
                    {
                        throw new EndlessLoopException();
                    }
                }

                StructDefinition structDefinition = new(CurrentNamespace, possibleStructName, attributes, fields, methods)
                {
                    BracketStart = braceletStart,
                    BracketEnd = braceletEnd,
                    ExportKeyword = ExportKeyword,
                };

                Structs.Add(structDefinition.FullName, structDefinition);

                return true;
            }

            #endregion

            #region Parse low level

            bool ExpectListValue(out Statement_ListValue listValue)
            {
                listValue = null;

                if (!ExpectOperator("[", out var o0))
                { return false; }

                listValue = new Statement_ListValue()
                {
                    Values = new List<Statement>(),
                    BracketLeft = o0,
                };

                int endlessSafe = 0;
                while (true)
                {
                    var v = ExpectExpression();
                    if (v != null)
                    {
                        listValue.Values.Add(v);

                        if (!ExpectOperator(","))
                        {
                            if (!ExpectOperator("]", out var o1))
                            { throw new SyntaxException("Unbalanced '['", o0); }
                            listValue.BracketRight = o1;
                            break;
                        }
                    }
                    else
                    {
                        if (!ExpectOperator("]", out var o1))
                        { throw new SyntaxException("Unbalanced '['", o0); }
                        listValue.BracketRight = o1;
                        break;
                    }

                    endlessSafe++;
                    if (endlessSafe >= 50) { throw new EndlessLoopException(); }
                }

                return true;
            }

            bool ExpectLiteral(out Statement_Literal statement)
            {
                int savedToken = currentTokenIndex;

                if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_FLOAT)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.text,
                        Type = TypeToken.CreateAnonymous("float", BuiltinType.FLOAT),
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_NUMBER)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.text,
                        Type = TypeToken.CreateAnonymous("int", BuiltinType.INT),
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.type == TokenType.LITERAL_STRING)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.text,
                        Type = TypeToken.CreateAnonymous("string", BuiltinType.STRING),
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (ExpectIdentifier("true", out var tTrue))
                {
                    Statement_Literal literal = new()
                    {
                        Value = "true",
                        Type = TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN),
                        ValueToken = CurrentToken,
                    };

                    tTrue.Analysis.Subtype = TokenSubtype.Keyword;

                    statement = literal;
                    return true;
                }
                else if (ExpectIdentifier("false", out var tFalse))
                {
                    Statement_Literal literal = new()
                    {
                        Value = "false",
                        Type = TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN),
                        ValueToken = CurrentToken,
                    };

                    tFalse.Analysis.Subtype = TokenSubtype.Keyword;

                    statement = literal;
                    return true;
                }

                currentTokenIndex = savedToken;

                statement = null;
                return false;
            }

            bool ExpectIndex(out Statement_Index statement)
            {
                if (ExpectOperator("[", out var token0))
                {
                    var st = ExpectOneValue();
                    if (ExpectOperator("]"))
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
            ///  <seealso cref="Statement_Field"></seealso>
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

                if (ExpectListValue(out var listValue))
                {
                    returnStatement = listValue;
                }
                else if (ExpectLiteral(out var literal))
                {
                    returnStatement = literal;
                }
                else if (ExpectOperator("(", out var braceletT))
                {
                    var expression = ExpectExpression();
                    if (expression == null)
                    { throw new SyntaxException("Expected expression after '('", braceletT); }

                    if (expression is Statement_Operator operation)
                    { operation.InsideBracelet = true; }

                    if (!ExpectOperator(")"))
                    { throw new SyntaxException("Unbalanced '('", braceletT); }

                    returnStatement = expression;
                }
                else if (ExpectIdentifier("new", out Token newIdentifier))
                {
                    newIdentifier.Analysis.Subtype = TokenSubtype.Keyword;

                    if (!ExpectIdentifier(out Token structName))
                    { throw new SyntaxException("Expected struct constructor after keyword 'new'", newIdentifier); }

                    structName.Analysis.Subtype = TokenSubtype.Struct;

                    List<string> targetLibraryPath = new();
                    List<Token> targetLibraryPathTokens = new();

                    while (ExpectOperator(".", out Token dotToken))
                    {
                        if (!ExpectIdentifier(out Token libraryToken))
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
                    { token.Analysis.Subtype = TokenSubtype.Library; }

                    if (structName == null)
                    { throw new SyntaxException("Expected struct constructor after keyword 'new'", newIdentifier); }

                    structName.Analysis.Subtype = TokenSubtype.Struct;

                    Statement_NewStruct newStructStatement = new(CurrentNamespace.ToArray(), targetLibraryPath.ToArray())
                    {
                        StructName = structName,
                    };

                    returnStatement = newStructStatement;
                }
                else if (ExpectIdentifier(out Token variableName))
                {
                    if (variableName.text == "this")
                    { Errors.Add(new Error("The keyword 'this' does not avaiable in the current context", variableName)); }

                    if (ExpectOperator("("))
                    {
                        currentTokenIndex = savedToken;
                        returnStatement = ExpectFunctionCall();
                    }
                    else
                    {
                        Statement_Variable variableNameStatement = new()
                        {
                            VariableName = variableName,
                        };

                        if (variableName.text == "this")
                        { variableName.Analysis.Subtype = TokenSubtype.Keyword; }
                        else
                        { variableName.Analysis.Subtype = TokenSubtype.VariableName; }

                        returnStatement = variableNameStatement;
                    }
                }

                while (true)
                {
                    if (ExpectOperator(".", out var tokenDot))
                    {
                        if (ExpectMethodCall(false, out var methodCall))
                        {
                            methodCall.PrevStatement = returnStatement;
                            returnStatement = methodCall;
                        }
                        else
                        {
                            if (!ExpectIdentifier(out Token fieldName))
                            { throw new SyntaxException("Expected field or method", tokenDot); }

                            var fieldStatement = new Statement_Field()
                            {
                                FieldName = fieldName,
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

            List<Statement> ParseFunctionBody(out Token braceletStart, out Token braceletEnd)
            {
                braceletEnd = null;

                if (!ExpectOperator("{", out braceletStart))
                { return null; }

                List<Statement> statements = new();

                int endlessSafe = 0;
                while (!ExpectOperator("}", out braceletEnd))
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
                            throw new SyntaxException("Unknown statement", Position.UnknownPosition);
                        }
                    }
                    else if (statement is Statement_Literal)
                    {
                        throw new SyntaxException("Unexpected kind of statement", statement.TotalPosition());
                    }
                    else if (statement is Statement_Variable)
                    {
                        throw new SyntaxException("Unexpected kind of statement", statement.TotalPosition());
                    }
                    else if (statement is Statement_NewStruct)
                    {
                        throw new SyntaxException("Unexpected kind of statement", statement.TotalPosition());
                    }
                    else
                    {
                        if (statement is StatementWithReturnValue statementWithReturnValue)
                        {
                            statementWithReturnValue.SaveValue = false;
                            statements.Add(statementWithReturnValue);
                        }
                        else
                        {
                            statements.Add(statement);
                        }

                        if (!ExpectOperator(";"))
                        { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition())); }
                    }

                    endlessSafe++;
                    if (endlessSafe > 500) throw new EndlessLoopException();
                }

                return statements;
            }

            Statement_NewVariable ExpectVariableDeclaration()
            {
                int startTokenIndex = currentTokenIndex;
                TypeToken possibleType = ExceptTypeToken(out var structNotFoundWarning);
                if (possibleType == null)
                { currentTokenIndex = startTokenIndex; return null; }

                if (!ExpectIdentifier(out Token possibleVariableName))
                { currentTokenIndex = startTokenIndex; return null; }

                possibleVariableName.Analysis.Subtype = TokenSubtype.VariableName;

                Statement_NewVariable statement = new()
                {
                    VariableName = possibleVariableName,
                    Type = possibleType,
                };

                if (structNotFoundWarning != null)
                { Warnings.Add(structNotFoundWarning); }

                if (ExpectOperator("=", out var eqT))
                {
                    statement.InitialValue = ExpectExpression() ?? throw new SyntaxException("Expected initial value after '=' in variable declaration", eqT);
                }
                else
                {
                    if (possibleType.typeName == BuiltinType.AUTO)
                    { throw new SyntaxException("Initial value for 'var' variable declaration is requied", possibleType); }
                }

                return statement;
            }

            Statement_ForLoop ExpectForStatement()
            {
                if (!ExpectIdentifier("for", out Token tokenFor))
                { return null; }

                tokenFor.Analysis.Subtype = TokenSubtype.Statement;

                if (!ExpectOperator("(", out Token tokenZarojel))
                { throw new SyntaxException("Expected '(' after \"for\" statement", tokenFor); }

                var variableDeclaration = ExpectVariableDeclaration();
                if (variableDeclaration == null)
                { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenZarojel); }

                if (!ExpectOperator(";"))
                { throw new SyntaxException("Expected ';' after \"for\" variable declaration", variableDeclaration.TotalPosition()); }

                Statement condition = ExpectExpression();
                if (condition == null)
                { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenZarojel); }

                if (!ExpectOperator(";"))
                { throw new SyntaxException($"Expected ';' after \"for\" condition, got {CurrentToken}", variableDeclaration.TotalPosition()); }

                Statement expression = ExpectExpression();
                if (expression == null)
                { throw new SyntaxException($"Expected expression after \"for\" condition, got {CurrentToken}", tokenZarojel); }

                if (!ExpectOperator(")", out Token tokenZarojel2))
                { throw new SyntaxException($"Expected ')' after \"for\" condition, got {CurrentToken}", condition.TotalPosition()); }

                if (!ExpectOperator("{", out var braceletStart))
                { throw new SyntaxException($"Expected '{{' after \"for\" condition, got {CurrentToken}", tokenZarojel2); }

                Statement_ForLoop forStatement = new()
                {
                    Keyword = tokenFor,
                    VariableDeclaration = variableDeclaration,
                    Condition = condition,
                    Expression = expression,
                    BracketStart = braceletStart,
                };

                int endlessSafe = 0;
                while (CurrentToken != null && CurrentToken != null && !ExpectOperator("}", out forStatement.BracketEnd))
                {
                    var statement = ExpectStatement();
                    if (statement == null) break;
                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition())); }

                    forStatement.Statements.Add(statement);

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
                if (!ExpectIdentifier("while", out Token tokenWhile))
                { return null; }

                tokenWhile.Analysis.Subtype = TokenSubtype.Statement;

                if (!ExpectOperator("(", out Token tokenZarojel))
                { throw new SyntaxException("Expected '(' after \"while\" statement", tokenWhile); }

                Statement condition = ExpectExpression();
                if (condition == null)
                { throw new SyntaxException("Expected condition after \"while\" statement", tokenZarojel); }

                if (!ExpectOperator(")", out Token tokenZarojel2))
                { throw new SyntaxException("Expected ')' after \"while\" condition", condition.TotalPosition()); }

                if (!ExpectOperator("{", out Token braceletStart))
                { throw new SyntaxException("Expected '{' after \"while\" condition", tokenZarojel2); }

                Statement_WhileLoop whileStatement = new()
                {
                    Keyword = tokenWhile,
                    Condition = condition,
                    BracketStart = braceletStart,
                };

                int endlessSafe = 0;
                while (CurrentToken != null && CurrentToken != null && !ExpectOperator("}", out whileStatement.BracketEnd))
                {
                    var statement = ExpectStatement();
                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error($"Expected ';' at end of statement  (after {statement.GetType().Name})", statement.TotalPosition())); }
                    if (statement == null) break;

                    whileStatement.Statements.Add(statement);

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
                statement.Parts.Add(ifStatement);

                int endlessSafe = 0;
                while (true)
                {
                    Statement_If_Part elseifStatement = ExpectIfSegmentStatement("elseif", Statement_If_Part.IfPart.ElseIf);
                    if (elseifStatement == null) break;
                    statement.Parts.Add(elseifStatement);

                    endlessSafe++;
                    if (endlessSafe > 100)
                    { throw new EndlessLoopException(); }
                }

                Statement_If_Part elseStatement = ExpectIfSegmentStatement("else", Statement_If_Part.IfPart.Else, false);
                if (elseStatement != null)
                {
                    statement.Parts.Add(elseStatement);
                }

                return statement;
            }

            Statement_If_Part ExpectIfSegmentStatement(string ifSegmentName = "if", Statement_If_Part.IfPart ifSegmentType = Statement_If_Part.IfPart.If, bool needParameters = true)
            {
                if (!ExpectIdentifier(ifSegmentName, out Token tokenIf))
                { return null; }

                tokenIf.Analysis.Subtype = TokenSubtype.Statement;

                Statement condition = null;
                if (needParameters)
                {
                    if (!ExpectOperator("(", out Token tokenZarojel))
                    { throw new SyntaxException("Expected '(' after \"" + ifSegmentName + "\" statement", tokenIf); }
                    condition = ExpectExpression();
                    if (condition == null)
                    { throw new SyntaxException("Expected condition after \"" + ifSegmentName + "\" statement", tokenZarojel); }

                    if (!ExpectOperator(")"))
                    { throw new SyntaxException("Expected ')' after \"" + ifSegmentName + "\" condition", condition.TotalPosition()); }
                }
                if (!ExpectOperator("{", out Token braceletStart))
                { throw new SyntaxException("Expected '{' after \"" + ifSegmentName + "\" condition", tokenIf); }

                Statement_If_Part ifStatement = null;

                switch (ifSegmentType)
                {
                    case Statement_If_Part.IfPart.If:
                        ifStatement = new Statement_If_If()
                        {
                            Keyword = tokenIf,
                            Condition = condition,
                            BracketStart = braceletStart,
                        };
                        break;
                    case Statement_If_Part.IfPart.ElseIf:
                        ifStatement = new Statement_If_ElseIf()
                        {
                            Keyword = tokenIf,
                            Condition = condition,
                            BracketStart = braceletStart,
                        };
                        break;
                    case Statement_If_Part.IfPart.Else:
                        ifStatement = new Statement_If_Else()
                        {
                            Keyword = tokenIf,
                            BracketStart = braceletStart,
                        };
                        break;
                }

                if (ifStatement == null)
                { throw new InternalException(); }

                int endlessSafe = 0;
                while (CurrentToken != null && !ExpectOperator("}", out ifStatement.BracketEnd))
                {
                    var statement = ExpectStatement();
                    if (!ExpectOperator(";"))
                    {
                        if (statement == null)
                        { throw new SyntaxException("Expected a statement", CurrentToken); }
                        else
                        { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition())); }
                    }

                    ifStatement.Statements.Add(statement);

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
                statement ??= ExpectVariableDeclaration();
                statement ??= ExpectExpression();
                return statement;
            }

            bool ExpectMethodCall(bool expectDot, out Statement_FunctionCall methodCall)
            {
                int startTokenIndex = currentTokenIndex;

                methodCall = null;

                if (expectDot)
                {
                    if (!ExpectOperator("."))
                    { currentTokenIndex = startTokenIndex; return false; }
                }

                if (!ExpectIdentifier(out var possibleFunctionName))
                { currentTokenIndex = startTokenIndex; return false; }

                if (!ExpectOperator("("))
                { currentTokenIndex = startTokenIndex; return false; }

                possibleFunctionName.Analysis.Subtype = TokenSubtype.MethodName;

                methodCall = new(CurrentNamespace.ToArray(), Array.Empty<string>())
                {
                    Identifier = possibleFunctionName,
                };

                bool expectParameter = false;

                int endlessSafe = 0;
                while (!ExpectOperator(")") || expectParameter)
                {
                    Statement parameter = ExpectExpression();
                    if (parameter == null)
                    { throw new SyntaxException("Expected expression as parameter", methodCall.TotalPosition()); }

                    methodCall.Parameters.Add(parameter);

                    if (ExpectOperator(")"))
                    { break; }

                    if (!ExpectOperator(","))
                    { throw new SyntaxException($"Expected ',' to separate parameters, got {CurrentToken}", parameter.TotalPosition()); }
                    else
                    { expectParameter = true; }

                    endlessSafe++;
                    if (endlessSafe > 100)
                    { throw new EndlessLoopException(); }
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
            ///  <seealso cref="Statement_Field"></seealso>
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
                if (ExpectOperator("!", out var tNotOperator))
                {
                    Statement statement = ExpectOneValue();
                    if (statement == null)
                    { throw new SyntaxException("Expected value or expression after '!' operator", tNotOperator); }

                    return new Statement_Operator(tNotOperator, statement);
                }

                /*
                if (ExpectOperator("-", out var tMinusOperator))
                {
                    Statement statement = ExpectOneValue();
                    if (statement == null)
                    { throw new SyntaxException("Expected value or expression after '-' operator", tMinusOperator); }

                    return new Statement_Operator(tMinusOperator, statement);
                }
                */

                // Possible a variable
                Statement leftStatement = ExpectOneValue();
                if (leftStatement == null) return null;

                if (ExpectOperator(new string[] {
                    "+=", "-=", "*=", "/=", "%=",
                    "&=", "|=", "^=",
                }, out var o0))
                {
                    var valueToAssign = ExpectOneValue();
                    if (valueToAssign == null)
                    { throw new SyntaxException("Expected expression", o0); }

                    Statement_Operator statementToAssign = new(new Token()
                    {
                        AbsolutePosition = o0.AbsolutePosition,
                        Analysis = new TokenAnalysis(),
                        text = o0.text.Replace("=", ""),
                        Position = o0.Position,
                        type = o0.type,
                    }, leftStatement, valueToAssign);

                    Statement_Operator operatorCall = new(new Token()
                    {
                        AbsolutePosition = o0.AbsolutePosition,
                        Analysis = new TokenAnalysis(),
                        text = "=",
                        Position = o0.Position,
                        type = o0.type,
                    }, leftStatement, statementToAssign);

                    return operatorCall;
                }
                else if (ExpectOperator("++", out var t0))
                {
                    Statement_Literal literalOne = new()
                    {
                        Value = "1",
                        Type = types["int"],
                        ImagineryPosition = t0.GetPosition(),
                    };

                    Statement_Operator statementToAssign = new(new Token()
                    {
                        AbsolutePosition = t0.AbsolutePosition,
                        Analysis = new TokenAnalysis(),
                        text = "+",
                        Position = t0.Position,
                        type = t0.type,
                    }, leftStatement, literalOne);

                    Statement_Operator operatorCall = new(new Token()
                    {
                        AbsolutePosition = t0.AbsolutePosition,
                        Analysis = new TokenAnalysis(),
                        text = "=",
                        Position = t0.Position,
                        type = t0.type,
                    }, leftStatement, statementToAssign);

                    return operatorCall;
                }
                else if (ExpectOperator("--", out var t1))
                {
                    Statement_Literal literalOne = new()
                    {
                        Value = "1",
                        Type = types["int"],
                        ImagineryPosition = t0.GetPosition(),
                    };

                    Statement_Operator statementToAssign = new(new Token()
                    {
                        AbsolutePosition = t1.AbsolutePosition,
                        Analysis = new TokenAnalysis(),
                        text = "-",
                        Position = t1.Position,
                        type = t1.type,
                    }, leftStatement, literalOne);

                    Statement_Operator operatorCall = new(new Token()
                    {
                        AbsolutePosition = t1.AbsolutePosition,
                        Analysis = new TokenAnalysis(),
                        text = "=",
                        Position = t1.Position,
                        type = t1.type,
                    }, leftStatement, statementToAssign);

                    return operatorCall;
                }

                while (true)
                {
                    if (!ExpectOperator(new string[]
                        {
                        "<<", ">>",
                        "+", "-", "*", "/", "%",
                        "=",
                        "<", ">", ">=", "<=", "!=", "==", "&", "|", "^"
                        }, out Token op)) break;

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
                            Statement_Operator operatorCall = new(op, rightmostOperator.Right, rightStatement);


                            rightmostOperator.Right = operatorCall;
                        }
                        else
                        {
                            Statement_Operator operatorCall = new(op, leftStatement, rightStatement);

                            leftStatement = operatorCall;
                        }
                    }
                    else
                    {
                        Statement_Operator operatorCall = new(op, leftStatement, rightStatement);

                        leftStatement = operatorCall;
                    }
                }

                return leftStatement;
            }

            Statement_Operator FindRightmostStatement(Statement statement, int rhsPrecedence)
            {
                if (statement is not Statement_Operator lhs) return null;
                if (OperatorPrecedence(lhs.Operator.text) >= rhsPrecedence) return null;
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

                if (!ExpectIdentifier(out Token possibleFunctionName))
                { currentTokenIndex = startTokenIndex; return null; }

                List<string> targetLibraryPath = new();
                List<Token> targetLibraryPathTokens = new();

                while (ExpectOperator(".", out Token dotToken))
                {
                    if (!ExpectIdentifier(out Token libraryToken))
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

                if (!ExpectOperator("("))
                { currentTokenIndex = startTokenIndex; return null; }

                foreach (var token in targetLibraryPathTokens)
                { token.Analysis.Subtype = TokenSubtype.Library; }

                possibleFunctionName.Analysis.Subtype = TokenSubtype.MethodName;

                Statement_FunctionCall functionCall = new(CurrentNamespace.ToArray(), targetLibraryPath.ToArray())
                {
                    Identifier = possibleFunctionName,
                };

                bool expectParameter = false;

                int endlessSafe = 0;
                while (!ExpectOperator(")") || expectParameter)
                {
                    Statement parameter = ExpectExpression();
                    if (parameter == null)
                    { throw new SyntaxException("Expected expression as parameter", functionCall.TotalPosition()); }

                    functionCall.Parameters.Add(parameter);

                    if (ExpectOperator(")"))
                    { break; }

                    if (!ExpectOperator(","))
                    { throw new SyntaxException("Expected ',' to separate parameters", parameter.TotalPosition()); }
                    else
                    { expectParameter = true; }

                    endlessSafe++;
                    if (endlessSafe > 100)
                    { throw new EndlessLoopException(); }
                }

                return functionCall;
            }

            /// <summary> return, break, continue, etc. </summary>
            Statement_FunctionCall ExpectKeywordCall(string name, bool canHaveParameters = false, bool needParameters = false)
            {
                int startTokenIndex = currentTokenIndex;

                if (!ExpectIdentifier(out Token possibleFunctionName))
                { currentTokenIndex = startTokenIndex; return null; }

                if (possibleFunctionName.text != name)
                { currentTokenIndex = startTokenIndex; return null; }

                possibleFunctionName.Analysis.Subtype = TokenSubtype.Statement;

                Statement_FunctionCall functionCall = new(Array.Empty<string>(), Array.Empty<string>())
                {
                    Identifier = possibleFunctionName,
                };

                if (canHaveParameters)
                {
                    Statement parameter = ExpectExpression();
                    if (parameter == null && needParameters)
                    { throw new SyntaxException("Expected expression as parameter", functionCall.TotalPosition()); }

                    if (parameter != null)
                    {
                        functionCall.Parameters.Add(parameter);
                    }
                }
                return functionCall;
            }

            #endregion

            bool ExpectAttribute(out FunctionDefinition.Attribute attribute)
            {
                int parseStart = currentTokenIndex;
                attribute = new();

                if (!ExpectOperator("[", out var t0))
                { currentTokenIndex = parseStart; return false; }

                if (!ExpectIdentifier(out Token attributeT))
                { currentTokenIndex = parseStart; return false; }

                attributeT.Analysis.Subtype = TokenSubtype.Type;

                if (ExpectOperator("(", out var t3))
                {
                    List<object> parameters = new();
                    int endlessSafe = 50;
                    while (!ExpectOperator(")"))
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

                if (!ExpectOperator("]"))
                { throw new SyntaxException("Unbalanced ]", t0); }

                attribute.Name = attributeT;
                return true;
            }

            ParameterDefinition ExpectField()
            {
                int startTokenIndex = currentTokenIndex;
                TypeToken possibleType = ExceptTypeToken();
                if (possibleType == null)
                { currentTokenIndex = startTokenIndex; return null; }

                if (!ExpectIdentifier(out Token possibleVariableName))
                { currentTokenIndex = startTokenIndex; return null; }

                if (ExpectOperator("("))
                { currentTokenIndex = startTokenIndex; return null; }

                possibleVariableName.Analysis.Subtype = TokenSubtype.None;

                ParameterDefinition field = new()
                {
                    name = possibleVariableName,
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

            bool ExpectIdentifier(out Token result) => ExpectIdentifier("", out result);
            bool ExpectIdentifier(string name, out Token result)
            {
                result = null;
                if (CurrentToken == null) return false;
                if (CurrentToken.type != TokenType.IDENTIFIER) return false;
                if (name.Length > 0 && CurrentToken.text != name) return false;

                result = CurrentToken;
                currentTokenIndex++;

                return true;
            }

            bool ExpectOperator(string name) => ExpectOperator(name, out _);
            bool ExpectOperator(string[] name, out Token result)
            {
                result = null;
                if (CurrentToken == null) return false;
                if (CurrentToken.type != TokenType.OPERATOR) return false;
                if (name.Contains(CurrentToken.text) == false) return false;

                result = CurrentToken;
                currentTokenIndex++;

                return true;
            }
            bool ExpectOperator(string name, out Token result)
            {
                result = null;
                if (CurrentToken == null) return false;
                if (CurrentToken.type != TokenType.OPERATOR) return false;
                if (name.Length > 0 && CurrentToken.text != name) return false;

                result = CurrentToken;
                currentTokenIndex++;

                return true;
            }

            TypeToken ExceptTypeToken(out Warning warning, bool allowVarKeyword = true, bool allowAnyKeyword = false)
            {
                int parseStart = currentTokenIndex;

                warning = null;
                if (!ExpectIdentifier(out Token possibleType)) return null;

                possibleType.Analysis.Subtype = TokenSubtype.Keyword;

                TypeToken newType = null;

                if (!types.TryGetValue(possibleType.text, out TypeToken builtinType))
                {
                    if (newType == null && possibleType.text == "any")
                    {
                        if (allowAnyKeyword)
                        {
                            newType = new TypeToken("any", BuiltinType.ANY, possibleType);
                        }
                        else
                        {
                            Errors.Add(new Error($"Type '{possibleType.text}' is not valid in the current context", possibleType));
                        }
                    }

                    if (newType == null && possibleType.text == "var")
                    {
                        if (allowVarKeyword)
                        {
                            newType = new TypeToken("var", BuiltinType.AUTO, possibleType);
                        }
                        else
                        {
                            Errors.Add(new Error($"Type '{possibleType.text}' is not valid in the current context", possibleType));
                        }
                    }

                    if (newType == null)
                    {
                        if (TryGetStruct(possibleType.text, out var s))
                        {
                            newType = new TypeToken(s.FullName, BuiltinType.STRUCT, possibleType);
                            newType.Analysis.Subtype = TokenSubtype.Struct;
                        }
                        else if (TryGetClass(possibleType.text, out var c))
                        {
                            newType = new TypeToken(c.FullName, BuiltinType.STRUCT, possibleType);
                            newType.Analysis.Subtype = TokenSubtype.Class;
                        }
                        else
                        {
                            newType = new TypeToken(possibleType.text, BuiltinType.STRUCT, possibleType);
                            warning = new Warning($"Type '{possibleType.text}' not found", possibleType);
                        }
                    }

                    if (newType == null)
                    { return null; }
                }
                else
                {
                    newType = new TypeToken(builtinType.text, builtinType.typeName, possibleType)
                    { ListOf = builtinType.ListOf };
                    newType.Analysis.Subtype = TokenSubtype.BuiltinType;
                }

                while (ExpectOperator("["))
                {
                    if (ExpectOperator("]"))
                    {
                        newType = new TypeToken(newType.text, newType, newType);
                    }
                    else
                    { currentTokenIndex = parseStart; return null; }
                }

                tokens[currentTokenIndex - 1] = newType;

                return newType;
            }
            TypeToken ExceptTypeToken(bool allowVarKeyword = true, bool allowAnyKeyword = false) => ExceptTypeToken(out Warning _, allowVarKeyword, allowAnyKeyword);

            bool TryGetStruct(string name, out StructDefinition @struct)
            {
                if (Structs.TryGetValue(name, out @struct))
                {
                    return true;
                }
                return false;
            }
            bool TryGetClass(string name, out ClassDefinition @class)
            {
                if (Classes.TryGetValue(name, out @class))
                {
                    return true;
                }
                return false;
            }

            #endregion

            public static TypeToken ParseType(string type)
            {
                if (string.IsNullOrEmpty(type)) throw new ArgumentException($"'{nameof(type)}' cannot be null or empty.", nameof(type));
                if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));

                Tokenizer tokenizer = new(TokenizerSettings.Default);
                Token[] tokens = tokenizer.Parse(type).Item1;

                TypeToken result = null;

                int i = -1;
                while (i < tokens.Length)
                {
                    i++;
                    if (tokens.Length <= i) break;

                    Token token = tokens[i];
                    if (i == 0)
                    {
                        if (token.type != TokenType.IDENTIFIER) throw new FormatException();
                        if (types.TryGetValue(token.text, out result)) continue;
                        result = TypeToken.CreateAnonymous(token.text, BuiltinType.STRUCT);
                        continue;
                    }
                    if (token.type != TokenType.OPERATOR) throw new FormatException();
                    if (token.text != "[") throw new FormatException();
                    i++;
                    if (tokens.Length <= i) throw new FormatException();
                    token = tokens[i];
                    if (token.type != TokenType.OPERATOR) throw new FormatException();
                    if (token.text != "]") throw new FormatException();
                    result = TypeToken.CreateAnonymous(result.text, result);
                }

                return result;
            }
        }
    }
}