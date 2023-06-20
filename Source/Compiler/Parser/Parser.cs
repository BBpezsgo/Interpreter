using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode
{
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.BBCode.Parser;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public enum BuiltinType
    {
        VOID,

        AUTO,
        ANY,

        BYTE,
        INT,
        FLOAT,

        BOOLEAN,

        CHAR,
    }

    public class TypeInstance : IEquatable<TypeInstance>
    {
        public Token Identifier;

        public TypeInstance(Token identifier) : base()
        {
            this.Identifier = identifier;
        }
        public static TypeInstance CreateAnonymous(LiteralType literalType, Func<string, string> typeDefinitionReplacer)
            => TypeInstance.CreateAnonymous(literalType.ToStringRepresentation(), typeDefinitionReplacer);
        public static TypeInstance CreateAnonymous(string name, Func<string, string> typeDefinitionReplacer)
        {
            string definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstance(Token.CreateAnonymous(name)); }
            else
            { return new TypeInstance(Token.CreateAnonymous(definedType)); }
        }
        public static TypeInstance CreateAnonymous(CompiledType compiledType)
        {
            if (compiledType is null) throw new ArgumentNullException(nameof(compiledType));
            return new TypeInstance(Token.CreateAnonymous(compiledType.Name));
        }

        public override string ToString() => this.Identifier.Content;

        public static bool operator ==(TypeInstance a, string b)
        {
            if (a is null && b is null) return true;
            if (a is not null && b is null) return false;
            if (a is null && b is not null) return false;
            return a.Identifier.Content == b;
        }
        public static bool operator !=(TypeInstance a, string b) => !(a == b);

        public static bool operator ==(string a, TypeInstance b) => b == a;
        public static bool operator !=(string a, TypeInstance b) => !(b == a);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            { return true; }

            if (obj is null)
            { return false; }

            if (obj is TypeInstance other)
            { return this.Equals(other); }

            return false;
        }

        public bool Equals(TypeInstance other) => other is not null && this.Identifier.Equals(other.Identifier);

        public override int GetHashCode() => HashCode.Combine(Identifier);
    }

    namespace Parser
    {
        using Statements;

        /// <summary>
        /// The parser for the BBCode language
        /// </summary>
        public class Parser
        {
            int currentTokenIndex;
            readonly List<Token> tokens = new();
            public Token[] Tokens => tokens.ToArray();

            Token CurrentToken => (currentTokenIndex >= 0 && currentTokenIndex < tokens.Count) ? tokens[currentTokenIndex] : null;

            static readonly string[] types = new string[]
            {
                "int",
                "void",
                "float",
                "bool",
                "byte",
                "char",
            };
            readonly Dictionary<string, int> operators = new();

            List<Warning> Warnings;
            public readonly List<Error> Errors = new();

            // === Result ===
            readonly List<FunctionDefinition> Functions = new();
            readonly List<EnumDefinition> Enums = new();
            readonly Dictionary<string, StructDefinition> Structs = new();
            readonly Dictionary<string, ClassDefinition> Classes = new();
            readonly List<UsingDefinition> Usings = new();
            readonly List<Statement_HashInfo> Hashes = new();
            readonly List<Statement> TopLevelStatements = new();
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

                operators.Add("&&", 2);
                operators.Add("||", 2);
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

                return new ParserResult(this.Functions, this.Structs.Values, this.Usings, this.Hashes, this.Classes.Values, this.TopLevelStatements, this.Enums);
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

            #region Parse top level

            bool ExpectHash(out Statement_HashInfo hashStatement)
            {
                hashStatement = null;

                if (!ExpectOperator("#", out var hashT))
                { return false; }

                hashT.AnalysedType = TokenAnalysedType.Hash;

                if (!ExpectIdentifier(out var hashName))
                { throw new SyntaxException($"Expected identifier after '#' , got {CurrentToken.TokenType.ToString().ToLower()} \"{CurrentToken.Content}\"", hashT); }

                hashName.AnalysedType = TokenAnalysedType.Hash;

                List<Statement_Literal> parameters = new();
                int endlessSafe = 50;
                while (!ExpectOperator(";"))
                {
                    if (!ExpectLiteral(out var parameter))
                    { throw new SyntaxException($"Expected hash literal parameter or ';' , got {CurrentToken.TokenType.ToString().ToLower()} \"{CurrentToken.Content}\"", CurrentToken); }

                    parameter.ValueToken.AnalysedType = TokenAnalysedType.HashParameter;
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

                keyword.AnalysedType = TokenAnalysedType.Keyword;

                List<Token> tokens = new();
                if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                {
                    tokens.Add(CurrentToken);
                    currentTokenIndex++;
                }
                else
                {
                    int endlessSafe = 50;
                    while (ExpectIdentifier(out Token pathIdentifier))
                    {
                        pathIdentifier.AnalysedType = TokenAnalysedType.Library;
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
                if (ExpectStructDefinition()) { }
                else if (ExpectClassDefinition()) { }
                else if (ExpectFunctionDefinition(out var functionDefinition))
                { Functions.Add(functionDefinition); }
                else if (ExpectEnumDefinition(out var enumDefinition))
                { Enums.Add(enumDefinition); }
                else
                {
                    Statement statement = ExpectStatement();
                    if (statement == null)
                    { throw new SyntaxException($"Expected top-level statement, type or function definition. Got a token {CurrentToken}", CurrentToken); }

                    SetStatementThings(statement);

                    TopLevelStatements.Add(statement);

                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition())); }
                }
            }

            bool ExpectEnumDefinition(out EnumDefinition enumDefinition)
            {
                int parseStart = currentTokenIndex;
                enumDefinition = null;

                List<FunctionDefinition.Attribute> attributes = new();
                while (ExpectAttribute(out var attr))
                {
                    bool alreadyHave = false;
                    foreach (var attribute in attributes)
                    {
                        if (attribute.Identifier == attr.Identifier)
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
                    { Errors.Add(new Error($"Attribute \"{attr}\" already applied", attr.Identifier)); }
                }

                if (!ExpectIdentifier("enum", out Token keyword))
                { currentTokenIndex = parseStart; return false; }

                keyword.AnalysedType = TokenAnalysedType.Keyword;

                if (!ExpectIdentifier(out Token identifier))
                { throw new SyntaxException($"Expected identifier token after keyword \"{keyword}\"", keyword.After()); }

                if (!ExpectOperator("{"))
                { throw new SyntaxException($"Expected '{{' after enum identifier", identifier.After()); }

                identifier.AnalysedType = TokenAnalysedType.Enum;

                List<EnumMemberDefinition> members = new();

                while (!ExpectOperator("}"))
                {
                    if (!ExpectIdentifier(out Token enumMemberIdentifier))
                    { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                    enumMemberIdentifier.AnalysedType = TokenAnalysedType.EnumMember;

                    EnumMemberDefinition newMember = new();
                    newMember.Identifier = enumMemberIdentifier;

                    if (ExpectOperator("=", out Token assignOperator))
                    {
                        if (!ExpectLiteral(out Statement_Literal value))
                        { throw new SyntaxException($"Expected literal after enum member assignment", assignOperator.After()); }

                        newMember.Value = value;
                    }

                    members.Add(newMember);

                    if (ExpectOperator("}"))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }

                    throw new SyntaxException("Expected ',' or '}'", CurrentToken);
                }

                enumDefinition = new()
                {
                    Identifier = identifier,
                    Attributes = attributes.ToArray(),
                    Members = members.ToArray(),
                };

                return true;
            }

            bool ExpectFunctionDefinition(out FunctionDefinition function)
            {
                int parseStart = currentTokenIndex;
                function = null;

                List<FunctionDefinition.Attribute> attributes = new();
                while (ExpectAttribute(out var attr))
                {
                    bool alreadyHave = false;
                    foreach (var attribute in attributes)
                    {
                        if (attribute.Identifier == attr.Identifier)
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
                    { Errors.Add(new Error("Attribute '" + attr + "' already applied to the function", attr.Identifier)); }
                }

                ExpectIdentifier("export", out Token ExportKeyword);

                TypeInstance possibleType = ExpectType(false);
                if (possibleType == null)
                { currentTokenIndex = parseStart; return false; }

                if (!ExpectIdentifier(out Token possibleNameT))
                { currentTokenIndex = parseStart; return false; }

                if (!ExpectOperator("("))
                { currentTokenIndex = parseStart; return false; }

                possibleNameT.AnalysedType = TokenAnalysedType.FunctionName;

                List<ParameterDefinition> parameters = new();

                var expectParameter = false;
                while (!ExpectOperator(")") || expectParameter)
                {
                    if (ExpectIdentifier("this", out Token thisKeywordT))
                    {
                        thisKeywordT.AnalysedType = TokenAnalysedType.Keyword;
                        if (parameters.Count > 0)
                        { Errors.Add(new Error("Keyword 'this' is only valid at the first parameter", thisKeywordT)); }
                    }

                    TypeInstance possibleParameterType = ExpectType(false, true);
                    if (possibleParameterType == null)
                    { throw new SyntaxException("Expected parameter type", CurrentToken); }

                    if (!ExpectIdentifier(out Token possibleParameterNameT))
                    { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                    possibleParameterNameT.AnalysedType = TokenAnalysedType.VariableName;

                    ParameterDefinition parameterDefinition = new()
                    {
                        Type = possibleParameterType,
                        Identifier = possibleParameterNameT,
                        withThisKeyword = thisKeywordT != null,
                    };
                    parameters.Add(parameterDefinition);

                    if (ExpectOperator(")"))
                    { break; }

                    if (!ExpectOperator(","))
                    { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                    else
                    { expectParameter = true; }
                }

                function = new(possibleNameT)
                {
                    Type = possibleType,
                    Attributes = attributes.ToArray(),
                    ExportKeyword = ExportKeyword,
                    Parameters = parameters.ToArray(),
                };

                List<Statement> statements = new();

                if (!ExpectOperator(";"))
                {
                    statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
                    function.BracketStart = braceletStart;
                    function.BracketEnd = braceletEnd;
                }

                function.Statements = statements.ToArray();

                return true;
            }

            bool ExpectGeneralFunctionDefinition(out GeneralFunctionDefinition function)
            {
                int parseStart = currentTokenIndex;
                function = null;

                ExpectIdentifier("export", out Token ExportKeyword);

                if (!ExpectIdentifier(out Token possibleNameT))
                { currentTokenIndex = parseStart; return false; }

                if (!ExpectOperator("("))
                { currentTokenIndex = parseStart; return false; }

                possibleNameT.AnalysedType = TokenAnalysedType.FunctionName;

                List<ParameterDefinition> parameters = new();

                var expectParameter = false;
                while (!ExpectOperator(")") || expectParameter)
                {
                    if (ExpectIdentifier("this", out Token thisKeywordT))
                    { throw new SyntaxException($"Keyword 'this' is not valid in general function definitions", thisKeywordT); }

                    TypeInstance possibleParameterType = ExpectType(false, true);
                    if (possibleParameterType == null)
                    { throw new SyntaxException("Expected parameter type", CurrentToken); }

                    if (!ExpectIdentifier(out Token possibleParameterNameT))
                    { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                    possibleParameterNameT.AnalysedType = TokenAnalysedType.VariableName;

                    ParameterDefinition parameterDefinition = new()
                    {
                        Type = possibleParameterType,
                        Identifier = possibleParameterNameT,
                        withThisKeyword = thisKeywordT != null,
                    };
                    parameters.Add(parameterDefinition);

                    if (ExpectOperator(")"))
                    { break; }

                    if (!ExpectOperator(","))
                    { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                    else
                    { expectParameter = true; }
                }

                function = new(possibleNameT)
                {
                    ExportKeyword = ExportKeyword,
                    Parameters = parameters.ToArray(),
                };

                if (ExpectOperator(";", out var tIdk))
                { throw new SyntaxException($"Body is requied for general function definition", tIdk); }

                List<Statement> statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
                function.BracketStart = braceletStart;
                function.BracketEnd = braceletEnd;

                function.Statements = statements.ToArray();

                return true;
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
                        if (attribute.Identifier == attr.Identifier)
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
                    { Errors.Add(new Error("Attribute '" + attr + "' already applied to the class", attr.Identifier)); }
                }

                ExpectIdentifier("export", out Token ExportKeyword);

                if (!ExpectIdentifier("class", out Token keyword))
                { currentTokenIndex = startTokenIndex; return false; }

                if (!ExpectIdentifier(out Token possibleClassName))
                { throw new SyntaxException("Expected class identifier after keyword 'class'", keyword); }

                if (!ExpectOperator("{", out var braceletStart))
                { throw new SyntaxException("Expected '{' after class identifier", possibleClassName); }

                possibleClassName.AnalysedType = TokenAnalysedType.Class;
                keyword.AnalysedType = TokenAnalysedType.Keyword;

                List<FieldDefinition> fields = new();
                List<FunctionDefinition> methods = new();
                List<GeneralFunctionDefinition> generalMethods = new();

                int endlessSafe = 0;
                Token braceletEnd;
                while (!ExpectOperator("}", out braceletEnd))
                {
                    if (ExpectFunctionDefinition(out var methodDefinition))
                    {
                        methods.Add(methodDefinition);
                    }
                    else if (ExpectGeneralFunctionDefinition(out var generalMethodDefinition))
                    {
                        generalMethods.Add(generalMethodDefinition);
                    }
                    else if (ExpectField(out FieldDefinition field))
                    {
                        fields.Add(field);
                        if (!ExpectOperator(";"))
                        { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", new Position(CurrentToken.Position.Start.Line))); }
                    }
                    else
                    {
                        throw new SyntaxException($"Expected field definition", CurrentToken);
                    }

                    endlessSafe++;
                    if (endlessSafe > 50)
                    {
                        throw new EndlessLoopException();
                    }
                }

                ClassDefinition classDefinition = new(possibleClassName, attributes, fields, methods, generalMethods)
                {
                    BracketStart = braceletStart,
                    BracketEnd = braceletEnd,
                    ExportKeyword = ExportKeyword,
                };

                Classes.Add(classDefinition.Name.Content, classDefinition);

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
                        if (attribute.Identifier == attr.Identifier)
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
                    { Errors.Add(new Error("Attribute '" + attr + "' already applied to the struct", attr.Identifier)); }
                }

                ExpectIdentifier("export", out Token ExportKeyword);

                if (!ExpectIdentifier("struct", out Token keyword))
                { currentTokenIndex = startTokenIndex; return false; }

                if (!ExpectIdentifier(out Token possibleStructName))
                { throw new SyntaxException("Expected struct identifier after keyword 'struct'", keyword); }

                if (!ExpectOperator("{", out var braceletStart))
                { throw new SyntaxException("Expected '{' after struct identifier", possibleStructName); }

                keyword.AnalysedType = TokenAnalysedType.Keyword;

                List<FieldDefinition> fields = new();
                Dictionary<string, FunctionDefinition> methods = new();

                int endlessSafe = 0;
                Token braceletEnd;
                while (!ExpectOperator("}", out braceletEnd))
                {
                    FieldDefinition field = ExpectField();
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

                StructDefinition structDefinition = new(possibleStructName, attributes, fields, methods)
                {
                    BracketStart = braceletStart,
                    BracketEnd = braceletEnd,
                    ExportKeyword = ExportKeyword,
                };

                Structs.Add(structDefinition.Name.Content, structDefinition);

                return true;
            }

            #endregion

            #region Parse low level

            bool ExpectListValue(out Statement_ListValue listValue)
            {
                listValue = null;

                if (!ExpectOperator("[", out var o0))
                { return false; }

                List<StatementWithReturnValue> values = new();
                listValue = new Statement_ListValue()
                {
                    Values = Array.Empty<StatementWithReturnValue>(),
                    BracketLeft = o0,
                };

                int endlessSafe = 0;
                while (true)
                {
                    var v = ExpectExpression();
                    if (v != null)
                    {
                        values.Add(v);

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
                listValue.Values = values.ToArray();

                return true;
            }

            bool ExpectLiteral(out Statement_Literal statement)
            {
                int savedToken = currentTokenIndex;

                if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.Content.Replace("_", ""),
                        Type = LiteralType.FLOAT,
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.Content.Replace("_", ""),
                        Type = LiteralType.INT,
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_HEX)
                {
                    Statement_Literal literal = new()
                    {
                        Value = Convert.ToInt32(CurrentToken.Content, 16).ToString(),
                        Type = LiteralType.INT,
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_BIN)
                {
                    Statement_Literal literal = new()
                    {
                        Value = Convert.ToInt32(CurrentToken.Content[2..].Replace("_", ""), 2).ToString(),
                        Type = LiteralType.INT,
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.Content,
                        Type = LiteralType.STRING,
                        ValueToken = CurrentToken,
                    };

                    currentTokenIndex++;

                    statement = literal;
                    return true;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_CHAR)
                {
                    Statement_Literal literal = new()
                    {
                        Value = CurrentToken.Content,
                        Type = LiteralType.CHAR,
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
                        Type = LiteralType.BOOLEAN,
                        ValueToken = CurrentToken,
                    };

                    tTrue.AnalysedType = TokenAnalysedType.Keyword;

                    statement = literal;
                    return true;
                }
                else if (ExpectIdentifier("false", out var tFalse))
                {
                    Statement_Literal literal = new()
                    {
                        Value = "false",
                        Type = LiteralType.BOOLEAN,
                        ValueToken = CurrentToken,
                    };

                    tFalse.AnalysedType = TokenAnalysedType.Keyword;

                    statement = literal;
                    return true;
                }

                currentTokenIndex = savedToken;

                statement = null;
                return false;
            }

            bool ExpectAs(out Statement_As statement)
            {
                int parseStart = currentTokenIndex;
                statement = null;

                StatementWithReturnValue prevStatement = ExpectOneValue();
                if (prevStatement == null)
                {
                    currentTokenIndex = parseStart;
                    return false;
                }

                if (!ExpectIdentifier("as", out Token keyword))
                {
                    currentTokenIndex = parseStart;
                    return false;
                }

                TypeInstance type = ExpectType(false, false);

                if (type == null)
                { throw new SyntaxException($"Expected type after 'as' keyword", keyword.After()); }

                statement = new Statement_As(prevStatement, keyword, type);
                return true;
            }

            bool ExpectIndex(out Statement_Index statement)
            {
                if (ExpectOperator("[", out var token0))
                {
                    var st = ExpectOneValue();
                    if (ExpectOperator("]", out _))
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
            ///  <seealso cref="Statement_NewInstance"></seealso>
            /// </item>
            /// <item>
            ///  <seealso cref="Statement_Field"></seealso>
            /// </item>
            /// <item>
            ///  <seealso cref="Statement_Variable"></seealso>
            /// </item>
            /// </list>
            /// </returns>
            StatementWithReturnValue ExpectOneValue()
            {
                int savedToken = currentTokenIndex;

                StatementWithReturnValue returnStatement = null;

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
                    newIdentifier.AnalysedType = TokenAnalysedType.Keyword;

                    if (!ExpectIdentifier(out Token instanceTypeName))
                    { throw new SyntaxException("Expected instance constructor after keyword 'new'", newIdentifier); }

                    if (instanceTypeName == null)
                    { throw new SyntaxException("Expected instance constructor after keyword 'new'", newIdentifier); }

                    if (ExpectOperator("("))
                    {
                        Statement_ConstructorCall newStructStatement = new()
                        {
                            TypeName = instanceTypeName,
                            Keyword = newIdentifier,
                        };

                        bool expectParameter = false;
                        List<StatementWithReturnValue> parameters = new();

                        int endlessSafe = 0;
                        while (!ExpectOperator(")") || expectParameter)
                        {
                            StatementWithReturnValue parameter = ExpectExpression();
                            if (parameter == null)
                            { throw new SyntaxException("Expected expression as parameter", newStructStatement.TotalPosition()); }

                            parameters.Add(parameter);

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
                        newStructStatement.Parameters = parameters.ToArray();

                        returnStatement = newStructStatement;
                    }
                    else
                    {
                        Statement_NewInstance newStructStatement = new()
                        {
                            TypeName = instanceTypeName,
                            Keyword = newIdentifier,
                        };

                        returnStatement = newStructStatement;
                    }
                }
                else if (ExpectIdentifier(out Token variableName))
                {
                    // if (variableName.Content == "this")
                    // { Errors.Add(new Error("The keyword 'this' does not avaiable in the current context", variableName)); }

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

                        if (variableName.Content == "this")
                        { variableName.AnalysedType = TokenAnalysedType.Keyword; }
                        else
                        { variableName.AnalysedType = TokenAnalysedType.VariableName; }

                        returnStatement = variableNameStatement;
                    }
                }
                else if (ExpectVariableAddressGetter(out Statement_MemoryAddressGetter memoryAddressGetter))
                {
                    returnStatement = memoryAddressGetter;
                }
                else if (ExpectVariableAddressFinder(out Statement_MemoryAddressFinder memoryAddressFinder))
                {
                    returnStatement = memoryAddressFinder;
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

                {
                    if (ExpectIdentifier("as", out Token keyword))
                    {
                        TypeInstance type = ExpectType(false, false);

                        if (type == null)
                        { throw new SyntaxException($"Expected type after 'as' keyword", keyword.After()); }

                        returnStatement = new Statement_As(returnStatement, keyword, type);
                    }
                }

                return returnStatement;
            }

            bool ExpectVariableAddressGetter(out Statement_MemoryAddressGetter statement)
            {
                var parseStart = currentTokenIndex;
                if (!ExpectOperator("&", out var refToken))
                {
                    statement = null;
                    currentTokenIndex = parseStart;
                    return false;
                }

                var prevStatement = ExpectOneValue();

                statement = new Statement_MemoryAddressGetter()
                {
                    OperatorToken = refToken,
                    PrevStatement = prevStatement,
                };
                return true;
            }

            bool ExpectVariableAddressFinder(out Statement_MemoryAddressFinder statement)
            {
                var parseStart = currentTokenIndex;
                if (!ExpectOperator("*", out var refToken))
                {
                    statement = null;
                    currentTokenIndex = parseStart;
                    return false;
                }

                var prevStatement = ExpectOneValue();

                statement = new Statement_MemoryAddressFinder()
                {
                    OperatorToken = refToken,
                    PrevStatement = prevStatement,
                };
                return true;
            }

            void SetStatementThings(Statement statement)
            {
                if (statement == null)
                {
                    if (CurrentToken != null)
                    { throw new SyntaxException($"Unknown statement null", CurrentToken); }
                    else
                    { throw new SyntaxException($"Unknown statement null", Position.UnknownPosition); }
                }

                if (statement is Statement_Literal)
                { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement.TotalPosition()); }

                if (statement is Statement_Variable)
                { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement.TotalPosition()); }

                if (statement is Statement_NewInstance)
                { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement.TotalPosition()); }

                if (statement is StatementWithReturnValue statementWithReturnValue)
                {
                    statementWithReturnValue.SaveValue = false;
                }
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
                    SetStatementThings(statement);

                    statements.Add(statement);

                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition())); }


                    endlessSafe++;
                    if (endlessSafe > 500) throw new EndlessLoopException();
                }

                return statements;
            }

            Statement_NewVariable ExpectVariableDeclaration()
            {
                int startTokenIndex = currentTokenIndex;
                TypeInstance possibleType = ExpectType();
                if (possibleType == null)
                { currentTokenIndex = startTokenIndex; return null; }

                if (!ExpectIdentifier(out Token possibleVariableName))
                { currentTokenIndex = startTokenIndex; return null; }

                possibleVariableName.AnalysedType = TokenAnalysedType.VariableName;

                Statement_NewVariable statement = new()
                {
                    VariableName = possibleVariableName,
                    Type = possibleType,
                };

                if (ExpectOperator("=", out var eqT))
                {
                    statement.InitialValue = ExpectExpression() ?? throw new SyntaxException("Expected initial value after '=' in variable declaration", eqT);
                }
                else
                {
                    if (possibleType.Identifier.Content == "var")
                    { throw new SyntaxException("Initial value for 'var' variable declaration is requied", possibleType.Identifier); }
                }

                return statement;
            }

            Statement_ForLoop ExpectForStatement()
            {
                if (!ExpectIdentifier("for", out Token tokenFor))
                { return null; }

                tokenFor.AnalysedType = TokenAnalysedType.Statement;

                if (!ExpectOperator("(", out Token tokenZarojel))
                { throw new SyntaxException("Expected '(' after \"for\" statement", tokenFor); }

                var variableDeclaration = ExpectVariableDeclaration();
                if (variableDeclaration == null)
                { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenZarojel); }

                if (!ExpectOperator(";"))
                { throw new SyntaxException("Expected ';' after \"for\" variable declaration", variableDeclaration.TotalPosition()); }

                StatementWithReturnValue condition = ExpectExpression();
                if (condition == null)
                { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenZarojel); }

                if (!ExpectOperator(";"))
                { throw new SyntaxException($"Expected ';' after \"for\" condition, got {CurrentToken}", variableDeclaration.TotalPosition()); }

                Statement expression = ExpectSetter();
                if (expression == null)
                { throw new SyntaxException($"Expected expression (setter) after \"for\" condition, got {CurrentToken}", tokenZarojel); }

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

                    SetStatementThings(statement);

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

                tokenWhile.AnalysedType = TokenAnalysedType.Statement;

                if (!ExpectOperator("(", out Token tokenZarojel))
                { throw new SyntaxException("Expected '(' after \"while\" statement", tokenWhile); }

                StatementWithReturnValue condition = ExpectExpression();
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
                    if (statement == null) break;

                    SetStatementThings(statement);

                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error($"Expected ';' at end of statement  (after {statement.GetType().Name})", statement.TotalPosition())); }

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

                tokenIf.AnalysedType = TokenAnalysedType.Statement;

                StatementWithReturnValue condition = null;
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

                    SetStatementThings(statement);

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
            ///  <seealso cref="Statement_KeywordCall"></seealso>
            /// </item>
            /// <item>
            ///  <seealso cref="Statement_If"></seealso>
            /// </item>
            /// <item>
            ///  <seealso cref="Statement_NewVariable"></seealso>
            /// </item>
            /// <item>
            ///  <seealso cref="Statement_Setter"></seealso>
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
                statement ??= ExpectKeywordCall("throw", true, true);
                statement ??= ExpectKeywordCall("break");
                statement ??= ExpectKeywordCall("delete", true, true);
                statement ??= ExpectIfStatement();
                statement ??= ExpectVariableDeclaration();
                statement ??= ExpectSetter();
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

                possibleFunctionName.AnalysedType = TokenAnalysedType.FunctionName;

                methodCall = new()
                {
                    Identifier = possibleFunctionName,
                };

                bool expectParameter = false;

                List<StatementWithReturnValue> parameters = new();
                int endlessSafe = 0;
                while (!ExpectOperator(")") || expectParameter)
                {
                    StatementWithReturnValue parameter = ExpectExpression();
                    if (parameter == null)
                    { throw new SyntaxException("Expected expression as parameter", methodCall.TotalPosition()); }

                    parameters.Add(parameter);

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
                methodCall.Parameters = parameters.ToArray();

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
            ///  <seealso cref="Statement_NewInstance"></seealso>
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
            StatementWithReturnValue ExpectExpression()
            {
                if (ExpectOperator("!", out var tNotOperator))
                {
                    StatementWithReturnValue statement = ExpectOneValue();
                    if (statement == null)
                    { throw new SyntaxException($"Expected OneValue after operator ('{tNotOperator}'), got {CurrentToken}", CurrentToken); }

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

                StatementWithReturnValue leftStatement = ExpectOneValue();
                if (leftStatement == null) return null;

                while (true)
                {
                    int parseStart = currentTokenIndex;
                    if (!ExpectOperator(new string[] {
                        "<<", ">>",
                        "+", "-", "*", "/", "%", "&", "|",
                        "<", ">", ">=", "<=", "!=", "==", "&&", "||", "^"
                    }, out Token op)) break;

                    StatementWithReturnValue rightStatement = ExpectOneValue();

                    if (rightStatement == null)
                    { throw new SyntaxException($"Expected OneValue after operator ('{op}'), got {CurrentToken}", CurrentToken); }

                    int rightSidePrecedence = OperatorPrecedence(op.Content);

                    Statement_Operator rightmostStatement = FindRightmostStatement(leftStatement, rightSidePrecedence);
                    if (rightmostStatement != null)
                    {
                        Statement_Operator operatorCall = new(op, rightmostStatement.Right, rightStatement);
                        rightmostStatement.Right = operatorCall;
                    }
                    else
                    {
                        Statement_Operator operatorCall = new(op, leftStatement, rightStatement);
                        leftStatement = operatorCall;
                    }
                }

                return leftStatement;
            }

            /// <exception cref="SyntaxException"></exception>
            Statement_Setter ExpectSetter()
            {
                int parseStart = currentTokenIndex;
                StatementWithReturnValue leftStatement = ExpectExpression();
                if (leftStatement == null)
                {
                    currentTokenIndex = parseStart;
                    return null;
                }

                if (ExpectOperator(new string[] {
                    "+=", "-=", "*=", "/=", "%=",
                    "&=", "|=", "^=",
                }, out var o0))
                {
                    StatementWithReturnValue valueToAssign = ExpectExpression();
                    if (valueToAssign == null)
                    { throw new SyntaxException("Expected expression", o0); }

                    Statement_Operator statementToAssign = new(new Token()
                    {
                        AbsolutePosition = o0.AbsolutePosition,
                        Content = o0.Content.Replace("=", ""),
                        Position = o0.Position,
                        TokenType = o0.TokenType,
                    }, leftStatement, valueToAssign);

                    return new Statement_Setter(new Token()
                    {
                        AbsolutePosition = o0.AbsolutePosition,
                        Content = "=",
                        Position = o0.Position,
                        TokenType = o0.TokenType,
                    }, leftStatement, statementToAssign);
                }

                if (ExpectOperator("++", out var t0))
                {
                    Statement_Literal literalOne = new()
                    {
                        Value = "1",
                        Type = LiteralType.INT,
                        ImagineryPosition = t0.GetPosition(),
                    };

                    Statement_Operator statementToAssign = new(new Token()
                    {
                        AbsolutePosition = t0.AbsolutePosition,
                        Content = "+",
                        Position = t0.Position,
                        TokenType = t0.TokenType,
                    }, leftStatement, literalOne);

                    return new Statement_Setter(new Token()
                    {
                        AbsolutePosition = t0.AbsolutePosition,
                        Content = "=",
                        Position = t0.Position,
                        TokenType = t0.TokenType,
                    }, leftStatement, statementToAssign);
                }

                if (ExpectOperator("--", out var t1))
                {
                    Statement_Literal literalOne = new()
                    {
                        Value = "1",
                        Type = LiteralType.INT,
                        ImagineryPosition = t1.GetPosition(),
                    };

                    Statement_Operator statementToAssign = new(new Token()
                    {
                        AbsolutePosition = t1.AbsolutePosition,
                        Content = "-",
                        Position = t1.Position,
                        TokenType = t1.TokenType,
                    }, leftStatement, literalOne);

                    return new Statement_Setter(new Token()
                    {
                        AbsolutePosition = t1.AbsolutePosition,
                        Content = "=",
                        Position = t1.Position,
                        TokenType = t1.TokenType,
                    }, leftStatement, statementToAssign);
                }

                if (ExpectOperator("=", out Token op))
                {
                    StatementWithReturnValue valueToAssign = ExpectExpression();
                    if (valueToAssign == null)
                    { throw new SyntaxException("Expected expression", op); }

                    return new Statement_Setter(op, leftStatement, valueToAssign);
                }

                currentTokenIndex = parseStart;
                return null;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="statement"></param>
            /// <param name="rightSidePrecedence"></param>
            /// <returns>
            /// <see langword="null"/> or <see cref="Statement_Operator"/>
            /// </returns>
            Statement_Operator FindRightmostStatement(Statement statement, int rightSidePrecedence)
            {
                if (statement is not Statement_Operator leftSide) return null;
                if (OperatorPrecedence(leftSide.Operator.Content) >= rightSidePrecedence) return null;
                if (leftSide.InsideBracelet) return null;

                Statement_Operator right = FindRightmostStatement(leftSide.Right, rightSidePrecedence);

                if (right == null) return leftSide;
                return right;
            }

            int OperatorPrecedence(string str)
            {
                if (operators.TryGetValue(str, out int precedence))
                { return precedence; }
                else throw new InternalException($"Precedence for operator {str} not found");
            }

            Statement_FunctionCall ExpectFunctionCall()
            {
                int startTokenIndex = currentTokenIndex;

                if (!ExpectIdentifier(out Token possibleFunctionName))
                { currentTokenIndex = startTokenIndex; return null; }

                if (possibleFunctionName == null)
                { currentTokenIndex = startTokenIndex; return null; }

                if (!ExpectOperator("("))
                { currentTokenIndex = startTokenIndex; return null; }

                possibleFunctionName.AnalysedType = TokenAnalysedType.BuiltinType;

                Statement_FunctionCall functionCall = new()
                {
                    Identifier = possibleFunctionName,
                };

                bool expectParameter = false;
                List<StatementWithReturnValue> parameters = new();

                int endlessSafe = 0;
                while (!ExpectOperator(")") || expectParameter)
                {
                    StatementWithReturnValue parameter = ExpectExpression();
                    if (parameter == null)
                    { throw new SyntaxException("Expected expression as parameter", functionCall.TotalPosition()); }

                    parameters.Add(parameter);

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
                functionCall.Parameters = parameters.ToArray();

                return functionCall;
            }

            /// <summary> return, break, continue, etc. </summary>
            Statement_KeywordCall ExpectKeywordCall(string name, bool canHaveParameters = false, bool needParameters = false)
            {
                int startTokenIndex = currentTokenIndex;

                if (!ExpectIdentifier(out Token possibleFunctionName))
                { currentTokenIndex = startTokenIndex; return null; }

                if (possibleFunctionName.Content != name)
                { currentTokenIndex = startTokenIndex; return null; }

                possibleFunctionName.AnalysedType = TokenAnalysedType.Statement;

                Statement_KeywordCall functionCall = new()
                {
                    Identifier = possibleFunctionName,
                };
                List<StatementWithReturnValue> parameters = new();

                if (canHaveParameters)
                {
                    StatementWithReturnValue parameter = ExpectExpression();
                    if (parameter == null && needParameters)
                    { throw new SyntaxException("Expected expression as parameter", functionCall.TotalPosition()); }

                    if (parameter != null)
                    { parameters.Add(parameter); }
                }
                functionCall.Parameters = parameters.ToArray();
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

                attributeT.AnalysedType = TokenAnalysedType.Attribute;

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

                attribute.Identifier = attributeT;
                return true;
            }

            FieldDefinition ExpectField()
            {
                int startTokenIndex = currentTokenIndex;

                if (ExpectIdentifier("private", out Token protectionToken))
                {

                }

                TypeInstance possibleType = ExpectType();
                if (possibleType == null)
                { currentTokenIndex = startTokenIndex; return null; }

                if (!ExpectIdentifier(out Token possibleVariableName))
                { currentTokenIndex = startTokenIndex; return null; }

                if (ExpectOperator("("))
                { currentTokenIndex = startTokenIndex; return null; }

                possibleVariableName.AnalysedType = TokenAnalysedType.None;

                FieldDefinition field = new()
                {
                    Identifier = possibleVariableName,
                    Type = possibleType,
                    ProtectionToken = protectionToken,
                };

                return field;
            }
            bool ExpectField(out FieldDefinition field)
            {
                field = ExpectField();
                return field != null;
            }

            void ExpectOneLiteral(out object value)
            {
                value = null;

                if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
                {
                    value = float.Parse(CurrentToken.Content.Replace("_", ""));

                    currentTokenIndex++;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    value = int.Parse(CurrentToken.Content.Replace("_", ""));

                    currentTokenIndex++;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_HEX)
                {
                    value = Convert.ToInt32(CurrentToken.Content, 16);

                    currentTokenIndex++;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_BIN)
                {
                    value = Convert.ToInt32(CurrentToken.Content.Replace("_", ""), 2);

                    currentTokenIndex++;
                }
                else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
                {
                    value = CurrentToken.Content;

                    currentTokenIndex++;
                }
            }

            #region Basic parsing

            bool ExpectIdentifier(out Token result) => ExpectIdentifier("", out result);
            bool ExpectIdentifier(string name, out Token result)
            {
                result = null;
                if (CurrentToken == null) return false;
                if (CurrentToken.TokenType != TokenType.IDENTIFIER) return false;
                if (name.Length > 0 && CurrentToken.Content != name) return false;

                result = CurrentToken;
                currentTokenIndex++;

                return true;
            }

            bool ExpectOperator(string name) => ExpectOperator(name, out _);
            bool ExpectOperator(string[] name, out Token result)
            {
                result = null;
                if (CurrentToken == null) return false;
                if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
                if (name.Contains(CurrentToken.Content) == false) return false;

                result = CurrentToken;
                currentTokenIndex++;

                return true;
            }
            bool ExpectOperator(string name, out Token result)
            {
                result = null;
                if (CurrentToken == null) return false;
                if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
                if (name.Length > 0 && CurrentToken.Content != name) return false;

                result = CurrentToken;
                currentTokenIndex++;

                return true;
            }

            TypeInstance ExpectType(bool allowVarKeyword = true, bool allowAnyKeyword = false)
            {
                int parseStart = currentTokenIndex;

                if (!ExpectIdentifier(out Token possibleType)) return null;

                possibleType.AnalysedType = TokenAnalysedType.Keyword;

                TypeInstance newType = null;

                if (!types.Contains(possibleType.Content))
                {
                    if (newType == null && possibleType.Content == "any")
                    {
                        if (allowAnyKeyword)
                        {
                            newType = new TypeInstance(possibleType);
                        }
                        else
                        {
                            Errors.Add(new Error($"Type '{possibleType.Content}' is not valid in the current context", possibleType));
                        }
                    }

                    if (newType == null && possibleType.Content == "var")
                    {
                        if (allowVarKeyword)
                        {
                            newType = new TypeInstance(possibleType);
                        }
                        else
                        {
                            Errors.Add(new Error($"Type '{possibleType.Content}' is not valid in the current context", possibleType));
                        }
                    }

                    if (newType == null)
                    {
                        if (TryGetStruct(possibleType.Content, out var s))
                        {
                            newType = new TypeInstance(possibleType);
                            newType.Identifier.AnalysedType = TokenAnalysedType.Struct;
                        }
                        else if (TryGetClass(possibleType.Content, out var c))
                        {
                            newType = new TypeInstance(possibleType);
                            newType.Identifier.AnalysedType = TokenAnalysedType.Class;
                        }
                        else
                        {
                            newType = new TypeInstance(possibleType);
                            // warning = new Warning($"Type '{possibleType.Content}' not found", possibleType);
                        }
                    }

                    if (newType == null)
                    { return null; }
                }
                else
                {
                    newType = new TypeInstance(possibleType);
                    newType.Identifier.AnalysedType = TokenAnalysedType.BuiltinType;
                }

                while (ExpectOperator("[", out var listToken0))
                {
                    if (ExpectOperator("]", out var listToken1))
                    {
                        // newType = new TypeToken(newType.Content, newType, newType);
                        Errors.Add(new Error($"Lists aren't supported as built-in feature", new Position(listToken0, listToken1)));
                    }
                    else
                    { currentTokenIndex = parseStart; return null; }
                }

                // tokens[currentTokenIndex - 1] = newType;

                return newType;
            }

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
        }
    }
}