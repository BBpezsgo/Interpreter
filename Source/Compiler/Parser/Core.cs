using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Parser
{
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Core;

    public enum LiteralType
    {
        INT,
        FLOAT,
        BOOLEAN,
        STRING,
        CHAR,
    }

    public static class LiteralTypeExtensions
    {
        public static string ToStringRepresentation(this LiteralType literalType) => literalType switch
        {
            LiteralType.INT => "int",
            LiteralType.FLOAT => "float",
            LiteralType.BOOLEAN => "bool",
            LiteralType.STRING => "string",
            LiteralType.CHAR => "char",
            _ => throw new NotImplementedException(),
        };
    }

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

    public class ParameterDefinition : Compiler.IElementWithKey<string>
    {
        public Token Identifier;
        public TypeInstance Type;

        public bool withThisKeyword;

        public string Key => Identifier.Content;

        public override string ToString() => $"{(withThisKeyword ? "this " : "")}{Type} {Identifier}";
        internal string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}{Type} {Identifier}";
    }

    public class FieldDefinition
    {
        public Token Identifier;
        public TypeInstance Type;
        public Token ProtectionToken;

        public override string ToString() => $"{(ProtectionToken != null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
        internal string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}{(ProtectionToken != null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
    }

    public class Exportable
    {
        public Token ExportKeyword;
        public bool IsExport => ExportKeyword != null;
    }

    public class EnumMemberDefinition : Compiler.IElementWithKey<string>
    {
        public Token Identifier;
        public Statement_Literal Value;

        public string Key => Identifier.Content;
    }

    public class EnumDefinition : IDefinition, Compiler.IElementWithKey<string>
    {
        public string FilePath { get; set; }

        public string Key => Identifier.Content;

        public Token Identifier;
        public EnumMemberDefinition[] Members;
        public FunctionDefinition.Attribute[] Attributes;
    }

    public class FunctionDefinition : Exportable, IDefinition, Compiler.IDefinitionComparer<FunctionDefinition>
    {
        public class Attribute : Compiler.IElementWithKey<string>
        {
            public Token Identifier;
            public object[] Parameters;

            public string Key => Identifier.Content;
        }

        public Token BracketStart;
        public Token BracketEnd;

        public Attribute[] Attributes;
        public readonly Token Identifier;
        public ParameterDefinition[] Parameters;
        public Statement[] Statements;
        public TypeInstance Type;

        public string FilePath { get; set; }

        /// <summary>
        /// The first parameter is labeled as 'this'
        /// </summary>
        public bool IsMethod => ((Parameters.Length > 0) && (Parameters[0].withThisKeyword));

        public FunctionDefinition(Token name)
        {
            Parameters = Array.Empty<ParameterDefinition>();
            Statements = Array.Empty<Statement>();
            Attributes = Array.Empty<Attribute>();
            this.Identifier = name;
        }

        public override string ToString()
        {
            return $"{(IsExport ? "export " : "")}{this.Type.Identifier.Content} {this.Identifier}" + (this.Parameters.Length > 0 ? "(...)" : "()") + " " + (this.Statements.Length > 0 ? "{...}" : "{}");
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

            return $"{" ".Repeat(ident)}{this.Type.Identifier.Content} {this.Identifier}" + ($"({string.Join(", ", parameters)})") + " " + (this.Statements.Length > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
        }

        public string ReadableID()
        {
            string result = this.Identifier.ToString();
            result += "(";
            for (int j = 0; j < this.Parameters.Length; j++)
            {
                if (j > 0) { result += ", "; }
                result += this.Parameters[j].Type.ToString();
            }
            result += ")";
            return result;
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;

        public bool IsSame(FunctionDefinition other)
        {
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (this.Parameters[i].Type.Identifier.Content != other.Parameters[i].Type.Identifier.Content) return false;
            }
            return true;
        }
    }

    public class GeneralFunctionDefinition : Exportable, IDefinition
    {
        public Token BracketStart;
        public Token BracketEnd;

        public readonly Token Identifier;
        public ParameterDefinition[] Parameters;
        public Statement[] Statements;

        public string FilePath { get; set; }

        /// <summary>
        /// The first parameter is labeled as 'this'
        /// </summary>
        public bool IsMethod => ((Parameters.Length > 0) && (Parameters[0].withThisKeyword));

        public GeneralFunctionDefinition(Token name)
        {
            Parameters = Array.Empty<ParameterDefinition>();
            Statements = Array.Empty<Statement>();
            this.Identifier = name;
        }

        public override string ToString()
        {
            return $"{(IsExport ? "export " : "")}{this.Identifier.Content}" + (this.Parameters.Length > 0 ? "(...)" : "()") + " " + (this.Statements.Length > 0 ? "{...}" : "{}");
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

            return $"{" ".Repeat(ident)}{this.Identifier.Content}" + ($"({string.Join(", ", parameters)})") + " " + (this.Statements.Length > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
        }

        public string ReadableID()
        {
            string result = this.Identifier.Content;
            result += "(";
            for (int j = 0; j < this.Parameters.Length; j++)
            {
                if (j > 0) { result += ", "; }
                result += this.Parameters[j].Type.ToString();
            }
            result += ")";
            return result;
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
    }

    public class ClassDefinition : Exportable, IDefinition, Compiler.IElementWithKey<string>
    {
        public readonly FunctionDefinition.Attribute[] Attributes;
        public readonly Token Name;
        public Token BracketStart;
        public Token BracketEnd;
        public List<Statement> Statements;
        public string FilePath { get; set; }
        public readonly FieldDefinition[] Fields;

        public string Key => Name.Content;

        public IReadOnlyCollection<FunctionDefinition> Methods => methods;
        public IReadOnlyCollection<GeneralFunctionDefinition> GeneralMethods => generalMethods;

        readonly FunctionDefinition[] methods;
        readonly GeneralFunctionDefinition[] generalMethods;

        public ClassDefinition(Token name, IEnumerable<FunctionDefinition.Attribute> attributes, IEnumerable<FieldDefinition> fields, IEnumerable<FunctionDefinition> methods, IEnumerable<GeneralFunctionDefinition> generalMethods)
        {
            this.Name = name;
            this.Fields = fields.ToArray();
            this.methods = methods.ToArray();
            this.generalMethods = generalMethods.ToArray();
            this.Attributes = attributes.ToArray();
            this.Statements = new List<Statement>();
        }

        public override string ToString()
        {
            return $"class {this.Name.Content} " + "{...}";
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> fields = new();
            foreach (var field in this.Fields)
            {
                fields.Add($"{" ".Repeat(ident)}" + field.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            List<string> methods = new();

            foreach (var generalMethod in this.generalMethods)
            {
                methods.Add($"{" ".Repeat(ident)}" + generalMethod.PrettyPrint((ident == 0) ? 2 : ident));
            }

            foreach (var method in this.methods)
            {
                methods.Add($"{" ".Repeat(ident)}" + method.PrettyPrint((ident == 0) ? 2 : ident));
            }

            return $"{" ".Repeat(ident)}class {this.Name.Content} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
    }

    public class StructDefinition : Exportable, IDefinition, Compiler.IElementWithKey<string>
    {
        public readonly FunctionDefinition.Attribute[] Attributes;
        public readonly Token Name;
        public Token BracketStart;
        public Token BracketEnd;
        public List<Statement> Statements;

        public string FilePath { get; set; }
        public readonly FieldDefinition[] Fields;

        public string Key => Name.Content;

        public IReadOnlyDictionary<string, FunctionDefinition> Methods => methods;
        readonly Dictionary<string, FunctionDefinition> methods;

        public StructDefinition(Token name, IEnumerable<FunctionDefinition.Attribute> attributes, IEnumerable<FieldDefinition> fields, IEnumerable<KeyValuePair<string, FunctionDefinition>> methods)
        {
            this.Name = name;
            this.Fields = fields.ToArray();
            this.methods = new Dictionary<string, FunctionDefinition>(methods);
            this.Attributes = attributes.ToArray();
            this.Statements = new List<Statement>();
        }

        public override string ToString()
        {
            return $"struct {this.Name.Content} " + "{...}";
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

            return $"{" ".Repeat(ident)}struct {this.Name.Content} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
    }

    public struct UsingAnalysis
    {
        public string Path;
        public bool Found;
        public double ParseTime;
    }

    public readonly struct ParserResult
    {
        public readonly FunctionDefinition[] Functions;
        public readonly StructDefinition[] Structs;
        public readonly ClassDefinition[] Classes;
        public readonly UsingDefinition[] Usings;
        public readonly Statement_HashInfo[] Hashes;
        public readonly List<UsingAnalysis> UsingsAnalytics;
        public readonly Statement[] TopLevelStatements;
        public readonly EnumDefinition[] Enums;

        public ParserResult(IEnumerable<FunctionDefinition> functions, IEnumerable<StructDefinition> structs, IEnumerable<UsingDefinition> usings, IEnumerable<Statement_HashInfo> hashes, IEnumerable<ClassDefinition> classes, IEnumerable<Statement> topLevelStatements, IEnumerable<EnumDefinition> enums)
        {
            Functions = functions.ToArray();
            Structs = structs.ToArray();
            Usings = usings.ToArray();
            UsingsAnalytics = new();
            Hashes = hashes.ToArray();
            Classes = classes.ToArray();
            TopLevelStatements = topLevelStatements.ToArray();
            Enums = enums.ToArray();
        }

        /// <summary>Converts the parsed AST into text</summary>
        public string PrettyPrint()
        {
            var x = "";

            foreach (var @using in Usings)
            {
                x += "using " + @using.PathString + ";\n";
            }

            foreach (var @struct in Structs)
            {
                x += @struct.PrettyPrint() + "\n";
            }

            foreach (var @class in Classes)
            {
                x += @class.PrettyPrint() + "\n";
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
                Console.Write(attribute.Identifier);
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

            foreach (var item in this.Structs)
            {
                foreach (var attr in item.Attributes)
                { Attribute(attr); }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("struct ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{item.Name} ");
                Console.Write("\n\r");

                foreach (var field in item.Fields)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write($"  {field.Type} ");

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{field.Identifier}");

                    Console.Write("\n\r");
                }

                Console.Write("\n\r");
                Console.ResetColor();
            }

            foreach (var item in this.Classes)
            {
                foreach (var attr in item.Attributes)
                { Attribute(attr); }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("class ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{item.Name} ");
                Console.Write("\n\r");

                foreach (var field in item.Fields)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write($"  {field.Type} ");

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{field.Identifier}");

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
                if (!Compiler.CodeGeneratorBase.BuiltinTypes.Contains(item.Type.Identifier.Content))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                Console.Write($"{item.Type} ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{item.Identifier.Content}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("(");
                for (int i = 0; i < item.Parameters.Length; i++)
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
                    if (!Compiler.CodeGeneratorBase.BuiltinTypes.Contains(param.Type.Identifier.Content))
                    { Console.ForegroundColor = ConsoleColor.Green; }
                    Console.Write($"{param.Type} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{param.Identifier}");
                }
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(")");

                if (item.Statements.Length > 0)
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
                            if (st2.PrevStatement != null) AddStatement(st2.PrevStatement);
                            foreach (var st3 in st2.Parameters)
                            { AddStatement(st3); }
                        }
                        else if (st is Statement_KeywordCall keywordCall)
                        {
                            foreach (var st_ in keywordCall.Parameters)
                            { AddStatement(st_); }
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

            for (int i = 0; i < this.Functions.Length; i++)
            { this.Functions[i].FilePath = path; }
            for (int i = 0; i < this.Enums.Length; i++)
            { this.Enums[i].FilePath = path; }
            for (int i = 0; i < this.Structs.Length; i++)
            {
                this.Structs[i].FilePath = path;
                for (int j = 0; j < this.Structs[i].Methods.Count; j++)
                {
                    this.Structs[i].Methods.ElementAt(j).Value.FilePath = path;
                }
            }
            for (int i = 0; i < this.Classes.Length; i++)
            {
                this.Classes[i].FilePath = path;
                for (int j = 0; j < this.Classes[i].Methods.Count; j++)
                {
                    this.Classes[i].Methods.ElementAt(j).FilePath = path;
                }
                for (int j = 0; j < this.Classes[i].GeneralMethods.Count; j++)
                {
                    this.Classes[i].GeneralMethods.ElementAt(j).FilePath = path;
                }
            }
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
            for (int i = 0; i < this.Functions.Length; i++)
            {
                if (string.IsNullOrEmpty(this.Functions[i].FilePath))
                { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {this.Functions[i]} is null"); }
                else
                { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {this.Functions[i]} : {this.Functions[i].FilePath}"); }
            }
            for (int i = 0; i < this.Structs.Length; i++)
            {
                if (string.IsNullOrEmpty(this.Structs[i].FilePath))
                { NotSetCallback?.Invoke($"StructDefinition.FilePath {this.Structs[i]} is null"); }
                else
                { NotSetCallback?.Invoke($"StructDefinition.FilePath {this.Structs[i]} : {this.Structs[i].FilePath}"); }
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

    public readonly struct ParserResultHeader
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
        /// <summary> Set by the Compiler </summary>
        public string CompiledUri;
        /// <summary> Set by the Compiler </summary>
        public double? DownloadTime;

        public string PathString
        {
            get
            {
                string result = "";
                for (int i = 0; i < Path.Length; i++)
                {
                    if (i > 0) result += ".";
                    result += Path[i].Content;
                }
                return result;
            }
        }
        public bool IsUrl => Path.Length == 1 && Uri.TryCreate(Path[0].Content, UriKind.Absolute, out var uri) && uri.Scheme != "file:";
    }

}
