using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode
{
    using Core;

    using Errors;

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

    [Serializable]
    public class TypeToken : Token
    {
        public BuiltinType typeName;
        public bool isList;

        public override string ToString()
        {
            return this.text + (isList ? "[]" : "");
        }

        public TypeToken(string name, BuiltinType type)
        {
            this.text = name;
            this.typeName = type;
        }

        public new TypeToken Clone()
        {
            return new TypeToken(this.text, this.typeName)
            {
                endOffset = this.endOffset,
                startOffset = this.startOffset,
                endOffsetTotal = this.endOffsetTotal,
                startOffsetTotal = this.startOffsetTotal,
                isList = this.isList,
                lineNumber = this.lineNumber,
                subtype = this.subtype,
            };
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

        [Serializable]
        public class ParameterDefinition
        {
            public string name;
            public TypeToken type;

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
            public TypeToken type;
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
                attributes = Array.Empty<Attribute>();
                this.namespacePath = namespacePath;
                this.Name = name;
            }

            public FunctionDefinition(List<string> namespacePath, string name)
            {
                parameters = new List<ParameterDefinition>();
                statements = new List<Statement>();
                attributes = Array.Empty<Attribute>();
                this.namespacePath = namespacePath.ToArray();
                this.Name = name;
            }

            public override string ToString()
            {
                return $"{this.type.text} {this.FullName}" + (this.parameters.Count > 0 ? "(...)" : "()") + " " + (this.statements.Count > 0 ? "{...}" : "{}");
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

                return $"{" ".Repeat(ident)}{this.type.text} {this.FullName}" + ($"({string.Join(", ", parameters)})") + " " + (this.statements.Count > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
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
            public TypeToken type;
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
                this.fields = new List<ParameterDefinition>();
                this.methods = new Dictionary<string, FunctionDefinition>();
                this.attributes = Array.Empty<FunctionDefinition.Attribute>();
                this.namespacePath = namespacePath;
                this.parameters = new List<ParameterDefinition>();
                this.statements = new List<Statement>();
                this.type = null;
            }
            public StructDefinition(List<string> namespacePath, string name)
            {
                this.name = name;
                this.fields = new List<ParameterDefinition>();
                this.methods = new Dictionary<string, FunctionDefinition>();
                this.attributes = Array.Empty<FunctionDefinition.Attribute>();
                this.namespacePath = namespacePath.ToArray();
                this.parameters = new List<ParameterDefinition>();
                this.statements = new List<Statement>();
                this.type = null;
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

        public struct ParserResult
        {
            public readonly List<FunctionDefinition> Functions;
            public readonly Dictionary<string, StructDefinition> Structs;
            public readonly List<Statement_NewVariable> GlobalVariables;
            public readonly List<string> Usings;

            public ParserResult(List<FunctionDefinition> functions, List<Statement_NewVariable> globalVariables, Dictionary<string, StructDefinition> structs, List<string> usings)
            {
                Functions = functions;
                GlobalVariables = globalVariables;
                Structs = structs;
                Usings = usings;
            }

            /// <summary>
            /// Converts the parsed AST into text
            /// </summary>
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
                    Console.Write($"{item.type} ");

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{item.variableName}");

                    Console.Write("\n\r");

                    Console.ResetColor();
                }

                Console.WriteLine("");

                foreach (var item in this.Structs)
                {
                    foreach (var attr in item.Value.attributes)
                    { Attribute(attr); }

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("struct ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{item.Key} ");
                    Console.Write("\n\r");

                    foreach (var field in item.Value.fields)
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
                    foreach (var attr in item.attributes)
                    { Attribute(attr); }

                    Console.ForegroundColor = ConsoleColor.Blue;
                    if (item.type.typeName == BuiltinType.STRUCT)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    Console.Write($"{item.type} ");

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"{item.FullName}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("(");
                    for (int i = 0; i < item.parameters.Count; i++)
                    {
                        if (i > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write($", ");
                        }

                        ParameterDefinition param = item.parameters[i];
                        Console.ForegroundColor = ConsoleColor.Blue;
                        if (param.withThisKeyword)
                        { Console.Write("this "); }
                        if (param.withRefKeyword)
                        { Console.Write("ref "); }
                        if (param.type.typeName == BuiltinType.STRUCT)
                        { Console.ForegroundColor = ConsoleColor.Green; }
                        Console.Write($"{param.type} ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{param.name}");
                    }
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(")");

                    if (item.statements.Count > 0)
                    {
                        int statementCount = 0;
                        void AddStatement(Statement st)
                        {
                            statementCount++;
                            if (st is Statement_ForLoop st0)
                            {
                                AddStatement(st0.condition);
                                AddStatement(st0.variableDeclaration);
                                AddStatement(st0.expression);
                            }
                            else if (st is Statement_WhileLoop st1)
                            {
                                AddStatement(st1.condition);
                            }
                            else if (st is Statement_FunctionCall st2)
                            {
                                foreach (var st3 in st2.parameters)
                                {
                                    AddStatement(st3);
                                }
                            }
                            else if (st is Statement_If_If st3)
                            {
                                AddStatement(st3.condition);
                            }
                            else if (st is Statement_If_ElseIf st4)
                            {
                                AddStatement(st4.condition);
                            }
                            else if (st is Statement_Index st5)
                            {
                                AddStatement(st5.indexStatement);
                                statementCount++;
                            }
                            else if (st is Statement_NewVariable st6)
                            {
                                if (st6.initialValue != null) AddStatement(st6.initialValue);
                            }
                            else if (st is Statement_Operator st7)
                            {
                                if (st7.Left != null) AddStatement(st7.Left);
                                if (st7.Right != null) AddStatement(st7.Right);
                            }

                            if (st is StatementParent st8)
                            {
                                foreach (var item in st8.statements)
                                {
                                    AddStatement(st);
                                }
                            }
                        }
                        foreach (var st in item.statements)
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
        }

        /// <summary>
        /// The parser for the BBCode language
        /// </summary>
        public class Parser
        {
            int currentTokenIndex;
            readonly List<Token> tokens = new();

            Token CurrentToken => (currentTokenIndex < tokens.Count) ? tokens[currentTokenIndex] : null;

            readonly Dictionary<string, TypeToken> types = new();
            readonly Dictionary<string, int> operators = new();
            bool enableThisKeyword;
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
            readonly List<FunctionDefinition> Functions = new();
            readonly Dictionary<string, StructDefinition> Structs = new();
            readonly List<Statement_NewVariable> GlobalVariables = new();
            readonly List<string> Usings = new();
            // === ===

            public Parser()
            {
                types.Add("int", new TypeToken("int", BuiltinType.INT));
                types.Add("string", new TypeToken("string", BuiltinType.STRING));
                types.Add("void", new TypeToken("void", BuiltinType.VOID));
                types.Add("float", new TypeToken("float", BuiltinType.FLOAT));
                types.Add("bool", new TypeToken("bool", BuiltinType.BOOLEAN));

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
            /// <exception cref="ParserException"/>
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

                return new ParserResult(this.Functions, this.GlobalVariables, this.Structs, this.Usings);
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
            /// <exception cref="ParserException"/>
            /// <exception cref="Exception"/>
            /// <exception cref="InternalException"/>
            /// <exception cref="NotImplementedException"/>
            /// <exception cref="System.Exception"/>
            /// <returns>
            /// The AST
            /// </returns>
            public static ParserResult Parse(string code, List<Warning> warnings, Action<string, Terminal.TerminalInterpreter.LogType> printCallback = null)
            {
                var (tokens, _) = Tokenizer.Parse(code, TokenizerSettings.Default, printCallback);

                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing...", Terminal.TerminalInterpreter.LogType.Debug); }

                Parser parser = new();
                var result = parser.Parse(tokens, warnings);

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", Terminal.TerminalInterpreter.LogType.Debug); }

                return result;
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

                TypeToken possibleType = ExceptTypeToken(false);
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

                    TypeToken possibleParameterType = ExceptTypeToken(false, true);
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

                Functions.Add(function);

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

            bool ExpectListValue(out Statement_ListValue listValue)
            {
                listValue = null;

                if (ExpectOperator("[", out var o0) == null)
                { return false; }

                listValue = new Statement_ListValue()
                {
                    Values = new List<Statement>()
                };

                int endlessSafe = 0;
                while (true)
                {
                    var v = ExpectExpression();
                    if (v == null)
                    { throw new SyntaxException("Expected expression", CurrentToken); }

                    listValue.Values.Add(v);

                    if (ExpectOperator(",") == null)
                    {
                        if (ExpectOperator("]") == null)
                        {
                            throw new SyntaxException("Unbalanced '['", o0);
                        }
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
                        value = CurrentToken.text,
                        type = new TypeToken("float", BuiltinType.FLOAT)
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
                        type = new TypeToken("int", BuiltinType.INT)
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
                        type = new TypeToken("string", BuiltinType.STRING)
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
                        type = new TypeToken("bool", BuiltinType.BOOLEAN)
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
                        type = new TypeToken("bool", BuiltinType.BOOLEAN)
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

                if (ExpectListValue(out var listValue))
                {
                    returnStatement = listValue;
                }
                else if (ExpectLiteral(out var literal))
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
                                position = new Position()
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
                        if (statement is StatementWithReturnValue statementWithReturnValue)
                        {
                            statementWithReturnValue.SaveValue = false;
                            statements.Add(statementWithReturnValue);
                        }
                        else
                        {
                            statements.Add(statement);
                        }

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
                TypeToken possibleType = ExceptTypeToken(out var structNotFoundWarning);
                if (possibleType == null)
                { currentTokenIndex = startTokenIndex; return null; }

                bool IsRef = false;
                if (ExpectIdentifier("ref") != null)
                {
                    if (enableRefKeyword)
                    {
                        throw new NotImplementedException();
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
                    statement.initialValue = ExpectExpression() ?? throw new SyntaxException("Expected initial value after '=' in variable declaration", eqT);
                }
                else
                {
                    if (IsRef)
                    { throw new SyntaxException("Initial value for reference variable declaration is requied"); }
                    if (possibleType.typeName == BuiltinType.AUTO)
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

                methodCall = new(CurrentNamespace.ToArray(), Array.Empty<string>(), true)
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

                if (ExpectOperator("-", out var tMinusOperator) != null)
                {
                    Statement statement = ExpectOneValue();
                    if (statement == null)
                    { throw new SyntaxException("Expected value or expression after '-' operator", tMinusOperator); }

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

                            operatorCall.position = op.Position;
                            operatorCall.position.Extend(rightmostOperator.Right.position.AbsolutePosition);
                            operatorCall.position.Extend(rightStatement.position.AbsolutePosition);


                            rightmostOperator.Right = operatorCall;
                        }
                        else
                        {
                            Statement_Operator operatorCall = new(op.text, leftStatement, rightStatement);

                            operatorCall.position = op.Position;
                            operatorCall.position.Extend(leftStatement.position.AbsolutePosition);
                            operatorCall.position.Extend(rightStatement.position.AbsolutePosition);

                            leftStatement = operatorCall;
                        }
                    }
                    else
                    {
                        Statement_Operator operatorCall = new(op.text, leftStatement, rightStatement);

                        operatorCall.position = op.Position;
                        operatorCall.position.Extend(leftStatement.position.AbsolutePosition);
                        operatorCall.position.Extend(rightStatement.position.AbsolutePosition);

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

                Statement_MethodCall methodCall = new(CurrentNamespace.ToArray(), Array.Empty<string>())
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
            Statement_FunctionCall ExpectKeywordCall(string name, bool canHaveParameters = false, bool needParameters = false)
            {
                int startTokenIndex = currentTokenIndex;

                Token possibleFunctionName = ExpectIdentifier();
                if (possibleFunctionName == null)
                { currentTokenIndex = startTokenIndex; return null; }

                if (possibleFunctionName.text != name)
                { currentTokenIndex = startTokenIndex; return null; }

                possibleFunctionName.subtype = TokenSubtype.Statement;

                Statement_FunctionCall functionCall = new(Array.Empty<string>(), Array.Empty<string>())
                {
                    functionNameT = possibleFunctionName
                };

                functionCall.position.Line = possibleFunctionName.lineNumber;
                functionCall.position.Extend(possibleFunctionName.Position.AbsolutePosition);

                if (canHaveParameters)
                {
                    Statement parameter = ExpectExpression();
                    if (parameter == null && needParameters)
                    { throw new SyntaxException("Expected expression as parameter", functionCall.position); }

                    if (parameter != null)
                    {
                        functionCall.position.Extend(parameter.position.AbsolutePosition);
                        functionCall.parameters.Add(parameter);
                    }
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

                TypeToken possibleType = ExceptTypeToken(false);
                if (possibleType == null)
                { currentTokenIndex = parseStart; return null; }

                Token possibleName = ExpectIdentifier();
                if (possibleName == null)
                { currentTokenIndex = parseStart; return null; }

                Token possibleOperator = ExpectOperator("(");
                if (possibleOperator == null)
                { currentTokenIndex = parseStart; return null; }

                throw new Errors.Exception("Methods are not supported", possibleType.Position);

                FunctionDefinition methodDefinition = new(Array.Empty<string>(), possibleName.text)
                {
                    type = possibleType,
                    attributes = attributes.ToArray()
                };

                possibleName.subtype = TokenSubtype.MethodName;

                var expectParameter = false;
                while (ExpectOperator(")") == null || expectParameter)
                {
                    Token thisKeywordT = ExpectIdentifier("this");
                    if (thisKeywordT != null)
                    {
                        thisKeywordT.subtype = TokenSubtype.Keyword;
                        if (methodDefinition.parameters.Count > 0)
                        { throw new ParserException("Keyword 'this' is only valid at the first parameter", thisKeywordT); }
                    }

                    TypeToken possibleParameterType = ExceptTypeToken(false, true);
                    if (possibleParameterType == null)
                        throw new SyntaxException("Expected parameter type", possibleOperator);

                    Token possibleParameterName = ExpectIdentifier();
                    if (possibleParameterName == null)
                        throw new SyntaxException("Expected a parameter name", possibleParameterType.Position);

                    possibleParameterName.subtype = TokenSubtype.VariableName;

                    ParameterDefinition parameterDefinition = new()
                    {
                        type = possibleParameterType,
                        name = possibleParameterName.text,
                        withThisKeyword = thisKeywordT != null
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
                TypeToken possibleType = ExceptTypeToken();
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

            TypeToken ExceptTypeToken(out Warning warning, bool allowVarKeyword = true, bool allowAnyKeyword = false)
            {
                warning = null;
                Token possibleType = ExpectIdentifier();
                if (possibleType == null) return null;

                possibleType.subtype = TokenSubtype.Keyword;

                bool typeFound = types.TryGetValue(possibleType.text, out TypeToken foundtype);

                TypeToken newType = null;

                if (typeFound == false)
                {
                    if (newType == null && possibleType.text == "any")
                    {
                        if (allowAnyKeyword)
                        { newType = new TypeToken("any", BuiltinType.ANY); }
                        else
                        {
                            throw new ParserException($"TypeToken '{possibleType.text}' is not valid in the current context");
                        }
                    }

                    if (newType == null && possibleType.text == "var")
                    {
                        if (allowVarKeyword)
                        { newType = new TypeToken("var", BuiltinType.AUTO); }
                        else
                        {
                            throw new ParserException($"TypeToken '{possibleType.text}' is not valid in the current context");
                        }
                    }

                    if (newType == null)
                    {
                        if (TryGetStruct(possibleType.text, out var s))
                        {
                            newType = new TypeToken(s.FullName, BuiltinType.STRUCT);
                            possibleType.subtype = TokenSubtype.Struct;
                        }
                        else
                        {
                            newType = new TypeToken(possibleType.text, BuiltinType.STRUCT);
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

                if (ExpectOperator("[") != null)
                {
                    if (ExpectOperator("]") != null)
                    { newType.isList = true; }
                    else
                    { throw new SyntaxException("Unbalanced '['"); }
                }
                return newType;
            }

            TypeToken ExceptTypeToken(bool allowVarKeyword = true, bool allowAnyKeyword = false) => ExceptTypeToken(out Warning _, allowVarKeyword, allowAnyKeyword);

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
}